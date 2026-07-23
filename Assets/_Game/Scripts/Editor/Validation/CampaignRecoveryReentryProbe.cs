using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

internal static partial class CampaignTwoArmSweepRunner
{
    private static CampaignClearedSiteReentryObservation ProbeClearedSiteReentry(
        RuntimeCombatContentLookup lookup,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex,
        CampaignBalanceSweepConfig config)
    {
        var cell = config.BuildGrid().First(value =>
            string.Equals(value.Squad.SquadId, "mixed", StringComparison.Ordinal)
            && string.Equals(value.BuildPower.QuantileId, "P80", StringComparison.Ordinal)
            && value.EnemyComposition.VariantIndex == 0);
        var session = H100SessionDriver.CreateSession(lookup, "campaign-recovery-reentry-probe");
        CampaignBalanceSweepRunner.AuthorSquad(
            session,
            lookup,
            "reentry-probe",
            cell.RosterArchetypeIds.ToArray(),
            itemIndex,
            redistributeStarterGear: false);
        ApplyBuildPower(session, lookup, itemIndex, cell.BuildPower);

        var siteId = session.SelectedCampaignSiteId;
        session.BeginNewExpedition();
        CompleteReentryProbeSite(session, cell, config, revisitIndex: 0);
        if (!session.Profile.CampaignProgress.ClearedSiteIds.Contains(siteId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"reentry probe failed to clear initial site '{siteId}'.");
        }

        var experienceBefore = LifetimeExperience(session.Profile.HeroProgressions);
        var goldBefore = session.Profile.Currencies.Gold;
        var echoBefore = session.Profile.Currencies.Echo;
        var inventoryBefore = session.Profile.Inventory.Count;
        var rewardLedgerBefore = session.Profile.RewardLedger.Count;
        var permanentBefore = session.Profile.UnlockedPermanentAugmentIds.Count;

        session.BeginNewExpedition();
        var canReenter = session.HasActiveExpeditionRun
                         && string.Equals(session.SelectedCampaignSiteId, siteId, StringComparison.Ordinal);
        if (!canReenter)
        {
            return new CampaignClearedSiteReentryObservation(
                siteId,
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        CompleteReentryProbeSite(session, cell, config, revisitIndex: 1);
        var experienceDelta = LifetimeExperience(session.Profile.HeroProgressions) - experienceBefore;
        var goldDelta = session.Profile.Currencies.Gold - goldBefore;
        var echoDelta = session.Profile.Currencies.Echo - echoBefore;
        var inventoryDelta = session.Profile.Inventory.Count - inventoryBefore;
        var rewardLedgerDelta = session.Profile.RewardLedger.Count - rewardLedgerBefore;
        var permanentDelta = session.Profile.UnlockedPermanentAugmentIds.Count - permanentBefore;
        var rewardsAgain = experienceDelta > 0
                           || goldDelta > 0
                           || echoDelta > 0
                           || inventoryDelta > 0
                           || rewardLedgerDelta > 0
                           || permanentDelta > 0;
        return new CampaignClearedSiteReentryObservation(
            siteId,
            true,
            rewardsAgain,
            experienceDelta,
            goldDelta,
            echoDelta,
            inventoryDelta,
            rewardLedgerDelta,
            permanentDelta);
    }

    private static void CompleteReentryProbeSite(
        GameSessionState session,
        CampaignBalanceGridCell cell,
        CampaignBalanceSweepConfig config,
        int revisitIndex)
    {
        var battleIndex = 0;
        while (true)
        {
            while (CampaignDefaultRouteNavigator.TryAdvanceIntermediateNonBattle(session))
            {
            }

            var node = session.GetSelectedExpeditionNode();
            if (node?.RequiresBattle != true)
            {
                break;
            }

            if (!session.TryBuildSelectedBattleState(
                    out _,
                    out var encounter,
                    out var allySnapshot,
                    out var buildError))
            {
                throw new InvalidOperationException(
                    $"reentry probe build failed at '{node.Id}': {buildError}");
            }

            var measuredEncounter = ProjectEncounter(encounter, cell.EnemyComposition);
            measuredEncounter = measuredEncounter with
            {
                Context = measuredEncounter.Context with
                {
                    BattleSeed = H100SessionDriver.DeriveSeed(
                        $"reentry-probe|{revisitIndex}|{node.Id}",
                        9000 + battleIndex),
                },
            };
            if (!session.TryComposeBattleState(
                    allySnapshot,
                    measuredEncounter,
                    out var state,
                    out var composeError))
            {
                throw new InvalidOperationException(
                    $"reentry probe compose failed at '{node.Id}': {composeError}");
            }

            var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
            if (result.Winner != TeamSide.Ally)
            {
                result = FindProgressionResult(
                             session,
                             allySnapshot,
                             measuredEncounter,
                             config.ProgressionRetrySeedCount)
                         ?? throw new InvalidOperationException(
                             $"reentry probe could not clear '{node.Id}' within the configured retry budget.");
            }

            session.MarkBattleResolved(
                true,
                result.StepCount,
                result.Events.Count,
                result.FinalUnits);
            if (session.PendingRewardChoices.Count > 0 && !session.ApplyRewardChoice(0))
            {
                throw new InvalidOperationException(
                    $"reentry probe could not apply the real reward choice at '{node.Id}'.");
            }

            session.ReturnToTownAfterReward();
            battleIndex++;
        }

        if (!session.ResolveSelectedNodeToRewardSettlement())
        {
            throw new InvalidOperationException("reentry probe could not resolve the site extract.");
        }

        if (session.PendingRewardChoices.Count > 0 && !session.ApplyRewardChoice(0))
        {
            throw new InvalidOperationException("reentry probe could not apply the extract reward choice.");
        }

        session.ReturnToTownAfterReward();
    }

    private static int LifetimeExperience(IEnumerable<HeroProgressionRecord> progressions)
    {
        var result = 0;
        foreach (var progression in progressions)
        {
            for (var level = 1; level < Math.Max(1, progression.Level); level++)
            {
                result += HeroProgressionCurve.ExperienceToNextLevel(level);
            }

            result += progression.Experience;
        }

        return result;
    }
}
