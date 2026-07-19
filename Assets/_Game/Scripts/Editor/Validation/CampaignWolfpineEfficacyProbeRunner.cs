using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Editor.SeedData;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>
/// Wolfpine C2b efficacy diagnostic. This observer consumes the canonical two-arm cell/session path and
/// existing combat state/event telemetry; it never changes battle state, content, or simulation rules.
/// </summary>
internal static partial class CampaignTwoArmSweepRunner
{
    private const string EfficacyEncounterId = "site_wolfpine_trail_boss_1";
    private const string EfficacyBossCharacterId = "npc_grey_fang";

    internal static CampaignWolfpineEfficacyReport RunWolfpineEfficacyProbe(int cellsPerSquad)
    {
        var config = CampaignBalanceSweepConfig.Default;
        config.Validate();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignWolfpineEfficacyProbeEntryPoint));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"campaign efficacy probe content unavailable: {contentError}");
        }

        var itemIndex = CampaignBalanceSweepRunner.LoadItemMetaIndex();
        var order = CampaignContentOrderIndex.Build(content);
        var grid = config.BuildGrid();
        var naiveArm = config.Arms.Single(arm => string.Equals(arm.ArmId, "naive", StringComparison.Ordinal));
        var accumulator = new CampaignTwoArmSweepAccumulator(config);
        var seedSet = new HashSet<int>();
        var selectedCellIds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var reports = new Dictionary<string, CampaignWolfpineEfficacySquadReport>(StringComparer.Ordinal);

        foreach (var squad in config.ReferenceSquads)
        {
            var candidates = grid
                .Where(cell => string.Equals(cell.Squad.SquadId, squad.SquadId, StringComparison.Ordinal))
                .ToArray();
            var sampleCount = Math.Clamp(cellsPerSquad, 1, candidates.Length);
            var sampledCells = Enumerable.Range(0, sampleCount)
                .Select(index => candidates[(int)Math.Floor(index * candidates.Length / (double)sampleCount)])
                .ToArray();
            selectedCellIds[squad.SquadId] = sampledCells.Select(cell => cell.CellId).ToArray();

            var squadAccumulator = new EfficacySquadAccumulator(squad.SquadId);
            foreach (var cell in sampledCells)
            {
                RunCell(
                    lookup,
                    itemIndex,
                    order,
                    config,
                    naiveArm,
                    cell,
                    accumulator,
                    EfficacyEncounterId,
                    (state, encounter) =>
                    {
                        if (!string.Equals(encounter.Context.EncounterId, EfficacyEncounterId, StringComparison.Ordinal))
                        {
                            return BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
                        }

                        seedSet.Add(encounter.Context.BattleSeed);
                        var observation = EfficacyBattleObserver.Run(state, encounter.Context.BattleSeed, cell.CellId);
                        squadAccumulator.Add(observation);
                        return observation.Result;
                    });
            }

            reports[squad.SquadId] = squadAccumulator.Build();
        }

        return new CampaignWolfpineEfficacyReport(
            "campaign-wolfpine-efficacy-v1",
            EfficacyEncounterId,
            naiveArm.ArmId,
            Math.Clamp(cellsPerSquad, 1, grid.Count / config.ReferenceSquads.Count),
            seedSet.OrderBy(seed => seed).ToArray(),
            selectedCellIds,
            reports);
    }

    private sealed class EfficacyBattleObserver
    {
        private readonly BattleState _state;
        private readonly string _bossUnitId;
        private readonly int _battleSeed;
        private readonly string _cellId;
        private readonly List<int> _enemyBacklineDiveKillTicks = new();
        private int _tickSamples;
        private int _concurrentDiveSum;
        private int _maxConcurrentDivers;
        private bool _everTwoConcurrentDivers;
        private bool _everTwoDistinctDiveTargets;
        private int _allBacklineDiveKillEvents;
        private int? _bossDeathTick;

        private EfficacyBattleObserver(BattleState state, int battleSeed, string cellId)
        {
            _state = state;
            _battleSeed = battleSeed;
            _cellId = cellId;
            _bossUnitId = state.AllUnits
                .Single(unit => unit.Side == TeamSide.Enemy
                                && string.Equals(unit.Definition.CharacterId, EfficacyBossCharacterId, StringComparison.Ordinal))
                .Id.Value;
        }

        public static EfficacyBattleObservation Run(BattleState state, int battleSeed, string cellId)
        {
            var observer = new EfficacyBattleObserver(state, battleSeed, cellId);
            var result = BattleResolver.Run(
                state,
                BattleSimulator.DefaultMaxSteps,
                observer.ObserveStep);
            return observer.Build(result);
        }

        private void ObserveStep(BattleSimulationStep step)
        {
            var enemyDivers = _state.AllUnits
                .Where(unit => unit.Side == TeamSide.Enemy
                               && unit.IsAlive
                               && unit.CurrentCombatIntent.Type == CombatIntentType.Dive)
                .ToArray();
            var concurrentDivers = enemyDivers.Length;
            _tickSamples++;
            _concurrentDiveSum += concurrentDivers;
            _maxConcurrentDivers = Math.Max(_maxConcurrentDivers, concurrentDivers);
            if (concurrentDivers >= 2)
            {
                _everTwoConcurrentDivers = true;
                var distinctTargets = enemyDivers
                    .Where(unit => unit.CurrentCombatIntent.TargetId != null)
                    .Select(unit => unit.CurrentCombatIntent.TargetId!.Value.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                _everTwoDistinctDiveTargets |= distinctTargets >= 2;
            }

            foreach (var battleEvent in step.Events.Where(value => value.EventKind == BattleEventKind.Kill))
            {
                if (battleEvent.KillPayload is { IsBacklineDiveKill: true })
                {
                    _allBacklineDiveKillEvents++;
                    var actorId = !string.IsNullOrWhiteSpace(battleEvent.KillPayload.ActualKiller.Value)
                        ? battleEvent.KillPayload.ActualKiller
                        : battleEvent.ActorId;
                    if (_state.FindUnit(actorId)?.Side == TeamSide.Enemy)
                    {
                        _enemyBacklineDiveKillTicks.Add(step.StepIndex);
                    }
                }

                var victimId = battleEvent.KillPayload?.ActualVictim.Value
                               ?? battleEvent.TargetId?.Value;
                if (_bossDeathTick == null && string.Equals(victimId, _bossUnitId, StringComparison.Ordinal))
                {
                    _bossDeathTick = step.StepIndex;
                }
            }
        }

        private EfficacyBattleObservation Build(BattleResult result)
        {
            var recordedBacklineKills = result.ActivityTelemetry?.BacklineDiveKillCount ?? 0;
            if (recordedBacklineKills != _allBacklineDiveKillEvents)
            {
                throw new InvalidOperationException(
                    $"BacklineDiveKill telemetry mismatch for {_cellId}: snapshot={recordedBacklineKills}, events={_allBacklineDiveKillEvents}.");
            }

            if (_bossDeathTick == null
                && result.Winner == TeamSide.Ally
                && _state.AllUnits.Single(unit => string.Equals(unit.Id.Value, _bossUnitId, StringComparison.Ordinal)).IsAlive == false)
            {
                _bossDeathTick = result.StepCount;
            }

            return new EfficacyBattleObservation(
                _cellId,
                _battleSeed,
                result,
                _maxConcurrentDivers,
                _tickSamples == 0 ? 0d : _concurrentDiveSum / (double)_tickSamples,
                _everTwoConcurrentDivers,
                _everTwoDistinctDiveTargets,
                _enemyBacklineDiveKillTicks.ToArray(),
                _bossDeathTick);
        }
    }

    private sealed class EfficacySquadAccumulator
    {
        private readonly string _squadId;
        private readonly List<EfficacyBattleObservation> _battles = new();

        public EfficacySquadAccumulator(string squadId)
        {
            _squadId = squadId;
        }

        public void Add(EfficacyBattleObservation battle)
        {
            _battles.Add(battle);
        }

        public CampaignWolfpineEfficacySquadReport Build()
        {
            if (_battles.Count == 0)
            {
                throw new InvalidOperationException($"No efficacy battles were recorded for {_squadId}.");
            }

            var wins = _battles.Where(battle => battle.Result.Winner == TeamSide.Ally).ToArray();
            var losses = _battles.Where(battle => battle.Result.Winner != TeamSide.Ally).ToArray();
            var battlesWithTwoDivers = _battles.Where(battle => battle.EverTwoConcurrentDivers).ToArray();
            var bossDeathTicksInWins = wins
                .Where(battle => battle.BossDeathTick != null)
                .Select(battle => battle.BossDeathTick!.Value)
                .ToArray();
            var bossDeathFractionsInWins = wins
                .Where(battle => battle.BossDeathTick != null && battle.Result.StepCount > 0)
                .Select(battle => battle.BossDeathTick!.Value / (double)battle.Result.StepCount)
                .ToArray();
            var killTicks = _battles.SelectMany(battle => battle.EnemyBacklineDiveKillTicks).ToArray();

            return new CampaignWolfpineEfficacySquadReport(
                _squadId,
                _battles.Count,
                wins.Length,
                losses.Length,
                wins.Length / (double)_battles.Count,
                _battles.Max(battle => battle.MaxConcurrentDivers),
                _battles.Average(battle => battle.MeanConcurrentDivers),
                _battles.Average(battle => battle.MaxConcurrentDivers),
                battlesWithTwoDivers.Length / (double)_battles.Count,
                battlesWithTwoDivers.Length == 0
                    ? 0d
                    : battlesWithTwoDivers.Count(battle => battle.EverTwoDistinctDiveTargets) / (double)battlesWithTwoDivers.Length,
                _battles.Average(battle => battle.EnemyBacklineDiveKillTicks.Count),
                Describe(killTicks.Select(value => (double)value)),
                Describe(bossDeathTicksInWins.Select(value => (double)value)),
                Describe(bossDeathFractionsInWins),
                Describe(_battles.Select(battle => (double)battle.Result.StepCount)),
                Describe(wins.Select(battle => (double)battle.Result.StepCount)),
                Describe(losses.Select(battle => (double)battle.Result.StepCount)));
        }
    }

    private static EfficacyDistribution Describe(IEnumerable<double> source)
    {
        var values = source.OrderBy(value => value).ToArray();
        if (values.Length == 0)
        {
            return new EfficacyDistribution(0, null, null, null, null, null);
        }

        return new EfficacyDistribution(
            values.Length,
            values[0],
            Quantile(values, .25),
            Quantile(values, .5),
            Quantile(values, .75),
            values[^1]);
    }

    private static double Quantile(IReadOnlyList<double> sortedValues, double quantile)
    {
        var position = (sortedValues.Count - 1) * quantile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = position - lower;
        return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * weight);
    }
}

