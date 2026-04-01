using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class TelemetryExplainValidator
{
    private static readonly HashSet<TelemetryEventKind> RequiredExplainKinds = new()
    {
        TelemetryEventKind.DamageApplied,
        TelemetryEventKind.HealingApplied,
        TelemetryEventKind.BarrierApplied,
        TelemetryEventKind.StatusApplied,
        TelemetryEventKind.GuardBroken,
        TelemetryEventKind.InterruptApplied,
        TelemetryEventKind.SummonSpawned,
        TelemetryEventKind.SummonDespawned,
        TelemetryEventKind.KillCredited,
    };

    public static bool RequiresExplainStamp(TelemetryEventKind eventKind)
    {
        return RequiredExplainKinds.Contains(eventKind);
    }

    public static bool HasCompleteExplainStamp(TelemetryEventRecord record)
    {
        if (record == null || !RequiresExplainStamp(record.EventKind))
        {
            return true;
        }

        return record.Explain != null
               && !string.IsNullOrWhiteSpace(record.Explain.SourceContentId)
               && !string.IsNullOrWhiteSpace(record.Explain.SourceDisplayName);
    }

    public static IReadOnlyList<string> CollectMissingExplainStampIssues(IEnumerable<TelemetryEventRecord> records)
    {
        if (records == null)
        {
            return Array.Empty<string>();
        }

        return records
            .Where(record => record != null && RequiresExplainStamp(record.EventKind) && !HasCompleteExplainStamp(record))
            .Select(record => $"{record.EventKind}@{record.TimeSeconds:0.###}:{record.Actor?.UnitBlueprintId ?? "unknown"}->{record.Target?.UnitBlueprintId ?? "none"}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }
}
