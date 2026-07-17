using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>run rows를 anchor/tier와 BT6/BT7 threshold metric으로 집계한다.</summary>
public static class IntentTrackMetricsCalculator
{
    private const double OneSidedZ95 = 1.6448536269514722d;
    private const double MissingLatencyPenalty = 1_000_000d;

    public static IntentTrackReport Calculate(
        IEnumerable<IntentTrackRunRecord> runRecords,
        int seedBase,
        int seedCount,
        int ownerAnchorCount,
        int systemMedoidCatalogCount,
        int systemMedoidSampleCount,
        IEnumerable<string> enabledLeverIds,
        string agencyWindowDefinition,
        string v1LeverCaveat,
        string rightSizeNote,
        IntentTrackPredicateCoverage predicateCoverage,
        string evaluatorVersion)
    {
        var runs = (runRecords ?? Array.Empty<IntentTrackRunRecord>())
            .OrderBy(value => value.ConceptKind, StringComparer.Ordinal)
            .ThenBy(value => value.ConceptId, StringComparer.Ordinal)
            .ThenBy(value => value.Seed)
            .ThenBy(value => value.RunId, StringComparer.Ordinal)
            .ToArray();
        Validate(runs, seedCount, ownerAnchorCount, systemMedoidSampleCount);
        var tiers = runs.GroupBy(value => value.AvailabilityTier, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Tier(group.Key, group.ToArray()))
            .ToArray();
        var owner = runs.Where(value => value.ConceptKind == "owner_anchor")
            .GroupBy(value => value.ConceptId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Concept(group.Key, "owner_anchor", group.ToArray()))
            .ToArray();
        var medoids = runs.Where(value => value.ConceptKind == "system_medoid")
            .GroupBy(value => value.ConceptId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Concept(group.Key, "system_medoid", group.ToArray()))
            .ToArray();
        var gapDistribution = CountById(runs.Select(value => value.GapKind));

        var core = tiers.SingleOrDefault(value => value.AvailabilityTier == "core")
                   ?? EmptyTier("core");
        var aspirational = tiers.SingleOrDefault(value => value.AvailabilityTier == "aspirational")
                            ?? EmptyTier("aspirational");
        var bt6 = new[]
        {
            Metric("core_track_realizability_rate", core.TrackAvailableRate, core.RunCount),
            Metric("core_track_realizability_lcb95", core.TrackAvailableLcb95, core.RunCount),
            Metric("aspirational_track_realizability_rate", aspirational.TrackAvailableRate, aspirational.RunCount),
            Metric("aspirational_track_realizability_lcb95", aspirational.TrackAvailableLcb95, aspirational.RunCount),
            Metric("core_first_progress_agency_window_p90", core.FirstProgressSampleCount == 0 ? MissingLatencyPenalty : core.FirstProgressP90, core.FirstProgressSampleCount),
            Metric("aspirational_first_progress_agency_window_p90", aspirational.FirstProgressSampleCount == 0 ? MissingLatencyPenalty : aspirational.FirstProgressP90, aspirational.FirstProgressSampleCount),
            Metric("starvation_run_rate", Rate(runs.Count(value => value.Starved), runs.Length), runs.Length),
            Metric("silent_dead_end_count", runs.Count(value => value.SilentDeadEnd), runs.Length),
        };

        var ownerFailureCount = owner.Count(value => !value.Pass);
        var bt7 = new[]
        {
            Metric("owner_anchor_realization_rate_min", owner.Length == 0 ? 0d : owner.Min(value => value.PolicyCaptureRate), owner.Sum(value => value.CaptureDenominator)),
            Metric("owner_anchor_realization_lcb95_min", owner.Length == 0 ? 0d : owner.Min(value => value.PolicyCaptureLcb95), owner.Sum(value => value.CaptureDenominator)),
            Metric("owner_anchor_realized_before_final_20_percent_rate_min", owner.Length == 0 ? 0d : owner.Min(value => value.RealizedBeforeFinalTwentyPercentRate), owner.Sum(value => value.PolicyRealizedCount)),
            Metric("owner_anchor_post_realization_battle_opportunity_min", owner.Length == 0 ? 0d : owner.Min(value => value.PayoffRunwayMin), owner.Sum(value => value.PolicyRealizedCount)),
            Metric("owner_anchor_payoff_witness_count_min", owner.Length == 0 ? 0d : owner.Min(value => value.PayoffWitnessCount), owner.Sum(value => value.PayoffWitnessCount)),
            Metric("owner_anchor_failure_count", ownerFailureCount, owner.Length),
            Metric("derived_medoid_pass_rate", Rate(medoids.Count(value => value.Pass), medoids.Length), medoids.Length),
        };

        return new IntentTrackReport
        {
            EvaluatorVersion = evaluatorVersion,
            SeedBase = seedBase,
            SeedCount = seedCount,
            OwnerAnchorCount = ownerAnchorCount,
            SystemMedoidCatalogCount = systemMedoidCatalogCount,
            SystemMedoidSampleCount = systemMedoidSampleCount,
            EnabledLeverIds = (enabledLeverIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            AgencyWindowDefinition = agencyWindowDefinition ?? string.Empty,
            V1LeverCaveat = v1LeverCaveat ?? string.Empty,
            RightSizeNote = rightSizeNote ?? string.Empty,
            PredicateCoverage = predicateCoverage ?? IntentTrackPredicateCoverage.Empty,
            TierSummaries = tiers,
            OwnerAnchorSummaries = owner,
            SystemMedoidSummaries = medoids,
            GapDistribution = gapDistribution,
            FalseHopeRate = Rate(runs.Count(value => value.SilentDeadEnd), runs.Count(value => !value.TrackAvailable)),
            Bt6Metrics = bt6,
            Bt7Metrics = bt7,
            Runs = runs,
        };
    }

    public static IReadOnlyList<H100GateEvaluator.ExternalObservation> ToBt67Observations(IntentTrackReport report)
        => report.Bt6Metrics.Concat(report.Bt7Metrics)
            .Select(value => new H100GateEvaluator.ExternalObservation(
                value.MetricId,
                value.Value,
                value.SampleCount,
                IntentTrackReportWriter.FileName))
            .ToArray();

    public static double OneSidedWilsonLowerBound(int successes, int trials)
    {
        if (trials <= 0) return 0d;
        if (successes < 0 || successes > trials) throw new ArgumentOutOfRangeException(nameof(successes));
        var n = (double)trials;
        var p = successes / n;
        var z2 = OneSidedZ95 * OneSidedZ95;
        var denominator = 1d + z2 / n;
        var center = p + z2 / (2d * n);
        var margin = OneSidedZ95 * Math.Sqrt((p * (1d - p) / n) + (z2 / (4d * n * n)));
        return Math.Max(0d, (center - margin) / denominator);
    }

    private static IntentTrackTierSummary Tier(string tier, IReadOnlyList<IntentTrackRunRecord> runs)
    {
        var track = runs.Where(value => value.TrackAvailable).ToArray();
        var firstProgress = track.Where(value => value.FirstProgressTime >= 0).Select(value => value.FirstProgressTime).ToArray();
        return new IntentTrackTierSummary(
            tier,
            runs.Count,
            track.Length,
            Rate(track.Length, runs.Count),
            OneSidedWilsonLowerBound(track.Length, runs.Count),
            firstProgress.Length,
            Percentile90(firstProgress),
            Percentile90(runs.Select(value => value.MaxAgencyDrought)),
            runs.Count(value => value.Starved),
            Rate(runs.Count(value => value.Starved), runs.Count));
    }

    private static IntentTrackConceptSummary Concept(
        string conceptId,
        string conceptKind,
        IReadOnlyList<IntentTrackRunRecord> runs)
    {
        var track = runs.Where(value => value.TrackAvailable).ToArray();
        var realizedOnTrack = track.Count(value => value.PolicyRealized);
        var realized = runs.Where(value => value.PolicyRealized).ToArray();
        var captureRate = Rate(realizedOnTrack, track.Length);
        var captureLcb = OneSidedWilsonLowerBound(realizedOnTrack, track.Length);
        var beforeRate = Rate(realized.Count(value => value.RealizedBeforeFinalTwentyPercent), realized.Length);
        var runwayMin = realized.Length == 0 ? 0 : realized.Min(value => value.PayoffRunway);
        var payoffWitnessCount = realized.Count(value => value.PayoffWitnessed);
        var counterCount = runs.Sum(value => value.CounterDecisionCount);
        var retained = runs.Sum(value => value.IdentityRetainedCounterDecisionCount);
        var variantRuns = runs.SelectMany(value => value.VariantResults).ToArray();
        var variantSummaries = variantRuns
            .GroupBy(value => value.VariantId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var values = group.ToArray();
                var predicates = values.SelectMany(value => value.IdentityPredicates)
                    .GroupBy(value => value.Predicate, StringComparer.Ordinal)
                    .OrderBy(predicateGroup => predicateGroup.Key, StringComparer.Ordinal)
                    .Select(predicateGroup => new IntentTrackPredicateSummary(
                        predicateGroup.Key,
                        predicateGroup.Select(value => value.PredicateKind).Distinct(StringComparer.Ordinal).Single(),
                        predicateGroup.Count(),
                        predicateGroup.Count(value => value.Satisfied)))
                    .ToArray();
                return new IntentTrackVariantSummary(
                    group.Key,
                    values.Select(value => value.AvailabilityTier).Distinct(StringComparer.Ordinal).Single(),
                    values.Length,
                    values.Count(value => value.AvailabilityKind == "v1_track"),
                    values.Count(value => value.AvailabilityKind == "lever_pending"),
                    values.Count(value => value.AvailabilityKind == "true_unavailable"),
                    CountById(values.SelectMany(value => value.PendingLeverIds)),
                    predicates);
            })
            .ToArray();
        var pass = captureRate >= 0.70d
                   && captureLcb >= 0.55d
                   && beforeRate >= 0.70d
                   && runwayMin >= 2
                   && payoffWitnessCount >= 1;
        return new IntentTrackConceptSummary(
            conceptId,
            conceptKind,
            runs.Select(value => value.AvailabilityTier).Distinct(StringComparer.Ordinal).Single(),
            runs.Count,
            track.Length,
            Rate(track.Length, runs.Count),
            OneSidedWilsonLowerBound(track.Length, runs.Count),
            realizedOnTrack,
            track.Length,
            captureRate,
            captureLcb,
            Percentile90(track.Where(value => value.FirstProgressTime >= 0).Select(value => value.FirstProgressTime)),
            Percentile90(runs.Select(value => value.MaxAgencyDrought)),
            runs.Count(value => value.Starved),
            Rate(runs.Count(value => value.Starved), runs.Count),
            beforeRate,
            runwayMin,
            payoffWitnessCount,
            counterCount,
            Rate(retained, counterCount),
            CountById(runs.Select(value => value.GapKind)),
            pass,
            variantSummaries.Length,
            track.Length,
            variantRuns.Count(value => value.AvailabilityKind == "v1_track"),
            variantRuns.Count(value => value.AvailabilityKind == "lever_pending"),
            variantRuns.Count(value => value.AvailabilityKind == "true_unavailable"),
            CountById(variantRuns.SelectMany(value => value.PendingLeverIds)),
            variantSummaries);
    }

