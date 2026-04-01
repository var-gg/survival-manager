using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class CounterCoverageAggregationService
{
    public static TeamCounterCoverageReport AggregateSummaries(IEnumerable<ContentGovernanceSummary?> summaries)
    {
        return Aggregate(summaries);
    }

    public static TeamCounterCoverageReport AggregateFromLoadouts(IEnumerable<BattleUnitLoadout> units)
    {
        return Aggregate(units.SelectMany(EnumerateGovernance));
    }

    private static TeamCounterCoverageReport Aggregate(IEnumerable<ContentGovernanceSummary?> summaries)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ArmorShred"] = 0,
            ["Exposure"] = 0,
            ["GuardBreakMultiHit"] = 0,
            ["TrackingArea"] = 0,
            ["TenacityStability"] = 0,
            ["AntiHealShatter"] = 0,
            ["InterceptPeel"] = 0,
            ["CleaveWaveclear"] = 0,
        };

        foreach (var summary in summaries.Where(summary => summary != null))
        {
            foreach (var contribution in summary!.DeclaredCounterTools ?? Array.Empty<CompiledCounterToolContribution>())
            {
                if (totals.ContainsKey(contribution.Tool))
                {
                    totals[contribution.Tool] += contribution.Strength;
                }
            }
        }

        var report = new TeamCounterCoverageReport();
        foreach (var pair in totals)
        {
            report.SetLevel(pair.Key, ToCoverageLevel(pair.Value));
        }

        return report;
    }

    private static IEnumerable<ContentGovernanceSummary?> EnumerateGovernance(BattleUnitLoadout unit)
    {
        yield return unit.Governance;

        foreach (var skill in unit.Skills ?? Array.Empty<BattleSkillSpec>())
        {
            yield return skill.Governance;
        }

        yield return unit.SignaturePassive?.Governance;
        yield return unit.FlexPassive?.Governance;
        yield return unit.MobilityReaction?.Governance;
    }

    private static CounterCoverageLevelValue ToCoverageLevel(int strengthScore)
    {
        if (strengthScore >= 3)
        {
            return CounterCoverageLevelValue.Strong;
        }

        if (strengthScore >= 2)
        {
            return CounterCoverageLevelValue.Standard;
        }

        return strengthScore >= 1 ? CounterCoverageLevelValue.Light : CounterCoverageLevelValue.None;
    }
}
