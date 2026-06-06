using NUnit.Framework;
using SM.Core.Numerics;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 1 포맷 판정 (ADR-0029 §2). 실제 sim 수치 패턴 3종 — 1000틱 누적 이동·range threshold lenSq 판별·
/// tiny-vector normalize — 을 Q16.16로 돌려 drift/precision/underflow floor를 측정한다. 통과 시 좌표 단일
/// Q16.16 확정(Q8.24 분리 불요), 실패 시 좌표 국소 승급. normalize는 FixedVector2(Phase 3) 전이라 inline probe.
/// sim 미사용.
/// </summary>
[Category("FastUnit")]
public sealed class FixedFormatDriftTests
{
    // FixedVector2 도입 전 포맷 probe용 inline 2D normalize. lenSq==0이면 fallback floor(방향 소실)를 노출.
    private static (Fixed32 nx, Fixed32 ny, Fixed32 lenSq) Normalize(Fixed32 x, Fixed32 y)
    {
        var lenSq = x * x + y * y;
        if (lenSq.Raw == 0)
        {
            return (Fixed32.Zero, Fixed32.Zero, lenSq);
        }

        var len = FixedMath.Sqrt(lenSq);
        return (x / len, y / len, lenSq);
    }

    [Test]
    public void Accumulation_1000Ticks_AdditionIsDriftFree()
    {
        // 고정소수 덧셈은 정확 → 1000틱 누적 == step × 1000 (비트 단위 동일, 누적 반올림 drift 없음).
        // 이것이 cross-platform 결정성의 핵심: 모든 백엔드가 동일 정수합을 낸다.
        var angle = AngleTurn32.FromDegreesQuantized(37); // 무리수에 가까운 방향
        var stepX = FixedMath.CosTurns(angle) * Fixed32.FromFloatQuantized(0.03f); // per-tick 변위
        var stepY = FixedMath.SinTurns(angle) * Fixed32.FromFloatQuantized(0.03f);

        var posX = Fixed32.Zero;
        var posY = Fixed32.Zero;
        for (var tick = 0; tick < 1000; tick++)
        {
            posX += stepX;
            posY += stepY;
        }

        Assert.That(posX, Is.EqualTo(stepX * Fixed32.FromInt(1000)));
        Assert.That(posY, Is.EqualTo(stepY * Fixed32.FromInt(1000)));
    }

    [Test]
    public void Accumulation_1000Ticks_PrecisionWithinThreshold()
    {
        // 누적 변위가 연속 이상값 대비 충분히 정확한지(LUT 1ULP + 변위 양자화의 1000× 증폭).
        const double deg = 37.0;
        var angle = AngleTurn32.FromDegreesQuantized(deg);
        var stepX = FixedMath.CosTurns(angle) * Fixed32.FromFloatQuantized(0.03f);

        var posX = Fixed32.Zero;
        for (var tick = 0; tick < 1000; tick++)
        {
            posX += stepX;
        }

        var ideal = System.Math.Cos(deg * System.Math.PI / 180.0) * 0.03 * 1000.0;
        var drift = System.Math.Abs(posX.ToFloat() - ideal);
        Assert.That(drift, Is.LessThan(0.02), $"1000틱 누적 drift={drift:F6} (ideal={ideal:F4}, fixed={posX.ToFloat():F4})");
    }

