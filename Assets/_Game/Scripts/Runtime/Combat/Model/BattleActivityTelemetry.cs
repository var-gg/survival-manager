using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SM.Core.Contracts;

namespace SM.Combat.Model;

public sealed record BattleActivityTelemetrySnapshot(
    IReadOnlyDictionary<TeamSide, float> MeanPairwiseDistanceByTeam,
    IReadOnlyDictionary<string, float> YSpreadStdByRow,
    float TargetEntropy,
    IReadOnlyDictionary<string, float> FocusHeatPerTarget,
    int OverfocusEvents,
    float TankAbsorbedFocusHeat,
    float StationaryBetweenAttacksRatio,
    int PostAttackRepositionCount,
    int TargetSwitchCount,
    string ReplayHash);

public sealed class BattleActivityTelemetryAccumulator
{
    private readonly Dictionary<TeamSide, float> _pairwiseDistanceSum = new();
    private readonly Dictionary<TeamSide, int> _pairwiseSampleCount = new();
    private readonly Dictionary<string, float> _rowYSpreadSum = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _rowYSpreadSampleCount = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _focusHeatPerTarget = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _targetCommitCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CombatVector2> _lastAttackPositionByActor = new(StringComparer.Ordinal);

    private int _stationaryAttackIntervals;
    private int _totalAttackIntervals;

    public int OverfocusEvents { get; private set; }
    public float TankAbsorbedFocusHeat { get; private set; }
    public int PostAttackRepositionCount { get; private set; }
    public int TargetSwitchCount { get; private set; }

    public void RecordStep(BattleState state)
    {
        RecordPairwiseDistance(state, TeamSide.Ally);
        RecordPairwiseDistance(state, TeamSide.Enemy);
        RecordRowYSpread(state, TeamSide.Ally, FormationLine.Frontline);
        RecordRowYSpread(state, TeamSide.Ally, FormationLine.Midline);
        RecordRowYSpread(state, TeamSide.Ally, FormationLine.Backline);
        RecordRowYSpread(state, TeamSide.Enemy, FormationLine.Frontline);
        RecordRowYSpread(state, TeamSide.Enemy, FormationLine.Midline);
        RecordRowYSpread(state, TeamSide.Enemy, FormationLine.Backline);
    }

    public void RecordTargetEvent(BattleState state, UnitSnapshot actor, UnitSnapshot? previousTarget, UnitSnapshot nextTarget)
    {
        if (nextTarget.Side == actor.Side)
        {
            return;
        }

        var targetId = nextTarget.Id.Value;
        Increment(_targetCommitCounts, targetId, 1);
        Increment(_focusHeatPerTarget, targetId, 1f);

        if (previousTarget != null && !string.Equals(previousTarget.Id.Value, targetId, StringComparison.Ordinal))
        {
            TargetSwitchCount++;
        }

        var currentFocus = state.GetOpponents(nextTarget.Side)
            .Count(unit => unit.IsAlive
                           && unit.Id != actor.Id
                           && (unit.CurrentTargetId == nextTarget.Id || unit.PendingTargetId == nextTarget.Id));
        if (currentFocus + 1 >= 3)
        {
            OverfocusEvents++;
        }

        if (nextTarget.Behavior.FormationLine == FormationLine.Frontline
            || string.Equals(nextTarget.Definition.ClassId, "vanguard", StringComparison.Ordinal))
        {
            TankAbsorbedFocusHeat += 1f;
        }
    }

    public void RecordActionResolved(UnitSnapshot actor, UnitSnapshot? target, BattleActionType actionType)
    {
        if (target == null || target.Side == actor.Side)
        {
            return;
        }

        if (actionType is not (BattleActionType.BasicAttack or BattleActionType.ActiveSkill))
        {
            return;
        }

        if (_lastAttackPositionByActor.TryGetValue(actor.Id.Value, out var previous))
        {
            _totalAttackIntervals++;
            if (previous.DistanceTo(actor.Position) <= Math.Max(0.08f, actor.NavigationRadius * 0.25f))
            {
                _stationaryAttackIntervals++;
            }
        }

        _lastAttackPositionByActor[actor.Id.Value] = actor.Position;
    }

    public void RecordPostAttackReposition()
    {
        PostAttackRepositionCount++;
    }

    public BattleActivityTelemetrySnapshot BuildSnapshot(BattleState state)
    {
        var meanPairwise = new Dictionary<TeamSide, float>
        {
            [TeamSide.Ally] = ResolveMean(_pairwiseDistanceSum, _pairwiseSampleCount, TeamSide.Ally),
            [TeamSide.Enemy] = ResolveMean(_pairwiseDistanceSum, _pairwiseSampleCount, TeamSide.Enemy),
        };
        var rowSpread = _rowYSpreadSum.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToDictionary(
                key => key,
                key => _rowYSpreadSum[key] / Math.Max(1, _rowYSpreadSampleCount[key]),
                StringComparer.Ordinal);
        var focusHeat = _focusHeatPerTarget
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var stationaryRatio = _totalAttackIntervals <= 0
            ? 0f
            : (float)_stationaryAttackIntervals / _totalAttackIntervals;
        var entropy = ComputeEntropy(_targetCommitCounts.Values);
        var snapshot = new BattleActivityTelemetrySnapshot(
            meanPairwise,
            rowSpread,
            entropy,
            focusHeat,
            OverfocusEvents,
            TankAbsorbedFocusHeat,
            stationaryRatio,
            PostAttackRepositionCount,
            TargetSwitchCount,
            string.Empty);
        return snapshot with { ReplayHash = ComputeReplayHash(state, snapshot) };
    }

