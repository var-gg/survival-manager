using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>Stage 4 진형 event/placement/healer/report 산출물을 결정적으로 기록한다.</summary>
public static class FormationEvaluationArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public sealed record ArtifactSet(
        string FormationEventsPath,
        string PlacementLeveragePath,
        string HealerMarginalValuePath,
        string FormationReportPath);

    public static ArtifactSet Write(
        string outputDirectory,
        IReadOnlyList<FormationEventLogRecord> eventLogs,
        IReadOnlyList<PlacementLeverageRecord> placementRecords,
        IReadOnlyList<HealerMarginalValueRecord> healerRecords,
        FormationEvaluationReport report)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var eventPath = Path.Combine(outputDirectory, "formation-events.jsonl");
        var placementPath = Path.Combine(outputDirectory, "placement-leverage.jsonl");
        var healerPath = Path.Combine(outputDirectory, "healer-marginal-value.jsonl");
        var reportPath = Path.Combine(outputDirectory, "formation-report.json");
        WriteLines(eventPath, eventLogs
            .OrderBy(value => value.PolicyId, StringComparer.Ordinal)
            .ThenBy(value => value.PairingId, StringComparer.Ordinal)
            .ThenBy(value => value.PlacementVariantId, StringComparer.Ordinal)
            .ThenBy(value => value.ChannelId, StringComparer.Ordinal));
        WriteLines(placementPath, placementRecords.OrderBy(value => value.PlacementSetId, StringComparer.Ordinal));
        WriteLines(healerPath, healerRecords.OrderBy(value => value.ComparisonId, StringComparer.Ordinal));
        File.WriteAllText(reportPath, HeadlessMetricJson.Serialize(report) + "\n", Utf8WithoutBom);
        return new ArtifactSet(eventPath, placementPath, healerPath, reportPath);
    }

    private static void WriteLines<T>(string path, IEnumerable<T> values)
    {
        var text = string.Join("\n", values.Select(value => HeadlessMetricJson.Serialize(value)));
        if (text.Length > 0)
        {
            text += "\n";
        }

        File.WriteAllText(path, text, Utf8WithoutBom);
    }
}
