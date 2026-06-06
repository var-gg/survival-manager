using NUnit.Framework;
using SM.Core.Numerics;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 1 wide 도메인 타입 검증 (ADR-0029 §Range budget). <see cref="Score64"/>(누산 전용)·<see cref="Hp64"/>·
/// <see cref="Resource64"/>(Wide×Fixed32만)의 widen·누산·스케일·clamp·비교를 격리 상태로 못박고, 핵심 불변식인
/// 오버플로 범위 예산(누산이 Int32를, Hp64×Fixed32가 Int64 한계를 넘지 않음)을 고정한다. sim 미사용.
/// </summary>
[Category("FastUnit")]
public sealed class WideFixedTypesTests
{
    // ---- Score64 (누산 전용) ----

    [Test]
    public void Score64_FromFixed_Widens_AndAccumulatesPastInt32()
    {
        // Fixed32.FromInt(20000).Raw = 1,310,720,000 (< int.MaxValue, Fixed32가 담음).
        // 3회 누산 = 3,932,160,000 > int.MaxValue(2,147,483,647) → Int32 누산기였다면 오버플로.
        var term = Score64.FromFixed(Fixed32.FromInt(20000));
        var acc = Score64.Zero + term + term + term;

        Assert.That(acc.Raw, Is.EqualTo(3_932_160_000L));
        Assert.That(acc.Raw, Is.GreaterThan(int.MaxValue),
            "Int32 누산이었다면 오버플로 — Score64가 Int64여야 하는 직접 근거.");
    }

    [Test]
    public void Score64_AddSubUnary_Symmetric()
    {
        var a = Score64.FromInt(1500);
        var b = Score64.FromInt(420);
        Assert.That(a + b - b, Is.EqualTo(a));
        Assert.That(-a, Is.EqualTo(Score64.Zero - a));
    }

    [Test]
    public void Score64_Comparison_AndTieBreakOrdering()
    {
        var lo = Score64.FromInt(10);
        var hi = Score64.FromInt(11);
        Assert.That(lo < hi, Is.True);
        Assert.That(hi > lo, Is.True);
        Assert.That(lo.CompareTo(hi), Is.LessThan(0));   // 정렬 키
        Assert.That(Score64.Max(lo, hi), Is.EqualTo(hi));
        Assert.That(Score64.Min(lo, hi), Is.EqualTo(lo));
    }

    // ---- Hp64 (Wide × Fixed32) ----

    [Test]
    public void Hp64_FromInt_Arithmetic_AndCompare()
    {
        Assert.That(Hp64.FromInt(100) + Hp64.FromInt(40), Is.EqualTo(Hp64.FromInt(140)));
        Assert.That(Hp64.FromInt(100) - Hp64.FromInt(140), Is.EqualTo(Hp64.FromInt(-40)));
        Assert.That(Hp64.FromInt(40) < Hp64.FromInt(100), Is.True);
        Assert.That(Hp64.FromInt(100).Raw, Is.EqualTo(100L * Fixed32.OneRaw));
    }

    [Test]
    public void Hp64_ScaleByFixed_KnownValues_AndCommutative()
    {
        var hp = Hp64.FromInt(100);
        var half = Fixed32.FromFloatQuantized(0.5f);
        Assert.That(hp * half, Is.EqualTo(Hp64.FromInt(50)));
        Assert.That(half * hp, Is.EqualTo(hp * half));                 // 교환
        Assert.That(Hp64.FromInt(200) * Fixed32.FromInt(3), Is.EqualTo(Hp64.FromInt(600)));
    }

    [Test]
    public void Hp64_ScaleByFixed_TruncatesTowardZero_Symmetrically()
    {
        var hp = Hp64.FromInt(7);
        var scale = Fixed32.FromFloatQuantized(0.3f); // 비정확 표현 → 절단 발생
        // 음수 부호는 결과를 반전만 시킨다(floor였다면 비대칭). Fixed32 mul과 동일 규칙.
        Assert.That(((-hp) * scale).Raw, Is.EqualTo(-((hp * scale).Raw)));
        Assert.That((hp * (-scale)).Raw, Is.EqualTo(-((hp * scale).Raw)));
    }

    [Test]
    public void Hp64_Clamp_Saturates()
    {
        var lo = Hp64.Zero;
        var hi = Hp64.FromInt(500);
        Assert.That(Hp64.Clamp(Hp64.FromInt(600), lo, hi), Is.EqualTo(hi));
        Assert.That(Hp64.Clamp(Hp64.FromInt(-30), lo, hi), Is.EqualTo(lo));
        Assert.That(Hp64.Clamp(Hp64.FromInt(250), lo, hi), Is.EqualTo(Hp64.FromInt(250)));
    }

    [Test]
    public void Hp64_ToFloat_Egress_RoundTrips()
    {
        Assert.That(Hp64.FromInt(1234).ToFloat(), Is.EqualTo(1234f));
        Assert.That((Hp64.FromInt(100) * Fixed32.FromFloatQuantized(0.25f)).ToFloat(),
            Is.EqualTo(25f).Within(0.001f));
    }

    // ---- 오버플로 범위 예산 (핵심 불변식) ----

    [Test]
    public void OverflowBudget_HpCeiling_TimesMaxMultiplier_StaysExactInInt64()
    {
        // ceiling 1,000,000 × mult 4 = 4,000,000. 중간곱 raw = 6.55e10 × 2.62e5 ≈ 1.72e16 < Int64 9.2e18.
        // 결과가 정확히 일치하면 wrap이 없었다는 증거(wrap이면 garbage).
        var hp = Hp64.FromInt(1_000_000);
        var mult = Fixed32.FromInt(4);
        var result = hp * mult;

        Assert.That(result, Is.EqualTo(Hp64.FromInt(4_000_000)));
        Assert.That(result.Raw, Is.EqualTo(4_000_000L * Fixed32.OneRaw)); // 262,144,000,000
        // 중간곱이 Int64 한계 대비 충분한 여유(~536×)를 가짐을 문서화.
        Assert.That((long)hp.Raw * mult.Raw, Is.LessThan(long.MaxValue / 100));
    }

    // ---- Resource64 (Hp64 계약 미러, 단위만 다름) ----

    [Test]
    public void Resource64_MirrorsWideContract()
    {
        var energy = Resource64.FromInt(80);
        var regen = Fixed32.FromFloatQuantized(1.5f);
        Assert.That(energy * regen, Is.EqualTo(Resource64.FromInt(120)));
        Assert.That(Resource64.Clamp(Resource64.FromInt(200), Resource64.Zero, Resource64.FromInt(100)),
            Is.EqualTo(Resource64.FromInt(100)));
        Assert.That(Resource64.FromInt(30) + Resource64.FromInt(10), Is.EqualTo(Resource64.FromInt(40)));
        Assert.That(Resource64.FromInt(50).ToFloat(), Is.EqualTo(50f));
    }
}
