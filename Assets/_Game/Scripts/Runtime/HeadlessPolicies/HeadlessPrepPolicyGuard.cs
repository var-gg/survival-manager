using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>prep transaction을 formation edit 2회, bench swap 1회, 보유 장비 재배치로 제한한다.</summary>
public static class HeadlessPrepPolicyGuard
{
    public const int MaximumFormationEdits = 2;
    public const int MaximumBenchSwaps = 1;
    public const int MaximumEquipmentAssignments = 1;

    public static void ValidateDecision(HeadlessPolicyObservation observation, HeadlessPrepDecision decision)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        if (decision == null) throw new InvalidOperationException("Policy must return a prep decision.");
        if (string.IsNullOrWhiteSpace(decision.Rationale)) throw new InvalidOperationException("Prep rationale is required.");
        if (double.IsNaN(decision.EstimatedValue) || double.IsInfinity(decision.EstimatedValue))
        {
            throw new InvalidOperationException("Prep estimated value must be finite.");
        }

        if (decision.EvidenceFactIds == null || decision.EvidenceFactIds.Count == 0
            || decision.EvidenceFactIds.Any(string.IsNullOrWhiteSpace)
            || decision.EvidenceFactIds.Distinct(StringComparer.Ordinal).Count() != decision.EvidenceFactIds.Count)
        {
            throw new HeadlessPolicyEvidenceException("Every prep decision must cite distinct player-visible facts.");
        }

        if (observation.CurrentPlacements.Count != observation.DeployCapacity)
        {
            throw new InvalidOperationException("Prep requires a complete current deployment snapshot.");
        }

        var deployment = new HeadlessDeploymentDecision(
            decision.Placements,
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds);
        HeadlessPolicyGuard.ValidateDeploymentDecision(observation, deployment);

        var beforeByHero = observation.CurrentPlacements.ToDictionary(value => value.HeroId, value => value.Anchor, StringComparer.Ordinal);
        var afterByHero = decision.Placements.ToDictionary(value => value.HeroId, value => value.Anchor, StringComparer.Ordinal);
        var removed = beforeByHero.Keys.Count(heroId => !afterByHero.ContainsKey(heroId));
        var added = afterByHero.Keys.Count(heroId => !beforeByHero.ContainsKey(heroId));
        if (removed != added || removed > MaximumBenchSwaps)
        {
            throw new InvalidOperationException($"Prep exceeds the {MaximumBenchSwaps}-bench-swap budget.");
        }

        var formationEdits = beforeByHero.Keys
            .Where(afterByHero.ContainsKey)
            .Count(heroId => beforeByHero[heroId] != afterByHero[heroId]);
        if (formationEdits > MaximumFormationEdits)
        {
            throw new InvalidOperationException($"Prep exceeds the {MaximumFormationEdits}-formation-edit budget.");
        }

        var owned = observation.OwnedItems.ToDictionary(
            value => value.Mechanics.ItemInstanceId,
            value => value,
            StringComparer.Ordinal);
        RequireDistinct(decision.EquipmentAssignments.Select(value => value.ItemInstanceId), "prep item");
        if (decision.EquipmentAssignments.Count > MaximumEquipmentAssignments)
        {
            throw new InvalidOperationException(
                $"Prep exceeds the {MaximumEquipmentAssignments}-equipment-assignment budget.");
        }

        foreach (var assignment in decision.EquipmentAssignments)
        {
            if (assignment == null
                || string.IsNullOrWhiteSpace(assignment.ItemInstanceId)
                || string.IsNullOrWhiteSpace(assignment.HeroId)
                || !owned.ContainsKey(assignment.ItemInstanceId)
                || !afterByHero.ContainsKey(assignment.HeroId))
            {
                throw new InvalidOperationException("Prep equipment assignment must move an owned item to a deployed hero.");
            }
        }
    }

    private static void RequireDistinct<T>(IEnumerable<T> values, string label)
    {
        var materialized = values.ToArray();
        if (materialized.Distinct().Count() != materialized.Length)
        {
            throw new InvalidOperationException($"Prep contains duplicate {label} values.");
        }
    }
}
