using System;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

public sealed record EvaluatedAction(
    BattleActionType ActionType,
    UnitSnapshot? Target,
    BattleSkillSpec? Skill,
    TacticRule Rule,
    FloatRange DesiredRangeBand,
    CombatActionState DesiredPhase,
    ReevaluationReason ReevaluationReason,
    bool RequiresEngagementSlot,
    EngagementSlotAssignment? SlotAssignment,
    MobilityDecision? Mobility,
    PositioningIntentKind PositioningIntent = PositioningIntentKind.None);

public static class TacticEvaluator
{
    private const float MeleeSlotRangeMin = 0.75f;
    private const float MeleeSlotRangeMax = 0.95f;
    private const float SupportMaxRangeFloor = 1.4f;
    private const float SupportRangeShrink = 0.8f;

    public static EvaluatedAction Evaluate(BattleState state, UnitSnapshot actor)
    {
        var reevaluationReason = actor.PendingReevaluationReason != ReevaluationReason.None
            ? actor.PendingReevaluationReason
            : actor.NeedsReevaluation
                ? ReevaluationReason.Cadence
                : ReevaluationReason.None;

        if (actor.Definition.HasLoopALoadout)
        {
            return EvaluateLoopA(state, actor, reevaluationReason);
        }

        return EvaluateLegacy(state, actor, reevaluationReason);
    }

    private static EvaluatedAction EvaluateLoopA(BattleState state, UnitSnapshot actor, ReevaluationReason reevaluationReason)
    {
        var fallbackRule = new TacticRule(999, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self, null);
        var stableTarget = ResolveStableTarget(state, actor, actor.Definition.EffectiveBasicAttack.TargetRuleData);
        var baseRangeBand = ResolveLoopARangeBand(actor, null, BattleActionType.BasicAttack);

        // Mobility interrupt
        var mobilityResult = TryMobility(state, actor, stableTarget, baseRangeBand, fallbackRule, reevaluationReason);
        if (mobilityResult != null)
        {
            return mobilityResult;
        }

        // Signature interrupt — energy-gated, highest skill priority
        var signatureResult = TryActiveSkill(state, actor, actor.Definition.EffectiveSignatureActive, stableTarget, fallbackRule, reevaluationReason,
            skill => StatusResolutionService.CanUseSkillSlot(actor, skill) && actor.CanSpendSignatureCastEnergy());
        if (signatureResult != null)
        {
            return signatureResult;
        }

        // Combat-relevant flex interrupt — Strike/Debuff flex interrupts the ground state
        var flex = actor.Definition.EffectiveFlexActive;
        if (flex != null && IsCombatRelevantFlex(flex))
        {
            var combatFlexResult = TryActiveSkill(state, actor, flex, stableTarget, fallbackRule, reevaluationReason,
                skill => StatusResolutionService.CanUseSkillSlot(actor, skill) && actor.CooldownRemaining <= 0f);
            if (combatFlexResult != null)
            {
                return combatFlexResult;
            }
        }

        // Ground state: BasicAttack — default combat action, generates energy
        var basicResult = TryBasicAttack(state, actor, stableTarget, baseRangeBand, fallbackRule, reevaluationReason);
        if (basicResult.ActionType != BattleActionType.WaitDefend)
        {
            return basicResult;
        }

        // Non-combat flex fallback — Heal/Shield/Buff/Utility when no basic attack target
        if (flex != null && !IsCombatRelevantFlex(flex))
        {
            var utilityFlexResult = TryActiveSkill(state, actor, flex, stableTarget, fallbackRule, reevaluationReason,
                skill => StatusResolutionService.CanUseSkillSlot(actor, skill) && actor.CooldownRemaining <= 0f);
            if (utilityFlexResult != null)
            {
                return utilityFlexResult;
            }
        }

        // Defend/Reposition
        return basicResult;
    }

    private static bool IsCombatRelevantFlex(BattleSkillSpec skill)
    {
        return skill.Kind is SkillKind.Strike or SkillKind.Debuff;
    }

