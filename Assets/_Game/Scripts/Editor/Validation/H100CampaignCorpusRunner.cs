using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.SealedLlmBridge;
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
        IReadOnlyList<IntentTraceRecord> IntentTraces,
        PlayerVisibleFactAuditResult FactAudit);

    public static Corpus Run(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        Action<string>? decisionLog = null,
        H100CampaignObservationHooks? observationHooks = null,
        Func<int, IHeadlessPolicy>? policyFactory = null,
        H100PlayerVisibleFactLedgerCollector? externalFactLedger = null)
    {
        var battles = new List<BattleMetricRecord>();
        var campaigns = new List<CampaignMetricRecord>(settings.CampaignCount);
        var factLedger = externalFactLedger ?? new H100PlayerVisibleFactLedgerCollector(settings.RunId);
        var intentTrace = new H100IntentTraceCollector(settings.RunId);
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
                intentTrace,
                decisionLog,
                observationHooks,
                policyFactory);
        }

        return new Corpus(
            battles,
            campaigns,
            factLedger.Facts,
            factLedger.Decisions,
            intentTrace.Records,
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
        H100IntentTraceCollector intentTrace,
        Action<string>? decisionLog,
        H100CampaignObservationHooks? observationHooks,
        Func<int, IHeadlessPolicy>? policyFactory)
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
            var policy = policyFactory?.Invoke(campaignIndex) ?? HeadlessPolicyFactory.Create(settings.PolicyId);
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
                    H100PolicyObservationBuilder.Build(
                        session,
                        lookup,
                        deploymentSeed,
                        includeTownRoster: policy is IHeadlessRosterPolicy));
                observationHooks?.DeploymentOffered?.Invoke(new H100DeploymentOfferedContext(
                    campaignId,
                    campaignIndex,
                    campaignSeed,
                    siteCount,
                    battleIndex,
                    decisionCount,
                    deploymentSeed,
                    session,
                    deploymentObservation));
                var deploymentDecision = H100SessionDriver.ApplyPolicyDeployment(
                    session,
                    lookup,
                    policy,
                    deploymentSeed,
                    deploymentObservation,
                    decisionLog,
                    decisionIndex: decisionCount,
                    decisionApplied: observationHooks?.DecisionApplied);
                factLedger.RecordDeployment(
                    campaignId,
                    campaignIndex,
                    siteCount,
                    decisionCount,
                    policy.Id,
                    deploymentDecision);
                intentTrace.Record(
                    campaignId,
                    campaignIndex,
                    siteCount,
                    decisionCount,
                    policy);
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

                    if (policy is IHeadlessPrepPolicy prepPolicy
                        && session.TryBuildSelectedBattleState(out _, out var prepEncounter, out _, out _)
                        && (prepEncounter.Context.IsBoss || IsElite(prepEncounter)))
                    {
                        var prepSeed = H100SessionDriver.DeriveSeed(
                            $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|{session.GetSelectedExpeditionNode()!.Id}|prep",
                            campaignSeed + battleIndex);
                        var prepObservation = factLedger.Observe(
                            campaignId,
                            campaignIndex,
                            siteCount,
                            decisionCount,
                            H100PolicyObservationBuilder.Build(
                                session,
                                lookup,
                                prepSeed,
                                includeTownRoster: true));
                        observationHooks?.PrepOffered?.Invoke(new H100PrepOfferedContext(
                            campaignId,
                            campaignIndex,
                            campaignSeed,
                            siteCount,
                            battleIndex,
                            decisionCount,
                            prepSeed,
                            session,
                            prepObservation));
                        var prepDecision = H100SessionDriver.ApplyPolicyPrep(
                            session,
                            policy,
                            prepPolicy,
                            prepSeed,
                            prepObservation,
                            decisionLog,
                            decisionCount,
                            observationHooks?.DecisionApplied);
                        factLedger.RecordPrep(
                            campaignId,
                            campaignIndex,
                            siteCount,
                            decisionCount,
                            policy.Id,
                            prepDecision);
                        intentTrace.Record(campaignId, campaignIndex, siteCount, decisionCount, policy);
                        decisionCount++;
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
                    observationHooks?.BattleCompleted?.Invoke(new H100BattleCompletedContext(
                        campaignId,
                        campaignIndex,
                        siteCount,
                        battleIndex,
                        result,
                        metric));
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
                    var rewardObservation = factLedger.Observe(
                        campaignId,
                        campaignIndex,
                        siteCount,
                        decisionCount,
                        H100PolicyObservationBuilder.Build(
                            session,
                            lookup,
                            rewardSeed,
                            includeTownRoster: policy is IHeadlessRosterPolicy));
                    observationHooks?.RewardOffered?.Invoke(new H100RewardOfferedContext(
                        campaignId,
                        campaignIndex,
                        campaignSeed,
                        siteCount,
                        battleIndex,
                        decisionCount,
                        rewardSeed,
                        session,
                        rewardObservation));
                    var rewardDecision = H100SessionDriver.ApplyPolicyReward(
                        session,
                        lookup,
                        policy,
                        rewardSeed,
                        rewardObservation,
                        decisionLog,
                        decisionIndex: decisionCount,
                        decisionApplied: observationHooks?.DecisionApplied);
                    factLedger.RecordReward(
                        campaignId,
                        campaignIndex,
                        siteCount,
                        decisionCount,
                        policy.Id,
                        rewardDecision);
                    intentTrace.Record(
                        campaignId,
                        campaignIndex,
                        siteCount,
                        decisionCount,
                        policy);
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
                if (!session.Profile.CampaignProgress.StoryCleared && policy is IHeadlessRosterPolicy rosterPolicy)
                {
                    decisionCount = H100RosterPolicyWindowRunner.Run(
                        session,
                        lookup,
                        policy,
                        rosterPolicy,
                        campaignId,
                        campaignIndex,
                        campaignSeed,
                        siteCount,
                        battleIndex,
                        decisionCount,
                        factLedger,
                        intentTrace,
                        decisionLog,
                        observationHooks);
                }

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
        catch (SealedLlmTerminalFailureException)
        {
            throw;
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

    private static bool IsElite(ResolvedEncounterContext encounter)
        => encounter.Context.EncounterId.Contains("_elite_", StringComparison.Ordinal)
           || encounter.Context.RewardSourceId.Contains("elite", StringComparison.OrdinalIgnoreCase);
}
