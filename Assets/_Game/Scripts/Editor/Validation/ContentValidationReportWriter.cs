using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace SM.Editor.Validation;

internal interface IReportWriter
{
    ContentValidationReport Write(ContentValidationReport report);
}

internal interface IValidationReportPathProvider
{
    string GetDefaultReportDirectory();
    ValidationReportOutputPaths BuildOutputPaths();
}

internal sealed record ValidationReportOutputPaths(
    string ReportDirectory,
    string JsonReportPath,
    string MarkdownSummaryPath,
    string ContentBudgetAuditJsonPath,
    string ContentBudgetAuditMarkdownPath,
    string CounterCoverageMatrixMarkdownPath,
    string ForbiddenFeatureReportMarkdownPath);

internal sealed record ValidationArtifact(string FilePath, string Contents);

internal interface IValidationArtifactRenderer
{
    IReadOnlyList<ValidationArtifact> Render(ContentValidationReport report);
}

internal interface IArtifactSink
{
    void Write(IReadOnlyList<ValidationArtifact> artifacts);
}

internal static class ContentValidationReportPaths
{
    internal static string GetDefaultReportDirectory(IUnityAssetGateway gateway)
    {
        return new ContentValidationReportPathProvider(gateway).GetDefaultReportDirectory();
    }
}

internal sealed class ContentValidationReportPathProvider : IValidationReportPathProvider
{
    private readonly IUnityAssetGateway _gateway;

    public ContentValidationReportPathProvider(IUnityAssetGateway gateway)
    {
        _gateway = gateway;
    }

    public string GetDefaultReportDirectory()
    {
        return Path.GetFullPath(Path.Combine(_gateway.GetProjectRoot(), ContentValidationPolicyCatalog.ReportFolderName));
    }

    public ValidationReportOutputPaths BuildOutputPaths()
    {
        var reportDirectory = GetDefaultReportDirectory();
        return new ValidationReportOutputPaths(
            reportDirectory,
            Path.Combine(reportDirectory, ContentValidationPolicyCatalog.JsonReportFileName),
            Path.Combine(reportDirectory, ContentValidationPolicyCatalog.MarkdownSummaryFileName),
            Path.Combine(reportDirectory, ContentValidationPolicyCatalog.BudgetAuditJsonFileName),
            Path.Combine(reportDirectory, ContentValidationPolicyCatalog.BudgetAuditMarkdownFileName),
            Path.Combine(reportDirectory, ContentValidationPolicyCatalog.CounterCoverageMatrixMarkdownFileName),
            Path.Combine(reportDirectory, ContentValidationPolicyCatalog.ForbiddenFeatureReportMarkdownFileName));
    }
}

internal sealed class FileArtifactSink : IArtifactSink
{
    public void Write(IReadOnlyList<ValidationArtifact> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            var directory = Path.GetDirectoryName(artifact.FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(artifact.FilePath, artifact.Contents);
        }
    }
}

internal sealed class CompositeReportWriter : IReportWriter
{
    private readonly IValidationReportPathProvider _pathProvider;
    private readonly IReadOnlyList<IValidationArtifactRenderer> _renderers;
    private readonly IArtifactSink _sink;

    public CompositeReportWriter(
        IValidationReportPathProvider pathProvider,
        IReadOnlyList<IValidationArtifactRenderer> renderers,
        IArtifactSink sink)
    {
        _pathProvider = pathProvider;
        _renderers = renderers;
        _sink = sink;
    }

    public ContentValidationReport Write(ContentValidationReport report)
    {
        var paths = _pathProvider.BuildOutputPaths();
        var withPaths = report with
        {
            JsonReportPath = paths.JsonReportPath,
            MarkdownSummaryPath = paths.MarkdownSummaryPath,
            ContentBudgetAuditJsonPath = paths.ContentBudgetAuditJsonPath,
            ContentBudgetAuditMarkdownPath = paths.ContentBudgetAuditMarkdownPath,
            CounterCoverageMatrixMarkdownPath = paths.CounterCoverageMatrixMarkdownPath,
            ForbiddenFeatureReportMarkdownPath = paths.ForbiddenFeatureReportMarkdownPath,
        };

        var artifacts = _renderers
            .SelectMany(renderer => renderer.Render(withPaths))
            .ToList();

        _sink.Write(artifacts);
        return withPaths;
    }
}

internal sealed class JsonValidationReportRenderer : IValidationArtifactRenderer
{
    public IReadOnlyList<ValidationArtifact> Render(ContentValidationReport report)
    {
        return new[]
        {
            new ValidationArtifact(report.JsonReportPath, JsonConvert.SerializeObject(report, Formatting.Indented)),
        };
    }
}

internal sealed class MarkdownValidationSummaryRenderer : IValidationArtifactRenderer
{
    public IReadOnlyList<ValidationArtifact> Render(ContentValidationReport report)
    {
        return new[]
        {
            new ValidationArtifact(report.MarkdownSummaryPath, BuildMarkdownSummary(report)),
        };
    }

