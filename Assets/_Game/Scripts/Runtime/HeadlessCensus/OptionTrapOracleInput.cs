using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

public sealed record OptionTrapOracleInput(
    IReadOnlyList<OptionWitnessContract> Contracts,
    IReadOnlyList<OptionMechanicalWitness> MechanicalWitnesses,
    IReadOnlyList<OptionPairedCounterfactual> PairedCounterfactuals,
    IReadOnlyList<OptionContinuationComparison> ContinuationComparisons,
    OptionTrapSamplingPlan SamplingPlan)
{
    public static OptionTrapOracleInput Empty { get; } = new(
        Array.Empty<OptionWitnessContract>(),
        Array.Empty<OptionMechanicalWitness>(),
        Array.Empty<OptionPairedCounterfactual>(),
        Array.Empty<OptionContinuationComparison>(),
        new OptionTrapSamplingPlan(0, 0, 0, 0, 0, string.Empty, string.Empty, string.Empty));
}
