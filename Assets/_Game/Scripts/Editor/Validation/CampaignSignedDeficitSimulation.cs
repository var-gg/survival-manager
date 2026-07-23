using System;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>
/// Production GameSessionState progression을 반복 방문 없이 실행하는 signed-deficit 실측 드라이버.
/// 첫 클리어 loot/reward와 사이트 사이 recruit/passive/refit은 정상 정책 경로를 그대로 사용한다.
/// </summary>
internal static class CampaignSignedDeficitSimulation
{
    private const int SiteSafety = 16;
    private const int BattleNodeSafety = 64;

    internal static CampaignCompletionObservation Run(
        RuntimeCombatContentLookup lookup,
        int campaignIndex,
        int campaignSeed,
        string policyId,
        double logPower,
        int adaptationRetryCap)
    {
        if (adaptationRetryCap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adaptationRetryCap));
        }

        var session = H100SessionDriver.CreateSession(lookup, $"campaign-signed-deficit-{campaignSeed:D10}");
        session.OverrideCampaignSeedForValidation(campaignSeed);
        var policy = HeadlessPolicyFactory.Create(policyId);
        var rosterPolicy = policy as IHeadlessRosterPolicy;
        var siteCount = 0;
        var battleCount = 0;
        var adaptationRetriesUsed = 0;

        while (!session.Profile.CampaignProgress.StoryCleared && siteCount < SiteSafety)
        {
            H100SessionDriver.AdvanceToNextUnclearedSite(session);
            var siteCompleted = false;
            var terminalNodeId = session.SelectedCampaignSiteId;
            for (var siteAttempt = 0;
                 siteAttempt <= adaptationRetryCap && !siteCompleted;
                 siteAttempt++)
            {
                if (siteAttempt > 0)
                {
                    adaptationRetriesUsed++;
                    if (rosterPolicy != null)
                    {
                        ApplyTownWindow(
                            session,
                            lookup,
                            rosterPolicy,
                            campaignSeed,
                            siteCount,
                            siteAttempt,
                            "adaptation");
                    }
                }

                var deploymentSeed = H100SessionDriver.DeriveSeed(
                    $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|deployment|attempt={siteAttempt}",
                    campaignSeed + siteCount);
                H100SessionDriver.ApplyPolicyDeployment(
                    session,
                    lookup,
                    policy,
                    deploymentSeed);
                session.BeginNewExpedition();

                var siteBattleCount = 0;
                var siteLost = false;
                while (true)
                {
                    while (CampaignDefaultRouteNavigator.TryAdvanceIntermediateNonBattle(session))
                    {
                    }

                    var selectedNode = session.GetSelectedExpeditionNode();
                    if (selectedNode?.RequiresBattle != true)
                    {
                        break;
                    }

                    terminalNodeId = selectedNode.Id;
                    siteBattleCount++;
                    if (siteBattleCount > BattleNodeSafety)
                    {
                        throw new InvalidOperationException(
                            $"Signed-deficit site battle safety exhausted: {selectedNode.Id}");
                    }

                    if (policy is IHeadlessPrepPolicy prepPolicy
                        && session.TryBuildSelectedBattleState(out _, out var prepEncounter, out _, out _)
                        && (prepEncounter.Context.IsBoss || IsElite(prepEncounter)))
                    {
                        var prepSeed = H100SessionDriver.DeriveSeed(
                            $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|{selectedNode.Id}|prep|attempt={siteAttempt}",
                            campaignSeed + battleCount);
                        H100SessionDriver.ApplyPolicyPrep(
                            session,
                            policy,
                            prepPolicy,
                            prepSeed,
                            H100PolicyObservationBuilder.Build(
                                session,
                                lookup,
                                prepSeed,
                                includeTownRoster: true));
                    }

                    if (!session.TryBuildSelectedBattleState(
                            out _,
                            out var encounter,
                            out var allySnapshot,
                            out var buildError))
                    {
                        throw new InvalidOperationException(
                            $"Signed-deficit battle build failed ({selectedNode.Id}): {buildError}");
                    }

                    var injectedSnapshot = CampaignPowerInjector.Apply(allySnapshot, logPower);
                    if (!session.TryComposeBattleState(
                            injectedSnapshot,
                            encounter,
                            out var state,
                            out var composeError))
                    {
                        throw new InvalidOperationException(
                            $"Signed-deficit battle compose failed ({selectedNode.Id}): {composeError}");
                    }

                    var goldBefore = session.Profile.Currencies.Gold;
                    var echoBefore = session.Profile.Currencies.Echo;
                    var rewardLedgerBefore = session.Profile.RewardLedger.Count;
                    var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
                    battleCount++;
                    var won = result.Winner == TeamSide.Ally;
                    session.MarkBattleResolved(
                        won,
                        result.StepCount,
                        result.Events.Count,
                        result.FinalUnits);
                    if (!won)
                    {
                        session.AbandonExpeditionRun();
                        RequireNoDefeatRewardMutation(
                            session,
                            selectedNode.Id,
                            goldBefore,
                            echoBefore,
                            rewardLedgerBefore);
                        siteLost = true;
                        break;
                    }

                    session.ResolveSelectedExpeditionNode();
                }

                if (siteLost)
                {
                    continue;
                }

                session.ResolveSelectedNodeToRewardSettlement();
                if (session.PendingRewardChoices.Count > 0)
                {
                    var rewardSeed = H100SessionDriver.DeriveSeed(
                        $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|first-clear-reward",
                        campaignSeed + siteCount);
                    H100SessionDriver.ApplyPolicyReward(
                        session,
                        lookup,
                        policy,
                        rewardSeed);
                }

                session.ReturnToTownAfterReward();
                if (!session.Profile.CampaignProgress.StoryCleared
                    && rosterPolicy != null)
                {
                    ApplyTownWindow(
                        session,
                        lookup,
                        rosterPolicy,
                        campaignSeed,
                        siteCount,
                        siteAttempt,
                        "first-clear");
                }

                siteCompleted = true;
            }

            if (!siteCompleted)
            {
                return new CampaignCompletionObservation(
                    false,
                    terminalNodeId,
                    battleCount,
                    siteCount,
                    adaptationRetriesUsed);
            }

            siteCount++;
        }

        if (!session.Profile.CampaignProgress.StoryCleared)
        {
            throw new InvalidOperationException(
                $"Signed-deficit campaign did not terminate within SiteSafety={SiteSafety}.");
        }

        return new CampaignCompletionObservation(
            true,
            "story-complete",
            battleCount,
            siteCount,
            adaptationRetriesUsed);
    }

    internal static void ApplyTownWindow(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessRosterPolicy rosterPolicy,
        int campaignSeed,
        int siteIndex,
        int siteAttempt,
        string phase)
    {
        var recruitSeed = TownSeed(session, campaignSeed, siteIndex, siteAttempt, phase, "recruit");
        H100SessionDriver.ApplyPolicyRecruit(
            session,
            rosterPolicy,
            H100RosterPolicyObservationBuilder.Build(session, lookup, recruitSeed));

        var passiveSeed = TownSeed(session, campaignSeed, siteIndex, siteAttempt, phase, "level_node");
        H100SessionDriver.ApplyPolicyPassive(
            session,
            rosterPolicy,
            H100RosterPolicyObservationBuilder.Build(session, lookup, passiveSeed));

        var refitSeed = TownSeed(session, campaignSeed, siteIndex, siteAttempt, phase, "refit");
        H100SessionDriver.ApplyPolicyRefit(
            session,
            rosterPolicy,
            H100RosterPolicyObservationBuilder.Build(session, lookup, refitSeed));
    }

    private static int TownSeed(
        GameSessionState session,
        int campaignSeed,
        int siteIndex,
        int siteAttempt,
        string phase,
        string kind)
        => H100SessionDriver.DeriveSeed(
            $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|town|{phase}|attempt={siteAttempt}|{kind}",
            campaignSeed + siteIndex);

    internal static void RequireNoDefeatRewardMutation(
        GameSessionState session,
        string nodeId,
        int goldBefore,
        int echoBefore,
        int rewardLedgerBefore)
    {
        if (session.Profile.Currencies.Gold != goldBefore
            || session.Profile.Currencies.Echo != echoBefore
            || session.Profile.RewardLedger.Count != rewardLedgerBefore
            || session.PendingRewardChoices.Count != 0)
        {
            throw new InvalidOperationException(
                $"Signed-deficit defeat at '{nodeId}' mutated reward resources; "
                + "the corrected no-farm scope permits first-clear resources only.");
        }
    }

    internal static bool IsElite(ResolvedEncounterContext encounter)
        => encounter.Context.EncounterId.Contains("_elite_", StringComparison.Ordinal)
           || encounter.Context.RewardSourceId.Contains("elite", StringComparison.OrdinalIgnoreCase);
}
