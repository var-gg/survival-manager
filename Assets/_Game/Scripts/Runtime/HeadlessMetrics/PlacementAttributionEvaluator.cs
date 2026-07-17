using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>
/// 같은 composition·encounter·seed에서 placement만 바꾼 trace를 전술/거리/타게팅/pathing/raw로 귀속한다.
/// 밸런스나 전투 truth를 수정하지 않는 진단 전용 evaluator다.
/// </summary>
public static class PlacementAttributionEvaluator
{
    public const string ComponentTactical = "visible_tactical_channel";
    public const string ComponentRawDistance = "raw_contact_geometry";
    public const string ComponentTargeting = "target_selection_discontinuity";
    public const string ComponentPathing = "pathing_artifact";
    public const string ComponentPolicyNoise = "policy_noise";
    public const string ComponentUnexplainedRaw = "unexplained_raw";
    public const string ComponentNoMaterialDelta = "no_material_outcome_delta";

    public const double MaterialPowerDelta = 0.10d;
    public const double BugGradeWinRateDelta = 0.25d;
    public const double FirstContactTimeDeltaSeconds = 0.50d;
    public const double FirstContactDistanceDelta = 0.35d;
    public const int TargetSwitchDelta = 2;
    public const int PathingReplanDelta = 2;
    public const double TravelDistanceDelta = 1.0d;
    public const double ApproachStallRatioDelta = 0.10d;
    public const double BroadGroupRate = 0.25d;
    public const double MajorityShare = 0.50d;

    private const double Epsilon = 0.000000001d;
    private const int MinimumTrapComparatorPairs = 8;

