using NUnit.Framework;
using SM.Core.Numerics;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 3.1 결정적 2D 벡터 검증 (ADR-0029). <see cref="FixedVector2"/>의 사칙·Dot·길이·거리·Lerp와
/// 핵심 불변식인 <see cref="FixedVector2.NormalizeOrFallback(FixedVector2)"/>(단위 벡터 정규화 + near-coincident
/// fallback)를 격리 상태로 못박는다. 병행 도입이라 sim 미연결(behavior-neutral).
/// </summary>
[Category("FastUnit")]
public sealed class FixedVector2Tests
{
    [Test]
    public void Arithmetic_AddSubScaleNegate()
    {
        var a = FixedVector2.FromInt(3, 4);
        var b = FixedVector2.FromInt(1, 2);
        Assert.That(a + b, Is.EqualTo(FixedVector2.FromInt(4, 6)));
        Assert.That(a - b, Is.EqualTo(FixedVector2.FromInt(2, 2)));
        Assert.That(a * Fixed32.FromInt(2), Is.EqualTo(FixedVector2.FromInt(6, 8)));
        Assert.That(Fixed32.FromInt(2) * a, Is.EqualTo(FixedVector2.FromInt(6, 8)));
        Assert.That(-a, Is.EqualTo(FixedVector2.FromInt(-3, -4)));
    }

    [Test]
    public void Dot_And_LengthSquared()
    {
        var a = FixedVector2.FromInt(3, 4);
        Assert.That(a.Dot(FixedVector2.FromInt(1, 2)), Is.EqualTo(Fixed32.FromInt(11))); // 3 + 8
        Assert.That(a.LengthSquared, Is.EqualTo(Fixed32.FromInt(25)));                   // 9 + 16
    }

    [Test]
    public void Length_And_Distance_PerfectSquare()
    {
        var a = FixedVector2.FromInt(3, 4);
        Assert.That(a.Length, Is.EqualTo(Fixed32.FromInt(5)));                              // sqrt(25)
        Assert.That(FixedVector2.Zero.DistanceTo(a), Is.EqualTo(Fixed32.FromInt(5)));
        Assert.That(FixedVector2.Zero.DistanceSquaredTo(a), Is.EqualTo(Fixed32.FromInt(25)));
    }

    [Test]
    public void Normalize_UnitVector_HasUnitLength()
    {
        var fallback = FixedVector2.FromInt(1, 0);
        var n = FixedVector2.FromInt(3, 4).NormalizeOrFallback(fallback);
        Assert.That(n.X.ToFloat(), Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(n.Y.ToFloat(), Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(n.Length.ToFloat(), Is.EqualTo(1f).Within(0.002f));
    }

    [Test]
    public void Normalize_TinyVector_ReturnsFallback()
    {
        // 성분 < ~0.004 → 성분² underflow로 lenSq=0 ≤ eps → fallback 방향 반환(양자화 노이즈 차단).
        var fallback = FixedVector2.FromInt(1, 0);
        var tiny = FixedVector2.FromFloatQuantized(0.001f, 0.001f);
        Assert.That(tiny.LengthSquared.Raw, Is.LessThanOrEqualTo(FixedVector2.DefaultNormalizeEpsSq.Raw));
        Assert.That(tiny.NormalizeOrFallback(fallback), Is.EqualTo(fallback));
    }

    [Test]
    public void Lerp_Endpoints_And_Midpoint()
    {
        var a = FixedVector2.FromInt(0, 0);
        var b = FixedVector2.FromInt(10, 0);
        Assert.That(FixedVector2.Lerp(a, b, Fixed32.Zero), Is.EqualTo(a));
        Assert.That(FixedVector2.Lerp(a, b, Fixed32.One), Is.EqualTo(b));
        Assert.That(FixedVector2.Lerp(a, b, Fixed32.FromFloatQuantized(0.5f)), Is.EqualTo(FixedVector2.FromInt(5, 0)));
    }

    [Test]
    public void Equality_And_Inequality()
    {
        Assert.That(FixedVector2.FromInt(3, 4) == FixedVector2.FromInt(3, 4), Is.True);
        Assert.That(FixedVector2.FromInt(3, 4) != FixedVector2.FromInt(3, 5), Is.True);
        Assert.That(FixedVector2.FromInt(3, 4).Equals(FixedVector2.FromInt(3, 4)), Is.True);
    }
}
