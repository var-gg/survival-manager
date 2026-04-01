using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class BattleTelemetryAnalysisService
{
    private const float RollingWindowSeconds = 1f;
    private const float MajorCollisionWindowSeconds = 0.2f;

    public static ReadabilityGateConfig DefaultReadabilityGateConfig { get; } = new();

    public static ReadabilityReport BuildReadabilityReport(
        IReadOnlyList<TelemetryEventRecord> telemetryEvents,
        int combatantCount,
        ReadabilityGateConfig? config = null)
    {
        config ??= DefaultReadabilityGateConfig;
        var ordered = telemetryEvents
            .Where(record => record != null)
            .OrderBy(record => record.TimeSeconds)
            .ToList();
        var aggregated = BuildVisualPackets(ordered, config);

        var totalDamage = ordered.Where(record => record.EventKind == TelemetryEventKind.DamageApplied).Sum(record => Math.Max(0f, record.ValueA));
        var unexplainedDamage = ordered
            .Where(record => record.EventKind == TelemetryEventKind.DamageApplied && IsUnexplained(record))
            .Sum(record => Math.Max(0f, record.ValueA));
        var totalHealing = ordered.Where(record => record.EventKind == TelemetryEventKind.HealingApplied).Sum(record => Math.Max(0f, record.ValueA));
        var unexplainedHealing = ordered
            .Where(record => record.EventKind == TelemetryEventKind.HealingApplied && IsUnexplained(record))
            .Sum(record => Math.Max(0f, record.ValueA));

        var majorPackets = aggregated.Where(packet => packet.Salience is SalienceClass.Major or SalienceClass.Critical).ToList();
        var offscreenMajor = majorPackets.Count(packet => packet.IsOffscreen);
        var nonAmbient = aggregated.Where(packet => packet.Salience != SalienceClass.Ambient && packet.Salience != SalienceClass.None).ToList();

        var report = new ReadabilityReport
        {
            UnexplainedDamageRatio = Ratio(unexplainedDamage, totalDamage),
            UnexplainedHealingRatio = Ratio(unexplainedHealing, totalHealing),
            OffscreenMajorEventRatio = Ratio(offscreenMajor, Math.Max(1, majorPackets.Count)),
            TargetSwitchesPer10sP95 = ComputeTargetSwitchesPer10sP95(ordered),
            IdleGapP95Seconds = ComputeIdleGapP95(nonAmbient),
            TimeToFirstMajorActionP50 = ordered
                .Where(record => record.Explain?.Salience is SalienceClass.Major or SalienceClass.Critical)
                .Select(record => record.TimeSeconds)
                .DefaultIfEmpty(0f)
                .First(),
            MajorEventCollisionRate = ComputeMajorCollisionRate(majorPackets),
            SalienceWeightPer1sP95 = ComputeSalienceWeightPer1sP95(aggregated),
            StatusChipOverflowRate = ComputeStatusChipOverflowRate(ordered, config),
            FloatingTextBurstOverflowRate = ComputeFloatingBurstOverflowRate(aggregated, config),
        };

        var violations = new List<ReadabilityViolationKind>();
        if (report.UnexplainedDamageRatio > config.UnexplainedDamageRatioMax)
        {
            violations.Add(ReadabilityViolationKind.UnexplainedDamage);
        }

        if (report.UnexplainedHealingRatio > config.UnexplainedHealingRatioMax)
        {
            violations.Add(ReadabilityViolationKind.UnexplainedHealing);
        }

        if (report.OffscreenMajorEventRatio > config.OffscreenMajorEventRatioMax)
        {
            violations.Add(ReadabilityViolationKind.OffscreenMajorEvent);
        }

        if (report.TargetSwitchesPer10sP95 > config.TargetSwitchesPer10sP95Max)
        {
            violations.Add(ReadabilityViolationKind.TargetThrash);
        }

        if (report.IdleGapP95Seconds > config.IdleGapP95MaxSeconds)
        {
            violations.Add(ReadabilityViolationKind.IdleGapTooLong);
        }

        if (report.TimeToFirstMajorActionP50 < config.TimeToFirstMajorActionP50Min
            || report.TimeToFirstMajorActionP50 > config.TimeToFirstMajorActionP50Max)
        {
            violations.Add(ReadabilityViolationKind.IdleGapTooLong);
        }

        if (report.MajorEventCollisionRate > config.MajorEventCollisionRateMax)
        {
            violations.Add(ReadabilityViolationKind.MajorEventCollision);
        }

        if (report.SalienceWeightPer1sP95 > config.ResolveScaledSalienceBudget(combatantCount))
        {
            violations.Add(ReadabilityViolationKind.SalienceOverload);
        }

        if (report.StatusChipOverflowRate > 0.05f)
        {
            violations.Add(ReadabilityViolationKind.StatusChipOverflow);
        }

        if (report.FloatingTextBurstOverflowRate > 0.05f)
        {
            violations.Add(ReadabilityViolationKind.FloatingTextBurstOverflow);
        }

        var systemDamageShare = ordered
            .Where(record => record.EventKind == TelemetryEventKind.DamageApplied && record.Explain?.SourceKind == ExplainedSourceKind.SystemRule)
            .Sum(record => Math.Max(0f, record.ValueA));
        if (Ratio(systemDamageShare, totalDamage) > 0.08f)
        {
            violations.Add(ReadabilityViolationKind.ProcChainOpacity);
        }

        report.Violations = violations.Distinct().ToArray();
        return report;
    }

    public static BattleSummaryReport BuildBattleSummary(
        BattleResult result,
        IReadOnlyList<TelemetryEventRecord> telemetryEvents,
        TelemetryContext? context,
        ReadabilityReport readability)
    {
        var ordered = telemetryEvents.OrderBy(record => record.TimeSeconds).ToList();
        var totalDamage = ordered
            .Where(record => record.EventKind == TelemetryEventKind.DamageApplied)
            .GroupBy(record => record.Explain?.SourceDisplayName ?? string.Empty)
            .Select(group => new { Name = group.Key, Value = group.Sum(record => Math.Max(0f, record.ValueA)) })
            .OrderByDescending(group => group.Value)
            .ThenBy(group => group.Name, StringComparer.Ordinal)
            .ToList();
        var totalDamageValue = totalDamage.Sum(entry => entry.Value);
        var firstDamage = ordered
            .Where(record => record.EventKind == TelemetryEventKind.DamageApplied)
            .Select(record => record.TimeSeconds)
            .DefaultIfEmpty(result.DurationSeconds)
            .First();
        var firstMajor = ordered
            .Where(record => record.Explain?.Salience is SalienceClass.Major or SalienceClass.Critical)
            .Select(record => record.TimeSeconds)
            .DefaultIfEmpty(result.DurationSeconds)
            .First();
        var deaths = ordered.Where(record => record.EventKind == TelemetryEventKind.KillCredited || record.EventKind == TelemetryEventKind.UnitDied).ToList();

        return new BattleSummaryReport
        {
            ScenarioId = context?.ScenarioId ?? string.Empty,
            Seed = context?.Seed ?? 0,
            BattleDurationSeconds = result.DurationSeconds,
            WinnerSideIndex = (int)result.Winner,
            TimeToFirstDamageSeconds = firstDamage,
            TimeToFirstMajorActionSeconds = firstMajor,
            TimeoutOccurred = result.StepCount >= 300,
            UnexplainedDamageRatio = readability.UnexplainedDamageRatio,
            UnexplainedHealingRatio = readability.UnexplainedHealingRatio,
            OffscreenMajorEventRatio = readability.OffscreenMajorEventRatio,
            TargetSwitchesPer10sP95 = readability.TargetSwitchesPer10sP95,
            IdleGapP95Seconds = readability.IdleGapP95Seconds,
            MajorEventCollisionRate = readability.MajorEventCollisionRate,
            SalienceWeightPer1sP95 = readability.SalienceWeightPer1sP95,
            OverkillRatio = ComputeOverkillRatio(ordered),
            TopDamageShare = totalDamageValue <= 0f ? 0f : totalDamage.FirstOrDefault()?.Value / totalDamageValue ?? 0f,
            DeadBeforeFirstMajorActionRate = Ratio(
                deaths.Count(record => record.TimeSeconds <= firstMajor + 0.001f),
                Math.Max(1, result.FinalUnits.Count)),
            TopDamageSources = totalDamage
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Take(5)
                .Select(entry => $"{entry.Name}:{entry.Value.ToString("0.##", CultureInfo.InvariantCulture)}")
                .ToArray(),
            TopDecisionReasons = ordered
                .Where(record => record.Explain != null)
                .GroupBy(record => record.Explain!.ReasonCode)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(5)
                .Select(group => $"{group.Key}:{group.Count()}")
                .ToArray(),
            DecisiveMoments = BuildDecisiveMoments(ordered),
        };
    }

    private static string[] BuildDecisiveMoments(IReadOnlyList<TelemetryEventRecord> ordered)
    {
        var moments = new List<string>();
        var firstDeath = ordered.FirstOrDefault(record => record.EventKind == TelemetryEventKind.KillCredited || record.EventKind == TelemetryEventKind.UnitDied);
        if (firstDeath != null)
        {
            moments.Add($"first_death:{firstDeath.TimeSeconds:0.##}:{firstDeath.Target?.UnitBlueprintId ?? firstDeath.Actor?.UnitBlueprintId ?? "unknown"}");
        }

        var firstCc = ordered.FirstOrDefault(record =>
            record.EventKind == TelemetryEventKind.GuardBroken
            || (record.EventKind == TelemetryEventKind.StatusApplied && record.StatusId is "stun" or "root" or "silence"));
        if (firstCc != null)
        {
            moments.Add($"first_cc_or_guard:{firstCc.TimeSeconds:0.##}:{firstCc.StatusId}");
        }

        var swingWindow = ordered
            .GroupBy(record => MathF.Floor(record.TimeSeconds))
            .Select(group => new
            {
                Time = group.Key,
                Weight = group.Sum(record => BattleTelemetryRecorder.GetSalienceWeight(record.Explain?.Salience ?? SalienceClass.None)),
            })
            .OrderByDescending(group => group.Weight)
            .ThenBy(group => group.Time)
            .FirstOrDefault();
        if (swingWindow != null)
        {
            moments.Add($"salience_swing:{swingWindow.Time:0.##}:{swingWindow.Weight:0.##}");
        }

        return moments.Take(3).ToArray();
    }

    private static float ComputeOverkillRatio(IReadOnlyList<TelemetryEventRecord> ordered)
    {
        var damage = ordered.Where(record => record.EventKind == TelemetryEventKind.DamageApplied).ToList();
        var kills = ordered.Where(record => record.EventKind == TelemetryEventKind.KillCredited).ToList();
        if (damage.Count == 0 || kills.Count == 0)
        {
            return 0f;
        }

        var terminalDamage = 0f;
        foreach (var kill in kills)
        {
            var targetId = kill.Target?.UnitInstanceId ?? string.Empty;
            var killingBlow = damage.LastOrDefault(record =>
                string.Equals(record.Target?.UnitInstanceId, targetId, StringComparison.Ordinal)
                && record.TimeSeconds <= kill.TimeSeconds + 0.0001f);
            if (killingBlow != null)
            {
                terminalDamage += Math.Max(0f, killingBlow.ValueA);
            }
        }

        return Ratio(terminalDamage, damage.Sum(record => Math.Max(0f, record.ValueA)));
    }

    private static float ComputeTargetSwitchesPer10sP95(IReadOnlyList<TelemetryEventRecord> ordered)
    {
        var samples = ordered
            .Where(record => record.EventKind == TelemetryEventKind.TargetSwitched && record.Actor != null)
            .GroupBy(record => record.Actor!.UnitInstanceId)
            .SelectMany(group => BuildWindowRates(group.Select(record => record.TimeSeconds).ToList(), 10f))
            .ToList();
        return Percentile(samples, 0.95f);
    }

    private static IEnumerable<float> BuildWindowRates(IReadOnlyList<float> times, float windowSeconds)
    {
        if (times.Count == 0)
        {
            yield return 0f;
            yield break;
        }

        for (var i = 0; i < times.Count; i++)
        {
            var start = times[i];
            var end = start + windowSeconds;
            var count = times.Count(value => value >= start && value <= end);
            yield return count * (10f / windowSeconds);
        }
    }

    private static float ComputeIdleGapP95(IReadOnlyList<VisualPacket> packets)
    {
        if (packets.Count < 2)
        {
            return 0f;
        }

        var gaps = new List<float>();
        for (var i = 1; i < packets.Count; i++)
        {
            gaps.Add(Math.Max(0f, packets[i].TimeSeconds - packets[i - 1].TimeSeconds));
        }

        return Percentile(gaps, 0.95f);
    }

    private static float ComputeMajorCollisionRate(IReadOnlyList<VisualPacket> packets)
    {
        if (packets.Count <= 1)
        {
            return 0f;
        }

        var collisions = 0;
        for (var i = 1; i < packets.Count; i++)
        {
            if (packets[i].TimeSeconds - packets[i - 1].TimeSeconds <= MajorCollisionWindowSeconds)
            {
                collisions++;
            }
        }

        return Ratio(collisions, packets.Count);
    }

    private static float ComputeSalienceWeightPer1sP95(IReadOnlyList<VisualPacket> packets)
    {
        var samples = new List<float>();
        for (var i = 0; i < packets.Count; i++)
        {
            var start = packets[i].TimeSeconds;
            var end = start + RollingWindowSeconds;
            var weight = packets
                .Where(packet => packet.TimeSeconds >= start && packet.TimeSeconds <= end)
                .Sum(packet => BattleTelemetryRecorder.GetSalienceWeight(packet.Salience));
            samples.Add(weight);
        }

        return Percentile(samples, 0.95f);
    }

    private static float ComputeStatusChipOverflowRate(IReadOnlyList<TelemetryEventRecord> ordered, ReadabilityGateConfig config)
    {
        var windows = ordered
            .Where(record => record.EventKind == TelemetryEventKind.StatusApplied && record.Target != null)
            .GroupBy(record => $"{record.Target!.UnitInstanceId}:{MathF.Floor(record.TimeSeconds)}", StringComparer.Ordinal)
            .Select(group => group.Select(record => record.StatusId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Count())
            .ToList();
        if (windows.Count == 0)
        {
            return 0f;
        }

        return Ratio(windows.Count(count => count > config.MaxStatusChipsPerUnit), windows.Count);
    }

    private static float ComputeFloatingBurstOverflowRate(IReadOnlyList<VisualPacket> packets, ReadabilityGateConfig config)
    {
        var windows = packets
            .Where(packet => packet.TargetId.Length > 0)
            .GroupBy(packet => $"{packet.TargetId}:{MathF.Floor(packet.TimeSeconds)}", StringComparer.Ordinal)
            .Select(group => group.Count())
            .ToList();
        if (windows.Count == 0)
        {
            return 0f;
        }

        return Ratio(windows.Count(count => count > config.MaxFloatingTextBurstsPerTargetPerSec), windows.Count);
    }

    private static IReadOnlyList<VisualPacket> BuildVisualPackets(IReadOnlyList<TelemetryEventRecord> ordered, ReadabilityGateConfig config)
    {
        var windowSeconds = config.MinorAggregationWindowMs / 1000f;
        var packets = new List<VisualPacket>();
        foreach (var record in ordered)
        {
            var salience = record.Explain?.Salience ?? SalienceClass.None;
            var sourceId = record.Explain?.SourceContentId ?? string.Empty;
            var targetId = record.Target?.UnitInstanceId ?? string.Empty;
            var statusId = record.StatusId ?? string.Empty;
            var kind = ResolveAggregationKind(record);

            if (kind != ReadabilityAggregationKind.None)
            {
                var existing = packets.LastOrDefault(packet =>
                    packet.AggregationKind == kind
                    && string.Equals(packet.SourceId, sourceId, StringComparison.Ordinal)
                    && string.Equals(packet.TargetId, targetId, StringComparison.Ordinal)
                    && string.Equals(packet.StatusId, statusId, StringComparison.Ordinal)
                    && Math.Abs(packet.TimeSeconds - record.TimeSeconds) <= windowSeconds);
                if (existing != null)
                {
                    existing.EventCount++;
                    existing.Value += Math.Max(0f, record.ValueA);
                    continue;
                }
            }

            packets.Add(new VisualPacket
            {
                TimeSeconds = record.TimeSeconds,
                SourceId = sourceId,
                TargetId = targetId,
                StatusId = statusId,
                Salience = salience,
                AggregationKind = kind,
                Value = Math.Max(0f, record.ValueA),
                EventCount = 1,
                IsOffscreen = false,
            });
        }

        return packets;
    }

    private static ReadabilityAggregationKind ResolveAggregationKind(TelemetryEventRecord record)
    {
        if (record.EventKind == TelemetryEventKind.DamageApplied && !string.IsNullOrWhiteSpace(record.StatusId))
        {
            return ReadabilityAggregationKind.MergeDotTicksByStatus;
        }

        if (record.EventKind == TelemetryEventKind.DamageApplied && record.Explain?.SourceKind == ExplainedSourceKind.BasicAttack)
        {
            return ReadabilityAggregationKind.MergeMinorTicksBySourceTarget;
        }

        if (record.EventKind == TelemetryEventKind.BarrierApplied)
        {
            return ReadabilityAggregationKind.CollapseRepeatedBarrierTicks;
        }

        return ReadabilityAggregationKind.None;
    }

    private static bool IsUnexplained(TelemetryEventRecord record)
    {
        return record.Explain == null
               || string.IsNullOrWhiteSpace(record.Explain.SourceContentId)
               || string.IsNullOrWhiteSpace(record.Explain.SourceDisplayName)
               || record.Explain.SourceKind == ExplainedSourceKind.SystemRule;
    }

    private static float Percentile(IReadOnlyList<float> values, float percentile)
    {
        if (values == null || values.Count == 0)
        {
            return 0f;
        }

        var ordered = values.OrderBy(value => value).ToList();
        var index = Math.Clamp((int)MathF.Ceiling((ordered.Count - 1) * percentile), 0, ordered.Count - 1);
        return ordered[index];
    }

    private static float Ratio(float numerator, float denominator)
    {
        return denominator <= 0f ? 0f : numerator / denominator;
    }

    private static float Ratio(int numerator, int denominator)
    {
        return denominator <= 0 ? 0f : numerator / (float)denominator;
    }

    private sealed class VisualPacket
    {
        public float TimeSeconds;
        public string SourceId = string.Empty;
        public string TargetId = string.Empty;
        public string StatusId = string.Empty;
        public SalienceClass Salience = SalienceClass.None;
        public ReadabilityAggregationKind AggregationKind = ReadabilityAggregationKind.None;
        public float Value = 0f;
        public int EventCount = 0;
        public bool IsOffscreen = false;
    }
}
