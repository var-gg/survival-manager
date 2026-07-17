using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.HeadlessMetrics;

/// <summary>
/// BattleSimulationStep을 읽기만 하며 첫 적대 접촉, 이동량, 접근 stall, pathing 재계획을 축약한다.
/// sim state와 RNG에는 쓰지 않는다.
/// </summary>
public sealed class PlacementAttributionTraceCollector
{
    private const double MovementEpsilon = 0.01d;

    private readonly Dictionary<string, UnitTraceState> _previous = new(StringComparer.Ordinal);
    private int _firstContactTick = -1;
    private double _firstContactDistance = -1d;
    private int _pathingReplanCount;
    private int _approachSamples;
    private int _approachStallSamples;
    private double _allyTravelDistance;

    public void Observe(BattleSimulationStep step)
    {
        if (step == null)
        {
            throw new ArgumentNullException(nameof(step));
        }

        var allies = step.Units
            .Where(unit => unit.Side == TeamSide.Ally && unit.EntityKind == CombatEntityKind.RosterUnit)
            .OrderBy(unit => unit.Id, StringComparer.Ordinal)
            .ToArray();
        foreach (var unit in allies)
        {
            var moved = 0d;
            if (_previous.TryGetValue(unit.Id, out var previous))
            {
                moved = Distance(previous.X, previous.Y, unit.Position.X, unit.Position.Y);
                _allyTravelDistance += moved;
                if (unit.PositioningIntentRevision > previous.PositioningIntentRevision
                    && IsPathingReason(unit.PositioningReplanReason))
                {
                    _pathingReplanCount++;
                }
            }

            if (unit.IsAlive
                && unit.ActionState == CombatActionState.Approach
                && !string.IsNullOrWhiteSpace(unit.TargetId))
            {
                _approachSamples++;
                if (_previous.ContainsKey(unit.Id) && moved <= MovementEpsilon)
                {
                    _approachStallSamples++;
                }
            }

            _previous[unit.Id] = new UnitTraceState(
                unit.Position.X,
                unit.Position.Y,
                unit.PositioningIntentRevision);
        }

        ObserveFirstHostileContact(step);
    }

    public PlacementTraceSummary Complete(BattleResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var activity = result.ActivityTelemetry;
        var firstTargetSignature = ResolveFirstTargetSignature(result.TelemetryEvents);
        return new PlacementTraceSummary(
            _firstContactTick,
            Round(_firstContactDistance),
            firstTargetSignature,
            Math.Max(0, activity?.TargetSwitchCount ?? 0),
            _pathingReplanCount,
            Round(_allyTravelDistance),
            _approachSamples == 0 ? 0d : Round(_approachStallSamples / (double)_approachSamples));
    }

    private void ObserveFirstHostileContact(BattleSimulationStep step)
    {
        if (_firstContactTick >= 0 || step.CombatEventIntents == null)
        {
            return;
        }

        var units = step.Units.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
        foreach (var intent in step.CombatEventIntents
                     .Where(value => value.Status == CombatEventIntentStatus.Contacted)
                     .OrderBy(value => value.ContactTick)
                     .ThenBy(value => value.ActionInstanceId.Value))
        {
            if (!units.TryGetValue(intent.ActorId.Value, out var actor)
                || actor.Side != TeamSide.Ally)
            {
                continue;
            }

            foreach (var contact in (intent.Contacts ?? Array.Empty<BattleContactIntent>())
                         .Where(value => value.TargetId != null)
                         .OrderBy(value => value.ContactIndex))
            {
                if (!units.TryGetValue(contact.TargetId!.Value.Value, out var target)
                    || target.Side == actor.Side)
                {
                    continue;
                }

                var centerDistance = Distance(
                    actor.Position.X,
                    actor.Position.Y,
                    target.Position.X,
                    target.Position.Y);
                _firstContactTick = contact.ContactTick;
                _firstContactDistance = Math.Max(
                    0d,
                    centerDistance - actor.NavigationRadius - target.NavigationRadius);
                return;
            }
        }
    }

    private static string ResolveFirstTargetSignature(IReadOnlyList<TelemetryEventRecord>? telemetry)
    {
        var targetEvents = (telemetry ?? Array.Empty<TelemetryEventRecord>())
            .Where(value => value.EventKind == TelemetryEventKind.TargetAcquired
                            && value.Actor != null
                            && value.Target != null
                            && value.Actor.SideIndex == (int)TeamSide.Ally
                            && value.Target.SideIndex != value.Actor.SideIndex)
            .OrderBy(value => value.TimeSeconds)
            .ThenBy(value => value.Actor!.UnitInstanceId, StringComparer.Ordinal)
            .ThenBy(value => value.Target!.UnitInstanceId, StringComparer.Ordinal)
            .ToArray();
        if (targetEvents.Length == 0)
        {
            return string.Empty;
        }

        var firstTime = targetEvents[0].TimeSeconds;
        return string.Join(
            "|",
            targetEvents.Where(value => Math.Abs(value.TimeSeconds - firstTime) <= 0.000001f)
                .Select(value => $"{value.Actor!.UnitInstanceId}->{value.Target!.UnitInstanceId}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static bool IsPathingReason(ReevaluationReason reason)
        => reason is ReevaluationReason.SlotLost or ReevaluationReason.RangeBreak or ReevaluationReason.TargetMoved;

    private static double Distance(double leftX, double leftY, double rightX, double rightY)
    {
        var dx = leftX - rightX;
        var dy = leftY - rightY;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double Round(double value)
        => value < 0d ? value : Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private sealed record UnitTraceState(double X, double Y, int PositioningIntentRevision);
}
