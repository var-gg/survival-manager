using System;
using System.Collections.Generic;

namespace SM.Editor.Validation;

public sealed record BalanceSweepShareEntry(
    string UnitId,
    float Share);

public sealed record BalanceSweepScenarioReport
{
    public string ScenarioId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TeamTacticId { get; init; } = string.Empty;
    public string CompileHash { get; init; } = string.Empty;
    public bool CompileHashDeterministic { get; init; }
    public bool FinalStateDeterministic { get; init; }
    public float WinRate { get; init; }
    public float AverageBattleDurationSeconds { get; init; }
    public float AverageFirstCastSeconds { get; init; }
    public float TemporaryAugmentDeadOfferRatio { get; init; }
    public int ValidationErrorCount { get; init; }
    public int ValidationWarningCount { get; init; }
    public IReadOnlyList<BalanceSweepShareEntry> DamageShare { get; init; } = Array.Empty<BalanceSweepShareEntry>();
    public IReadOnlyList<BalanceSweepShareEntry> HealShare { get; init; } = Array.Empty<BalanceSweepShareEntry>();
    public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();
}

public sealed record BalanceSweepReport
{
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public string ValidationReportPath { get; init; } = string.Empty;
    public int ValidationErrorCount { get; init; }
    public int ValidationWarningCount { get; init; }
    public float SynergyTierUpliftWinRate { get; init; }
    public float SynergyTierUpliftDurationDeltaSeconds { get; init; }
    public IReadOnlyList<string> Outliers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BalanceSweepScenarioReport> Scenarios { get; init; } = Array.Empty<BalanceSweepScenarioReport>();
    public string JsonReportPath { get; init; } = string.Empty;
    public string CsvReportPath { get; init; } = string.Empty;
}
