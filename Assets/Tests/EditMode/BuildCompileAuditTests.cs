using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Json;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BuildCompileAuditTests
{
    [Test]
    public void LoadoutCompiler_ChangesHash_WhenPassiveBoardOrPermanentAugmentChanges()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var baseSnapshot, out var error), Is.True, error);

        var passiveNode = new PassiveNodeTemplate(
            "node_attack_boost",
            new CombatModifierPackage(
                "node_attack_boost",
                ModifierSource.Other,
                new[] { new StatModifier(StatKey.Attack, ModifierOp.Flat, 2f, ModifierSource.Other, "node_attack_boost") }),
            new[] { "node:attack_boost" });
        var snapshot = baseSnapshot with
        {
            PassiveNodes = new Dictionary<string, PassiveNodeTemplate>(baseSnapshot.PassiveNodes, StringComparer.Ordinal)
            {
                [passiveNode.Id] = passiveNode
            }
        };

        var archetypeId = lookup.GetCanonicalArchetypeIds().First();
        Assert.That(lookup.TryGetArchetype(archetypeId, out var archetype), Is.True);
        Assert.That(lookup.TryGetTraitIds(archetypeId, out var positives, out var negatives), Is.True);
        var hero = new HeroRecord("hero-1", "Hero 1", archetypeId, archetype!.Race.Id, archetype.Class.Id, positives[0], negatives[0]);
        var blueprint = new SquadBlueprintState(
            "blueprint.default",
            "Default Build",
            TeamPostureType.StandardAdvance,
            string.Empty,
            new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.FrontCenter] = hero.Id },
            new[] { hero.Id },
            new Dictionary<string, string> { [hero.Id] = "anchor" });

        var compiler = new LoadoutCompiler();
        var baseLoadouts = new Dictionary<string, HeroLoadoutState>
        {
            [hero.Id] = new(hero.Id, Array.Empty<string>(), Array.Empty<string>(), string.Empty, Array.Empty<string>(), Array.Empty<string>())
        };
        var progressedLoadouts = new Dictionary<string, HeroLoadoutState>
        {
            [hero.Id] = new(hero.Id, Array.Empty<string>(), Array.Empty<string>(), "board.attack", new[] { passiveNode.Id }, Array.Empty<string>())
        };
        var overlay = new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty);

        var baseline = compiler.Compile(
            new[] { hero },
            baseLoadouts,
            new Dictionary<string, HeroProgressionState>(),
            new Dictionary<string, ItemInstanceState>(),
            new Dictionary<string, SkillInstanceState>(),
            new Dictionary<string, PassiveBoardSelectionState>(),
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            overlay,
            snapshot);
        var variant = compiler.Compile(
            new[] { hero },
            progressedLoadouts,
            new Dictionary<string, HeroProgressionState>(),
            new Dictionary<string, ItemInstanceState>(),
            new Dictionary<string, SkillInstanceState>(),
            new Dictionary<string, PassiveBoardSelectionState>
            {
                [hero.Id] = new(hero.Id, "board.attack", new[] { passiveNode.Id })
            },
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, new[] { "augment_perm_legacy_blade" }),
            blueprint,
            overlay,
            snapshot);

        Assert.That(baseline.CompileHash, Is.Not.EqualTo(variant.CompileHash));
        Assert.That(variant.Allies[0].Packages!.Any(package => package.Source == ModifierSource.Augment), Is.True);
        Assert.That(variant.Allies[0].Packages!.Any(package => package.Source == ModifierSource.Other), Is.True);
    }

    [Test]
    public void ReplayAssembler_IsDeterministic_ForSameSnapshotAndSeed()
    {
        var ally = CombatTestFactory.CreateUnit("ally.a", anchor: DeploymentAnchorId.FrontCenter);
        var enemy = CombatTestFactory.CreateUnit("enemy.a", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.BackCenter);
        var snapshot = new BattleLoadoutSnapshot(
            "snapshot:test",
            LoadoutCompiler.CurrentCompileVersion,
            "hash:test",
            new TeamTacticProfile("posture:standard", "Standard", TeamPostureType.StandardAdvance),
            new[] { ally },
            new[] { ally.Id },
            new[] { "human", "vanguard" });

        var resultA = BattleResolver.Run(CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: 31), 60);
        var resultB = BattleResolver.Run(CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: 31), 60);
        var replayA = ReplayAssembler.Assemble(snapshot, new[] { enemy }, resultA, 31, "2026-03-30T00:00:00Z", "2026-03-30T00:01:00Z");
        var replayB = ReplayAssembler.Assemble(snapshot, new[] { enemy }, resultB, 31, "2026-03-30T00:00:00Z", "2026-03-30T00:01:00Z");

        Assert.That(replayA.Header.FinalStateHash, Is.EqualTo(replayB.Header.FinalStateHash));
        Assert.That(replayA.EventStream.Select(@event => @event.LogCode).ToArray(), Is.EqualTo(replayB.EventStream.Select(@event => @event.LogCode).ToArray()));
        Assert.That(replayA.Keyframes.Select(frame => frame.StateHash).ToArray(), Is.EqualTo(replayB.Keyframes.Select(frame => frame.StateHash).ToArray()));
    }

    [Test]
    public void LoadoutCompiler_ChangesHash_WhenManaEnvelopeOrSkillCooldownChanges()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var baseSnapshot, out var error), Is.True, error);

        var archetypeId = lookup.GetCanonicalArchetypeIds().First();
        Assert.That(lookup.TryGetArchetype(archetypeId, out var archetypeDefinition), Is.True);
        Assert.That(lookup.TryGetTraitIds(archetypeId, out var positives, out var negatives), Is.True);
        var hero = new HeroRecord("hero-2", "Hero 2", archetypeId, archetypeDefinition!.Race.Id, archetypeDefinition.Class.Id, positives[0], negatives[0]);
        var blueprint = new SquadBlueprintState(
            "blueprint.mana",
            "Mana Build",
            TeamPostureType.StandardAdvance,
            string.Empty,
            new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.BackCenter] = hero.Id },
            new[] { hero.Id },
            new Dictionary<string, string> { [hero.Id] = "carry" });
        var compiler = new LoadoutCompiler();
        var overlay = new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty);

        var baseline = compiler.Compile(
            new[] { hero },
            new Dictionary<string, HeroLoadoutState>(),
            new Dictionary<string, HeroProgressionState>(),
            new Dictionary<string, ItemInstanceState>(),
            new Dictionary<string, SkillInstanceState>(),
            new Dictionary<string, PassiveBoardSelectionState>(),
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            overlay,
            baseSnapshot);

        var firstArchetype = baseSnapshot.Archetypes[archetypeId];
        var variantSnapshot = baseSnapshot with
        {
            Archetypes = new Dictionary<string, CombatArchetypeTemplate>(baseSnapshot.Archetypes, StringComparer.Ordinal)
            {
                [archetypeId] = firstArchetype with
                {
                    Mana = new ManaEnvelope(30f, 3f, 4f),
                    Skills = firstArchetype.Skills.Select(skill => skill with { BaseCooldownSeconds = 2.5f, CastWindupSeconds = 0.4f }).ToList()
                }
            }
        };

        var variant = compiler.Compile(
            new[] { hero },
            new Dictionary<string, HeroLoadoutState>(),
            new Dictionary<string, HeroProgressionState>(),
            new Dictionary<string, ItemInstanceState>(),
            new Dictionary<string, SkillInstanceState>(),
            new Dictionary<string, PassiveBoardSelectionState>(),
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            overlay,
            variantSnapshot);

        Assert.That(variant.CompileHash, Is.Not.EqualTo(baseline.CompileHash));
        Assert.That(variant.Allies[0].EffectiveMana.Max, Is.EqualTo(30f));
        Assert.That(variant.Allies[0].Skills.All(skill => skill.BaseCooldownSeconds == 2.5f), Is.True);
    }

    [Test]
    public void ActiveRun_RoundTrips_WithCompileHash_AndCombatAssemblyDoesNotReferencePersistence()
    {
        var session = new GameSessionState(new RuntimeCombatContentLookup());
        session.BindProfile(new SM.Persistence.Abstractions.Models.SaveProfile());
        session.BeginNewExpedition();
        var snapshot = session.BuildBattleLoadoutSnapshot();

        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sm_build_compile_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new JsonSaveRepository(root);
            repo.Save(session.Profile);
            var loaded = repo.LoadOrCreate("default");

            var resumed = new GameSessionState(new RuntimeCombatContentLookup());
            resumed.BindProfile(loaded);

            Assert.That(resumed.ActiveRun, Is.Not.Null);
            Assert.That(resumed.ActiveRun!.Overlay.LastCompileHash, Is.EqualTo(snapshot.CompileHash));
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, true);
            }
        }

        var references = typeof(BattleResolver).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.That(references.Any(name => name != null && name.StartsWith("SM.Persistence", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void LoadoutCompiler_NormalizesSkillSlots_AndKeepsOneSkillPerCompiledSlot()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var baseSnapshot, out var error), Is.True, error);

        var archetypeId = lookup.GetCanonicalArchetypeIds().First();
        Assert.That(lookup.TryGetArchetype(archetypeId, out var archetypeDefinition), Is.True);
        Assert.That(lookup.TryGetTraitIds(archetypeId, out var positives, out var negatives), Is.True);
        var hero = new HeroRecord("hero-slots", "Hero Slots", archetypeId, archetypeDefinition!.Race.Id, archetypeDefinition.Class.Id, positives[0], negatives[0]);
        var blueprint = new SquadBlueprintState(
            "blueprint.slots",
            "Slot Build",
            TeamPostureType.StandardAdvance,
            string.Empty,
            new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.FrontCenter] = hero.Id },
            new[] { hero.Id },
            new Dictionary<string, string> { [hero.Id] = "anchor" });

        var slotSkills = new[]
        {
            new BattleSkillSpec("skill.slot.legacy_core", "Legacy Core", SkillKind.Strike, 2f, 1f, CompiledSkillSlots.CoreActive),
            new BattleSkillSpec("skill.slot.core", "Core", SkillKind.Strike, 2f, 1f, CompiledSkillSlots.CoreActive),
            new BattleSkillSpec("skill.slot.utility", "Utility", SkillKind.Utility, 0f, 1f, CompiledSkillSlots.UtilityActive),
            new BattleSkillSpec("skill.slot.passive", "Passive", SkillKind.Buff, 0f, 0f, CompiledSkillSlots.Passive),
            new BattleSkillSpec("skill.slot.support", "Support", SkillKind.Buff, 0f, 0f, CompiledSkillSlots.Support),
        };

        var variantSnapshot = baseSnapshot with
        {
            Archetypes = new Dictionary<string, CombatArchetypeTemplate>(baseSnapshot.Archetypes, StringComparer.Ordinal)
            {
                [archetypeId] = baseSnapshot.Archetypes[archetypeId] with { Skills = Array.Empty<BattleSkillSpec>() }
            },
            SkillCatalog = slotSkills.ToDictionary(skill => skill.Id, skill => skill, StringComparer.Ordinal)
        };

        var heroLoadouts = new Dictionary<string, HeroLoadoutState>
        {
            [hero.Id] = new(hero.Id, Array.Empty<string>(), new[] { "inst-legacy", "inst-core", "inst-utility", "inst-passive", "inst-support" }, string.Empty, Array.Empty<string>(), Array.Empty<string>())
        };
        var skillInstances = new Dictionary<string, SkillInstanceState>
        {
            ["inst-legacy"] = new("inst-legacy", "skill.slot.legacy_core", "active_core", Array.Empty<string>()),
            ["inst-core"] = new("inst-core", "skill.slot.core", CompiledSkillSlots.CoreActive, Array.Empty<string>()),
            ["inst-utility"] = new("inst-utility", "skill.slot.utility", CompiledSkillSlots.UtilityActive, Array.Empty<string>()),
            ["inst-passive"] = new("inst-passive", "skill.slot.passive", CompiledSkillSlots.Passive, Array.Empty<string>()),
            ["inst-support"] = new("inst-support", "skill.slot.support", CompiledSkillSlots.Support, Array.Empty<string>()),
        };

        var compiler = new LoadoutCompiler();
        var snapshot = compiler.Compile(
            new[] { hero },
            heroLoadouts,
            new Dictionary<string, HeroProgressionState>(),
            new Dictionary<string, ItemInstanceState>(),
            skillInstances,
            new Dictionary<string, PassiveBoardSelectionState>(),
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            variantSnapshot);

        var slotKinds = snapshot.Allies[0].Skills.Select(skill => skill.SlotKind).ToArray();
        Assert.That(slotKinds, Is.EqualTo(new[]
        {
            CompiledSkillSlots.CoreActive,
            CompiledSkillSlots.UtilityActive,
            CompiledSkillSlots.Passive,
            CompiledSkillSlots.Support
        }));
        Assert.That(snapshot.Allies[0].Skills.Count, Is.EqualTo(4));
    }

    [Test]
    public void LoadoutCompiler_ChangesHash_WhenSkillMetadataChanges()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var baseSnapshot, out var error), Is.True, error);

        var archetypeId = lookup.GetCanonicalArchetypeIds().First();
        Assert.That(lookup.TryGetArchetype(archetypeId, out var archetypeDefinition), Is.True);
        Assert.That(lookup.TryGetTraitIds(archetypeId, out var positives, out var negatives), Is.True);
        var hero = new HeroRecord("hero-metadata", "Hero Metadata", archetypeId, archetypeDefinition!.Race.Id, archetypeDefinition.Class.Id, positives[0], negatives[0]);
        var blueprint = new SquadBlueprintState(
            "blueprint.metadata",
            "Metadata Build",
            TeamPostureType.StandardAdvance,
            string.Empty,
            new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.BackCenter] = hero.Id },
            new[] { hero.Id },
            new Dictionary<string, string> { [hero.Id] = "carry" });
        var compiler = new LoadoutCompiler();
        var overlay = new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty);

        var baseline = compiler.Compile(
            new[] { hero },
            new Dictionary<string, HeroLoadoutState>(),
            new Dictionary<string, HeroProgressionState>(),
            new Dictionary<string, ItemInstanceState>(),
            new Dictionary<string, SkillInstanceState>(),
            new Dictionary<string, PassiveBoardSelectionState>(),
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            overlay,
            baseSnapshot);

        var firstArchetype = baseSnapshot.Archetypes[archetypeId];
        var firstSkill = firstArchetype.Skills.First();
        var variantSnapshot = baseSnapshot with
        {
            Archetypes = new Dictionary<string, CombatArchetypeTemplate>(baseSnapshot.Archetypes, StringComparer.Ordinal)
            {
                [archetypeId] = firstArchetype with
                {
                    Skills = firstArchetype.Skills
                        .Select(skill => skill.Id == firstSkill.Id
                            ? skill with
                            {
                                Delivery = SkillDelivery.Projectile,
                                TargetRule = SkillTargetRule.MarkedTarget,
                                HealthCoeff = skill.HealthCoeff + 0.5f,
                                CanCrit = !skill.CanCrit
                            }
                            : skill)
                        .ToList()
                }
            },
            SkillCatalog = new Dictionary<string, BattleSkillSpec>(baseSnapshot.SkillCatalog, StringComparer.Ordinal)
            {
                [firstSkill.Id] = firstSkill with
                {
                    Delivery = SkillDelivery.Projectile,
                    TargetRule = SkillTargetRule.MarkedTarget,
                    HealthCoeff = firstSkill.HealthCoeff + 0.5f,
                    CanCrit = !firstSkill.CanCrit
                }
            }
        };

        var variant = compiler.Compile(
            new[] { hero },
            new Dictionary<string, HeroLoadoutState>(),
            new Dictionary<string, HeroProgressionState>(),
            new Dictionary<string, ItemInstanceState>(),
            new Dictionary<string, SkillInstanceState>(),
            new Dictionary<string, PassiveBoardSelectionState>(),
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            overlay,
            variantSnapshot);

        Assert.That(variant.CompileHash, Is.Not.EqualTo(baseline.CompileHash));
    }

    [Test]
    public void PassiveBoardDefinition_MigratesLegacyArchetypeId_ToClassId()
    {
        const string folderPath = "Assets/Tests/EditMode/Temp";
        const string assetPath = "Assets/Tests/EditMode/Temp/passiveboard_migration.asset";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/Tests/EditMode", "Temp");
        }

        try
        {
            var asset = ScriptableObject.CreateInstance<SM.Content.Definitions.PassiveBoardDefinition>();
            asset.Id = "board.mystic";
            asset.NameKey = "content.passive_board.board_mystic.name";
            asset.DescriptionKey = "content.passive_board.board_mystic.desc";
            asset.ClassId = "mystic";
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();

            var yaml = File.ReadAllText(assetPath).Replace("ClassId: mystic", "ArchetypeId: mystic");
            File.WriteAllText(assetPath, yaml);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var reloaded = AssetDatabase.LoadAssetAtPath<SM.Content.Definitions.PassiveBoardDefinition>(assetPath)
                ?? AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<SM.Content.Definitions.PassiveBoardDefinition>().FirstOrDefault();
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded!.ClassId, Is.EqualTo("mystic"));
        }
        finally
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }
}
