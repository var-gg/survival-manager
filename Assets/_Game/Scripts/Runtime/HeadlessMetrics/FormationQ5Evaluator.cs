using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>Pro Q5의 small-N diagnostic threshold를 Coverage와 Competent에 분리 적용한다.</summary>
public static class FormationQ5Evaluator
{
    private const double MinimumCompetentPrevalence = 0.60d;
    private const double MinimumEligibleChannelFireRate = 0.20d;
    private const double MinimumCompetentImpact = 0.35d;
    private const double MinimumPlacementMedian = 0.05d;
    private const double MaximumPlacementMedian = 0.15d;
    private const double MinimumSensitiveMedian = 0.08d;
    private const double MaximumSensitiveMedian = 0.20d;
    private const double MaximumPlacementP90 = 0.30d;
    private const double MaximumDefaultOptimalRate = 0.60d;

    public static FormationEvaluationReport Evaluate(
        string runId,
        string coveragePolicyId,
        string competentPolicyId,
        FormationCausalEvaluator.Result causal,
        PlacementLeverageEvaluator.Result placement,
        HealerMarginalValueEvaluator.Result healer)
    {
        var coverage = causal.PolicySummaries.FirstOrDefault(summary =>
            string.Equals(summary.PolicyId, coveragePolicyId, StringComparison.Ordinal));
        var competent = causal.PolicySummaries.FirstOrDefault(summary =>
            string.Equals(summary.PolicyId, competentPolicyId, StringComparison.Ordinal));
        var coveragePass = coverage != null
                           && FormationChannelIds.All.All(channelId =>
                               coverage.Channels.Single(channel => channel.ChannelId == channelId).FiredCount > 0);
        var channelPrevalencePass = competent != null
                                    && FormationChannelIds.All.All(channelId =>
                                    {
                                        var channel = competent.Channels.Single(value => value.ChannelId == channelId);
                                        return channel.EligibleCount > 0
                                               && channel.EligibleFireRate >= MinimumEligibleChannelFireRate;
                                    });
        var prevalencePass = competent != null
                             && competent.PrevalenceRate >= MinimumCompetentPrevalence
                             && channelPrevalencePass;
        var impactPass = competent != null && competent.ImpactRate >= MinimumCompetentImpact;
        var legibilityPass = competent != null && competent.LegibleRate >= 0.999999d;
        var placementPass = placement.Records.Count > 0
                            && placement.MedianLeverage >= MinimumPlacementMedian
                            && placement.MedianLeverage <= MaximumPlacementMedian
                            && placement.SensitiveMedianLeverage >= MinimumSensitiveMedian
                            && placement.SensitiveMedianLeverage <= MaximumSensitiveMedian
                            && placement.LeverageP90 < MaximumPlacementP90
                            && placement.DefaultOptimalRate <= MaximumDefaultOptimalRate;
        var healerPass = healer.PositiveStateCount > 0
                         && healer.AlignedPositiveStateCount == healer.PositiveStateCount;
        var competentPass = prevalencePass && impactPass && legibilityPass && placementPass && healerPass;
        var tuning = ResolveTuningChannels(competent);

        return new FormationEvaluationReport
        {
            RunId = runId ?? string.Empty,
            CoveragePolicyId = coveragePolicyId ?? string.Empty,
            CompetentPolicyId = competentPolicyId ?? string.Empty,
            CausalMethod = FormationCausalEvaluator.CausalMethodId,
            CausalPrecisionNote = "best-effort v1: same seed full rerun with placement event-presence delta; subsystem tagged RNG ablation deferred",
            CoveragePass = coveragePass,
            CompetentPrevalencePass = prevalencePass,
            CompetentImpactPass = impactPass,
            CompetentLegibilityPass = legibilityPass,
            PlacementLeveragePass = placementPass,
            HealerSelectionPass = healerPass,
            CompetentQ5Pass = competentPass,
            NeedsStageFiveBalance = coveragePass && !competentPass,
            ChannelsNeedingTuning = tuning,
            PolicySummaries = causal.PolicySummaries,
            Placement = new FormationEvaluationReport.PlacementGateSummary
            {
                ComparisonSetCount = placement.Records.Count,
                MedianLeverage = placement.MedianLeverage,
                SensitiveMedianLeverage = placement.SensitiveMedianLeverage,
                LeverageP90 = placement.LeverageP90,
                DefaultOptimalRate = placement.DefaultOptimalRate,
            },
            Healer = new FormationEvaluationReport.HealerGateSummary
            {
                ComparisonCount = healer.Records.Count,
                PositiveStateCount = healer.PositiveStateCount,
                AlignedPositiveStateCount = healer.AlignedPositiveStateCount,
                PositiveSelectionAlignmentRate = healer.PositiveSelectionAlignmentRate,
            },
        };
    }

    private static IReadOnlyList<string> ResolveTuningChannels(FormationPolicySummary? competent)
    {
        if (competent == null)
        {
            return FormationChannelIds.All.ToArray();
        }

        return competent.Channels
            .Where(channel => channel.EligibleCount == 0
                              || channel.EligibleFireRate < MinimumEligibleChannelFireRate
                              || channel.EligibleCausalRate < MinimumCompetentImpact)
            .Select(channel => channel.ChannelId)
            .OrderBy(channelId => channelId, StringComparer.Ordinal)
            .ToArray();
    }
}
