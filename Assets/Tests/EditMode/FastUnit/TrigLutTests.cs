using NUnit.Framework;
using SM.Core.Numerics;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 1 sin/cos turn-LUT 검증 (ADR-0029 §LUT/angle). <see cref="SinLut"/> 테이블·<see cref="AngleTurn32"/>
/// (BAM)·<see cref="FixedMath.SinTurns"/>/<see cref="FixedMath.CosTurns"/>를 격리 상태로 못박는다. cardinal
/// 정확값·피타고라스 항등식·홀짝 대칭(±1 ULP)·단조·테이블 핀-해시 락·각 wrap/ingress. 런타임 MathF 미사용. sim 미사용.
/// </summary>
[Category("FastUnit")]
public sealed class TrigLutTests
{
    // FNV-1a 64 / little-endian 4-byte (tools/gen-sin-lut.ps1 및 BattleStateCanonicalHash와 동일 규약).
    private static string ComputeFnv1a64(int[] values)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var v in values)
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                var b = (byte)((v >> shift) & 0xFF);
                hash ^= b;
                hash *= prime;
            }
        }

        return hash.ToString("x16");
    }

    [Test]
    public void Lut_Length_And_CardinalNodes_Exact()
    {
        Assert.That(SinLut.Values.Length, Is.EqualTo(4096));
        Assert.That(SinLut.EntryCount, Is.EqualTo(4096));
        Assert.That(SinLut.Values[0], Is.EqualTo(0));        // sin 0
        Assert.That(SinLut.Values[1024], Is.EqualTo(65536)); // sin 90°  = One.Raw
        Assert.That(SinLut.Values[2048], Is.EqualTo(0));     // sin 180°
        Assert.That(SinLut.Values[3072], Is.EqualTo(-65536)); // sin 270° = -One.Raw
    }

    [Test]
    public void SinTurns_CardinalAngles_Exact()
    {
        Assert.That(FixedMath.SinTurns(AngleTurn32.Zero), Is.EqualTo(Fixed32.Zero));
        Assert.That(FixedMath.SinTurns(AngleTurn32.QuarterTurn), Is.EqualTo(Fixed32.One));
        Assert.That(FixedMath.SinTurns(AngleTurn32.HalfTurn), Is.EqualTo(Fixed32.Zero));
        Assert.That(FixedMath.SinTurns(AngleTurn32.ThreeQuarterTurn), Is.EqualTo(-Fixed32.One));
    }

    [Test]
    public void CosTurns_CardinalAngles_Exact()
    {
        Assert.That(FixedMath.CosTurns(AngleTurn32.Zero), Is.EqualTo(Fixed32.One));
        Assert.That(FixedMath.CosTurns(AngleTurn32.QuarterTurn), Is.EqualTo(Fixed32.Zero));
        Assert.That(FixedMath.CosTurns(AngleTurn32.HalfTurn), Is.EqualTo(-Fixed32.One));
        Assert.That(FixedMath.CosTurns(AngleTurn32.ThreeQuarterTurn), Is.EqualTo(Fixed32.Zero));
    }

    [Test]
    public void SinTurns_OddSymmetry_WithinOneUlp()
    {
        // sin(-x) = -sin(x). 노드에선 정확, 비노드에선 보간 절단으로 ±1 ULP(결정적 양자화 artifact).
        uint[] raws = { 0x00000000u, 0x10000000u, 0x12345678u, 0x40000000u, 0x55555555u, 0x7FFFFFFFu, 0xABCDEF01u };
        foreach (var r in raws)
        {
            var a = AngleTurn32.FromRaw(r);
            Assert.That(FixedMath.SinTurns(-a).Raw, Is.EqualTo(-FixedMath.SinTurns(a).Raw).Within(1), $"raw=0x{r:x8}");
        }
    }

    [Test]
    public void SinCos_PythagoreanIdentity_WithinTolerance()
    {
        // sin²+cos² ≈ 1. 동일 테이블 + Q16.16 → 오차는 양자화·보간·mul 절단 합(~1e-4) 이내.
        for (var deg = 0; deg < 360; deg++)
        {
            var a = AngleTurn32.FromDegreesQuantized(deg);
            var s = FixedMath.SinTurns(a);
            var c = FixedMath.CosTurns(a);
            var identity = (s * s + c * c).ToFloat();
            Assert.That(identity, Is.EqualTo(1f).Within(0.001f), $"deg={deg} sin²+cos²={identity}");
        }
    }

    [Test]
    public void SinTurns_FirstQuadrant_MonotonicNondecreasing()
    {
        var prev = int.MinValue;
        for (var k = 0; k <= 64; k++)
        {
            var raw = (uint)((long)AngleTurn32.QuarterTurn.Raw * k / 64);
            var s = FixedMath.SinTurns(AngleTurn32.FromRaw(raw)).Raw;
            Assert.That(s, Is.GreaterThanOrEqualTo(prev), $"1사분면 비단조 at k={k}");
            prev = s;
        }

        Assert.That(FixedMath.SinTurns(AngleTurn32.FromRaw(0u)).Raw, Is.EqualTo(0));
        Assert.That(FixedMath.SinTurns(AngleTurn32.QuarterTurn).Raw, Is.EqualTo(65536));
    }

    [Test]
    public void AngleTurn32_Arithmetic_Wraps()
    {
        Assert.That(AngleTurn32.QuarterTurn + AngleTurn32.QuarterTurn, Is.EqualTo(AngleTurn32.HalfTurn));
        Assert.That(AngleTurn32.HalfTurn + AngleTurn32.HalfTurn, Is.EqualTo(AngleTurn32.Zero));        // uint wrap
        Assert.That(-AngleTurn32.QuarterTurn, Is.EqualTo(AngleTurn32.ThreeQuarterTurn));               // 반사
        Assert.That(AngleTurn32.ThreeQuarterTurn + AngleTurn32.QuarterTurn, Is.EqualTo(AngleTurn32.Zero));
    }

    [Test]
    public void AngleTurn32_Ingress_Quantizes()
    {
        Assert.That(AngleTurn32.FromDegreesQuantized(0), Is.EqualTo(AngleTurn32.Zero));
        Assert.That(AngleTurn32.FromDegreesQuantized(90), Is.EqualTo(AngleTurn32.QuarterTurn));
        Assert.That(AngleTurn32.FromDegreesQuantized(180), Is.EqualTo(AngleTurn32.HalfTurn));
        Assert.That(AngleTurn32.FromDegreesQuantized(270), Is.EqualTo(AngleTurn32.ThreeQuarterTurn));
        Assert.That(AngleTurn32.FromDegreesQuantized(360), Is.EqualTo(AngleTurn32.Zero));               // wrap
        Assert.That(AngleTurn32.FromTurnsQuantized(0.25), Is.EqualTo(AngleTurn32.QuarterTurn));
        Assert.That(AngleTurn32.FromTurnsQuantized(1.25), Is.EqualTo(AngleTurn32.QuarterTurn));         // wrap
    }

    [Test]
    public void SinLut_TableHash_MatchesPin()
    {
        // 테이블 드리프트 락. Values를 손대거나 EntryCount를 바꾸면 이 핀이 깨진다 — 재생성 시
        // tools/gen-sin-lut.ps1 출력 TableHashV1로 갱신할 것.
        Assert.That(ComputeFnv1a64(SinLut.Values), Is.EqualTo("00361da115eb6196"));
    }
}