    private static EvaluatedAction? TryMobility(
        BattleState state, UnitSnapshot actor, UnitSnapshot? stableTarget,
        FloatRange baseRangeBand, TacticRule fallbackRule, ReevaluationReason reevaluationReason)
    {
        if (actor.EffectiveMobilityReaction is not { } mobilityReaction
            || actor.IsStunned || actor.IsRooted || actor.MobilityCooldownRemaining > 0f)
        {
            return null;
        }

        var mobilityTarget = stableTarget ?? TargetScoringService.SelectTarget(state, actor, mobilityReaction.TargetRuleData);
        var mobilityDecision = mobilityTarget == null ? null : MovementResolver.BuildMobilityDecision(actor, mobilityTarget, baseRangeBand);
        if (mobilityDecision == null)
        {
            return null;
        }

        return new EvaluatedAction(
            BattleActionType.BasicAttack, mobilityTarget, null, fallbackRule, baseRangeBand,
            ResolvePhase(actor, mobilityTarget, baseRangeBand, null, mobilityDecision),
            reevaluationReason, false, null, mobilityDecision);
    }

    private static EvaluatedAction? TryActiveSkill(
        BattleState state, UnitSnapshot actor, BattleSkillSpec? skill, UnitSnapshot? stableTarget,
        TacticRule fallbackRule, ReevaluationReason reevaluationReason,
        Func<BattleSkillSpec, bool> readyCheck)
    {
        if (skill == null || !readyCheck(skill))
        {
            return null;
        }

        var target = ResolveStableTarget(state, actor, skill.TargetRuleData)
                     ?? TargetScoringService.SelectTarget(state, actor, skill.TargetRuleData);
        if (target == null)
        {
            return null;
        }

        var rangeBand = ResolveLoopARangeBand(actor, skill, BattleActionType.ActiveSkill);
        var requiresSlot = target.Side != actor.Side && EngagementSlotService.RequiresSlotting(actor, rangeBand);
        var positioningIntent = EngagementSlotService.ResolvePositioningIntent(state, actor, target, rangeBand);
        var slotAssignment = requiresSlot ? EngagementSlotService.Resolve(state, actor, target, rangeBand, positioningIntent) : null;
        return new EvaluatedAction(
            BattleActionType.ActiveSkill, target, skill, fallbackRule, rangeBand,
            ResolvePhase(actor, target, rangeBand, slotAssignment, null),
            reevaluationReason, requiresSlot, slotAssignment, null, positioningIntent);
    }

    private static EvaluatedAction TryBasicAttack(
        BattleState state, UnitSnapshot actor, UnitSnapshot? stableTarget,
        FloatRange baseRangeBand, TacticRule fallbackRule, ReevaluationReason reevaluationReason)
    {
        var basicTarget = stableTarget
                          ?? TargetScoringService.SelectTarget(state, actor, actor.Definition.EffectiveBasicAttack.TargetRuleData);
        if (basicTarget != null)
        {
            var requiresSlot = basicTarget.Side != actor.Side && EngagementSlotService.RequiresSlotting(actor, baseRangeBand);
            var positioningIntent = EngagementSlotService.ResolvePositioningIntent(state, actor, basicTarget, baseRangeBand);
            var slotAssignment = requiresSlot ? EngagementSlotService.Resolve(state, actor, basicTarget, baseRangeBand, positioningIntent) : null;
            return new EvaluatedAction(
                BattleActionType.BasicAttack, basicTarget, null, fallbackRule, baseRangeBand,
                ResolvePhase(actor, basicTarget, baseRangeBand, slotAssignment, null),
                reevaluationReason, requiresSlot, slotAssignment, null, positioningIntent);
        }

        return new EvaluatedAction(
            BattleActionType.WaitDefend, actor, null, fallbackRule,
            new FloatRange(0f, 0f), CombatActionState.Reposition,
            reevaluationReason, false, null, null);
    }

