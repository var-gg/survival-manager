using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>arrival/oracle/report를 stable order와 UTF-8 no-BOM으로 기록한다.</summary>
public static class SunkenSolvabilityArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public sealed record ArtifactSet(
        string ArrivalSnapshotsPath,
        string OracleCandidatesPath,
        string DiagnosisReportPath);

    public static ArtifactSet Write(
        string outputDirectory,
        IReadOnlyList<SunkenArrivalSnapshotRecord> snapshots,
        IReadOnlyList<SunkenOracleCandidateRecord> candidates,
        SunkenSolvabilityReport report)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var snapshotPath = Path.Combine(outputDirectory, "arrival-snapshots.jsonl");
        var candidatesPath = Path.Combine(outputDirectory, "oracle-candidates.jsonl");
        var reportPath = Path.Combine(outputDirectory, "sunken-diagnosis.json");
        WriteLines(snapshotPath, snapshots
            .OrderBy(value => value.PolicyId, StringComparer.Ordinal)
            .ThenBy(value => value.CampaignSeed)
            .ThenBy(value => value.SampleId, StringComparer.Ordinal));
        WriteLines(candidatesPath, candidates
            .OrderBy(value => value.SampleId, StringComparer.Ordinal)
            .ThenBy(value => value.Scope, StringComparer.Ordinal)
            .ThenBy(value => value.StateVariantId, StringComparer.Ordinal)
            .ThenByDescending(value => value.IsPolicyChoice)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal));
        File.WriteAllText(reportPath, HeadlessMetricJson.Serialize(report) + "\n", Utf8WithoutBom);
        return new ArtifactSet(snapshotPath, candidatesPath, reportPath);
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
