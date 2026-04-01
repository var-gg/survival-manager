using System.Collections.Generic;

namespace SM.Combat.Model;

public enum CounterCoverageLevelValue
{
    None = 0,
    Light = 1,
    Standard = 2,
    Strong = 3,
}

public sealed record CompiledCounterToolContribution(
    string Tool,
    int Strength);

public sealed record ContentGovernanceSummary(
    string Rarity,
    string PowerBand,
    string RoleProfile,
    int BudgetFinalScore,
    IReadOnlyList<string> DeclaredThreatPatterns,
    IReadOnlyList<CompiledCounterToolContribution> DeclaredCounterTools,
    string DeclaredFeatureFlags);

public sealed class TeamCounterCoverageReport
{
    public CounterCoverageLevelValue ArmorShred = CounterCoverageLevelValue.None;
    public CounterCoverageLevelValue Exposure = CounterCoverageLevelValue.None;
    public CounterCoverageLevelValue GuardBreakMultiHit = CounterCoverageLevelValue.None;
    public CounterCoverageLevelValue TrackingArea = CounterCoverageLevelValue.None;
    public CounterCoverageLevelValue TenacityStability = CounterCoverageLevelValue.None;
    public CounterCoverageLevelValue AntiHealShatter = CounterCoverageLevelValue.None;
    public CounterCoverageLevelValue InterceptPeel = CounterCoverageLevelValue.None;
    public CounterCoverageLevelValue CleaveWaveclear = CounterCoverageLevelValue.None;

    public CounterCoverageLevelValue GetLevel(string tool)
    {
        return tool switch
        {
            "ArmorShred" => ArmorShred,
            "Exposure" => Exposure,
            "GuardBreakMultiHit" => GuardBreakMultiHit,
            "TrackingArea" => TrackingArea,
            "TenacityStability" => TenacityStability,
            "AntiHealShatter" => AntiHealShatter,
            "InterceptPeel" => InterceptPeel,
            "CleaveWaveclear" => CleaveWaveclear,
            _ => CounterCoverageLevelValue.None,
        };
    }

    public void SetLevel(string tool, CounterCoverageLevelValue level)
    {
        switch (tool)
        {
            case "ArmorShred":
                ArmorShred = level;
                break;
            case "Exposure":
                Exposure = level;
                break;
            case "GuardBreakMultiHit":
                GuardBreakMultiHit = level;
                break;
            case "TrackingArea":
                TrackingArea = level;
                break;
            case "TenacityStability":
                TenacityStability = level;
                break;
            case "AntiHealShatter":
                AntiHealShatter = level;
                break;
            case "InterceptPeel":
                InterceptPeel = level;
                break;
            case "CleaveWaveclear":
                CleaveWaveclear = level;
                break;
        }
    }
}
