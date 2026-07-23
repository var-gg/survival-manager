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

        CampaignEquipmentUpgradePolicy.Apply(session, itemIndex);
        var baselineEquippedGradeSum = EquippedGradeSum(session, itemIndex);
        var effectiveEquipmentSlotCount = session.Profile.Heroes.Count * 3;
        var gradePowerKappa = ResolveGradePowerKappa(lookup);
        var revisits = new List<CampaignRevisitRewardObservation>();
        for (var revisitIndex = 1;
             revisitIndex <= CampaignRecoveryRewardPolicy.RewardedRevisitLimit + 1;
             revisitIndex++)
        {
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
                return BuildReentryObservation(
                    siteId,
                    false,
                    baselineEquippedGradeSum,
                    effectiveEquipmentSlotCount,
                    revisits);
            }

            CompleteReentryProbeSite(session, cell, config, revisitIndex);
            CampaignEquipmentUpgradePolicy.Apply(session, itemIndex);
            var equippedGradeSum = EquippedGradeSum(session, itemIndex);
            var cumulativeAverageGradeStep =
                (equippedGradeSum - baselineEquippedGradeSum) / (double)effectiveEquipmentSlotCount;
            revisits.Add(new CampaignRevisitRewardObservation(
                revisitIndex,
                LifetimeExperience(session.Profile.HeroProgressions) - experienceBefore,
                session.Profile.Currencies.Gold - goldBefore,
                session.Profile.Currencies.Echo - echoBefore,
                session.Profile.Inventory.Count - inventoryBefore,
                session.Profile.RewardLedger.Count - rewardLedgerBefore,
                session.Profile.UnlockedPermanentAugmentIds.Count - permanentBefore,
                equippedGradeSum,
                cumulativeAverageGradeStep,
                (Math.Exp(gradePowerKappa * cumulativeAverageGradeStep) - 1d) * 100d));
        }

        return BuildReentryObservation(
            siteId,
            true,
            baselineEquippedGradeSum,
            effectiveEquipmentSlotCount,
            revisits);
    }

    private static CampaignClearedSiteReentryObservation BuildReentryObservation(
        string siteId,
        bool canReenter,
        int baselineEquippedGradeSum,
        int effectiveEquipmentSlotCount,
        IReadOnlyList<CampaignRevisitRewardObservation> revisits)
    {
        var first = revisits.FirstOrDefault();
        var exhausted = revisits.FirstOrDefault(revisit =>
            revisit.RevisitIndex == CampaignRecoveryRewardPolicy.RewardedRevisitLimit + 1);
        var rewardsAgain = first != null && HasPersistentReward(first);
        var unboundedFarmClosed = exhausted != null && !HasPersistentReward(exhausted);
        return new CampaignClearedSiteReentryObservation(
            siteId,
            canReenter,
            rewardsAgain,
            baselineEquippedGradeSum,
            effectiveEquipmentSlotCount,
            first?.LifetimeExperienceDelta ?? 0,
            first?.GoldDelta ?? 0,
            first?.EchoDelta ?? 0,
            first?.InventoryDelta ?? 0,
            first?.RewardLedgerDelta ?? 0,
            first?.PermanentAugmentDelta ?? 0,
            unboundedFarmClosed,
            revisits);
    }

    private static int EquippedGradeSum(
        GameSessionState session,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex)
    {
        return session.Profile.Inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.EquippedHeroId))
            .Where(item => itemIndex.ContainsKey(item.ItemBaseId))
            .Sum(item => item.RolledRarityTier >= 0
                ? item.RolledRarityTier
                : (int)itemIndex[item.ItemBaseId].RarityTier);
    }

    private static double ResolveGradePowerKappa(RuntimeCombatContentLookup lookup)
    {
        if (!lookup.TryGetCombatSnapshot(out var content, out var error))
        {
            throw new InvalidOperationException($"reentry grade power content unavailable: {error}");
        }

        var values = (content.DropTables?.Values ?? Array.Empty<DropTableTemplate>())
            .Where(table => table.GradeProfiles is { Count: > 0 })
            .Select(table => (double)table.GradePowerKappa)
            .Distinct()
            .ToArray();
        if (values.Length != 1 || values[0] <= 0d)
        {
            throw new InvalidOperationException(
                $"reentry probe requires one positive grade-power kappa, got [{string.Join(",", values)}].");
        }

        return values[0];
    }

    private static bool HasPersistentReward(CampaignRevisitRewardObservation observation)
    {
        return observation.LifetimeExperienceDelta > 0
               || observation.GoldDelta > 0
               || observation.EchoDelta > 0
               || observation.InventoryDelta > 0
               || observation.RewardLedgerDelta > 0
               || observation.PermanentAugmentDelta > 0;
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
