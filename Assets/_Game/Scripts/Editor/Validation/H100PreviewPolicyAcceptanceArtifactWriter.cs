using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

internal static class H100PreviewPolicyAcceptanceArtifactWriter
{
    public static ArtifactPaths Write(
        string outputDirectory,
        IReadOnlyList<SunkenArrivalSnapshotRecord> arrivals,
        IReadOnlyList<SunkenOracleCandidateRecord> sunkenCandidates,
        H100PreviewPolicyAcceptanceReport report)
    {
        Directory.CreateDirectory(outputDirectory);
        var arrivalPath = Path.Combine(outputDirectory, "preview-policy-arrivals.jsonl");
        var candidatePath = Path.Combine(outputDirectory, "preview-policy-sunken-candidates.jsonl");
        var pairPath = Path.Combine(outputDirectory, "preview-policy-pairs.jsonl");
        var reportPath = Path.Combine(outputDirectory, "preview-policy-acceptance.json");
        WriteJsonLines(arrivalPath, arrivals);
        WriteJsonLines(candidatePath, sunkenCandidates);
        WriteJsonLines(pairPath, report.PairedCases);
        File.WriteAllText(reportPath, HeadlessMetricJson.Serialize(report) + Environment.NewLine, new UTF8Encoding(false));
        return new ArtifactPaths(arrivalPath, candidatePath, pairPath, reportPath);
    }

    private static void WriteJsonLines<T>(string path, IEnumerable<T> rows)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        foreach (var row in rows)
        {
            writer.WriteLine(HeadlessMetricJson.Serialize(row));
        }
    }

    internal sealed record ArtifactPaths(
        string ArrivalPath,
        string CandidatePath,
        string PairPath,
        string ReportPath);
}
