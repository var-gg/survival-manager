using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

public sealed class LoadoutCompilerClosureTests
{
    private sealed record HeroSpec(
        string HeroId,
        string ArchetypeId,
        DeploymentAnchorId Anchor,
        string RoleInstructionId,
        IReadOnlyList<string> PassiveNodeIds,
        IReadOnlyList<string> ItemIds,
        IReadOnlyList<string> AffixIds,
        IReadOnlyList<SkillInstanceState>? SkillInstances = null);

    private sealed record SquadSpec(
        string BlueprintId,
        string TeamTacticId,
        IReadOnlyList<string> TemporaryAugmentIds,
        IReadOnlyList<string> PermanentAugmentIds,
        IReadOnlyList<HeroSpec> Heroes);

    [Test]
    public void LoadoutCompiler_SameScenarioTwice_ProducesStableHashAndCanonicalSkillOrder()
    {
        var compiler = new LoadoutCompiler();
        var content = BuildContentSnapshot();
        var first = CompileSquad(compiler, content, BuildBaselineSpec());
        var second = CompileSquad(compiler, content, BuildBaselineSpec());

        Assert.That(first.CompileHash, Is.EqualTo(second.CompileHash));
        Assert.That(first.Allies.Select(ally => ally.PreferredAnchor), Is.EqualTo(new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.BackTop,
        }));
        Assert.That(first.Allies.All(ally => ally.Skills.Select(skill => skill.SlotKind).SequenceEqual(CompiledSkillSlots.Ordered)), Is.True);
    }

    [Test]
    public void LoadoutCompiler_ChangingTeamTacticOrRoleInstructionChangesHash()
    {
        var compiler = new LoadoutCompiler();
        var content = BuildContentSnapshot();
        var baseline = CompileSquad(compiler, content, BuildBaselineSpec());
        var changedTactic = CompileSquad(compiler, content, BuildBaselineSpec(teamTacticId: "team_tactic_hold_line"));
        var changedRole = CompileSquad(compiler, content, BuildBaselineSpec(roleOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hero.warden"] = "support",
        }));

        Assert.That(changedTactic.CompileHash, Is.Not.EqualTo(baseline.CompileHash));
        Assert.That(changedRole.CompileHash, Is.Not.EqualTo(baseline.CompileHash));
    }

    [Test]
    public void LoadoutCompiler_NormalizesLegacyStatAliasAndSkillSlot()
    {
        var compiler = new LoadoutCompiler();
        var content = BuildContentSnapshot();
        var snapshot = CompileSquad(compiler, content, BuildAliasNormalizationSpec());

        var ally = snapshot.Allies.Single();
        Assert.That(ally.NumericPackages.SelectMany(package => package.Modifiers).Any(modifier =>
            modifier.SourceId == "affix.attack_alias" && modifier.Stat == StatKey.PhysPower), Is.True);
        Assert.That(ally.Skills.Select(skill => skill.SlotKind), Is.EqualTo(CompiledSkillSlots.Ordered));
        Assert.That(ally.Skills.Any(skill => string.Equals(skill.SlotKind, "active_core", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void LoadoutCompiler_ThrowsWhenCanonicalSkillSlotMissing()
    {
        var compiler = new LoadoutCompiler();
        var content = BuildContentSnapshot();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CompileSquad(compiler, content, BuildMissingSlotSpec()));
        Assert.That(exception!.Message, Does.Contain("Missing"));
    }

    [Test]
    public void LoadoutCompiler_ProvenanceIncludesExpectedSourceKinds()
    {
        var compiler = new LoadoutCompiler();
        var content = BuildContentSnapshot();
        var snapshot = CompileSquad(compiler, content, BuildBaselineSpec());
        var artifactKinds = snapshot.Provenance!.Select(entry => entry.ArtifactKind).ToHashSet(StringComparer.Ordinal);

        Assert.That(artifactKinds, Contains.Item("archetype_base"));
        Assert.That(artifactKinds, Contains.Item("item"));
        Assert.That(artifactKinds, Contains.Item("affix"));
        Assert.That(artifactKinds, Contains.Item("augment_temporary"));
        Assert.That(artifactKinds, Contains.Item("augment_permanent"));
        Assert.That(artifactKinds, Contains.Item("passive_numeric"));
        Assert.That(artifactKinds, Contains.Item("team_tactic"));
        Assert.That(artifactKinds, Contains.Item("role_instruction"));
        Assert.That(artifactKinds, Contains.Item("skill_slot"));
        Assert.That(artifactKinds, Contains.Item("team_numeric"));
        Assert.That(snapshot.Provenance.Any(entry => entry.Source == ModifierSource.Synergy && entry.ArtifactKind == "team_numeric"), Is.True);
    }

    private static BattleLoadoutSnapshot CompileSquad(LoadoutCompiler compiler, CombatContentSnapshot content, SquadSpec spec)
    {
        var heroes = new List<HeroRecord>(spec.Heroes.Count);
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var skillInstances = new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal);
        var passiveSelections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        var deploymentAssignments = new Dictionary<DeploymentAnchorId, string>();
        var heroRoleIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var heroSpec in spec.Heroes)
        {
            var archetype = content.Archetypes[heroSpec.ArchetypeId];
            heroes.Add(new HeroRecord(
                heroSpec.HeroId,
                heroSpec.HeroId,
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                string.Empty,
                string.Empty));

            var itemInstanceIds = new List<string>();
            for (var index = 0; index < heroSpec.ItemIds.Count; index++)
            {
                var itemInstanceId = $"{heroSpec.HeroId}.item.{index}";
                itemInstances[itemInstanceId] = new ItemInstanceState(itemInstanceId, heroSpec.ItemIds[index], heroSpec.AffixIds, heroSpec.HeroId);
                itemInstanceIds.Add(itemInstanceId);
            }

            var skillInstanceIds = new List<string>();
            if (heroSpec.SkillInstances != null)
            {
                foreach (var skillInstance in heroSpec.SkillInstances)
                {
                    skillInstances[skillInstance.SkillInstanceId] = skillInstance;
                    skillInstanceIds.Add(skillInstance.SkillInstanceId);
                }
            }

            heroLoadouts[heroSpec.HeroId] = new HeroLoadoutState(
                heroSpec.HeroId,
                itemInstanceIds,
                skillInstanceIds,
                $"board.{archetype.ClassId}",
                heroSpec.PassiveNodeIds,
                spec.PermanentAugmentIds);
            heroProgressions[heroSpec.HeroId] = new HeroProgressionState(
                heroSpec.HeroId,
                1,
                0,
                heroSpec.PassiveNodeIds,
                archetype.Skills.Select(skill => skill.Id).ToList());
            passiveSelections[heroSpec.HeroId] = new PassiveBoardSelectionState(heroSpec.HeroId, $"board.{archetype.ClassId}", heroSpec.PassiveNodeIds);
            deploymentAssignments[heroSpec.Anchor] = heroSpec.HeroId;
            heroRoleIds[heroSpec.HeroId] = heroSpec.RoleInstructionId;
        }

        return compiler.Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            itemInstances,
            skillInstances,
            passiveSelections,
            new PermanentAugmentLoadoutState(spec.BlueprintId, spec.PermanentAugmentIds),
            new SquadBlueprintState(
                spec.BlueprintId,
                spec.BlueprintId,
                spec.TeamTacticId == "team_tactic_hold_line" ? TeamPostureType.HoldLine : TeamPostureType.StandardAdvance,
                spec.TeamTacticId,
                deploymentAssignments,
                heroes.Select(hero => hero.Id).ToList(),
                heroRoleIds),
            new RunOverlayState(0, spec.TemporaryAugmentIds, Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }

    private static SquadSpec BuildBaselineSpec(string teamTacticId = "team_tactic_standard_advance", IReadOnlyDictionary<string, string>? roleOverrides = null)
    {
        roleOverrides ??= new Dictionary<string, string>(StringComparer.Ordinal);
        return new SquadSpec(
            "baseline",
            teamTacticId,
            new[] { "augment.temp" },
            new[] { "augment.perm" },
            new HeroSpec[]
            {
                new(
                    "hero.warden",
                    "warden",
                    DeploymentAnchorId.FrontCenter,
                    roleOverrides.TryGetValue("hero.warden", out var wardenRole) ? wardenRole : "anchor",
                    new[] { "node.vanguard.small" },
                    new[] { "item.shield" },
                    new[] { "affix.attack_alias" }),
                new(
                    "hero.raider",
                    "raider",
                    DeploymentAnchorId.BackTop,
                    roleOverrides.TryGetValue("hero.raider", out var raiderRole) ? raiderRole : "carry",
                    new[] { "node.duelist.small" },
                    new[] { "item.blade" },
                    new[] { "affix.speed" }),
            });
    }

    private static SquadSpec BuildAliasNormalizationSpec()
    {
        return new SquadSpec(
            "alias-normalization",
            "team_tactic_standard_advance",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new HeroSpec(
                    "hero.warden",
                    "warden",
                    DeploymentAnchorId.FrontCenter,
                    "anchor",
                    new[] { "node.vanguard.small" },
                    new[] { "item.shield" },
                    new[] { "affix.attack_alias" },
                    new[]
                    {
                        new SkillInstanceState("hero.warden.skill.0", "skill.warden.core", "active_core", Array.Empty<string>()),
                        new SkillInstanceState("hero.warden.skill.1", "skill.warden.utility", "utility_active", Array.Empty<string>()),
                        new SkillInstanceState("hero.warden.skill.2", "skill.vanguard.passive", "passive", Array.Empty<string>()),
                        new SkillInstanceState("hero.warden.skill.3", "skill.vanguard.support", "support", Array.Empty<string>()),
                    })
            });
    }

    private static SquadSpec BuildMissingSlotSpec()
    {
        return new SquadSpec(
            "missing-slot",
            "team_tactic_standard_advance",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new HeroSpec(
                    "hero.warden",
                    "warden",
                    DeploymentAnchorId.FrontCenter,
                    "anchor",
                    new[] { "node.vanguard.small" },
                    new[] { "item.shield" },
                    new[] { "affix.attack_alias" },
                    new[]
                    {
                        new SkillInstanceState("hero.warden.skill.0", "skill.warden.core", "active_core", Array.Empty<string>()),
                        new SkillInstanceState("hero.warden.skill.1", "skill.warden.utility", "utility_active", Array.Empty<string>()),
                        new SkillInstanceState("hero.warden.skill.2", "skill.vanguard.passive", "passive", Array.Empty<string>()),
                    })
            });
    }

    private static CombatContentSnapshot BuildContentSnapshot()
    {
        var attackAlias = ResolveStatKey("attack");
        var baseRules = new[]
        {
            new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
        };

        var wardenSkills = new[]
        {
            CreateSkill("skill.warden.core", CompiledSkillSlots.CoreActive, 4.5f),
            CreateSkill("skill.warden.utility", CompiledSkillSlots.UtilityActive, 0f, SkillKind.Utility),
            CreateSkill("skill.vanguard.passive", CompiledSkillSlots.Passive, 0f, SkillKind.Buff),
            CreateSkill("skill.vanguard.support", CompiledSkillSlots.Support, 0f, SkillKind.Buff),
        };
        var raiderSkills = new[]
        {
            CreateSkill("skill.raider.core", CompiledSkillSlots.CoreActive, 4.2f),
            CreateSkill("skill.raider.utility", CompiledSkillSlots.UtilityActive, 0f, SkillKind.Utility),
            CreateSkill("skill.duelist.passive", CompiledSkillSlots.Passive, 0f, SkillKind.Buff),
            CreateSkill("skill.duelist.support", CompiledSkillSlots.Support, 0f, SkillKind.Buff),
        };

        return new CombatContentSnapshot(
            new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal)
            {
                ["warden"] = new CombatArchetypeTemplate(
                    "warden",
                    "Warden",
                    "human",
                    "vanguard",
                    DeploymentAnchorId.FrontCenter,
                    CreateBaseStats(18f, 3f, 2.2f),
                    baseRules,
                    wardenSkills,
                    "anchor",
                    0.25f,
                    1.2f,
                    new ManaEnvelope(0f, 0f, 0f)),
                ["raider"] = new CombatArchetypeTemplate(
                    "raider",
                    "Raider",
                    "human",
                    "duelist",
                    DeploymentAnchorId.BackTop,
                    CreateBaseStats(14f, 1.2f, 3f),
                    baseRules,
                    raiderSkills,
                    "carry",
                    1.2f,
                    0.5f,
                    new ManaEnvelope(0f, 0f, 0f)),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal)
            {
                ["item.shield"] = new CombatModifierPackage("item.shield", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 3f, ModifierSource.Item, "item.shield"),
                }),
                ["item.blade"] = new CombatModifierPackage("item.blade", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.PhysPower, ModifierOp.Flat, 2f, ModifierSource.Item, "item.blade"),
                }),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal)
            {
                ["affix.attack_alias"] = new CombatModifierPackage("affix.attack_alias", ModifierSource.Item, new[]
                {
                    new StatModifier(attackAlias, ModifierOp.Flat, 1f, ModifierSource.Item, "affix.attack_alias"),
                }),
                ["affix.speed"] = new CombatModifierPackage("affix.speed", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.AttackSpeed, ModifierOp.Flat, 0.1f, ModifierSource.Item, "affix.speed"),
                }),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal)
            {
                ["augment.temp"] = new CombatModifierPackage("augment.temp", ModifierSource.Augment, new[]
                {
                    new StatModifier(StatKey.AttackSpeed, ModifierOp.Flat, 0.15f, ModifierSource.Augment, "augment.temp"),
                }),
                ["augment.perm"] = new CombatModifierPackage("augment.perm", ModifierSource.Augment, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 2f, ModifierSource.Augment, "augment.perm"),
                }),
            },
            wardenSkills.Concat(raiderSkills).ToDictionary(skill => skill.Id, skill => skill, StringComparer.Ordinal),
            new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal)
            {
                ["team_tactic_standard_advance"] = new TeamTacticTemplate("team_tactic_standard_advance", new TeamTacticProfile("team_tactic_standard_advance", "Standard", TeamPostureType.StandardAdvance, 1f, 0f, 0f, 0f, 0f, 0f)),
                ["team_tactic_hold_line"] = new TeamTacticTemplate("team_tactic_hold_line", new TeamTacticProfile("team_tactic_hold_line", "Hold", TeamPostureType.HoldLine, 0.92f, 0.02f, -0.08f, 0.1f, 0.55f, 0.12f)),
            },
            new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal)
            {
                ["anchor"] = new RoleInstructionTemplate("anchor", new SlotRoleInstruction(DeploymentAnchorId.FrontCenter, "anchor", 0.8f, 0.05f, 0.02f)),
                ["support"] = new RoleInstructionTemplate("support", new SlotRoleInstruction(DeploymentAnchorId.FrontCenter, "support", 0.45f, 0.02f, 0.25f)),
                ["carry"] = new RoleInstructionTemplate("carry", new SlotRoleInstruction(DeploymentAnchorId.BackTop, "carry", -0.05f, 0.15f, 0.45f)),
            },
            new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal)
            {
                ["node.vanguard.small"] = new PassiveNodeTemplate(
                    "node.vanguard.small",
                    new CombatModifierPackage("node.vanguard.small", ModifierSource.Other, new[]
                    {
                        new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 2f, ModifierSource.Other, "node.vanguard.small"),
                    }),
                    new[] { "frontline" }),
                ["node.duelist.small"] = new PassiveNodeTemplate(
                    "node.duelist.small",
                    new CombatModifierPackage("node.duelist.small", ModifierSource.Other, new[]
                    {
                        new StatModifier(StatKey.PhysPower, ModifierOp.Flat, 1f, ModifierSource.Other, "node.duelist.small"),
                    }),
                    new[] { "carry" }),
            },
            new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal)
            {
                ["augment.temp"] = new AugmentCatalogEntry("augment.temp", "combat", "temp_family", 1, false, false, new[] { "anchor" }, Array.Empty<string>(), new CombatModifierPackage("augment.temp", ModifierSource.Augment, new[]
                {
                    new StatModifier(StatKey.AttackSpeed, ModifierOp.Flat, 0.15f, ModifierSource.Augment, "augment.temp"),
                })),
                ["augment.perm"] = new AugmentCatalogEntry("augment.perm", "combat", "perm_family", 1, true, false, new[] { "support" }, Array.Empty<string>(), new CombatModifierPackage("augment.perm", ModifierSource.Augment, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 2f, ModifierSource.Augment, "augment.perm"),
                })),
            },
            new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal)
            {
                ["human:2"] = new SynergyTierTemplate("human:2", new TeamSynergyTierRule("human", "human", 2, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 1f, ModifierSource.Synergy, "human:2"),
                })),
            },
            new Dictionary<string, IReadOnlyList<BattleSkillSpec>>(StringComparer.Ordinal));
    }

    private static BattleSkillSpec CreateSkill(string id, string slotKind, float power, SkillKind kind = SkillKind.Strike)
    {
        return new BattleSkillSpec(
            id,
            id,
            kind,
            power,
            1.5f,
            slotKind,
            Array.Empty<string>(),
            kind == SkillKind.Utility || kind == SkillKind.Buff ? DamageType.Healing : DamageType.Physical,
            power,
            1f,
            0f,
            kind == SkillKind.Buff ? 0.5f : 0f,
            0f,
            1.2f,
            0.2f,
            Array.Empty<string>(),
            0f,
            false,
            kind == SkillKind.Utility || kind == SkillKind.Buff ? SkillDelivery.Aura : SkillDelivery.Melee,
            kind == SkillKind.Buff ? SkillTargetRule.Self : SkillTargetRule.NearestEnemy,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static Dictionary<StatKey, float> CreateBaseStats(float health, float armor, float attackSpeed)
    {
        return new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = health,
            [StatKey.Armor] = armor,
            [StatKey.AttackSpeed] = attackSpeed,
            [StatKey.AttackRange] = 1.5f,
            [StatKey.AttackWindup] = 0.2f,
            [StatKey.AttackCooldown] = 1f,
            [StatKey.LeashDistance] = 5f,
            [StatKey.TargetSwitchDelay] = 0.2f,
            [StatKey.PhysPower] = 4f,
        };
    }

    private static StatKey ResolveStatKey(string statId)
    {
        Assert.That(StatKey.TryResolve(statId, out var key, out _), Is.True);
        return key;
    }
}
