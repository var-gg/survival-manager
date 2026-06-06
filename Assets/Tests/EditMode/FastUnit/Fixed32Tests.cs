using NUnit.Framework;
using SM.Core.Numerics;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 1 numeric foundation 검증 (ADR-0029). <see cref="Fixed32"/>/<see cref="FixedMath"/>의 변환·사칙·
/// 반올림(truncate-toward-zero, 음수 대칭)·Sqrt·PowInt·div0를 격리 상태로 못박는다. sim 미사용.
/// </summary>
[Category("FastUnit")]
public sealed class Fixed32Tests
{
    [Test]
    public void Conversion_RoundTrips()
    {
        Assert.That(Fixed32.FromInt(5).ToFloat(), Is.EqualTo(5f));
        Assert.That(Fixed32.FromFloatQuantized(0.5f).ToFloat(), Is.EqualTo(0.5f));   // 정확 표현
        Assert.That(Fixed32.FromFloatQuantized(0.25f).ToFloat(), Is.EqualTo(0.25f));
        Assert.That(Fixed32.FromFloatQuantized(1.1f).ToFloat(), Is.EqualTo(1.1f).Within(0.0001f)); // 양자화 오차 한계
        Assert.That(Fixed32.One.Raw, Is.EqualTo(65536));
    }

    [Test]
    public void Arithmetic_KnownValues()
    {
        Assert.That(Fixed32.FromInt(3) * Fixed32.FromInt(4), Is.EqualTo(Fixed32.FromInt(12)));
        Assert.That(Fixed32.FromInt(12) / Fixed32.FromInt(4), Is.EqualTo(Fixed32.FromInt(3)));
        Assert.That(Fixed32.One + Fixed32.One, Is.EqualTo(Fixed32.FromInt(2)));
        Assert.That(Fixed32.FromFloatQuantized(0.5f) * Fixed32.FromFloatQuantized(0.5f),
            Is.EqualTo(Fixed32.FromFloatQuantized(0.25f)));
    }

    [Test]
    public void Multiply_TruncatesTowardZero_Symmetrically()
    {
        var a = Fixed32.FromFloatQuantized(1.1f);
        var b = Fixed32.FromFloatQuantized(1.3f);

        // 음수 부호는 결과를 반전만 시킨다(floor였다면 비대칭). 단일 반올림 규칙의 핵심 불변식.
        Assert.That(((-a) * b).Raw, Is.EqualTo(-((a * b).Raw)), "음수 mul이 -(양수 mul)과 다르다(반올림 비대칭).");
        Assert.That((a * (-b)).Raw, Is.EqualTo(-((a * b).Raw)));
    }

    [Test]
    public void ShiftRightTrunc_RoundsTowardZero_NotFloor()
    {
        Assert.That(FixedMath.ShiftRightTrunc(3L, 1), Is.EqualTo(1L));    // 1.5 -> 1
        Assert.That(FixedMath.ShiftRightTrunc(-3L, 1), Is.EqualTo(-1L));  // -1.5 -> -1 (floor였다면 -2)
        Assert.That(FixedMath.ShiftRightTrunc(-8L, 1), Is.EqualTo(-4L));  // 정확
    }

    [Test]
    public void Sqrt_PerfectSquares_AndMonotonic_AndIrrationalFloor()
    {
        Assert.That(FixedMath.Sqrt(Fixed32.FromInt(4)), Is.EqualTo(Fixed32.FromInt(2)));
        Assert.That(FixedMath.Sqrt(Fixed32.FromInt(9)), Is.EqualTo(Fixed32.FromInt(3)));
        Assert.That(FixedMath.Sqrt(Fixed32.FromInt(144)), Is.EqualTo(Fixed32.FromInt(12)));
        Assert.That(FixedMath.Sqrt(Fixed32.FromInt(2)).Raw, Is.LessThan(FixedMath.Sqrt(Fixed32.FromInt(3)).Raw));
        Assert.That(FixedMath.Sqrt(Fixed32.FromInt(2)).ToFloat(), Is.EqualTo(1.41421f).Within(0.001f));
        Assert.That(FixedMath.Sqrt(Fixed32.FromInt(-5)), Is.EqualTo(Fixed32.Zero)); // 음수 → 0
    }

    [Test]
    public void PowInt_KnownValues()
    {
        Assert.That(FixedMath.PowInt(Fixed32.FromInt(2), 10), Is.EqualTo(Fixed32.FromInt(1024)));
        Assert.That(FixedMath.PowInt(Fixed32.FromFloatQuantized(0.75f), 0), Is.EqualTo(Fixed32.One));
        var x = Fixed32.FromFloatQuantized(0.75f);
        Assert.That(FixedMath.PowInt(x, 1), Is.EqualTo(x));
    }

    [Test]
    public void Divide_ByZero_ReturnsZero()
    {
        Assert.That(Fixed32.FromInt(5) / Fixed32.Zero, Is.EqualTo(Fixed32.Zero));
    }

    [Test]
    public void Helpers_AbsMinMaxSignClamp()
    {
        Assert.That(Fixed32.Abs(Fixed32.FromInt(-7)), Is.EqualTo(Fixed32.FromInt(7)));
        Assert.That(Fixed32.Min(Fixed32.FromInt(3), Fixed32.FromInt(5)), Is.EqualTo(Fixed32.FromInt(3)));
        Assert.That(Fixed32.Max(Fixed32.FromInt(3), Fixed32.FromInt(5)), Is.EqualTo(Fixed32.FromInt(5)));
        Assert.That(Fixed32.Sign(Fixed32.FromInt(-2)), Is.EqualTo(-1));
        Assert.That(Fixed32.Sign(Fixed32.Zero), Is.EqualTo(0));
        Assert.That(Fixed32.Clamp(Fixed32.FromInt(9), Fixed32.FromInt(0), Fixed32.FromInt(5)), Is.EqualTo(Fixed32.FromInt(5)));
        Assert.That(Fixed32.Clamp(Fixed32.FromInt(-9), Fixed32.FromInt(0), Fixed32.FromInt(5)), Is.EqualTo(Fixed32.Zero));
    }
}