    public static PlacementAttributionReport Evaluate(
        string runId,
        IReadOnlyList<PlacementAttributionBattleRecord> battles,
        IReadOnlyList<FormationOptionAttributionEvidence>? formationEvidence = null,
        string rightSizeNote = "")
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("run id is empty", nameof(runId));
        }

        if (battles == null)
        {
            throw new ArgumentNullException(nameof(battles));
        }

        var ordered = battles
            .OrderBy(value => value.ComparisonKind, StringComparer.Ordinal)
            .ThenBy(value => value.PairingId, StringComparer.Ordinal)
            .ThenBy(value => value.PlacementVariantId, StringComparer.Ordinal)
            .ThenBy(value => value.BattleId, StringComparer.Ordinal)
            .ToArray();
        var failures = ordered.Where(value => !string.IsNullOrWhiteSpace(value.FailureCode))
            .Select(value => $"{value.BattleId}={value.FailureCode}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var valid = ordered.Where(value => string.IsNullOrWhiteSpace(value.FailureCode)).ToArray();
        var pairEvaluations = BuildPairs(valid);
        var pairRows = pairEvaluations.Select(value => value.Record).ToArray();
        var sample = BuildSample(ordered, valid, pairRows);
        var components = BuildComponents(pairRows);
        var semantic = BuildSemanticSummary(pairRows);
        var anchors = BuildAnchorDominance(valid, sample);
        var options = BuildFormationOptions(
            valid,
            pairEvaluations,
            formationEvidence ?? Array.Empty<FormationOptionAttributionEvidence>());
        var conditions = BuildProConditions(pairRows, semantic, components, anchors);
        var triggered = conditions.Any(value => value.Triggered);
        var trapCandidate = options.Any(value => value.TrapCandidate);
        var status = failures.Length == 0 ? "complete" : "technical_failure";
        var verdict = failures.Length > 0
            ? "insufficient_evidence"
            : triggered || trapCandidate
                ? "bug_or_trap_candidate"
                : "no_bug_grade_condition_observed";

        return new PlacementAttributionReport
        {
            RunId = runId,
            Status = status,
            Verdict = verdict,
            Methodology = BuildMethodology(rightSizeNote),
            Sample = sample,
            Components = components,
            SemanticSwap = semantic,
            ProConditions = conditions,
            AnchorDominance = anchors,
            FormationOptions = options,
            PairAttributions = pairRows,
            TechnicalFailures = failures,
        };
    }

    private static IReadOnlyList<PairEvaluation> BuildPairs(
        IReadOnlyList<PlacementAttributionBattleRecord> battles)
    {
        var results = new List<PairEvaluation>();
        foreach (var group in battles.Where(value => !string.IsNullOrWhiteSpace(value.PairingId))
                     .GroupBy(value => value.PairingId, StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            var baseline = group.Where(value => value.IsBaseline)
                .OrderBy(value => value.PlacementVariantId, StringComparer.Ordinal)
                .ThenBy(value => value.BattleId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (baseline == null)
            {
                continue;
            }

            foreach (var candidate in group.Where(value => !ReferenceEquals(value, baseline))
                         .OrderBy(value => value.PlacementVariantId, StringComparer.Ordinal)
                         .ThenBy(value => value.BattleId, StringComparer.Ordinal))
            {
                RequirePairedControls(group.Key, baseline, candidate);
                var channelDeltas = baseline.Channels.Select(value => value.ChannelId)
                    .Concat(candidate.Channels.Select(value => value.ChannelId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(channelId => new PlacementAttributionReport.ChannelDelta(
                        channelId,
                        ChannelCount(candidate, channelId) - ChannelCount(baseline, channelId)))
                    .ToArray();
                var winnerChanged = !string.Equals(
                    candidate.WinnerSide,
                    baseline.WinnerSide,
                    StringComparison.Ordinal);
                var allyWinDelta = WinValue(candidate.WinnerSide) - WinValue(baseline.WinnerSide);
                var powerDelta = candidate.NormalizedFinalPowerDifference - baseline.NormalizedFinalPowerDifference;
                var material = winnerChanged || Math.Abs(powerDelta) >= MaterialPowerDelta;
                var contactPresenceChanged = (baseline.Trace.FirstContactTick >= 0)
                                             != (candidate.Trace.FirstContactTick >= 0);
                var contactTimeDelta = ContactTime(candidate) - ContactTime(baseline);
                var contactDistanceDelta = DifferenceWithMissing(
                    candidate.Trace.FirstContactDistance,
                    baseline.Trace.FirstContactDistance);
                var firstTargetChanged = !string.Equals(
                    candidate.Trace.FirstTargetSignature,
                    baseline.Trace.FirstTargetSignature,
                    StringComparison.Ordinal);
                var targetSwitchDelta = candidate.Trace.TargetSwitchCount - baseline.Trace.TargetSwitchCount;
                var replanDelta = candidate.Trace.PathingReplanCount - baseline.Trace.PathingReplanCount;
                var travelDelta = candidate.Trace.AllyTravelDistance - baseline.Trace.AllyTravelDistance;
                var stallDelta = candidate.Trace.ApproachStallRatio - baseline.Trace.ApproachStallRatio;
                var classification = Classify(
                    material,
                    channelDeltas,
                    contactPresenceChanged,
                    contactTimeDelta,
                    contactDistanceDelta,
                    firstTargetChanged,
                    targetSwitchDelta,
                    replanDelta,
                    travelDelta,
                    stallDelta);
                var record = new PlacementAttributionReport.PairAttributionRecord
                {
                    ComparisonId = $"{group.Key}|{candidate.PlacementVariantId}",
                    PairingId = group.Key,
                    ComparisonKind = baseline.ComparisonKind,
                    CompositionId = baseline.CompositionId,
                    EncounterFamilyId = baseline.EncounterFamilyId,
                    Seed = baseline.Seed,
                    BattleSeed = baseline.BattleSeed,
                    BaselinePlacementVariantId = baseline.PlacementVariantId,
                    CandidatePlacementVariantId = candidate.PlacementVariantId,
                    BaselineProfileId = baseline.FormationProfileId,
                    CandidateProfileId = candidate.FormationProfileId,
                    SemanticFeaturesPreserved = Equals(
                        baseline.FormationFeatures,
                        candidate.FormationFeatures),
                    WinnerChanged = winnerChanged,
                    AllyWinDelta = Round(allyWinDelta),
                    NormalizedPowerDelta = Round(powerDelta),
                    MaterialOutcomeDelta = material,
                    ChannelDeltas = channelDeltas,
                    FirstContactTimeDelta = Round(contactTimeDelta),
                    FirstContactDistanceDelta = Round(contactDistanceDelta),
                    FirstTargetChanged = firstTargetChanged,
                    TargetSwitchDelta = targetSwitchDelta,
                    PathingReplanDelta = replanDelta,
                    AllyTravelDistanceDelta = Round(travelDelta),
                    ApproachStallRatioDelta = Round(stallDelta),
                    Component = classification.Component,
                    PlayerVisibleExplainable = classification.PlayerVisibleExplainable,
                    Explanation = classification.Explanation,
                };
                results.Add(new PairEvaluation(record, baseline, candidate));
            }
        }

        return results.OrderBy(value => value.Record.ComparisonKind, StringComparer.Ordinal)
            .ThenBy(value => value.Record.ComparisonId, StringComparer.Ordinal)
            .ToArray();
    }

    private static Classification Classify(
        bool material,
        IReadOnlyList<PlacementAttributionReport.ChannelDelta> channelDeltas,
        bool contactPresenceChanged,
        double contactTimeDelta,
        double contactDistanceDelta,
        bool firstTargetChanged,
        int targetSwitchDelta,
        int replanDelta,
        double travelDelta,
        double stallDelta)
    {
        if (!material)
        {
            return new Classification(ComponentNoMaterialDelta, false, "winner stable and normalized power delta < 0.10");
        }

        var tactical = channelDeltas.Where(value => value.EventCountDelta != 0)
            .Select(value => value.ChannelId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (tactical.Length > 0)
        {
            return new Classification(
                ComponentTactical,
                true,
                $"typed player-visible channel delta: {string.Join(",", tactical)}");
        }

        if (firstTargetChanged || Math.Abs(targetSwitchDelta) >= TargetSwitchDelta)
        {
            return new Classification(
                ComponentTargeting,
                false,
                $"no channel delta; first_target_changed={firstTargetChanged.ToString().ToLowerInvariant()}, target_switch_delta={targetSwitchDelta}");
        }

        if (contactPresenceChanged
            || Math.Abs(contactTimeDelta) >= FirstContactTimeDeltaSeconds
            || Math.Abs(contactDistanceDelta) >= FirstContactDistanceDelta)
        {
            return new Classification(
                ComponentRawDistance,
                false,
                "no channel delta; first-contact presence/time/distance crossed the raw geometry threshold");
        }

        if (Math.Abs(replanDelta) >= PathingReplanDelta
            || Math.Abs(travelDelta) >= TravelDistanceDelta
            || Math.Abs(stallDelta) >= ApproachStallRatioDelta)
        {
            return new Classification(
                ComponentPathing,
                false,
                "no channel/target/contact threshold; pathing replan/travel/stall delta crossed threshold");
        }

        return new Classification(
            ComponentUnexplainedRaw,
            false,
            "material outcome delta without typed tactical, targeting, contact geometry, or pathing threshold evidence");
    }

    private static PlacementAttributionReport.MethodologySummary BuildMethodology(string rightSizeNote)
        => new()
        {
            PairingControl = "same composition, encounter family, battle seed, content, and simulation limit; placement only",
            SemanticSwapRule = "same-role units exchange adjacent anchors and the full FormationFeatureClassifier snapshot must remain equal",
            TacticalRule = "material outcome delta plus any typed flank/rear/screen/save/dive event-count delta",
            TargetingRule = $"no tactical delta and first target changes or absolute target-switch delta >= {TargetSwitchDelta}",
            DistanceRule = $"no tactical/targeting delta and first-contact presence changes, time delta >= {Format(FirstContactTimeDeltaSeconds)}s, or edge-distance delta >= {Format(FirstContactDistanceDelta)}",
            PathingRule = $"no prior signal and replan delta >= {PathingReplanDelta}, travel delta >= {Format(TravelDistanceDelta)}, or approach-stall delta >= {Format(ApproachStallRatioDelta)}",
            PolicyNoiseRule = "fixed-placement pairs do not invoke a deployment policy, so pair-level policy noise is zero; E05/E06 selection and non-use evidence is reported under formation_options",
            UnexplainedRule = $"winner changes or normalized power delta >= {Format(MaterialPowerDelta)} without any preceding trace signal",
            BroadEvidenceRule = $"condition must cover >= {Format(BroadGroupRate)} of semantic groups or > {Format(MajorityShare)} of material weight and at least two encounter families; anchor dominance must be positive for every sampled composition and family",
            TrapCandidateRule = $"intended context eligible, zero positive channel witness, E05 track available, and >= {MinimumTrapComparatorPairs} equal-cost profile pairs with comparator non-worse >= 0.95 and strictly better >= 0.50",
            RightSizeNote = rightSizeNote ?? string.Empty,
        };

    private static PlacementAttributionReport.SampleSummary BuildSample(
        IReadOnlyList<PlacementAttributionBattleRecord> all,
        IReadOnlyList<PlacementAttributionBattleRecord> valid,
        IReadOnlyList<PlacementAttributionReport.PairAttributionRecord> pairs)
        => new()
        {
            BattleCount = all.Count,
            ValidBattleCount = valid.Count,
            FailedBattleCount = all.Count - valid.Count,
            PairCount = pairs.Count,
            CompositionCount = valid.Select(value => value.CompositionId).Distinct(StringComparer.Ordinal).Count(),
            EncounterFamilyCount = valid.Select(value => value.EncounterFamilyId).Distinct(StringComparer.Ordinal).Count(),
            SeedCount = valid.Select(value => value.Seed).Distinct().Count(),
            SemanticSwapPairCount = pairs.Count(value => value.ComparisonKind == PlacementAttributionComparisonKind.SemanticAdjacentSwap),
            ProfileTransitionPairCount = pairs.Count(value => value.ComparisonKind == PlacementAttributionComparisonKind.ProfileTransition),
            AnchorSweepPairCount = pairs.Count(value => value.ComparisonKind == PlacementAttributionComparisonKind.AnchorSweep),
        };

    private static PlacementAttributionReport.ComponentSummary BuildComponents(
        IReadOnlyList<PlacementAttributionReport.PairAttributionRecord> pairs)
    {
        var material = pairs.Where(value => value.MaterialOutcomeDelta).ToArray();
        var totalWeight = material.Sum(Weight);
        return new PlacementAttributionReport.ComponentSummary
        {
            MaterialPairCount = material.Length,
            NoMaterialDeltaPairCount = pairs.Count(value => !value.MaterialOutcomeDelta),
            TacticalPairCount = Count(ComponentTactical),
            RawDistancePairCount = Count(ComponentRawDistance),
            TargetingPairCount = Count(ComponentTargeting),
            PathingPairCount = Count(ComponentPathing),
            PolicyNoisePairCount = Count(ComponentPolicyNoise),
            UnexplainedRawPairCount = Count(ComponentUnexplainedRaw),
            TacticalShare = Share(ComponentTactical),
            RawDistanceTargetingShare = Share(ComponentRawDistance, ComponentTargeting),
            PathingShare = Share(ComponentPathing),
            PolicyNoiseShare = Share(ComponentPolicyNoise),
            UnexplainedRawShare = Share(ComponentUnexplainedRaw),
            PlayerVisibleExplainableShare = totalWeight <= Epsilon
                ? 0d
                : Round(material.Where(value => value.PlayerVisibleExplainable).Sum(Weight) / totalWeight),
        };

        int Count(params string[] components)
            => material.Count(value => components.Contains(value.Component, StringComparer.Ordinal));

        double Share(params string[] components)
            => totalWeight <= Epsilon
                ? 0d
                : Round(material.Where(value => components.Contains(value.Component, StringComparer.Ordinal)).Sum(Weight) / totalWeight);
    }

    private static PlacementAttributionReport.SemanticSwapSummary BuildSemanticSummary(
        IReadOnlyList<PlacementAttributionReport.PairAttributionRecord> pairs)
    {
        var semanticRows = pairs.Where(value => value.ComparisonKind == PlacementAttributionComparisonKind.SemanticAdjacentSwap)
            .ToArray();
        var groups = semanticRows
            .GroupBy(value => $"{value.CompositionId}|{value.EncounterFamilyId}", StringComparer.Ordinal)
            .Select(group =>
            {
                var rows = group.OrderBy(value => value.Seed).ToArray();
                var average = rows.Average(value => value.AllyWinDelta);
                var nonzero = rows.Where(value => Math.Abs(value.AllyWinDelta) > Epsilon).ToArray();
                var sameDirection = nonzero.Length == rows.Length
                                    && nonzero.Select(value => Math.Sign(value.AllyWinDelta)).Distinct().Count() == 1;
                return new SemanticGroup(
                    rows[0].EncounterFamilyId,
                    rows.Length,
                    average,
                    rows.Length >= 2 && sameDirection && Math.Abs(average) >= BugGradeWinRateDelta);
            })
            .OrderBy(value => value.EncounterFamilyId, StringComparer.Ordinal)
            .ThenBy(value => value.AverageWinDelta)
            .ToArray();
        var flagged = groups.Where(value => value.RepeatedReversal).ToArray();
        return new PlacementAttributionReport.SemanticSwapSummary
        {
            GroupCount = groups.Length,
            RepeatedReversalGroupCount = flagged.Length,
            RepeatedReversalEncounterFamilyCount = flagged.Select(value => value.EncounterFamilyId)
                .Distinct(StringComparer.Ordinal).Count(),
            RepeatedReversalGroupRate = groups.Length == 0 ? 0d : Round(flagged.Length / (double)groups.Length),
            FeatureInvariantViolationCount = semanticRows.Count(value => !value.SemanticFeaturesPreserved),
            MedianAbsoluteWinRateDelta = Percentile(
                groups.Select(value => Math.Abs(value.AverageWinDelta)).OrderBy(value => value).ToArray(),
                0.5d),
        };
    }

    private static IReadOnlyList<PlacementAttributionReport.AnchorDominanceRow> BuildAnchorDominance(
        IReadOnlyList<PlacementAttributionBattleRecord> battles,
        PlacementAttributionReport.SampleSummary sample)
    {
        var rows = battles.Where(value => value.ComparisonKind == PlacementAttributionComparisonKind.AnchorSweep)
            .ToArray();
        var anchors = rows.SelectMany(value => value.AnchorIdsByMemberIndex).Distinct().OrderBy(value => value).ToArray();
        return anchors.Select(anchorId =>
        {
            var used = rows.Where(value => value.AnchorIdsByMemberIndex.Contains(anchorId)).ToArray();
            var unused = rows.Where(value => !value.AnchorIdsByMemberIndex.Contains(anchorId)).ToArray();
            var compositionDeltas = GroupDeltas(rows, value => value.CompositionId, anchorId);
            var familyDeltas = GroupDeltas(rows, value => value.EncounterFamilyId, anchorId);
            var stratumDeltas = GroupDeltas(
                rows,
                value => $"{value.CompositionId}|{value.EncounterFamilyId}",
                anchorId);
            var usedRate = WinRate(used);
            var unusedRate = WinRate(unused);
            var medianStratum = Percentile(stratumDeltas.Select(value => value.Delta).OrderBy(value => value).ToArray(), 0.5d);
            var buildIndependent = compositionDeltas.Length == sample.CompositionCount
                                   && compositionDeltas.All(value => value.Delta > Epsilon)
                                   && familyDeltas.Length == sample.EncounterFamilyCount
                                   && familyDeltas.All(value => value.Delta > Epsilon)
                                   && medianStratum >= BugGradeWinRateDelta;
            return new PlacementAttributionReport.AnchorDominanceRow
            {
                AnchorId = anchorId,
                UsedBattleCount = used.Length,
                UnusedBattleCount = unused.Length,
                UsedWinRate = usedRate,
                UnusedWinRate = unusedRate,
                WinRateDelta = Round(usedRate - unusedRate),
                MedianStratumDelta = medianStratum,
                EvaluableCompositionCount = compositionDeltas.Length,
                PositiveCompositionCount = compositionDeltas.Count(value => value.Delta > Epsilon),
                EvaluableEncounterFamilyCount = familyDeltas.Length,
                PositiveEncounterFamilyCount = familyDeltas.Count(value => value.Delta > Epsilon),
                BuildIndependentDominance = buildIndependent,
            };
        }).ToArray();
    }

    private static IReadOnlyList<PlacementAttributionReport.FormationOptionResult> BuildFormationOptions(
        IReadOnlyList<PlacementAttributionBattleRecord> battles,
        IReadOnlyList<PairEvaluation> pairs,
        IReadOnlyList<FormationOptionAttributionEvidence> evidence)
    {
        var anchorBattles = battles.Where(value => value.ComparisonKind == PlacementAttributionComparisonKind.AnchorSweep)
            .ToArray();
        return evidence.OrderBy(value => value.ChannelId, StringComparer.Ordinal)
            .Select(input =>
            {
                var profiles = input.IntendedProfileIds.ToHashSet(StringComparer.Ordinal);
                var intended = anchorBattles.Where(value => profiles.Contains(value.FormationProfileId)).ToArray();
                var eligible = intended.Count(value => ChannelEligible(value, input.ChannelId));
                var fired = intended.Count(value => ChannelCount(value, input.ChannelId) > 0);
                var comparators = pairs.Where(value => value.Record.ComparisonKind == PlacementAttributionComparisonKind.ProfileTransition)
                    .Select(value => ResolveComparator(value, profiles))
                    .Where(value => value != null)
                    .Cast<ComparatorResult>()
                    .ToArray();
                var nonWorseRate = comparators.Length == 0
                    ? 0d
                    : comparators.Count(value => value.NonWorse) / (double)comparators.Length;
                var strictlyBetterRate = comparators.Length == 0
                    ? 0d
                    : comparators.Count(value => value.StrictlyBetter) / (double)comparators.Length;
                var positiveWitness = input.StageFourFiredCount + fired;
                var totalEligible = input.StageFourEligibleCount + eligible;
                var trap = totalEligible > 0
                           && positiveWitness == 0
                           && input.TrackAvailableCount > 0
                           && comparators.Length >= MinimumTrapComparatorPairs
                           && nonWorseRate >= 0.95d
                           && strictlyBetterRate >= 0.50d;
                return new PlacementAttributionReport.FormationOptionResult
                {
                    ChannelId = input.ChannelId,
                    IntendedProfileIds = input.IntendedProfileIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    StageFourEligibleCount = input.StageFourEligibleCount,
                    StageFourFiredCount = input.StageFourFiredCount,
                    TacticalIntendedEligibleCount = eligible,
                    TacticalIntendedFiredCount = fired,
                    PositiveWitnessCount = positiveWitness,
                    TrackVariantCount = input.TrackVariantCount,
                    TrackEvaluationCount = input.TrackEvaluationCount,
                    TrackAvailableCount = input.TrackAvailableCount,
                    PolicyRealizedCount = input.PolicyRealizedCount,
                    GenericPayoffWitnessCount = input.GenericPayoffWitnessCount,
                    PreviewFormationDecisionCount = input.PreviewFormationDecisionCount,
                    PreviewEvidenceSupportedCount = input.PreviewEvidenceSupportedCount,
                    EqualCostComparatorPairCount = comparators.Length,
                    ComparatorNonWorseRate = Round(nonWorseRate),
                    ComparatorStrictlyBetterRate = Round(strictlyBetterRate),
                    NonUseReason = ResolveNonUseReason(input, eligible, fired, positiveWitness),
                    TrapCandidate = trap,
                };
            }).ToArray();
    }

    private static IReadOnlyList<PlacementAttributionReport.ProConditionResult> BuildProConditions(
        IReadOnlyList<PlacementAttributionReport.PairAttributionRecord> pairs,
        PlacementAttributionReport.SemanticSwapSummary semantic,
        PlacementAttributionReport.ComponentSummary components,
        IReadOnlyList<PlacementAttributionReport.AnchorDominanceRow> anchors)
    {
        var material = pairs.Where(value => value.MaterialOutcomeDelta).ToArray();
        var raw = material.Where(value => value.Component is ComponentRawDistance or ComponentTargeting).ToArray();
        var unexplained = material.Where(value => !value.PlayerVisibleExplainable).ToArray();
        var rawFamilies = raw.Select(value => value.EncounterFamilyId).Distinct(StringComparer.Ordinal).Count();
        var unexplainedFamilies = unexplained.Select(value => value.EncounterFamilyId).Distinct(StringComparer.Ordinal).Count();
        var dominant = anchors.Where(value => value.BuildIndependentDominance).ToArray();
        var maxAnchorDelta = anchors.Count == 0 ? 0d : anchors.Max(value => value.MedianStratumDelta);
        var conditionOne = semantic.GroupCount > 0
                           && semantic.FeatureInvariantViolationCount == 0
                           && semantic.RepeatedReversalGroupRate >= BroadGroupRate
                           && semantic.RepeatedReversalEncounterFamilyCount >= 2;
        var conditionTwo = components.RawDistanceTargetingShare > MajorityShare && rawFamilies >= 2;
        var conditionThree = dominant.Length > 0;
        var unexplainableShare = material.Length == 0 ? 0d : Round(1d - components.PlayerVisibleExplainableShare);
        var conditionFour = unexplainableShare > MajorityShare && unexplainedFamilies >= 2;
        return new[]
        {
            new PlacementAttributionReport.ProConditionResult
            {
                ConditionId = "pro_1_semantic_adjacent_reversal",
                Description = "same-meaning adjacent swaps repeatedly reverse >=25%p across broad samples",
                Triggered = conditionOne,
                ObservedValue = semantic.RepeatedReversalGroupRate,
                Threshold = BroadGroupRate,
                SupportingGroupCount = semantic.RepeatedReversalGroupCount,
                SupportingEncounterFamilyCount = semantic.RepeatedReversalEncounterFamilyCount,
                Evidence = $"median_abs_win_delta={Format(semantic.MedianAbsoluteWinRateDelta)}; feature_invariant_violations={semantic.FeatureInvariantViolationCount}",
            },
            new PlacementAttributionReport.ProConditionResult
            {
                ConditionId = "pro_2_raw_distance_targeting_majority",
                Description = "raw contact geometry or target-selection discontinuity explains most material leverage",
                Triggered = conditionTwo,
                ObservedValue = components.RawDistanceTargetingShare,
                Threshold = MajorityShare,
                SupportingGroupCount = raw.Length,
                SupportingEncounterFamilyCount = rawFamilies,
                Evidence = $"raw_distance={components.RawDistancePairCount}; targeting={components.TargetingPairCount}; tactical_share={Format(components.TacticalShare)}",
            },
            new PlacementAttributionReport.ProConditionResult
            {
                ConditionId = "pro_3_build_independent_anchor_dominance",
                Description = "one anchor dominates independently of composition across all encounter families",
                Triggered = conditionThree,
                ObservedValue = Round(maxAnchorDelta),
                Threshold = BugGradeWinRateDelta,
                SupportingGroupCount = dominant.Length,
                SupportingEncounterFamilyCount = dominant.Length == 0 ? 0 : dominant.Max(value => value.PositiveEncounterFamilyCount),
                Evidence = dominant.Length == 0
                    ? "no anchor was positive for every sampled composition and encounter family"
                    : $"dominant_anchors={string.Join(",", dominant.Select(value => value.AnchorId))}",
            },
            new PlacementAttributionReport.ProConditionResult
            {
                ConditionId = "pro_4_player_visible_explanation_gap",
                Description = "material outcome differences lack typed player-visible tactical explanations",
                Triggered = conditionFour,
                ObservedValue = unexplainableShare,
                Threshold = MajorityShare,
                SupportingGroupCount = unexplained.Length,
                SupportingEncounterFamilyCount = unexplainedFamilies,
                Evidence = $"player_visible_explainable_share={Format(components.PlayerVisibleExplainableShare)}",
            },
        };
    }

    private static ComparatorResult? ResolveComparator(PairEvaluation pair, HashSet<string> intendedProfiles)
    {
        var baselineIntended = intendedProfiles.Contains(pair.Baseline.FormationProfileId);
        var candidateIntended = intendedProfiles.Contains(pair.Candidate.FormationProfileId);
        if (baselineIntended == candidateIntended)
        {
            return null;
        }

        var option = baselineIntended ? pair.Baseline : pair.Candidate;
        var comparator = baselineIntended ? pair.Candidate : pair.Baseline;
        var optionWin = WinValue(option.WinnerSide);
        var comparatorWin = WinValue(comparator.WinnerSide);
        var nonWorse = comparatorWin + Epsilon >= optionWin
                       && comparator.NormalizedFinalPowerDifference + Epsilon >= option.NormalizedFinalPowerDifference;
        var strictlyBetter = comparatorWin > optionWin + Epsilon
                             || comparator.NormalizedFinalPowerDifference > option.NormalizedFinalPowerDifference + Epsilon;
        return new ComparatorResult(nonWorse, strictlyBetter);
    }

    private static string ResolveNonUseReason(
        FormationOptionAttributionEvidence evidence,
        int tacticalEligible,
        int tacticalFired,
        int positiveWitness)
    {
        if (positiveWitness > 0)
        {
            return evidence.StageFourFiredCount == 0 && tacticalFired > 0
                ? "stage4_policy_selection_gap_but_intended_profile_positive_witness_exists"
                : "positive_witness_observed";
        }

        if (evidence.StageFourEligibleCount + tacticalEligible == 0)
        {
            return "situation_not_actually_eligible";
        }

        if (evidence.TrackEvaluationCount > 0 && evidence.TrackAvailableCount == 0)
        {
            return "intended_formation_track_unavailable_or_pending";
        }

        if (evidence.PreviewFormationDecisionCount == 0)
        {
            return "policy_did_not_select_visible_formation_response";
        }

        if (evidence.PreviewEvidenceSupportedCount > 0)
        {
            return "eligible_after_visible_policy_choice_but_channel_did_not_fire";
        }

        return "eligible_context_not_converted_to_channel_witness";
    }

    private static GroupDelta[] GroupDeltas(
        IReadOnlyList<PlacementAttributionBattleRecord> rows,
        Func<PlacementAttributionBattleRecord, string> keySelector,
        int anchorId)
        => rows.GroupBy(keySelector, StringComparer.Ordinal)
            .Select(group =>
            {
                var used = group.Where(value => value.AnchorIdsByMemberIndex.Contains(anchorId)).ToArray();
                var unused = group.Where(value => !value.AnchorIdsByMemberIndex.Contains(anchorId)).ToArray();
                return used.Length == 0 || unused.Length == 0
                    ? null
                    : new GroupDelta(group.Key, WinRate(used) - WinRate(unused));
            })
            .Where(value => value != null)
            .Cast<GroupDelta>()
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();

    private static void RequirePairedControls(
        string pairingId,
        PlacementAttributionBattleRecord baseline,
        PlacementAttributionBattleRecord candidate)
    {
        var mismatches = new List<string>();
        RequireEqual("run_id", baseline.RunId, candidate.RunId);
        RequireEqual("comparison_kind", baseline.ComparisonKind, candidate.ComparisonKind);
        RequireEqual("composition_id", baseline.CompositionId, candidate.CompositionId);
        RequireEqual("concept_variant_id", baseline.ConceptVariantId, candidate.ConceptVariantId);
        RequireEqual("encounter_family_id", baseline.EncounterFamilyId, candidate.EncounterFamilyId);
        RequireEqual("scenario_id", baseline.ScenarioId, candidate.ScenarioId);
        if (baseline.Seed != candidate.Seed)
        {
            mismatches.Add("seed");
        }

        if (baseline.BattleSeed != candidate.BattleSeed)
        {
            mismatches.Add("battle_seed");
        }

        if (Math.Abs(baseline.FixedStepSeconds - candidate.FixedStepSeconds) > Epsilon)
        {
            mismatches.Add("fixed_step_seconds");
        }

        if (string.Equals(
                baseline.PlacementVariantId,
                candidate.PlacementVariantId,
                StringComparison.Ordinal))
        {
            mismatches.Add("placement_variant_id_not_distinct");
        }

        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                $"Placement pair control mismatch ({pairingId}): {string.Join(",", mismatches.OrderBy(value => value, StringComparer.Ordinal))}");
        }

        void RequireEqual(string field, string left, string right)
        {
            if (!string.Equals(left, right, StringComparison.Ordinal))
            {
                mismatches.Add(field);
            }
        }
    }

    private static int ChannelCount(PlacementAttributionBattleRecord record, string channelId)
        => record.Channels.FirstOrDefault(value => string.Equals(value.ChannelId, channelId, StringComparison.Ordinal))?.EventCount ?? 0;

    private static bool ChannelEligible(PlacementAttributionBattleRecord record, string channelId)
        => record.Channels.FirstOrDefault(value => string.Equals(value.ChannelId, channelId, StringComparison.Ordinal))?.Eligible ?? false;

    private static double ContactTime(PlacementAttributionBattleRecord record)
        => record.Trace.FirstContactTick < 0 ? -1d : record.Trace.FirstContactTick * record.FixedStepSeconds;

    private static double DifferenceWithMissing(double candidate, double baseline)
        => candidate < 0d && baseline < 0d
            ? 0d
            : candidate < 0d || baseline < 0d
                ? FirstContactDistanceDelta
                : candidate - baseline;

    private static double WinRate(IReadOnlyList<PlacementAttributionBattleRecord> rows)
        => rows.Count == 0 ? 0d : rows.Average(value => WinValue(value.WinnerSide));

    private static double WinValue(string winnerSide)
        => string.Equals(winnerSide, "ally", StringComparison.Ordinal) ? 1d : 0d;

    private static double Weight(PlacementAttributionReport.PairAttributionRecord value)
        => value.WinnerChanged ? 1d : Math.Abs(value.NormalizedPowerDelta);

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0d;
        }

        var position = (sorted.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        var value = lower == upper
            ? sorted[lower]
            : sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
        return Round(value);
    }

    private static double Round(double value)
        => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static string Format(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);

    private sealed record PairEvaluation(
        PlacementAttributionReport.PairAttributionRecord Record,
        PlacementAttributionBattleRecord Baseline,
        PlacementAttributionBattleRecord Candidate);

    private sealed record Classification(string Component, bool PlayerVisibleExplainable, string Explanation);
    private sealed record SemanticGroup(string EncounterFamilyId, int SeedCount, double AverageWinDelta, bool RepeatedReversal);
    private sealed record GroupDelta(string Key, double Delta);
    private sealed record ComparatorResult(bool NonWorse, bool StrictlyBetter);
}
