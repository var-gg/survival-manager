using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class RecruitPackGenerator
{
    public static RecruitPackGenerationResult GeneratePack(
        IReadOnlyDictionary<string, CombatArchetypeTemplate> archetypes,
        CombatContentSnapshot snapshot,
        IReadOnlyList<HeroRecord> roster,
        IReadOnlyList<string> temporaryAugmentIds,
        IReadOnlyList<string> permanentAugmentIds,
        RecruitPityState pityState,
        RecruitPhaseState phaseState,
        int seed)
    {
        var pool = RecruitmentTemplateResolver.GetRecruitPool(archetypes, roster);
        var plan = TeamPlanEvaluator.Evaluate(roster, archetypes, snapshot, temporaryAugmentIds, permanentAugmentIds);
        var scoutDirective = phaseState.PendingScoutDirective?.Clone() ?? new ScoutDirective();
        var evaluations = pool
            .Select(template => (Template: template, Evaluation: RecruitCandidateScoringService.Evaluate(template, plan, scoutDirective, roster)))
            .ToList();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        var random = new Random(seed);
        var protectedFloor = ResolveProtectedFloor(pityState);

        var standardA = SelectWeighted(evaluations, selectedIds, RecruitOfferSlotType.StandardA, protectedFloor, random);
        var standardB = SelectWeighted(evaluations, selectedIds, RecruitOfferSlotType.StandardB, protectedFloor, random);
        var onPlan = SelectOnPlan(evaluations, selectedIds, protectedFloor, random);
        var protectedSlot = SelectProtected(evaluations, selectedIds, protectedFloor, onPlan.Evaluation.PlanFit, random);

        var offers = new[]
        {
            BuildPreview(standardA.Template, standardA.Evaluation, RecruitOfferSlotType.StandardA, false, false, plan, scoutDirective, seed + 1),
            BuildPreview(standardB.Template, standardB.Evaluation, RecruitOfferSlotType.StandardB, false, false, plan, scoutDirective, seed + 2),
            BuildPreview(onPlan.Template, onPlan.Evaluation, RecruitOfferSlotType.OnPlan, false, !scoutDirective.IsNone, plan, scoutDirective, seed + 3),
            BuildPreview(protectedSlot.Template, protectedSlot.Evaluation, RecruitOfferSlotType.Protected, protectedSlot.ProtectedByPity, false, plan, scoutDirective, seed + 4),
        };

        ValidatePackInvariants(evaluations, offers);

        var updatedPity = pityState.Clone();
        updatedPity.PacksSinceRarePlusSeen = offers.Any(offer => offer.Metadata.Tier.IsRarePlus())
            ? 0
            : updatedPity.PacksSinceRarePlusSeen + 1;
        updatedPity.PacksSinceEpicSeen = offers.Any(offer => offer.Metadata.Tier == RecruitTier.Epic)
            ? 0
            : updatedPity.PacksSinceEpicSeen + 1;

        var updatedPhase = phaseState.Clone();
        updatedPhase.PendingScoutDirective = new ScoutDirective();
        return new RecruitPackGenerationResult(offers, updatedPity, updatedPhase);
    }

    private static (CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation) SelectWeighted(
        IReadOnlyList<(CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation)> evaluations,
        HashSet<string> selectedIds,
        RecruitOfferSlotType slotType,
        RecruitTier protectedFloor,
        Random random)
    {
        var available = ReserveProtectedFloorCandidates(
            evaluations,
            evaluations.Where(entry => !selectedIds.Contains(entry.Template.Id)),
            selectedIds,
            protectedFloor);
        if (available.Count == 0)
        {
            throw new InvalidOperationException($"No recruit candidate available for slot {slotType}.");
        }

        var weights = available
            .Select(entry => Math.Max(1, BaseWeight(entry.Evaluation.Tier) + entry.Evaluation.Score.Total + 3))
            .ToList();
        var selectedIndex = SelectWeightedIndex(weights, random);
        var selected = available[selectedIndex];
        selectedIds.Add(selected.Template.Id);
        return selected;
    }

    private static (CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation) SelectOnPlan(
        IReadOnlyList<(CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation)> evaluations,
        HashSet<string> selectedIds,
        RecruitTier protectedFloor,
        Random random)
    {
        var onPlan = ReserveProtectedFloorCandidates(
                evaluations,
                evaluations.Where(entry => entry.Evaluation.PlanFit == CandidatePlanFit.OnPlan && !selectedIds.Contains(entry.Template.Id)),
                selectedIds,
                protectedFloor)
            .OrderByDescending(entry => entry.Evaluation.Score.Total)
            .ThenBy(entry => entry.Template.Id, StringComparer.Ordinal)
            .ToList();
        if (onPlan.Count > 0)
        {
            var selected = onPlan[random.Next(onPlan.Count)];
            selectedIds.Add(selected.Template.Id);
            return selected;
        }

        var bridge = ReserveProtectedFloorCandidates(
                evaluations,
                evaluations.Where(entry => entry.Evaluation.PlanFit == CandidatePlanFit.Bridge && !selectedIds.Contains(entry.Template.Id)),
                selectedIds,
                protectedFloor)
            .OrderByDescending(entry => entry.Evaluation.Score.Total)
            .ThenBy(entry => entry.Template.Id, StringComparer.Ordinal)
            .ToList();
        if (bridge.Count > 0)
        {
            var selected = bridge[0];
            selectedIds.Add(selected.Template.Id);
            return selected;
        }

        var fallback = ReserveProtectedFloorCandidates(
                evaluations,
                evaluations.Where(entry => !selectedIds.Contains(entry.Template.Id)),
                selectedIds,
                protectedFloor)
            .OrderByDescending(entry => entry.Evaluation.Score.Total)
            .ThenBy(entry => entry.Template.Id, StringComparer.Ordinal)
            .First();
        selectedIds.Add(fallback.Template.Id);
        return fallback;
    }

    private static (CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation, bool ProtectedByPity) SelectProtected(
        IReadOnlyList<(CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation)> evaluations,
        HashSet<string> selectedIds,
        RecruitTier floor,
        CandidatePlanFit onPlanFit,
        Random random)
    {
        var available = evaluations
            .Where(entry => !selectedIds.Contains(entry.Template.Id) && entry.Evaluation.Tier >= floor)
            .OrderByDescending(entry => entry.Evaluation.PlanFit == CandidatePlanFit.OnPlan ? 2 : entry.Evaluation.PlanFit == CandidatePlanFit.Bridge ? 1 : 0)
            .ThenByDescending(entry => entry.Evaluation.Score.Total)
            .ThenBy(entry => entry.Template.Id, StringComparer.Ordinal)
            .ToList();
        if (available.Count == 0)
        {
            var anyRemaining = evaluations.FirstOrDefault(entry => !selectedIds.Contains(entry.Template.Id));
            if (anyRemaining != default)
            {
                selectedIds.Add(anyRemaining.Template.Id);
                return (anyRemaining.Template, anyRemaining.Evaluation, false);
            }

            throw new InvalidOperationException($"Protected slot tier floor '{floor}' could not be satisfied.");
        }

        var selected = available[0];
        if (onPlanFit != CandidatePlanFit.OnPlan)
        {
            var onPlanProtected = available.FirstOrDefault(entry => entry.Evaluation.PlanFit == CandidatePlanFit.OnPlan);
            if (onPlanProtected != default)
            {
                selected = onPlanProtected;
            }
        }

        selectedIds.Add(selected.Template.Id);
        return (selected.Template, selected.Evaluation, floor != RecruitTier.Common);
    }

    private static RecruitTier ResolveProtectedFloor(RecruitPityState pityState)
    {
        return pityState.PacksSinceEpicSeen >= 8
            ? RecruitTier.Epic
            : pityState.PacksSinceRarePlusSeen >= 3
                ? RecruitTier.Rare
                : RecruitTier.Common;
    }

    private static List<(CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation)> ReserveProtectedFloorCandidates(
        IReadOnlyList<(CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation)> evaluations,
        IEnumerable<(CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation)> candidates,
        HashSet<string> selectedIds,
        RecruitTier protectedFloor)
    {
        var available = candidates.ToList();
        if (available.Count == 0 || protectedFloor == RecruitTier.Common)
        {
            return available;
        }

        var safeCandidates = available
            .Where(entry =>
                entry.Evaluation.Tier < protectedFloor
                || evaluations.Any(other =>
                    !selectedIds.Contains(other.Template.Id)
                    && !string.Equals(other.Template.Id, entry.Template.Id, StringComparison.Ordinal)
                    && other.Evaluation.Tier >= protectedFloor))
            .ToList();

        return safeCandidates.Count > 0 ? safeCandidates : available;
    }

    private static RecruitUnitPreview BuildPreview(
        CombatArchetypeTemplate template,
        RecruitCandidateEvaluation evaluation,
        RecruitOfferSlotType slotType,
        bool protectedByPity,
        bool biasedByScout,
        TeamPlanProfile plan,
        ScoutDirective directive,
        int seed)
    {
        var biasMode = slotType == RecruitOfferSlotType.OnPlan
            ? FlexRollBiasMode.NativePlusPlanBiased
            : FlexRollBiasMode.NativeBiased;
        var pair = RecruitPreviewBuilder.Roll(template, plan, directive, biasMode, seed);
        return new RecruitUnitPreview
        {
            UnitBlueprintId = template.Id,
            UnitInstanceSeed = $"recruit:{template.Id}:{slotType}:{seed}",
            FlexActiveId = pair.FlexActiveId,
            FlexPassiveId = pair.FlexPassiveId,
            Metadata = new RecruitOfferMetadata
            {
                SlotType = slotType,
                Tier = evaluation.Tier,
                PlanFit = evaluation.PlanFit,
                PlanScore = evaluation.Score,
                ProtectedByPity = protectedByPity,
                BiasedByScout = biasedByScout,
                GoldCost = RecruitmentBalanceCatalog.DefaultRecruitTierCosts.GetCost(evaluation.Tier),
            }
        };
    }

    private static void ValidatePackInvariants(
        IReadOnlyList<(CombatArchetypeTemplate Template, RecruitCandidateEvaluation Evaluation)> evaluations,
        IReadOnlyList<RecruitUnitPreview> offers)
    {
        if (offers.Select(offer => offer.UnitBlueprintId).Distinct(StringComparer.Ordinal).Count() != offers.Count)
        {
            throw new InvalidOperationException("Recruit pack contains duplicate blueprints.");
        }

        var onPlanExists = evaluations.Any(entry => entry.Evaluation.PlanFit == CandidatePlanFit.OnPlan);
        var bridgeExists = evaluations.Any(entry => entry.Evaluation.PlanFit == CandidatePlanFit.Bridge);
        var onPlanSlot = offers.Single(offer => offer.Metadata.SlotType == RecruitOfferSlotType.OnPlan);
        var protectedSlot = offers.Single(offer => offer.Metadata.SlotType == RecruitOfferSlotType.Protected);
        if (onPlanExists && onPlanSlot.Metadata.PlanFit != CandidatePlanFit.OnPlan)
        {
            throw new InvalidOperationException("On-plan candidate existed but OnPlan slot resolved off-plan.");
        }

        if ((onPlanExists || bridgeExists)
            && onPlanSlot.Metadata.PlanFit == CandidatePlanFit.OffPlan
            && protectedSlot.Metadata.PlanFit == CandidatePlanFit.OffPlan)
        {
            throw new InvalidOperationException("OnPlan and Protected slots both resolved off-plan while recoverable candidates existed.");
        }
    }

    private static int BaseWeight(RecruitTier tier)
    {
        return tier switch
        {
            RecruitTier.Common => 6,
            RecruitTier.Rare => 3,
            RecruitTier.Epic => 1,
            _ => 1,
        };
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
