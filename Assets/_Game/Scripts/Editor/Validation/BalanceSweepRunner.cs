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
        BattleReplayBundle Replay,
        BattleRunMetrics Metrics);

    private sealed record BattleRunMetrics(
        float TimeToFirstMeaningfulActionSeconds,
        float AverageRepositionCount,
        float AverageTargetAccessTimeSeconds,
        float AverageFrontlineSurvivalTimeSeconds,
        IReadOnlyDictionary<string, float> DamageTakenTotals);

    private sealed class SweepMetricTracker
    {
        private readonly HashSet<string> _allyIds;
        private readonly HashSet<string> _enemyIds;
        private readonly HashSet<string> _frontlineIds;
        private readonly Dictionary<string, CombatActionState> _lastActionStates;
        private readonly Dictionary<string, int> _repositionCounts;
        private readonly Dictionary<string, float> _firstTargetAccessTimes;
        private readonly Dictionary<string, float> _deathTimes;
        private readonly Dictionary<string, string> _allyNames;
        private readonly Dictionary<string, float> _damageTakenByUnit;
        private float? _firstMeaningfulActionSeconds;

        public SweepMetricTracker(BattleState state)
        {
            _allyIds = state.Allies.Select(unit => unit.Id.Value).ToHashSet(StringComparer.Ordinal);
            _enemyIds = state.Enemies.Select(unit => unit.Id.Value).ToHashSet(StringComparer.Ordinal);
            _frontlineIds = state.Allies
                .Where(unit => unit.Anchor.IsFrontRow())
                .Select(unit => unit.Id.Value)
                .ToHashSet(StringComparer.Ordinal);
            _lastActionStates = state.Allies.ToDictionary(unit => unit.Id.Value, unit => unit.ActionState, StringComparer.Ordinal);
            _repositionCounts = state.Allies.ToDictionary(unit => unit.Id.Value, _ => 0, StringComparer.Ordinal);
            _firstTargetAccessTimes = new Dictionary<string, float>(StringComparer.Ordinal);
            _deathTimes = new Dictionary<string, float>(StringComparer.Ordinal);
            _allyNames = state.Allies.ToDictionary(unit => unit.Id.Value, unit => unit.Definition.Name, StringComparer.Ordinal);
            _damageTakenByUnit = state.Allies.ToDictionary(unit => unit.Id.Value, _ => 0f, StringComparer.Ordinal);
        }

        public void Observe(BattleSimulationStep step)
        {
            foreach (var unit in step.Units.Where(unit => _allyIds.Contains(unit.Id)))
            {
                if (_lastActionStates.TryGetValue(unit.Id, out var previousState)
                    && !IsRepositionState(previousState)
                    && IsRepositionState(unit.ActionState))
                {
                    _repositionCounts[unit.Id] = _repositionCounts.TryGetValue(unit.Id, out var current)
                        ? current + 1
                        : 1;
                }

                _lastActionStates[unit.Id] = unit.ActionState;

                if (!unit.IsAlive && !_deathTimes.ContainsKey(unit.Id))
                {
                    _deathTimes[unit.Id] = step.TimeSeconds;
                }
            }

            foreach (var @event in step.Events)
            {
                var actorId = @event.ActorId.Value;
                var targetId = @event.TargetId?.Value ?? string.Empty;

                if (_firstMeaningfulActionSeconds is null && IsMeaningfulAction(@event, actorId))
                {
                    _firstMeaningfulActionSeconds = @event.TimeSeconds;
                }

                if (_allyIds.Contains(actorId)
                    && _enemyIds.Contains(targetId)
                    && !_firstTargetAccessTimes.ContainsKey(actorId)
                    && IsDamageEvent(@event))
                {
                    _firstTargetAccessTimes[actorId] = @event.TimeSeconds;
                }

                if (_allyIds.Contains(targetId) && IsDamageTakenEvent(@event))
                {
                    _damageTakenByUnit[targetId] = _damageTakenByUnit.TryGetValue(targetId, out var current)
                        ? current + @event.Value
                        : @event.Value;

                    if (!_allyNames.ContainsKey(targetId) && !string.IsNullOrWhiteSpace(@event.TargetName))
                    {
                        _allyNames[targetId] = @event.TargetName;
                    }
                }
            }
        }

        public BattleRunMetrics Build(float durationSeconds)
        {
            var frontlineIds = _frontlineIds.Count > 0 ? _frontlineIds : _allyIds;
            var averageRepositionCount = (float)_repositionCounts.Values.DefaultIfEmpty(0).Average();
            var averageTargetAccessTime = (float)_allyIds
                .Select(id => _firstTargetAccessTimes.TryGetValue(id, out var value) ? value : durationSeconds)
                .DefaultIfEmpty(durationSeconds)
                .Average();
            var averageFrontlineSurvival = (float)frontlineIds
                .Select(id => _deathTimes.TryGetValue(id, out var value) ? value : durationSeconds)
                .DefaultIfEmpty(durationSeconds)
                .Average();

            return new BattleRunMetrics(
                _firstMeaningfulActionSeconds ?? durationSeconds,
                averageRepositionCount,
                averageTargetAccessTime,
                averageFrontlineSurvival,
                BuildDamageTakenTotals());
        }

        private IReadOnlyDictionary<string, float> BuildDamageTakenTotals()
        {
            return _damageTakenByUnit
                .Where(pair => pair.Value > 0f)
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => _allyNames.TryGetValue(pair.Key, out var name) && !string.IsNullOrWhiteSpace(name) ? name : pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);
        }

        private bool IsMeaningfulAction(BattleEvent @event, string actorId)
        {
            if (!_allyIds.Contains(actorId) || @event.ActionType == BattleActionType.WaitDefend)
            {
                return false;
            }

            if (@event.EventKind != BattleEventKind.Action)
            {
                return true;
            }

            return @event.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage or BattleLogCode.ActiveSkillHeal;
        }

        private static bool IsRepositionState(CombatActionState state)
        {
            return state is CombatActionState.Reposition or CombatActionState.BreakContact or CombatActionState.SecurePosition;
        }

        private static bool IsDamageEvent(BattleEvent @event)
        {
            return @event.Value > 0f && @event.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage;
        }

        private static bool IsDamageTakenEvent(BattleEvent @event)
        {
            return @event.Value > 0f
                   && (@event.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage
                       || (@event.LogCode == BattleLogCode.Generic && string.Equals(@event.Note, "status_tick", StringComparison.Ordinal)));
        }
    }

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

        return BalanceSweepReportWriter.Write(report, GetDefaultReportDirectory());
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
        var averageMeaningfulAction = seededRuns.Average(run => run.Metrics.TimeToFirstMeaningfulActionSeconds);
        var averageRepositionCount = seededRuns.Average(run => run.Metrics.AverageRepositionCount);
        var averageTargetAccessTime = seededRuns.Average(run => run.Metrics.AverageTargetAccessTimeSeconds);
        var averageFrontlineSurvival = seededRuns.Average(run => run.Metrics.AverageFrontlineSurvivalTimeSeconds);
        var damageShare = BuildShareReport(seededRuns, BattleLogCode.BasicAttackDamage, BattleLogCode.ActiveSkillDamage);
        var healShare = BuildShareReport(seededRuns, BattleLogCode.ActiveSkillHeal);
        var damageTakenDistribution = BuildTakenShareReport(seededRuns);
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
            TimeToFirstMeaningfulActionSeconds = averageMeaningfulAction,
            AverageRepositionCount = averageRepositionCount,
            AverageTargetAccessTimeSeconds = averageTargetAccessTime,
            AverageFrontlineSurvivalTimeSeconds = averageFrontlineSurvival,
            TemporaryAugmentDeadOfferRatio = deadOfferRatio,
            ValidationErrorCount = validationReport.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error),
            ValidationWarningCount = validationReport.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning),
            DamageShare = damageShare,
            HealShare = healShare,
            DamageSourceDistribution = damageShare,
            DamageTakenDistribution = damageTakenDistribution,
            Flags = flags,
        };
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
        var simulator = new BattleSimulator(state, BattleSimulator.DefaultMaxSteps);
        var tracker = new SweepMetricTracker(state);
        while (!simulator.IsFinished)
        {
            tracker.Observe(simulator.Step());
        }

        var result = simulator.RunToEnd();
        var metrics = tracker.Build(result.DurationSeconds);
        var timestamp = $"seed:{seed}";
        var replay = ReplayAssembler.Assemble(playerSnapshot, enemyLoadout, result, seed, timestamp, timestamp);
        return new BattleRunDigest(result, replay, metrics);
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

    private static IReadOnlyList<BalanceSweepShareEntry> BuildTakenShareReport(IReadOnlyList<BattleRunDigest> runs)
    {
        var totals = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            foreach (var entry in run.Metrics.DamageTakenTotals)
            {
                totals[entry.Key] = totals.TryGetValue(entry.Key, out var current)
                    ? current + entry.Value
                    : entry.Value;
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

}