    private static EvaluatedAction EvaluateLegacy(BattleState state, UnitSnapshot actor, ReevaluationReason reevaluationReason)
    {
        var stableTarget = ResolveStableTarget(state, actor, null);
        var ordered = actor.Definition.Tactics.OrderBy(x => x.Priority);
        foreach (var rule in ordered)
        {
            if (rule.ActionType == BattleActionType.ActiveSkill && !StatusResolutionService.CanUseActiveSkill(actor))
            {
                continue;
            }

            var skill = rule.ActionType == BattleActionType.ActiveSkill
                ? actor.ResolveSkill(rule.SkillId)
                : null;
            var target = ResolveLegacyTarget(state, actor, stableTarget, rule, skill);
            if (!ConditionMatches(state, actor, rule, skill, target))
            {
                continue;
            }

            var rangeBand = ResolveLegacyRangeBand(actor, skill, rule.ActionType);
            var requiresSlot = target != null && target.Side != actor.Side && EngagementSlotService.RequiresSlotting(actor, rangeBand);
            var positioningIntent = target != null
                ? EngagementSlotService.ResolvePositioningIntent(state, actor, target, rangeBand)
                : PositioningIntentKind.None;
            var slotAssignment = requiresSlot && target != null
                ? EngagementSlotService.Resolve(state, actor, target, rangeBand, positioningIntent)
                : null;
            var mobility = target != null && target.Side != actor.Side
                ? MovementResolver.BuildMobilityDecision(actor, target, rangeBand)
                : null;
            return new EvaluatedAction(
                rule.ActionType,
                target,
                skill,
                rule,
                rangeBand,
                ResolvePhase(actor, target, rangeBand, slotAssignment, mobility),
                reevaluationReason,
                requiresSlot,
                slotAssignment,
                mobility,
                positioningIntent);
        }

        var fallbackRule = new TacticRule(999, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self, null);
        return new EvaluatedAction(
            BattleActionType.WaitDefend,
            actor,
            null,
            fallbackRule,
            new FloatRange(0f, 0f),
            CombatActionState.Reposition,
            reevaluationReason,
            false,
            null,
            null);
    }

    private static UnitSnapshot? ResolveLegacyTarget(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? stableTarget,
        TacticRule rule,
        BattleSkillSpec? skill)
    {
        if (rule.TargetSelector == TargetSelectorType.Self)
        {
            return actor;
        }

        if (rule.TargetSelector != TargetSelectorType.LowestHpAlly
            && stableTarget == null
            && actor.CurrentTargetId == null
            && actor.TargetSwitchLockRemaining > 0f)
        {
            return null;
        }

        if (stableTarget != null
            && stableTarget.IsAlive
            && stableTarget.Side != actor.Side
            && !actor.NeedsReevaluation)
        {
            return stableTarget;
        }

        return TargetScoringService.SelectTarget(state, actor, rule.TargetSelector, rule.ActionType, rule.SkillId);
    }

    private static UnitSnapshot? ResolveStableTarget(BattleState state, UnitSnapshot actor, TargetRule? targetRule)
    {
        var currentTarget = state.FindUnit(actor.CurrentTargetId);
        if (currentTarget == null || !currentTarget.IsAlive)
        {
            return null;
        }

        if (actor.TargetSwitchLockRemaining > 0f)
        {
            return currentTarget;
        }

        if (targetRule != null)
        {
            var maxAcquireRange = targetRule.MaxAcquireRange > 0f ? targetRule.MaxAcquireRange : actor.AttackRange;
            if (MovementResolver.ComputeEdgeDistance(actor, currentTarget) > maxAcquireRange + 1f)
            {
                return null;
            }
        }

        return !actor.NeedsReevaluation
            ? currentTarget
            : null;
    }

    private static FloatRange ResolveLoopARangeBand(UnitSnapshot actor, BattleSkillSpec? skill, BattleActionType actionType)
    {
        if (actionType == BattleActionType.WaitDefend)
        {
            return new FloatRange(0f, 0f);
        }

        var desiredMax = Math.Max(0.4f, skill?.Range ?? actor.AttackRange);
        var preferredMin = actor.Behavior.PreferredRangeMin > 0f ? actor.Behavior.PreferredRangeMin : actor.PreferredRangeBand.ClampedMin;
        var preferredMax = actor.Behavior.PreferredRangeMax > 0f ? actor.Behavior.PreferredRangeMax : actor.PreferredRangeBand.ClampedMax;
        var min = Math.Min(preferredMin, desiredMax);
        var max = Math.Min(Math.Max(min, preferredMax), desiredMax);
        return new FloatRange(min, max <= 0f ? desiredMax : max);
    }

