using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Ids;

namespace SM.Combat.Services;

public static class BattleFactory
{
    public static BattleState Create(
        IReadOnlyList<UnitDefinition> allyDefinitions,
        IReadOnlyList<UnitDefinition> enemyDefinitions,
        TeamPostureType allyPosture = TeamPostureType.StandardAdvance,
        TeamPostureType enemyPosture = TeamPostureType.StandardAdvance,
        float fixedStepSeconds = BattleSimulator.DefaultFixedStepSeconds,
        int seed = 7)
    {
        var allyPackages = SynergyService.BuildForTeam(allyDefinitions);
        var enemyPackages = SynergyService.BuildForTeam(enemyDefinitions);

        var allies = allyDefinitions.Select((def, index) =>
        {
            var merged = MergePackages(def, allyPackages);
            var anchorPosition = ResolveAnchorPosition(TeamSide.Ally, merged.PreferredAnchor);
            var spawnPosition = ResolveSpawnPosition(TeamSide.Ally, merged.PreferredAnchor);
            return new UnitSnapshot(new EntityId($"ally_{index}_{merged.Id}"), TeamSide.Ally, merged, anchorPosition, spawnPosition);
        }).ToList();

        var enemies = enemyDefinitions.Select((def, index) =>
        {
            var merged = MergePackages(def, enemyPackages);
            var anchorPosition = ResolveAnchorPosition(TeamSide.Enemy, merged.PreferredAnchor);
            var spawnPosition = ResolveSpawnPosition(TeamSide.Enemy, merged.PreferredAnchor);
            return new UnitSnapshot(new EntityId($"enemy_{index}_{merged.Id}"), TeamSide.Enemy, merged, anchorPosition, spawnPosition);
        }).ToList();

        return new BattleState(allies, enemies, allyPosture, enemyPosture, fixedStepSeconds, seed);
    }

    private static UnitDefinition MergePackages(UnitDefinition definition, IReadOnlyList<CombatModifierPackage> teamPackages)
    {
        var merged = (definition.Packages ?? new List<CombatModifierPackage>()).Concat(teamPackages).ToList();
        return definition with { Packages = merged };
    }

    public static CombatVector2 ResolveAnchorPosition(TeamSide side, DeploymentAnchorId anchor)
    {
        var frontX = side == TeamSide.Ally ? -2.8f : 2.8f;
        var backX = side == TeamSide.Ally ? -4.9f : 4.9f;
        var y = anchor switch
        {
            DeploymentAnchorId.FrontTop or DeploymentAnchorId.BackTop => 1.8f,
            DeploymentAnchorId.FrontCenter or DeploymentAnchorId.BackCenter => 0f,
            _ => -1.8f,
        };

        return new CombatVector2(anchor.IsFrontRow() ? frontX : backX, y);
    }

    public static CombatVector2 ResolveSpawnPosition(TeamSide side, DeploymentAnchorId anchor)
    {
        var anchorPosition = ResolveAnchorPosition(side, anchor);
        var offset = side == TeamSide.Ally ? -1.25f : 1.25f;
        return anchorPosition + new CombatVector2(offset, 0f);
    }
}
