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
        var materialized = units
            .Where(unit => unit.EntityKind == SM.Core.Contracts.CombatEntityKind.RosterUnit)
            .ToList();

        // V1 권위 breakpoint: 세력(race) 2/4 · 직업(class) 2/3 (wiki-combat-v1-index / project_v1_system_authority).
        // 이 폴백은 authored 시너지 tier rule 이 없을 때만 쓰이는 안전망이지만 V1 티어와 일치해야 한다.
        foreach (var raceGroup in materialized.GroupBy(x => x.RaceId))
        {
            var count = raceGroup.Count();
            if (count >= 2)
            {
                list.Add(new CombatModifierPackage(
                    $"race:{raceGroup.Key}:{count}",
                    ModifierSource.Synergy,
                    new[] { new StatModifier(StatKey.PhysPower, ModifierOp.Flat, count >= 4 ? 4f : 2f, ModifierSource.Synergy, $"race:{raceGroup.Key}:{count}") }));
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
                    new[] { new StatModifier(StatKey.Armor, ModifierOp.Flat, count >= 3 ? 4f : 2f, ModifierSource.Synergy, $"class:{classGroup.Key}:{count}") }));
            }
        }

        return list;
    }

    public static IReadOnlyList<CombatModifierPackage> BuildForTeam(
        IEnumerable<BattleUnitLoadout> units,
        IEnumerable<TeamSynergyTierRule> tierRules)
    {
        var materialized = units
            .Where(unit => unit.EntityKind == SM.Core.Contracts.CombatEntityKind.RosterUnit)
            .ToList();
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
