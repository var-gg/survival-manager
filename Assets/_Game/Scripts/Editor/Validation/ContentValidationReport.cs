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
    public string JsonReportPath { get; init; } = string.Empty;
    public string MarkdownSummaryPath { get; init; } = string.Empty;
}
