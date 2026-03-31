using System;
using System.Linq;
using SM.Combat.Model;

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
    MobilityDecision? Mobility);

public static class TacticEvaluator
{
    public static EvaluatedAction Evaluate(BattleState state, UnitSnapshot actor)
    {
        var reevaluationReason = actor.PendingReevaluationReason != ReevaluationReason.None
            ? actor.PendingReevaluationReason
            : actor.NeedsReevaluation
                ? ReevaluationReason.Cadence
                : ReevaluationReason.None;
        var stableTarget = ResolveStableTarget(state, actor);
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
            var target = ResolveTarget(state, actor, stableTarget, rule, skill);
            if (!ConditionMatches(state, actor, rule, skill, target))
            {
                continue;
            }

            var rangeBand = ResolveRangeBand(actor, skill, rule.ActionType);
            var requiresSlot = target != null && target.Side != actor.Side && EngagementSlotService.RequiresSlotting(actor, rangeBand);
            var slotAssignment = requiresSlot && target != null
                ? EngagementSlotService.Resolve(state, actor, target, rangeBand)
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
                mobility);
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

    private static UnitSnapshot? ResolveTarget(
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

        if (stableTarget != null
            && stableTarget.IsAlive
            && stableTarget.Side != actor.Side
            && !actor.NeedsReevaluation)
        {
            return stableTarget;
        }

        return TargetScoringService.SelectTarget(state, actor, rule.TargetSelector, rule.ActionType, rule.SkillId);
    }

    private static UnitSnapshot? ResolveStableTarget(BattleState state, UnitSnapshot actor)
    {
        var currentTarget = state.FindUnit(actor.CurrentTargetId);
        if (currentTarget == null || !currentTarget.IsAlive)
        {
            return null;
        }

        if (!actor.NeedsReevaluation || actor.TargetSwitchLockRemaining > 0f)
        {
            return currentTarget;
        }

        return null;
    }

    private static FloatRange ResolveRangeBand(UnitSnapshot actor, BattleSkillSpec? skill, BattleActionType actionType)
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
            return new FloatRange(Math.Max(0.75f, authored.ClampedMin), Math.Max(0.95f, reach));
        }

        if (skill?.Kind is SkillKind.Heal or SkillKind.Shield or SkillKind.Buff or SkillKind.Utility)
        {
            min = Math.Max(0.8f, desiredMax - 0.9f);
            max = desiredMax;
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
        if (distance > rangeBand.ClampedMax + actor.Behavior.RangeHysteresis)
        {
            return CombatActionState.Approach;
        }

        if (distance < rangeBand.ClampedMin - actor.Behavior.RangeHysteresis)
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