    private static IntentTrackTierSummary EmptyTier(string tier)
        => new(tier, 0, 0, 0d, 0d, 0, -1, -1, 0, 0d);

    private static IntentTrackMetricValue Metric(string id, double value, int sampleCount)
        => new(id, value, Math.Max(0, sampleCount));

    private static IReadOnlyList<IntentTrackCount> CountById(IEnumerable<string> values)
        => values.GroupBy(value => value ?? string.Empty, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new IntentTrackCount(group.Key, group.Count()))
            .ToArray();

    private static int Percentile90(IEnumerable<int> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0) return -1;
        var index = Math.Max(0, (int)Math.Ceiling(ordered.Length * 0.90d) - 1);
        return ordered[index];
    }

    private static double Rate(int numerator, int denominator)
        => denominator <= 0 ? 0d : (double)numerator / denominator;

    private static void Validate(
        IReadOnlyList<IntentTrackRunRecord> runs,
        int seedCount,
        int ownerAnchorCount,
        int medoidSampleCount)
    {
        if (seedCount <= 0) throw new ArgumentOutOfRangeException(nameof(seedCount));
        if (ownerAnchorCount <= 0) throw new ArgumentOutOfRangeException(nameof(ownerAnchorCount));
        if (medoidSampleCount < 0) throw new ArgumentOutOfRangeException(nameof(medoidSampleCount));
        if (runs.Any(value => string.IsNullOrWhiteSpace(value.RunId)
                              || string.IsNullOrWhiteSpace(value.ConceptId)
                              || string.IsNullOrWhiteSpace(value.RepresentativeVariantId)
                              || value.AgencyWindowCount < 0
                              || value.MaxAgencyDrought < 0
                              || value.VariantCount <= 0
                              || value.VariantResults == null
                              || value.VariantResults.Count != value.VariantCount))
        {
            throw new ArgumentException("Intent track run row is invalid.", nameof(runs));
        }

        foreach (var run in runs)
        {
            var duplicateVariant = run.VariantResults.GroupBy(value => value.VariantId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            var v1TrackCount = run.VariantResults.Count(value => value.AvailabilityKind == "v1_track");
            var pendingCount = run.VariantResults.Count(value => value.AvailabilityKind == "lever_pending");
            var unavailableCount = run.VariantResults.Count(value => value.AvailabilityKind == "true_unavailable");
            if (duplicateVariant != null
                || v1TrackCount + pendingCount + unavailableCount != run.VariantCount
                || pendingCount != run.LeverPendingVariantCount
                || unavailableCount != run.TrueUnavailableVariantCount
                || run.TrackAvailable != (v1TrackCount > 0)
                || run.TrackAvailable && string.IsNullOrWhiteSpace(run.SelectedTrackVariantId))
            {
                throw new ArgumentException($"Intent track variant breakdown is invalid: {run.RunId}", nameof(runs));
            }
        }

        var duplicate = runs.GroupBy(value => value.RunId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null) throw new ArgumentException($"Duplicate intent track run id: {duplicate.Key}", nameof(runs));
        var ownerConceptCount = runs.Where(value => value.ConceptKind == "owner_anchor")
            .Select(value => value.ConceptId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var medoidConceptCount = runs.Where(value => value.ConceptKind == "system_medoid")
            .Select(value => value.ConceptId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (ownerConceptCount != ownerAnchorCount || medoidConceptCount != medoidSampleCount)
        {
            throw new ArgumentException(
                $"Intent track concept coverage mismatch: owner={ownerConceptCount}/{ownerAnchorCount}, medoid={medoidConceptCount}/{medoidSampleCount}.",
                nameof(runs));
        }

        if (runs.GroupBy(value => (value.ConceptKind, value.ConceptId)).Any(group => group.Count() != seedCount))
        {
            throw new ArgumentException($"Every intent track concept must have exactly {seedCount} seed runs.", nameof(runs));
        }


        if (runs.GroupBy(value => (value.ConceptKind, value.ConceptId)).Any(group =>
                group.Select(value => string.Join("|", value.VariantResults
                        .Select(variant => variant.VariantId)
                        .OrderBy(value => value, StringComparer.Ordinal)))
                    .Distinct(StringComparer.Ordinal)
                    .Count() != 1))
        {
            throw new ArgumentException("Intent track variant coverage changed across seeds.", nameof(runs));
        }
    }
}
