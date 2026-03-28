using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Ids;

namespace SM.Combat.Services;

public static class BattleFactory
{
    public static BattleState Create(IReadOnlyList<UnitDefinition> allyDefinitions, IReadOnlyList<UnitDefinition> enemyDefinitions)
    {
        var allyPackages = SynergyService.BuildForTeam(allyDefinitions);
        var enemyPackages = SynergyService.BuildForTeam(enemyDefinitions);

        var allies = allyDefinitions.Select(def =>
        {
            var merged = MergePackages(def, allyPackages);
            return new UnitSnapshot(EntityId.New("ally"), TeamSide.Ally, merged);
        }).ToList();

        var enemies = enemyDefinitions.Select(def =>
        {
            var merged = MergePackages(def, enemyPackages);
            return new UnitSnapshot(EntityId.New("enemy"), TeamSide.Enemy, merged);
        }).ToList();

        return new BattleState(allies, enemies);
    }

    private static UnitDefinition MergePackages(UnitDefinition definition, IReadOnlyList<CombatModifierPackage> teamPackages)
    {
        var merged = (definition.Packages ?? new List<CombatModifierPackage>()).Concat(teamPackages).ToList();
        return definition with { Packages = merged };
    }
}
