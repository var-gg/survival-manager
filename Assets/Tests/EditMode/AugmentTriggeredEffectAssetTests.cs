using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Contracts;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// wave-augment-depth Inc 1b-content — 실제 augment .asset 에 author된 starter 트리거 효과가
/// 올바른 값으로 역직렬화되는지 검증 (enum int 매핑 실수까지 포착 = 실제 콘텐츠 경로 확인).
/// </summary>
[Category("BatchOnly")]
public sealed class AugmentTriggeredEffectAssetTests
{
    private static AugmentDefinition Load(string id)
    {
        var augment = Resources.Load<AugmentDefinition>($"_Game/Content/Definitions/Augments/{id}");
        Assert.That(augment, Is.Not.Null, $"Augment asset not found: {id}");
        return augment;
    }

    [Test]
    public void GoldBastion_HasBattleStartSelfBarrier()
    {
        var augment = Load("augment_gold_bastion");
        Assert.That(augment.TriggeredEffects, Has.Count.EqualTo(1));
        var effect = augment.TriggeredEffects[0];
        Assert.That(effect.Trigger, Is.EqualTo(CombatTriggerKind.BattleStart));
        Assert.That(effect.Op, Is.EqualTo(TriggeredEffectOp.Barrier));
        Assert.That(effect.Scope, Is.EqualTo(EffectScope.Self));
        Assert.That(effect.Magnitude, Is.EqualTo(40f));
    }

    [Test]
    public void GoldFury_HasOnKillSelfHeal()
    {
        var augment = Load("augment_gold_fury");
        Assert.That(augment.TriggeredEffects, Has.Count.EqualTo(1));
        var effect = augment.TriggeredEffects[0];
        Assert.That(effect.Trigger, Is.EqualTo(CombatTriggerKind.OnKill));
        Assert.That(effect.Op, Is.EqualTo(TriggeredEffectOp.Heal));
        Assert.That(effect.Scope, Is.EqualTo(EffectScope.Self));
        Assert.That(effect.Magnitude, Is.EqualTo(8f));
    }

    [Test]
    public void PlatinumReckoning_HasOnHpBelowSelfBarrier()
    {
        var augment = Load("augment_platinum_reckoning");
        Assert.That(augment.TriggeredEffects, Has.Count.EqualTo(1));
        var effect = augment.TriggeredEffects[0];
        Assert.That(effect.Trigger, Is.EqualTo(CombatTriggerKind.OnHpBelow));
        Assert.That(effect.Op, Is.EqualTo(TriggeredEffectOp.Barrier));
        Assert.That(effect.Scope, Is.EqualTo(EffectScope.Self));
        Assert.That(effect.ThresholdRatio, Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(effect.Magnitude, Is.EqualTo(60f));
    }

    [Test]
    public void SilverGuard_HasBattleStartTeamGuarded()
    {
        var augment = Load("augment_silver_guard");
        Assert.That(augment.TriggeredEffects, Has.Count.EqualTo(1));
        var effect = augment.TriggeredEffects[0];
        Assert.That(effect.Trigger, Is.EqualTo(CombatTriggerKind.BattleStart));
        Assert.That(effect.Op, Is.EqualTo(TriggeredEffectOp.ApplyStatus));
        Assert.That(effect.Scope, Is.EqualTo(EffectScope.AlliedCombatants));
        Assert.That(effect.StatusId, Is.EqualTo("guarded"));
        Assert.That(effect.DurationSeconds, Is.EqualTo(8f));
    }
}
