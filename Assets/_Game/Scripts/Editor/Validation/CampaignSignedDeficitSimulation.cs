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
        double logPower)
    {
        var campaignId = $"signed-deficit-{campaignIndex:D6}";
        var session = H100SessionDriver.CreateSession(lookup, $"campaign-signed-deficit-{campaignSeed:D10}");
        session.OverrideCampaignSeedForValidation(campaignSeed);
        var policy = HeadlessPolicyFactory.Create(policyId);
        var siteCount = 0;
        var battleCount = 0;

        while (!session.Profile.CampaignProgress.StoryCleared && siteCount < SiteSafety)
        {
            H100SessionDriver.AdvanceToNextUnclearedSite(session);
            var deploymentSeed = H100SessionDriver.DeriveSeed(
                $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|deployment",
                campaignSeed + siteCount);
            H100SessionDriver.ApplyPolicyDeployment(
                session,
                lookup,
                policy,
                deploymentSeed);
            session.BeginNewExpedition();

            var siteBattleCount = 0;
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
                        $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|{selectedNode.Id}|prep",
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
                    return new CampaignCompletionObservation(
                        false,
                        selectedNode.Id,
                        battleCount,
                        siteCount);
                }

                session.ResolveSelectedExpeditionNode();
            }

            session.ResolveSelectedNodeToRewardSettlement();
            if (session.PendingRewardChoices.Count > 0)
            {
                var rewardSeed = H100SessionDriver.DeriveSeed(
                    $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|reward",
                    campaignSeed + siteCount);
                H100SessionDriver.ApplyPolicyReward(
                    session,
                    lookup,
                    policy,
                    rewardSeed);
            }

            session.ReturnToTownAfterReward();
            if (!session.Profile.CampaignProgress.StoryCleared
                && policy is IHeadlessRosterPolicy rosterPolicy)
            {
                ApplyTownWindow(
                    session,
                    lookup,
                    rosterPolicy,
                    campaignSeed,
                    siteCount);
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
            siteCount);
    }

    private static void ApplyTownWindow(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessRosterPolicy rosterPolicy,
        int campaignSeed,
        int siteIndex)
    {
        var recruitSeed = TownSeed(session, campaignSeed, siteIndex, "recruit");
        H100SessionDriver.ApplyPolicyRecruit(
            session,
            rosterPolicy,
            H100RosterPolicyObservationBuilder.Build(session, lookup, recruitSeed));

        var passiveSeed = TownSeed(session, campaignSeed, siteIndex, "level_node");
        H100SessionDriver.ApplyPolicyPassive(
            session,
            rosterPolicy,
            H100RosterPolicyObservationBuilder.Build(session, lookup, passiveSeed));

        var refitSeed = TownSeed(session, campaignSeed, siteIndex, "refit");
        H100SessionDriver.ApplyPolicyRefit(
            session,
            rosterPolicy,
            H100RosterPolicyObservationBuilder.Build(session, lookup, refitSeed));
    }

    private static int TownSeed(
        GameSessionState session,
        int campaignSeed,
        int siteIndex,
        string kind)
        => H100SessionDriver.DeriveSeed(
            $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|town|{kind}",
            campaignSeed + siteIndex);

    private static bool IsElite(ResolvedEncounterContext encounter)
        => encounter.Context.EncounterId.Contains("_elite_", StringComparison.Ordinal)
           || encounter.Context.RewardSourceId.Contains("elite", StringComparison.OrdinalIgnoreCase);
}