    private void RecordPairwiseDistance(BattleState state, TeamSide side)
    {
        var units = state.GetTeam(side).Where(unit => unit.IsAlive).OrderBy(unit => unit.Id.Value, StringComparer.Ordinal).ToList();
        if (units.Count < 2)
        {
            return;
        }

        var sum = 0f;
        var count = 0;
        for (var i = 0; i < units.Count; i++)
        {
            for (var j = i + 1; j < units.Count; j++)
            {
                sum += units[i].Position.DistanceTo(units[j].Position);
                count++;
            }
        }

        if (count <= 0)
        {
            return;
        }

        Increment(_pairwiseDistanceSum, side, sum / count);
        Increment(_pairwiseSampleCount, side, 1);
    }

    private void RecordRowYSpread(BattleState state, TeamSide side, FormationLine row)
    {
        var yValues = state.GetTeam(side)
            .Where(unit => unit.IsAlive && unit.Behavior.FormationLine == row)
            .Select(unit => unit.Position.Y)
            .ToList();
        if (yValues.Count < 2)
        {
            return;
        }

        var mean = yValues.Sum() / yValues.Count;
        var variance = yValues.Sum(value => (value - mean) * (value - mean)) / yValues.Count;
        var key = $"{side}:{row}";
        Increment(_rowYSpreadSum, key, MathF.Sqrt(variance));
        Increment(_rowYSpreadSampleCount, key, 1);
    }

    private static float ResolveMean(
        IReadOnlyDictionary<TeamSide, float> sums,
        IReadOnlyDictionary<TeamSide, int> counts,
        TeamSide side)
    {
        return sums.TryGetValue(side, out var sum) && counts.TryGetValue(side, out var count) && count > 0
            ? sum / count
            : 0f;
    }

    private static float ComputeEntropy(IEnumerable<int> counts)
    {
        var values = counts.Where(count => count > 0).ToList();
        var total = values.Sum();
        if (total <= 0)
        {
            return 0f;
        }

        var entropy = 0f;
        foreach (var count in values)
        {
            var p = (float)count / total;
            entropy -= p * (MathF.Log(p) / MathF.Log(2f));
        }

        return entropy;
    }

    private static string ComputeReplayHash(BattleState state, BattleActivityTelemetrySnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.Append(state.Seed).Append('|')
            .Append(state.StepIndex).Append('|')
            .Append(Format(state.ElapsedSeconds)).Append('|')
            .Append(Format(snapshot.TargetEntropy)).Append('|')
            .Append(snapshot.OverfocusEvents).Append('|')
            .Append(Format(snapshot.TankAbsorbedFocusHeat)).Append('|')
            .Append(Format(snapshot.StationaryBetweenAttacksRatio)).Append('|')
            .Append(snapshot.PostAttackRepositionCount).Append('|')
            .Append(snapshot.TargetSwitchCount).Append('|');

        foreach (var pair in snapshot.MeanPairwiseDistanceByTeam.OrderBy(pair => pair.Key))
        {
            builder.Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('|');
        }

        foreach (var pair in snapshot.YSpreadStdByRow.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('|');
        }

        foreach (var pair in snapshot.FocusHeatPerTarget.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('|');
        }

        foreach (var unit in state.AllUnits.OrderBy(unit => unit.Id.Value, StringComparer.Ordinal))
        {
            builder.Append(unit.Id.Value).Append(':')
                .Append(Format(unit.CurrentHealth)).Append(':')
                .Append(Format(unit.Barrier)).Append(':')
                .Append(unit.IsAlive).Append(':')
                .Append(Format(unit.Position.X)).Append(':')
                .Append(Format(unit.Position.Y)).Append('|');
        }

        return StableHash(builder.ToString());
    }

    private static string StableHash(string input)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var ch in input)
            {
                hash ^= ch;
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void Increment<TKey>(IDictionary<TKey, float> dictionary, TKey key, float value)
        where TKey : notnull
    {
        dictionary[key] = dictionary.TryGetValue(key, out var current)
            ? current + value
            : value;
    }

    private static void Increment<TKey>(IDictionary<TKey, int> dictionary, TKey key, int value)
        where TKey : notnull
    {
        dictionary[key] = dictionary.TryGetValue(key, out var current)
            ? current + value
            : value;
    }
}
