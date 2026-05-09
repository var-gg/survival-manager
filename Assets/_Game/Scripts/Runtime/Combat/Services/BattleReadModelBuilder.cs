using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class BattleReadModelBuilder
{
    public static BattleSimulationStep BuildStep(
        BattleState state,
        IReadOnlyList<BattleEvent> events,
        bool isFinished,
        TeamSide? winner)
    {
        var units = state.AllUnits
            .OrderBy(unit => unit.Side)
            .ThenBy(unit => unit.Anchor)
            .ThenBy(unit => unit.Definition.Name)
            .Select(unit =>
            {
                var target = state.FindUnit(unit.CurrentTargetId) ?? state.FindUnit(unit.PendingTargetId);
                return new BattleUnitReadModel(
                    unit.Id.Value,
                    unit.Definition.Name,
                    unit.Side,
                    unit.Anchor,
                    unit.Definition.RaceId,
                    unit.Definition.ClassId,
                    unit.Position,
                    unit.CurrentHealth,
                    unit.MaxHealth,
                    unit.IsAlive,
                    unit.ActionState,
                    unit.PendingActionType,
                    target?.Id.Value,
                    target?.Definition.Name,
                    unit.WindupProgress,
                    unit.CooldownRemaining,
                    unit.CurrentEnergy,
                    unit.MaxEnergy,
                    unit.IsDefending,
                    unit.Barrier,
                    unit.Statuses.Select(status => status.StatusId).ToList(),
                    unit.HeadAnchorHeight,
                    unit.NavigationRadius,
                    unit.SeparationRadius,
                    unit.PreferredRangeBand.ClampedMin,
                    unit.PreferredRangeBand.ClampedMax,
                    unit.Footprint.EngagementSlotRadius,
                    unit.Footprint.EngagementSlotCount,
                    unit.EntityKind,
                    unit.CurrentTargetSelector.ToString(),
                    unit.CurrentFallbackPolicy.ToString(),
                    unit.RetargetLockRemaining,
                    unit.Behavior.FrontlineGuardRadius,
                    unit.Definition.EffectiveSignatureActive?.TargetRuleData?.ClusterRadius
                        ?? unit.Definition.EffectiveFlexActive?.TargetRuleData?.ClusterRadius
                        ?? unit.Definition.EffectiveBasicAttack.TargetRuleData?.ClusterRadius
                        ?? 2.5f,
                    unit.Definition.ArchetypeId,
                    unit.Definition.CharacterId,
                    unit.Definition.RoleInstructionId,
                    unit.Definition.RoleTag,
                    unit.Definition.EffectiveSignatureActive?.Id ?? string.Empty,
                    unit.Definition.EffectiveSignatureActive?.Name ?? string.Empty,
                    unit.Definition.EffectiveFlexActive?.Id ?? string.Empty,
                    unit.Definition.EffectiveFlexActive?.Name ?? string.Empty,
                    unit.PositioningIntent,
                    unit.PositioningReplanReason,
                    unit.PositioningIntentRevision,
                    unit.AttackSpeed,
                    unit.AttackCooldown,
                    unit.SkillHaste);
            })
            .ToList();

        return new BattleSimulationStep(
            state.StepIndex,
            state.ElapsedSeconds,
            units,
            events,
            isFinished,
            winner);
    }
}
