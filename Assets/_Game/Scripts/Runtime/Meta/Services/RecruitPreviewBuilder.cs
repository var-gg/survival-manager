using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record FlexRollResult(
    string FlexActiveId,
    string FlexPassiveId);

public static class RecruitPreviewBuilder
{
    public static FlexRollResult Roll(
        CombatArchetypeTemplate template,
        TeamPlanProfile plan,
        ScoutDirective? directive,
        FlexRollBiasMode biasMode,
        int seed)
    {
        var activePool = RecruitmentTemplateResolver.GetFlexActivePool(template);
        var passivePool = RecruitmentTemplateResolver.GetFlexPassivePool(template);
        var validPairs = new List<(BattleSkillSpec Active, BattleSkillSpec Passive, int Weight)>();

        foreach (var active in activePool)
        {
            foreach (var passive in passivePool)
            {
                if (RecruitmentTemplateResolver.IsBannedPairing(template, active.Id, passive.Id))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(active.MutuallyExclusiveGroupId)
                    && string.Equals(active.MutuallyExclusiveGroupId, passive.MutuallyExclusiveGroupId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (RecruitmentTemplateResolver.HasSameFamilyConflict(active, template.SignatureActive, template.SignaturePassive)
                    || RecruitmentTemplateResolver.HasSameFamilyConflict(passive, template.SignatureActive, template.SignaturePassive))
                {
                    continue;
                }

                var nativeCoherent = RecruitmentTemplateResolver.IsNativeCoherent(template, active)
                                    || RecruitmentTemplateResolver.IsNativeCoherent(template, passive);
                if (!nativeCoherent)
                {
                    continue;
                }

                var weight = 1;
                if (RecruitmentTemplateResolver.IsNativeCoherent(template, active))
                {
                    weight += 2;
                }

                if (RecruitmentTemplateResolver.IsNativeCoherent(template, passive))
                {
                    weight += 2;
                }

                if (biasMode == FlexRollBiasMode.NativePlusPlanBiased
                    && (RecruitmentTemplateResolver.IsPlanCoherent(template, active, plan, directive)
                        || RecruitmentTemplateResolver.IsPlanCoherent(template, passive, plan, directive)))
                {
                    weight += 4;
                }

                validPairs.Add((active, passive, weight));
            }
        }

        if (validPairs.Count == 0)
        {
            if (activePool.Count > 0 && passivePool.Count > 0)
            {
                return new FlexRollResult(activePool[0].Id, passivePool[0].Id);
            }

            return new FlexRollResult(
                activePool.Count > 0 ? activePool[0].Id : string.Empty,
                passivePool.Count > 0 ? passivePool[0].Id : string.Empty);
        }

        var random = new Random(seed);
        var selectedIndex = SelectWeightedIndex(validPairs.Select(pair => pair.Weight).ToList(), random);
        var selected = validPairs[selectedIndex];
        return new FlexRollResult(selected.Active.Id, selected.Passive.Id);
    }

    private static int SelectWeightedIndex(IReadOnlyList<int> weights, Random random)
    {
        var total = weights.Sum(weight => Math.Max(1, weight));
        var roll = random.Next(total);
        var cursor = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            cursor += Math.Max(1, weights[i]);
            if (roll < cursor)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }
}
