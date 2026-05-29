using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Editor.Validation;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// ⑤ — ① 이 추가한 augment TriggeredEffects authoring surface 의 content lint 검증.
/// CombatTriggerEngine 이 조용히 무시/no-op 하는 authoring 실수를 AugmentSchemaRule 이 콘텐츠 단계에서
/// 잡는지 확인하고, well-formed 효과(실제 author된 augment 형태)에는 false positive 가 없음을 보장한다.
/// </summary>
[Category("BatchOnly")]
public sealed class AugmentTriggeredEffectSchemaRuleTests
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

    private T Own<T>(T asset) where T : Object
    {
        _ownedObjects.Add(asset);
        return asset;
    }

    private AugmentDefinition BuildAugment(TriggeredEffectSpec effect)
    {
        var augment = Own(ScriptableObject.CreateInstance<AugmentDefinition>());
        augment.Id = "augment_trigger_probe";
        augment.TriggeredEffects = new List<TriggeredEffectSpec> { effect };
        return augment;
    }

    private IReadOnlyList<string> ValidateCodes(AugmentDefinition augment)
    {
        var descriptor = new ValidationAssetDescriptor(augment, "Assets/test_augment.asset", ValidationAssetSourceKind.Explicit, augment.GetType());
        var catalog = new ValidationAssetCatalog(new[] { descriptor });
        var issues = new List<ContentValidationIssue>();
        new AugmentSchemaRule().Validate(descriptor, catalog, issues);
        return issues.Select(issue => issue.Code).ToArray();
    }

    [Test]
    public void Flags_UnsupportedScope_SilentlyTreatedAsSelf()
    {
        var codes = ValidateCodes(BuildAugment(new TriggeredEffectSpec
        {
            Trigger = CombatTriggerKind.BattleStart,
            Op = TriggeredEffectOp.Barrier,
            Scope = EffectScope.GlobalCombat,
            Magnitude = 20f,
        }));

        Assert.That(codes, Contains.Item("augment.trigger_scope_unsupported"));
    }

    [Test]
    public void Flags_GainEnergy_AsUnimplemented()
    {
        var codes = ValidateCodes(BuildAugment(new TriggeredEffectSpec
        {
            Trigger = CombatTriggerKind.OnKill,
            Op = TriggeredEffectOp.GainEnergy,
            Scope = EffectScope.Self,
            Magnitude = 10f,
        }));

        Assert.That(codes, Contains.Item("augment.trigger_op_unimplemented"));
    }

    [Test]
    public void Flags_ApplyStatus_WithEmptyStatusId()
    {
        var codes = ValidateCodes(BuildAugment(new TriggeredEffectSpec
        {
            Trigger = CombatTriggerKind.BattleStart,
            Op = TriggeredEffectOp.ApplyStatus,
            Scope = EffectScope.AlliedCombatants,
            StatusId = "",
            DurationSeconds = 5f,
        }));

        Assert.That(codes, Contains.Item("augment.trigger_status_id"));
    }

    [Test]
    public void Flags_HealOp_WithNonPositiveMagnitude()
    {
        var codes = ValidateCodes(BuildAugment(new TriggeredEffectSpec
        {
            Trigger = CombatTriggerKind.OnKill,
            Op = TriggeredEffectOp.Heal,
            Scope = EffectScope.Self,
            Magnitude = 0f,
        }));

        Assert.That(codes, Contains.Item("augment.trigger_magnitude"));
    }

    [Test]
    public void Flags_OnHpBelow_WithThresholdOutOfRange()
    {
        var codes = ValidateCodes(BuildAugment(new TriggeredEffectSpec
        {
            Trigger = CombatTriggerKind.OnHpBelow,
            Op = TriggeredEffectOp.Barrier,
            Scope = EffectScope.Self,
            Magnitude = 30f,
            ThresholdRatio = 0f,
        }));

        Assert.That(codes, Contains.Item("augment.trigger_threshold"));
    }

    [Test]
    public void Accepts_WellFormedTriggeredEffect_NoTriggerLint()
    {
        // augment_gold_bastion 형태 (BattleStart / Barrier / Self / Magnitude 40) — 실제 author된 유효 효과.
        var codes = ValidateCodes(BuildAugment(new TriggeredEffectSpec
        {
            Trigger = CombatTriggerKind.BattleStart,
            Op = TriggeredEffectOp.Barrier,
            Scope = EffectScope.Self,
            Magnitude = 40f,
        }));

        var triggerCodes = codes.Where(code => code.StartsWith("augment.trigger")).ToArray();
        Assert.That(triggerCodes, Is.Empty, "Well-formed triggered effect must not raise trigger lint: " + string.Join(", ", triggerCodes));
    }
}
