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
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>
/// Seal-enabled and plain-Refit concept policies run against the same full campaign,
/// while a separate no-Seal lane proves the pre-extension intent trace golden.
/// </summary>
public static class H100SealPolicyMeasurementRunner
{
    private const string GateSpecRelativePath =
        "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-v1.json";
    private const string DefaultCoverageAnchorId = "anchor_iron_line";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static void RunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(
            nameof(H100SealPolicyMeasurementRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputRoot = ResolvePath(
            projectRoot,
            Environment.GetEnvironmentVariable("SM_H100_SEAL_MEASUREMENT_OUTPUT")
            ?? "Logs/h100-seal-policy-measurement");
        var baselineTracePath = ResolvePath(
            projectRoot,
            Environment.GetEnvironmentVariable("SM_H100_SEAL_BASELINE_TRACE")
            ?? "Logs/20260726-seal-headless-policy/baseline/coverage/intent_trace.jsonl");
        var seedBase = ReadInt("SM_H100_SEAL_SEED_BASE", 1701);
        var siteSafety = ReadPositiveInt("SM_H100_SEAL_SITE_SAFETY", 32);
        var maxBattleSteps = ReadPositiveInt("SM_H100_SEAL_MAX_BATTLE_STEPS", 300);
        var anchorId = Environment.GetEnvironmentVariable("SM_H100_SEAL_COVERAGE_ANCHOR")
                       ?? DefaultCoverageAnchorId;

        Directory.CreateDirectory(outputRoot);
        if (!File.Exists(baselineTracePath))
        {
            throw new FileNotFoundException(
                "The pre-change no-Seal intent trace baseline is required.",
                baselineTracePath);
        }

        var spec = H100GateSpec.LoadFromFile(Path.Combine(projectRoot, GateSpecRelativePath));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var settings = new H100MetricsRunSettings(
            BattleCount: 1,
            CampaignCount: 1,
            ReplayCopies: 2,
            SeedBase: seedBase,
            CampaignSiteSafety: siteSafety,
            MaxBattleSteps: maxBattleSteps,
            WriteCsv: false,
            OutputDirectory: outputRoot,
            PolicyId: ConceptCommitPolicy.PolicyId);
        var catalog = H100ConceptCatalogRunner.DeriveForSnapshot(snapshot);
        var coverageIntent = H100ConceptIntentProjector.ProjectSingle(catalog, anchorId);

        var goldenDirectory = Path.Combine(outputRoot, "no-seal-golden");
        var goldenCorpus = H100CampaignCorpusRunner.Run(
            lookup,
            settings,
            spec.TargetBattleSeconds,
            observationHooks: new H100CampaignObservationHooks(
                RosterObservationTransform: DisableSeal),
            policyFactory: _ => new ConceptCommitPolicy(coverageIntent));
        var candidateTracePath = IntentTraceArtifactWriter.Write(
            goldenDirectory,
            goldenCorpus.IntentTraces);
        var golden = CompareGolden(baselineTracePath, candidateTracePath);
        if (!golden.ByteIdentical)
        {
            throw new InvalidOperationException(
                $"No-Seal intent trace moved: baseline={golden.BaselineSha256}, "
                + $"candidate={golden.CandidateSha256}.");
        }

        var targetAffixId = SelectMissingAffix(snapshot, lookup, settings);
        var measurementIntent = AddMissingAffixIntent(
            coverageIntent,
            targetAffixId);
        var withSeal = RunArm(
            "with-seal",
            lookup,
            snapshot,
            settings,
            spec.TargetBattleSeconds,
            measurementIntent,
            disableSeal: false);
        var withoutSeal = RunArm(
            "without-seal",
            lookup,
            snapshot,
            settings,
            spec.TargetBattleSeconds,
            measurementIntent,
            disableSeal: true);
        var delta = BuildDelta(withSeal, withoutSeal);
        var verdict = withSeal.SealCount == 0
            ? "The policy correctly ignored Seal in this paired full campaign."
            : delta.CraftingEchoSpent == 0
              && NullableDelta(withSeal.MeanRollQualityAfter, withoutSeal.MeanRollQualityAfter) == 0d
              && !delta.CampaignOutcomeChanged
                ? "The policy exercised Seal, but the paired campaign showed no measurable change."
                : "The policy exercised Seal and changed at least one measured crafting or campaign outcome.";
        var report = new H100SealPolicyMeasurementReport(
            "h100-seal-policy-measurement-v1",
            anchorId,
            measurementIntent.IntentId,
            targetAffixId,
            golden,
            withSeal,
            withoutSeal,
            delta,
            verdict);
        var reportPath = Path.Combine(outputRoot, "seal-policy-measurement.json");
        File.WriteAllText(
            reportPath,
            HeadlessMetricJson.Serialize(report) + "\n",
            Utf8WithoutBom);
        Debug.Log(
            $"[H100SealPolicyMeasurement] seals={withSeal.SealCount};"
            + $"plain_refits={withSeal.PlainRefitCount};"
            + $"echo_delta={delta.CraftingEchoSpent};"
            + $"quality_after_delta={FormatNullable(delta.MeanRollQualityAfter)};"
            + $"outcome_changed={delta.CampaignOutcomeChanged};"
            + $"golden={golden.ByteIdentical};output={reportPath}");
    }

    private static H100SealPolicyArmReport RunArm(
        string armId,
        RuntimeCombatContentLookup lookup,
        CombatContentSnapshot snapshot,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        HeadlessConceptIntent intent,
        bool disableSeal)
    {
        var collector = new SealMeasurementCollector(snapshot);
        var hooks = collector.CreateHooks(disableSeal ? DisableSeal : null);
        var corpus = H100CampaignCorpusRunner.Run(
            lookup,
            settings,
            targetBattleSeconds,
            observationHooks: hooks,
            policyFactory: _ => new ConceptCommitPolicy(intent));
        var campaign = corpus.Campaigns.Single();
        return collector.BuildReport(armId, campaign);
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
        var campaignId = "campaign-000000";
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
            source.IdentityPredicates.Concat(new[] { $"owned:affix:{targetAffixId}" }).ToArray(),
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

    private static H100SealPolicyDelta BuildDelta(
        H100SealPolicyArmReport withSeal,
        H100SealPolicyArmReport withoutSeal)
    {
        var left = withSeal.Campaign;
        var right = withoutSeal.Campaign;
        var outcomeChanged = left.Completed != right.Completed
                             || left.Truncated != right.Truncated
                             || !string.Equals(
                                 left.TerminalReason,
                                 right.TerminalReason,
                                 StringComparison.Ordinal)
                             || left.SiteCount != right.SiteCount
                             || left.BattleCount != right.BattleCount
                             || left.WinCount != right.WinCount
                             || left.LossCount != right.LossCount;
        return new H100SealPolicyDelta(
            withSeal.SealCount - withoutSeal.SealCount,
            withSeal.CraftingEchoSpent - withoutSeal.CraftingEchoSpent,
            NullableDelta(withSeal.MeanRollQualityAfter, withoutSeal.MeanRollQualityAfter),
            NullableDelta(withSeal.MeanRollQualityGain, withoutSeal.MeanRollQualityGain),
            BoolInt(left.Completed) - BoolInt(right.Completed),
            left.SiteCount - right.SiteCount,
            left.BattleCount - right.BattleCount,
            left.WinCount - right.WinCount,
            left.LossCount - right.LossCount,
            outcomeChanged);
    }

    private static double? NullableDelta(double? left, double? right)
        => left.HasValue && right.HasValue ? left.Value - right.Value : null;

    private static int BoolInt(bool value) => value ? 1 : 0;

    private static string ResolvePath(string projectRoot, string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(projectRoot, path));

    private static int ReadPositiveInt(string name, int fallback)
    {
        var value = ReadInt(name, fallback);
        if (value <= 0)
        {
            throw new InvalidOperationException($"{name} must be positive.");
        }

        return value;
    }

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

    private static string FormatNullable(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";

    private sealed class SealMeasurementCollector
    {
        private readonly CombatContentSnapshot _snapshot;
        private readonly Dictionary<int, PendingRefit> _pending = new();
        private readonly List<H100SealCraftingOperationRecord> _operations = new();
        private int _refitWindowCount;
        private int _skipCount;

        public SealMeasurementCollector(CombatContentSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public H100CampaignObservationHooks CreateHooks(
            Func<HeadlessRosterPolicyObservation, HeadlessRosterPolicyObservation>? transform)
            => new(
                RosterDecisionOffered: OnRosterDecisionOffered,
                DecisionApplied: OnDecisionApplied,
                RosterObservationTransform: transform);

        public H100SealPolicyArmReport BuildReport(
            string armId,
            CampaignMetricRecord campaign)
        {
            var sealedOperations = _operations
                .Where(value => string.Equals(value.Operation, "seal", StringComparison.Ordinal))
                .ToArray();
            var qualityBefore = AverageOrNull(_operations.Select(value => value.RollQualityBefore));
            var qualityAfter = AverageOrNull(_operations.Select(value => value.RollQualityAfter));
            var qualityGain = AverageOrNull(_operations.Select(value => value.RollQualityDelta));
            return new H100SealPolicyArmReport(
                armId,
                _refitWindowCount,
                sealedOperations.Length,
                _operations.Count - sealedOperations.Length,
                _skipCount,
                _operations.Sum(value => value.EchoSpent),
                sealedOperations
                    .Select(value => $"{value.ItemId}:{value.ItemInstanceId}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                qualityBefore,
                qualityAfter,
                qualityGain,
                _operations.ToArray(),
                campaign);
        }

        private void OnRosterDecisionOffered(H100RosterDecisionOfferedContext context)
        {
            if (!string.Equals(context.LeverId, IntentTrackLeverId.Refit, StringComparison.Ordinal))
            {
                return;
            }

            _refitWindowCount++;
            var items = context.Session.Profile.Inventory
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemInstanceId))
                .ToDictionary(
                    item => item.ItemInstanceId,
                    item => new PendingItem(
                        item.ItemBaseId,
                        MeasureItem(_snapshot, item)),
                    StringComparer.Ordinal);
            _pending.Add(
                context.DecisionIndex,
                new PendingRefit(context.Session.Profile.Currencies.Echo, items));
        }

        private void OnDecisionApplied(H100DecisionAppliedContext context)
        {
            if (!string.Equals(context.SeamType, IntentTrackLeverId.Refit, StringComparison.Ordinal))
            {
                return;
            }

            if (!_pending.Remove(context.DecisionIndex, out var pending))
            {
                throw new InvalidOperationException(
                    $"Refit decision {context.DecisionIndex} has no offered observation.");
            }

            if (string.Equals(context.AppliedActionDescriptor, "skip", StringComparison.Ordinal))
            {
                _skipCount++;
                return;
            }

            var parts = context.AppliedActionDescriptor.Split(':');
            if (parts.Length is not (2 or 3)
                || (parts.Length == 3
                    && !parts[2].StartsWith("seal=", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Unexpected refit descriptor '{context.AppliedActionDescriptor}'.");
            }

            var itemInstanceId = parts[0];
            if (!pending.Items.TryGetValue(itemInstanceId, out var before))
            {
                throw new InvalidOperationException(
                    $"Applied refit item '{itemInstanceId}' was not in the offered inventory.");
            }

            var item = context.Session.Profile.Inventory.Single(value =>
                string.Equals(value.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
            var echoSpent = pending.EchoBefore - context.Session.Profile.Currencies.Echo;
            if (echoSpent < 0)
            {
                throw new InvalidOperationException(
                    $"Crafting increased Echo by {-echoSpent}.");
            }

            var afterQuality = MeasureItem(_snapshot, item);
            _operations.Add(new H100SealCraftingOperationRecord(
                context.DecisionIndex,
                parts.Length == 3 ? "seal" : "refit",
                before.ItemId,
                itemInstanceId,
                echoSpent,
                before.RollQuality,
                afterQuality,
                afterQuality - before.RollQuality));
        }

        private static double MeasureItem(
            CombatContentSnapshot snapshot,
            InventoryItemRecord item)
        {
            var affixIds = (item.AffixIds ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            var affixIdSet = affixIds.ToHashSet(StringComparer.Ordinal);
            var magnitudes = (item.AffixMagnitudeRolls
                              ?? new List<InventoryAffixMagnitudeRecord>())
                .Where(value => value != null && affixIdSet.Contains(value.AffixId))
                .GroupBy(value => value.AffixId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Magnitude,
                    StringComparer.Ordinal);
            return RefitRollQuality.Measure(snapshot, affixIds, magnitudes);
        }

        private static double? AverageOrNull(IEnumerable<double> values)
        {
            var materialized = values.ToArray();
            return materialized.Length == 0 ? null : materialized.Average();
        }

        private sealed record PendingRefit(
            int EchoBefore,
            IReadOnlyDictionary<string, PendingItem> Items);

        private sealed record PendingItem(
            string ItemId,
            double RollQuality);
    }
}
