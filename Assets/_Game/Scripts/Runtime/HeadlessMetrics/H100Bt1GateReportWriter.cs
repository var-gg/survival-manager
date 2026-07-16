using System;
using System.IO;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>RC1 gate-report.json과 경로를 공유하지 않는 BT1 결정적 report writer.</summary>
public static class H100Bt1GateReportWriter
{
    public const string FileName = "h100-bt1-gate-report.json";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Write(string outputDirectory, H100Bt1GateReport report)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory가 비어 있다.", nameof(outputDirectory));
        }

        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, FileName);
        File.WriteAllText(path, HeadlessMetricJson.Serialize(report) + "\n", Utf8WithoutBom);
        return path;
    }
}
