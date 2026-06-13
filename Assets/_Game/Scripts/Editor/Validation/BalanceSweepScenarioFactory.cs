using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
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

public sealed record BalanceScenarioCoverageMatrixRow(
    string ScenarioId,
    string SourceLane,
    string CharacterArchetypes,
    string EncounterFamily,
    string BuildLane,
    string SynergyAxis,
    string StatusFamily,
    string ReadabilityAsk,
    string KpiCoverage,
    string GapDisposition);

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

    private sealed record ThreatScenarioSpec(
        string ScenarioId,
        string Description,
        string TeamTacticId,
        ThreatPattern ThreatPattern,
        string EncounterId,
        IReadOnlyList<string> TemporaryAugmentIds,
        IReadOnlyList<string> PermanentAugmentIds,
        IReadOnlyList<int> Seeds,
        IReadOnlyList<SweepHeroSpec> Heroes);

    private static SweepHeroSpec MakeHero(
        string heroId,
        string archetypeId,
        DeploymentAnchorId anchor,
        string roleInstructionId,
        string passiveBoardId,
        IReadOnlyList<string> passiveNodeIds)
    {
        return new SweepHeroSpec(
            heroId,
            archetypeId,
            anchor,
            roleInstructionId,
            passiveBoardId,
            passiveNodeIds,
            Array.Empty<SweepItemSpec>());
    }

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

    private static readonly IReadOnlyList<ThreatScenarioSpec> ThreatScenarios = new ThreatScenarioSpec[]
    {
        new(
            "ArmorFrontlineScenario",
            "ArmorFrontline lane harness against an armor-heavy authored encounter.",
            "team_tactic_standard_advance",
            ThreatPattern.ArmorFrontline,
            "site_ashen_gate_elite_1",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.warden", "warden", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" }),
                MakeHero("hero.slayer", "slayer", DeploymentAnchorId.FrontBottom, "bruiser", "board_duelist", new[] { "passive_duelist_small_01", "passive_duelist_notable_01" }),
                MakeHero("hero.marksman", "marksman", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_01", "passive_ranger_notable_01" }),
                MakeHero("hero.priest", "priest", DeploymentAnchorId.BackBottom, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
            }),
        new(
            "ResistanceShellScenario",
            "ResistanceShell lane harness against a resistance-heavy authored encounter.",
            "team_tactic_standard_advance",
            ThreatPattern.ResistanceShell,
            "site_ruined_crypts_boss_1",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.warden", "warden", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" }),
                MakeHero("hero.hexer", "hexer", DeploymentAnchorId.BackCenter, "support", "board_mystic", new[] { "passive_mystic_small_02", "passive_mystic_notable_02" }),
                MakeHero("hero.scout", "scout", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_02", "passive_ranger_notable_02" }),
                MakeHero("hero.priest", "priest", DeploymentAnchorId.BackBottom, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
            }),
        new(
            "GuardBulwarkScenario",
            "GuardBulwark lane harness against a guard-heavy authored encounter.",
            "team_tactic_standard_advance",
            ThreatPattern.GuardBulwark,
            "site_ashen_gate_boss_1",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.warden", "warden", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" }),
                MakeHero("hero.raider", "raider", DeploymentAnchorId.FrontBottom, "bruiser", "board_duelist", new[] { "passive_duelist_small_02", "passive_duelist_notable_02" }),
                MakeHero("hero.marksman", "marksman", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_01", "passive_ranger_notable_01" }),
                MakeHero("hero.priest", "priest", DeploymentAnchorId.BackBottom, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
            }),
        new(
            "EvasiveSkirmishScenario",
            "EvasiveSkirmish lane harness against evasive skirmishers.",
            "team_tactic_collapse_weak_side",
            ThreatPattern.EvasiveSkirmish,
            "site_wolfpine_trail_skirmish_1",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.bulwark", "bulwark", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_02", "passive_vanguard_notable_02" }),
                MakeHero("hero.hunter", "hunter", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_01", "passive_ranger_notable_01" }),
                MakeHero("hero.marksman", "marksman", DeploymentAnchorId.BackCenter, "carry", "board_ranger", new[] { "passive_ranger_small_02", "passive_ranger_notable_02" }),
                MakeHero("hero.priest", "priest", DeploymentAnchorId.BackBottom, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
            }),
        new(
            "ControlChainScenario",
            "ControlChain lane harness against layered CC pressure.",
            "team_tactic_hold_line",
            ThreatPattern.ControlChain,
            "site_worldscar_depths_boss_1",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.warden", "warden", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" }),
                MakeHero("hero.raider", "raider", DeploymentAnchorId.FrontBottom, "bruiser", "board_duelist", new[] { "passive_duelist_small_02", "passive_duelist_notable_02" }),
                MakeHero("hero.marksman", "marksman", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_01", "passive_ranger_notable_01" }),
                MakeHero("hero.priest", "priest", DeploymentAnchorId.BackBottom, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
            }),
        new(
            "SustainBallScenario",
            "SustainBall lane harness against attrition and healing shells.",
            "team_tactic_protect_carry",
            ThreatPattern.SustainBall,
            "site_starved_menagerie_elite_1",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.warden", "warden", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" }),
                MakeHero("hero.hexer", "hexer", DeploymentAnchorId.BackCenter, "support", "board_mystic", new[] { "passive_mystic_small_02", "passive_mystic_notable_02" }),
                MakeHero("hero.shaman", "shaman", DeploymentAnchorId.BackBottom, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
                MakeHero("hero.hunter", "hunter", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_01", "passive_ranger_notable_01" }),
            }),
        new(
            "DiveBacklineScenario",
            "DiveBackline lane harness against backline pressure and peel checks.",
            "team_tactic_hold_line",
            ThreatPattern.DiveBackline,
            "site_wolfpine_trail_skirmish_2",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.warden", "warden", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" }),
                MakeHero("hero.bulwark", "bulwark", DeploymentAnchorId.FrontTop, "anchor", "board_vanguard", new[] { "passive_vanguard_small_02", "passive_vanguard_notable_02" }),
                MakeHero("hero.marksman", "marksman", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_01", "passive_ranger_notable_01" }),
                MakeHero("hero.priest", "priest", DeploymentAnchorId.BackBottom, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
            }),
        new(
            "SwarmFloodScenario",
            "SwarmFlood lane harness against multi-body board pressure.",
            "team_tactic_standard_advance",
            ThreatPattern.SwarmFlood,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { 17, 23 },
            new[]
            {
                MakeHero("hero.warden", "warden", DeploymentAnchorId.FrontCenter, "anchor", "board_vanguard", new[] { "passive_vanguard_small_01", "passive_vanguard_notable_01" }),
                MakeHero("hero.shaman", "shaman", DeploymentAnchorId.BackCenter, "support", "board_mystic", new[] { "passive_mystic_small_01", "passive_mystic_notable_01" }),
                MakeHero("hero.hunter", "hunter", DeploymentAnchorId.BackTop, "carry", "board_ranger", new[] { "passive_ranger_small_01", "passive_ranger_notable_01" }),
                MakeHero("hero.scout", "scout", DeploymentAnchorId.BackBottom, "carry", "board_ranger", new[] { "passive_ranger_small_02", "passive_ranger_notable_02" }),
            }),
    };

    public static IReadOnlyList<BalanceSweepScenarioInput> BuildSmokeScenarios()
    {
#if UNITY_EDITOR
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
#endif
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var enemyLoadout = BuildObserverSmokeEnemies(content);
        return SmokeScenarios
            .Select(spec => BuildScenario(spec, content, enemyLoadout))
            .ToList();
    }

    public static IReadOnlyList<BalanceSweepScenarioInput> BuildThreatTopologyScenarios()
    {
#if UNITY_EDITOR
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
#endif
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return ThreatScenarios
            .Select(spec => BuildThreatScenario(spec, content))
            .ToList();
    }

    public static IReadOnlyList<BalanceScenarioCoverageMatrixRow> BuildCoverageMatrixRows()
    {
        var rows = new List<BalanceScenarioCoverageMatrixRow>();
        rows.AddRange(SmokeScenarios.Select(spec => new BalanceScenarioCoverageMatrixRow(
            spec.ScenarioId,
            "balance-sweep-smoke",
            FormatArchetypes(spec.Heroes),
            "observer_smoke",
            spec.TeamTacticId,
            FormatSynergyAxis(spec.TemporaryAugmentIds, spec.PermanentAugmentIds),
            "baseline/direct-damage",
            "determinism/cadence/duration",
            "compile_hash, final_state_hash, average_battle_duration, first_signature_cast",
            "covered")));

        rows.AddRange(ThreatScenarios.Select(spec => new BalanceScenarioCoverageMatrixRow(
            spec.ScenarioId,
            "balance-sweep-threat-topology",
            FormatArchetypes(spec.Heroes),
            string.IsNullOrWhiteSpace(spec.EncounterId) ? spec.ThreatPattern.ToString() : spec.EncounterId,
            spec.TeamTacticId,
            FormatSynergyAxis(spec.TemporaryAugmentIds, spec.PermanentAugmentIds),
            ResolveStatusFamily(spec.ThreatPattern),
            ResolveReadabilityAsk(spec.ThreatPattern),
            "counter_coverage, validation_error_count, validation_warning_count",
            spec.ThreatPattern == ThreatPattern.SwarmFlood ? "debug-only enemy synthesis" : "covered")));

        rows.AddRange(BuildLoopDScenarioRows());
        rows.Add(new BalanceScenarioCoverageMatrixRow(
            "RunLite_EconomyChoice",
            "loopd-runlite",
            "first_playable_roster",
            "mini_run_progression",
            "recruit/retrain/scout/duplicate",
            "purchase_plan",
            "n/a",
            "economy choice readability",
            "dead_offer_ratio, no_affordable_option_rate, echo_spend_ratio, protected_purchase_share, on_plan_purchase_share",
            "covered"));

        return rows
            .OrderBy(row => row.SourceLane, StringComparer.Ordinal)
            .ThenBy(row => row.ScenarioId, StringComparer.Ordinal)
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

    private static BalanceSweepScenarioInput BuildThreatScenario(ThreatScenarioSpec spec, CombatContentSnapshot content)
    {
        var enemyLoadout = BuildThreatEnemyLoadout(spec, content);
        return BuildScenario(
            new SweepScenarioSpec(
                spec.ScenarioId,
                spec.Description,
                spec.TeamTacticId,
                spec.TemporaryAugmentIds,
                spec.PermanentAugmentIds,
                spec.Seeds,
                spec.Heroes),
            content,
            enemyLoadout);
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

    private static IReadOnlyList<BattleUnitLoadout> BuildThreatEnemyLoadout(ThreatScenarioSpec spec, CombatContentSnapshot content)
    {
        if (!string.IsNullOrWhiteSpace(spec.EncounterId))
        {
            return BuildCatalogEncounterEnemies(content, spec.EncounterId);
        }

        var plan = spec.ThreatPattern switch
        {
            ThreatPattern.SwarmFlood => new BattleEncounterPlan(
                new[]
                {
                    new BattleParticipantSpec("enemy.swarm.1", "swarm-1", "scout", DeploymentAnchorId.FrontTop, string.Empty, string.Empty, Array.Empty<BattleEquippedItemSpec>(), Array.Empty<string>()),
                    new BattleParticipantSpec("enemy.swarm.2", "swarm-2", "hunter", DeploymentAnchorId.FrontCenter, string.Empty, string.Empty, Array.Empty<BattleEquippedItemSpec>(), Array.Empty<string>()),
                    new BattleParticipantSpec("enemy.swarm.3", "swarm-3", "scout", DeploymentAnchorId.FrontBottom, string.Empty, string.Empty, Array.Empty<BattleEquippedItemSpec>(), Array.Empty<string>()),
                    new BattleParticipantSpec("enemy.swarm.4", "swarm-4", "hunter", DeploymentAnchorId.BackTop, string.Empty, string.Empty, Array.Empty<BattleEquippedItemSpec>(), Array.Empty<string>()),
                    new BattleParticipantSpec("enemy.swarm.5", "swarm-5", "raider", DeploymentAnchorId.BackCenter, string.Empty, string.Empty, Array.Empty<BattleEquippedItemSpec>(), Array.Empty<string>()),
                    new BattleParticipantSpec("enemy.swarm.6", "swarm-6", "priest", DeploymentAnchorId.BackBottom, string.Empty, string.Empty, Array.Empty<BattleEquippedItemSpec>(), Array.Empty<string>()),
                },
                TeamPostureType.CollapseWeakSide),
            _ => BattleEncounterPlans.CreateObserverSmokePlan(),
        };

        var build = BattleSetupBuilder.Build(Array.Empty<BattleParticipantSpec>(), plan, content);
        if (!build.IsSuccess)
        {
            throw new InvalidOperationException(build.Error ?? $"Threat scenario '{spec.ScenarioId}' enemy build failed.");
        }

        return build.Enemies;
    }

    private static IReadOnlyList<BattleUnitLoadout> BuildCatalogEncounterEnemies(CombatContentSnapshot content, string encounterId)
    {
        if (content.Encounters == null || !content.Encounters.TryGetValue(encounterId, out var encounter))
        {
            throw new InvalidOperationException($"Threat scenario references missing encounter '{encounterId}'.");
        }

        var chapterId = content.ExpeditionSites != null && content.ExpeditionSites.TryGetValue(encounter.SiteId, out var site)
            ? site.ChapterId
            : "topology";
        var context = new BattleContextState(
            chapterId,
            encounter.SiteId,
            0,
            encounter.Id,
            17,
            $"topology:{encounter.Id}",
            encounter.RewardSourceId,
            Math.Max(1, encounter.ThreatSkulls),
            encounter.Kind == EncounterKindValue.Boss,
            encounter.FactionId,
            encounter.BossOverlayId);
        var resolver = new EncounterResolutionService(content);
        if (!resolver.TryResolveEncounter(context, out var resolved, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return resolved.Enemies;
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

    private static IEnumerable<BalanceScenarioCoverageMatrixRow> BuildLoopDScenarioRows()
    {
        foreach (BalanceScenarioId scenarioId in Enum.GetValues(typeof(BalanceScenarioId)))
        {
            var sourceLane = scenarioId is BalanceScenarioId.Standard_BalancedMirror_3v3
                or BalanceScenarioId.Dive_vs_BacklinePeel
                or BalanceScenarioId.SustainBall_vs_BurstSpike
                or BalanceScenarioId.SwarmFlood_vs_Cleave
                or BalanceScenarioId.ArmorFrontline_vs_ArmorShred
                or BalanceScenarioId.ResistanceShell_vs_Exposure
                or BalanceScenarioId.GuardBulwark_vs_MultiHitBreak
                or BalanceScenarioId.MixedDraft_4v4
                    ? "loopd-purekit/systemic"
                    : "loopd-purekit";
            yield return new BalanceScenarioCoverageMatrixRow(
                scenarioId.ToString(),
                sourceLane,
                ResolveLoopDArchetypes(scenarioId),
                ResolveLoopDEncounterFamily(scenarioId),
                ResolveLoopDBuildLane(scenarioId),
                ResolveLoopDSynergyAxis(scenarioId),
                ResolveLoopDStatusFamily(scenarioId),
                "readability fatal/watchlist",
                ResolveLoopDKpiCoverage(scenarioId),
                scenarioId == BalanceScenarioId.MixedDraft_4v4 ? "ManualLoopD mirror/draw policy deferred" : "covered");
        }
    }

    private static string FormatArchetypes(IEnumerable<SweepHeroSpec> heroes)
    {
        return string.Join("/", heroes.Select(hero => hero.ArchetypeId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal));
    }

    private static string FormatSynergyAxis(IReadOnlyList<string> temporaryAugments, IReadOnlyList<string> permanentAugments)
    {
        var parts = temporaryAugments.Concat(permanentAugments).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        return parts.Length == 0 ? "none" : string.Join("/", parts);
    }

    private static string ResolveStatusFamily(ThreatPattern threatPattern)
    {
        return threatPattern switch
        {
            ThreatPattern.ControlChain => "control/tenacity",
            ThreatPattern.SustainBall => "heal/barrier",
            ThreatPattern.GuardBulwark => "guardbreak/barrier",
            ThreatPattern.ResistanceShell => "exposure/resistance",
            ThreatPattern.SwarmFlood => "summon/swarm",
            _ => "direct-damage",
        };
    }

    private static string ResolveReadabilityAsk(ThreatPattern threatPattern)
    {
        return threatPattern switch
        {
            ThreatPattern.DiveBackline => "backline access and peel clarity",
            ThreatPattern.SwarmFlood => "salience overload and cleave readability",
            ThreatPattern.ControlChain => "control chain clarity",
            ThreatPattern.SustainBall => "sustain source explainability",
            _ => "counter topology readability",
        };
    }

    private static string ResolveLoopDArchetypes(BalanceScenarioId scenarioId)
    {
        return scenarioId switch
        {
            BalanceScenarioId.Duel_MeleeMirror_1v1 => "slayer",
            BalanceScenarioId.Standard_BalancedMirror_3v3 => "marksman/priest/warden",
            BalanceScenarioId.MeleeCollapse_vs_RangerHold => "raider/reaver/slayer vs marksman/priest/warden",
            BalanceScenarioId.Dive_vs_BacklinePeel => "raider/reaver/scout vs bulwark/priest/warden",
            BalanceScenarioId.SustainBall_vs_BurstSpike => "priest/shaman/warden vs hexer/hunter/slayer",
            BalanceScenarioId.SwarmFlood_vs_Cleave => "hunter/scout/shaman vs marksman/priest/warden",
            BalanceScenarioId.ControlChain_vs_Tenacity => "hexer/shaman/warden vs priest/raider/warden",
            BalanceScenarioId.ArmorFrontline_vs_ArmorShred => "bulwark/guardian/priest vs marksman/raider/slayer",
            BalanceScenarioId.ResistanceShell_vs_Exposure => "hexer/priest/shaman vs hexer/scout/shaman",
            BalanceScenarioId.GuardBulwark_vs_MultiHitBreak => "bulwark/guardian/marksman vs priest/raider/scout",
            BalanceScenarioId.EvasiveSkirmish_vs_TrackingArea => "hunter/marksman/scout vs priest/shaman/warden",
            BalanceScenarioId.MixedDraft_4v4 => "marksman/priest/raider/warden vs bulwark/reaver/scout/shaman",
            _ => "unknown",
        };
    }

    private static string ResolveLoopDEncounterFamily(BalanceScenarioId scenarioId)
    {
        return scenarioId switch
        {
            BalanceScenarioId.Standard_BalancedMirror_3v3 or BalanceScenarioId.Duel_MeleeMirror_1v1 => "mirror",
            BalanceScenarioId.MixedDraft_4v4 => "mixed_draft",
            _ => scenarioId.ToString(),
        };
    }

    private static string ResolveLoopDBuildLane(BalanceScenarioId scenarioId)
    {
        return scenarioId switch
        {
            BalanceScenarioId.Dive_vs_BacklinePeel => "dive/peel",
            BalanceScenarioId.SustainBall_vs_BurstSpike => "sustain/burst",
            BalanceScenarioId.SwarmFlood_vs_Cleave => "swarm/cleave",
            BalanceScenarioId.ControlChain_vs_Tenacity => "control/tenacity",
            BalanceScenarioId.MixedDraft_4v4 => "mixed 4v4",
            _ => "combat topology",
        };
    }

    private static string ResolveLoopDSynergyAxis(BalanceScenarioId scenarioId)
    {
        return scenarioId == BalanceScenarioId.MixedDraft_4v4
            ? "2/4 + 2/3 grammar observation"
            : "first playable unit synergy";
    }

    private static string ResolveLoopDStatusFamily(BalanceScenarioId scenarioId)
    {
        return scenarioId switch
        {
            BalanceScenarioId.ControlChain_vs_Tenacity => "control/cleanse/tenacity",
            BalanceScenarioId.SustainBall_vs_BurstSpike => "heal/barrier/anti-heal",
            BalanceScenarioId.GuardBulwark_vs_MultiHitBreak => "guardbreak/barrier",
            BalanceScenarioId.ResistanceShell_vs_Exposure => "exposure/resistance",
            BalanceScenarioId.SwarmFlood_vs_Cleave => "summon/swarm",
            _ => "damage/targeting",
        };
    }

    private static string ResolveLoopDKpiCoverage(BalanceScenarioId scenarioId)
    {
        return scenarioId switch
        {
            BalanceScenarioId.Standard_BalancedMirror_3v3 => "average_battle_duration, first_signature_cast, readability_fatal",
            BalanceScenarioId.MixedDraft_4v4 => "timeout_rate, draw_policy_observation, readability_fatal",
            BalanceScenarioId.SustainBall_vs_BurstSpike => "average_battle_duration, first_signature_cast, synergy_uplift, readability_fatal",
            _ => "average_battle_duration, first_signature_cast, top_damage_share, readability_fatal, missing_explain_stamp",
        };
    }
}
