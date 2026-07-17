using System;
using System.IO;
using System.Text;

namespace SM.HeadlessCensus;

/// <summary>option_trap_report.json을 invariant·ordinal·UTF-8 no-BOM으로 기록한다.</summary>
public static class OptionTrapArtifactWriter
{
    public const string FileName = "option_trap_report.json";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string Serialize(OptionTrapReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        return BuildSpaceJson.Serialize(report) + "\n";
    }

    public static string Write(string outputDirectory, OptionTrapReport report)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, FileName);
        File.WriteAllText(path, Serialize(report), Utf8WithoutBom);
        return path;
    }
}
