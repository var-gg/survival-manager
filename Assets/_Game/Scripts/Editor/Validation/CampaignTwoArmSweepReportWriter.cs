using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace SM.Editor.Validation;

internal static class CampaignTwoArmSweepReportWriter
{
    private const string ReportFolder = "Logs/balance-sweep-campaign";
    private const string ReportFile = "two_arm_baseline_report.json";

    public static CampaignTwoArmSweepReport Write(CampaignTwoArmSweepReport report)
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var directory = Path.Combine(root, ReportFolder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ReportFile);
        var withPath = report with { JsonReportPath = path };
        File.WriteAllText(
            path,
            JsonConvert.SerializeObject(withPath, Formatting.Indented),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Debug.Log(
            $"[CampaignTwoArmSweep] status={withPath.Summary.Status} nodes={withPath.Summary.NodeCount} "
            + $"naive={withPath.Summary.MeanNaiveWinRate:0.000} info={withPath.Summary.MeanInfoWinRate:0.000} "
            + $"gap={withPath.Summary.MeanGap:0.000} bossGap={withPath.Summary.MeanBossGap:0.000} "
            + $"samples={withPath.Grid.ExecutedCellCountPerArmPerNode} report={path}");
        return withPath;
    }
}