internal sealed record EfficacyBattleObservation(
    string CellId,
    int BattleSeed,
    BattleResult Result,
    int MaxConcurrentDivers,
    double MeanConcurrentDivers,
    bool EverTwoConcurrentDivers,
    bool EverTwoDistinctDiveTargets,
    IReadOnlyList<int> EnemyBacklineDiveKillTicks,
    int? BossDeathTick);

internal sealed record EfficacyDistribution(
    int Count,
    double? Min,
    double? P25,
    double? Median,
    double? P75,
    double? Max);

internal sealed record CampaignWolfpineEfficacySquadReport(
    string SquadId,
    int Battles,
    int Wins,
    int Losses,
    double NaiveWinRate,
    int MaxConcurrentDiversObserved,
    double MeanConcurrentDiversPerTick,
    double MeanMaxConcurrentDivers,
    double FractionBattlesWithAtLeastTwoConcurrentDivers,
    double FractionAtLeastTwoBattlesWithDistinctTargets,
    double EnemyBacklineDiveKillsMean,
    EfficacyDistribution EnemyBacklineDiveKillTickDistribution,
    EfficacyDistribution BossDeathTickInWins,
    EfficacyDistribution BossDeathFractionInWins,
    EfficacyDistribution BattleLengthTicks,
    EfficacyDistribution WinningBattleLengthTicks,
    EfficacyDistribution LosingBattleLengthTicks);