    internal string BuildMarkdownSummary(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Content Validation Summary");
        builder.AppendLine();
        builder.AppendLine($"- generatedAtUtc: `{report.GeneratedAtUtc}`");
        builder.AppendLine($"- validationPhase: `{report.ValidationPhase}`");
        builder.AppendLine($"- errors: `{report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error)}`");
        builder.AppendLine($"- warnings: `{report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning)}`");
        builder.AppendLine();
        builder.AppendLine("## Launch Scope");
        builder.AppendLine();
        builder.AppendLine($"- archetypes: `{report.LaunchScope.ArchetypeCount}` (core `{report.LaunchScope.CoreArchetypeCount}`, specialist `{report.LaunchScope.SpecialistArchetypeCount}`)");
        builder.AppendLine($"- skills: `{report.LaunchScope.SkillCount}`");
        builder.AppendLine($"- equippables: `{report.LaunchScope.EquippableCount}`");
        builder.AppendLine($"- affixes: `{report.LaunchScope.AffixCount}`");
        builder.AppendLine($"- passiveBoards: `{report.LaunchScope.PassiveBoardCount}`");
        builder.AppendLine($"- passiveNodes: `{report.LaunchScope.PassiveNodeCount}`");
        builder.AppendLine($"- tempAugments: `{report.LaunchScope.TemporaryAugmentCount}`");
        builder.AppendLine($"- permAugments: `{report.LaunchScope.PermanentAugmentCount}`");
        builder.AppendLine($"- synergyFamilies: `{report.LaunchScope.SynergyFamilyCount}`");
        builder.AppendLine($"- teamTactics: `{report.LaunchScope.TeamTacticCount}`");
        builder.AppendLine($"- roleInstructions: `{report.LaunchScope.RoleInstructionCount}`");
        builder.AppendLine();
        builder.AppendLine("## Floor Gaps");
        builder.AppendLine();
        if (report.FloorGaps.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var gap in report.FloorGaps)
            {
                builder.AppendLine($"- {gap}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Safe Target Gaps");
        builder.AppendLine();
        if (report.SafeTargetGaps.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var gap in report.SafeTargetGaps)
            {
                builder.AppendLine($"- {gap}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Passive Boards");
        builder.AppendLine();
        foreach (var board in report.PassiveBoards)
        {
            builder.AppendLine($"- `{board.ClassId}`: small `{board.SmallCount}`, notable `{board.NotableCount}`, keystone `{board.KeystoneCount}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Issues");
        builder.AppendLine();
        if (report.Issues.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var issue in report.Issues)
            {
                builder.AppendLine($"- `{issue.Severity}` `{issue.Code}` {issue.Message} ({issue.AssetPath}{(string.IsNullOrWhiteSpace(issue.Scope) ? string.Empty : $" / {issue.Scope}")})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Loop C Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- budgetAuditJson: `{report.ContentBudgetAuditJsonPath}`");
        builder.AppendLine($"- budgetAuditMarkdown: `{report.ContentBudgetAuditMarkdownPath}`");
        builder.AppendLine($"- counterCoverageMatrix: `{report.CounterCoverageMatrixMarkdownPath}`");
        builder.AppendLine($"- forbiddenFeatureReport: `{report.ForbiddenFeatureReportMarkdownPath}`");
        return builder.ToString();
    }
}

internal sealed class LoopCArtifactRenderer : IValidationArtifactRenderer
{
    public IReadOnlyList<ValidationArtifact> Render(ContentValidationReport report)
    {
        return new[]
        {
            new ValidationArtifact(report.ContentBudgetAuditJsonPath, JsonConvert.SerializeObject(report.LoopC.BudgetAudit, Formatting.Indented)),
            new ValidationArtifact(report.ContentBudgetAuditMarkdownPath, BuildLoopCBudgetAuditMarkdown(report)),
            new ValidationArtifact(report.CounterCoverageMatrixMarkdownPath, BuildCounterCoverageMatrixMarkdown(report)),
            new ValidationArtifact(report.ForbiddenFeatureReportMarkdownPath, BuildForbiddenFeatureReportMarkdown(report)),
        };
    }

    internal string BuildLoopCBudgetAuditMarkdown(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Content Budget Audit");
        builder.AppendLine();
        builder.AppendLine("| Content | Kind | Domain | Rarity | PowerBand | Role | Final | Derived | Delta | Counters |");
        builder.AppendLine("|---|---|---|---|---|---|---:|---:|---:|---|");
        foreach (var entry in report.LoopC.BudgetAudit)
        {
            builder.AppendLine($"| `{entry.ContentId}` | `{entry.ContentKind}` | `{entry.Domain}` | `{entry.Rarity}` | `{entry.PowerBand}` | `{entry.RoleProfile}` | {entry.BudgetFinalScore} | {entry.DerivedScore} | {entry.DerivedDelta} | {entry.CounterTools} |");
        }

        return builder.ToString();
    }

    internal string BuildCounterCoverageMatrixMarkdown(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Counter Coverage Matrix");
        builder.AppendLine();
        builder.AppendLine("| Content | Kind | Threats | Counters |");
        builder.AppendLine("|---|---|---|---|");
        foreach (var entry in report.LoopC.CounterCoverageMatrix)
        {
            builder.AppendLine($"| `{entry.ContentId}` | `{entry.ContentKind}` | {entry.ThreatPatterns} | {entry.CounterTools} |");
        }

        return builder.ToString();
    }

    internal string BuildForbiddenFeatureReportMarkdown(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# V1 Forbidden Feature Report");
        builder.AppendLine();
        if (report.LoopC.ForbiddenFeatureEntries.Count == 0)
        {
            builder.AppendLine("- none");
            return builder.ToString();
        }

        foreach (var entry in report.LoopC.ForbiddenFeatureEntries)
        {
            builder.AppendLine($"- `{entry.ContentId}` `{entry.FeatureFlags}` {entry.Reason} ({entry.AssetPath})");
        }

        return builder.ToString();
    }
}
