using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Editor.Validation;

public sealed record BalanceSweepScenarioInput(
    string ScenarioId,
    string Description,
    string TeamTacticId,
    CombatContentSnapshot Content,
    BattleLoadoutSnapshot PlayerSnapshot,
    IReadOnlyList<BattleUnitLoadout> EnemyLoadout,
    IReadOnlyList<int> Seeds);

public static class BalanceSweepScenarioFactory
{
    private sealed record SweepItemSpec(
        string ItemId,
        IReadOnlyList<string> AffixIds);

    private sealed record SweepHeroSpec(
        string HeroId,
        string ArchetypeId,
        DeploymentAnchorId Anchor,
        string RoleInstructionId,
        string PassiveBoardId,
        IReadOnlyList<string> PassiveNodeIds,
        IReadOnlyList<SweepItemSpec> Items);

    private sealed record SweepScenarioSpec(
        string ScenarioId,
        string Description,
        string TeamTacticId,
        IReadOnlyList<string> TemporaryAugmentIds,
        IReadOnlyList<string> PermanentAugmentIds,
        IReadOnlyList<int> Seeds,
        IReadOnlyList<SweepHeroSpec> Heroes);

    private static readonly IReadOnlyList<SweepScenarioSpec> SmokeScenarios = new[]
    {
        new SweepScenarioSpec(
            "mixed_floor_control",
            "Mixed paid-floor squad against the observer smoke encounter.",
            "team_tactic_standard_advance",
            new[] { "augment_silver_guard", "augment_silver_hunt" },
            new[] { "augment_perm_legacy_oath" },
            new[] { 17, 23, 29 },
            new SweepHeroSpec[]
            {
                new(
                    "hero.warden",
                    "warden",
                    DeploymentAnchorId.FrontCenter,
                    "anchor",
                    "board_vanguard",
                    new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" },
                    new SweepItemSpec[]
                    {
                        new("item_guardian_shield", new[] { "affix_guarded" }),
                        new("item_warden_armor", new[] { "affix_bracing" }),
                        new("item_warden_trinket", new[] { "affix_hallowed" }),
                    }),
                new(
                    "hero.raider",
                    "raider",
                    DeploymentAnchorId.FrontBottom,
                    "bruiser",
                    "board_duelist",
                    new[] { "passive_duelist_small_01", "passive_duelist_notable_01" },
                    new SweepItemSpec[]
                    {
                        new("item_reaver_blade", new[] { "affix_fierce" }),
                        new("item_raider_armor", new[] { "affix_relentless" }),
                        new("item_raider_trinket", new[] { "affix_precise" }),
                    }),
                new(
                    "hero.marksman",
                    "marksman",
                    DeploymentAnchorId.BackTop,
                    "carry",
                    "board_ranger",
                    new[] { "passive_ranger_small_01", "passive_ranger_notable_01" },
                    new SweepItemSpec[]
                    {
                        new("item_marksman_bow", new[] { "affix_farshot" }),
                        new("item_marksman_armor", new[] { "affix_watchful" }),
                        new("item_marksman_trinket", new[] { "affix_quick" }),
                    }),
                new(
                    "hero.priest",
                    "priest",
                    DeploymentAnchorId.BackBottom,
                    "support",
                    "board_mystic",
                    new[] { "passive_mystic_small_01", "passive_mystic_notable_01" },
                    new SweepItemSpec[]
                    {
                        new("item_priest_focus", new[] { "affix_channeling" }),
                        new("item_priest_armor", new[] { "affix_lucid" }),
                        new("item_prayer_bead", new[] { "affix_mender" }),
                    }),
            }),
        new SweepScenarioSpec(
            "focused_beastkin_push",
            "Beastkin-focused launch-floor squad against the observer smoke encounter.",
            "team_tactic_collapse_weak_side",
            new[] { "augment_silver_stride", "augment_silver_ward" },
            new[] { "augment_perm_legacy_fang" },
            new[] { 17, 23, 29 },
            new SweepHeroSpec[]
            {
                new(
                    "hero.bulwark",
                    "bulwark",
                    DeploymentAnchorId.FrontCenter,
                    "anchor",
                    "board_vanguard",
                    new[] { "passive_vanguard_small_02", "passive_vanguard_notable_02" },
                    new SweepItemSpec[]
                    {
                        new("item_bulwark_shield", new[] { "affix_guarded" }),
                        new("item_bulwark_armor", new[] { "affix_bracing" }),
                        new("item_bulwark_trinket", new[] { "affix_hallowed" }),
                    }),
                new(
                    "hero.raider",
                    "raider",
                    DeploymentAnchorId.FrontBottom,
                    "bruiser",
                    "board_duelist",
                    new[] { "passive_duelist_small_02", "passive_duelist_notable_02" },
                    new SweepItemSpec[]
                    {
                        new("item_reaver_blade", new[] { "affix_fierce" }),
                        new("item_raider_armor", new[] { "affix_ravenous" }),
                        new("item_raider_trinket", new[] { "affix_precise" }),
                    }),
                new(
                    "hero.scout",
                    "scout",
                    DeploymentAnchorId.BackTop,
                    "carry",
                    "board_ranger",
                    new[] { "passive_ranger_small_02", "passive_ranger_notable_02" },
                    new SweepItemSpec[]
                    {
                        new("item_scout_bow", new[] { "affix_farshot" }),
                        new("item_scout_armor", new[] { "affix_watchful" }),
                        new("item_lucky_charm", new[] { "affix_quick" }),
                    }),
                new(
                    "hero.shaman",
                    "shaman",
                    DeploymentAnchorId.BackBottom,
                    "support",
                    "board_mystic",
                    new[] { "passive_mystic_small_02", "passive_mystic_notable_02" },
                    new SweepItemSpec[]
                    {
                        new("item_shaman_focus", new[] { "affix_channeling" }),
                        new("item_shaman_armor", new[] { "affix_packborn" }),
                        new("item_shaman_trinket", new[] { "affix_mender" }),
                    }),
            }),
    };

