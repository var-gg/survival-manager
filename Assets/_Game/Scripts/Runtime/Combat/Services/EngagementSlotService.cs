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
    private const float MinDesiredEdgeDistance = 0.55f;
    private const float MinOverflowRadiusScale = 0.45f;
    private const float ArenaHalfWidth = 8f;
    private const float ArenaHalfHeight = 3.2f;

    public static bool RequiresSlotting(UnitSnapshot actor, FloatRange rangeBand)
    {
        return actor.CombatReach <= MaxMeleeCombatReach
               && rangeBand.ClampedMax <= Math.Max(MaxMeleeRangeThreshold, actor.CombatReach + MeleeRangeBuffer);
    }

    public static PositioningIntentKind ResolvePositioningIntent(BattleState state, UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand)
    {
        if (!RequiresSlotting(actor, rangeBand))
        {
            return PositioningIntentKind.MaintainRange;
        }

        var roll = PositiveModulo(state.Seed + StableHash(actor.Id.Value) + StableHash(target.Id.Value), 100);
        return actor.Definition.ClassId switch
        {
            "duelist" when roll < 34 => PositioningIntentKind.FlankLeft,
            "duelist" when roll < 68 => PositioningIntentKind.FlankRight,
            "duelist" => PositioningIntentKind.BacklineDive,
            "vanguard" when roll < 55 => PositioningIntentKind.Frontline,
            "vanguard" when roll < 78 => PositioningIntentKind.FlankLeft,
            "vanguard" => PositioningIntentKind.FlankRight,
            _ when roll < 45 => PositioningIntentKind.Frontline,
            _ when roll < 70 => PositioningIntentKind.FlankLeft,
            _ when roll < 95 => PositioningIntentKind.FlankRight,
            _ => PositioningIntentKind.BacklineDive,
        };
    }

    public static EngagementSlotAssignment? Resolve(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        FloatRange rangeBand,
        PositioningIntentKind intent = PositioningIntentKind.None)
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
        var spreadDegrees = ResolveSpreadDegrees(intent, slotCount);
        var centerDegrees = ResolveCenterDegrees(actor.Side, intent);
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

    private static float ResolveSpreadDegrees(PositioningIntentKind intent, int slotCount)
    {
        if (slotCount == 1)
        {
            return 0f;
        }

        return intent switch
        {
            PositioningIntentKind.FlankLeft or PositioningIntentKind.FlankRight => 36f,
            PositioningIntentKind.BacklineDive => 54f,
            _ => SlotSpreadDegrees,
        };
    }

    private static float ResolveCenterDegrees(TeamSide actorSide, PositioningIntentKind intent)
    {
        return intent switch
        {
            PositioningIntentKind.FlankLeft => actorSide == TeamSide.Ally ? 125f : 55f,
            PositioningIntentKind.FlankRight => actorSide == TeamSide.Ally ? 235f : -55f,
            PositioningIntentKind.BacklineDive => actorSide == TeamSide.Ally ? 0f : 180f,
            _ => actorSide == TeamSide.Ally ? 180f : 0f,
        };
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return hash;
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