internal sealed record CampaignWolfpineEfficacyReport(
    string SchemaVersion,
    string EncounterId,
    string Arm,
    int CellsPerSquad,
    IReadOnlyList<int> BattleSeeds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SelectedCellIds,
    IReadOnlyDictionary<string, CampaignWolfpineEfficacySquadReport> PerSquad);

public static class CampaignWolfpineEfficacyProbeEntryPoint
{
    private const string OutputRelativePath = "Logs/campaign-wolfpine-efficacy/efficacy-envelope-report.json";

    public static void RunFromCli()
    {
        try
        {
            const int defaultCellsPerSquad = 64;
            var requested = Environment.GetEnvironmentVariable("SM_CAMPAIGN_EFFICACY_CELLS_PER_SQUAD");
            var cellsPerSquad = int.TryParse(requested, out var parsed)
                ? Math.Clamp(parsed, 1, 160)
                : defaultCellsPerSquad;
            var report = CampaignTwoArmSweepRunner.RunWolfpineEfficacyProbe(cellsPerSquad);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputPath = Path.Combine(projectRoot, OutputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log($"[CampaignWolfpineEfficacy] report={outputPath}");
            Debug.Log($"[CampaignWolfpineEfficacy] {JsonConvert.SerializeObject(report, Formatting.None)}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