    public static IReadOnlyList<BalanceSweepScenarioInput> BuildSmokeScenarios()
    {
#if UNITY_EDITOR
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
#endif
        var lookup = new RuntimeCombatContentLookup();
        if (!lookup.TryGetCombatSnapshot(out var content, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var enemyLoadout = BuildObserverSmokeEnemies(content);
        return SmokeScenarios
            .Select(spec => BuildScenario(spec, content, enemyLoadout))
            .ToList();
    }

    private static BalanceSweepScenarioInput BuildScenario(
        SweepScenarioSpec spec,
        CombatContentSnapshot content,
        IReadOnlyList<BattleUnitLoadout> enemyLoadout)
    {
        var heroes = new List<HeroRecord>(spec.Heroes.Count);
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var passiveSelections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        var deploymentAssignments = new Dictionary<DeploymentAnchorId, string>();
        var heroRoleIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var heroSpec in spec.Heroes)
        {
            if (!content.Archetypes.TryGetValue(heroSpec.ArchetypeId, out var archetype))
            {
                throw new InvalidOperationException($"Balance sweep scenario '{spec.ScenarioId}' references missing archetype '{heroSpec.ArchetypeId}'.");
            }

            heroes.Add(new HeroRecord(
                heroSpec.HeroId,
                heroSpec.HeroId,
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                string.Empty,
                string.Empty));

            var equippedItemInstanceIds = new List<string>();
            for (var itemIndex = 0; itemIndex < heroSpec.Items.Count; itemIndex++)
            {
                var itemSpec = heroSpec.Items[itemIndex];
                var instanceId = $"{heroSpec.HeroId}.item.{itemIndex}";
                itemInstances[instanceId] = new ItemInstanceState(instanceId, itemSpec.ItemId, itemSpec.AffixIds, heroSpec.HeroId);
                equippedItemInstanceIds.Add(instanceId);
            }

            heroLoadouts[heroSpec.HeroId] = new HeroLoadoutState(
                heroSpec.HeroId,
                equippedItemInstanceIds,
                Array.Empty<string>(),
                heroSpec.PassiveBoardId,
                heroSpec.PassiveNodeIds,
                spec.PermanentAugmentIds);
            heroProgressions[heroSpec.HeroId] = new HeroProgressionState(
                heroSpec.HeroId,
                1,
                0,
                heroSpec.PassiveNodeIds,
                archetype.Skills.Select(skill => skill.Id).ToList());
            passiveSelections[heroSpec.HeroId] = new PassiveBoardSelectionState(
                heroSpec.HeroId,
                heroSpec.PassiveBoardId,
                heroSpec.PassiveNodeIds);
            deploymentAssignments[heroSpec.Anchor] = heroSpec.HeroId;
            heroRoleIds[heroSpec.HeroId] = heroSpec.RoleInstructionId;
        }

        var blueprint = new SquadBlueprintState(
            spec.ScenarioId,
            spec.Description,
            ResolveTeamPosture(spec.TeamTacticId),
            spec.TeamTacticId,
            deploymentAssignments,
            heroes.Select(hero => hero.Id).ToList(),
            heroRoleIds);

        var compiler = new LoadoutCompiler();
        var snapshot = compiler.Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            itemInstances,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            passiveSelections,
            new PermanentAugmentLoadoutState(spec.ScenarioId, spec.PermanentAugmentIds),
            blueprint,
            new RunOverlayState(0, spec.TemporaryAugmentIds, Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);

        return new BalanceSweepScenarioInput(
            spec.ScenarioId,
            spec.Description,
            spec.TeamTacticId,
            content,
            snapshot,
            enemyLoadout,
            spec.Seeds);
    }

    private static IReadOnlyList<BattleUnitLoadout> BuildObserverSmokeEnemies(CombatContentSnapshot content)
    {
        var build = BattleSetupBuilder.Build(Array.Empty<BattleParticipantSpec>(), BattleEncounterPlans.CreateObserverSmokePlan(), content);
        if (!build.IsSuccess)
        {
            throw new InvalidOperationException(build.Error ?? "Balance sweep encounter build failed.");
        }

        return build.Enemies;
    }

    private static TeamPostureType ResolveTeamPosture(string teamTacticId)
    {
        return teamTacticId switch
        {
            "team_tactic_hold_line" => TeamPostureType.HoldLine,
            "team_tactic_protect_carry" => TeamPostureType.ProtectCarry,
            "team_tactic_collapse_weak_side" => TeamPostureType.CollapseWeakSide,
            "team_tactic_all_in_backline" => TeamPostureType.AllInBackline,
            _ => TeamPostureType.StandardAdvance,
        };
    }
}
