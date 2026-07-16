using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>fact와 decision을 같은 결정적 JSONL timeline으로 기록한다.</summary>
public static class PlayerVisibleFactLedgerArtifactWriter
{
    public const string FileName = "player_visible_fact_ledger.jsonl";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Write(
        string outputDirectory,
        IReadOnlyList<PlayerVisibleFactRecord> facts,
        IReadOnlyList<PlayerVisibleDecisionRecord> decisions)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var rows = (facts ?? Array.Empty<PlayerVisibleFactRecord>())
            .Select(fact => new LedgerRow(
                fact.RunId,
                fact.CampaignId,
                fact.ObservedAt,
                0,
                fact.FactId,
                HeadlessMetricJson.Serialize(fact)))
            .Concat((decisions ?? Array.Empty<PlayerVisibleDecisionRecord>())
                .Select(decision => new LedgerRow(
                    decision.RunId,
                    decision.CampaignId,
                    decision.DecidedAt,
                    1,
                    decision.DecisionId,
                    HeadlessMetricJson.Serialize(decision))))
            .OrderBy(row => row.RunId, StringComparer.Ordinal)
            .ThenBy(row => row.CampaignId, StringComparer.Ordinal)
            .ThenBy(row => row.At.CampaignIndex)
            .ThenBy(row => row.At.DecisionIndex)
            .ThenBy(row => row.At.SiteIndex)
            .ThenBy(row => row.KindOrder)
            .ThenBy(row => row.StableId, StringComparer.Ordinal)
            .Select(row => row.Json)
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

    private sealed record LedgerRow(
        string RunId,
        string CampaignId,
        PlayerVisibleTimelinePoint At,
        int KindOrder,
        string StableId,
        string Json);
}
