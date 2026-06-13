using System;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class AiPerceptionBlackboardService
{
    public static AiPerceptionBlackboard Build(BattleState state, UnitSnapshot actor)
    {
        if (state == null || actor == null)
        {
            return AiPerceptionBlackboard.Empty(string.Empty, TeamSide.Ally, 0);
        }

        var teamBlackboard = state.GetTeamBlackboard(actor.Side);
        var allies = state.GetTeam(actor.Side).Where(unit => unit.IsAlive).ToList();
        var enemies = state.GetOpponents(actor.Side).Where(unit => unit.IsAlive).ToList();
        var nearestEnemyDistance = enemies.Count == 0
            ? 0f
            : enemies.Min(enemy => MovementResolver.ComputeEdgeDistance(actor, enemy));

        return new AiPerceptionBlackboard(
            actor.Id.Value,
            actor.Side,
            state.StepIndex,
            teamBlackboard.FocusMarkId,
            teamBlackboard.CarryId,
            teamBlackboard.FrontlineBreachers.Count,
            allies.Count,
            enemies.Count,
            allies.Count == 0 ? 0f : allies.Min(unit => unit.HealthRatio),
            enemies.Count == 0 ? 0f : enemies.Min(unit => unit.HealthRatio),
            nearestEnemyDistance,
            actor.CurrentCombatIntent.Type == CombatIntentType.Dive,
            enemies.Any(enemy => BattleFormationConsequence.IsScreenedBackline(state, enemy)));
    }
}
