using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Services;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class BalanceSweepRunner
{
    private const string ReportFolderName = "Logs/balance-sweep";
    private const string JsonReportFileName = "balance-sweep-report.json";
    private const string CsvReportFileName = "balance-sweep-summary.csv";

    private sealed record BattleRunDigest(
        BattleResult Result,
        BattleReplayBundle Replay);

    [MenuItem("SM/Validation/Run Balance Sweep Smoke")]
    public static void RunSmokeMenu()
    {
        var report = RunSmokeAndWriteReport();
        foreach (var outlier in report.Outliers)
        {
            Debug.LogWarning($"[BalanceSweep] {outlier}");
        }

        Debug.Log($"[BalanceSweep] Report written. Json={report.JsonReportPath} Csv={report.CsvReportPath}");
    }

    [MenuItem("SM/Validation/Run Balance Sweep Smoke", true)]
    public static bool ValidateRunSmokeMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public static string GetDefaultReportDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFolderName));
    }

    public static BalanceSweepReport RunSmokeAndWriteReport()
    {
        var validationReport = ContentDefinitionValidator.ValidateAndWriteReport();
        var baselineInputs = BalanceSweepScenarioFactory.BuildSmokeScenarios();
        var determinismInputs = BalanceSweepScenarioFactory.BuildSmokeScenarios()
            .ToDictionary(input => input.ScenarioId, StringComparer.Ordinal);

        var scenarioReports = baselineInputs
            .Select(input => BuildScenarioReport(input, determinismInputs[input.ScenarioId], validationReport))
            .ToList();

        var mixedScenario = scenarioReports.FirstOrDefault(report => string.Equals(report.ScenarioId, "mixed_floor_control", StringComparison.Ordinal));
        var focusedScenario = scenarioReports.FirstOrDefault(report => string.Equals(report.ScenarioId, "focused_beastkin_push", StringComparison.Ordinal));
        var synergyUpliftWinRate = focusedScenario == null || mixedScenario == null
            ? 0f
            : focusedScenario.WinRate - mixedScenario.WinRate;
        var synergyUpliftDuration = focusedScenario == null || mixedScenario == null
            ? 0f
            : focusedScenario.AverageBattleDurationSeconds - mixedScenario.AverageBattleDurationSeconds;

        var outliers = scenarioReports
            .SelectMany(report => report.Flags.Select(flag => $"{report.ScenarioId}:{flag}"))
            .ToList();
        if (focusedScenario != null && mixedScenario != null && synergyUpliftWinRate < -0.05f)
        {
            outliers.Add($"global:synergy_uplift_negative ({synergyUpliftWinRate.ToString("0.###", CultureInfo.InvariantCulture)})");
        }

        var report = new BalanceSweepReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            ValidationReportPath = validationReport.JsonReportPath,
            ValidationErrorCount = validationReport.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error),
            ValidationWarningCount = validationReport.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning),
            SynergyTierUpliftWinRate = synergyUpliftWinRate,
            SynergyTierUpliftDurationDeltaSeconds = synergyUpliftDuration,
            Outliers = outliers,
            Scenarios = scenarioReports,
        };

        return WriteReport(report);
    }

    private static BalanceSweepScenarioReport BuildScenarioReport(
        BalanceSweepScenarioInput input,
        BalanceSweepScenarioInput determinismInput,
        ContentValidationReport validationReport)
    {
        var compileHashDeterministic = string.Equals(
            input.PlayerSnapshot.CompileHash,
            determinismInput.PlayerSnapshot.CompileHash,
            StringComparison.Ordinal);

        var deterministicSeed = input.Seeds[0];
        var firstDeterminismRun = RunBattle(input.PlayerSnapshot, input.EnemyLoadout, deterministicSeed);
        var secondDeterminismRun = RunBattle(input.PlayerSnapshot, input.EnemyLoadout, deterministicSeed);
        var finalStateDeterministic =
            string.Equals(firstDeterminismRun.Replay.Header.FinalStateHash, secondDeterminismRun.Replay.Header.FinalStateHash, StringComparison.Ordinal)
            && firstDeterminismRun.Result.Winner == secondDeterminismRun.Result.Winner
            && firstDeterminismRun.Result.StepCount == secondDeterminismRun.Result.StepCount;

        var seededRuns = input.Seeds
            .Select(seed => RunBattle(input.PlayerSnapshot, input.EnemyLoadout, seed))
            .ToList();

        var totalRuns = Math.Max(1, seededRuns.Count);
        var winRate = seededRuns.Count(run => run.Result.Winner == TeamSide.Ally) / (float)totalRuns;
        var averageDuration = seededRuns.Average(run => run.Result.DurationSeconds);
        var averageFirstCast = seededRuns.Average(run => ResolveFirstCastSeconds(run.Result));
        var damageShare = BuildShareReport(seededRuns, BattleLogCode.BasicAttackDamage, BattleLogCode.ActiveSkillDamage);
        var healShare = BuildShareReport(seededRuns, BattleLogCode.ActiveSkillHeal);
        var deadOfferRatio = ResolveTemporaryAugmentDeadOfferRatio(input);

        var flags = new List<string>();
        if (!compileHashDeterministic)
        {
            flags.Add("compile_hash_nondeterministic");
        }

        if (!finalStateDeterministic)
        {
            flags.Add("battle_result_nondeterministic");
        }

        if (validationReport.Issues.Any(issue => issue.Severity == ContentValidationSeverity.Error))
        {
            flags.Add("validation_errors_present");
        }

        if (averageFirstCast > 3.0f)
        {
            flags.Add("first_cast_over_3_0s");
        }

        if (averageDuration < 4f || averageDuration > 40f)
        {
            flags.Add("battle_duration_out_of_band");
        }

        if (deadOfferRatio > 0.6f)
        {
            flags.Add("dead_offer_ratio_over_0_6");
        }

        return new BalanceSweepScenarioReport
        {
            ScenarioId = input.ScenarioId,
            Description = input.Description,
            TeamTacticId = input.TeamTacticId,
            CompileHash = input.PlayerSnapshot.CompileHash,
            CompileHashDeterministic = compileHashDeterministic,
            FinalStateDeterministic = finalStateDeterministic,
            WinRate = winRate,
            AverageBattleDurationSeconds = averageDuration,
            AverageFirstCastSeconds = averageFirstCast,
            TemporaryAugmentDeadOfferRatio = deadOfferRatio,
            ValidationErrorCount = validationReport.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error),
            ValidationWarningCount = validationReport.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning),
            DamageShare = damageShare,
            HealShare = healShare,
            Flags = flags,
        };
    }

    private static BalanceSweepReport WriteReport(BalanceSweepReport report)
    {
        var reportDirectory = GetDefaultReportDirectory();
        Directory.CreateDirectory(reportDirectory);

        var jsonPath = Path.Combine(reportDirectory, JsonReportFileName);
        var csvPath = Path.Combine(reportDirectory, CsvReportFileName);
        var withPaths = report with
        {
            JsonReportPath = jsonPath,
            CsvReportPath = csvPath,
        };

        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(withPaths, Formatting.Indented));
        File.WriteAllText(csvPath, BuildCsvSummary(withPaths));
        return withPaths;
    }

    private static BattleRunDigest RunBattle(
        BattleLoadoutSnapshot playerSnapshot,
        IReadOnlyList<BattleUnitLoadout> enemyLoadout,
        int seed)
    {
        var state = BattleFactory.Create(
            playerSnapshot.Allies,
            enemyLoadout,
            playerSnapshot.TeamTactic.Posture,
            enemyLoadout.FirstOrDefault()?.TeamTactic?.Posture ?? TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds,
            seed);
        var simulator = new BattleSimulator(state, 300);
        var result = simulator.RunToEnd();
        var timestamp = $"seed:{seed}";
        var replay = ReplayAssembler.Assemble(playerSnapshot, enemyLoadout, result, seed, timestamp, timestamp);
        return new BattleRunDigest(result, replay);
    }

    private static float ResolveFirstCastSeconds(BattleResult result)
    {
        return result.Events
            .Where(@event => @event.ActionType == BattleActionType.ActiveSkill)
            .Select(@event => @event.TimeSeconds)
            .DefaultIfEmpty(result.DurationSeconds)
            .First();
    }

    private static IReadOnlyList<BalanceSweepShareEntry> BuildShareReport(
        IReadOnlyList<BattleRunDigest> runs,
        params BattleLogCode[] logCodes)
    {
        var totals = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            foreach (var @event in run.Result.Events.Where(@event => logCodes.Contains(@event.LogCode) && @event.ActorId.Value.StartsWith("ally_", StringComparison.Ordinal)))
            {
                totals[@event.ActorName] = totals.TryGetValue(@event.ActorName, out var current)
                    ? current + @event.Value
                    : @event.Value;
            }
        }

        var grandTotal = totals.Values.Sum();
        if (grandTotal <= 0f)
        {
            return Array.Empty<BalanceSweepShareEntry>();
        }

        return totals
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new BalanceSweepShareEntry(pair.Key, pair.Value / grandTotal))
            .ToList();
    }

    private static float ResolveTemporaryAugmentDeadOfferRatio(BalanceSweepScenarioInput input)
    {
        var temporaryAugments = input.Content.AugmentCatalog.Values
            .Where(augment => !augment.IsPermanent)
            .ToList();
        if (temporaryAugments.Count == 0)
        {
            return 0f;
        }

        var teamTags = new HashSet<string>(input.PlayerSnapshot.TeamTags ?? Array.Empty<string>(), StringComparer.Ordinal);
        var deadCount = temporaryAugments.Count(augment =>
            augment.Tags.Count > 0
            && !augment.Tags.Any(tag => teamTags.Contains(tag)));
        return deadCount / (float)temporaryAugments.Count;
    }

    private static string BuildCsvSummary(BalanceSweepReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("scenario_id,team_tactic_id,compile_hash_deterministic,final_state_deterministic,win_rate,avg_battle_duration_seconds,avg_first_cast_seconds,temporary_augment_dead_offer_ratio,validation_errors,validation_warnings,flags");
        foreach (var scenario in report.Scenarios)
        {
            builder.Append(scenario.ScenarioId).Append(',')
                .Append(scenario.TeamTacticId).Append(',')
                .Append(scenario.CompileHashDeterministic ? "true" : "false").Append(',')
                .Append(scenario.FinalStateDeterministic ? "true" : "false").Append(',')
                .Append(scenario.WinRate.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.AverageBattleDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(scenario.AverageFirstCastSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
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
