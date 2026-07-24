using System;
using System.Collections.Generic;

namespace SM.Meta.Services;

/// <summary>
/// Natural generated-affix graph를 exact terminal score에 조건부로 순회한다.
/// A1에서는 production caller가 없으며 A2 item-level Refit이 이 API를 조립한다.
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
