using System;
using System.IO;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>placement_attribution_report.json을 UTF-8 no-BOM으로 결정적으로 기록한다.</summary>
public static class PlacementAttributionArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Write(string outputDirectory, PlacementAttributionReport report)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        }

        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "placement_attribution_report.json");
        File.WriteAllText(path, HeadlessMetricJson.Serialize(report) + "\n", Utf8WithoutBom);
        return path;
    }
}
