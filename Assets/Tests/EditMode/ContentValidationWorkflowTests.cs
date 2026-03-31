using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

public sealed class ContentValidationWorkflowTests
{
    private const string TempRoot = "Assets/Resources/_Game/Content/Definitions/__TestValidationTemp";

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.EnsureCanonicalSampleContent();
        DeleteTempRoot();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTempRoot();
    }

    [Test]
    public void ContentDefinitionValidator_FlagsBrokenTempAssets()
    {
        EnsureFolder(TempRoot);
        var assets = new List<ScriptableObject>();

        assets.Add(CreateTempAsset<UnitArchetypeDefinition>("archetype_invalid_scope.asset", asset =>
        {
            asset.Id = "INVALID ID";
            asset.NameKey = "content.archetype.temp_invalid_scope.name";
            asset.ScopeKind = (ArchetypeScopeValue)999;
            asset.RoleFamilyTag = "striker";
            asset.PrimaryWeaponFamilyTag = "blade";
        }));

        assets.Add(CreateTempAsset<RoleInstructionDefinition>("role_missing_key.asset", asset =>
        {
            asset.Id = "temp_role_missing_key";
            asset.NameKey = string.Empty;
            asset.RoleTag = "support";
        }));

        var missingEntryRole = CreateTempAsset<RoleInstructionDefinition>("role_missing_entry.asset", asset =>
        {
            asset.Id = "temp_role_missing_entry";
            asset.NameKey = "content.role.temp_role_missing_entry.name";
            asset.RoleTag = "support";
        });
        assets.Add(missingEntryRole);
        SetHiddenStringField(missingEntryRole, "legacyDisplayName", "Legacy Role");
        Assert.That(missingEntryRole.LegacyDisplayName, Is.EqualTo("Legacy Role"));

        assets.Add(CreateTempAsset<SkillDefinitionAsset>("skill_invalid_slot.asset", asset =>
        {
            asset.Id = "temp_invalid_slot_skill";
            asset.NameKey = "INVALID KEY";
            asset.DescriptionKey = "content.skill.temp_invalid_slot_skill.desc";
            asset.Kind = SkillKindValue.Strike;
            asset.SlotKind = (SkillSlotKindValue)999;
            asset.DamageType = DamageTypeValue.Physical;
            asset.Delivery = SkillDeliveryValue.Melee;
            asset.TargetRule = SkillTargetRuleValue.NearestEnemy;
        }));

        var passiveNode = CreateTempAsset<PassiveNodeDefinition>("passive_node_invalid.asset", asset =>
        {
            asset.Id = "temp_passive_node_invalid";
            asset.BoardId = "temp_board_invalid";
            asset.NameKey = "content.passive_node.temp_passive_node_invalid.name";
            asset.DescriptionKey = "content.passive_node.temp_passive_node_invalid.desc";
            asset.NodeKind = PassiveNodeKindValue.Small;
        });
        assets.Add(passiveNode);

        assets.Add(CreateTempAsset<PassiveBoardDefinition>("passive_board_invalid.asset", asset =>
        {
            asset.Id = "temp_board_invalid";
            asset.NameKey = "content.passive_board.temp_board_invalid.name";
            asset.DescriptionKey = "content.passive_board.temp_board_invalid.desc";
            asset.ClassId = string.Empty;
            asset.Nodes = new List<PassiveNodeDefinition> { passiveNode };
        }));

        var synergyTier = CreateTempAsset<SynergyTierDefinition>("synergy_tier_invalid.asset", asset =>
        {
            asset.Id = "temp_synergy_tier_invalid";
            asset.NameKey = "content.synergy.temp_synergy_tier_invalid.name";
            asset.DescriptionKey = "content.synergy.temp_synergy_tier_invalid.desc";
            asset.Threshold = 2;
        });
        assets.Add(synergyTier);

        assets.Add(CreateTempAsset<SynergyDefinition>("synergy_invalid.asset", asset =>
        {
            asset.Id = "temp_synergy_invalid";
            asset.NameKey = "content.synergy.temp_synergy_invalid.name";
            asset.DescriptionKey = "content.synergy.temp_synergy_invalid.desc";
            asset.CountedTagId = "temp_tag";
            asset.Tiers = new List<SynergyTierDefinition> { synergyTier };
        }));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var report = ContentDefinitionValidator.BuildValidationReport(assets);
        var writtenReport = ContentDefinitionValidator.WriteValidationReport(report);
        var codes = report.Issues.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);

        Assert.That(codes, Contains.Item("localization.missing_key"));
        Assert.That(codes, Contains.Item("localization.invalid_key"));
        Assert.That(codes, Contains.Item("localization.missing_entry"));
        Assert.That(codes, Contains.Item("id.invalid_pattern"));
        Assert.That(codes, Contains.Item("enum.undefined"));
        Assert.That(codes, Contains.Item("passive_board.class_id"));
        Assert.That(codes, Contains.Item("passive_board.shape"));
        Assert.That(codes, Contains.Item("synergy.thresholds"));
        Assert.That(File.Exists(writtenReport.JsonReportPath), Is.True);
        Assert.That(File.Exists(writtenReport.MarkdownSummaryPath), Is.True);
    }

    [Test]
    public void ContentDefinitionValidator_ReportsLaunchFloorThresholdGaps()
    {
        var method = typeof(ContentDefinitionValidator).GetMethod("BuildLaunchScopeGapList", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var lowCounts = new LaunchScopeCountReport
        {
            ArchetypeCount = 0,
            CoreArchetypeCount = 0,
            SpecialistArchetypeCount = 0,
            SkillCount = 0,
            EquippableCount = 0,
            AffixCount = 0,
            PassiveBoardCount = 0,
            PassiveNodeCount = 0,
            TemporaryAugmentCount = 0,
            PermanentAugmentCount = 0,
            SynergyFamilyCount = 0,
        };

        var gaps = method!.Invoke(null, new object[] { lowCounts, ContentDefinitionValidator.PaidLaunchFloor }) as List<string>;

        Assert.That(gaps, Is.Not.Null);
        Assert.That(gaps, Contains.Item("archetypes 0/12"));
        Assert.That(gaps, Contains.Item("skills 0/40"));
        Assert.That(gaps, Contains.Item("equippables 0/36"));
        Assert.That(gaps, Contains.Item("passiveNodes 0/72"));
        Assert.That(gaps, Contains.Item("tempAugments 0/18"));
        Assert.That(gaps, Contains.Item("permAugments 0/9"));
        Assert.That(gaps, Contains.Item("synergyFamilies 0/7"));
    }

    private static T CreateTempAsset<T>(string fileName, Action<T> configure) where T : ScriptableObject
    {
        var path = $"{TempRoot}/{fileName}";
        var asset = ScriptableObject.CreateInstance<T>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void SetHiddenStringField(UnityEngine.Object asset, string fieldName, string value)
    {
        var field = asset.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing hidden field '{fieldName}' on {asset.GetType().Name}.");
        field!.SetValue(asset, value);
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void DeleteTempRoot()
    {
        if (!AssetDatabase.IsValidFolder(TempRoot))
        {
            return;
        }

        AssetDatabase.DeleteAsset(TempRoot);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }
}
