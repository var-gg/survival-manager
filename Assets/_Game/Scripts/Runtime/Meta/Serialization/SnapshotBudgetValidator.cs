using System;
using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Meta.Model;

namespace SM.Meta.Serialization;

public sealed record BudgetViolation(
    string SubjectId,
    string SubjectKind,
    int AuthoredScore,
    int Target,
    int Tolerance);

public static class SnapshotBudgetValidator
{
    public static IReadOnlyList<BudgetViolation> ValidateAll(CombatContentSnapshot snapshot)
    {
        var violations = new List<BudgetViolation>();
        ValidateArchetypes(snapshot, violations);
        ValidateAugments(snapshot, violations);
        return violations;
    }

    public static IReadOnlyList<BudgetViolation> ValidateArchetypes(CombatContentSnapshot snapshot)
    {
        var violations = new List<BudgetViolation>();
        ValidateArchetypes(snapshot, violations);
        return violations;
    }

    public static IReadOnlyList<BudgetViolation> ValidateAugments(CombatContentSnapshot snapshot)
    {
        var violations = new List<BudgetViolation>();
        ValidateAugments(snapshot, violations);
        return violations;
    }

    private static void ValidateArchetypes(CombatContentSnapshot snapshot, List<BudgetViolation> violations)
    {
        foreach (var (id, template) in snapshot.Archetypes)
        {
            if (template.Governance == null) continue;
            if (!Enum.TryParse<ContentRarity>(template.Governance.Rarity, out var rarity)) continue;
            if (!LoopCContentGovernance.UnitBudgetTargets.TryGetValue(rarity, out var budget)) continue;

            var score = template.Governance.BudgetFinalScore;
            if (Math.Abs(score - budget.Target) > budget.Tolerance)
            {
                violations.Add(new BudgetViolation(id, "Archetype", score, budget.Target, budget.Tolerance));
            }
        }
    }

    public static IReadOnlyList<BudgetViolation> ValidateSingleArchetype(string id, CombatArchetypeTemplate template)
    {
        var violations = new List<BudgetViolation>();
        if (template.Governance == null) return violations;
        if (!Enum.TryParse<ContentRarity>(template.Governance.Rarity, out var rarity)) return violations;
        if (!LoopCContentGovernance.UnitBudgetTargets.TryGetValue(rarity, out var budget)) return violations;

        var score = template.Governance.BudgetFinalScore;
        if (Math.Abs(score - budget.Target) > budget.Tolerance)
        {
            violations.Add(new BudgetViolation(id, "Archetype", score, budget.Target, budget.Tolerance));
        }

        return violations;
    }

    public static IReadOnlyList<BudgetViolation> ValidateSingleAugment(string id, AugmentCatalogEntry entry)
    {
        var violations = new List<BudgetViolation>();
        if (entry.Governance == null) return violations;
        if (!Enum.TryParse<ContentRarity>(entry.Governance.Rarity, out var rarity)) return violations;
        if (!LoopCContentGovernance.AugmentBudgetTargets.TryGetValue(rarity, out var budget)) return violations;

        var score = entry.Governance.BudgetFinalScore;
        if (Math.Abs(score - budget.Target) > budget.Tolerance)
        {
            violations.Add(new BudgetViolation(id, "Augment", score, budget.Target, budget.Tolerance));
        }

        return violations;
    }

    private static void ValidateAugments(CombatContentSnapshot snapshot, List<BudgetViolation> violations)
    {
        if (snapshot.AugmentCatalog == null) return;
        foreach (var (id, entry) in snapshot.AugmentCatalog)
        {
            if (entry.Governance == null) continue;
            if (!Enum.TryParse<ContentRarity>(entry.Governance.Rarity, out var rarity)) continue;
            if (!LoopCContentGovernance.AugmentBudgetTargets.TryGetValue(rarity, out var budget)) continue;

            var score = entry.Governance.BudgetFinalScore;
            if (Math.Abs(score - budget.Target) > budget.Tolerance)
            {
                violations.Add(new BudgetViolation(id, "Augment", score, budget.Target, budget.Tolerance));
            }
        }
    }
}
