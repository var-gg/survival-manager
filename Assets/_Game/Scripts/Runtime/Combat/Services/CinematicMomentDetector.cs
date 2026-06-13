using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class CinematicMomentDetector
{
    public static readonly IReadOnlyList<CinematicMomentId> RequiredMomentIds = new[]
    {
        CinematicMomentId.SaveMoment,
        CinematicMomentId.BacklineDive,
        CinematicMomentId.InterposeSuccess,
        CinematicMomentId.SilenceCancel,
        CinematicMomentId.BarrierAbsorb,
        CinematicMomentId.SunderBurst,
        CinematicMomentId.ControlBreak,
        CinematicMomentId.StanceSwing,
        CinematicMomentId.SynergyPulse,
        CinematicMomentId.TacticPerfectRead,
    };

    public static IReadOnlyList<CinematicMomentRecord> Detect(IEnumerable<TelemetryEventRecord> telemetryEvents)
    {
        var ordered = telemetryEvents
            .Where(record => record != null)
            .OrderBy(record => record.TimeSeconds)
            .ToList();
        var moments = new List<CinematicMomentRecord>();

        AddFirst(moments, ordered, CinematicMomentId.SaveMoment, IsSaveMoment, "save_moment");
        AddFirst(moments, ordered, CinematicMomentId.BacklineDive, IsBacklineDive, "backline_dive");
        AddFirst(moments, ordered, CinematicMomentId.InterposeSuccess, IsInterposeSuccess, "interpose_success");
        AddFirst(moments, ordered, CinematicMomentId.SilenceCancel, IsSilenceCancel, "silence_cancel");
        AddFirst(moments, ordered, CinematicMomentId.BarrierAbsorb, IsBarrierAbsorb, "barrier_absorb");
        AddFirst(moments, ordered, CinematicMomentId.SunderBurst, IsSunderBurst, "sunder_burst");
        AddFirst(moments, ordered, CinematicMomentId.ControlBreak, IsControlBreak, "control_break");
        AddFirst(moments, ordered, CinematicMomentId.StanceSwing, IsStanceSwing, "stance_swing");
        AddFirst(moments, ordered, CinematicMomentId.SynergyPulse, IsSynergyPulse, "synergy_pulse");
        AddFirst(moments, ordered, CinematicMomentId.TacticPerfectRead, IsTacticPerfectRead, "tactic_perfect_read");

        return moments
            .GroupBy(moment => moment.Id)
            .Select(group => group.OrderBy(moment => moment.TimeSeconds).ThenBy(moment => moment.StableKey, StringComparer.Ordinal).First())
            .OrderBy(moment => moment.TimeSeconds)
            .ThenBy(moment => moment.Id)
            .ToArray();
    }

    private static void AddFirst(
        ICollection<CinematicMomentRecord> moments,
        IReadOnlyList<TelemetryEventRecord> ordered,
        CinematicMomentId id,
        Func<TelemetryEventRecord, bool> predicate,
        string evidence)
    {
        var source = ordered.FirstOrDefault(predicate);
        if (source == null)
        {
            return;
        }

        moments.Add(ToMoment(id, source, evidence));
    }

    private static CinematicMomentRecord ToMoment(CinematicMomentId id, TelemetryEventRecord source, string evidence)
    {
        var actorId = source.Actor?.UnitInstanceId ?? source.Actor?.UnitBlueprintId ?? string.Empty;
        var targetId = source.Target?.UnitInstanceId ?? source.Target?.UnitBlueprintId ?? string.Empty;
        var skillId = string.IsNullOrWhiteSpace(source.SkillId)
            ? source.Explain?.SourceContentId ?? string.Empty
            : source.SkillId;
        return new CinematicMomentRecord(
            id,
            source.TimeSeconds,
            actorId,
            targetId,
            skillId,
            source.StatusId,
            evidence,
            ResolveScore(source));
    }

    private static float ResolveScore(TelemetryEventRecord source)
    {
        var salience = source.Explain?.Salience ?? SalienceClass.None;
        return Math.Max(1f, BattleTelemetryRecorder.GetSalienceWeight(salience));
    }

    private static bool IsMetric(TelemetryEventRecord record, string metricId)
        => record.EventKind == TelemetryEventKind.ActivityMetricRecorded
           && string.Equals(record.StringValueA, metricId, StringComparison.Ordinal)
           && record.ValueA > 0f;

    private static bool IsSaveMoment(TelemetryEventRecord record)
        => IsMetric(record, "SaveMomentCount")
           || (record.EventKind == TelemetryEventKind.HealingApplied
               && string.Equals(record.StringValueB, "save_moment", StringComparison.Ordinal));

    private static bool IsBacklineDive(TelemetryEventRecord record)
        => IsMetric(record, "BacklineDiveKillCount")
           || (record.EventKind == TelemetryEventKind.KillCredited
               && record.Explain?.ReasonCode == DecisionReasonCode.PunishExposedBackline);

    private static bool IsInterposeSuccess(TelemetryEventRecord record)
        => IsMetric(record, "ScreenAbsorbCount")
           || IsMetric(record, "ScreenDeterrenceCount")
           || string.Equals(record.StringValueA, "interpose_success", StringComparison.Ordinal);

    private static bool IsSilenceCancel(TelemetryEventRecord record)
        => (record.EventKind == TelemetryEventKind.InterruptApplied
            && (string.Equals(record.StatusId, "silence", StringComparison.Ordinal)
                || string.Equals(record.StringValueA, "silence", StringComparison.Ordinal)))
           || (record.EventKind == TelemetryEventKind.StatusApplied
               && string.Equals(record.StatusId, "silence", StringComparison.Ordinal));

    private static bool IsBarrierAbsorb(TelemetryEventRecord record)
        => record.EventKind == TelemetryEventKind.BarrierApplied
           || string.Equals(record.StringValueA, "barrier_absorb", StringComparison.Ordinal);

    private static bool IsSunderBurst(TelemetryEventRecord record)
        => string.Equals(record.StatusId, "sunder", StringComparison.Ordinal)
           && record.EventKind is TelemetryEventKind.StatusApplied or TelemetryEventKind.DamageApplied;

    private static bool IsControlBreak(TelemetryEventRecord record)
        => record.EventKind == TelemetryEventKind.GuardBroken
           || (record.EventKind == TelemetryEventKind.StatusRemoved
               && record.StatusId is "stun" or "root" or "silence");

    private static bool IsStanceSwing(TelemetryEventRecord record)
        => record.EventKind == TelemetryEventKind.PositioningIntentUpdated
           || IsMetric(record, "FocusMarkSwitchCount")
           || string.Equals(record.StringValueA, "stance_swing", StringComparison.Ordinal);

    private static bool IsSynergyPulse(TelemetryEventRecord record)
        => !string.IsNullOrWhiteSpace(record.SynergyId)
           || record.Explain?.SourceKind == ExplainedSourceKind.Synergy;

    private static bool IsTacticPerfectRead(TelemetryEventRecord record)
        => record.EventKind == TelemetryEventKind.TacticReadEvaluated
           && (record.BoolValueA || string.Equals(record.StringValueA, "perfect_read", StringComparison.Ordinal));
}
