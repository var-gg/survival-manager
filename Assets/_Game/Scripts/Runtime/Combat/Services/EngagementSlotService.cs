using System;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class EngagementSlotService
{
    public static bool RequiresSlotting(UnitSnapshot actor, FloatRange rangeBand)
    {
        return actor.CombatReach <= 1.6f || rangeBand.ClampedMax <= Math.Max(1.6f, actor.CombatReach + 0.2f);
    }

    public static EngagementSlotAssignment? Resolve(BattleState state, UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand)
    {
        if (!RequiresSlotting(actor, rangeBand))
        {
            return null;
        }

        var attackers = state.GetOpponents(target.Side)
            .Where(unit => unit.IsAlive && (unit.Id == actor.Id || unit.CurrentTargetId == target.Id || unit.PendingTargetId == target.Id))
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToList();
        if (attackers.All(unit => unit.Id != actor.Id))
        {
            attackers.Add(actor);
            attackers = attackers.OrderBy(unit => unit.Id.Value, StringComparer.Ordinal).ToList();
        }

        var actorIndex = attackers.FindIndex(unit => unit.Id == actor.Id);
        if (actorIndex < 0)
        {
            return null;
        }

        var slotCount = Math.Max(1, target.Footprint.EngagementSlotCount);
        var ringRadius = Math.Max(
            target.Footprint.EngagementSlotRadius,
            target.NavigationRadius + actor.NavigationRadius + 0.25f);
        var isOverflow = actorIndex >= slotCount;
        var ringOffset = actorIndex / slotCount;
        var slotIndex = actorIndex % slotCount;
        var spreadDegrees = slotCount == 1 ? 0f : 160f;
        var centerDegrees = actor.Side == TeamSide.Ally ? 180f : 0f;
        var startDegrees = centerDegrees - (spreadDegrees * 0.5f);
        var stepDegrees = slotCount == 1 ? 0f : spreadDegrees / (slotCount - 1);
        var angleDegrees = startDegrees + (stepDegrees * slotIndex);
        var angleRadians = angleDegrees * (MathF.PI / 180f);
        var radius = ringRadius + (ringOffset * Math.Max(0.45f, actor.SeparationRadius));
        var position = target.Position + new CombatVector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians)) * radius;
        return new EngagementSlotAssignment(target.Id, slotIndex, position, isOverflow);
    }
}
