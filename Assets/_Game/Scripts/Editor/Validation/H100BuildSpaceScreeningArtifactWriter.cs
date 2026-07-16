using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

internal static class H100BuildSpaceScreeningArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static H100BuildSpaceScreeningSummary Write(
        string outputDirectory,
        IReadOnlyList<BattleMetricRecord> records,
        IReadOnlyList<string> selectedBuildIds,
        IReadOnlyList<string> medoidSignatures,
        int seedCount)
    {
        Directory.CreateDirectory(outputDirectory);
        var ordered = records.OrderBy(record => record.BattleId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ToArray();
        var jsonl = string.Join("\n", ordered.Select(record => HeadlessMetricJson.Serialize(record)));
        if (jsonl.Length > 0)
        {
            jsonl += "\n";
        }

        File.WriteAllText(Path.Combine(outputDirectory, "screening-smoke.jsonl"), jsonl, Utf8WithoutBom);
        var summary = new H100BuildSpaceScreeningSummary(
            "h100-build-space-screening-smoke-v1",
            selectedBuildIds.Count,
            medoidSignatures.Count,
            seedCount,
            ordered.Length,
            ordered.Count(record => !string.IsNullOrWhiteSpace(record.FailureCode)),
            ordered.Count(record => record.Crashed),
            ordered.Count(record => record.Timeout),
            ordered.Count(record => string.Equals(record.WinnerSide, "ally", StringComparison.Ordinal)),
            ReplayHash.ComputeManifest(ordered.Select(record => record.ReplayHash)),
            selectedBuildIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            medoidSignatures.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        File.WriteAllText(
            Path.Combine(outputDirectory, "screening-smoke-summary.json"),
            HeadlessMetricJson.Serialize(summary) + "\n",
            Utf8WithoutBom);
        return summary;
    }
}

internal sealed record H100BuildSpaceScreeningSummary(
    string SchemaVersion,
    int BuildCount,
    int MedoidCount,
    int SeedCount,
    int RecordCount,
    int FailureCount,
    int CrashCount,
    int TimeoutCount,
    int AllyWinCount,
    string ReplayManifestHash,
    IReadOnlyList<string> SelectedBuildIds,
    IReadOnlyList<string> MedoidSignatures);
