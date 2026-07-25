using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Editor.Validation;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class TriggeredEffectSpecValidatorParityTests
{
    private readonly List<Object> _ownedObjects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _ownedObjects.Where(asset => asset != null))
        {
            Object.DestroyImmediate(asset);
        }

        _ownedObjects.Clear();
    }

    [Test]
    public void ExtractedValidator_PreservesEveryOriginalCheck_ForAugmentsAndAffixes()
    {
        var augment = Own(ScriptableObject.CreateInstance<AugmentDefinition>());
        augment.Id = "augment_trigger_parity_probe";
        augment.TriggeredEffects = BuildInvalidEffects();

        var affix = Own(ScriptableObject.CreateInstance<AffixDefinition>());
        affix.Id = "affix_trigger_parity_probe";
        affix.EffectType = AffixEffectTypeValue.Proc;
        affix.ValueMin = 1f;
        affix.ValueMax = 1f;
        affix.BudgetScore = 1f;
        affix.TriggeredEffects = BuildInvalidEffects();

        var augmentIssues = ValidateAugment(augment);
        var affixIssues = ValidateAffix(affix);

        AssertOriginalCheckSet(augmentIssues, "augment");
        AssertOriginalCheckSet(affixIssues, "affix");
    }

    private static List<TriggeredEffectSpec> BuildInvalidEffects()
    {
        return new List<TriggeredEffectSpec>
        {
            null!,
            new()
            {
                Trigger = (CombatTriggerKind)(-1),
                Op = (TriggeredEffectOp)(-1),
                Scope = (EffectScope)(-1),
            },
            new()
            {
                Trigger = CombatTriggerKind.BattleStart,
                Op = TriggeredEffectOp.ApplyStatus,
                Scope = EffectScope.Self,
                StatusId = string.Empty,
                DurationSeconds = 0f,
            },
            new()
            {
                Trigger = CombatTriggerKind.OnKill,
                Op = TriggeredEffectOp.Heal,
                Scope = EffectScope.Self,
                Magnitude = 0f,
            },
            new()
            {
                Trigger = CombatTriggerKind.OnKill,
                Op = TriggeredEffectOp.Barrier,
                Scope = EffectScope.Self,
                Magnitude = 0f,
            },
            new()
            {
                Trigger = CombatTriggerKind.OnKill,
                Op = TriggeredEffectOp.GainEnergy,
                Scope = EffectScope.Self,
                Magnitude = 0f,
            },
            new()
            {
                Trigger = CombatTriggerKind.OnHpBelow,
                Op = TriggeredEffectOp.Barrier,
                Scope = EffectScope.Self,
                Magnitude = 1f,
                ThresholdRatio = 0f,
            },
        };
    }

    private static IReadOnlyList<ContentValidationIssue> ValidateAugment(AugmentDefinition augment)
    {
        var descriptor = new ValidationAssetDescriptor(
            augment,
            "Assets/test_augment_trigger_parity.asset",
            ValidationAssetSourceKind.Explicit,
            augment.GetType());
        var catalog = new ValidationAssetCatalog(new[] { descriptor });
        var issues = new List<ContentValidationIssue>();
        new AugmentSchemaRule().Validate(descriptor, catalog, issues);
        return issues;
    }

    private static IReadOnlyList<ContentValidationIssue> ValidateAffix(AffixDefinition affix)
    {
        var descriptor = new ValidationAssetDescriptor(
            affix,
            "Assets/test_affix_trigger_parity.asset",
            ValidationAssetSourceKind.Explicit,
            affix.GetType());
        var catalog = new ValidationAssetCatalog(new[] { descriptor });
        var issues = new List<ContentValidationIssue>();
        new AffixSchemaRule().Validate(descriptor, catalog, issues);
        return issues;
    }

    private static void AssertOriginalCheckSet(
        IReadOnlyList<ContentValidationIssue> issues,
        string codePrefix)
    {
        var codes = issues.Select(issue => issue.Code).ToArray();

        Assert.That(codes.Count(code => code == "enum.undefined"), Is.EqualTo(3),
            $"{codePrefix}: trigger, op, and scope must each reject undefined enum values");
        Assert.That(codes, Contains.Item($"{codePrefix}.trigger_scope_unsupported"));
        Assert.That(codes, Contains.Item($"{codePrefix}.trigger_status_id"));
        Assert.That(codes, Contains.Item($"{codePrefix}.trigger_status_duration"));
        Assert.That(codes.Count(code => code == $"{codePrefix}.trigger_magnitude"), Is.EqualTo(3),
            $"{codePrefix}: Heal, Barrier, and GainEnergy must each reject no-op magnitudes");
        Assert.That(codes, Contains.Item($"{codePrefix}.trigger_threshold"));
        Assert.That(codes, Contains.Item($"{codePrefix}.trigger_null"));
    }

    private T Own<T>(T asset) where T : Object
    {
        _ownedObjects.Add(asset);
        return asset;
    }
}
