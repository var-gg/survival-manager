using System;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class EngagementSlotService
{
    private const float MaxMeleeCombatReach = 1.6f;
    private const float MaxMeleeRangeThreshold = 1.8f;
    private const float MeleeRangeBuffer = 0.25f;
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

        var context = state.GetTacticContext(actor.Side);
        var flankBias = MathF.Abs(context.FlankBias);
        var roll = PositiveModulo(state.Seed + StableHash(actor.Id.Value) + StableHash(target.Id.Value) + (state.StepIndex * 17), 100);
        var flankThreshold = 45 + (int)MathF.Round(flankBias * 20f);
        return actor.Definition.ClassId switch
        {
            "duelist" when roll < 34 => PositioningIntentKind.FlankLeft,
            "duelist" when roll < 68 => PositioningIntentKind.FlankRight,
            "duelist" => PositioningIntentKind.BacklineDive,
            "vanguard" when roll < 55 => PositioningIntentKind.Frontline,
            "vanguard" when roll < 78 => PositioningIntentKind.FlankLeft,
            "vanguard" => PositioningIntentKind.FlankRight,
            _ when roll < 100 - flankThreshold => PositioningIntentKind.Frontline,
            _ when roll < 100 - (flankThreshold / 2) => PositioningIntentKind.FlankLeft,
            _ when roll < 98 => PositioningIntentKind.FlankRight,
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

        if (actor.EngagementSlot is { } existing
            && existing.TargetId == target.Id
            && IsSlotStillClear(state, actor, existing))
        {
            return existing;
        }

        var context = state.GetTacticContext(actor.Side);
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
        var spreadDegrees = ResolveSpreadDegrees(intent, slotCount, context);
        var centerDegrees = ResolveCenterDegrees(actor.Side, intent, context);
        var startDegrees = centerDegrees - (spreadDegrees * 0.5f);
        var stepDegrees = slotCount == 1 ? 0f : spreadDegrees / (slotCount - 1);
        var radius = ringRadius + (ringOffset * Math.Max(MinOverflowRadiusScale, actor.SeparationRadius));
        var selected = ResolveSlotCandidate(
            state,
            actor,
            target,
            context,
            startDegrees,
            stepDegrees,
            slotIndex,
            slotCount,
            radius);
        state.ActivityTelemetry.RecordHandednessSlotPreference(selected.PreferenceHit, selected.HasPreference);
        return new EngagementSlotAssignment(target.Id, selected.SlotIndex, selected.Position, isOverflow);
    }

    private static (int SlotIndex, CombatVector2 Position, bool PreferenceHit, bool HasPreference) ResolveSlotCandidate(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        TacticContext context,
        float startDegrees,
        float stepDegrees,
        int preferredSlotIndex,
        int slotCount,
        float radius)
    {
        var handSign = HandednessDecisionService.ResolvePreferredSideSign(actor.Definition.DominantHand, actor.Side);
        var handWeight = HandednessDecisionService.ResolveHandednessSlotWeight(actor, context);
        if (handSign == 0 || handWeight <= 0f)
        {
            return (preferredSlotIndex, BuildSlotPosition(target, startDegrees, stepDegrees, preferredSlotIndex, radius), true, false);
        }

        var bestScore = float.NegativeInfinity;
        var bestSlotIndex = preferredSlotIndex;
        var bestPosition = BuildSlotPosition(target, startDegrees, stepDegrees, preferredSlotIndex, radius);
        var bestPreferenceHit = false;
        var found = false;

        for (var candidateIndex = 0; candidateIndex < slotCount; candidateIndex++)
        {
            var position = BuildSlotPosition(target, startDegrees, stepDegrees, candidateIndex, radius);
            if (!IsCandidateClear(state, actor, position))
            {
                continue;
            }

            var angleRadians = (startDegrees + (stepDegrees * candidateIndex)) * (MathF.PI / 180f);
            var candidateSideSign = MathF.Sin(angleRadians) >= 0f ? 1 : -1;
            var handScore = candidateSideSign == handSign ? handWeight : -handWeight;
            var baseScore = -MathF.Abs(candidateIndex - preferredSlotIndex) * 0.035f;
            var score = baseScore + handScore;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestSlotIndex = candidateIndex;
            bestPosition = position;
            bestPreferenceHit = handSign == 0 || candidateSideSign == handSign;
            found = true;
        }

        if (!found)
        {
            var angleRadians = (startDegrees + (stepDegrees * preferredSlotIndex)) * (MathF.PI / 180f);
            var candidateSideSign = MathF.Sin(angleRadians) >= 0f ? 1 : -1;
            bestPreferenceHit = handSign == 0 || candidateSideSign == handSign;
        }

        return (bestSlotIndex, bestPosition, bestPreferenceHit, handSign != 0);
    }

    private static CombatVector2 BuildSlotPosition(UnitSnapshot target, float startDegrees, float stepDegrees, int slotIndex, float radius)
    {
        var angleDegrees = startDegrees + (stepDegrees * slotIndex);
        var angleRadians = angleDegrees * (MathF.PI / 180f);
        var position = target.Position + new CombatVector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians)) * radius;
        return new CombatVector2(
            Math.Clamp(position.X, -ArenaHalfWidth, ArenaHalfWidth),
            Math.Clamp(position.Y, -ArenaHalfHeight, ArenaHalfHeight));
    }

    private static bool IsCandidateClear(BattleState state, UnitSnapshot actor, CombatVector2 position)
    {
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || !obstacle.IsAlive)
            {
                continue;
            }

            var clearance = actor.NavigationRadius + obstacle.NavigationRadius + 0.03f;
            if (position.DistanceTo(obstacle.Position) < clearance)
            {
                return false;
            }
        }

        return true;
    }

    private static float ResolveCandidateCrowding(BattleState state, UnitSnapshot actor, CombatVector2 position)
    {
        return state.AllUnits
            .Where(unit => unit.IsAlive && unit.Id != actor.Id)
            .Sum(unit => 1f / MathF.Max(0.05f, position.DistanceTo(unit.Position)));
    }

    private static bool IsSlotStillClear(BattleState state, UnitSnapshot actor, EngagementSlotAssignment slot)
    {
        if (MathF.Abs(slot.Position.X) > ArenaHalfWidth || MathF.Abs(slot.Position.Y) > ArenaHalfHeight)
        {
            return false;
        }

        var target = state.FindUnit(slot.TargetId);
        if (target == null || !target.IsAlive)
        {
            return false;
        }

        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || !obstacle.IsAlive)
            {
                continue;
            }

            var clearance = actor.NavigationRadius + obstacle.NavigationRadius + 0.03f;
            if (slot.Position.DistanceTo(obstacle.Position) < clearance)
            {
                return false;
            }
        }

        return true;
    }

    private static float ResolveSpreadDegrees(PositioningIntentKind intent, int slotCount, TacticContext context)
    {
        if (slotCount == 1)
        {
            return 0f;
        }

        var frontlineSpread = Lerp(140f, 210f, context.FrontSpacingBias);
        var flankSpread = Lerp(28f, 58f, TacticContext.Clamp01(context.Width));
        var compactPenalty = context.Compactness * 35f;
        var spread = intent switch
        {
            PositioningIntentKind.FlankLeft or PositioningIntentKind.FlankRight => flankSpread - (compactPenalty * 0.35f),
            PositioningIntentKind.BacklineDive => MathF.Max(flankSpread, 54f) - (compactPenalty * 0.25f),
            _ => frontlineSpread - compactPenalty,
        };
        return Math.Clamp(spread, 18f, 230f);
    }

    private static float ResolveCenterDegrees(TeamSide actorSide, PositioningIntentKind intent, TacticContext context)
    {
        var center = intent switch
        {
            PositioningIntentKind.FlankLeft => actorSide == TeamSide.Ally ? 125f : 55f,
            PositioningIntentKind.FlankRight => actorSide == TeamSide.Ally ? 235f : -55f,
            PositioningIntentKind.BacklineDive => actorSide == TeamSide.Ally ? 0f : 180f,
            _ => actorSide == TeamSide.Ally ? 180f : 0f,
        };
        var sideSign = actorSide == TeamSide.Ally ? 1f : -1f;
        return center + (context.FlankBias * sideSign * 20f);
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + ((to - from) * TacticContext.Clamp01(t));
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
