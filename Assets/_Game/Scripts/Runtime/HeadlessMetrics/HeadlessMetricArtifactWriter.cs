using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>JSONL/CSV/GateReport 파일 세트를 결정적으로 생성한다.</summary>
public static class HeadlessMetricArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public sealed record ArtifactSet(
        string BattleJsonlPath,
        string CampaignJsonlPath,
        string GateReportPath,
        string? BattleCsvPath,
        string? CampaignCsvPath);

    public static ArtifactSet Write(
        string outputDirectory,
        IReadOnlyList<BattleMetricRecord> battleRecords,
        IReadOnlyList<CampaignMetricRecord> campaignRecords,
        GateReport gateReport,
        bool writeCsv)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory가 비어 있다.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var battles = battleRecords.Select(Normalize)
            .OrderBy(record => record.RunId, StringComparer.Ordinal)
            .ThenBy(record => record.CampaignId, StringComparer.Ordinal)
            .ThenBy(record => record.ReplayGroupId, StringComparer.Ordinal)
            .ThenBy(record => record.ReplayIteration)
            .ThenBy(record => record.BattleId, StringComparer.Ordinal)
            .ThenBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.ReplayHash, StringComparer.Ordinal)
            .ThenBy(record => record.FailureCode, StringComparer.Ordinal)
            .ToArray();
        var campaigns = campaignRecords.Select(Normalize)
            .OrderBy(record => record.RunId, StringComparer.Ordinal)
            .ThenBy(record => record.CampaignId, StringComparer.Ordinal)
            .ThenBy(record => record.PolicyId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ToArray();
        var battleJsonlPath = Path.Combine(outputDirectory, "battle-metrics.jsonl");
        var campaignJsonlPath = Path.Combine(outputDirectory, "campaign-metrics.jsonl");
        var gateReportPath = Path.Combine(outputDirectory, "gate-report.json");
        WriteLines(battleJsonlPath, battles.Select(record => HeadlessMetricJson.Serialize(record)));
        WriteLines(campaignJsonlPath, campaigns.Select(record => HeadlessMetricJson.Serialize(record)));
        File.WriteAllText(gateReportPath, HeadlessMetricJson.Serialize(gateReport) + "\n", Utf8WithoutBom);

        string? battleCsvPath = null;
        string? campaignCsvPath = null;
        if (writeCsv)
        {
            battleCsvPath = Path.Combine(outputDirectory, "battle-metrics.csv");
            campaignCsvPath = Path.Combine(outputDirectory, "campaign-metrics.csv");
            File.WriteAllText(battleCsvPath, BuildBattleCsv(battles), Utf8WithoutBom);
            File.WriteAllText(campaignCsvPath, BuildCampaignCsv(campaigns), Utf8WithoutBom);
        }
        else
        {
            DeleteIfPresent(Path.Combine(outputDirectory, "battle-metrics.csv"));
            DeleteIfPresent(Path.Combine(outputDirectory, "campaign-metrics.csv"));
        }

        return new ArtifactSet(battleJsonlPath, campaignJsonlPath, gateReportPath, battleCsvPath, campaignCsvPath);
    }

    private static BattleMetricRecord Normalize(BattleMetricRecord record)
    {
        return record with
        {
            SynergyRuleActivationCounts = MetricCount.Normalize(record.SynergyRuleActivationCounts),
            ComboRuleActivationCounts = MetricCount.Normalize(record.ComboRuleActivationCounts),
            AugmentRuleActivationCounts = MetricCount.Normalize(record.AugmentRuleActivationCounts),
            DoctrineRuleActivationCounts = MetricCount.Normalize(record.DoctrineRuleActivationCounts),
            AllyBuildComponentCounts = MetricCount.Normalize(record.AllyBuildComponentCounts),
            EnemyBuildComponentCounts = MetricCount.Normalize(record.EnemyBuildComponentCounts),
            EligibleDepthRuleIds = NormalizeStrings(record.EligibleDepthRuleIds),
            FiredDepthRuleIds = NormalizeStrings(record.FiredDepthRuleIds),
            CausalDepthRuleIds = NormalizeStrings(record.CausalDepthRuleIds),
        };
    }

    private static CampaignMetricRecord Normalize(CampaignMetricRecord record)
    {
        return record with { BuildFamilySelectionCounts = MetricCount.Normalize(record.BuildFamilySelectionCounts) };
    }

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void WriteLines(string path, IEnumerable<string> lines)
    {
        var text = string.Join("\n", lines);
        if (text.Length > 0)
        {
            text += "\n";
        }

        File.WriteAllText(path, text, Utf8WithoutBom);
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string BuildBattleCsv(IEnumerable<BattleMetricRecord> records)
    {
        var rows = new List<string>
        {
            "schema_version,run_id,campaign_id,battle_id,replay_group_id,replay_iteration,scenario_id,policy_id,build_family_id,opponent_family_id,ally_formation_id,enemy_formation_id,ally_build_component_counts,enemy_build_component_counts,intentional_hard_counter,seed,fixed_step_seconds,step_count,duration_seconds,winner_side,timeout,stomp,first_death_side,ally_surviving_hp,enemy_surviving_hp,final_hp_difference,normalized_final_power_difference,flank_strike_count,rear_strike_count,screen_block_count,screen_absorb_count,screen_deterrence_count,save_moment_count,backline_dive_kill_count,synergy_rule_counts,combo_rule_counts,augment_rule_counts,doctrine_rule_counts,eligible_depth_rule_ids,fired_depth_rule_ids,causal_depth_rule_ids,salient_event_count,causal_salient_event_count,formation_win_rate_leverage,formation_sensitive,default_formation_was_optimal,replay_hash,canonical_state_hash,activity_replay_hash,crashed,softlocked,contains_non_finite,illegal_negative_state,non_terminating,failure_code"
        };
        rows.AddRange(records.Select(record => string.Join(",", new[]
        {
            Csv(record.SchemaVersion), Csv(record.RunId), Csv(record.CampaignId), Csv(record.BattleId),
            Csv(record.ReplayGroupId), Number(record.ReplayIteration), Csv(record.ScenarioId), Csv(record.PolicyId),
            Csv(record.BuildFamilyId), Csv(record.OpponentFamilyId), Csv(record.AllyFormationId),
            Csv(record.EnemyFormationId), Csv(Counts(record.AllyBuildComponentCounts)),
            Csv(Counts(record.EnemyBuildComponentCounts)), Bool(record.IntentionalHardCounter),
            Number(record.Seed), Number(record.FixedStepSeconds), Number(record.StepCount),
            Number(record.DurationSeconds), Csv(record.WinnerSide), Bool(record.Timeout), Bool(record.Stomp),
            Csv(record.FirstDeathSide), Number(record.AllySurvivingHp), Number(record.EnemySurvivingHp),
            Number(record.FinalHpDifference), Number(record.NormalizedFinalPowerDifference), Number(record.FlankStrikeCount),
            Number(record.RearStrikeCount), Number(record.ScreenBlockCount), Number(record.ScreenAbsorbCount),
            Number(record.ScreenDeterrenceCount), Number(record.SaveMomentCount),
            Number(record.BacklineDiveKillCount), Csv(Counts(record.SynergyRuleActivationCounts)),
            Csv(Counts(record.ComboRuleActivationCounts)), Csv(Counts(record.AugmentRuleActivationCounts)),
            Csv(Counts(record.DoctrineRuleActivationCounts)), Csv(Ids(record.EligibleDepthRuleIds)),
            Csv(Ids(record.FiredDepthRuleIds)), Csv(Ids(record.CausalDepthRuleIds)),
            Number(record.SalientEventCount), Number(record.CausalSalientEventCount),
            NullableNumber(record.FormationWinRateLeverage), NullableBool(record.FormationSensitive),
            NullableBool(record.DefaultFormationWasOptimal), Csv(record.ReplayHash), Csv(record.CanonicalStateHash),
            Csv(record.ActivityReplayHash),
            Bool(record.Crashed), Bool(record.Softlocked), Bool(record.ContainsNonFinite), Bool(record.IllegalNegativeState),
            Bool(record.NonTerminating), Csv(record.FailureCode),
        })));
        return string.Join("\n", rows) + "\n";
    }

    private static string BuildCampaignCsv(IEnumerable<CampaignMetricRecord> records)
    {
        var rows = new List<string>
        {
            "schema_version,run_id,campaign_id,policy_id,difficulty_id,seed,completed,truncated,terminal_reason,site_count,battle_count,win_count,loss_count,timeout_count,stomp_count,forced_timeout_count,total_battle_seconds,decision_count,decision_metrics_available,important_decision_count,near_best_alternative_decision_count,high_leverage_decision_count,macro_family_id,build_family_counts,crash_count,softlock_count,non_finite_state_count,illegal_negative_state_count,non_terminating_battle_count,replay_manifest_hash"
        };
        rows.AddRange(records.Select(record => string.Join(",", new[]
        {
            Csv(record.SchemaVersion), Csv(record.RunId), Csv(record.CampaignId), Csv(record.PolicyId),
            Csv(record.DifficultyId), Number(record.Seed), Bool(record.Completed), Bool(record.Truncated), Csv(record.TerminalReason),
            Number(record.SiteCount), Number(record.BattleCount), Number(record.WinCount), Number(record.LossCount),
            Number(record.TimeoutCount), Number(record.StompCount), Number(record.ForcedTimeoutCount),
            Number(record.TotalBattleSeconds), Number(record.DecisionCount), Bool(record.DecisionMetricsAvailable),
            Number(record.ImportantDecisionCount), Number(record.NearBestAlternativeDecisionCount),
            Number(record.HighLeverageDecisionCount), Csv(record.MacroFamilyId),
            Csv(Counts(record.BuildFamilySelectionCounts)), Number(record.CrashCount), Number(record.SoftlockCount),
            Number(record.NonFiniteStateCount), Number(record.IllegalNegativeStateCount),
            Number(record.NonTerminatingBattleCount), Csv(record.ReplayManifestHash),
        })));
        return string.Join("\n", rows) + "\n";
    }

    private static string Counts(IEnumerable<MetricCount> values)
        => string.Join(";", MetricCount.Normalize(values).Select(value => $"{value.Id}={value.Count.ToString(CultureInfo.InvariantCulture)}"));

    private static string Ids(IEnumerable<string> values) => string.Join(";", NormalizeStrings(values));

    private static string Csv(string? value)
        => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string NullableNumber(float? value) => value.HasValue ? Number(value.Value) : string.Empty;

    private static string Bool(bool value) => value ? "true" : "false";

    private static string NullableBool(bool? value) => value.HasValue ? Bool(value.Value) : string.Empty;
}
