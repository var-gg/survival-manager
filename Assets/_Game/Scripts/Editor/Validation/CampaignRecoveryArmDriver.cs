using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Editor.Validation;

internal static partial class CampaignTwoArmSweepRunner
{
    private static CampaignRecoveryPairObservation RunRecoveryPair(
        RuntimeCombatContentLookup lookup,
        CampaignBalanceGridCell cell,
        CampaignRecoveryArrival arrival,
        int attemptCap)
    {
        var armA = RunRecoveryArm(
            lookup,
            cell,
            arrival,
            attemptCap,
            applyRecovery: true);
        var armB = RunRecoveryArm(
            lookup,
            cell,
            arrival,
            attemptCap,
            applyRecovery: false);
        return new CampaignRecoveryPairObservation(cell.CellId, armA, armB);
    }

    private static CampaignRecoveryArmObservation RunRecoveryArm(
        RuntimeCombatContentLookup lookup,
        CampaignBalanceGridCell cell,
        CampaignRecoveryArrival arrival,
        int attemptCap,
        bool applyRecovery)
    {
        var session = H100SessionDriver.CreateSession(
            lookup,
            H100ProfileSnapshotCodec.Restore(arrival.ProfileSnapshot));
        var policy = HeadlessPolicyFactory.Create(HeadlessPolicyFactory.PreviewGroundedConceptId);
        if (policy is not IHeadlessRosterPolicy rosterPolicy || policy is not IHeadlessPrepPolicy prepPolicy)
        {
            throw new InvalidOperationException(
                $"{HeadlessPolicyFactory.PreviewGroundedConceptId} must expose roster and prep policy seams.");
        }

        var attempts = new List<CampaignRecoveryAttemptObservation>(attemptCap);
        var mutations = new CampaignRecoveryMutationCounter();
        var cleared = false;
        for (var attempt = 1; attempt <= attemptCap; attempt++)
        {
            if (attempt > 1)
            {
                if (applyRecovery)
                {
                    ApplyRecoveryDeployment(session, lookup, policy, cell, attempt);
                }

                session.BeginNewExpedition();
            }

            var attemptObservation = RunRecoveryAttempt(
                session,
                lookup,
                policy,
                rosterPolicy,
                prepPolicy,
                cell,
                arrival.Target,
                attempt,
                applyRecovery,
                mutations);
            attempts.Add(attemptObservation);
            if (attemptObservation.TargetWon)
            {
                cleared = true;
                break;
            }
        }

        return new CampaignRecoveryArmObservation(
            applyRecovery ? "A_real_recovery" : "B_control_no_recovery",
            cleared,
            cleared ? attempts.Count : attemptCap + 1,
            attempts,
            mutations.ToObservation());
    }

