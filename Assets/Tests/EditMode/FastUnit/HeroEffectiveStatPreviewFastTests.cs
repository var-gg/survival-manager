using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// HeroEffectiveStatPreview.Resolve 검증 — base + numeric package 를 전투(StatBlock)와 동일하게 합쳐
/// effective/delta 를 산출하는지. 합산 수식 자체는 StatBlock(별도 테스트)에 위임하므로 여기선
/// 시트 표면화 계약(키 순서·delta·없는 키·null 가드)에 집중한다.
/// </summary>
[Category("FastUnit")]
public sealed class HeroEffectiveStatPreviewFastTests
{
    private static CombatModifierPackage Package(string sourceId, params StatModifier[] modifiers)
    {
        return new CombatModifierPackage(sourceId, ModifierSource.Item, modifiers.ToList());
    }

    private static StatModifier Modifier(StatKey stat, ModifierOp op, float value)
    {
        return new StatModifier(stat, op, value, ModifierSource.Item, "test");
    }

    [Test]
    public void Resolve_FlatModifier_AddsToEffective_DeltaIsFlat()
    {
        var baseStats = new Dictionary<StatKey, float> { [StatKey.MaxHealth] = 100f };
        var packages = new[] { Package("item", Modifier(StatKey.MaxHealth, ModifierOp.Flat, 50f)) };

        var entries = HeroEffectiveStatPreview.Resolve(baseStats, packages, new[] { StatKey.MaxHealth });

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(100f, entries[0].BaseValue, 0.001f);
        Assert.AreEqual(150f, entries[0].EffectiveValue, 0.001f);
        Assert.AreEqual(50f, entries[0].Delta, 0.001f);
    }

    [Test]
    public void Resolve_AppliesFlatThenIncreasedThenMore_MatchesStatBlockOrder()
    {
        var baseStats = new Dictionary<StatKey, float> { [StatKey.PhysPower] = 100f };
        var packages = new[]
        {
            Package(
                "stack",
                Modifier(StatKey.PhysPower, ModifierOp.Flat, 10f),
                Modifier(StatKey.PhysPower, ModifierOp.Increased, 0.5f),
                Modifier(StatKey.PhysPower, ModifierOp.More, 0.2f)),
        };

        var entries = HeroEffectiveStatPreview.Resolve(baseStats, packages, new[] { StatKey.PhysPower });

        // (100 + 10) * (1 + 0.5) * (1 + 0.2) = 198
        Assert.AreEqual(198f, entries[0].EffectiveValue, 0.01f);
        Assert.AreEqual(98f, entries[0].Delta, 0.01f);
    }

    [Test]
    public void Resolve_ModifierOnOtherStat_DoesNotBleed()
    {
        var baseStats = new Dictionary<StatKey, float>
        {
            [StatKey.Armor] = 30f,
            [StatKey.PhysPower] = 20f,
        };
        var packages = new[] { Package("weapon", Modifier(StatKey.PhysPower, ModifierOp.Flat, 100f)) };

        var entries = HeroEffectiveStatPreview.Resolve(baseStats, packages, new[] { StatKey.Armor });

        Assert.AreEqual(30f, entries[0].EffectiveValue, 0.001f, "다른 스탯 modifier 는 Armor 에 영향 없음");
        Assert.AreEqual(0f, entries[0].Delta, 0.001f);
    }

    [Test]
    public void Resolve_NoPackages_EffectiveEqualsBase_NoDelta()
    {
        var baseStats = new Dictionary<StatKey, float> { [StatKey.MaxHealth] = 250f };

        var entries = HeroEffectiveStatPreview.Resolve(baseStats, null, new[] { StatKey.MaxHealth });

        Assert.AreEqual(250f, entries[0].EffectiveValue, 0.001f);
        Assert.AreEqual(0f, entries[0].Delta, 0.001f);
    }

    [Test]
    public void Resolve_PreservesKeyOrder_AndOneEntryPerKey()
    {
        var baseStats = new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = 100f,
            [StatKey.Armor] = 30f,
            [StatKey.PhysPower] = 20f,
        };
        var keys = new[] { StatKey.PhysPower, StatKey.MaxHealth, StatKey.Armor };

        var entries = HeroEffectiveStatPreview.Resolve(baseStats, null, keys);

        CollectionAssert.AreEqual(keys, entries.Select(entry => entry.Key).ToArray());
    }

    [Test]
    public void Resolve_MissingBaseEntry_TreatedAsZero()
    {
        var baseStats = new Dictionary<StatKey, float> { [StatKey.MaxHealth] = 100f };

        // SkillHaste 는 base 에 없음 → base 0, modifier 없음 → effective 0.
        var entries = HeroEffectiveStatPreview.Resolve(baseStats, null, new[] { StatKey.SkillHaste });

        Assert.AreEqual(0f, entries[0].BaseValue, 0.001f);
        Assert.AreEqual(0f, entries[0].EffectiveValue, 0.001f);
    }

    [Test]
    public void Resolve_NullBaseStats_ReturnsEmpty()
    {
        var entries = HeroEffectiveStatPreview.Resolve(null, null, new[] { StatKey.MaxHealth });

        Assert.IsEmpty(entries);
    }

    [Test]
    public void Resolve_EmptyKeys_ReturnsEmpty()
    {
        var baseStats = new Dictionary<StatKey, float> { [StatKey.MaxHealth] = 100f };

        var entries = HeroEffectiveStatPreview.Resolve(baseStats, null, System.Array.Empty<StatKey>());

        Assert.IsEmpty(entries);
    }

    [Test]
    public void Resolve_NullUnit_ReturnsEmpty()
    {
        BattleUnitLoadout unit = null;
        var entries = HeroEffectiveStatPreview.Resolve(unit, new[] { StatKey.MaxHealth });

        Assert.IsEmpty(entries);
    }
}
