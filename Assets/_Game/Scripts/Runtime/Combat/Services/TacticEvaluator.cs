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
    MobilityDecision? Mobility,
    PositioningIntentKind PositioningIntent = PositioningIntentKind.None);

public static class TacticEvaluator
{
    private const float HealActivationHealthRatio = 0.9f;
    private const float MeleeSlotRangeMin = 0.55f;
    private const float MeleeSlotRangeMax = 0.85f;
    private const float SupportMaxRangeFloor = 1.4f;
    private const float SupportRangeShrink = 0.8f;

    public static AiPerceptionBlackboard BuildPerceptionBlackboard(BattleState state, UnitSnapshot actor)
        => AiPerceptionBlackboardService.Build(state, actor);

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

    /// <summary>
    /// Phase 1 narrow retarget seam. A Dive or Peel intent (set by RoleBrain) forces the unit's target to the
    /// intent's chosen enemy, overriding the normal melee-nearest selection — but ONLY for those two intents and
    /// ONLY toward a living enemy. The baseline target selection and the melee-nearest override are otherwise
    /// untouched, so default (non-Dive/Peel) behavior is unchanged. Returns false (and leaves the evaluation
    /// as-is) when no override applies.
    /// </summary>
    public static bool TryApplyIntentTargetOverride(
        BattleState state, UnitSnapshot actor, EvaluatedAction evaluated, out EvaluatedAction retargeted)
    {
        retargeted = evaluated;
        if (evaluated.Target?.Side == actor.Side
            || evaluated.Skill?.Kind is SkillKind.Heal or SkillKind.Shield or SkillKind.Buff)
        {
            return false;
        }

        var intent = actor.CurrentCombatIntent;
        if (intent.Type != CombatIntentType.Dive && intent.Type != CombatIntentType.Peel)
        {
            return false;
        }

        if (intent.TargetId == null)
        {
            return false;
        }

        var forced = state.FindUnit(intent.TargetId);
        if (forced == null || !forced.IsAlive || forced.Side == actor.Side)
        {
            return false;
        }

        if (evaluated.Target != null && evaluated.Target.Id == forced.Id)
        {
            return false; // already on the forced target — nothing to change
        }

        retargeted = evaluated with { Target = forced };
        return true;
    }

