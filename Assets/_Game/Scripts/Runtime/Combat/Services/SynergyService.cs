using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Combat.Services;

public static class SynergyService
{
    public static IReadOnlyList<CombatModifierPackage> BuildForTeam(IEnumerable<BattleUnitLoadout> units)
    {
        var list = new List<CombatModifierPackage>();
        var materialized = units.ToList();

        foreach (var raceGroup in materialized.GroupBy(x => x.RaceId))
        {
            var count = raceGroup.Count();
            if (count >= 2)
            {
                list.Add(new CombatModifierPackage(
                    $"race:{raceGroup.Key}:{count}",
                    ModifierSource.Synergy,
                    new[] { new StatModifier(StatKey.Attack, ModifierOp.Flat, count >= 3 ? 4f : 2f, ModifierSource.Synergy, $"race:{raceGroup.Key}:{count}") }));
            }
        }

        foreach (var classGroup in materialized.GroupBy(x => x.ClassId))
        {
            var count = classGroup.Count();
            if (count >= 2)
            {
                list.Add(new CombatModifierPackage(
                    $"class:{classGroup.Key}:{count}",
                    ModifierSource.Synergy,
                    new[] { new StatModifier(StatKey.Defense, ModifierOp.Flat, count >= 4 ? 4f : 2f, ModifierSource.Synergy, $"class:{classGroup.Key}:{count}") }));
            }
        }

        return list;
    }

    public static IReadOnlyList<CombatModifierPackage> BuildForTeam(
        IEnumerable<BattleUnitLoadout> units,
        IEnumerable<TeamSynergyTierRule> tierRules)
    {
        var materialized = units.ToList();
        var compiled = new List<CombatModifierPackage>();
        foreach (var rule in tierRules)
        {
            var count = materialized.Count(unit => unit.CompileTags?.Contains(rule.CountedTagId) == true);
            if (count < rule.Threshold)
            {
                continue;
            }

            compiled.Add(new CombatModifierPackage(
                $"synergy:{rule.SynergyId}:{rule.Threshold}",
                ModifierSource.Synergy,
                rule.Modifiers));
        }

        return compiled.Count > 0 ? compiled : BuildForTeam(materialized);
    }
}
