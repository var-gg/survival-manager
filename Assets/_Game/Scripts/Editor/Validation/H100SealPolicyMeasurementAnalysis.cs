using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.Editor.Validation;

internal static class H100SealPolicyMeasurementAnalysis
{
    private static readonly double[] WidthMultipliers =
        { 1d, 1.25d, 1.5d, 2d, 3d, 4d, 6d, 8d };

    internal static IReadOnlyList<H100SealPolicyCalibration> CalibrationGrid()
    {
        var result = new List<H100SealPolicyCalibration>(27);
        foreach (var threshold in new[] { 0.50d, 0.70d, 0.85d })
        {
            foreach (var floor in new[] { 0.00d, 0.01d, 0.05d })
            {
                foreach (var baseline in new[] { 0.40d, 0.50d, 0.60d })
                {
                    result.Add(new H100SealPolicyCalibration(
                        threshold,
                        floor,
                        baseline));
                }
            }
        }

        return result;
    }

    internal static H100SealRefitWindowCensus BuildCensus(
        H100SealPolicyArmReport noSeal,
        int seedBase,
        int seedCount)
    {
        var windows = noSeal.RefitWindows;
        var byCampaign = windows
            .GroupBy(value => value.CampaignIndex)
            .ToDictionary(group => group.Key, group => group.Count());
        var campaignSeeds = noSeal.Campaigns
            .Select((campaign, index) => new
            {
                Index = ParseCampaignIndex(campaign.CampaignId, index),
                campaign.Seed,
            })
            .ToDictionary(value => value.Index, value => value.Seed);
        var windowsPerCampaign = Enumerable.Range(0, seedCount)
            .Select(index => new H100SealCampaignWindowCount(
                index,
                seedBase + index,
                campaignSeeds.GetValueOrDefault(
                    index,
                    H100SessionDriver.DeriveSeed("campaign", seedBase + index)),
                byCampaign.GetValueOrDefault(index)))
            .ToArray();
        var qualities = windows
            .SelectMany(value => value.CandidateAffixes)
            .Select(value => value.RollQuality)
            .ToArray();
        var qualityVectors = windows
            .Where(value => value.CandidateAffixes.Count != 0)
            .Select(value => string.Join(
                "|",
                value.CandidateAffixes
                    .OrderBy(affix => affix.SlotIndex)
                    .Select(affix => affix.RollQuality.ToString(
                        "R",
                        CultureInfo.InvariantCulture))))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var distinctVisibleAffixes = windows
            .SelectMany(window => window.VisibleInventoryItems.SelectMany(item =>
                item.Affixes.Select(affix => new
                {
                    window.CampaignIndex,
                    item.ItemInstanceId,
                    affix.AffixId,
                    affix.RollQuality,
                })))
            .GroupBy(
                value => new
                {
                    value.CampaignIndex,
                    value.ItemInstanceId,
                    value.AffixId,
                })
            .Select(group => group.First().RollQuality)
            .ToArray();
        var zeroCandidateAffixWindows = windows
            .Where(value => value.CandidateAffixCount == 0)
            .ToArray();
        var campaignsWithWindows = windows
            .Select(value => value.CampaignIndex)
            .Distinct()
            .Count();
        var inadequacy = new List<string>();
        if (campaignsWithWindows < 24)
        {
            inadequacy.Add(
                $"campaigns_with_windows={campaignsWithWindows.ToString(CultureInfo.InvariantCulture)}<24");
        }

        if (windows.Count < 64)
        {
            inadequacy.Add(
                $"total_windows={windows.Count.ToString(CultureInfo.InvariantCulture)}<64");
        }

        if (qualities.Length < 64)
        {
            inadequacy.Add(
                $"candidate_affix_observations={qualities.Length.ToString(CultureInfo.InvariantCulture)}<64");
        }

        if (qualityVectors < 16)
        {
            inadequacy.Add(
                $"distinct_quality_vectors={qualityVectors.ToString(CultureInfo.InvariantCulture)}<16");
        }

        return new H100SealRefitWindowCensus(
            windowsPerCampaign,
            campaignsWithWindows,
            windows.Count,
            qualities.Length,
            qualityVectors,
            distinctVisibleAffixes.Length,
            Quantiles(windows.Select(value => (double)value.WalletGold)),
            Quantiles(windows.Select(value => (double)value.WalletEcho)),
            windows.GroupBy(value => value.CandidateAffixCount)
                .OrderBy(group => group.Key)
                .Select(group => new H100SealCountFrequency(group.Key, group.Count()))
                .ToArray(),
            windows.Count(value => value.CandidateAllowsSeal),
            windows.Sum(value => value.AffordableSealQuoteCount),
            Quantiles(qualities),
            Histogram(qualities),
            qualities.Length == 0
                ? 0d
                : (double)qualities.Count(value => value >= 0.70d) / qualities.Length,
            qualities.Length == 0 ? null : qualities.Max(),
            Quantiles(distinctVisibleAffixes),
            Histogram(distinctVisibleAffixes),
            distinctVisibleAffixes.Length == 0 ? null : distinctVisibleAffixes.Max(),
            zeroCandidateAffixWindows.Length,
            zeroCandidateAffixWindows.Count(window =>
                window.VisibleInventoryItems.Any(item => item.AffixCount >= 2)),
            zeroCandidateAffixWindows.Count(window =>
                window.VisibleInventoryItems.Any(item =>
                    item.AffixCount >= 2 && item.HasLegalRefitSlot)),
            zeroCandidateAffixWindows.Count(window =>
                window.VisibleInventoryItems.Any(item =>
                    item.AffixCount >= 2
                    && item.HasLegalRefitSlot
                    && item.PlainRefitAffordable)),
            AverageOrNull(windows
                .Where(value => value.CandidateSelectionBias.HasValue)
                .Select(value => value.CandidateSelectionBias!.Value)),
            inadequacy.Count == 0,
            inadequacy);
    }