    private static EvaluatedAction EvaluateLoopA(BattleState state, UnitSnapshot actor, ReevaluationReason reevaluationReason)
    {
        var fallbackRule = new TacticRule(999, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self, null);
        // P1 플레이어 타겟 지시: 기본공격(=교전 대상) 타게팅에만 적용한다. 저작된 스킬 규칙은 보존,
        // Dive/Peel 의도와 melee 최근접 가드(Q5)는 지시보다 우선. stable-target 히스테리시스도 그대로
        // (지시는 재획득 시점에 작동) — 안티-스래시 계약 불변.
        var directedBasicRule = PlayerTargetDirectiveRules.Apply(
            actor.Definition.TargetDirective,
            actor.Definition.EffectiveBasicAttack.TargetRuleData);
        var stableTarget = ResolveStableTarget(state, actor, directedBasicRule);
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

        // Ground-state flex interrupt — offensive flexes always qualify; allied Heal flexes qualify only when
        // their authored rule resolves an injured same-side target (validated again in TryActiveSkill).
        var flex = actor.Definition.EffectiveFlexActive;
        if (flex != null && IsGroundStateInterruptingFlex(flex))
        {
            var combatFlexResult = TryActiveSkill(state, actor, flex, stableTarget, fallbackRule, reevaluationReason,
                skill => StatusResolutionService.CanUseSkillSlot(actor, skill) && actor.CooldownRemaining <= 0f);
            if (combatFlexResult != null)
            {
                return combatFlexResult;
            }
        }

        // Ground state: BasicAttack — default combat action, generates energy
        var basicResult = TryBasicAttack(state, actor, stableTarget, directedBasicRule, baseRangeBand, fallbackRule, reevaluationReason);
        if (basicResult.ActionType != BattleActionType.WaitDefend)
        {
            return basicResult;
        }

        // Non-interrupting flex fallback — Shield/Buff/Utility when no basic attack target.
        if (flex != null && !IsGroundStateInterruptingFlex(flex))
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

    private static bool IsGroundStateInterruptingFlex(BattleSkillSpec skill)
    {
        return IsCombatRelevantFlex(skill)
               || skill.Kind == SkillKind.Heal
               && skill.TargetRuleData?.Domain is TargetDomain.AlliedUnit or TargetDomain.Self;
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
            ResolvePhase(actor, mobilityTarget, baseRangeBand, mobilityDecision),
            reevaluationReason, mobilityDecision);
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

        // A combat target lock must never redirect a Heal onto the current enemy. Heal asks its own authored
        // rule directly, then the same-side/meaningful-injury gate below prevents both wrong-side restoration
        // and low-value fallback self-casts (heal-lock). Other skills preserve the existing stable-target contract.
        var target = skill.Kind == SkillKind.Heal
            ? TargetScoringService.SelectTarget(state, actor, skill.TargetRuleData)
            : ResolveStableTarget(state, actor, skill.TargetRuleData)
              ?? TargetScoringService.SelectTarget(state, actor, skill.TargetRuleData);
        if (target == null)
        {
            return null;
        }

        var isAlliedSupportKind = skill.Kind is SkillKind.Heal or SkillKind.Shield or SkillKind.Buff;
        if (isAlliedSupportKind && target.Side != actor.Side)
        {
            return null;
        }

        if (skill.Kind == SkillKind.Heal && target.HealthRatio >= HealActivationHealthRatio)
        {
            return null;
        }

        var rangeBand = ResolveLoopARangeBand(actor, skill, BattleActionType.ActiveSkill);
        var positioningIntent = ApproachOffsetService.ResolvePositioningIntent(state, actor, target, rangeBand);
        return new EvaluatedAction(
            BattleActionType.ActiveSkill, target, skill, fallbackRule, rangeBand,
            ResolvePhase(actor, target, rangeBand, null),
            reevaluationReason, null, positioningIntent);
    }

    private static EvaluatedAction TryBasicAttack(
        BattleState state, UnitSnapshot actor, UnitSnapshot? stableTarget, TargetRule basicRule,
        FloatRange baseRangeBand, TacticRule fallbackRule, ReevaluationReason reevaluationReason)
    {
        var basicTarget = stableTarget
                          ?? TargetScoringService.SelectTarget(state, actor, basicRule);
        if (basicTarget != null)
        {
            var positioningIntent = ApproachOffsetService.ResolvePositioningIntent(state, actor, basicTarget, baseRangeBand);
            return new EvaluatedAction(
                BattleActionType.BasicAttack, basicTarget, null, fallbackRule, baseRangeBand,
                ResolvePhase(actor, basicTarget, baseRangeBand, null),
                reevaluationReason, null, positioningIntent);
        }

        return new EvaluatedAction(
            BattleActionType.WaitDefend, actor, null, fallbackRule,
            new FloatRange(0f, 0f), CombatActionState.Reposition,
            reevaluationReason, null);
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
            var positioningIntent = target != null
                ? ApproachOffsetService.ResolvePositioningIntent(state, actor, target, rangeBand)
                : PositioningIntentKind.None;
            var mobility = target != null && target.Side != actor.Side
                ? MovementResolver.BuildMobilityDecision(actor, target, rangeBand)
                : null;
            return new EvaluatedAction(
                rule.ActionType,
                target,
                skill,
                rule,
                rangeBand,
                ResolvePhase(actor, target, rangeBand, mobility),
                reevaluationReason,
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
        if (currentTarget == null || !currentTarget.IsAlive || currentTarget.Side == actor.Side)
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

        if (ApproachOffsetService.IsMeleeEngagement(actor, authored))
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

        var distance = MovementResolver.ComputeEdgeDistance(actor, target);
        var actionStartBuffer = Math.Min(actor.Behavior.ApproachBuffer, MovementResolver.ActionStartRangeTolerance);
        if (distance > rangeBand.ClampedMax + actionStartBuffer)
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
