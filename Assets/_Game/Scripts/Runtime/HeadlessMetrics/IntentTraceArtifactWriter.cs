using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>wallclock/GUID 없이 intent decision timeline을 결정적 JSONL로 기록한다.</summary>
public static class IntentTraceArtifactWriter
{
    public const string FileName = "intent_trace.jsonl";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Write(string outputDirectory, IReadOnlyList<IntentTraceRecord> traces)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var rows = (traces ?? Array.Empty<IntentTraceRecord>())
            .OrderBy(value => value.RunId, StringComparer.Ordinal)
            .ThenBy(value => value.CampaignId, StringComparer.Ordinal)
            .ThenBy(value => value.DecidedAt.CampaignIndex)
            .ThenBy(value => value.DecidedAt.SiteIndex)
            .ThenBy(value => value.DecidedAt.DecisionIndex)
            .ThenBy(value => value.TraceId, StringComparer.Ordinal)
            .Select(HeadlessMetricJson.Serialize)
            .ToArray();
        var text = string.Join("\n", rows);
        if (text.Length > 0)
        {
            text += "\n";
        }

        var path = Path.Combine(outputDirectory, FileName);
        File.WriteAllText(path, text, Utf8WithoutBom);
        return path;
    }
}
