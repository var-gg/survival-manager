using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace SM.Editor.Validation;

internal static class BalanceSweepCsvWriter
{
    internal static string BuildCsvSummary(BalanceSweepReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("scenario_id,team_tactic_id,compile_hash_deterministic,final_state_deterministic,win_rate,avg_battle_duration_seconds,avg_first_cast_seconds,time_to_first_meaningful_action_seconds,avg_reposition_count,avg_target_access_time_seconds,avg_frontline_survival_time_seconds,temporary_augment_dead_offer_ratio,validation_errors,validation_warnings,flags");
        foreach (var scenario in report.Scenarios)
        {
            builder.Append(scenario.ScenarioId).Append(',')
                .Append(scenario.TeamTacticId).Append(',')
                .Append(scenario.CompileHashDeterministic ? "true" : "false").Append(',')
                .Append(scenario.FinalStateDeterministic ? "true" : "false").Append(',')
                .Append(scenario.WinRate.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.AverageBattleDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.AverageFirstCastSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.TimeToFirstMeaningfulActionSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.AverageRepositionCount.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.AverageTargetAccessTimeSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.AverageFrontlineSurvivalTimeSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.TemporaryAugmentDeadOfferRatio.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.ValidationErrorCount).Append(',')
                .Append(scenario.ValidationWarningCount).Append(',')
                .Append('"').Append(string.Join("|", scenario.Flags)).Append('"')
                .AppendLine();
        }

        builder.AppendLine();
        builder.AppendLine($"global_synergy_tier_uplift_win_rate,{report.SynergyTierUpliftWinRate.ToString("0.###", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"global_synergy_tier_uplift_duration_delta_seconds,{report.SynergyTierUpliftDurationDeltaSeconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        return builder.ToString();
    }
}

internal static class BalanceSweepReportWriter
{
    internal static BalanceSweepReport Write(BalanceSweepReport report, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);

        var jsonPath = Path.Combine(reportDirectory, "balance-sweep-report.json");
        var csvPath = Path.Combine(reportDirectory, "balance-sweep-summary.csv");
        var withPaths = report with
        {
            JsonReportPath = jsonPath,
            CsvReportPath = csvPath,
        };

        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(withPaths, Formatting.Indented));
        File.WriteAllText(csvPath, BalanceSweepCsvWriter.BuildCsvSummary(withPaths));
        return withPaths;
    }
}
