using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
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
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(ContentValidationWorkflowTests));
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

        var gaps = LaunchScopeGapEvaluator.BuildGapList(lowCounts, ContentDefinitionValidator.PaidLaunchFloor);

        Assert.That(gaps, Contains.Item("archetypes 0/12"));
        Assert.That(gaps, Contains.Item("skills 0/40"));
        Assert.That(gaps, Contains.Item("equippables 0/36"));
        Assert.That(gaps, Contains.Item("passiveNodes 0/72"));
        Assert.That(gaps, Contains.Item("tempAugments 0/18"));
        Assert.That(gaps, Contains.Item("permAugments 0/9"));
        Assert.That(gaps, Contains.Item("synergyFamilies 0/7"));
    }

    [Test]
    public void ContentDefinitionValidator_FlagsDeepSchemaContractDrift()
    {
        EnsureFolder(TempRoot);
        var assets = new List<ScriptableObject>();

        var vanguardTag = CreateTempAsset<StableTagDefinition>("tag_vanguard.asset", asset =>
        {
            asset.Id = "vanguard";
            asset.NameKey = "content.tag.vanguard.name";
        });
        assets.Add(vanguardTag);

        var rangerTag = CreateTempAsset<StableTagDefinition>("tag_ranger.asset", asset =>
        {
            asset.Id = "ranger";
            asset.NameKey = "content.tag.ranger.name";
        });
        assets.Add(rangerTag);

        var passiveSkill = CreateTempAsset<SkillDefinitionAsset>("skill_temp_passive.asset", asset =>
        {
            asset.Id = "skill_temp_passive";
            asset.NameKey = "content.skill.temp_passive.name";
            asset.DescriptionKey = "content.skill.temp_passive.desc";
            asset.TemplateType = SkillTemplateTypeValue.TriggerPassive;
            asset.Kind = SkillKindValue.Buff;
            asset.SlotKind = SkillSlotKindValue.Passive;
            asset.DamageType = DamageTypeValue.Physical;
            asset.Delivery = SkillDeliveryValue.Aura;
            asset.TargetRule = SkillTargetRuleValue.Self;
            asset.AppliedStatuses.Add(new StatusApplicationRule
            {
                Id = "rule_temp_status",
                StatusId = "marked",
                DurationSeconds = 2f,
                MaxStacks = 3,
                StackCap = 1,
            });
        });
        assets.Add(passiveSkill);

        var utilitySkill = CreateTempAsset<SkillDefinitionAsset>("skill_temp_utility.asset", asset =>
        {
            asset.Id = "skill_temp_utility";
            asset.NameKey = "content.skill.temp_utility.name";
            asset.DescriptionKey = "content.skill.temp_utility.desc";
            asset.TemplateType = SkillTemplateTypeValue.ProjectileShot;
            asset.Kind = SkillKindValue.Strike;
            asset.SlotKind = SkillSlotKindValue.UtilityActive;
            asset.DamageType = DamageTypeValue.Physical;
            asset.Delivery = SkillDeliveryValue.Projectile;
            asset.TargetRule = SkillTargetRuleValue.NearestEnemy;
            asset.RangeMin = 4f;
            asset.RangeMax = 2f;
            asset.AiIntents.Add(SkillAiIntentValue.MaintainRange);
            asset.AiIntents.Add(SkillAiIntentValue.MaintainRange);
            asset.AiScoreHints.MinimumTargetHealthRatio = 0.8f;
            asset.AiScoreHints.MaximumTargetHealthRatio = 0.4f;
            asset.AnimationHookId = "INVALID HOOK";
        });
        assets.Add(utilitySkill);

        var coreSkill = CreateTempAsset<SkillDefinitionAsset>("skill_temp_core.asset", asset =>
        {
            asset.Id = "skill_temp_core";
            asset.NameKey = "content.skill.temp_core.name";
            asset.DescriptionKey = "content.skill.temp_core.desc";
            asset.TemplateType = SkillTemplateTypeValue.SingleTargetStrike;
            asset.Kind = SkillKindValue.Strike;
            asset.SlotKind = SkillSlotKindValue.CoreActive;
            asset.DamageType = DamageTypeValue.Physical;
            asset.Delivery = SkillDeliveryValue.Melee;
            asset.TargetRule = SkillTargetRuleValue.NearestEnemy;
        });
        assets.Add(coreSkill);

        var supportSkill = CreateTempAsset<SkillDefinitionAsset>("skill_temp_support.asset", asset =>
        {
            asset.Id = "support_temp_support";
            asset.NameKey = "content.skill.temp_support.name";
            asset.DescriptionKey = "content.skill.temp_support.desc";
            asset.TemplateType = SkillTemplateTypeValue.AllyBuffAuraPulse;
            asset.Kind = SkillKindValue.Buff;
            asset.SlotKind = SkillSlotKindValue.Support;
            asset.DamageType = DamageTypeValue.Physical;
            asset.Delivery = SkillDeliveryValue.Aura;
            asset.TargetRule = SkillTargetRuleValue.ProtectedAlly;
            asset.SupportAllowedTags.Add(vanguardTag);
        });
        assets.Add(supportSkill);

        assets.Add(CreateTempAsset<AffixDefinition>("affix_temp_schema.asset", asset =>
        {
            asset.Id = "affix_temp_schema";
            asset.NameKey = "content.affix.temp_schema.name";
            asset.DescriptionKey = "content.affix.temp_schema.desc";
            asset.Category = AffixCategoryValue.OffenseFlat;
            asset.AffixFamily = AffixFamilyValue.ConditionalTagged;
            asset.EffectType = AffixEffectTypeValue.StatModifier;
            asset.ValueMin = 10f;
            asset.ValueMax = 5f;
            asset.RequiredTags.Add(vanguardTag);
            asset.ExcludedTags.Add(vanguardTag);
            asset.ExclusiveGroupId = "INVALID GROUP";
        }));

        assets.Add(CreateTempAsset<AugmentDefinition>("augment_temp_schema.asset", asset =>
        {
            asset.Id = "augment_temp_schema";
            asset.NameKey = "content.augment.temp_schema.name";
            asset.DescriptionKey = "content.augment.temp_schema.desc";
            asset.FamilyId = "augment_temp_schema";
            asset.OfferBucket = AugmentOfferBucketValue.SynergyLinked;
            asset.RiskRewardClass = AugmentRiskRewardClassValue.RiskReward;
            asset.BudgetScore = -1f;
            asset.ProtectionTags.Add(rangerTag);
        }));

        assets.Add(CreateTempAsset<StatusFamilyDefinition>("status_family_temp_schema.asset", asset =>
        {
            asset.Id = "temp_schema_status";
            asset.NameKey = "content.status.temp_schema.name";
            asset.DescriptionKey = "content.status.temp_schema.desc";
            asset.DefaultStackCap = 0;
            asset.VisualPriority = -1;
        }));

        var traitPool = CreateTempAsset<TraitPoolDefinition>("traitpool_temp_schema.asset", asset =>
        {
            asset.Id = "traitpool_temp_schema";
            asset.ArchetypeId = "temp_schema";
        });
        assets.Add(traitPool);

        assets.Add(CreateTempAsset<UnitArchetypeDefinition>("archetype_temp_schema.asset", asset =>
        {
            asset.Id = "archetype_temp_schema";
            asset.NameKey = "content.archetype.temp_schema.name";
            asset.ScopeKind = ArchetypeScopeValue.Core;
            asset.Race = AssetDatabase.LoadAssetAtPath<RaceDefinition>($"{SampleSeedGenerator.ResourcesRoot}/Races/race_human.asset");
            asset.Class = AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{SampleSeedGenerator.ResourcesRoot}/Classes/class_vanguard.asset");
            asset.TraitPool = traitPool;
            asset.RoleFamilyTag = "vanguard";
            asset.PrimaryWeaponFamilyTag = "shield";
            asset.TacticPreset = new List<TacticPresetEntry>
            {
                new() { Priority = 1, ConditionType = TacticConditionTypeValue.LowestHpEnemy, ActionType = BattleActionTypeValue.BasicAttack, TargetSelector = TargetSelectorTypeValue.LowestHpEnemy }
            };
            asset.Skills = new List<SkillDefinitionAsset> { coreSkill, utilitySkill, passiveSkill, supportSkill };
            asset.LockedSignatureActiveSkill = passiveSkill;
            asset.LockedSignaturePassiveSkill = passiveSkill;
            asset.FlexSupportSkillPool.Add(utilitySkill);
        }));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var report = ContentDefinitionValidator.BuildValidationReport(assets);
        var codes = report.Issues.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);

        Assert.That(codes, Contains.Item("affix.value_band"));
        Assert.That(codes, Contains.Item("affix.tag_overlap"));
        Assert.That(codes, Contains.Item("affix.effect_payload"));
        Assert.That(codes, Contains.Item("skill.range_band"));
        Assert.That(codes, Contains.Item("skill.ai_intents"));
        Assert.That(codes, Contains.Item("skill.ai_score_hints"));
        Assert.That(codes, Contains.Item("augment.offer_metadata"));
        Assert.That(codes, Contains.Item("augment.budget_score"));
        Assert.That(codes, Contains.Item("status.family_defaults"));
        Assert.That(codes, Contains.Item("status.rule_stack_cap"));
        Assert.That(codes, Contains.Item("archetype.locked_signature_active"));
        Assert.That(codes, Contains.Item("archetype.flex_support_pool"));
    }

    [Test]
    public void BalanceSweepRunner_CsvSummary_ContainsExtendedMetricColumns()
    {
        var report = new BalanceSweepReport
        {
            Scenarios = new[]
            {
                new BalanceSweepScenarioReport
                {
                    ScenarioId = "schema_probe",
                    TeamTacticId = "team_tactic_standard_advance",
                    CompileHashDeterministic = true,
                    FinalStateDeterministic = true,
                    WinRate = 0.5f,
                    AverageBattleDurationSeconds = 12f,
                    AverageFirstCastSeconds = 1.4f,
                    TimeToFirstMeaningfulActionSeconds = 0.8f,
                    AverageRepositionCount = 2.5f,
                    AverageTargetAccessTimeSeconds = 1.9f,
                    AverageFrontlineSurvivalTimeSeconds = 10.2f,
                }
            }
        };

        var csv = BalanceSweepCsvWriter.BuildCsvSummary(report);
        Assert.That(csv, Does.Contain("time_to_first_meaningful_action_seconds"));
        Assert.That(csv, Does.Contain("avg_reposition_count"));
        Assert.That(csv, Does.Contain("avg_target_access_time_seconds"));
        Assert.That(csv, Does.Contain("avg_frontline_survival_time_seconds"));
    }

    [Test]
    public void ContentValidationRuntimeFactory_CreatesDefaultServices()
    {
        var services = ContentValidationRuntimeFactory.CreateDefault();

        Assert.That(services.Validator, Is.Not.Null);
        Assert.That(services.ReportWriter, Is.Not.Null);
        var reportDirectory = services.ReportPaths.GetDefaultReportDirectory().Replace(Path.DirectorySeparatorChar, '/');
        Assert.That(reportDirectory, Does.Contain("Logs/content-validation"));
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
        var serializedObject = new SerializedObject(asset);
        var property = serializedObject.FindProperty(fieldName);
        Assert.That(property, Is.Not.Null, $"Missing hidden field '{fieldName}' on {asset.GetType().Name}.");
        property!.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
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

    private static T LoadAnyAsset<T>(string folder) where T : UnityEngine.Object
    {
        var path = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(candidate => AssetDatabase.LoadAssetAtPath<T>(candidate) != null);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = AssetDatabase.FindAssets(string.Empty, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(candidate => AssetDatabase.LoadAssetAtPath<T>(candidate) != null);
        }

        Assert.That(path, Is.Not.Null.And.Not.Empty, $"Missing canonical asset under {folder} for {typeof(T).Name}.");
        var asset = AssetDatabase.LoadAssetAtPath<T>(path!);
        Assert.That(asset, Is.Not.Null, $"Failed to load canonical asset at {path}.");
        return asset!;
    }
}
