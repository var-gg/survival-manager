using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Numerics;
using SM.Core.Stats;
using SM.Editor.SeedData;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class DuelistBuildContentWitnessTests
{
    private static readonly string[] BehaviorTagIds =
    {
        "duelist_dive_commit",
        "duelist_hold_bruiser",
        "duelist_peel",
        "execute_low_hp",
        "dive_assassin_keystone",
    };

    private static readonly Dictionary<string, string> RuleTagsByNode = new(StringComparer.Ordinal)
    {
        ["passive_duelist_notable_01"] = "duelist_dive_commit",
        ["passive_duelist_notable_02"] = "duelist_hold_bruiser",
        ["passive_duelist_notable_03"] = "execute_low_hp",
        ["passive_duelist_notable_06"] = "duelist_peel",
        ["passive_duelist_keystone_01"] = "dive_assassin_keystone",
    };

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(DuelistBuildContentWitnessTests));
    }

    [Test]
    public void RatifiedBalance_UsesExactClassBaselineAndPointNineEightBuildGate()
    {
        var classAsset = AssetDatabase.LoadAssetAtPath<ClassDefinition>(
            "Assets/Resources/_Game/Content/Definitions/Classes/class_duelist.asset");
        Assert.That(classAsset, Is.Not.Null);
        Assert.That(classAsset!.BaselineDamageMultiplierPercent, Is.EqualTo(102));
        Assert.That(classAsset.BaseCritChance, Is.EqualTo(0.12f));
        Assert.That(classAsset.CritMultiplierCap, Is.EqualTo(1.85f));

        var content = new RuntimeCombatContentLookup().Snapshot;
        var classPackage = content.Archetypes["slayer"].ClassStatPackage;
        Assert.That(classPackage, Is.Not.Null);
        var baseline = classPackage!.Modifiers.Single(modifier =>
            modifier.Stat == StatKey.PhysPower && modifier.Op == ModifierOp.More);
        const int expectedMultiplierRaw = Fixed32.OneRaw * 102 / 100;
        Assert.That(
            Fixed32.OneRaw + Fixed32.FromFloatQuantized(baseline.Value).Raw,
            Is.EqualTo(expectedMultiplierRaw),
            "Duelist baseline must use the exact 65536*102/100 multiplier.");

        var rangerClassPackage = content.Archetypes["hunter"].ClassStatPackage;
        Assert.That(rangerClassPackage, Is.Not.Null);
        Assert.That(rangerClassPackage!.Modifiers.Any(modifier =>
            modifier.Stat == StatKey.PhysPower && modifier.Op == ModifierOp.More), Is.False);

        var gate = content.PassiveNodes["passive_duelist_notable_01"].Package.Modifiers.Single(modifier =>
            modifier.Stat == StatKey.MaxHealth && modifier.Op == ModifierOp.More);
        Assert.That(gate.Value, Is.EqualTo(-0.02f));
        Assert.That(classPackage.Modifiers.Any(modifier => modifier.Stat == StatKey.AttackSpeed), Is.False);
    }

    [Test]
    public void Board_HasExactlyTwentyFourReauthoredNodes_AndSixStableTags()
    {
        var board = AssetDatabase.LoadAssetAtPath<PassiveBoardDefinition>(
            $"{SampleSeedGenerator.ResourcesRoot}/PassiveBoards/board_duelist.asset");
        Assert.That(board, Is.Not.Null);
        Assert.That(board!.Nodes.Count, Is.EqualTo(24));
        Assert.That(board.Nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(24));

        var allRuleTags = board.Nodes
            .SelectMany(node => node.RuleModifierTags)
            .Select(tag => tag.Id)
            .ToArray();
        Assert.That(allRuleTags.OrderBy(id => id, StringComparer.Ordinal),
            Is.EqualTo(BehaviorTagIds.OrderBy(id => id, StringComparer.Ordinal)));

        foreach (var pair in RuleTagsByNode)
        {
            var node = board.Nodes.Single(entry => entry.Id == pair.Key);
            Assert.That(node.RuleModifierTags.Select(tag => tag.Id), Is.EqualTo(new[] { pair.Value }), pair.Key);
        }

        foreach (var gateId in new[] { "passive_duelist_notable_01", "passive_duelist_notable_02" })
        {
            var gate = board.Nodes.Single(node => node.Id == gateId);
            Assert.That(gate.MutualExclusionTags.Select(tag => tag.Id),
                Is.EqualTo(new[] { "tag_duelist_build_gate" }));
        }

        var stableTagIds = BehaviorTagIds.Append("tag_duelist_build_gate").ToArray();
        foreach (var tagId in stableTagIds)
        {
            var tag = AssetDatabase.LoadAssetAtPath<StableTagDefinition>(
                $"{SampleSeedGenerator.ResourcesRoot}/StableTags/tag_{tagId}.asset");
            Assert.That(tag, Is.Not.Null, tagId);
            Assert.That(tag!.Id, Is.EqualTo(tagId));
        }
    }

    [Test]
    public void NewGhostSkills_CarryExactSunderAndLastBastionPayloads()
    {
        var sunder = LoadSkill("skill_sunder_rhythm");
        Assert.That(sunder.SlotKind, Is.EqualTo(SkillSlotKindValue.Support));
        Assert.That(sunder.SupportAllowedTags.Select(tag => tag.Id), Is.EqualTo(new[] { "strike" }));
        Assert.That(sunder.SupportBlockedTags.Select(tag => tag.Id), Does.Contain("dash"));
        var sunderStatus = sunder.SupportModifier.AddedStatuses.Single();
        Assert.That(sunderStatus.StatusId, Is.EqualTo("sunder"));
        Assert.That(sunderStatus.Magnitude, Is.EqualTo(0.50f));
        Assert.That(sunderStatus.MaxStacks, Is.EqualTo(3));
        Assert.That(sunderStatus.DurationSeconds, Is.EqualTo(3.5f));
        Assert.That(sunderStatus.RefreshDurationOnReapply, Is.True);

        var bastion = LoadSkill("skill_last_bastion");
        Assert.That(bastion.SlotKind, Is.EqualTo(SkillSlotKindValue.Passive));
        Assert.That(bastion.TriggeredEffects, Has.Count.EqualTo(2));
        Assert.That(bastion.TriggeredEffects.All(effect =>
            effect.Trigger == CombatTriggerKind.OnHpBelow
            && Math.Abs(effect.ThresholdRatio - 0.40f) < 0.0001f
            && effect.Scope == EffectScope.Self), Is.True);
        Assert.That(bastion.TriggeredEffects.Any(effect =>
            effect.Op == TriggeredEffectOp.Barrier && Math.Abs(effect.Magnitude - 6f) < 0.0001f), Is.True);
        Assert.That(bastion.TriggeredEffects.Any(effect =>
            effect.Op == TriggeredEffectOp.ApplyStatus
            && effect.StatusId == "guarded"
            && Math.Abs(effect.DurationSeconds - 2.5f) < 0.0001f), Is.True);
    }

    [Test]
    public void SunderRhythm_DisplayStatesFlatArmorAndResistDrain()
    {
        const string descriptionKey = "content.skill.skill_sunder_rhythm.desc";
        LocalizationSettings.InitializationOperation.WaitForCompletion();
        var collection = LocalizationEditorSettings.GetStringTableCollection(ContentLocalizationTables.Skills);
        var ko = collection!.GetTable(new LocaleIdentifier("ko")) as StringTable;
        var en = collection.GetTable(new LocaleIdentifier("en")) as StringTable;

        Assert.That(
            ko!.GetEntry(descriptionKey)!.Value,
            Is.EqualTo("근접 타격이 방어와 저항을 각각 0.5씩 낮추며 3회까지 중첩되고 재타격 시 3.5초 지속시간이 갱신됩니다."));
        Assert.That(
            en!.GetEntry(descriptionKey)!.Value,
            Is.EqualTo("Melee strikes reduce Armor and Resist by 0.5, stack up to three times, and refresh the 3.5-second duration on hit."));
    }

    [Test]
    public void RealCompile_DerivesRoleVariantsAndDeliversBothGrantedSkillChannels()
    {
        var content = new RuntimeCombatContentLookup().Snapshot;
        Assert.That(CompileHero(content, "slayer", new[] { "passive_duelist_notable_01" }).Allies.Single().RoleVariant,
            Is.EqualTo(RoleVariantTag.Diver));
        Assert.That(CompileHero(content, "slayer", new[]
            {
                "passive_duelist_notable_01", "passive_duelist_notable_03",
            }).Allies.Single().RoleVariant,
            Is.EqualTo(RoleVariantTag.Executioner));

        var forward = CompileHero(content, "slayer", new[]
        {
            "passive_duelist_notable_01", "passive_duelist_notable_03", "passive_duelist_notable_02",
        }).Allies.Single();
        var reverse = CompileHero(content, "slayer", new[]
        {
            "passive_duelist_notable_02", "passive_duelist_notable_03", "passive_duelist_notable_01",
        }).Allies.Single();
        Assert.That(forward.RoleVariant, Is.EqualTo(RoleVariantTag.Peeler));
        Assert.That(reverse.RoleVariant, Is.EqualTo(RoleVariantTag.Peeler));

        var sunderChain = BuildPrerequisiteClosure(content, "passive_duelist_notable_05");
        var sunderUnit = CompileHero(content, "slayer", sunderChain).Allies.Single();
        var sunderStatus = sunderUnit.Skills.Single(skill => skill.Id == "skill_slayer_core")
            .AppliedStatuses!.Single(status => status.StatusId == "sunder");
        Assert.That(sunderStatus.Magnitude, Is.EqualTo(0.50f));
        Assert.That(sunderStatus.MaxStacks, Is.EqualTo(3));
        Assert.That(sunderStatus.DurationSeconds, Is.EqualTo(3.5f));

        var bastionChain = BuildPrerequisiteClosure(content, "passive_duelist_keystone_02");
        var bastionUnit = CompileHero(content, "slayer", bastionChain).Allies.Single();
        Assert.That(bastionUnit.EffectiveTriggeredEffects.Count(effect =>
            effect.SourceId == "skill_last_bastion" && effect.Trigger == CombatTriggerKind.OnHpBelow),
            Is.EqualTo(2));
    }

    [Test]
    public void CritCapDump_ProvesDiveOnePointEightFive_BruiserOnePointSevenFive_AndRangerLower()
    {
        var content = new RuntimeCombatContentLookup().Snapshot;
        var dive = CompileHero(content, "slayer", new[]
        {
            "passive_duelist_small_02", "passive_duelist_small_04", "passive_duelist_notable_01",
            "passive_duelist_small_06", "passive_duelist_small_08", "passive_duelist_notable_03",
            "passive_duelist_small_05", "passive_duelist_small_07",
        }).Allies.Single();
        var bruiser = CompileHero(content, "slayer", new[]
        {
            "passive_duelist_small_01", "passive_duelist_small_03", "passive_duelist_notable_02",
        }).Allies.Single();
        var ranger = CompileHero(content, "hunter", Array.Empty<string>()).Allies.Single();

        var diveStats = WithCritOverflow(dive);
        var bruiserStats = WithCritOverflow(bruiser);
        var rangerStats = WithCritOverflow(ranger);
        Assert.That(diveStats.Get(StatKey.CritChance), Is.EqualTo(0.40f).Within(0.0001f));
        Assert.That(1f + diveStats.Get(StatKey.CritMultiplier), Is.EqualTo(1.85f).Within(0.0001f));
        Assert.That(bruiserStats.Get(StatKey.CritChance), Is.EqualTo(0.08f).Within(0.0001f));
        Assert.That(1f + bruiserStats.Get(StatKey.CritMultiplier), Is.EqualTo(1.75f).Within(0.0001f));
        Assert.That(rangerStats.Get(StatKey.CritChance), Is.EqualTo(0.30f).Within(0.0001f));
        Assert.That(1f + rangerStats.Get(StatKey.CritMultiplier), Is.LessThan(1.85f));
    }

    private static SkillDefinitionAsset LoadSkill(string skillId)
        => AssetDatabase.LoadAssetAtPath<SkillDefinitionAsset>(
               $"{SampleSeedGenerator.ResourcesRoot}/Skills/{skillId}.asset")
           ?? throw new AssertionException($"Missing skill asset '{skillId}'.");

    private static StatBlock WithCritOverflow(BattleUnitLoadout unit)
    {
        const string sourceId = "witness:crit_overflow";
        var modifiers = unit.NumericPackages.SelectMany(package => package.Modifiers).Concat(new[]
        {
            new StatModifier(StatKey.CritChance, ModifierOp.Flat, 5f, ModifierSource.Other, sourceId),
            new StatModifier(StatKey.CritMultiplier, ModifierOp.Flat, 5f, ModifierSource.Other, sourceId),
        });
        return new StatBlock(new Dictionary<StatKey, float>(unit.BaseStats), modifiers);
    }

    private static IReadOnlyList<string> BuildPrerequisiteClosure(CombatContentSnapshot content, string nodeId)
    {
        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (!visited.Add(id) || !content.PassiveNodes.TryGetValue(id, out var node))
            {
                return;
            }

            foreach (var prerequisite in node.PrerequisiteNodeIds ?? Array.Empty<string>())
            {
                Visit(prerequisite);
            }

            result.Add(id);
        }

        Visit(nodeId);
        return result;
    }

    private static BattleLoadoutSnapshot CompileHero(
        CombatContentSnapshot content,
        string archetypeId,
        IReadOnlyList<string> selectedNodeIds)
    {
        var archetype = content.Archetypes[archetypeId];
        const string heroId = "hero.duelist-build-witness";
        var boardId = $"board_{archetype.ClassId}";
        var heroes = new[]
        {
            new HeroRecord(heroId, heroId, archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var loadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            [heroId] = new(heroId, new[] { "item.duelist-build-witness.blade" }, Array.Empty<string>(), boardId, Array.Empty<string>(), Array.Empty<string>()),
        };
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal)
        {
            ["item.duelist-build-witness.blade"] = new(
                "item.duelist-build-witness.blade",
                "item_slayer_blade",
                Array.Empty<string>(),
                heroId),
        };
        var progressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal)
        {
            [heroId] = new(heroId, 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).ToList()),
        };
        var passives = selectedNodeIds.Count == 0
            ? new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal)
            : new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal)
            {
                [heroId] = new(heroId, boardId, selectedNodeIds),
            };

        return new LoadoutCompiler().Compile(
            heroes,
            loadouts,
            progressions,
            itemInstances,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            passives,
            new PermanentAugmentLoadoutState("bp.duelist-build-witness", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.duelist-build-witness",
                "bp.duelist-build-witness",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.FrontCenter] = heroId },
                new[] { heroId },
                new Dictionary<string, string>(StringComparer.Ordinal)),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }
}
