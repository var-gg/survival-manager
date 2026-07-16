using System;
using System.IO;
using System.Text;

namespace SM.HeadlessMetrics;

public static class IntentTrackReportWriter
{
    public const string FileName = "intent_track_report.json";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Write(string outputDirectory, IntentTrackReport report)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        if (report == null) throw new ArgumentNullException(nameof(report));
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, FileName);
        File.WriteAllText(path, HeadlessMetricJson.Serialize(report) + "\n", Utf8WithoutBom);
        return path;
    }
}
