using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessPolicies;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>
/// Corrected no-farm campaign을 실행하고 실제 cap-exhausted battle wall만 계측한다.
/// 각 wall의 최소 추가 power는 이후 battle에도 누적 적용해 full-playthrough wall 간격을 관찰한다.
/// </summary>
internal static class CampaignWallDeficitSimulation
{
    private const int SiteSafety = 16;
    private const int BattleNodeSafety = 64;

    internal static CampaignWallDeficitCampaignObservation Run(
        RuntimeCombatContentLookup lookup,
        int campaignIndex,
        int campaignSeed,
        string policyId,
        double searchMaximum,
        double tolerance,
        int adaptationRetryCap)
    {
        if (adaptationRetryCap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adaptationRetryCap));
        }

        var session = H100SessionDriver.CreateSession(
            lookup,
            $"campaign-wall-deficit-{campaignSeed:D10}");
        session.OverrideCampaignSeedForValidation(campaignSeed);
        var policy = HeadlessPolicyFactory.Create(policyId);
        if (policy is not IHeadlessRosterPolicy rosterPolicy)
        {
            throw new InvalidOperationException(
                $"{policyId} must expose the roster policy seams used by corrected no-farm measurement.");
        }

        var walls = new List<CampaignWallDeficitObservation>();
        var siteCount = 0;
        var completedBattleNodes = 0;
        var encounteredBattleCount = 0;
        var cumulativeLogPower = 0d;

        while (!session.Profile.CampaignProgress.StoryCleared && siteCount < SiteSafety)
        {
            H100SessionDriver.AdvanceToNextUnclearedSite(session);
            var siteCompleted = false;
            for (var siteAttempt = 0;
                 siteAttempt <= adaptationRetryCap && !siteCompleted;
                 siteAttempt++)
            {
                if (siteAttempt > 0)
                {
                    CampaignSignedDeficitSimulation.ApplyTownWindow(
                        session,
                        lookup,
                        rosterPolicy,
                        campaignSeed,
                        siteCount,
                        siteAttempt,
                        "adaptation");
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

                var siteAttemptBattleNodes = 0;
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

                    siteBattleCount++;
                    if (siteBattleCount > BattleNodeSafety)
                    {
                        throw new InvalidOperationException(
                            $"Wall-deficit site battle safety exhausted: {selectedNode.Id}");
                    }

                    if (policy is IHeadlessPrepPolicy prepPolicy
                        && session.TryBuildSelectedBattleState(out _, out var prepEncounter, out _, out _)
                        && (prepEncounter.Context.IsBoss
                            || CampaignSignedDeficitSimulation.IsElite(prepEncounter)))
                    {
                        var prepSeed = H100SessionDriver.DeriveSeed(
                            $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|{selectedNode.Id}|prep|attempt={siteAttempt}",
                            campaignSeed + encounteredBattleCount);
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
                            $"Wall-deficit battle build failed ({selectedNode.Id}): {buildError}");
                    }

                    var results = new SortedDictionary<double, BattleResult>();
                    BattleResult Evaluate(double additionalLogPower)
                    {
                        if (results.TryGetValue(additionalLogPower, out var cached))
                        {
                            return cached;
                        }

                        var totalLogPower = cumulativeLogPower + additionalLogPower;
                        var injectedSnapshot = CampaignPowerInjector.Apply(allySnapshot, totalLogPower);
                        if (!session.TryComposeBattleState(
                                injectedSnapshot,
                                encounter,
                                out var state,
                                out var composeError))
                        {
                            throw new InvalidOperationException(
                                $"Wall-deficit battle compose failed ({selectedNode.Id}): {composeError}");
                        }

                        var evaluated = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
                        results.Add(additionalLogPower, evaluated);
                        return evaluated;
                    }

                    var goldBefore = session.Profile.Currencies.Gold;
                    var echoBefore = session.Profile.Currencies.Echo;
                    var rewardLedgerBefore = session.Profile.RewardLedger.Count;
                    var result = Evaluate(0d);
                    encounteredBattleCount++;
                    var won = result.Winner == TeamSide.Ally;
                    if (!won && siteAttempt == adaptationRetryCap)
                    {
                        var search = CampaignWallDeficitSearch.FindMinimumWinningCorrection(
                            additional => Evaluate(additional).Winner == TeamSide.Ally,
                            searchMaximum,
                            tolerance);
                        if (search.RightCensored || !search.AdditionalLogDeficit.HasValue)
                        {
                            throw new InvalidOperationException(
                                $"Per-wall deficit exceeded search maximum {searchMaximum:R}: "
                                + $"{session.SelectedCampaignChapterId}/{session.SelectedCampaignSiteId}/{selectedNode.Id}");
                        }

                        if (search.MonotonicityViolated)
                        {
                            throw new InvalidOperationException(
                                $"Per-wall deficit was non-monotone across observed search points: "
                                + $"{session.SelectedCampaignChapterId}/{session.SelectedCampaignSiteId}/{selectedNode.Id}");
                        }

                        var additionalLogDeficit = search.AdditionalLogDeficit.Value;
                        result = Evaluate(additionalLogDeficit);
                        if (result.Winner != TeamSide.Ally)
                        {
                            throw new InvalidOperationException(
                                $"Per-wall winning correction did not win: {selectedNode.Id}");
                        }

                        var nodeOrdinal = completedBattleNodes + siteAttemptBattleNodes + 1;
                        var cumulativeBefore = cumulativeLogPower;
                        cumulativeLogPower += additionalLogDeficit;
                        walls.Add(new CampaignWallDeficitObservation(
                            walls.Count,
                            session.SelectedCampaignChapterId,
                            session.SelectedCampaignSiteId,
                            selectedNode.Id,
                            siteAttempt,
                            nodeOrdinal,
                            cumulativeBefore,
                            additionalLogDeficit,
                            cumulativeLogPower,
                            ToPowerPercent(additionalLogDeficit),
                            search.EvaluationCount,
                            0));
                        won = true;
                    }

                    session.MarkBattleResolved(
                        won,
                        result.StepCount,
                        result.Events.Count,
                        result.FinalUnits);
                    if (!won)
                    {
                        session.AbandonExpeditionRun();
                        CampaignSignedDeficitSimulation.RequireNoDefeatRewardMutation(
                            session,
                            selectedNode.Id,
                            goldBefore,
                            echoBefore,
                            rewardLedgerBefore);
                        siteLost = true;
                        break;
                    }

                    session.ResolveSelectedExpeditionNode();
                    siteAttemptBattleNodes++;
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
                if (!session.Profile.CampaignProgress.StoryCleared)
                {
                    CampaignSignedDeficitSimulation.ApplyTownWindow(
                        session,
                        lookup,
                        rosterPolicy,
                        campaignSeed,
                        siteCount,
                        siteAttempt,
                        "first-clear");
                }

                completedBattleNodes += siteAttemptBattleNodes;
                siteCompleted = true;
            }

            if (!siteCompleted)
            {
                throw new InvalidOperationException(
                    $"Wall-deficit intervention failed to complete site {session.SelectedCampaignSiteId}.");
            }

            siteCount++;
        }

        if (!session.Profile.CampaignProgress.StoryCleared)
        {
            throw new InvalidOperationException(
                $"Wall-deficit campaign did not terminate within SiteSafety={SiteSafety}.");
        }

        var completedWalls = walls.Select((wall, index) =>
        {
            var endpointOrdinal = index + 1 < walls.Count
                ? walls[index + 1].BattleNodeOrdinal
                : completedBattleNodes + 1;
            return wall with
            {
                NodesAdvancedAfterUnblock = endpointOrdinal - wall.BattleNodeOrdinal,
            };
        }).ToArray();
        if (completedWalls.Any(value => value.NodesAdvancedAfterUnblock <= 0))
        {
            throw new InvalidOperationException(
                "Wall progress must advance at least one canonical battle node.");
        }

        return new CampaignWallDeficitCampaignObservation(
            campaignIndex,
            campaignSeed,
            true,
            siteCount,
            completedBattleNodes,
            completedWalls.Length,
            cumulativeLogPower,
            completedWalls);
    }

    private static double ToPowerPercent(double logPower)
        => (Math.Exp(logPower) - 1d) * 100d;
}
