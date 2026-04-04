using System;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class EngagementSlotService
{
    private const float MaxMeleeCombatReach = 1.6f;
    private const float MaxMeleeRangeThreshold = 1.8f;
    private const float MeleeRangeBuffer = 0.25f;
    private const float SlotSpreadDegrees = 160f;
    private const float MinDesiredEdgeDistance = 0.85f;
    private const float MinOverflowRadiusScale = 0.45f;
    private const float ArenaHalfWidth = 8f;
    private const float ArenaHalfHeight = 3.2f;

    public static bool RequiresSlotting(UnitSnapshot actor, FloatRange rangeBand)
    {
        return actor.CombatReach <= MaxMeleeCombatReach
               && rangeBand.ClampedMax <= Math.Max(MaxMeleeRangeThreshold, actor.CombatReach + MeleeRangeBuffer);
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
        var desiredEdgeDistance = Math.Max(
            MathF.Max(rangeBand.ClampedMin, actor.CombatReach),
            MinDesiredEdgeDistance);
        var ringRadius = Math.Max(
            target.Footprint.EngagementSlotRadius + actor.NavigationRadius,
            target.NavigationRadius + actor.NavigationRadius + desiredEdgeDistance);
        var isOverflow = actorIndex >= slotCount;
        var ringOffset = actorIndex / slotCount;
        var slotIndex = actorIndex % slotCount;
        var spreadDegrees = slotCount == 1 ? 0f : SlotSpreadDegrees;
        var centerDegrees = actor.Side == TeamSide.Ally ? 180f : 0f;
        var startDegrees = centerDegrees - (spreadDegrees * 0.5f);
        var stepDegrees = slotCount == 1 ? 0f : spreadDegrees / (slotCount - 1);
        var angleDegrees = startDegrees + (stepDegrees * slotIndex);
        var angleRadians = angleDegrees * (MathF.PI / 180f);
        var radius = ringRadius + (ringOffset * Math.Max(MinOverflowRadiusScale, actor.SeparationRadius));
        var position = target.Position + new CombatVector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians)) * radius;
        position = new CombatVector2(
            Math.Clamp(position.X, -ArenaHalfWidth, ArenaHalfWidth),
            Math.Clamp(position.Y, -ArenaHalfHeight, ArenaHalfHeight));
        return new EngagementSlotAssignment(target.Id, slotIndex, position, isOverflow);
    }
}