    internal static H100SealPolicySweepResult BuildSweepResult(
        H100SealPolicyArmReport arm,
        H100SealPolicyArmReport noSeal)
    {
        var calibration = arm.Calibration
                          ?? throw new InvalidOperationException(
                              "A policy sweep arm requires calibration metadata.");
        var baselineTerminals = noSeal.TerminalCampaigns.ToDictionary(
            value => value.CampaignIndex);
        var pairs = arm.TerminalCampaigns
            .Where(value => value.Observed
                            && value.Gold.HasValue
                            && value.Echo.HasValue
                            && value.InventoryMeanRollQuality.HasValue
                            && baselineTerminals.TryGetValue(
                                value.CampaignIndex,
                                out var baseline)
                            && baseline.Observed
                            && baseline.Gold.HasValue
                            && baseline.Echo.HasValue
                            && baseline.InventoryMeanRollQuality.HasValue)
            .Select(value =>
            {
                var baseline = baselineTerminals[value.CampaignIndex];
                return new TerminalPair(value, baseline);
            })
            .ToArray();
        var currencyDelta = AverageOrNull(pairs.Select(value =>
            (double)(value.Arm.Echo!.Value - value.Baseline.Echo!.Value)));
        var goldDelta = AverageOrNull(pairs.Select(value =>
            (double)(value.Arm.Gold!.Value - value.Baseline.Gold!.Value)));
        var qualityDelta = AverageOrNull(pairs.Select(value =>
            value.Arm.InventoryMeanRollQuality!.Value
            - value.Baseline.InventoryMeanRollQuality!.Value));
        var completedDelta = CompletedCount(arm) - CompletedCount(noSeal);
        var outcomeDelta = Rate(
                               CompletedCount(arm),
                               arm.Campaigns.Count)
                           - Rate(
                               CompletedCount(noSeal),
                               noSeal.Campaigns.Count);
        var crashDelta = arm.Campaigns.Sum(value => value.CrashCount)
                         - noSeal.Campaigns.Sum(value => value.CrashCount);
        var truncationDelta = arm.Campaigns.Count(value => value.Truncated)
                              - noSeal.Campaigns.Count(value => value.Truncated);
        var factAuditPassed = FactAuditPassed(arm);
        var missingPairs = Math.Max(
            arm.TerminalCampaigns.Count,
            noSeal.TerminalCampaigns.Count) - pairs.Length;
        var meetsH2 = arm.CampaignsWithSeal >= 4
                      && qualityDelta > 0d
                      && outcomeDelta >= 0d
                      && crashDelta <= 0
                      && truncationDelta <= 0
                      && factAuditPassed
                      && goldDelta == 0d
                      && missingPairs <= 4;
        return new H100SealPolicySweepResult(
            calibration,
            arm.RefitWindowCount,
            arm.SealCount,
            arm.CampaignsWithSeal,
            Rate(arm.SealCount, arm.RefitWindowCount),
            currencyDelta,
            goldDelta,
            arm.CraftingEchoSpent - noSeal.CraftingEchoSpent,
            qualityDelta,
            outcomeDelta,
            completedDelta,
            arm.Campaigns.Sum(value => value.SiteCount)
            - noSeal.Campaigns.Sum(value => value.SiteCount),
            arm.Campaigns.Sum(value => value.BattleCount)
            - noSeal.Campaigns.Sum(value => value.BattleCount),
            arm.Campaigns.Sum(value => value.WinCount)
            - noSeal.Campaigns.Sum(value => value.WinCount),
            arm.Campaigns.Sum(value => value.LossCount)
            - noSeal.Campaigns.Sum(value => value.LossCount),
            crashDelta,
            truncationDelta,
            pairs.Length,
            missingPairs,
            factAuditPassed,
            meetsH2);
    }