    private static CampaignRecoveryAttemptObservation RunRecoveryAttempt(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        IHeadlessRosterPolicy rosterPolicy,
        IHeadlessPrepPolicy prepPolicy,
        CampaignBalanceGridCell cell,
        CampaignRecoveryTarget target,
        int attempt,
        bool applyRecovery,
        CampaignRecoveryMutationCounter mutations)
    {
        var settlements = new List<CampaignRecoverySettlementObservation>();
        CampaignRecoveryPowerObservation? runEntryPower = null;
        CampaignRecoveryPowerObservation? targetPower = null;

        while (true)
        {
            while (CampaignDefaultRouteNavigator.TryAdvanceIntermediateNonBattle(session))
            {
            }

            var node = session.GetSelectedExpeditionNode();
            if (node?.RequiresBattle != true)
            {
                throw new InvalidOperationException(
                    $"recovery target was not reached before route end: target={target.NodeId} "
                    + $"cell={cell.CellId} attempt={attempt}");
            }

            if (!session.TryBuildSelectedBattleState(
                    out _,
                    out var encounter,
                    out var allySnapshot,
                    out var buildError))
            {
                throw new InvalidOperationException(
                    $"recovery battle build failed: target={target.NodeId} node={node.Id} "
                    + $"cell={cell.CellId} attempt={attempt}: {buildError}");
            }

            if (applyRecovery
                && attempt > 1
                && (encounter.Context.IsBoss || IsElite(encounter)))
            {
                var prepSeed = RecoveryDecisionSeed(
                    cell.CellId,
                    target.NodeId,
                    node.Id,
                    attempt,
                    "prep");
                var prepObservation = H100PolicyObservationBuilder.Build(
                        session,
                        lookup,
                        prepSeed,
                        includeTownRoster: true)
                    .WithEnemyPreview(ProjectPreview(
                        H100PolicyObservationBuilder.Build(
                            session,
                            lookup,
                            prepSeed,
                            includeTownRoster: true).EnemyPreview,
                        ProjectEncounter(encounter, cell.EnemyComposition).Enemies));
                var prep = H100SessionDriver.ApplyPolicyPrep(
                    session,
                    policy,
                    prepPolicy,
                    prepSeed,
                    prepObservation);
                mutations.PrepEquipmentAssignments += prep.EquipmentAssignments.Count;

                if (!session.TryBuildSelectedBattleState(
                        out _,
                        out encounter,
                        out allySnapshot,
                        out buildError))
                {
                    throw new InvalidOperationException(
                        $"recovery post-prep build failed: target={target.NodeId} node={node.Id} "
                        + $"cell={cell.CellId} attempt={attempt}: {buildError}");
                }
            }

            runEntryPower ??= MeasurePower(allySnapshot, session);
            var isTarget = string.Equals(
                encounter.Context.EncounterId,
                target.NodeId,
                StringComparison.Ordinal);
            if (isTarget)
            {
                targetPower = MeasurePower(allySnapshot, session);
            }

            var measuredEncounter = ProjectEncounter(encounter, cell.EnemyComposition);
            var battleSeed = attempt == 1 && isTarget
                ? measuredEncounter.Context.BattleSeed
                : RecoveryBattleSeed(cell.CellId, target.NodeId, node.Id, attempt);
            measuredEncounter = measuredEncounter with
            {
                Context = measuredEncounter.Context with { BattleSeed = battleSeed },
            };
            if (!session.TryComposeBattleState(
                    allySnapshot,
                    measuredEncounter,
                    out var state,
                    out var composeError))
            {
                throw new InvalidOperationException(
                    $"recovery battle compose failed: target={target.NodeId} node={node.Id} "
                    + $"cell={cell.CellId} attempt={attempt}: {composeError}");
            }

            var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
            var won = result.Winner == TeamSide.Ally;
            if (isTarget && won)
            {
                return new CampaignRecoveryAttemptObservation(
                    attempt,
                    true,
                    true,
                    node.Id,
                    battleSeed,
                    runEntryPower,
                    targetPower,
                    settlements);
            }

            var settlement = SettleRecoveryBattle(
                session,
                lookup,
                policy,
                rosterPolicy,
                cell,
                target,
                node.Id,
                attempt,
                won,
                result,
                applyRecovery,
                mutations);
            settlements.Add(settlement);
            if (!won)
            {
                return new CampaignRecoveryAttemptObservation(
                    attempt,
                    isTarget,
                    false,
                    node.Id,
                    battleSeed,
                    runEntryPower,
                    targetPower,
                    settlements);
            }
        }
    }

    private static CampaignRecoverySettlementObservation SettleRecoveryBattle(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        IHeadlessRosterPolicy rosterPolicy,
        CampaignBalanceGridCell cell,
        CampaignRecoveryTarget target,
        string nodeId,
        int attempt,
        bool victory,
        BattleResult result,
        bool applyRecovery,
        CampaignRecoveryMutationCounter mutations)
    {
        session.MarkBattleResolved(
            victory,
            result.StepCount,
            result.Events.Count,
            applyRecovery ? result.FinalUnits : null);
        var goldBefore = session.Profile.Currencies.Gold;
        var echoBefore = session.Profile.Currencies.Echo;
        var permanentBefore = session.Profile.UnlockedPermanentAugmentIds.Count;
        var choiceKind = string.Empty;
        var augmentChoice = 0;
        if (applyRecovery && session.PendingRewardChoices.Count > 0)
        {
            var rewardSeed = RecoveryDecisionSeed(
                cell.CellId,
                target.NodeId,
                nodeId,
                attempt,
                victory ? "victory-reward" : "defeat-reward");
            var observation = H100PolicyObservationBuilder.Build(
                session,
                lookup,
                rewardSeed,
                includeTownRoster: true);
            var decision = H100SessionDriver.ApplyPolicyReward(
                session,
                lookup,
                policy,
                rewardSeed,
                observation);
            if (decision.OptionIndex >= 0 && decision.OptionIndex < observation.RewardOptions.Count)
            {
                var selectedKind = observation.RewardOptions[decision.OptionIndex].Kind;
                choiceKind = selectedKind.ToString();
                augmentChoice = selectedKind == HeadlessRewardKind.TemporaryAugment ? 1 : 0;
            }
        }

        session.ReturnToTownAfterReward();
        var runTerminated = !session.HasActiveExpeditionRun && session.ActiveRun == null;
        var goldDelta = session.Profile.Currencies.Gold - goldBefore;
        var echoDelta = session.Profile.Currencies.Echo - echoBefore;
        var permanentDelta = Math.Max(
            augmentChoice,
            session.Profile.UnlockedPermanentAugmentIds.Count - permanentBefore);
        var recruitApplied = 0;
        var passiveApplied = 0;
        var refitApplied = 0;
        var townDecisionsDriven = false;
        if (!victory && applyRecovery)
        {
            mutations.DefeatSettlements++;
            var decisions = ApplyRecoveryTownDecisions(
                session,
                lookup,
                rosterPolicy,
                cell,
                target,
                attempt);
            recruitApplied = decisions.Recruit;
            passiveApplied = decisions.Passive;
            refitApplied = decisions.Refit;
            townDecisionsDriven = true;
        }

        mutations.GoldDelta += goldDelta;
        mutations.EchoDelta += echoDelta;
        mutations.AugmentChoices += permanentDelta;
        mutations.RecruitDecisionsApplied += recruitApplied;
        mutations.PassiveDecisionsApplied += passiveApplied;
        mutations.RefitDecisionsApplied += refitApplied;

        return new CampaignRecoverySettlementObservation(
            nodeId,
            victory,
            choiceKind,
            goldDelta,
            echoDelta,
            permanentDelta,
            runTerminated,
            townDecisionsDriven,
            recruitApplied,
            passiveApplied,
            refitApplied,
            mutations.PrepEquipmentAssignments);
    }

