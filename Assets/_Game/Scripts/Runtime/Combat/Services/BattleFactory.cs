using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Ids;

namespace SM.Combat.Services;

public static class BattleFactory
{
    public static BattleState Create(
        IReadOnlyList<BattleUnitLoadout> allyDefinitions,
        IReadOnlyList<BattleUnitLoadout> enemyDefinitions,
        TeamPostureType allyPosture = TeamPostureType.StandardAdvance,
        TeamPostureType enemyPosture = TeamPostureType.StandardAdvance,
        float fixedStepSeconds = BattleSimulator.DefaultFixedStepSeconds,
        int seed = 7,
        BattlefieldLayout? layout = null)
    {
        var resolved = layout ?? BattlefieldLayout.Default;
        var allyPackages = ResolveTeamPackages(allyDefinitions);
        var enemyPackages = ResolveTeamPackages(enemyDefinitions);

        var allies = allyDefinitions.Select((def, index) =>
        {
            var merged = MergePackages(def, allyPackages);
            var anchorPosition = resolved.ResolveAnchorPosition(TeamSide.Ally, merged.PreferredAnchor);
            var spawnPosition = resolved.ResolveSpawnPosition(TeamSide.Ally, merged.PreferredAnchor);
            return new UnitSnapshot(new EntityId($"ally_{index}_{merged.Id}"), TeamSide.Ally, merged, anchorPosition, spawnPosition);
        }).ToList();

        var enemies = enemyDefinitions.Select((def, index) =>
        {
            var merged = MergePackages(def, enemyPackages);
            var anchorPosition = resolved.ResolveAnchorPosition(TeamSide.Enemy, merged.PreferredAnchor);
            var spawnPosition = resolved.ResolveSpawnPosition(TeamSide.Enemy, merged.PreferredAnchor);
            return new UnitSnapshot(new EntityId($"enemy_{index}_{merged.Id}"), TeamSide.Enemy, merged, anchorPosition, spawnPosition);
        }).ToList();

        return new BattleState(allies, enemies, allyPosture, enemyPosture, fixedStepSeconds, seed);
    }

    private static IReadOnlyList<CombatModifierPackage> ResolveTeamPackages(IReadOnlyList<BattleUnitLoadout> definitions)
    {
        var precompiled = definitions
            .SelectMany(definition => definition.TeamPackages ?? new List<CombatModifierPackage>())
            .GroupBy(package => $"{package.Source}:{package.SourceId}")
            .Select(group => group.First())
            .ToList();
        return precompiled.Count > 0 ? precompiled : SynergyService.BuildForTeam(definitions);
    }

    private static BattleUnitLoadout MergePackages(BattleUnitLoadout definition, IReadOnlyList<CombatModifierPackage> teamPackages)
    {
        var merged = definition.NumericPackages.Concat(teamPackages).ToList();
        return definition with
        {
            Packages = merged,
            TeamPackages = teamPackages
        };
    }

    public static CombatVector2 ResolveAnchorPosition(TeamSide side, DeploymentAnchorId anchor)
        => BattlefieldLayout.Default.ResolveAnchorPosition(side, anchor);

    public static CombatVector2 ResolveSpawnPosition(TeamSide side, DeploymentAnchorId anchor)
        => BattlefieldLayout.Default.ResolveSpawnPosition(side, anchor);
}
