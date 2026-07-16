using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>policy-choice 전투만 집계한 진형 prevalence/impact/legibility 요약.</summary>
public sealed record FormationPolicySummary(
    string PolicyId,
    int BattleCount,
    int NontrivialBattleCount,
    int AnyFormationBattleCount,
    int CausalBattleCount,
    double PrevalenceRate,
    double ImpactRate,
    double LegibleRate,
    IReadOnlyList<FormationPolicySummary.ChannelSummary> Channels)
{
    public sealed record ChannelSummary(
        string ChannelId,
        int EligibleCount,
        int FiredCount,
        int CausalCount,
        int LegibleCount,
        double EligibleFireRate,
        double EligibleCausalRate,
        double FiredLegibleRate);
}
