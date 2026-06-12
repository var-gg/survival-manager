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
    float ClusterCohesionIndex,
    IReadOnlyDictionary<string, float> BuffCoverageHistogramByType,
    IReadOnlyDictionary<string, float> BuffEfficacyBonusByType,
    int ClusterBuffOvercapEvents,
    int BuffMissedByDistanceCount,
    float AoeCandidateClusterScore,
    IReadOnlyDictionary<string, float> AoeCatchCountHistogram,
    int CleaveCatchCount,
    int ChainJumpCount,
    int KnockbackDispersalEvents,
    float ReclusterLatencyMs,
    float FocusDamageContribution,
    float BuffValueContribution,
    float AoeCostTaken,
    float ClusterTradeoffNetValue,
    float ScreenMitigationContribution,
    int ScreenAbsorbCount,
    int ScreenDeterrenceCount,
    float FlankDamageContribution,
    int FlankStrikeCount,
    int RearStrikeCount,
    int BacklineDiveKillCount,
    int SaveMomentCount,
    int FocusMarkSwitchCount,
    int FrontlineBreachCount,
    float HandednessSlotPreferenceHitRatio,
    IReadOnlyDictionary<string, float> HandednessLateralResetSideHistogram,
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
    private readonly Dictionary<string, float> _buffCoverageHistogramByType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _buffEfficacyBonusByType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _aoeCatchCountHistogram = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _handednessLateralResetSideHistogram = new(StringComparer.Ordinal);
    // 차단(우회 유도) dedup: 같은 공격자가 지속 차단당해도 액터당 1회만 센다 — 카운터가 "순간"을 세게.
    private readonly HashSet<string> _screenDeterredActors = new(StringComparer.Ordinal);
    // 구출 episode dedup: 빈사 1회당 1회 — 회복(≥reset 문턱) 후에만 같은 유닛이 다시 카운트된다.
    private readonly HashSet<string> _saveCreditedTargets = new(StringComparer.Ordinal);

    private int _stationaryAttackIntervals;
    private int _totalAttackIntervals;
    private int _clusterTradeoffSamples;
    private int _handednessSlotPreferenceSamples;
    private int _handednessSlotPreferenceHits;

    public int OverfocusEvents { get; private set; }
    public float TankAbsorbedFocusHeat { get; private set; }
    public int PostAttackRepositionCount { get; private set; }
    public int TargetSwitchCount { get; private set; }
    public float ClusterCohesionIndex { get; private set; }
    public int ClusterBuffOvercapEvents { get; private set; }
    public int BuffMissedByDistanceCount { get; private set; }
    public float AoeCandidateClusterScore { get; private set; }
    public int CleaveCatchCount { get; private set; }
    public int ChainJumpCount { get; private set; }
    public int KnockbackDispersalEvents { get; private set; }
    public float ReclusterLatencyMs { get; private set; }
    public float FocusDamageContribution { get; private set; }
    public float BuffValueContribution { get; private set; }
    public float AoeCostTaken { get; private set; }
    public float ClusterTradeoffNetValue { get; private set; }
    public float ScreenMitigationContribution { get; private set; }
    public int ScreenAbsorbCount { get; private set; }
    public int ScreenDeterrenceCount { get; private set; }
    public float FlankDamageContribution { get; private set; }
    public int FlankStrikeCount { get; private set; }
    public int RearStrikeCount { get; private set; }
    public int BacklineDiveKillCount { get; private set; }
    public int SaveMomentCount { get; private set; }
    public int FocusMarkSwitchCount { get; private set; }
    public int FrontlineBreachCount { get; private set; }

    /// <summary>Phase 2 블랙보드: 팀 FocusMark가 다른 표적으로 넘어간 횟수(초기 지정은 제외).</summary>
    public void RecordFocusMarkSwitch()
    {
        FocusMarkSwitchCount++;
    }

    /// <summary>Phase 2 블랙보드: 새 breacher가 아군 전선 뒤 후열 위협 반경에 진입한 횟수.</summary>
    public void RecordFrontlineBreach()
    {
        FrontlineBreachCount++;
    }

    public void RecordStep(BattleState state)
    {
        // 구출 episode 해제: reset 문턱 이상으로 회복된 유닛은 다음 빈사에서 다시 카운트된다.
        foreach (var unit in state.AllUnits)
        {
            if (unit.IsAlive && unit.HealthRatio >= SaveMomentEpisodeResetRatio)
            {
                _saveCreditedTargets.Remove(unit.Id.Value);
            }
        }

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

    public void RecordFocusDamageContribution(float value)
    {
        var contribution = Math.Max(0f, value);
        FocusDamageContribution += contribution;
        ClusterTradeoffNetValue += contribution;
    }

    public void RecordKnockbackDispersalEvent()
    {
        KnockbackDispersalEvents++;
    }

    /// <summary>P0 차단: 스크린이 멀쩡한 후열이 흡수한 피해 기여(경감 전후 차).</summary>
    public void RecordScreenAbsorb(float contribution)
    {
        ScreenMitigationContribution += Math.Max(0f, contribution);
        ScreenAbsorbCount++;
    }

    /// <summary>
    /// P0 차단(우회 유도): 타게팅이 스크린 페널티 때문에 후열 대신 다른 표적으로 돌아선 순간.
    /// 차단의 주 발현은 피해 흡수가 아니라 "후열을 노리지 못하게 만드는 것"이라 흡수만 세면 차단이
    /// 건강하게 작동할수록 0이 된다(T1 sweep 측정 0/24). 같은 공격자는 1회만 — 지속 차단을 틱마다
    /// 다시 세지 않는다.
    /// </summary>
    public void RecordScreenDeterrence(string actorId)
    {
        if (_screenDeterredActors.Add(actorId))
        {
            ScreenDeterrenceCount++;
        }
    }

    /// <summary>P0 측면: 측면/후방 공격이 추가한 피해 기여.</summary>
    public void RecordFlankStrike(float contribution, bool isRear)
    {
        FlankDamageContribution += Math.Max(0f, contribution);
        FlankStrikeCount++;
        if (isRear)
        {
            RearStrikeCount++;
        }
    }

    /// <summary>cinematic detector: Dive 의도 유닛이 후열을 처치.</summary>
    public void RecordBacklineDiveKill()
    {
        BacklineDiveKillCount++;
    }

    /// <summary>구출 episode 해제 문턱 — 트리거(25%)보다 높게 잡아 경계 진동을 막는다.</summary>
    private const float SaveMomentEpisodeResetRatio = 0.35f;

    /// <summary>
    /// cinematic detector: 빈사(25% 미만) 아군을 회복으로 구출. episode 단위 — 같은 빈사 상태에
    /// 힐이 여러 번 닿아도 1회만 세고, 대상이 reset 문턱(35%) 이상으로 회복된 뒤 다시 빈사가 되면
    /// 새 episode로 카운트한다. 카운터가 "힐 틱"이 아니라 "구출 순간"을 세게(T1 sweep에서 mean 21
    /// 인플레이션 측정). true를 반환할 때만 호출자가 구출 note/accent를 발행한다.
    /// </summary>
    public bool TryBeginSaveMomentEpisode(string targetId)
    {
        if (!_saveCreditedTargets.Add(targetId))
        {
            return false;
        }

        SaveMomentCount++;
        return true;
    }

    public void RecordHandednessSlotPreference(bool preferenceHit, bool hasPreference)
    {
        if (!hasPreference)
        {
            return;
        }

        _handednessSlotPreferenceSamples++;
        if (preferenceHit)
        {
            _handednessSlotPreferenceHits++;
        }
    }

    public void RecordHandednessLateralReset(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        Increment(_handednessLateralResetSideHistogram, label, 1f);
    }

    public void RecordClusterTradeoff(ClusterTradeoffTelemetryFrame frame)
    {
        _clusterTradeoffSamples++;
        ClusterCohesionIndex += frame.ClusterCohesionIndex;
        ClusterBuffOvercapEvents += frame.ClusterBuffOvercapEvents;
        BuffMissedByDistanceCount += frame.BuffMissedByDistanceCount;
        AoeCandidateClusterScore += frame.AoeCandidateClusterScore;
        CleaveCatchCount += frame.CleaveCatchCount;
        ChainJumpCount += frame.ChainJumpCount;
        KnockbackDispersalEvents += frame.KnockbackDispersalEvents;
        ReclusterLatencyMs += frame.ReclusterLatencyMs;
        BuffValueContribution += frame.BuffValueContribution;
        AoeCostTaken += frame.AoeCostTaken;
        ClusterTradeoffNetValue += frame.ClusterTradeoffNetValue;

        foreach (var pair in frame.BuffCoverageHistogramByType)
        {
            Increment(_buffCoverageHistogramByType, pair.Key, pair.Value);
        }

        foreach (var pair in frame.BuffEfficacyBonusByType)
        {
            Increment(_buffEfficacyBonusByType, pair.Key, pair.Value);
        }

        foreach (var pair in frame.AoeCatchCountHistogram)
        {
            Increment(_aoeCatchCountHistogram, pair.Key, pair.Value);
        }
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
            ResolveSampleMean(ClusterCohesionIndex),
            ResolveSampleDictionary(_buffCoverageHistogramByType),
            ResolveSampleDictionary(_buffEfficacyBonusByType),
            ClusterBuffOvercapEvents,
            BuffMissedByDistanceCount,
            ResolveSampleMean(AoeCandidateClusterScore),
            ResolveSampleDictionary(_aoeCatchCountHistogram),
            CleaveCatchCount,
            ChainJumpCount,
            KnockbackDispersalEvents,
            ResolveSampleMean(ReclusterLatencyMs),
            FocusDamageContribution,
            BuffValueContribution,
            AoeCostTaken,
            ClusterTradeoffNetValue,
            ScreenMitigationContribution,
            ScreenAbsorbCount,
            ScreenDeterrenceCount,
            FlankDamageContribution,
            FlankStrikeCount,
            RearStrikeCount,
            BacklineDiveKillCount,
            SaveMomentCount,
            FocusMarkSwitchCount,
            FrontlineBreachCount,
            _handednessSlotPreferenceSamples <= 0
                ? 0f
                : (float)_handednessSlotPreferenceHits / _handednessSlotPreferenceSamples,
            _handednessLateralResetSideHistogram
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
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
            .Append(snapshot.TargetSwitchCount).Append('|')
            .Append(Format(snapshot.ClusterCohesionIndex)).Append('|')
            .Append(snapshot.ClusterBuffOvercapEvents).Append('|')
            .Append(snapshot.BuffMissedByDistanceCount).Append('|')
            .Append(Format(snapshot.AoeCandidateClusterScore)).Append('|')
            .Append(snapshot.CleaveCatchCount).Append('|')
            .Append(snapshot.ChainJumpCount).Append('|')
            .Append(snapshot.KnockbackDispersalEvents).Append('|')
            .Append(Format(snapshot.ReclusterLatencyMs)).Append('|')
            .Append(Format(snapshot.FocusDamageContribution)).Append('|')
            .Append(Format(snapshot.BuffValueContribution)).Append('|')
            .Append(Format(snapshot.AoeCostTaken)).Append('|')
            .Append(Format(snapshot.ClusterTradeoffNetValue)).Append('|')
            .Append(Format(snapshot.ScreenMitigationContribution)).Append('|')
            .Append(snapshot.ScreenAbsorbCount).Append('|')
            .Append(snapshot.ScreenDeterrenceCount).Append('|')
            .Append(Format(snapshot.FlankDamageContribution)).Append('|')
            .Append(snapshot.FlankStrikeCount).Append('|')
            .Append(snapshot.RearStrikeCount).Append('|')
            .Append(snapshot.BacklineDiveKillCount).Append('|')
            .Append(snapshot.SaveMomentCount).Append('|')
            .Append(snapshot.FocusMarkSwitchCount).Append('|')
            .Append(snapshot.FrontlineBreachCount).Append('|');
        builder.Append(Format(snapshot.HandednessSlotPreferenceHitRatio)).Append('|');

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

        foreach (var pair in snapshot.BuffCoverageHistogramByType.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("buff_cov:").Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('|');
        }

        foreach (var pair in snapshot.BuffEfficacyBonusByType.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("buff_bonus:").Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('|');
        }

        foreach (var pair in snapshot.AoeCatchCountHistogram.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("aoe_catch:").Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('|');
        }

        foreach (var pair in snapshot.HandednessLateralResetSideHistogram.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("hand_reset:").Append(pair.Key).Append('=').Append(Format(pair.Value)).Append('|');
        }

        foreach (var unit in state.AllUnits.OrderBy(unit => unit.Id.Value, StringComparer.Ordinal))
        {
            builder.Append(unit.Id.Value).Append(':')
                .Append(unit.Definition.DominantHand).Append(':')
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

    private float ResolveSampleMean(float sum)
    {
        return _clusterTradeoffSamples <= 0 ? 0f : sum / _clusterTradeoffSamples;
    }

    private Dictionary<string, float> ResolveSampleDictionary(IReadOnlyDictionary<string, float> source)
    {
        var divisor = Math.Max(1, _clusterTradeoffSamples);
        return source
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value / divisor, StringComparer.Ordinal);
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
