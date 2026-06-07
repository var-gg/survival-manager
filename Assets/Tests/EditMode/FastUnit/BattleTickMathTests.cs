using NUnit.Framework;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 2.2a 정수 틱 변환 권위 (ADR-0029 §Time). <see cref="BattleTickMath.DurationToTicks"/>의 골든 테이블
/// (doc §8 입력)·경계(정수 떨어짐 시 비-부풀림)·최소 1틱·Instant(0틱)를 못박고, 추출 충실도(짧은 클린 케이스에서
/// 기존 float 감산 루프와 동일)를 확인한다. 순수 additive — sim 미rewire(behavior-neutral).
/// </summary>
[Category("FastUnit")]
public sealed class BattleTickMathTests
{
    [TestCase(0.0, 0)]
    [TestCase(0.001, 1)]
    [TestCase(0.05, 1)]
    [TestCase(0.1, 1)]
    [TestCase(0.11, 2)]
    [TestCase(0.2, 2)]
    [TestCase(1.0, 10)]
    [TestCase(3.0, 30)]
    public void DurationToTicks_GoldenTable(double seconds, int expectedTicks)
    {
        Assert.That(BattleTickMath.DurationToTicks(seconds), Is.EqualTo(expectedTicks));
    }

    [Test]
    public void DurationToTicks_NegativeOrZero_IsZeroTicks()
    {
        Assert.That(BattleTickMath.DurationToTicks(0.0), Is.EqualTo(0));    // Instant
        Assert.That(BattleTickMath.DurationToTicks(-1.0), Is.EqualTo(0));
    }

    [Test]
    public void DurationToTicks_IntegerBoundary_DoesNotOverInflate()
    {
        // sec*rate가 정수에 떨어지면 그 값 그대로(eps가 +1 틱 부풀림 방지).
        Assert.That(BattleTickMath.DurationToTicks(0.3), Is.EqualTo(3));   // 3.0 → 3 (not 4)
        Assert.That(BattleTickMath.DurationToTicks(0.29), Is.EqualTo(3));  // 2.9 → 3
        Assert.That(BattleTickMath.DurationToTicks(0.31), Is.EqualTo(4));  // 3.1 → 4
    }

    [Test]
    public void DurationToTicks_TinyPositive_FloorsToOneTick()
    {
        Assert.That(BattleTickMath.DurationToTicks(0.0001), Is.EqualTo(1));
    }

    [Test]
    public void DurationToTicks_MatchesWindupLoop_OnCleanDurations()
    {
        // 추출 충실도: 드리프트 없는 케이스에서 기존 float 감산 루프(StepsUntilResolve)와 동일(cadence 보존).
        foreach (var sec in new[] { 0.1f, 0.11f, 0.2f })
        {
            Assert.That(BattleTickMath.DurationToTicks(sec),
                Is.EqualTo(BattleWindupTickMath.StepsUntilResolve(sec, 0.1f)), $"sec={sec}");
        }
    }

    [Test]
    public void DurationToTicks_FixesFloatWindupLoopDrift()
    {
        // 발견: 0.5s windup은 float 감산 루프가 0.1f 표현오차 누적으로 0을 넘겨 6틱으로 드리프트한다.
        // DurationToTicks는 정수 권위라 수학적으로 정확한 5틱. 2.2c 배선 시 이런 duration의 cadence가
        // ±1틱 바뀐다 → DeterminismBaseline 의도적 재기록 대상(doc §5).
        Assert.That(BattleTickMath.DurationToTicks(0.5), Is.EqualTo(5));
        Assert.That(BattleWindupTickMath.StepsUntilResolve(0.5f, 0.1f),
            Is.Not.EqualTo(BattleTickMath.DurationToTicks(0.5)));
    }
}
