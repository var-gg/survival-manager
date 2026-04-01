using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record RetrainResult(
    string FlexActiveId,
    string FlexPassiveId,
    UnitRetrainState RetrainState,
    int EchoCost,
    bool IsPlanCoherent,
    bool UsedPity);

public static class RetrainService
{
    public static RetrainResult Retrain(
        CombatArchetypeTemplate template,
        string currentFlexActiveId,
        string currentFlexPassiveId,
        RetrainOperationKind operation,
        UnitRetrainState state,
        TeamPlanProfile plan,
        RetrainCostTable costTable,
        int seed)
    {
        var activePool = RecruitmentTemplateResolver.GetFlexActivePool(template);
        var passivePool = RecruitmentTemplateResolver.GetFlexPassivePool(template);
        var cost = costTable.GetTotalCost(operation, state);

        var allowPlanPity = state.ConsecutivePlanIncoherentRetrains >= 2;
        var usedPity = false;
        var validPairs = BuildValidPairs(template, activePool, passivePool, currentFlexActiveId, currentFlexPassiveId, state, operation);
        var coherentPairs = validPairs
            .Where(pair =>
                RecruitmentTemplateResolver.IsPlanCoherent(template, pair.Active, plan)
                || RecruitmentTemplateResolver.IsPlanCoherent(template, pair.Passive, plan))
            .ToList();
        if (allowPlanPity && coherentPairs.Count > 0)
        {
            validPairs = coherentPairs;
            usedPity = true;
        }

        if (validPairs.Count == 0)
        {
            throw new InvalidOperationException($"Retrain candidate pool is empty for archetype '{template.Id}'.");
        }

        var random = new Random(seed);
        var chosen = validPairs[random.Next(validPairs.Count)];
        var isPlanCoherent =
            RecruitmentTemplateResolver.IsPlanCoherent(template, chosen.Active, plan)
            || RecruitmentTemplateResolver.IsPlanCoherent(template, chosen.Passive, plan);
        var updatedState = state.Clone();
        updatedState.RetrainCount++;
        updatedState.TotalEchoSpent += cost;
        updatedState.PreviousFlexActiveId = currentFlexActiveId;
        updatedState.PreviousFlexPassiveId = currentFlexPassiveId;
        if (isPlanCoherent)
        {
            updatedState.ConsecutivePlanIncoherentRetrains = 0;
        }
        else if (allowPlanPity && coherentPairs.Count == 0)
        {
            updatedState.ConsecutivePlanIncoherentRetrains = state.ConsecutivePlanIncoherentRetrains;
        }
        else
        {
            updatedState.ConsecutivePlanIncoherentRetrains++;
        }

        return new RetrainResult(
            chosen.Active.Id,
            chosen.Passive.Id,
            updatedState,
            cost,
            isPlanCoherent,
            usedPity);
    }

    private static List<(BattleSkillSpec Active, BattleSkillSpec Passive)> BuildValidPairs(
        CombatArchetypeTemplate template,
        IReadOnlyList<BattleSkillSpec> activePool,
        IReadOnlyList<BattleSkillSpec> passivePool,
        string currentFlexActiveId,
        string currentFlexPassiveId,
        UnitRetrainState state,
        RetrainOperationKind operation)
    {
        var activeCandidates = operation switch
        {
            RetrainOperationKind.RerollFlexPassive => activePool.Where(option => string.Equals(option.Id, currentFlexActiveId, StringComparison.Ordinal)).ToList(),
            _ => activePool.Where(option =>
                    !string.Equals(option.Id, currentFlexActiveId, StringComparison.Ordinal)
                    && !string.Equals(option.Id, state.PreviousFlexActiveId, StringComparison.Ordinal))
                .ToList(),
        };
        var passiveCandidates = operation switch
        {
            RetrainOperationKind.RerollFlexActive => passivePool.Where(option => string.Equals(option.Id, currentFlexPassiveId, StringComparison.Ordinal)).ToList(),
            _ => passivePool.Where(option =>
                    !string.Equals(option.Id, currentFlexPassiveId, StringComparison.Ordinal)
                    && !string.Equals(option.Id, state.PreviousFlexPassiveId, StringComparison.Ordinal))
                .ToList(),
        };

        var valid = new List<(BattleSkillSpec Active, BattleSkillSpec Passive)>();
        foreach (var active in activeCandidates)
        {
            foreach (var passive in passiveCandidates)
            {
                if (IsInvalidPair(template, active, passive))
                {
                    continue;
                }

                if (!RecruitmentTemplateResolver.IsNativeCoherent(template, active)
                    && !RecruitmentTemplateResolver.IsNativeCoherent(template, passive))
                {
                    continue;
                }

                valid.Add((active, passive));
            }
        }

        return valid;
    }

    private static bool IsInvalidPair(
        CombatArchetypeTemplate template,
        BattleSkillSpec active,
        BattleSkillSpec passive)
    {
        if (RecruitmentTemplateResolver.HasSameFamilyConflict(active, template.SignatureActive, template.SignaturePassive)
            || RecruitmentTemplateResolver.HasSameFamilyConflict(passive, template.SignatureActive, template.SignaturePassive))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(active.MutuallyExclusiveGroupId)
            && string.Equals(active.MutuallyExclusiveGroupId, passive.MutuallyExclusiveGroupId, StringComparison.Ordinal))
        {
            return true;
        }

        return RecruitmentTemplateResolver.IsBannedPairing(template, active.Id, passive.Id);
    }
}
