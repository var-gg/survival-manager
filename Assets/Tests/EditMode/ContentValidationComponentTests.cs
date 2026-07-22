using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Editor.Validation;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class ContentValidationComponentTests
{
    private readonly List<UnityEngine.Object> _ownedObjects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _ownedObjects.Where(asset => asset != null))
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }

        _ownedObjects.Clear();
    }

    [Test]
    public void RuntimeFactory_CreatesExplicitValidationGraph()
    {
        var services = ContentValidationRuntimeFactory.CreateDefault();

        Assert.That(services.Validator, Is.Not.Null);
        Assert.That(services.ReportWriter, Is.Not.Null);
        // path separator OS-aware: Windows `\` vs Unix `/`
        Assert.That(services.ReportPaths.GetDefaultReportDirectory(), Does.Match(@"Logs[/\\]content-validation"));
    }

    [Test]
    public void ClassRoleFamilyMapping_RoundTripsDuelistAndStriker()
    {
        Assert.That(ContentValidationPolicyCatalog.TryGetRoleFamilyForCanonicalClassId("duelist", out var roleFamily), Is.True);
        Assert.That(roleFamily, Is.EqualTo("striker"));
        Assert.That(ContentValidationPolicyCatalog.TryGetCanonicalClassIdForRoleFamily("striker", out var canonicalClassId), Is.True);
        Assert.That(canonicalClassId, Is.EqualTo("duelist"));
    }

    [Test]
    public void ArchetypeSchemaRule_FlagsGlossaryRoleFamilyDrift()
    {
        var rule = new ArchetypeSchemaRule();
        var archetype = Own(ScriptableObject.CreateInstance<UnitArchetypeDefinition>());
        archetype.Id = "archetype_glossary_probe";
        archetype.Race = Own(ScriptableObject.CreateInstance<RaceDefinition>());
        archetype.Class = Own(ScriptableObject.CreateInstance<ClassDefinition>());
        archetype.Class.Id = "duelist";
        archetype.TraitPool = Own(ScriptableObject.CreateInstance<TraitPoolDefinition>());
        archetype.RoleFamilyTag = "vanguard";
        archetype.PrimaryWeaponFamilyTag = "blade";
        archetype.TacticPreset = new List<TacticPresetEntry> { new() };
        archetype.IsRecruitable = false;

        var issues = new List<ContentValidationIssue>();
        rule.Validate(new ValidationAssetDescriptor(archetype, "Assets/test_archetype.asset", ValidationAssetSourceKind.Explicit, archetype.GetType()), new ValidationAssetCatalog(new[]
        {
            new ValidationAssetDescriptor(archetype, "Assets/test_archetype.asset", ValidationAssetSourceKind.Explicit, archetype.GetType()),
        }), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("glossary.duelist_role_family"));
    }

    [Test]
    public void SkillSchemaRule_FlagsRangeAndAiHintDrift()
    {
        var rule = new SkillSchemaRule();
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_probe";
        skill.RangeMin = 5f;
        skill.RangeMax = 1f;
        skill.AiScoreHints.MinimumTargetHealthRatio = 0.8f;
        skill.AiScoreHints.MaximumTargetHealthRatio = 0.4f;

        var issues = new List<ContentValidationIssue>();
        rule.Validate(new ValidationAssetDescriptor(skill, "Assets/test_skill.asset", ValidationAssetSourceKind.Explicit, skill.GetType()), EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.range_band"));
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.ai_score_hints"));
    }

    [Test]
    public void RuleTagSchemaGuard_AcceptsOnlyRuntimeInterpretedBehaviorTags()
    {
        var interpretedTag = Own(ScriptableObject.CreateInstance<StableTagDefinition>());
        interpretedTag.Id = CombatBehaviorTags.DuelistDiveCommit;
        var issues = new List<ContentValidationIssue>();

        ContentDefinitionSchemaRuleSupport.ValidateRuleTagScaffold(
            issues,
            new[] { interpretedTag },
            "Assets/interpreted_rule_tag.asset",
            "Probe");

        Assert.That(issues.Select(issue => issue.Code), Does.Not.Contain("rule_tag.scaffold_only"));
    }

    [Test]
    public void RuleTagSchemaGuard_StillRejectsUninterpretedBehaviorTags()
    {
        var scaffoldTag = Own(ScriptableObject.CreateInstance<StableTagDefinition>());
        scaffoldTag.Id = "uninterpreted_probe";
        var issues = new List<ContentValidationIssue>();

        ContentDefinitionSchemaRuleSupport.ValidateRuleTagScaffold(
            issues,
            new[] { scaffoldTag },
            "Assets/uninterpreted_rule_tag.asset",
            "Probe");

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("rule_tag.scaffold_only"));
        Assert.That(issues.Single(issue => issue.Code == "rule_tag.scaffold_only").Message,
            Does.Contain("uninterpreted_probe"));
    }

    [Test]
    public void SkillSchemaRule_FlagsAreaEffectAuthoringDrift()
    {
        var rule = new SkillSchemaRule();
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_area_probe";
        skill.Kind = SkillKindValue.Buff;
        skill.AreaEffectFamily = AreaEffectFamilyValue.GroundAoe;
        skill.Radius = 0f;

        var issues = new List<ContentValidationIssue>();
        rule.Validate(new ValidationAssetDescriptor(skill, "Assets/test_skill_area.asset", ValidationAssetSourceKind.Explicit, skill.GetType()), EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.area_effect_radius"),
            "radius 없는 AoE 가족 저작은 sim 게이트가 조용히 단일 대상으로 떨어뜨린다 — 잠복 결함으로 차단");
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.area_effect_kind"),
            "Strike/Debuff 외 kind는 AoE 데미지 경로에 도달하지 못한다 — 죽은 저작으로 차단");
    }

    [Test]
    public void SkillSchemaRule_AcceptsStrikeAreaEffectAuthoring()
    {
        var rule = new SkillSchemaRule();
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_area_valid_probe";
        skill.Kind = SkillKindValue.Strike;
        skill.AreaEffectFamily = AreaEffectFamilyValue.CleaveCone;
        skill.Radius = 2.25f;

        var issues = new List<ContentValidationIssue>();
        rule.Validate(new ValidationAssetDescriptor(skill, "Assets/test_skill_area_valid.asset", ValidationAssetSourceKind.Explicit, skill.GetType()), EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Does.Not.Contain("skill.area_effect_radius"));
        Assert.That(issues.Select(issue => issue.Code), Does.Not.Contain("skill.area_effect_kind"));
    }

    [Test]
    public void SkillSchemaRule_RequiresDedicatedOpeningLockAndSaneBlinkGeometry()
    {
        var rule = new SkillSchemaRule();
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_blink_validation_probe";
        skill.StartsOnCooldown = true;
        skill.OpeningLockSeconds = 0f;
        skill.Range = 8f;
        skill.DisplacementKind = SkillDisplacementKind.SelfBlinkToTarget;
        skill.DisplacementDistance = 0f;

        var issues = new List<ContentValidationIssue>();
        var descriptor = new ValidationAssetDescriptor(
            skill,
            "Assets/test_skill_blink_validation.asset",
            ValidationAssetSourceKind.Explicit,
            skill.GetType());
        rule.Validate(descriptor, EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.opening_lock_seconds"));
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.self_blink_geometry"));

        skill.OpeningLockSeconds = 1f;
        skill.DisplacementDistance = 8.5f;
        issues.Clear();
        rule.Validate(descriptor, EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Does.Not.Contain("skill.opening_lock_seconds"));
        Assert.That(issues.Select(issue => issue.Code), Does.Not.Contain("skill.self_blink_geometry"));
    }

    [Test]
    public void SkillCatalogValidator_RequiresDrawbackCreditForSelfScopedHostileStatus()
    {
        var exposed = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        exposed.Id = "exposed_probe";
        exposed.Group = StatusGroupValue.TacticalMark;

        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_self_hostile_probe";
        skill.BudgetCard = new BudgetCard
        {
            Domain = BudgetDomain.Skill,
            Vector = new BudgetVector(),
        };
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "skill_self_hostile_probe:self_exposed",
            StatusId = exposed.Id,
            Scope = EffectScope.Self,
        });

        var catalog = ToCatalog(new ScriptableObject[] { exposed, skill });
        var issues = new List<ContentValidationIssue>();
        new SkillCatalogValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.self_hostile_status_drawback_credit"));

        skill.BudgetCard.Vector.DrawbackCredit = 1;
        issues.Clear();
        new SkillCatalogValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Does.Not.Contain("skill.self_hostile_status_drawback_credit"));
    }

    [Test]
    public void SkillCatalogValidator_FlagsMissingStatusReference()
    {
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_missing_status";
        skill.AppliedStatuses.Add(new StatusApplicationRule { StatusId = "missing_status", MaxStacks = 1 });

        var catalog = new ValidationAssetCatalog(new[]
        {
            new ValidationAssetDescriptor(skill, "Assets/skill_missing_status.asset", ValidationAssetSourceKind.Explicit, skill.GetType()),
        });

        var issues = new List<ContentValidationIssue>();
        new SkillCatalogValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.status_ref"));
    }

    [Test]
    public void DefaultCatalogValidation_RejectsClampedFractionalStatusMagnitudes_FromEveryAuthoredCarrier()
    {
        var slow = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        slow.Id = "slow";
        slow.DampensTempo = true;
        slow.MagnitudeScale = 1f;
        slow.MagnitudeUnit = MagnitudeUnit.Rate;

        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_status_magnitude_probe";
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "skill.applied",
            StatusId = slow.Id,
            Magnitude = 1f,
        });
        skill.SupportModifier.AddedStatuses.Add(new StatusApplicationRule
        {
            Id = "skill.support",
            StatusId = slow.Id,
            Magnitude = 1f,
        });
        skill.TriggeredEffects.Add(new TriggeredEffectSpec
        {
            Op = TriggeredEffectOp.ApplyStatus,
            StatusId = slow.Id,
            Magnitude = 1f,
        });

        var augment = Own(ScriptableObject.CreateInstance<AugmentDefinition>());
        augment.Id = "augment_status_magnitude_probe";
        augment.TriggeredEffects.Add(new TriggeredEffectSpec
        {
            Op = TriggeredEffectOp.ApplyStatus,
            StatusId = slow.Id,
            Magnitude = 1f,
        });

        var overlay = Own(ScriptableObject.CreateInstance<BossOverlayDefinition>());
        overlay.Id = "overlay_status_magnitude_probe";
        overlay.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "overlay.applied",
            StatusId = slow.Id,
            Magnitude = 1f,
        });

        var issues = new List<ContentValidationIssue>();
        CatalogValidationRuleRegistry.CreateDefault().Validate(
            ToCatalog(new ScriptableObject[] { slow, skill, augment, overlay }),
            issues);

        var magnitudeIssues = issues
            .Where(issue => issue.Code == "status.channel_magnitude_range")
            .Select(issue => issue.Scope)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
        Assert.That(magnitudeIssues, Is.EqualTo(new[]
        {
            "AugmentDefinition.TriggeredEffects[0]",
            "BossOverlayDefinition.AppliedStatuses[0]",
            "SkillDefinition.AppliedStatuses[0]",
            "SkillDefinition.SupportModifier.AddedStatuses[0]",
            "SkillDefinition.TriggeredEffects[0]",
        }), "모든 shipped authoring carrier가 runtime clamp에 닿는 fractional magnitude를 차단해야 한다");
    }

    [Test]
    public void DefaultCatalogValidation_RejectsInstantMagnitudesSilentlyRaisedByRuntimeFloors()
    {
        var barrier = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        barrier.Id = "barrier";
        barrier.GrantsBarrierOnApply = true;
        barrier.MagnitudeScale = 1f;

        var burn = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        burn.Id = "burn";
        burn.AppliesPeriodicDamage = true;
        burn.MagnitudeScale = 1f;

        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_status_floor_probe";
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "skill.barrier",
            StatusId = barrier.Id,
            Magnitude = 0.5f,
        });
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "skill.burn",
            StatusId = burn.Id,
            Magnitude = 0.5f,
        });

        var issues = new List<ContentValidationIssue>();
        CatalogValidationRuleRegistry.CreateDefault().Validate(
            ToCatalog(new ScriptableObject[] { barrier, burn, skill }),
            issues);

        var magnitudeIssues = issues
            .Where(issue => issue.Code == "status.channel_magnitude_range")
            .Select(issue => issue.Scope)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
        Assert.That(magnitudeIssues, Is.EqualTo(new[]
        {
            "SkillDefinition.AppliedStatuses[0]",
            "SkillDefinition.AppliedStatuses[1]",
        }), "즉시 보호막과 주기 피해가 runtime 1.0 floor에 의해 조용히 보정되면 안 된다");
    }

    [Test]
    public void DefaultCatalogValidation_AllowsMembershipOnlyZeroButRejectsMagnitudeOnlyZero()
    {
        var marked = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        marked.Id = "marked";
        marked.AmplifiesIncomingDamage = true;
        marked.MarksTarget = true;
        marked.MagnitudeScale = 1f;
        marked.MagnitudeUnit = MagnitudeUnit.Rate;
        var exposed = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        exposed.Id = "exposed";
        exposed.AmplifiesIncomingDamage = true;
        exposed.MagnitudeScale = 1f;
        exposed.MagnitudeUnit = MagnitudeUnit.Rate;
        var wound = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        wound.Id = "wound";
        wound.ReducesHealing = true;
        wound.MagnitudeScale = 1f;
        wound.MagnitudeUnit = MagnitudeUnit.Rate;
        var slow = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        slow.Id = "slow";
        slow.DampensTempo = true;
        slow.MagnitudeScale = 1f;
        slow.MagnitudeUnit = MagnitudeUnit.Rate;
        var sunder = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        sunder.Id = "sunder";
        sunder.ShredsDefense = true;
        sunder.MagnitudeScale = 1f;
        sunder.MagnitudeUnit = MagnitudeUnit.Flat;

        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_zero_channel_probe";
        foreach (var family in new[] { marked, exposed, wound, slow, sunder })
        {
            skill.AppliedStatuses.Add(new StatusApplicationRule
            {
                Id = $"zero.{family.Id}",
                StatusId = family.Id,
                Magnitude = 0f,
            });
        }

        var issues = new List<ContentValidationIssue>();
        CatalogValidationRuleRegistry.CreateDefault().Validate(
            ToCatalog(new ScriptableObject[] { marked, exposed, wound, slow, sunder, skill }),
            issues);

        Assert.That(
            issues.Where(issue => issue.Code == "status.channel_zero_membership_only")
                .Select(issue => (issue.Scope, issue.Severity))
                .ToArray(),
            Is.EqualTo(new[]
            {
                ("SkillDefinition.AppliedStatuses[0]", ContentValidationSeverity.Warning),
            }),
            "marked zero는 MarksTarget membership을 유지하므로 warning이어야 한다");
        Assert.That(
            issues.Where(issue => issue.Code == "status.channel_magnitude_range")
                .Select(issue => issue.Scope)
                .OrderBy(scope => scope, StringComparer.Ordinal)
                .ToArray(),
            Is.EqualTo(new[]
            {
                "SkillDefinition.AppliedStatuses[1]",
                "SkillDefinition.AppliedStatuses[2]",
                "SkillDefinition.AppliedStatuses[3]",
                "SkillDefinition.AppliedStatuses[4]",
            }),
            "비-magnitude kind가 없는 amplify/reduce/tempo/sunder 적용은 effective 0을 허용하면 안 된다");
    }

    [Test]
    public void DefaultCatalogValidation_RejectsUnitMismatchButAllowsAnyPositiveFlatSunder()
    {
        var sunder = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        sunder.Id = "sunder";
        sunder.ShredsDefense = true;
        sunder.MagnitudeScale = 1f;
        sunder.MagnitudeUnit = MagnitudeUnit.Flat;
        var mistypedSunder = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        mistypedSunder.Id = "mistyped_sunder";
        mistypedSunder.ShredsDefense = true;
        mistypedSunder.MagnitudeScale = 1f;
        mistypedSunder.MagnitudeUnit = MagnitudeUnit.Rate;
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_sunder_unit_probe";
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "sunder.small_flat",
            StatusId = sunder.Id,
            Magnitude = 0.06f,
        });
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "sunder.quarter_flat",
            StatusId = sunder.Id,
            Magnitude = 0.25f,
        });
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "sunder.large_flat",
            StatusId = sunder.Id,
            Magnitude = 2f,
        });
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "sunder.rate_mismatch",
            StatusId = mistypedSunder.Id,
            Magnitude = 0.06f,
        });

        var issues = new List<ContentValidationIssue>();
        CatalogValidationRuleRegistry.CreateDefault().Validate(
            ToCatalog(new ScriptableObject[] { sunder, mistypedSunder, skill }),
            issues);

        Assert.That(
            issues.Where(issue => issue.Code == "status.channel_magnitude_range"),
            Is.Empty,
            "flat channel은 magnitude 크기만으로 단위를 추론하지 않으며 0.06, 0.25, 2.0을 모두 허용해야 한다");
        Assert.That(
            issues.Where(issue => issue.Code == "status.channel_magnitude_unit_mismatch")
                .Select(issue => issue.Scope)
                .ToArray(),
            Is.EqualTo(new[] { "StatusFamilyDefinition.MagnitudeUnit" }),
            "rate로 선언된 defense shred는 숫자 band가 아니라 consuming channel과의 단위 불일치로 거부해야 한다");
    }

    [Test]
    public void DefaultCatalogValidation_RejectsMaxStacksOutsideShredsDefense()
    {
        var marked = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        marked.Id = "marked";
        marked.AmplifiesIncomingDamage = true;
        marked.MarksTarget = true;
        marked.MagnitudeScale = 1f;
        marked.MagnitudeUnit = MagnitudeUnit.Rate;
        var sunder = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        sunder.Id = "sunder";
        sunder.ShredsDefense = true;
        sunder.MagnitudeScale = 1f;
        sunder.MagnitudeUnit = MagnitudeUnit.Flat;
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_stack_channel_probe";
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "marked.inert_stacks",
            StatusId = marked.Id,
            Magnitude = 0.2f,
            MaxStacks = 3,
        });
        skill.AppliedStatuses.Add(new StatusApplicationRule
        {
            Id = "sunder.live_stacks",
            StatusId = sunder.Id,
            Magnitude = 0.25f,
            MaxStacks = 3,
        });

        var issues = new List<ContentValidationIssue>();
        CatalogValidationRuleRegistry.CreateDefault().Validate(
            ToCatalog(new ScriptableObject[] { marked, sunder, skill }),
            issues);

        Assert.That(
            issues.Where(issue => issue.Code == "status.channel_inert_stacking")
                .Select(issue => issue.Scope)
                .ToArray(),
            Is.EqualTo(new[] { "SkillDefinition.AppliedStatuses[0]" }),
            "V1에서 stacks를 소비하지 않는 family의 MaxStacks > 1은 silently inert authoring이므로 error여야 한다");
    }

    [Test]
    public void DefaultCatalogValidation_RejectsUnknownStatuses_FromPreviouslyUncoveredCarriers()
    {
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_unknown_status_probe";
        skill.SupportModifier.AddedStatuses.Add(new StatusApplicationRule
        {
            Id = "skill.support.missing",
            StatusId = "missing_support_status",
            Magnitude = 0.2f,
        });
        skill.TriggeredEffects.Add(new TriggeredEffectSpec
        {
            Op = TriggeredEffectOp.ApplyStatus,
            StatusId = "missing_skill_triggered_status",
            Magnitude = 0.2f,
        });

        var augment = Own(ScriptableObject.CreateInstance<AugmentDefinition>());
        augment.Id = "augment_unknown_status_probe";
        augment.TriggeredEffects.Add(new TriggeredEffectSpec
        {
            Op = TriggeredEffectOp.ApplyStatus,
            StatusId = "missing_augment_triggered_status",
            Magnitude = 0.2f,
        });

        var issues = new List<ContentValidationIssue>();
        CatalogValidationRuleRegistry.CreateDefault().Validate(
            ToCatalog(new ScriptableObject[] { skill, augment }),
            issues);

        var referenceIssues = issues
            .Where(issue => issue.Code == "status.application_status_ref")
            .Select(issue => issue.Scope)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
        Assert.That(referenceIssues, Is.EqualTo(new[]
        {
            "AugmentDefinition.TriggeredEffects[0]",
            "SkillDefinition.SupportModifier.AddedStatuses[0]",
            "SkillDefinition.TriggeredEffects[0]",
        }), "기존 reference validator가 걷지 않는 carrier도 잘못된 StatusId를 fail closed 해야 한다");
    }

    [Test]
    public void SkillCatalogValidator_FlagsMissingClassGate_ForClassOwnedSkill()
    {
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_probe_core";
        skill.SlotKind = SkillSlotKindValue.CoreActive;

        var catalog = new ValidationAssetCatalog(new[]
        {
            new ValidationAssetDescriptor(skill, "Assets/skill_probe_core.asset", ValidationAssetSourceKind.Explicit, skill.GetType()),
        });

        var issues = new List<ContentValidationIssue>();
        new SkillCatalogValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.class_gate_required"));
    }

    [Test]
    public void SkillCatalogValidator_FlagsSupportModifierWithoutClassOrRoleGate()
    {
        var pierceTag = Own(ScriptableObject.CreateInstance<StableTagDefinition>());
        pierceTag.Id = "pierce";
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "support_probe";
        skill.SlotKind = SkillSlotKindValue.Support;
        skill.SupportAllowedTags.Add(pierceTag);

        var catalog = new ValidationAssetCatalog(new ScriptableObject[]
        {
            skill,
            pierceTag,
        }.Select((asset, index) => new ValidationAssetDescriptor(asset, $"Assets/probe_{index}.asset", ValidationAssetSourceKind.Explicit, asset.GetType())).ToList());

        var issues = new List<ContentValidationIssue>();
        new SkillCatalogValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.support_gate_anchor"));
    }

    [Test]
    public void SchemaRules_FlagMissingPreVfxHooks()
    {
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_missing_vfx";
        skill.NameKey = "content.skill.missing_vfx.name";
        skill.DescriptionKey = "content.skill.missing_vfx.desc";
        var item = Own(ScriptableObject.CreateInstance<ItemBaseDefinition>());
        item.Id = "item_missing_icon";
        item.NameKey = "content.item.missing_icon.name";
        item.DescriptionKey = "content.item.missing_icon.desc";
        var augment = Own(ScriptableObject.CreateInstance<AugmentDefinition>());
        augment.Id = "augment_missing_icon";
        augment.NameKey = "content.augment.missing_icon.name";
        augment.DescriptionKey = "content.augment.missing_icon.desc";
        augment.FamilyId = "probe_family";
        var status = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        status.Id = "missing_vfx_status";
        status.NameKey = "content.status.missing_vfx.name";
        status.DescriptionKey = "content.status.missing_vfx.desc";

        var issues = new List<ContentValidationIssue>();
        new SkillSchemaRule().Validate(new ValidationAssetDescriptor(skill, "Assets/skill_missing_vfx.asset", ValidationAssetSourceKind.Explicit, skill.GetType()), EmptyCatalog(), issues);
        new ItemSchemaRule().Validate(new ValidationAssetDescriptor(item, "Assets/item_missing_icon.asset", ValidationAssetSourceKind.Explicit, item.GetType()), EmptyCatalog(), issues);
        new AugmentSchemaRule().Validate(new ValidationAssetDescriptor(augment, "Assets/augment_missing_icon.asset", ValidationAssetSourceKind.Explicit, augment.GetType()), EmptyCatalog(), issues);
        new StatusFamilySchemaRule().Validate(new ValidationAssetDescriptor(status, "Assets/status_missing_vfx.asset", ValidationAssetSourceKind.Explicit, status.GetType()), EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.icon_required"));
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.vfx_hook_required"));
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("item.icon_required"));
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("augment.icon_required"));
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("status.vfx_cue_required"));
    }

    [TestCase("barrier")]
    [TestCase("unstoppable")]
    [TestCase("guarded")]
    [TestCase("damage_reduction")]
    public void StatusFamilySchemaRule_ProtectiveKindsRequireDefensiveBoonGroup(string protectiveKind)
    {
        var status = Own(ScriptableObject.CreateInstance<StatusFamilyDefinition>());
        status.Id = $"misclassified_{protectiveKind}";
        status.NameKey = $"content.status.misclassified_{protectiveKind}.name";
        status.DescriptionKey = $"content.status.misclassified_{protectiveKind}.desc";
        status.VfxCueId = $"vfx.status.misclassified_{protectiveKind}";
        status.Group = StatusGroupValue.Control;
        switch (protectiveKind)
        {
            case "barrier":
                status.GrantsBarrierOnApply = true;
                break;
            case "unstoppable":
                status.GrantsUnstoppable = true;
                break;
            case "guarded":
                status.GrantsGuardedDefense = true;
                break;
            case "damage_reduction":
                status.IncomingDamageDelta = -0.1f;
                break;
        }

        var issues = new List<ContentValidationIssue>();
        new StatusFamilySchemaRule().Validate(
            new ValidationAssetDescriptor(
                status,
                $"Assets/status_{protectiveKind}.asset",
                ValidationAssetSourceKind.Explicit,
                status.GetType()),
            EmptyCatalog(),
            issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("status.defensive_boon_group"));
    }

    [Test]
    public void SkillSchemaRule_ResolvesPresentationMappingForPilotSkill()
    {
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_ember_arrow";
        skill.NameKey = "content.skill.ember_arrow.name";
        skill.DescriptionKey = "content.skill.ember_arrow.desc";
        skill.IconId = "skill_icon_ember_arrow";
        skill.VfxHookId = "vfx.skill_ember_arrow";
        skill.Kind = SkillKindValue.Strike;
        skill.DamageType = DamageTypeValue.Physical;
        skill.Delivery = SkillDeliveryValue.Projectile;

        var issues = new List<ContentValidationIssue>();
        new SkillSchemaRule().Validate(new ValidationAssetDescriptor(skill, "Assets/skill_ember_arrow.asset", ValidationAssetSourceKind.Explicit, skill.GetType()), EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Does.Not.Contain("skill.presentation_mapping"));
    }

    [Test]
    public void FactionIsolationValidator_FlagsSynergyLeak()
    {
        var site = Own(ScriptableObject.CreateInstance<ExpeditionSiteDefinition>());
        site.Id = "site_probe";
        site.FactionId = "faction_alpha";

        var synergy = Own(ScriptableObject.CreateInstance<SynergyDefinition>());
        synergy.Id = "synergy_probe";
        synergy.CountedTagId = "faction_alpha";

        var catalog = new ValidationAssetCatalog(new ValidationAssetDescriptor[]
        {
            new(site, "Assets/site_probe.asset", ValidationAssetSourceKind.Explicit, site.GetType()),
            new(synergy, "Assets/synergy_probe.asset", ValidationAssetSourceKind.Explicit, synergy.GetType()),
        });

        var issues = new List<ContentValidationIssue>();
        new FactionIsolationValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("faction.synergy_leak"));
    }

    [Test]
    public void SiteGraphCatalogValidator_RejectsDuplicateUnknownOrphanAndTerminalLessCycle()
    {
        var site = Own(ScriptableObject.CreateInstance<ExpeditionSiteDefinition>());
        site.Id = "site_graph_probe";
        var graph = Own(ScriptableObject.CreateInstance<SiteGraphDefinition>());
        graph.Id = "site_graph_malformed_probe";
        graph.SiteId = site.Id;
        graph.Nodes = new List<SiteGraphNodeDefinition>
        {
            new() { NodeId = "entry", NextNodeIds = new List<string> { "cycle_a", "missing" } },
            new() { NodeId = "cycle_a", NextNodeIds = new List<string> { "cycle_b" } },
            new() { NodeId = "cycle_a", NextNodeIds = new List<string> { "cycle_b" } },
            new() { NodeId = "cycle_b", NextNodeIds = new List<string> { "cycle_a" } },
            new() { NodeId = "orphan", NextNodeIds = new List<string> { "orphan_terminal" } },
            new() { NodeId = "orphan_terminal" },
        };
        var issues = new List<ContentValidationIssue>();

        new SiteGraphCatalogValidator().Validate(
            new CatalogValidationContext(ToCatalog(new ScriptableObject[] { site, graph })),
            issues);

        var codes = issues.Select(issue => issue.Code).ToList();
        Assert.That(codes, Contains.Item("site_graph.duplicate_node_id"));
        Assert.That(codes, Contains.Item("site_graph.unknown_target"));
        Assert.That(codes, Contains.Item("site_graph.orphan_node"));
        Assert.That(codes, Contains.Item("site_graph.no_terminal_path"));
    }

    [Test]
    public void SiteGraphCatalogValidator_RejectsLegacyIdentityDriftAndRiskFirstDefaultRoute()
    {
        var site = Own(ScriptableObject.CreateInstance<ExpeditionSiteDefinition>());
        site.Id = "site_graph_identity_probe";
        site.EncounterIds = new List<string> { "legacy_skirmish", "legacy_boss" };

        var skirmish = Own(ScriptableObject.CreateInstance<EncounterDefinition>());
        skirmish.Id = "legacy_skirmish";
        skirmish.RewardSourceId = "reward_source_skirmish";
        var boss = Own(ScriptableObject.CreateInstance<EncounterDefinition>());
        boss.Id = "legacy_boss";
        boss.RewardSourceId = "reward_source_boss";

        var graph = Own(ScriptableObject.CreateInstance<SiteGraphDefinition>());
        graph.Id = "site_graph_identity_drift_probe";
        graph.SiteId = site.Id;
        graph.Nodes = new List<SiteGraphNodeDefinition>
        {
            new()
            {
                NodeId = "wrong_first_coordinate",
                LaneTag = "safe",
                EncounterId = boss.Id,
                RewardSourceId = boss.RewardSourceId,
                NextNodeIds = new List<string> { "safe_second" },
            },
            new()
            {
                NodeId = "safe_second",
                LaneTag = "safe",
                EncounterId = boss.Id,
                RewardSourceId = skirmish.RewardSourceId,
                NextNodeIds = new List<string> { "extract" },
            },
            new() { NodeId = "extract", LaneTag = "safe" },
            new()
            {
                NodeId = "entry",
                LaneTag = "safe",
                NextNodeIds = new List<string> { "risk_first", "wrong_first_coordinate" },
            },
            new()
            {
                NodeId = "risk_first",
                LaneTag = "risk",
                NextNodeIds = new List<string> { "safe_second" },
            },
        };
        var issues = new List<ContentValidationIssue>();

        new SiteGraphCatalogValidator().Validate(
            new CatalogValidationContext(ToCatalog(new ScriptableObject[] { site, skirmish, boss, graph })),
            issues);

        var codes = issues.Select(issue => issue.Code).ToList();
        Assert.That(codes, Contains.Item("site_graph.safe_lane_coordinate"));
        Assert.That(codes, Contains.Item("site_graph.safe_lane_reward_source"));
        Assert.That(codes, Contains.Item("site_graph.default_route_risk"));
    }

    [Test]
    public void CharacterCatalogValidator_LocksExecutableCharacterCoverage()
    {
        var fullCatalog = ContentValidationPolicyCatalog.RequiredExecutableCharacterIdsInRosterOrder
            .Select(id => OwnCharacter(id))
            .ToArray();
        var passIssues = new List<ContentValidationIssue>();
        new CharacterCatalogValidator().Validate(new CatalogValidationContext(ToCatalog(fullCatalog)), passIssues);

        Assert.That(passIssues.Select(issue => issue.Code), Does.Not.Contain("character.executable_catalog_floor"));

        var missingCatalog = fullCatalog
            .Where(character => !string.Equals(character.Id, "mirror_cantor", StringComparison.Ordinal))
            .ToArray();
        var issues = new List<ContentValidationIssue>();
        new CharacterCatalogValidator().Validate(new CatalogValidationContext(ToCatalog(missingCatalog)), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("character.executable_catalog_floor"));
    }

    [Test]
    public void DefaultLocalizationShapeProvider_ReturnsExpectedFieldsForSkill()
    {
        var provider = new DefaultLocalizationShapeProvider();

        Assert.That(provider.TryGetShape(typeof(SkillDefinitionAsset), out var shape), Is.True);
        Assert.That(shape.Fields.Select(field => field.FieldName), Is.SupersetOf(new[] { "NameKey", "DescriptionKey" }));
    }

    [Test]
    public void CompositeLocalizationShapeProvider_UsesFallbackOnlyForUnknownTypes()
    {
        var primary = new StubShapeProvider();
        var fallback = new TrackingFallbackShapeProvider();
        var provider = new CompositeLocalizationShapeProvider(primary, fallback);

        Assert.That(provider.TryGetShape(typeof(ShapeKnownAsset), out _), Is.True);
        Assert.That(provider.TryGetShape(typeof(ShapeUnknownAsset), out _), Is.True);
        Assert.That(fallback.Calls, Is.EqualTo(1));
    }

    [Test]
    public void DescriptorDrivenLocalizationInspector_DistinguishesMissingCollectionLocaleAndEntry()
    {
        var provider = new StubShapeProvider(typeof(LocalizationProbeAsset), new LocalizationShape("ProbeTable", new[] { new LocalizedFieldDescriptor("NameKey") }));
        var asset = Own(ScriptableObject.CreateInstance<LocalizationProbeAsset>());
        asset.NameKey = "content.probe.name";
        var descriptor = new ValidationAssetDescriptor(asset, "Assets/localization_probe.asset", ValidationAssetSourceKind.Explicit, asset.GetType());

        var missingCollectionBuilder = new ValidationReportBuilder();
        new DescriptorDrivenLocalizationInspector(provider, new StubLocalizationLookup(null))
            .InspectAsset(descriptor, missingCollectionBuilder);
        Assert.That(missingCollectionBuilder.Issues.Select(issue => issue.Code), Contains.Item("localization.missing_collection"));

        var missingLocaleBuilder = new ValidationReportBuilder();
        new DescriptorDrivenLocalizationInspector(provider, new StubLocalizationLookup(new StubLocalizationCollection(hasKoTable: false, hasEntry: true)))
            .InspectAsset(descriptor, missingLocaleBuilder);
        Assert.That(missingLocaleBuilder.Issues.Select(issue => issue.Code), Contains.Item("localization.missing_locale_table"));

        var missingEntryBuilder = new ValidationReportBuilder();
        new DescriptorDrivenLocalizationInspector(provider, new StubLocalizationLookup(new StubLocalizationCollection(hasKoTable: true, hasEntry: false)))
            .InspectAsset(descriptor, missingEntryBuilder);
        Assert.That(missingEntryBuilder.Issues.Select(issue => issue.Code), Contains.Item("localization.missing_entry"));
    }

    [Test]
    public void CompositeReportWriter_RendersArtifactsAndDelegatesPersistenceToSink()
    {
        var sink = new RecordingArtifactSink();
        var writer = new CompositeReportWriter(
            new StubReportPathProvider("A:/reports"),
            new IValidationArtifactRenderer[]
            {
                new JsonValidationReportRenderer(),
                new MarkdownValidationSummaryRenderer(),
                new LoopCArtifactRenderer(),
            },
            sink);

        var report = writer.Write(new ContentValidationReport
        {
            Issues = new[] { new ContentValidationIssue(ContentValidationSeverity.Warning, "probe", "warning", "Assets/probe.asset") },
        });

        Assert.That(report.JsonReportPath, Is.EqualTo("A:/reports/content-validation-report.json"));
        Assert.That(sink.Artifacts.Count, Is.EqualTo(6));
        Assert.That(sink.Artifacts.Any(artifact => artifact.FilePath.EndsWith("content-validation-summary.md", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void LoopCGovernanceOrchestrator_PreservesConfiguredRuleOrder()
    {
        var executionOrder = new List<string>();
        var orchestrator = new LoopCGovernanceOrchestrator(
            new FixedSubjectExtractor(Array.Empty<LoopCGovernanceSubject>()),
            new ILoopCGovernanceRule[]
            {
                new RecordingGovernanceRule("first", executionOrder),
                new RecordingGovernanceRule("second", executionOrder),
                new RecordingGovernanceRule("third", executionOrder),
            });

        orchestrator.Validate(EmptyCatalog(), new List<ContentValidationIssue>());

        Assert.That(executionOrder, Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public void DefaultLoopCGovernanceSubjectExtractor_ExtractsNestedGovernedDefinitions()
    {
        var archetype = Own(ScriptableObject.CreateInstance<UnitArchetypeDefinition>());
        archetype.Id = "loopc_unit";
        archetype.BudgetCard = new BudgetCard { Domain = BudgetDomain.UnitBlueprint };
        archetype.Loadout.SignaturePassive = new PassiveDefinition
        {
            Id = "loopc_signature_passive",
            BudgetCard = new BudgetCard { Domain = BudgetDomain.Passive },
        };

        var subjects = new DefaultLoopCGovernanceSubjectExtractor().Extract(new ValidationAssetCatalog(new[]
        {
            new ValidationAssetDescriptor(archetype, "Assets/loopc_unit.asset", ValidationAssetSourceKind.Explicit, archetype.GetType()),
        }));

        Assert.That(subjects.Select(subject => subject.ContentKind), Contains.Item(nameof(UnitArchetypeDefinition)));
        Assert.That(subjects.Select(subject => subject.ContentId), Contains.Item("loopc_signature_passive"));
    }

    private ValidationAssetCatalog EmptyCatalog()
    {
        return new ValidationAssetCatalog(Array.Empty<ValidationAssetDescriptor>());
    }

    private static ValidationAssetCatalog ToCatalog(IEnumerable<ScriptableObject> assets)
    {
        return new ValidationAssetCatalog(assets
            .Select((asset, index) => new ValidationAssetDescriptor(asset, $"Assets/test_asset_{index}.asset", ValidationAssetSourceKind.Explicit, asset.GetType()))
            .ToList());
    }

    private CharacterDefinition OwnCharacter(string id)
    {
        var character = Own(ScriptableObject.CreateInstance<CharacterDefinition>());
        character.Id = id;
        return character;
    }

    private T Own<T>(T asset) where T : UnityEngine.Object
    {
        _ownedObjects.Add(asset);
        return asset;
    }

    private sealed class ShapeKnownAsset : ScriptableObject
    {
        public string NameKey = string.Empty;
    }

    private sealed class ShapeUnknownAsset : ScriptableObject
    {
        public string NameKey = string.Empty;
    }

    private sealed class LocalizationProbeAsset : ScriptableObject
    {
        public string NameKey = string.Empty;
    }

    private sealed class StubShapeProvider : ILocalizationShapeProvider
    {
        private readonly Dictionary<Type, LocalizationShape> _shapes = new();

        public StubShapeProvider()
        {
            _shapes[typeof(ShapeKnownAsset)] = new LocalizationShape("KnownTable", new[] { new LocalizedFieldDescriptor("NameKey") });
        }

        public StubShapeProvider(Type type, LocalizationShape shape)
        {
            _shapes[type] = shape;
        }

        public bool TryGetShape(Type type, out LocalizationShape shape)
        {
            return _shapes.TryGetValue(type, out shape!);
        }
    }

    private sealed class TrackingFallbackShapeProvider : ILocalizationShapeProvider
    {
        public int Calls { get; private set; }

        public bool TryGetShape(Type type, out LocalizationShape shape)
        {
            Calls++;
            shape = new LocalizationShape("FallbackTable", new[] { new LocalizedFieldDescriptor("NameKey") });
            return true;
        }
    }

    private sealed class StubLocalizationLookup : ILocalizationEntryLookup
    {
        private readonly ILocalizationTableCollection? _collection;

        public StubLocalizationLookup(ILocalizationTableCollection? collection)
        {
            _collection = collection;
        }

        public ILocalizationTableCollection? GetCollection(string tableName)
        {
            return _collection;
        }
    }

    private sealed class StubLocalizationCollection : ILocalizationTableCollection
    {
        private readonly bool _hasKoTable;
        private readonly bool _hasEntry;

        public StubLocalizationCollection(bool hasKoTable, bool hasEntry)
        {
            _hasKoTable = hasKoTable;
            _hasEntry = hasEntry;
        }

        public bool HasLocaleTable(string localeCode)
        {
            return localeCode == "en" || _hasKoTable;
        }

        public bool HasEntry(string localeCode, string key)
        {
            return _hasEntry;
        }
    }

    private sealed class StubReportPathProvider : IValidationReportPathProvider
    {
        private readonly string _root;

        public StubReportPathProvider(string root)
        {
            _root = root;
        }

        public string GetDefaultReportDirectory()
        {
            return _root;
        }

        public ValidationReportOutputPaths BuildOutputPaths()
        {
            return new ValidationReportOutputPaths(
                _root,
                $"{_root}/content-validation-report.json",
                $"{_root}/content-validation-summary.md",
                $"{_root}/content_budget_audit.json",
                $"{_root}/content_budget_audit.md",
                $"{_root}/counter_coverage_matrix.md",
                $"{_root}/v1_forbidden_feature_report.md");
        }
    }

    private sealed class RecordingArtifactSink : IArtifactSink
    {
        public List<ValidationArtifact> Artifacts { get; } = new();

        public void Write(IReadOnlyList<ValidationArtifact> artifacts)
        {
            Artifacts.AddRange(artifacts);
        }
    }

    private sealed class FixedSubjectExtractor : ILoopCGovernanceSubjectExtractor
    {
        private readonly IReadOnlyList<LoopCGovernanceSubject> _subjects;

        public FixedSubjectExtractor(IReadOnlyList<LoopCGovernanceSubject> subjects)
        {
            _subjects = subjects;
        }

        public IReadOnlyList<LoopCGovernanceSubject> Extract(ValidationAssetCatalog catalog)
        {
            return _subjects;
        }
    }

    private sealed class RecordingGovernanceRule : ILoopCGovernanceRule
    {
        private readonly string _label;
        private readonly ICollection<string> _executionOrder;

        public RecordingGovernanceRule(string label, ICollection<string> executionOrder)
        {
            _label = label;
            _executionOrder = executionOrder;
        }

        public void Execute(LoopCGovernanceContext context)
        {
            _executionOrder.Add(_label);
        }
    }
}
