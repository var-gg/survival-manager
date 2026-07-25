using System;
using System.Collections.Generic;

namespace SM.Meta.Services;

/// <summary>
/// Natural generated-affix graph를 exact terminal score에 조건부로 순회한다.
/// Current magnitude Refit intentionally does not consume this identity-quality machinery.
/// Retain it for a future affix-identity reroll craft rather than treating it as dead code.
/// </summary>
public sealed class AffixQualityConditionedSelector
{
    public IReadOnlyList<string> SelectBudgetWeightedConditioned(
        AffixQualityProfile profile,
        int exactFinalScoreQ,
        int seed)
    {
        return SelectBudgetWeightedConditioned(
            profile,
            exactFinalScoreQ,
            new Random(seed));
    }

    public IReadOnlyList<string> SelectBudgetWeightedConditioned(
        AffixQualityProfile profile,
        int exactFinalScoreQ,
        Random random)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        return profile.CompiledGraph.SelectBudgetWeightedConditioned(
            exactFinalScoreQ,
            random);
    }
}