    [Test]
    public void RangeThreshold_LenSq_ClassifiesAndDiscriminates()
    {
        // 사거리 판정은 Sqrt 회피하고 lenSq vs R² 비교. Q16.16(해상도 ~1.5e-5)이 arena 스케일에서
        // in/out을 정확히 가르고, 0.01단위 거리차를 분해하는지.
        var rSq = Fixed32.FromFloatQuantized(6.25f); // R=2.5
        var nearSq = Fixed32.FromFloatQuantized(2.49f) * Fixed32.FromFloatQuantized(2.49f);
        var farSq = Fixed32.FromFloatQuantized(2.51f) * Fixed32.FromFloatQuantized(2.51f);

        Assert.That(nearSq < rSq, Is.True, $"nearSq={nearSq.ToFloat()} 가 R²=6.25 미만이어야");
        Assert.That(farSq > rSq, Is.True, $"farSq={farSq.ToFloat()} 가 R²=6.25 초과여야");
        Assert.That(nearSq < farSq, Is.True);

        // 미세 판별: 2.50 vs 2.51 (거리 0.01차)도 lenSq에서 구분 가능 + 갭이 해상도보다 훨씬 큼.
        var aSq = Fixed32.FromFloatQuantized(2.50f) * Fixed32.FromFloatQuantized(2.50f);
        var bSq = Fixed32.FromFloatQuantized(2.51f) * Fixed32.FromFloatQuantized(2.51f);
        Assert.That(bSq.Raw, Is.GreaterThan(aSq.Raw));
        Assert.That((bSq - aSq).ToFloat(), Is.GreaterThan(0.04)); // 갭 ~0.05 >> 1.5e-5
    }

    [Test]
    public void RangeThreshold_MaxArenaDistance_Representable()
    {
        // doc §Range budget: lenSq ≤ ~290, distance ≤ ~17. 둘 다 Q16.16(±32768)에 큰 여유.
        var maxLenSq = Fixed32.FromInt(290);
        Assert.That(maxLenSq.Raw, Is.EqualTo(290 * Fixed32.OneRaw)); // 오버플로 없음
        Assert.That(FixedMath.Sqrt(maxLenSq).ToFloat(), Is.EqualTo(17.0294f).Within(0.01f));
    }

    [Test]
    public void TinyVector_Normalize_LengthHoldsDownToFloor()
    {
        // 길이 L 벡터를 여러 방향으로 정규화 → 결과 길이가 1에 수렴하는지. L이 작아질수록 성분² 절단이
        // 누적되지만 L ≥ 0.02까지 오차는 gameplay-무관 수준.
        (double L, double tol)[] cases =
        {
            (1.0, 0.002), (0.5, 0.004), (0.1, 0.01), (0.05, 0.02), (0.02, 0.04),
        };
        int[] degrees = { 0, 30, 45, 73 };

        foreach (var (l, tol) in cases)
        {
            foreach (var deg in degrees)
            {
                var a = AngleTurn32.FromDegreesQuantized(deg);
                var scale = Fixed32.FromFloatQuantized((float)l);
                var x = FixedMath.CosTurns(a) * scale;
                var y = FixedMath.SinTurns(a) * scale;

                var (nx, ny, lenSq) = Normalize(x, y);
                Assert.That(lenSq.Raw, Is.GreaterThan(0), $"L={l} deg={deg}: lenSq underflow");

                var nlen = FixedMath.Sqrt(nx * nx + ny * ny).ToFloat();
                Assert.That(nlen, Is.EqualTo(1f).Within(tol), $"L={l} deg={deg} normalized len={nlen:F5}");
            }
        }
    }

    [Test]
    public void TinyVector_BelowFloor_LenSqUnderflows_RequiresFallback()
    {
        // 성분 < 256/65536 ≈ 0.0039면 성분²(raw² >> 16)이 0으로 절단 → lenSq=0 → 정규화 불능.
        // 따라서 Phase 3 NormalizeOrFallback은 lenSq==0(또는 < ε²) floor에서 기본 방향을 반환해야 한다.
        var x = Fixed32.FromFloatQuantized(0.003f); // raw 196, 196² >> 16 = 0
        var y = Fixed32.FromFloatQuantized(0.003f);
        var lenSq = x * x + y * y;

        Assert.That(lenSq.Raw, Is.EqualTo(0), "성분² underflow로 lenSq=0 — fallback floor의 존재 근거");

        var (nx, ny, _) = Normalize(x, y);
        Assert.That(nx, Is.EqualTo(Fixed32.Zero)); // floor: 방향 소실 → 호출부가 fallback 책임
        Assert.That(ny, Is.EqualTo(Fixed32.Zero));
    }
}
