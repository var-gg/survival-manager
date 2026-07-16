using System;
using System.IO;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>정보 표면 감사 결과를 stable JSON으로 기록한다.</summary>
public static class InformationSurfaceAuditArtifactWriter
{
    public const string FileName = "information_surface_audit.json";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Write(string outputDirectory, InformationSurfaceAuditResult result)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, FileName);
        File.WriteAllText(path, HeadlessMetricJson.Serialize(result) + "\n", Utf8WithoutBom);
        return path;
    }
}