    internal static H100SealH2Verdict BuildH2Verdict(
        H100SealRefitWindowCensus census,
        IReadOnlyList<H100SealPolicySweepResult> sweep)
    {
        var qualifying = sweep
            .Where(value => value.MeetsH2Rule)
            .OrderByDescending(value => value.OutcomeDelta)
            .ThenByDescending(value => value.RollQualityDelta)
            .ThenByDescending(value => value.CurrencyDelta)
            .ThenBy(value => value.Calibration.Threshold)
            .ThenBy(value => value.Calibration.NetValueFloor)
            .ThenBy(value => value.Calibration.Baseline)
            .FirstOrDefault();
        var terminalAdequate = sweep.All(value => value.MissingTerminalPairCount <= 4);
        var underpoweredHelpfulCalibration = sweep.Any(value =>
            value.CampaignsWithSeal is > 0 and < 4
            && value.RollQualityDelta > 0d
            && value.OutcomeDelta >= 0d
            && value.CrashDelta <= 0
            && value.TruncationDelta <= 0
            && value.FactAuditPassed
            && value.TerminalGoldDelta == 0d
            && value.MissingTerminalPairCount <= 4);
        var insufficient = !census.DataAdequate
                           || !terminalAdequate
                           || underpoweredHelpfulCalibration;
        var anySeals = sweep.Any(value => value.SealCount != 0);
        var ruledOut = qualifying == null && !insufficient;
        var achieved = qualifying == null
            ? anySeals
                ? "At least one calibration selected Seal, but none met the preregistered paired help rule."
                : "No preregistered calibration selected Seal."
            : $"seal_frequency={qualifying.SealFrequency:R};"
              + $"campaigns_with_seal={qualifying.CampaignsWithSeal};"
              + $"echo_delta={Format(qualifying.CurrencyDelta)};"
              + $"roll_quality_delta={Format(qualifying.RollQualityDelta)};"
              + $"completion_rate_delta={qualifying.OutcomeDelta:R}";
        return new H100SealH2Verdict(
            anySeals,
            qualifying?.Calibration,
            achieved,
            ruledOut,
            insufficient);
    }

    internal static H100SealWidthProbeReport BuildWidthProbe(
        H100SealPolicyArmReport noSeal,
        IReadOnlyList<H100SealPolicySweepResult> sweep,
        H100SealH2Verdict h2)
    {
        if (!h2.RuledOut)
        {
            return new H100SealWidthProbeReport(
                false,
                h2.BestSetting != null
                    ? "Skipped because the policy sweep supports H2."
                    : "Skipped because H2 could not be ruled out with adequate data.",
                null,
                null,
                "Conditional preregistered in-memory width probe.",
                true,
                Array.Empty<H100SealWidthProbePoint>());
        }

        var points = WidthMultipliers.Select(multiplier =>
        {
            var maximumError = 0d;
            foreach (var affix in noSeal.RefitWindows.SelectMany(value =>
                         value.CandidateAffixes))
            {
                var width = affix.ValueMax - (double)affix.ValueMin;
                if (width <= 0d)
                {
                    continue;
                }

                var midpoint = (affix.ValueMin + (double)affix.ValueMax) / 2d;
                var expandedWidth = width * multiplier;
                var expandedMin = midpoint - (expandedWidth / 2d);
                var expandedMagnitude =
                    expandedMin + (affix.RollQuality * expandedWidth);
                var recomputedQuality =
                    (expandedMagnitude - expandedMin) / expandedWidth;
                maximumError = Math.Max(
                    maximumError,
                    Math.Abs(recomputedQuality - affix.RollQuality));
            }

            return new H100SealWidthProbePoint(
                multiplier,
                maximumError,
                maximumError > 1e-12d,
                sweep.Count == 0 ? 0d : sweep.Max(value => value.SealFrequency));
        }).ToArray();
        var changed = points.FirstOrDefault(value => value.DecisionSurfaceChanged);
        return new H100SealWidthProbeReport(
            true,
            changed == null
                ? "No multiplier through 8.0 changed normalized roll quality or the policy decision surface."
                : "At least one multiplier changed the reconstructed normalized-quality surface.",
            null,
            null,
            "For each observed non-degenerate affix range, multiply width about its midpoint, preserve the observed roll position, reconstruct magnitude, and re-normalize. The current policy consumes this normalized position, so unchanged positions imply unchanged decisions.",
            true,
            points);
    }