    private static (int Recruit, int Passive, int Refit) ApplyRecoveryTownDecisions(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessRosterPolicy rosterPolicy,
        CampaignBalanceGridCell cell,
        CampaignRecoveryTarget target,
        int attempt)
    {
        var recruitObservation = H100RosterPolicyObservationBuilder.Build(
            session,
            lookup,
            RecoveryDecisionSeed(cell.CellId, target.NodeId, target.SiteId, attempt, "town-recruit"));
        var recruit = H100SessionDriver.ApplyPolicyRecruit(session, rosterPolicy, recruitObservation);

        var passiveObservation = H100RosterPolicyObservationBuilder.Build(
            session,
            lookup,
            RecoveryDecisionSeed(cell.CellId, target.NodeId, target.SiteId, attempt, "town-passive"));
        var passive = H100SessionDriver.ApplyPolicyPassive(session, rosterPolicy, passiveObservation);

        var refitObservation = H100RosterPolicyObservationBuilder.Build(
            session,
            lookup,
            RecoveryDecisionSeed(cell.CellId, target.NodeId, target.SiteId, attempt, "town-refit"));
        var refit = H100SessionDriver.ApplyPolicyRefit(session, rosterPolicy, refitObservation);
        return (
            recruit.IsNoOp ? 0 : 1,
            passive.IsNoOp ? 0 : 1,
            refit.IsNoOp ? 0 : 1);
    }

    private static void ApplyRecoveryDeployment(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        CampaignBalanceGridCell cell,
        int attempt)
    {
        var seed = RecoveryDecisionSeed(
            cell.CellId,
            session.SelectedCampaignSiteId,
            session.SelectedCampaignChapterId,
            attempt,
            "deployment");
        var observation = H100PolicyObservationBuilder.Build(
            session,
            lookup,
            seed,
            includeTownRoster: true);
        H100SessionDriver.ApplyPolicyDeployment(
            session,
            lookup,
            policy,
            seed,
            observation);
    }

    private static CampaignRecoveryPowerObservation MeasurePower(
        BattleLoadoutSnapshot snapshot,
        GameSessionState session)
    {
        var keys = new[] { StatKey.MaxHealth, StatKey.PhysPower, StatKey.MagPower };
        var effectiveHp = 0d;
        var effectiveOffense = 0d;
        foreach (var ally in snapshot.Allies)
        {
            var stats = HeroEffectiveStatPreview.Resolve(ally, keys)
                .ToDictionary(entry => entry.Key, entry => entry.EffectiveValue);
            effectiveHp += stats.TryGetValue(StatKey.MaxHealth, out var hp) ? hp : 0f;
            effectiveOffense += stats.TryGetValue(StatKey.PhysPower, out var physical) ? physical : 0f;
            effectiveOffense += stats.TryGetValue(StatKey.MagPower, out var magical) ? magical : 0f;
        }

        var heroLevelSum = session.Profile.HeroProgressions
            .Where(value => snapshot.BattleDeployHeroIds.Contains(value.HeroId, StringComparer.Ordinal))
            .Sum(value => value.Level);
        var equippedItemCount = session.Profile.Heroes
            .Where(value => snapshot.BattleDeployHeroIds.Contains(value.HeroId, StringComparer.Ordinal))
            .Sum(value => value.EquippedItemIds.Count);
        return new CampaignRecoveryPowerObservation(
            Math.Round(effectiveHp, 6, MidpointRounding.AwayFromZero),
            Math.Round(effectiveOffense, 6, MidpointRounding.AwayFromZero),
            heroLevelSum,
            equippedItemCount);
    }

    private static int RecoveryBattleSeed(
        string cellId,
        string targetNodeId,
        string nodeId,
        int attempt)
        => H100SessionDriver.DeriveSeed(
            $"recovery-battle|{cellId}|{targetNodeId}|{nodeId}",
            3000 + attempt);

    private static int RecoveryDecisionSeed(
        string cellId,
        string targetNodeId,
        string subjectId,
        int attempt,
        string kind)
        => H100SessionDriver.DeriveSeed(
            $"recovery-decision|{cellId}|{targetNodeId}|{subjectId}|{kind}",
            7000 + attempt);
}
