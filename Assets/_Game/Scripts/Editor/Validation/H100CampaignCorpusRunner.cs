using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>실 campaign/session progression을 no-cheat scripted policy로 실행해 캠페인 metrics를 만든다.</summary>
internal static class H100CampaignCorpusRunner
{
    private const int MaxBattleNodesPerSite = 64;

    internal sealed record Corpus(
        IReadOnlyList<BattleMetricRecord> Battles,
        IReadOnlyList<CampaignMetricRecord> Campaigns,
        IReadOnlyList<PlayerVisibleFactRecord> Facts,
        IReadOnlyList<PlayerVisibleDecisionRecord> Decisions,
        PlayerVisibleFactAuditResult FactAudit);

    public static Corpus Run(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        Action<string>? decisionLog = null,
        H100CampaignObservationHooks? observationHooks = null)
    {
        var battles = new List<BattleMetricRecord>();
        var campaigns = new List<CampaignMetricRecord>(settings.CampaignCount);
        var factLedger = new H100PlayerVisibleFactLedgerCollector(settings.RunId);
        for (var index = 0; index < settings.CampaignCount; index++)
        {
            if (observationHooks?.StopRequested?.Invoke() == true)
            {
                break;
            }

            RunOne(
                lookup,
                settings,
                targetBattleSeconds,
                index,
                battles,
                campaigns,
                factLedger,
                decisionLog,
                observationHooks);
        }

        return new Corpus(
            battles,
            campaigns,
            factLedger.Facts,
            factLedger.Decisions,
            PlayerVisibleFactLedgerAuditor.Audit(factLedger.Facts, factLedger.Decisions));
    }

