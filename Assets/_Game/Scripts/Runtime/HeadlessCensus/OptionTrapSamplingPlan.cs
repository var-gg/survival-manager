namespace SM.HeadlessCensus;

public sealed record OptionTrapSamplingPlan(
    int SeedBase,
    int SeedCount,
    int MedoidPlacementCount,
    int HealthySampleCount,
    int FullCensusPlacementCount,
    string HealthySamplingMethod,
    string IntendedContextRule,
    string FullCensusRule);
