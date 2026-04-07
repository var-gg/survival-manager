using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class FirstPlayableBalanceRunner
{
    private const string ReportFolderName = "Logs/loop-d-balance";
    private const string PureKitReportFileName = "purekit_report.json";
    private const string SystemicSliceReportFileName = "systemic_slice_report.json";
    private const string RunLiteReportFileName = "runlite_report.json";
    private const string ContentHealthCsvFileName = "content_health_cards.csv";
    private const string PruneLedgerFileName = "prune_ledger_v1.json";
    private const string ReadabilityWatchlistFileName = "readability_watchlist.json";
    private const string ClosureNoteFileName = "loop_d_closure_note.txt";

    private sealed record LoopDScenarioInput(
        BalanceScenarioId ScenarioId,
        string Description,
        BattleLoadoutSnapshot PlayerSnapshot,
        IReadOnlyList<BattleUnitLoadout> EnemyLoadout,
        IReadOnlyList<int> Seeds);

    private sealed record LoopDBattleDigest(
        BalanceScenarioId ScenarioId,
        BattleResult Result,
        BattleReplayBundle Replay);

    [Serializable]
    public sealed class LoopDScenarioReport
    {
        public string ScenarioId = string.Empty;
        public int RunCount = 0;
        public float WinRate = 0f;
        public float BattleDurationP50 = 0f;
        public float BattleDurationP90 = 0f;
        public float TimeoutRate = 0f;
        public float TimeToFirstDamageP50 = 0f;
        public float TimeToFirstMajorActionP50 = 0f;
        public float DeadBeforeFirstMajorActionRate = 0f;
        public float TopDamageShareP90 = 0f;
        public float ReadabilityFatalRate = 0f;
        public int MissingExplainStampCount = 0;
        public string[] TopViolations = Array.Empty<string>();
    }

    [Serializable]
    public sealed class LoopDSuiteReport
    {
        public string SuiteId = string.Empty;
        public int ScenarioCount = 0;
        public int SeedCount = 0;
        public bool Passed = true;
        public string[] Failures = Array.Empty<string>();
        public LoopDScenarioReport[] Scenarios = Array.Empty<LoopDScenarioReport>();
    }

    [Serializable]
    public sealed class LoopDRunLiteReport
    {
        public bool Passed = true;
        public string[] Failures = Array.Empty<string>();
        public RunLiteSummaryReport Summary = new();
    }

    public sealed record FirstPlayableBalanceRunResult(
        LoopDSuiteReport PureKit,
        LoopDSuiteReport SystemicSlice,
        LoopDRunLiteReport RunLite,
        IReadOnlyList<ContentHealthCard> ContentHealthCards,
        IReadOnlyList<PruneLedgerEntry> PruneLedger,
        IReadOnlyList<string> ReadabilityWatchlist,
        IReadOnlyList<string> Failures,
        string ReportDirectory);

    [MenuItem("SM/Validation/Run Loop D Balance")]
    public static void RunMenu()
    {
        var report = RunAndWriteReport();
        if (report.Failures.Count > 0)
        {
            throw new InvalidOperationException($"Loop D balance gate failed: {string.Join(", ", report.Failures)}");
        }

        Debug.Log($"[LoopD] Reports written to {report.ReportDirectory}");
    }

    [MenuItem("SM/Validation/Run Loop D Balance Smoke")]
    public static void RunSmokeMenu()
    {
        var report = RunAndWriteReport(smokeMode: true);
        if (report.Failures.Count > 0)
        {
            throw new InvalidOperationException($"Loop D smoke gate failed: {string.Join(", ", report.Failures)}");
        }

        Debug.Log($"[LoopD] Smoke reports written to {report.ReportDirectory}");
    }

    public static FirstPlayableSliceGenerationResult GenerateSliceArtifacts()
    {
        return FirstPlayableSliceGenerator.GenerateAndWriteArtifacts();
    }

    public static LoopDSuiteReport RunPureKitAndWriteArtifacts(bool smokeMode = false)
    {
        GenerateSliceArtifacts();
        var snapshot = ResolveSnapshot();
        var report = RunPureKitSuite(snapshot, smokeMode);
        WriteJsonReport(PureKitReportFileName, report);
        return report;
    }

    public static LoopDSuiteReport RunSystemicSliceAndWriteArtifacts(bool smokeMode = false)
    {
        GenerateSliceArtifacts();
        var report = RunSystemicSliceSuite(smokeMode);
        WriteJsonReport(SystemicSliceReportFileName, report);
        return report;
    }

    public static LoopDRunLiteReport RunRunLiteAndWriteArtifacts(bool smokeMode = false)
    {
        GenerateSliceArtifacts();
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        var report = RunRunLiteSuite(lookup, smokeMode);
        WriteJsonReport(RunLiteReportFileName, report);
        return report;
    }

    public static FirstPlayableBalanceRunResult RunAndWriteReport(bool smokeMode = false)
    {
        Debug.Log($"[LoopD] RunAndWriteReport start smoke={smokeMode}");
        var sliceResult = GenerateSliceArtifacts();
        Debug.Log("[LoopD] Slice generation complete");
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        var snapshot = ResolveSnapshot(lookup);

        Debug.Log("[LoopD] PureKit suite start");
        var pureKit = RunPureKitSuite(snapshot, smokeMode);
        Debug.Log("[LoopD] PureKit suite complete");
        Debug.Log("[LoopD] Systemic suite start");
        var systemic = RunSystemicSliceSuite(smokeMode);
        Debug.Log("[LoopD] Systemic suite complete");
        Debug.Log("[LoopD] RunLite suite start");
        var runLite = RunRunLiteSuite(lookup, smokeMode);
        Debug.Log("[LoopD] RunLite suite complete");
        Debug.Log("[LoopD] Content health/prune start");
        var healthCards = BuildContentHealthCards(snapshot, sliceResult.Slice, pureKit, systemic);
        var pruneLedger = BuildPruneLedger(snapshot, sliceResult.Slice, healthCards);
        Debug.Log("[LoopD] Content health/prune complete");
        var readabilityWatchlist = pureKit.Scenarios
            .Concat(systemic.Scenarios)
            .Where(report => report.TopViolations.Length > 0)
            .Select(report => $"{report.ScenarioId}:{string.Join(",", report.TopViolations)}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var failures = EvaluateFailures(snapshot, sliceResult.Slice, pureKit, systemic, runLite, healthCards, pruneLedger);
        var reportDirectory = EnsureReportDirectory();

        WriteJsonReport(PureKitReportFileName, pureKit);
        WriteJsonReport(SystemicSliceReportFileName, systemic);
        WriteJsonReport(RunLiteReportFileName, runLite);
        WriteTextReport(ContentHealthCsvFileName, BuildContentHealthCsv(healthCards));
        WriteJsonReport(PruneLedgerFileName, pruneLedger);
        WriteJsonReport(ReadabilityWatchlistFileName, readabilityWatchlist);
        WriteTextReport(ClosureNoteFileName, BuildClosureNote(sliceResult.Slice, pureKit, systemic, runLite, pruneLedger, readabilityWatchlist));

        return new FirstPlayableBalanceRunResult(
            pureKit,
            systemic,
            runLite,
            healthCards,
            pruneLedger,
            readabilityWatchlist,
            failures,
            reportDirectory);
    }

    public static string EnsureReportDirectory()
    {
        var reportDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFolderName));
        Directory.CreateDirectory(reportDirectory);
        return reportDirectory;
    }

    private static CombatContentSnapshot ResolveSnapshot(RuntimeCombatContentLookup? lookup = null)
    {
        lookup ??= new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return snapshot;
    }

    private static void WriteJsonReport(string fileName, object value)
    {
        var path = Path.Combine(EnsureReportDirectory(), fileName);
        File.WriteAllText(path, JsonConvert.SerializeObject(value, Formatting.Indented));
    }

    private static void WriteTextReport(string fileName, string content)
    {
        var path = Path.Combine(EnsureReportDirectory(), fileName);
        File.WriteAllText(path, content);
    }

    private static LoopDSuiteReport RunPureKitSuite(CombatContentSnapshot snapshot, bool smokeMode)
    {
        var seedsPerScenario = smokeMode ? 1 : 32;
        var inputs = BuildPureKitScenarioInputs(snapshot, seedsPerScenario, smokeMode);
        var digests = inputs.SelectMany(RunScenario).ToList();
        var reports = inputs
            .Select(input => BuildScenarioReport(input.ScenarioId, digests.Where(digest => digest.ScenarioId == input.ScenarioId).ToList()))
            .ToArray();
        var failures = new List<string>();
        if (reports.Any(report => report.BattleDurationP50 < 18f || report.BattleDurationP50 > 30f))
        {
            failures.Add("purekit.battle_duration_p50");
        }

        if (reports.Any(report => report.BattleDurationP90 > 38f))
        {
            failures.Add("purekit.battle_duration_p90");
        }

        if (reports.Any(report => report.TimeoutRate > 0.02f))
        {
            failures.Add("purekit.timeout_rate");
        }

        if (reports.Any(report => report.TimeToFirstDamageP50 > 1.75f))
        {
            failures.Add("purekit.first_damage");
        }

        if (reports.Any(report => report.TimeToFirstMajorActionP50 < 1.25f || report.TimeToFirstMajorActionP50 > 6f))
        {
            failures.Add("purekit.first_major_action");
        }

        if (reports.Any(report => report.DeadBeforeFirstMajorActionRate > 0.15f))
        {
            failures.Add("purekit.dead_before_major");
        }

        if (reports.Any(report => report.TopDamageShareP90 > 0.65f))
        {
            failures.Add("purekit.top_damage_share");
        }

        return new LoopDSuiteReport
        {
            SuiteId = BalanceSuiteId.PureKit.ToString(),
            ScenarioCount = reports.Length,
            SeedCount = seedsPerScenario,
            Passed = failures.Count == 0,
            Failures = failures.ToArray(),
            Scenarios = reports,
        };
    }

    private static LoopDSuiteReport RunSystemicSliceSuite(bool smokeMode)
    {
        var seedsPerScenario = smokeMode ? 1 : 40;
        var smokeScenarios = BalanceSweepScenarioFactory.BuildSmokeScenarios();
        var threatScenarios = BalanceSweepScenarioFactory.BuildThreatTopologyScenarios()
            .ToDictionary(input => input.ScenarioId, StringComparer.Ordinal);
        var mapped = new[]
        {
            MapSystemicScenario(BalanceScenarioId.Standard_BalancedMirror_3v3, smokeScenarios[0], seedsPerScenario),
            MapSystemicScenario(BalanceScenarioId.Dive_vs_BacklinePeel, threatScenarios["DiveBacklineScenario"], seedsPerScenario),
            MapSystemicScenario(BalanceScenarioId.SustainBall_vs_BurstSpike, threatScenarios["SustainBallScenario"], seedsPerScenario),
            MapSystemicScenario(BalanceScenarioId.SwarmFlood_vs_Cleave, threatScenarios["SwarmFloodScenario"], seedsPerScenario),
            MapSystemicScenario(BalanceScenarioId.ArmorFrontline_vs_ArmorShred, threatScenarios["ArmorFrontlineScenario"], seedsPerScenario),
            MapSystemicScenario(BalanceScenarioId.ResistanceShell_vs_Exposure, threatScenarios["ResistanceShellScenario"], seedsPerScenario),
            MapSystemicScenario(BalanceScenarioId.GuardBulwark_vs_MultiHitBreak, threatScenarios["GuardBulwarkScenario"], seedsPerScenario),
            MapSystemicScenario(BalanceScenarioId.MixedDraft_4v4, smokeScenarios[1], seedsPerScenario),
        };
        if (smokeMode)
        {
            mapped = mapped
                .Where(input => input.ScenarioId is BalanceScenarioId.Standard_BalancedMirror_3v3 or BalanceScenarioId.Dive_vs_BacklinePeel or BalanceScenarioId.ArmorFrontline_vs_ArmorShred)
                .ToArray();
        }
        var digests = mapped.SelectMany(RunScenario).ToList();
        var reports = mapped
            .Select(input => BuildScenarioReport(input.ScenarioId, digests.Where(digest => digest.ScenarioId == input.ScenarioId).ToList()))
            .ToArray();
        var failures = reports.Any(report => report.ReadabilityFatalRate > 0f)
            ? new[] { "systemic.readability_fatal" }
            : Array.Empty<string>();

        return new LoopDSuiteReport
        {
            SuiteId = BalanceSuiteId.SystemicSlice.ToString(),
            ScenarioCount = reports.Length,
            SeedCount = seedsPerScenario,
            Passed = failures.Length == 0,
            Failures = failures,
            Scenarios = reports,
        };
    }

    private static LoopDRunLiteReport RunRunLiteSuite(RuntimeCombatContentLookup lookup, bool smokeMode)
    {
        var seedCount = smokeMode ? 2 : 48;
        var reports = new List<RunLiteSummaryReport>();
        for (var seed = 0; seed < seedCount; seed++)
        {
            reports.Add(RunMiniRun(lookup, seed));
        }

        var aggregated = new RunLiteSummaryReport
        {
            Seed = seedCount,
            CombatsCompleted = (int)reports.Average(report => report.CombatsCompleted),
            RecruitsPurchased = (int)reports.Average(report => report.RecruitsPurchased),
            ScoutUses = (int)reports.Average(report => report.ScoutUses),
            RetrainUses = (int)reports.Average(report => report.RetrainUses),
            DuplicatesConverted = (int)reports.Average(report => report.DuplicatesConverted),
            NoAffordableOptionRate = reports.Average(report => report.NoAffordableOptionRate),
            MeaningfulChoicePhaseRate = reports.Average(report => report.MeaningfulChoicePhaseRate),
            EchoSpendRatio = reports.Average(report => report.EchoSpendRatio),
            OnPlanPurchaseShare = reports.Average(report => report.OnPlanPurchaseShare),
            ProtectedPurchaseShare = reports.Average(report => report.ProtectedPurchaseShare),
            RetrainUseRate = reports.Average(report => report.RetrainUseRate),
            ScoutUseRate = reports.Average(report => report.ScoutUseRate),
        };

        var failures = new List<string>();
        if (aggregated.NoAffordableOptionRate > 0f)
        {
            failures.Add("runlite.no_affordable_option");
        }

        if (aggregated.MeaningfulChoicePhaseRate < 0.90f)
        {
            failures.Add("runlite.meaningful_choice");
        }

        if (aggregated.EchoSpendRatio < 0.35f || aggregated.EchoSpendRatio > 0.75f)
        {
            failures.Add("runlite.echo_spend_ratio");
        }

        if (aggregated.OnPlanPurchaseShare < 0.20f || aggregated.OnPlanPurchaseShare > 0.55f)
        {
            failures.Add("runlite.on_plan_purchase_share");
        }

        if (aggregated.ProtectedPurchaseShare < 0.10f || aggregated.ProtectedPurchaseShare > 0.35f)
        {
            failures.Add("runlite.protected_purchase_share");
        }

        if (aggregated.RetrainUseRate < 0.15f || aggregated.RetrainUseRate > 0.45f)
        {
            failures.Add("runlite.retrain_use_rate");
        }

        if (aggregated.ScoutUseRate < 0.20f || aggregated.ScoutUseRate > 0.60f)
        {
            failures.Add("runlite.scout_use_rate");
        }

        return new LoopDRunLiteReport
        {
            Passed = failures.Count == 0,
            Failures = failures.ToArray(),
            Summary = aggregated,
        };
    }

    private static IReadOnlyList<LoopDScenarioInput> BuildPureKitScenarioInputs(CombatContentSnapshot snapshot, int seedsPerScenario, bool smokeMode)
    {
        var seeds = Enumerable.Range(0, seedsPerScenario).Select(index => 1000 + index).ToList();
        var scenarios = new[]
        {
            BuildMirrorScenario(snapshot, BalanceScenarioId.Duel_MeleeMirror_1v1, "slayer", Array.Empty<string>(), seeds),
            BuildMirrorScenario(snapshot, BalanceScenarioId.Standard_BalancedMirror_3v3, "warden", new[] { "marksman", "priest" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.MeleeCollapse_vs_RangerHold, new[] { "slayer", "raider", "reaver" }, new[] { "warden", "marksman", "priest" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.Dive_vs_BacklinePeel, new[] { "raider", "reaver", "scout" }, new[] { "warden", "bulwark", "priest" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.SustainBall_vs_BurstSpike, new[] { "priest", "shaman", "warden" }, new[] { "slayer", "hunter", "hexer" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.SwarmFlood_vs_Cleave, new[] { "shaman", "hunter", "scout" }, new[] { "warden", "marksman", "priest" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.ControlChain_vs_Tenacity, new[] { "hexer", "shaman", "warden" }, new[] { "warden", "raider", "priest" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.ArmorFrontline_vs_ArmorShred, new[] { "bulwark", "guardian", "priest" }, new[] { "slayer", "raider", "marksman" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.ResistanceShell_vs_Exposure, new[] { "priest", "hexer", "shaman" }, new[] { "hexer", "shaman", "scout" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.GuardBulwark_vs_MultiHitBreak, new[] { "guardian", "bulwark", "marksman" }, new[] { "raider", "scout", "priest" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.EvasiveSkirmish_vs_TrackingArea, new[] { "hunter", "scout", "marksman" }, new[] { "shaman", "priest", "warden" }, seeds),
            BuildScenario(snapshot, BalanceScenarioId.MixedDraft_4v4, new[] { "warden", "raider", "marksman", "priest" }, new[] { "bulwark", "reaver", "scout", "shaman" }, seeds),
        };

        return smokeMode
            ? scenarios.Where(input => input.ScenarioId is BalanceScenarioId.Duel_MeleeMirror_1v1 or BalanceScenarioId.Standard_BalancedMirror_3v3 or BalanceScenarioId.Dive_vs_BacklinePeel)
                .ToArray()
            : scenarios;
    }

    private static LoopDScenarioInput BuildMirrorScenario(
        CombatContentSnapshot snapshot,
        BalanceScenarioId scenarioId,
        string archetypeId,
        IReadOnlyList<string> extraArchetypes,
        IReadOnlyList<int> seeds)
    {
        var archetypes = new[] { archetypeId }.Concat(extraArchetypes).ToArray();
        return BuildScenario(snapshot, scenarioId, archetypes, archetypes, seeds);
    }

    private static LoopDScenarioInput BuildScenario(
        CombatContentSnapshot snapshot,
        BalanceScenarioId scenarioId,
        IReadOnlyList<string> allyArchetypes,
        IReadOnlyList<string> enemyArchetypes,
        IReadOnlyList<int> seeds)
    {
        var allySnapshot = CompileSimpleSnapshot(snapshot, $"loopd.{scenarioId}.ally", allyArchetypes, TeamPostureType.StandardAdvance);
        var enemySnapshot = CompileSimpleSnapshot(snapshot, $"loopd.{scenarioId}.enemy", enemyArchetypes, TeamPostureType.StandardAdvance);
        return new LoopDScenarioInput(scenarioId, scenarioId.ToString(), allySnapshot, enemySnapshot.Allies, seeds);
    }

    private static LoopDScenarioInput MapSystemicScenario(BalanceScenarioId scenarioId, BalanceSweepScenarioInput input, int seedsPerScenario)
    {
        return new LoopDScenarioInput(
            scenarioId,
            input.Description,
            input.PlayerSnapshot,
            input.EnemyLoadout,
            Enumerable.Range(0, seedsPerScenario).Select(index => 2000 + index).ToList());
    }

    private static IEnumerable<LoopDBattleDigest> RunScenario(LoopDScenarioInput input)
    {
        foreach (var seed in input.Seeds)
        {
            var state = BattleFactory.Create(
                input.PlayerSnapshot.Allies,
                input.EnemyLoadout,
                input.PlayerSnapshot.TeamTactic.Posture,
                input.EnemyLoadout.FirstOrDefault()?.TeamTactic?.Posture ?? TeamPostureType.StandardAdvance,
                BattleSimulator.DefaultFixedStepSeconds,
                seed);
            var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
            var replay = ReplayAssembler.Assemble(input.PlayerSnapshot, input.EnemyLoadout, result, seed, $"seed:{seed}", $"seed:{seed}");
            yield return new LoopDBattleDigest(input.ScenarioId, result, replay);
        }
    }

    private static BattleLoadoutSnapshot CompileSimpleSnapshot(
        CombatContentSnapshot content,
        string blueprintId,
        IReadOnlyList<string> archetypeIds,
        TeamPostureType posture)
    {
        var compiler = new LoadoutCompiler();
        var heroes = new List<HeroRecord>();
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var assignments = new Dictionary<DeploymentAnchorId, string>();
        var anchors = new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontBottom,
            DeploymentAnchorId.BackTop,
            DeploymentAnchorId.BackCenter,
            DeploymentAnchorId.BackBottom,
        };

        for (var index = 0; index < archetypeIds.Count; index++)
        {
            if (!content.Archetypes.TryGetValue(archetypeIds[index], out var archetype))
            {
                throw new InvalidOperationException($"Loop D scenario missing archetype '{archetypeIds[index]}'.");
            }

            var heroId = $"{blueprintId}.hero.{index}";
            heroes.Add(new HeroRecord(heroId, archetype.DisplayName, archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty));
            heroLoadouts[heroId] = new HeroLoadoutState(heroId, Array.Empty<string>(), Array.Empty<string>(), string.Empty, Array.Empty<string>(), Array.Empty<string>());
            heroProgressions[heroId] = new HeroProgressionState(heroId, 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).Distinct(StringComparer.Ordinal).ToList());
            assignments[anchors[index]] = heroId;
        }

        return compiler.Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState(blueprintId, Array.Empty<string>()),
            new SquadBlueprintState(
                blueprintId,
                blueprintId,
                posture,
                "team_tactic_standard_advance",
                assignments,
                heroes.Select(hero => hero.Id).ToList(),
                new Dictionary<string, string>(StringComparer.Ordinal)),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }

    private static LoopDScenarioReport BuildScenarioReport(BalanceScenarioId scenarioId, IReadOnlyList<LoopDBattleDigest> digests)
    {
        var durations = digests.Select(digest => digest.Result.DurationSeconds).ToList();
        var timeToFirstDamage = digests.Select(digest => digest.Replay.BattleSummary?.TimeToFirstDamageSeconds ?? digest.Result.DurationSeconds).ToList();
        var timeToFirstMajor = digests.Select(digest => digest.Replay.BattleSummary?.TimeToFirstMajorActionSeconds ?? digest.Result.DurationSeconds).ToList();
        var deadBeforeMajor = digests.Select(digest => digest.Replay.BattleSummary?.DeadBeforeFirstMajorActionRate ?? 0f).ToList();
        var topDamageShares = digests.Select(digest => digest.Replay.BattleSummary?.TopDamageShare ?? 0f).ToList();
        var readabilityFatals = digests.Select(digest => IsReadabilityFatal(digest.Replay.Readability) ? 1f : 0f).ToList();
        var missingExplainStampCount = digests
            .SelectMany(digest => TelemetryExplainValidator.CollectMissingExplainStampIssues(digest.Replay.TelemetryEvents ?? Array.Empty<TelemetryEventRecord>()))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var topViolations = digests
            .SelectMany(digest => digest.Replay.Readability?.Violations ?? Array.Empty<ReadabilityViolationKind>())
            .GroupBy(violation => violation)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(3)
            .Select(group => group.Key.ToString())
            .ToArray();

        return new LoopDScenarioReport
        {
            ScenarioId = scenarioId.ToString(),
            RunCount = digests.Count,
            WinRate = digests.Count == 0 ? 0f : digests.Count(digest => digest.Result.Winner == TeamSide.Ally) / (float)digests.Count,
            BattleDurationP50 = Percentile(durations, 0.5f),
            BattleDurationP90 = Percentile(durations, 0.9f),
            TimeoutRate = digests.Count == 0 ? 0f : digests.Count(digest => digest.Result.StepCount >= BattleSimulator.DefaultMaxSteps) / (float)digests.Count,
            TimeToFirstDamageP50 = Percentile(timeToFirstDamage, 0.5f),
            TimeToFirstMajorActionP50 = Percentile(timeToFirstMajor, 0.5f),
            DeadBeforeFirstMajorActionRate = deadBeforeMajor.Count == 0 ? 0f : deadBeforeMajor.Average(),
            TopDamageShareP90 = Percentile(topDamageShares, 0.9f),
            ReadabilityFatalRate = readabilityFatals.Count == 0 ? 0f : readabilityFatals.Average(),
            MissingExplainStampCount = missingExplainStampCount,
            TopViolations = topViolations,
        };
    }

    private static RunLiteSummaryReport RunMiniRun(RuntimeCombatContentLookup lookup, int seed)
    {
        Debug.Log($"[LoopD] RunLite seed {seed} start");
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());
        session.SetCurrentScene(SceneNames.Town);
        session.ClearRuntimeTelemetry();

        while (session.Profile.Heroes.Count > 3)
        {
            session.DismissHero(session.Profile.Heroes.Last().HeroId);
        }

        var startEcho = Math.Max(1, session.Profile.Currencies.Echo);
        var meaningfulPhases = 0;
        var noAffordableOptionPhases = 0;
        var recruitsPurchased = 0;
        var onPlanPurchases = 0;
        var protectedPurchases = 0;
        var scoutUses = 0;
        var retrainUses = 0;

        for (var phase = 0; phase < 3; phase++)
        {
            Debug.Log($"[LoopD] RunLite seed {seed} phase {phase} recruit");
            session.SetCurrentScene(SceneNames.Town);
            var offers = session.RecruitOffers;
            var affordable = offers.Where(offer => offer.Metadata.GoldCost <= session.Profile.Currencies.Gold).ToList();
            if (offers.Count >= 2 || affordable.Count >= 1 || session.CanUseScout)
            {
                meaningfulPhases++;
            }

            if (offers.Count > 0
                && session.Profile.Currencies.Gold >= offers.Min(offer => offer.Metadata.GoldCost)
                && affordable.Count == 0)
            {
                noAffordableOptionPhases++;
            }

            if (phase != 1 && session.CanUseScout && session.Profile.Currencies.Echo >= RecruitmentBalanceCatalog.ScoutEchoCost)
            {
                var scoutResult = session.UseScout(new ScoutDirective
                {
                    Kind = ScoutDirectiveKind.Support,
                });
                if (scoutResult.IsSuccess)
                {
                    scoutUses++;
                }
            }

            var chosenOffer = affordable
                .OrderByDescending(offer => offer.Metadata.PlanFit == CandidatePlanFit.OnPlan)
                .ThenByDescending(offer => offer.Metadata.ProtectedByPity)
                .ThenByDescending(offer => offer.Metadata.Tier)
                .FirstOrDefault();
            if (chosenOffer != null)
            {
                var index = offers
                    .Select((offer, offerIndex) => new { offer, offerIndex })
                    .First(pair => ReferenceEquals(pair.offer, chosenOffer))
                    .offerIndex;
                var recruitResult = session.Recruit(index);
                if (recruitResult.IsSuccess)
                {
                    recruitsPurchased++;
                    if (chosenOffer.Metadata.PlanFit == CandidatePlanFit.OnPlan)
                    {
                        onPlanPurchases++;
                    }

                    if (chosenOffer.Metadata.ProtectedByPity || chosenOffer.Metadata.SlotType == RecruitOfferSlotType.Protected)
                    {
                        protectedPurchases++;
                    }
                }
            }

            var retrainTarget = session.Profile.Heroes.FirstOrDefault();
            if (retrainTarget != null)
            {
                var retrainCost = RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(RetrainOperationKind.RerollFlexActive, retrainTarget.RetrainState);
                if (session.Profile.Currencies.Echo >= retrainCost)
                {
                    var retrain = session.RetrainHero(retrainTarget.HeroId, RetrainOperationKind.RerollFlexActive);
                    if (retrain.IsSuccess)
                    {
                        retrainUses++;
                    }
                }
            }

            session.PrepareQuickBattleSmoke();
            var snapshot = session.BuildBattleLoadoutSnapshot();
            if (session.TryResolveCurrentEncounter(out var encounter, out _))
            {
                Debug.Log($"[LoopD] RunLite seed {seed} phase {phase} battle");
                var state = BattleFactory.Create(snapshot.Allies, encounter.Enemies, snapshot.TeamTactic.Posture, encounter.EnemyPosture, BattleSimulator.DefaultFixedStepSeconds, seed + phase);
                var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
                var replay = ReplayAssembler.Assemble(snapshot, encounter.Enemies, result, seed + phase, $"runlite:{seed}:{phase}", $"runlite:{seed}:{phase}");
                session.RecordBattleAudit(replay);
                session.MarkBattleResolved(result.Winner == TeamSide.Ally, result.StepCount, result.Events.Count);
                if (session.PendingRewardChoices.Count > 0)
                {
                    session.ApplyRewardChoice(0);
                }
            }
        }

        Debug.Log($"[LoopD] RunLite seed {seed} complete");

        var duplicateConversions = session.RuntimeTelemetryEvents.Count(record => record.EventKind == TelemetryEventKind.DuplicateConverted);
        return new RunLiteSummaryReport
        {
            Seed = seed,
            CombatsCompleted = 3,
            RecruitsPurchased = recruitsPurchased,
            ScoutUses = scoutUses,
            RetrainUses = retrainUses,
            DuplicatesConverted = duplicateConversions,
            NoAffordableOptionRate = noAffordableOptionPhases / 3f,
            MeaningfulChoicePhaseRate = meaningfulPhases / 3f,
            EchoSpendRatio = (startEcho - session.Profile.Currencies.Echo) / (float)startEcho,
            OnPlanPurchaseShare = recruitsPurchased == 0 ? 0f : onPlanPurchases / (float)recruitsPurchased,
            ProtectedPurchaseShare = recruitsPurchased == 0 ? 0f : protectedPurchases / (float)recruitsPurchased,
            RetrainUseRate = retrainUses / 3f,
            ScoutUseRate = scoutUses / 3f,
        };
    }

    private static IReadOnlyList<ContentHealthCard> BuildContentHealthCards(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        LoopDSuiteReport pureKit,
        LoopDSuiteReport systemic)
    {
        var cards = new List<ContentHealthCard>();
        var fingerprintsById = slice.UnitBlueprintIds
            .Where(id => snapshot.Archetypes.TryGetValue(id, out _))
            .ToDictionary(
                id => id,
                id => LoopDContentHealthAnalysisService.BuildFingerprint(snapshot.Archetypes[id]),
                StringComparer.Ordinal);
        foreach (var unitId in slice.UnitBlueprintIds)
        {
            if (!snapshot.Archetypes.TryGetValue(unitId, out var template))
            {
                continue;
            }

            var readabilityDebt = pureKit.Scenarios.Any(report => report.TopViolations.Length > 0) ? 8 : 4;
            var providesUniqueCoverage = LoopDContentHealthAnalysisService.ProvidesUniqueCoverage(template.Governance);
            var fingerprint = fingerprintsById[unitId];
            var card = new ContentHealthCard
            {
                ContentId = unitId,
                ContentKind = ContentKind.UnitBlueprint,
                ExposureCount = pureKit.SeedCount * pureKit.ScenarioCount,
                PickCount = pureKit.ScenarioCount,
                PickRate = 1f,
                PresenceWinRate = pureKit.Scenarios.Length == 0 ? 0f : pureKit.Scenarios.Average(report => report.WinRate),
                PresenceWinDelta = 0f,
                HighestIdentitySimilarity = LoopDContentHealthAnalysisService.CalculateHighestIdentitySimilarity(
                    fingerprint,
                    fingerprintsById
                        .Where(pair => !string.Equals(pair.Key, unitId, StringComparison.Ordinal))
                        .Select(pair => pair.Value)),
                ProvidesUniqueCoverage = providesUniqueCoverage,
                Debt = new ContentDebtVector
                {
                    PowerDebt = 4,
                    ReadabilityDebt = readabilityDebt,
                    RedundancyDebt = providesUniqueCoverage ? 2 : 8,
                    VarianceDebt = 2,
                    TopologyDebt = providesUniqueCoverage ? 1 : 4,
                    EconomyDebt = 0,
                },
                Fingerprint = fingerprint,
                Reasons = readabilityDebt >= 8
                    ? new[] { PruneReason.ReadabilityDebt }
                    : Array.Empty<PruneReason>(),
            };
            card.Grade = LoopDContentHealthAnalysisService.ResolveGrade(card);
            card.SuggestedDisposition = LoopDContentHealthAnalysisService.ResolveDisposition(card);
            cards.Add(card);
        }

        foreach (var contentId in slice.ParkingLotContentIds)
        {
            cards.Add(new ContentHealthCard
            {
                ContentId = contentId,
                ContentKind = LoopDContentHealthAnalysisService.ResolveContentKind(snapshot, slice, contentId),
                Grade = ContentHealthGrade.InsufficientData,
                SuggestedDisposition = PruneDisposition.MoveOutOfV1,
                Reasons = new[] { PruneReason.UnsupportedBySliceCoverage },
                HighestIdentitySimilarity = 0.92f,
                Debt = new ContentDebtVector
                {
                    ReadabilityDebt = 3,
                    RedundancyDebt = 10,
                    TopologyDebt = 4,
                },
                Fingerprint = new ContentIdentityFingerprint(),
            });
        }

        return cards
            .OrderBy(card => card.ContentKind)
            .ThenBy(card => card.ContentId, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<PruneLedgerEntry> BuildPruneLedger(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        IReadOnlyList<ContentHealthCard> healthCards)
    {
        var entries = healthCards
            .Where(card => card.SuggestedDisposition != PruneDisposition.Keep)
            .Select(card => new PruneLedgerEntry
            {
                ContentId = card.ContentId,
                ContentKind = card.ContentKind,
                Grade = card.Grade,
                Disposition = card.SuggestedDisposition,
                Reasons = card.Reasons.Length > 0 ? card.Reasons : new[] { PruneReason.UnsupportedBySliceCoverage },
                ProvidesUniqueCoverage = card.ProvidesUniqueCoverage,
                Note = slice.ParkingLotContentIds.Contains(card.ContentId, StringComparer.Ordinal)
                    ? "Slice cap overflow parked out of V1."
                    : "Readability debt must be simplified before retune.",
            })
            .OrderBy(entry => entry.ContentKind)
            .ThenBy(entry => entry.ContentId, StringComparer.Ordinal)
            .ToList();

        if (entries.Count == 0 && slice.ParkingLotContentIds.Count > 0)
        {
            var fallbackId = slice.ParkingLotContentIds[0];
            entries.Add(new PruneLedgerEntry
            {
                ContentId = fallbackId,
                ContentKind = LoopDContentHealthAnalysisService.ResolveContentKind(snapshot, slice, fallbackId),
                Grade = ContentHealthGrade.InsufficientData,
                Disposition = PruneDisposition.MoveOutOfV1,
                Reasons = new[] { PruneReason.UnsupportedBySliceCoverage },
                Note = "Loop D fallback parking entry.",
            });
        }

        return entries;
    }

    private static IReadOnlyList<string> EvaluateFailures(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        LoopDSuiteReport pureKit,
        LoopDSuiteReport systemic,
        LoopDRunLiteReport runLite,
        IReadOnlyList<ContentHealthCard> healthCards,
        IReadOnlyList<PruneLedgerEntry> pruneLedger)
    {
        var failures = new List<string>();
        if (slice.UnitBlueprintIds.Count > slice.UnitBlueprintCap
            || slice.SignatureActiveIds.Count > slice.SignatureActiveCap
            || slice.SignaturePassiveIds.Count > slice.SignaturePassiveCap
            || slice.FlexActiveIds.Count > slice.FlexActiveCap
            || slice.FlexPassiveIds.Count > slice.FlexPassiveCap
            || slice.AffixIds.Count > slice.AffixCap
            || slice.SynergyFamilyIds.Count > slice.SynergyFamilyCap
            || slice.AugmentIds.Count > slice.AugmentCap)
        {
            failures.Add("slice.cap_exceeded");
        }

        foreach (var quota in slice.CoverageQuotas)
        {
            if (ResolveQuotaCount(snapshot, slice, quota.Kind) < quota.MinimumCount)
            {
                failures.Add($"slice.quota.{quota.Kind}");
            }
        }

        if (slice.RequireAllThreatPatternsCovered && !AllThreatPatternsCovered(snapshot, slice))
        {
            failures.Add("slice.threat_coverage");
        }

        if (slice.RequireAllCounterToolsCovered && !AllCounterToolsCovered(snapshot, slice))
        {
            failures.Add("slice.counter_coverage");
        }

        if (!pureKit.Passed)
        {
            failures.AddRange(pureKit.Failures.Select(failure => $"purekit:{failure}"));
        }

        if (!systemic.Passed)
        {
            failures.AddRange(systemic.Failures.Select(failure => $"systemic:{failure}"));
        }

        if (!runLite.Passed)
        {
            failures.AddRange(runLite.Failures.Select(failure => $"runlite:{failure}"));
        }

        if (pureKit.Scenarios.Any(report => report.MissingExplainStampCount > 0)
            || systemic.Scenarios.Any(report => report.MissingExplainStampCount > 0))
        {
            failures.Add("telemetry.explain_stamp_missing");
        }

        if (healthCards.Any(card => slice.Contains(card.ContentKind, card.ContentId) && card.Grade == ContentHealthGrade.Broken))
        {
            failures.Add("slice.contains_broken_content");
        }

        if (pruneLedger.Count == 0)
        {
            failures.Add("prune.empty");
        }

        return failures.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    private static int ResolveQuotaCount(CombatContentSnapshot snapshot, FirstPlayableSliceDefinition slice, SliceCoverageQuotaKind kind)
    {
        return slice.UnitBlueprintIds
            .Where(id => snapshot.Archetypes.TryGetValue(id, out _))
            .Select(id => snapshot.Archetypes[id])
            .Count(template => MatchesQuota(template, kind));
    }

    private static bool MatchesQuota(CombatArchetypeTemplate template, SliceCoverageQuotaKind kind)
    {
        var governance = template.Governance;
        var toolSet = new HashSet<string>((governance?.DeclaredCounterTools ?? Array.Empty<CompiledCounterToolContribution>()).Select(tool => tool.Tool), StringComparer.Ordinal);
        var threatSet = new HashSet<string>(governance?.DeclaredThreatPatterns ?? Array.Empty<string>(), StringComparer.Ordinal);
        return kind switch
        {
            SliceCoverageQuotaKind.FrontlineAnchor => template.Behavior?.FormationLine == FormationLine.Frontline,
            SliceCoverageQuotaKind.MeleePressure => string.Equals(template.ClassId, "duelist", StringComparison.Ordinal),
            SliceCoverageQuotaKind.BacklineCarry => template.Behavior?.FormationLine == FormationLine.Backline && string.Equals(template.ClassId, "ranger", StringComparison.Ordinal),
            SliceCoverageQuotaKind.MagicSource => string.Equals(template.ClassId, "mystic", StringComparison.Ordinal),
            SliceCoverageQuotaKind.SupportSource => string.Equals(template.RoleTag, "support", StringComparison.Ordinal) || string.Equals(template.ClassId, "mystic", StringComparison.Ordinal),
            SliceCoverageQuotaKind.DiveSource => threatSet.Contains(ThreatPattern.DiveBackline.ToString()) || toolSet.Contains(CounterTool.InterceptPeel.ToString()),
            SliceCoverageQuotaKind.SummonSource => template.Skills.Any(skill => skill.SummonProfile != null),
            SliceCoverageQuotaKind.AntiSwarmSource => toolSet.Contains(CounterTool.CleaveWaveclear.ToString()),
            SliceCoverageQuotaKind.AntiSustainSource => toolSet.Contains(CounterTool.AntiHealShatter.ToString()),
            SliceCoverageQuotaKind.AntiControlSource => toolSet.Contains(CounterTool.TenacityStability.ToString()),
            _ => false,
        };
    }

    private static bool AllThreatPatternsCovered(CombatContentSnapshot snapshot, FirstPlayableSliceDefinition slice)
    {
        var threats = slice.UnitBlueprintIds
            .Where(id => snapshot.Archetypes.TryGetValue(id, out _))
            .SelectMany(id => snapshot.Archetypes[id].Governance?.DeclaredThreatPatterns ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        return Enum.GetNames(typeof(ThreatPattern)).All(threats.Contains);
    }

    private static bool AllCounterToolsCovered(CombatContentSnapshot snapshot, FirstPlayableSliceDefinition slice)
    {
        var tools = slice.UnitBlueprintIds
            .Where(id => snapshot.Archetypes.TryGetValue(id, out _))
            .SelectMany(id => snapshot.Archetypes[id].Governance?.DeclaredCounterTools ?? Array.Empty<CompiledCounterToolContribution>())
            .Select(tool => tool.Tool)
            .ToHashSet(StringComparer.Ordinal);
        return Enum.GetNames(typeof(CounterTool)).All(tools.Contains);
    }

    private static bool IsReadabilityFatal(ReadabilityReport? readability)
    {
        return readability != null
               && (readability.UnexplainedDamageRatio > 0.10f
                   || readability.UnexplainedHealingRatio > 0.10f
                   || readability.MajorEventCollisionRate > 0.30f
                   || readability.SalienceWeightPer1sP95 > 12f);
    }

    private static string BuildContentHealthCsv(IReadOnlyList<ContentHealthCard> cards)
    {
        var builder = new StringBuilder();
        builder.AppendLine("content_id,content_kind,grade,disposition,exposure_count,pick_count,pick_rate,presence_win_rate,presence_win_delta,total_debt,unique_coverage");
        foreach (var card in cards)
        {
            builder.Append(card.ContentId).Append(',')
                .Append(card.ContentKind).Append(',')
                .Append(card.Grade).Append(',')
                .Append(card.SuggestedDisposition).Append(',')
                .Append(card.ExposureCount).Append(',')
                .Append(card.PickCount).Append(',')
                .Append(card.PickRate.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(card.PresenceWinRate.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(card.PresenceWinDelta.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(card.Debt.Total).Append(',')
                .Append(card.ProvidesUniqueCoverage ? "true" : "false")
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildClosureNote(FirstPlayableSliceDefinition slice, LoopDSuiteReport pureKit, LoopDSuiteReport systemic, LoopDRunLiteReport runLite, IReadOnlyList<PruneLedgerEntry> pruneLedger, IReadOnlyList<string> readabilityWatchlist)
    {
        var keepCount = pruneLedger.Count(entry => entry.Disposition == PruneDisposition.Keep);
        var retuneCount = pruneLedger.Count(entry => entry.Disposition == PruneDisposition.RetuneNumbers || entry.Disposition == PruneDisposition.RetuneCadence);
        var simplifyCount = pruneLedger.Count(entry => entry.Disposition == PruneDisposition.SimplifyReadability);
        var moveCount = pruneLedger.Count(entry => entry.Disposition == PruneDisposition.MoveOutOfV1);
        var removeCount = pruneLedger.Count(entry => entry.Disposition == PruneDisposition.Remove);
        var watchlist = readabilityWatchlist.Take(3).ToArray();
        return
$@"Loop D closure summary
- first playable slice selected: units={slice.UnitBlueprintIds.Count}, signatureActives={slice.SignatureActiveIds.Count}, signaturePassives={slice.SignaturePassiveIds.Count}, flexActives={slice.FlexActiveIds.Count}, flexPassives={slice.FlexPassiveIds.Count}, affixes={slice.AffixIds.Count}, synergies={slice.SynergyFamilyIds.Count}, augments={slice.AugmentIds.Count}
- telemetry coverage: combat/recruit/retrain/duplicate/readability
- readability gate result: pureKit={(pureKit.Passed ? "pass" : "fail")}, systemic={(systemic.Passed ? "pass" : "fail")}, runLite={(runLite.Passed ? "pass" : "fail")}
- prune ledger summary: keep={keepCount}, retune={retuneCount}, simplify={simplifyCount}, move-out-of-v1={moveCount}, remove={removeCount}
- biggest watchlist items: {(watchlist.Length == 0 ? "none" : string.Join("; ", watchlist))}
- remaining known gaps for post-Loop-D: synergy family authored count is below cap 8 when catalog stays at 7; RunLite uses quick-battle smoke instead of authored site ladder; offscreen major event uses current runtime viewport heuristic";
    }

    private static float Percentile(IReadOnlyList<float> values, float percentile)
    {
        if (values.Count == 0)
        {
            return 0f;
        }

        var ordered = values.OrderBy(value => value).ToList();
        var index = Mathf.Clamp(Mathf.CeilToInt((ordered.Count - 1) * percentile), 0, ordered.Count - 1);
        return ordered[index];
    }
}
