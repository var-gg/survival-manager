using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace SM.Editor.Validation;

internal interface IReportWriter
{
    ContentValidationReport Write(ContentValidationReport report);
}

internal static class ContentValidationReportPaths
{
    internal static string GetDefaultReportDirectory(IUnityAssetGateway gateway)
    {
        return Path.GetFullPath(Path.Combine(gateway.GetProjectRoot(), ContentValidationPolicyCatalog.ReportFolderName));
    }
}

internal sealed class CompositeReportWriter : IReportWriter
{
    private readonly IUnityAssetGateway _gateway;
    private readonly JsonReportWriter _jsonWriter;
    private readonly MarkdownSummaryWriter _markdownWriter;
    private readonly LoopCArtifactWriter _loopCWriter;

    public CompositeReportWriter(
        IUnityAssetGateway gateway,
        JsonReportWriter jsonWriter,
        MarkdownSummaryWriter markdownWriter,
        LoopCArtifactWriter loopCWriter)
    {
        _gateway = gateway;
        _jsonWriter = jsonWriter;
        _markdownWriter = markdownWriter;
        _loopCWriter = loopCWriter;
    }

    public ContentValidationReport Write(ContentValidationReport report)
    {
        var reportDirectory = ContentValidationReportPaths.GetDefaultReportDirectory(_gateway);
        Directory.CreateDirectory(reportDirectory);

        var jsonPath = Path.Combine(reportDirectory, ContentValidationPolicyCatalog.JsonReportFileName);
        var markdownPath = Path.Combine(reportDirectory, ContentValidationPolicyCatalog.MarkdownSummaryFileName);
        var budgetAuditJsonPath = Path.Combine(reportDirectory, ContentValidationPolicyCatalog.BudgetAuditJsonFileName);
        var budgetAuditMarkdownPath = Path.Combine(reportDirectory, ContentValidationPolicyCatalog.BudgetAuditMarkdownFileName);
        var counterCoverageMatrixMarkdownPath = Path.Combine(reportDirectory, ContentValidationPolicyCatalog.CounterCoverageMatrixMarkdownFileName);
        var forbiddenFeatureReportMarkdownPath = Path.Combine(reportDirectory, ContentValidationPolicyCatalog.ForbiddenFeatureReportMarkdownFileName);

        var withPaths = report with
        {
            JsonReportPath = jsonPath,
            MarkdownSummaryPath = markdownPath,
            ContentBudgetAuditJsonPath = budgetAuditJsonPath,
            ContentBudgetAuditMarkdownPath = budgetAuditMarkdownPath,
            CounterCoverageMatrixMarkdownPath = counterCoverageMatrixMarkdownPath,
            ForbiddenFeatureReportMarkdownPath = forbiddenFeatureReportMarkdownPath,
        };

        _jsonWriter.Write(withPaths);
        _markdownWriter.Write(withPaths);
        _loopCWriter.Write(withPaths);
        return withPaths;
    }
}

internal sealed class JsonReportWriter
{
    internal void Write(ContentValidationReport report)
    {
        File.WriteAllText(report.JsonReportPath, JsonConvert.SerializeObject(report, Formatting.Indented));
    }
}

internal sealed class MarkdownSummaryWriter
{
    internal void Write(ContentValidationReport report)
    {
        File.WriteAllText(report.MarkdownSummaryPath, BuildMarkdownSummary(report));
    }

    private static string BuildMarkdownSummary(ContentValidationReport report)
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

internal sealed class LoopCArtifactWriter
{
    internal void Write(ContentValidationReport report)
    {
        File.WriteAllText(report.ContentBudgetAuditJsonPath, JsonConvert.SerializeObject(report.LoopC.BudgetAudit, Formatting.Indented));
        File.WriteAllText(report.ContentBudgetAuditMarkdownPath, BuildLoopCBudgetAuditMarkdown(report));
        File.WriteAllText(report.CounterCoverageMatrixMarkdownPath, BuildCounterCoverageMatrixMarkdown(report));
        File.WriteAllText(report.ForbiddenFeatureReportMarkdownPath, BuildForbiddenFeatureReportMarkdown(report));
    }

    private static string BuildLoopCBudgetAuditMarkdown(ContentValidationReport report)
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

    private static string BuildCounterCoverageMatrixMarkdown(ContentValidationReport report)
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

    private static string BuildForbiddenFeatureReportMarkdown(ContentValidationReport report)
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
