using System.Collections.Generic;
using NUnit.Framework;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 2.1 StatBlock 결정적 순서 (ADR-0029 §우선순위 ①). base 합산의 Dictionary 열거 순서 의존 제거를
/// 잠재 버그(canonical+legacy alias 동시 존재)로 못박고, modifier pipeline(base+flat → ×(1+inc) → ×more →
/// clamp)이 리팩터 후에도 동일 공식임을 고정한다. 현 콘텐츠 bit-neutral(별도 battle 결정성 테스트로 무회귀 확인).
/// </summary>
[Category("FastUnit")]
public sealed class StatBlockDeterminismTests
{
    private static StatBlock Block(Dictionary<StatKey, float> baseValues, params StatModifier[] mods)
        => new(baseValues, mods);

    private static StatModifier Mod(StatKey stat, ModifierOp op, float value)
        => new(stat, op, value, ModifierSource.Other, "test");

    [Test]
    public void Get_BaseAliasCollision_SumsDeterministically()
    {
        // canonical(armor) + legacy alias(defense→armor)가 둘 다 base에 있으면 합산. 정렬 합산이라 열거 순서 무관.
        var block = Block(new Dictionary<StatKey, float> { [StatKey.Armor] = 5f, [StatKey.Defense] = 3f });
        Assert.That(block.Get(StatKey.Armor), Is.EqualTo(8f));
        Assert.That(block.Get(StatKey.Defense), Is.EqualTo(8f)); // legacy 읽기도 canonical로 귀결
    }

    [Test]
    public void Get_ModifierPipeline_MatchesFormula()
    {
        // (base + Σflat) × (1 + Σincreased) × Π(1 + more) → clamp.
        var block = Block(
            new Dictionary<StatKey, float> { [StatKey.PhysPower] = 100f },
            Mod(StatKey.PhysPower, ModifierOp.Flat, 20f),
            Mod(StatKey.PhysPower, ModifierOp.Increased, 0.5f),
            Mod(StatKey.PhysPower, ModifierOp.More, 0.2f));
        // (100+20)*1.5*1.2 = 216
        Assert.That(block.Get(StatKey.PhysPower), Is.EqualTo(216f).Within(0.0001f));
    }

    [Test]
    public void Get_MultipleMore_StackMultiplicatively()
    {
        var block = Block(
            new Dictionary<StatKey, float> { [StatKey.PhysPower] = 100f },
            Mod(StatKey.PhysPower, ModifierOp.More, 0.5f),
            Mod(StatKey.PhysPower, ModifierOp.More, 0.2f));
        // 100 * 1.5 * 1.2 = 180
        Assert.That(block.Get(StatKey.PhysPower), Is.EqualTo(180f).Within(0.0001f));
    }

    [Test]
    public void Get_Clamp_AppliesMinAndMax()
    {
        var capped = Block(
            new Dictionary<StatKey, float> { [StatKey.MoveSpeed] = 100f },
            Mod(StatKey.MoveSpeed, ModifierOp.ClampMax, 50f));
        Assert.That(capped.Get(StatKey.MoveSpeed), Is.EqualTo(50f));

        var floored = Block(
            new Dictionary<StatKey, float> { [StatKey.MoveSpeed] = 10f },
            Mod(StatKey.MoveSpeed, ModifierOp.ClampMin, 25f));
        Assert.That(floored.Get(StatKey.MoveSpeed), Is.EqualTo(25f));
    }

    [Test]
    public void Get_TightestClampWins_AcrossMultipleBounds()
    {
        // ClampMax 여러 개 → 최솟값 채택(가장 빡빡한 상한), ClampMin 여러 개 → 최댓값.
        var block = Block(
            new Dictionary<StatKey, float> { [StatKey.MoveSpeed] = 100f },
            Mod(StatKey.MoveSpeed, ModifierOp.ClampMax, 70f),
            Mod(StatKey.MoveSpeed, ModifierOp.ClampMax, 40f));
        Assert.That(block.Get(StatKey.MoveSpeed), Is.EqualTo(40f));
    }

    [Test]
    public void Get_LegacyAliasModifier_AppliesToCanonical()
    {
        // Defense(→Armor) modifier가 Armor 읽기에 반영(canonicalize 경유).
        var block = Block(
            new Dictionary<StatKey, float> { [StatKey.Armor] = 10f },
            Mod(StatKey.Defense, ModifierOp.Flat, 5f));
        Assert.That(block.Get(StatKey.Armor), Is.EqualTo(15f));
    }

    [Test]
    public void Get_MissingKey_ReturnsZero()
    {
        var block = Block(new Dictionary<StatKey, float>());
        Assert.That(block.Get(StatKey.CritChance), Is.EqualTo(0f));
    }

    // ── Phase 4: fixed stat 권위(GetWide/GetFixed) ──

    [Test]
    public void GetWide_ModifierPipeline_MatchesGetProjection()
    {
        // GetWide(Hp64 fixed 계산)의 float projection이 Get(float)과 같은 공식 결과여야 한다(양자화 오차 내).
        var block = Block(
            new Dictionary<StatKey, float> { [StatKey.PhysPower] = 100f },
            Mod(StatKey.PhysPower, ModifierOp.Flat, 20f),
            Mod(StatKey.PhysPower, ModifierOp.Increased, 0.5f),
            Mod(StatKey.PhysPower, ModifierOp.More, 0.2f));
        // (100+20)*1.5*1.2 = 216
        Assert.That(block.GetWide(StatKey.PhysPower).ToFloat(), Is.EqualTo(216f).Within(0.01f));
        Assert.That(block.GetWide(StatKey.PhysPower).ToFloat(), Is.EqualTo(block.Get(StatKey.PhysPower)).Within(0.01f));
    }

    [Test]
    public void GetFixed_SmallStat_ComputesInFixed()
    {
        // 배수·퍼센트 stat(Int32 범위)은 GetFixed(Fixed32)로. 0.25 × (1+0.2) = 0.30.
        var block = Block(
            new Dictionary<StatKey, float> { [StatKey.CritChance] = 0.25f },
            Mod(StatKey.CritChance, ModifierOp.Increased, 0.2f));
        Assert.That(block.GetFixed(StatKey.CritChance).ToFloat(), Is.EqualTo(0.30f).Within(0.001f));
    }

    [Test]
    public void GetWide_LargeMagnitude_RequiresHp64Authority()
    {
        // MaxHealth 규모(>32768)는 Fixed32 범위를 넘으므로 Hp64 권위(GetWide)가 필요하다.
        var block = Block(new Dictionary<StatKey, float> { [StatKey.MaxHealth] = 250000f });
        Assert.That(block.GetWide(StatKey.MaxHealth).ToFloat(), Is.EqualTo(250000f).Within(1f));
    }
}