    internal static H100SealMeasurementConclusion BuildConclusion(
        H100SealRefitWindowCensus census,
        H100SealH2Verdict h2,
        H100SealWidthProbeReport widthProbe)
    {
        if (!census.DataAdequate || h2.InsufficientData)
        {
            return new H100SealMeasurementConclusion(
                "none",
                "low",
                true,
                "Run a larger preregistered paired sample after resolving the stated adequacy failures.");
        }

        if (h2.BestSetting != null)
        {
            return new H100SealMeasurementConclusion(
                "H2",
                "moderate",
                false,
                "Confirm the selected calibration in an independent held-out seed panel before shipping any constant.");
        }

        if (widthProbe.MultiplierNeeded.HasValue
            && census.FractionAtOrAbove070 < 0.05d)
        {
            return new H100SealMeasurementConclusion(
                "H1",
                "moderate",
                false,
                "Validate the width multiplier on a held-out in-memory content panel before any asset change.");
        }

        return new H100SealMeasurementConclusion(
            "neither",
            "moderate",
            false,
            "A follow-up must define an absolute-magnitude-to-Echo utility model or isolate the remaining opportunity, affordability, item-structure, and campaign-leverage confounds.");
    }

    internal static IReadOnlyList<string> BuildSurprises(
        H100SealRefitWindowCensus census,
        H100SealH2Verdict h2,
        H100SealWidthProbeReport widthProbe)
    {
        var surprises = new List<string>();
        var medianWindows = Quantiles(census.WindowsPerCampaign.Select(value =>
            (double)value.WindowCount)).P50;
        if (medianWindows <= 4d)
        {
            surprises.Add(
                $"Median refit windows per campaign was {Format(medianWindows)}, so opportunity count remains a material constraint.");
        }

        var singleAffixWindows = census.AffixesPerCandidateItem
            .Where(value => value.Value < 2)
            .Sum(value => value.Count);
        if (singleAffixWindows != 0)
        {
            surprises.Add(
                $"{singleAffixWindows.ToString(CultureInfo.InvariantCulture)} refit windows had fewer than two candidate affixes, making Seal structurally unavailable.");
        }

        if (widthProbe.Ran
            && widthProbe.Points.All(value => !value.DecisionSurfaceChanged))
        {
            surprises.Add(
                "Authored range-width multiplication left normalized roll quality and the current Seal decision surface invariant.");
        }

        if (h2.AnyReasonableCalibrationSeals && h2.BestSetting == null)
        {
            surprises.Add(
                "Some calibrations selected Seal, but none satisfied the preregistered paired help rule.");
        }

        return surprises;
    }

    internal static H100SealQuantiles Quantiles(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        return new H100SealQuantiles(
            Quantile(ordered, 0d),
            Quantile(ordered, 0.10d),
            Quantile(ordered, 0.25d),
            Quantile(ordered, 0.50d),
            Quantile(ordered, 0.75d),
            Quantile(ordered, 0.90d),
            Quantile(ordered, 1d));
    }

    private static IReadOnlyList<H100SealHistogramBin> Histogram(
        IReadOnlyList<double> values)
    {
        var result = new H100SealHistogramBin[10];
        for (var index = 0; index < result.Length; index++)
        {
            var lower = index / 10d;
            var upper = (index + 1) / 10d;
            var upperInclusive = index == result.Length - 1;
            var count = values.Count(value =>
                value >= lower
                && (upperInclusive ? value <= upper : value < upper));
            result[index] = new H100SealHistogramBin(
                lower,
                upper,
                upperInclusive,
                count);
        }

        return result;
    }

    private static double? Quantile(IReadOnlyList<double> values, double probability)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var position = probability * (values.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return values[lower];
        }

        var fraction = position - lower;
        return values[lower] + ((values[upper] - values[lower]) * fraction);
    }

    private static bool FactAuditPassed(H100SealPolicyArmReport arm)
        => arm.FactAudit.PostDecisionInformationReferenceCount == 0
           && arm.FactAudit.NonUiSemanticInternalFieldReferenceCount == 0
           && arm.FactAudit.OracleOrTruthLeakCount == 0
           && arm.FactAudit.UnsupportedCertainClaimCount == 0;

    private static int CompletedCount(H100SealPolicyArmReport arm)
        => arm.Campaigns.Count(value => value.Completed);

    private static double Rate(int numerator, int denominator)
        => denominator == 0 ? 0d : (double)numerator / denominator;

    private static double? AverageOrNull(IEnumerable<double> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 ? null : materialized.Average();
    }

    private static int ParseCampaignIndex(string campaignId, int fallback)
    {
        const string prefix = "campaign-";
        return campaignId.StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(
                   campaignId.Substring(prefix.Length),
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out var parsed)
            ? parsed
            : fallback;
    }

    private static string Format(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";

    private sealed record TerminalPair(
        H100SealCampaignTerminalRecord Arm,
        H100SealCampaignTerminalRecord Baseline);
}