    private static void RunOne(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        int campaignIndex,
        ICollection<BattleMetricRecord> allBattles,
        ICollection<CampaignMetricRecord> allCampaigns,
        H100PlayerVisibleFactLedgerCollector factLedger,
        Action<string>? decisionLog,
        H100CampaignObservationHooks? observationHooks)
    {
        var campaignId = $"campaign-{campaignIndex:D6}";
        var campaignSeed = H100SessionDriver.DeriveSeed("campaign", settings.SeedBase + campaignIndex);
        var campaignBattles = new List<BattleMetricRecord>();
        GameSessionState? session = null;
        var siteCount = 0;
        var decisionCount = 0;
        var terminalReason = "unknown";
        var crashCount = 0;
        var softlockCount = 0;
        var truncated = false;
        var defeated = false;
        var battleIndex = 0;

        try
        {
            var policy = HeadlessPolicyFactory.Create(settings.PolicyId);
            session = H100SessionDriver.CreateSession(lookup, settings.PairingProfileId(campaignId));
            while (!session.Profile.CampaignProgress.StoryCleared
                   && siteCount < settings.CampaignSiteSafety
                   && !defeated)
            {
                H100SessionDriver.AdvanceToNextUnclearedSite(session);
                var deploymentSeed = H100SessionDriver.DeriveSeed(
                    $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|deployment",
                    campaignSeed + siteCount);
                var deploymentObservation = factLedger.Observe(
                    campaignId,
                    campaignIndex,
                    siteCount,
                    decisionCount,
                    H100PolicyObservationBuilder.Build(session, lookup, deploymentSeed));
                var deploymentDecision = H100SessionDriver.ApplyPolicyDeployment(
                    session,
                    lookup,
                    policy,
                    deploymentSeed,
                    deploymentObservation,
                    decisionLog);
                factLedger.RecordDeployment(
                    campaignId,
                    campaignIndex,
                    siteCount,
                    decisionCount,
                    policy.Id,
                    deploymentDecision);
                decisionCount++;
                observationHooks?.SiteArrived?.Invoke(new H100SiteArrivalContext(
                    campaignId,
                    campaignIndex,
                    campaignSeed,
                    siteCount,
                    battleIndex,
                    deploymentSeed,
                    session,
                    deploymentDecision));
                session.BeginNewExpedition();
                var siteBattleCount = 0;
                while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
                {
                    siteBattleCount++;
                    if (siteBattleCount > MaxBattleNodesPerSite)
                    {
                        softlockCount++;
                        terminalReason = "battle-node-safety-exhausted";
                        session.AbandonExpeditionRun();
                        defeated = true;
                        break;
                    }

                    var replayGroupId = $"{campaignId}-battle-{battleIndex:D4}";
                    var battleId = $"{replayGroupId}-copy-00";
                    if (!session.TryBuildSelectedBattleState(out _, out var encounter, out var allySnapshot, out var buildError))
                    {
                        campaignBattles.Add(BattleMetricProjector.ProjectFailure(
                            settings.RunId, campaignId, battleId, replayGroupId, 0, "unavailable",
                            settings.PolicyId, campaignSeed, $"build:{buildError}"));
                        crashCount++;
                        terminalReason = "battle-build-failed";
                        session.AbandonExpeditionRun();
                        defeated = true;
                        break;
                    }

                    var battleSeed = H100SessionDriver.DeriveSeed(
                        encounter.Context.BattleContextHash,
                        campaignSeed + battleIndex);
                    var seededEncounter = encounter with { Context = encounter.Context with { BattleSeed = battleSeed } };
                    var scenarioId = H100SessionDriver.ScenarioId(encounter.Context);
                    if (!session.TryComposeBattleState(allySnapshot, seededEncounter, out var state, out var composeError))
                    {
                        campaignBattles.Add(BattleMetricProjector.ProjectFailure(
                            settings.RunId, campaignId, battleId, replayGroupId, 0, scenarioId,
                            settings.PolicyId, battleSeed, $"compose:{composeError}"));
                        crashCount++;
                        terminalReason = "battle-compose-failed";
                        session.AbandonExpeditionRun();
                        defeated = true;
                        break;
                    }

                    var highlightLedger = new BattleHighlightLedger();
                    var result = BattleResolver.Run(state, settings.MaxBattleSteps, highlightLedger.Record);
                    var metric = BattleMetricProjector.Project(
                        settings.RunId, campaignId, battleId, replayGroupId, 0, scenarioId,
                        settings.PolicyId, state, result, settings.MaxBattleSteps, targetBattleSeconds);
                    campaignBattles.Add(metric);
                    battleIndex++;

                    var finalUnits = result.FinalUnits;
                    var payoff = highlightLedger.BuildFormationPayoff(
                        actorId => finalUnits.FirstOrDefault(unit => string.Equals(unit.Id, actorId, StringComparison.Ordinal))?.Name ?? actorId);
                    var won = result.Winner == TeamSide.Ally;
                    session.MarkBattleResolved(won, result.StepCount, result.Events.Count, finalUnits, payoff);
                    if (!won)
                    {
                        session.AbandonExpeditionRun();
                        terminalReason = "battle-defeat";
                        defeated = true;
                        break;
                    }

                    session.ResolveSelectedExpeditionNode();
                }

                if (defeated)
                {
                    break;
                }

                session.ResolveSelectedNodeToRewardSettlement();
                if (session.PendingRewardChoices.Count > 0)
                {
                    var rewardSeed = H100SessionDriver.DeriveSeed(
                        $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|reward",
                        campaignSeed + siteCount);
                    observationHooks?.RewardOffered?.Invoke(new H100RewardOfferedContext(
                        campaignId,
                        campaignIndex,
                        campaignSeed,
                        siteCount,
                        battleIndex,
                        rewardSeed,
                        session));
                    var rewardObservation = factLedger.Observe(
                        campaignId,
                        campaignIndex,
                        siteCount,
                        decisionCount,
                        H100PolicyObservationBuilder.Build(session, lookup, rewardSeed));
                    var rewardDecision = H100SessionDriver.ApplyPolicyReward(
                        session,
                        lookup,
                        policy,
                        rewardSeed,
                        rewardObservation,
                        decisionLog);
                    factLedger.RecordReward(
                        campaignId,
                        campaignIndex,
                        siteCount,
                        decisionCount,
                        policy.Id,
                        rewardDecision);
                    decisionCount++;
                    observationHooks?.RewardChosen?.Invoke(new H100RewardChosenContext(
                        campaignId,
                        campaignIndex,
                        campaignSeed,
                        siteCount,
                        battleIndex,
                        session,
                        rewardDecision));
                }

                session.ReturnToTownAfterReward();
                siteCount++;
            }

            if (session.Profile.CampaignProgress.StoryCleared)
            {
                terminalReason = "story-complete";
            }
            else if (!defeated && siteCount >= settings.CampaignSiteSafety)
            {
                terminalReason = "site-safety-exhausted";
                truncated = true;
            }
        }
        catch (Exception exception) when (exception is not HeadlessPolicyEvidenceException
                                          && exception is not PlayerVisibleProvenanceException)
        {
            terminalReason = $"exception:{exception.GetType().Name}";
            crashCount++;
        }

        foreach (var battle in campaignBattles)
        {
            allBattles.Add(battle);
        }

        var familyCounts = MetricCount.Normalize(
            campaignBattles.Where(record => !string.IsNullOrWhiteSpace(record.BuildFamilyId))
                .GroupBy(record => record.BuildFamilyId, StringComparer.Ordinal)
                .Select(group => new MetricCount(group.Key, group.Count())));
        var macroFamily = familyCounts
            .OrderByDescending(value => value.Count)
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .Select(value => value.Id)
            .FirstOrDefault() ?? string.Empty;
        var completed = session?.Profile.CampaignProgress.StoryCleared == true;
        allCampaigns.Add(new CampaignMetricRecord
        {
            RunId = settings.RunId,
            CampaignId = campaignId,
            PolicyId = settings.PolicyId,
            Seed = campaignSeed,
            Completed = completed,
            Truncated = truncated,
            TerminalReason = terminalReason,
            SiteCount = siteCount,
            BattleCount = campaignBattles.Count,
            WinCount = campaignBattles.Count(record => record.WinnerSide == "ally"),
            LossCount = campaignBattles.Count(record => record.WinnerSide == "enemy"),
            TimeoutCount = campaignBattles.Count(record => record.Timeout),
            StompCount = campaignBattles.Count(record => record.Stomp),
            ForcedTimeoutCount = campaignBattles.Count(record => record.Timeout),
            TotalBattleSeconds = campaignBattles.Sum(record => record.DurationSeconds),
            DecisionCount = decisionCount,
            DecisionMetricsAvailable = false,
            ImportantDecisionCount = 0,
            NearBestAlternativeDecisionCount = 0,
            HighLeverageDecisionCount = 0,
            MacroFamilyId = macroFamily,
            BuildFamilySelectionCounts = familyCounts,
            CrashCount = crashCount,
            SoftlockCount = softlockCount,
            NonFiniteStateCount = campaignBattles.Count(record => record.ContainsNonFinite),
            IllegalNegativeStateCount = campaignBattles.Count(record => record.IllegalNegativeState),
            NonTerminatingBattleCount = campaignBattles.Count(record => record.NonTerminating),
            ReplayManifestHash = ReplayHash.ComputeManifest(campaignBattles.Select(record => record.ReplayHash)),
        });
    }
}
