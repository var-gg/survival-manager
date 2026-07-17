using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>E05 track, E06 preview policy, Stage 4 competent 결과를 formation channel별로 조인한 입력.</summary>
public sealed record FormationOptionAttributionEvidence(
    string ChannelId,
    IReadOnlyList<string> IntendedProfileIds,
    int StageFourEligibleCount,
    int StageFourFiredCount,
    int TrackVariantCount,
    int TrackEvaluationCount,
    int TrackAvailableCount,
    int PolicyRealizedCount,
    int GenericPayoffWitnessCount,
    int PreviewFormationDecisionCount,
    int PreviewEvidenceSupportedCount)
{
    public static FormationOptionAttributionEvidence Empty(string channelId)
        => new(
            channelId,
            Array.Empty<string>(),
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
}