    private static FloatRange ResolveLegacyRangeBand(UnitSnapshot actor, BattleSkillSpec? skill, BattleActionType actionType)
    {
        if (actionType == BattleActionType.WaitDefend)
        {
            return new FloatRange(0f, 0f);
        }

        var desiredMax = Math.Max(0.4f, skill?.Range ?? actor.AttackRange);
        var authored = actor.PreferredRangeBand;
        var min = Math.Min(authored.ClampedMin, desiredMax);
        var max = Math.Min(Math.Max(min, authored.ClampedMax), desiredMax);

        if (EngagementSlotService.RequiresSlotting(actor, authored))
        {
            var reach = Math.Min(desiredMax, Math.Max(actor.CombatReach, authored.ClampedMax));
            return new FloatRange(Math.Max(MeleeSlotRangeMin, authored.ClampedMin), Math.Max(MeleeSlotRangeMax, reach));
        }

        if (skill?.Kind is SkillKind.Heal or SkillKind.Shield or SkillKind.Buff)
        {
            max = Math.Max(SupportMaxRangeFloor, desiredMax - SupportRangeShrink);
            min = Math.Max(SupportRangeShrink, max - SupportRangeShrink);
        }

        if (max <= 0f)
        {
            max = desiredMax;
        }

        if (min > max)
        {
            min = max;
        }

        return new FloatRange(min, max);
    }

    private static CombatActionState ResolvePhase(
        UnitSnapshot actor,
        UnitSnapshot? target,
        FloatRange rangeBand,
        EngagementSlotAssignment? slotAssignment,
        MobilityDecision? mobility)
    {
        if (target == null || target.Side == actor.Side)
        {
            return CombatActionState.Reposition;
        }

        if (mobility != null)
        {
            return mobility.Profile.Purpose is MobilityPurpose.Disengage or MobilityPurpose.Evade or MobilityPurpose.MaintainRange
                ? CombatActionState.BreakContact
                : CombatActionState.Reposition;
        }

        if (slotAssignment is { IsOverflow: true })
        {
            return CombatActionState.SecurePosition;
        }

        var distance = MovementResolver.ComputeEdgeDistance(actor, target);
        if (distance > rangeBand.ClampedMax + actor.Behavior.ApproachBuffer)
        {
            return CombatActionState.Approach;
        }

        if (distance < rangeBand.ClampedMin - actor.Behavior.RetreatBuffer)
        {
            return CombatActionState.BreakContact;
        }

        return actor.CooldownRemaining > 0f
            ? CombatActionState.Recover
            : CombatActionState.ExecuteAction;
    }

    private static bool ConditionMatches(BattleState state, UnitSnapshot actor, TacticRule rule, BattleSkillSpec? skill, UnitSnapshot? target)
    {
        switch (rule.ConditionType)
        {
            case TacticConditionType.SelfHpBelow:
                return actor.HealthRatio <= rule.Threshold && target is not null;
            case TacticConditionType.AllyHpBelow:
                return state.GetTeam(actor.Side).Where(x => x.IsAlive).Any(x => x.HealthRatio <= rule.Threshold) && target is not null;
            case TacticConditionType.EnemyInRange:
                return target is not null
                       && target.Side != actor.Side
                       && MovementResolver.IsInActionRange(actor, target, skill?.Range ?? actor.AttackRange);
            case TacticConditionType.LowestHpEnemy:
                return target is not null;
            case TacticConditionType.EnemyExposed:
                return target is not null
                       && target.Side != actor.Side
                       && TargetScoringService.ComputeExposureScore(state, target) >= MathF.Max(1f, rule.Threshold);
            case TacticConditionType.Fallback:
                return target is not null;
            default:
                return false;
        }
    }
}
