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
                    unit.IsDefending);
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
