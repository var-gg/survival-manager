using System;
using System.Collections.Generic;
using SM.Content.Definitions;

namespace SM.Editor.Validation;

public enum ContentValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public sealed record ContentValidationIssue(
    ContentValidationSeverity Severity,
    string Code,
    string Message,
    string AssetPath,
    string Scope = "");

public sealed record PassiveBoardShapeReport
{
    public string BoardId { get; init; } = string.Empty;
    public string ClassId { get; init; } = string.Empty;
    public int SmallCount { get; init; }
    public int NotableCount { get; init; }
    public int KeystoneCount { get; init; }
}

public sealed record ContentValidationReport
{
    public string GeneratedAtUtc { get; init; } = DateTime.UtcNow.ToString("O");
    public ContentLocalizationPhaseValue ValidationPhase { get; init; } = ContentLocalizationPolicy.CurrentPhase;
    public LaunchScopeCountReport LaunchScope { get; init; } = new();
    public IReadOnlyList<string> FloorGaps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SafeTargetGaps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PassiveBoardShapeReport> PassiveBoards { get; init; } = Array.Empty<PassiveBoardShapeReport>();
    public IReadOnlyList<ContentValidationIssue> Issues { get; init; } = Array.Empty<ContentValidationIssue>();
    public LoopCValidationSummary LoopC { get; init; } = new();
    public string JsonReportPath { get; init; } = string.Empty;
    public string MarkdownSummaryPath { get; init; } = string.Empty;
    public string ContentBudgetAuditJsonPath { get; init; } = string.Empty;
    public string ContentBudgetAuditMarkdownPath { get; init; } = string.Empty;
    public string CounterCoverageMatrixMarkdownPath { get; init; } = string.Empty;
    public string ForbiddenFeatureReportMarkdownPath { get; init; } = string.Empty;
}

public sealed record LoopCContentBudgetAuditEntry(
    string ContentId,
    string ContentKind,
    string AssetPath,
    string Domain,
    string Rarity,
    string PowerBand,
    string RoleProfile,
    int BudgetFinalScore,
    int DerivedScore,
    int DerivedDelta,
    string ThreatPatterns,
    string CounterTools,
    string FeatureFlags);

public sealed record LoopCCounterCoverageMatrixEntry(
    string ContentId,
    string ContentKind,
    string AssetPath,
    string ThreatPatterns,
    string CounterTools);

public sealed record LoopCForbiddenFeatureEntry(
    string ContentId,
    string ContentKind,
    string AssetPath,
    string FeatureFlags,
    string Reason);

public sealed record LoopCValidationSummary
{
    public IReadOnlyList<LoopCContentBudgetAuditEntry> BudgetAudit { get; init; } = Array.Empty<LoopCContentBudgetAuditEntry>();
    public IReadOnlyList<LoopCCounterCoverageMatrixEntry> CounterCoverageMatrix { get; init; } = Array.Empty<LoopCCounterCoverageMatrixEntry>();
    public IReadOnlyList<LoopCForbiddenFeatureEntry> ForbiddenFeatureEntries { get; init; } = Array.Empty<LoopCForbiddenFeatureEntry>();
}
