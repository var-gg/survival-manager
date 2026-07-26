using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>
/// Runs the frozen 32-seed Seal census and policy-only calibration sweep.
/// The measurement adapters do not mutate authored content or shipped policy constants.
/// </summary>
public static class H100SealPolicyMeasurementRunner
{
    private const string GateSpecRelativePath =
        "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-v1.json";
    private const string DefaultCoverageAnchorId = "anchor_iron_line";
    private const int PreregisteredSeedCount = 32;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static void RunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(
            nameof(H100SealPolicyMeasurementRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputRoot = ResolveOutputPath(
            projectRoot,
            Environment.GetEnvironmentVariable("SM_H100_SEAL_MEASUREMENT_OUTPUT")
            ?? "Logs/20260726-seal-prereg-sample/measurement");
        var reportPath = Path.Combine(outputRoot, "seal-policy-measurement.json");
        var sampleStartedPath = Path.Combine(outputRoot, "sample-started.txt");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                $"Preregistered measurement report already exists; retry is forbidden: {reportPath}");
        }

        if (File.Exists(sampleStartedPath))
        {
            throw new InvalidOperationException(
                $"Preregistered measurement already started; retry is forbidden: {sampleStartedPath}");
        }

        var baselineTracePath = ResolveExistingPath(
            projectRoot,
            Environment.GetEnvironmentVariable("SM_H100_SEAL_BASELINE_TRACE")
            ?? "Logs/20260726-seal-headless-policy/baseline/coverage/intent_trace.jsonl");
        var preregistrationPath = ResolveExistingPath(
            projectRoot,
            Environment.GetEnvironmentVariable("SM_H100_SEAL_PREREGISTRATION")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".orchestrator",
                "jobs",
                "20260726-seal-prereg-sample",
                "preregistration.md"));
        var seedBase = ReadInt("SM_H100_SEAL_SEED_BASE", 1701);
        var seedCount = ReadPositiveInt(
            "SM_H100_SEAL_SEED_COUNT",
            PreregisteredSeedCount);
        var measurementMode = ReadMeasurementMode();
        if (seedCount != PreregisteredSeedCount)
        {
            throw new InvalidOperationException(
                $"Frozen preregistration requires exactly {PreregisteredSeedCount} seeds.");
        }

        var siteSafety = ReadPositiveInt("SM_H100_SEAL_SITE_SAFETY", 32);
        var maxBattleSteps = ReadPositiveInt("SM_H100_SEAL_MAX_BATTLE_STEPS", 300);
        var anchorId = Environment.GetEnvironmentVariable("SM_H100_SEAL_COVERAGE_ANCHOR")
                       ?? DefaultCoverageAnchorId;
        var preregistrationBytes = File.ReadAllBytes(preregistrationPath);
        var preregistrationSha256 = Sha256(preregistrationBytes);

        Directory.CreateDirectory(outputRoot);
        var spec = H100GateSpec.LoadFromFile(Path.Combine(projectRoot, GateSpecRelativePath));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var settings = new H100MetricsRunSettings(
            BattleCount: 1,
            CampaignCount: seedCount,
            ReplayCopies: 2,
            SeedBase: seedBase,
            CampaignSiteSafety: siteSafety,
            MaxBattleSteps: maxBattleSteps,
            WriteCsv: false,
            OutputDirectory: outputRoot,
            PolicyId: ConceptCommitPolicy.PolicyId);
        var catalog = H100ConceptCatalogRunner.DeriveForSnapshot(snapshot);
        var coverageIntent = H100ConceptIntentProjector.ProjectSingle(catalog, anchorId);

        var golden = RunGolden(
            outputRoot,
            baselineTracePath,
            lookup,
            settings with { CampaignCount = 1 },
            spec.TargetBattleSeconds,
            coverageIntent);
        var targetAffixId = SelectMissingAffix(snapshot, lookup, settings);
        var measurementIntent = AddMissingAffixIntent(coverageIntent, targetAffixId);

        File.WriteAllText(
            sampleStartedPath,
            $"preregistration_sha256={preregistrationSha256}\n"
            + $"seed_base={seedBase.ToString(CultureInfo.InvariantCulture)}\n"
            + $"seed_count={seedCount.ToString(CultureInfo.InvariantCulture)}\n",
            Utf8WithoutBom);
        Debug.Log(
            $"[H100SealPolicyMeasurement] sample_start seeds={seedCount};"
            + $"seed_base={seedBase};mode={measurementMode};"
            + $"grid={CalibrationGridForMode(measurementMode).Count};"
            + $"preregistration_sha256={preregistrationSha256}");
        var noSeal = RunArm(
            "no-seal",
            lookup,
            snapshot,
            settings,
            spec.TargetBattleSeconds,
            measurementIntent,
            calibration: null,
            forceNoSeal: true);
        var census = H100SealPolicyMeasurementAnalysis.BuildCensus(
            noSeal,
            seedBase,
            seedCount);
        var calibrations = CalibrationGridForMode(measurementMode);
        var sweep = new List<H100SealPolicySweepResult>(calibrations.Count);
        foreach (var calibration in calibrations)
        {
            var arm = RunArm(
                calibration.Id,
                lookup,
                snapshot,
                settings,
                spec.TargetBattleSeconds,
                measurementIntent,
                calibration,
                forceNoSeal: false);
            var result = H100SealPolicyMeasurementAnalysis.BuildSweepResult(arm, noSeal);
            sweep.Add(result);
            Debug.Log(
                $"[H100SealPolicyMeasurement] setting={calibration.Id};"
                + $"seals={result.SealCount};campaigns={result.CampaignsWithSeal};"
                + $"quality_delta={Format(result.RollQualityDelta)};"
                + $"echo_delta={Format(result.CurrencyDelta)};"
                + $"outcome_delta={result.OutcomeDelta.ToString("R", CultureInfo.InvariantCulture)}");
        }

        var h2 = H100SealPolicyMeasurementAnalysis.BuildH2Verdict(census, sweep);
        var widthProbe = H100SealPolicyMeasurementAnalysis.BuildWidthProbe(
            noSeal,
            sweep,
            h2);
        var conclusion = H100SealPolicyMeasurementAnalysis.BuildConclusion(
            census,
            h2,
            widthProbe);
        var surprises = H100SealPolicyMeasurementAnalysis.BuildSurprises(
            census,
            h2,
            widthProbe);
        var report = new H100SealPolicyMeasurementReport(
            "h100-seal-policy-measurement-v2",
            preregistrationSha256,
            seedBase,
            seedCount,
            measurementMode,
            Enumerable.Range(seedBase, seedCount).ToArray(),
            anchorId,
            measurementIntent.IntentId,
            targetAffixId,
            golden,
            census,
            noSeal,
            sweep,
            h2,
            widthProbe,
            conclusion,
            surprises);
        File.WriteAllText(
            reportPath,
            HeadlessMetricJson.Serialize(report) + "\n",
            Utf8WithoutBom);
        Debug.Log(
            $"[H100SealPolicyMeasurement] status=complete;"
            + $"supported={conclusion.SupportedHypothesis};"
            + $"windows={census.TotalWindows};"
            + $"affix_observations={census.CandidateAffixObservationCount};"
            + $"h2_ruled_out={h2.RuledOut};"
            + $"width_probe={widthProbe.Ran};"
            + $"golden={golden.ByteIdentical};output={reportPath}");
    }

    private static H100SealGoldenComparison RunGolden(
        string outputRoot,
        string baselineTracePath,
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        HeadlessConceptIntent coverageIntent)
    {
        var goldenCorpus = H100CampaignCorpusRunner.Run(
            lookup,
            settings,
            targetBattleSeconds,
            observationHooks: new H100CampaignObservationHooks(
                RosterObservationTransform: DisableSeal),
            policyFactory: _ => new ConceptCommitPolicy(coverageIntent));
        var candidateTracePath = IntentTraceArtifactWriter.Write(
            Path.Combine(outputRoot, "no-seal-golden"),
            goldenCorpus.IntentTraces);
        var golden = CompareGolden(baselineTracePath, candidateTracePath);
        if (!golden.ByteIdentical)
        {
            throw new InvalidOperationException(
                $"No-Seal intent trace moved: baseline={golden.BaselineSha256}, "
                + $"candidate={golden.CandidateSha256}.");
        }

        return golden;
    }

    private static H100SealPolicyArmReport RunArm(
        string armId,
        RuntimeCombatContentLookup lookup,
        CombatContentSnapshot snapshot,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        HeadlessConceptIntent intent,
        H100SealPolicyCalibration? calibration,
        bool forceNoSeal)
    {
        var collector = new H100SealPolicyMeasurementCollector(
            snapshot,
            settings.SeedBase);
        var corpus = H100CampaignCorpusRunner.Run(
            lookup,
            settings,
            targetBattleSeconds,
            observationHooks: collector.CreateHooks(
                forceNoSeal ? DisableSeal : null),
            policyFactory: _ => new H100SealCalibrationPolicy(
                intent,
                calibration,
                forceNoSeal));
        return collector.BuildReport(armId, calibration, corpus);
    }

    private static HeadlessRosterPolicyObservation DisableSeal(
        HeadlessRosterPolicyObservation observation)
    {
        var refitItems = observation.RefitItems
            .Select(item => new HeadlessRefitItemObservation(
                item.ItemId,
                item.ItemInstanceId,
                item.EquippedHeroId,
                item.Tags,
                item.WeaponFamilyTag,
                item.EchoCost,
                item.AffixSlots,
                allowsSeal: false,
                sealCosts: Array.Empty<HeadlessSealCostObservation>()))
            .ToArray();
        return new HeadlessRosterPolicyObservation(
            observation.DecisionSeed,
            observation.ChapterId,
            observation.SiteId,
            observation.RosterCapacity,
            observation.Roster,
            observation.Wallet,
            observation.RecruitOffers,
            observation.PassiveHeroes,
            refitItems,
            observation.EvidenceFactIdsBySignal);
    }

    private static string SelectMissingAffix(
        CombatContentSnapshot snapshot,
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings)
    {
        const string campaignId = "campaign-000000";
        var session = H100SessionDriver.CreateSession(
            lookup,
            settings.PairingProfileId(campaignId));
        var owned = session.Profile.Inventory
            .SelectMany(item => item.AffixIds ?? new List<string>())
            .ToHashSet(StringComparer.Ordinal);
        return (snapshot.AffixCatalog?.Keys ?? Array.Empty<string>())
                   .Where(value => !owned.Contains(value))
                   .OrderBy(value => value, StringComparer.Ordinal)
                   .FirstOrDefault()
               ?? throw new InvalidOperationException(
                   "The measurement campaign has no deterministic missing affix target.");
    }

    private static HeadlessConceptIntent AddMissingAffixIntent(
        HeadlessConceptIntent source,
        string targetAffixId)
        => new(
            $"{source.IntentId}-seal-measurement-{targetAffixId}",
            source.SourceLane,
            source.IdentityPredicates
                .Concat(new[] { $"owned:affix:{targetAffixId}" })
                .ToArray(),
            source.ProgressMilestones,
            source.PayoffWitnessId,
            source.AllowedSubstitutions,
            source.FlexSlots,
            source.CounterAffordances,
            source.AvailabilityTier,
            source.PivotConditions);

    private static H100SealGoldenComparison CompareGolden(
        string baselinePath,
        string candidatePath)
    {
        var baseline = File.ReadAllBytes(baselinePath);
        var candidate = File.ReadAllBytes(candidatePath);
        return new H100SealGoldenComparison(
            baselinePath,
            candidatePath,
            Sha256(baseline),
            Sha256(candidate),
            baseline.LongLength,
            candidate.LongLength,
            baseline.SequenceEqual(candidate));
    }

    private static string ResolveOutputPath(string projectRoot, string path)
    {
        var candidate = Path.GetFullPath(
            Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
        var rootWithSeparator =
            projectRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Seal measurement output must stay inside the project root: {candidate}");
        }

        return candidate;
    }

    private static string ResolveExistingPath(string projectRoot, string path)
    {
        var candidate = Path.GetFullPath(
            Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                "Required Seal measurement input does not exist.",
                candidate);
        }

        return candidate;
    }

    private static int ReadPositiveInt(string name, int fallback)
    {
        var value = ReadInt(name, fallback);
        if (value <= 0)
        {
            throw new InvalidOperationException($"{name} must be positive.");
        }

        return value;
    }

    private static string ReadMeasurementMode()
    {
        var mode = Environment.GetEnvironmentVariable("SM_H100_SEAL_MEASUREMENT_MODE")
                   ?? "full";
        return mode switch
        {
            "full" => mode,
            "census" => mode,
            "shipped" => mode,
            _ => throw new InvalidOperationException(
                "SM_H100_SEAL_MEASUREMENT_MODE must be full, census, or shipped."),
        };
    }

    private static IReadOnlyList<H100SealPolicyCalibration> CalibrationGridForMode(
        string measurementMode)
        => measurementMode switch
        {
            "full" => H100SealPolicyMeasurementAnalysis.CalibrationGrid(),
            "census" => Array.Empty<H100SealPolicyCalibration>(),
            "shipped" => new[]
            {
                new H100SealPolicyCalibration(
                    Threshold: 0.70d,
                    NetValueFloor: 0.01d,
                    Baseline: 0.50d),
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(measurementMode),
                measurementMode,
                "Unknown Seal measurement mode."),
        };

    private static int ReadInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw)
            ? fallback
            : int.Parse(raw, CultureInfo.InvariantCulture);
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return string.Concat(
            sha.ComputeHash(bytes)
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string Format(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";
}
