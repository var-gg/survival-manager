using System;
using System.Globalization;

namespace SM.Core.Numerics
{
    /// <summary>
    /// 결정적 2D 벡터 (ADR-0029 / Phase 3 spatial). 성분은 <see cref="Fixed32"/>(Q16.16) — 좌표·거리·방향용.
    /// 기존 float <c>CombatVector2</c>를 즉시 대체하지 않고 <b>병행</b> 도입한다(implicit 변환 금지, 명시
    /// quantize/projection만). 모든 normalize는 <see cref="NormalizeOrFallback(FixedVector2)"/>를 경유해
    /// near-coincident 벡터가 양자화 노이즈를 방향으로 증폭하는 것을 막는다. arena 스케일(좌표 ±8, 거리² ≤ ~297)은
    /// 단일 Q16.16로 충분하다(Phase 1 drift 판정).
    /// </summary>
    public readonly struct FixedVector2 : IEquatable<FixedVector2>
    {
        public readonly Fixed32 X;
        public readonly Fixed32 Y;

        public FixedVector2(Fixed32 x, Fixed32 y)
        {
            X = x;
            Y = y;
        }

        public static FixedVector2 FromRaw(int xRaw, int yRaw) => new(Fixed32.FromRaw(xRaw), Fixed32.FromRaw(yRaw));
        public static FixedVector2 FromInt(int x, int y) => new(Fixed32.FromInt(x), Fixed32.FromInt(y));

        public static readonly FixedVector2 Zero = new(Fixed32.Zero, Fixed32.Zero);

        /// <summary>near-coincident 판정 임계(lenSq raw 기준). 이 값 이하면 normalize 대신 fallback 방향을 쓴다.
        /// Phase 1 underflow floor(성분 &lt; ~0.004 → lenSq=0)와 저정밀 구간(길이 0.02~0.05)을 안전하게 덮는다.</summary>
        public static readonly Fixed32 DefaultNormalizeEpsSq = Fixed32.FromRaw(256); // ≈ lenSq 0.0039 ≈ 길이 0.0625

        /// <summary>Ingress 전용(tool/저작 좌표 양자화). authoritative sim 내부 호출 금지(lint 대상).</summary>
        public static FixedVector2 FromFloatQuantized(float x, float y)
            => new(Fixed32.FromFloatQuantized(x), Fixed32.FromFloatQuantized(y));

        public static FixedVector2 operator +(FixedVector2 a, FixedVector2 b) => new(a.X + b.X, a.Y + b.Y);
        public static FixedVector2 operator -(FixedVector2 a, FixedVector2 b) => new(a.X - b.X, a.Y - b.Y);
        public static FixedVector2 operator -(FixedVector2 a) => new(-a.X, -a.Y);
        public static FixedVector2 operator *(FixedVector2 a, Fixed32 scale) => new(a.X * scale, a.Y * scale);
        public static FixedVector2 operator *(Fixed32 scale, FixedVector2 a) => new(a.X * scale, a.Y * scale);

        public Fixed32 Dot(FixedVector2 other) => (X * other.X) + (Y * other.Y);
        public Fixed32 LengthSquared => (X * X) + (Y * Y);
        public Fixed32 Length => FixedMath.Sqrt(LengthSquared);
        public Fixed32 DistanceSquaredTo(FixedVector2 other) => (this - other).LengthSquared;
        public Fixed32 DistanceTo(FixedVector2 other) => (this - other).Length;

        /// <summary>
        /// 결정적 정규화. <paramref name="fallback"/>은 near-coincident(lenSq ≤ <paramref name="epsSq"/>)일 때
        /// 반환할 방향이다 — 호출부가 안정 키 해시 등에서 만들어 넘겨, 양자화 노이즈가 방향을 좌우하지 않게 한다.
        /// </summary>
        public FixedVector2 NormalizeOrFallback(FixedVector2 fallback, Fixed32 epsSq)
        {
            var lenSq = LengthSquared;
            if (lenSq.Raw <= epsSq.Raw)
            {
                return fallback;
            }

            var len = FixedMath.Sqrt(lenSq);
            return new FixedVector2(X / len, Y / len);
        }

        /// <summary><see cref="DefaultNormalizeEpsSq"/>를 쓰는 편의 오버로드.</summary>
        public FixedVector2 NormalizeOrFallback(FixedVector2 fallback)
            => NormalizeOrFallback(fallback, DefaultNormalizeEpsSq);

        /// <summary>선형 보간 a→b, t∈[0,1] (truncate-toward-zero). t 범위 보장은 호출부 책임.</summary>
        public static FixedVector2 Lerp(FixedVector2 a, FixedVector2 b, Fixed32 t)
            => a + ((b - a) * t);

        public static bool operator ==(FixedVector2 a, FixedVector2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(FixedVector2 a, FixedVector2 b) => !(a == b);

        public bool Equals(FixedVector2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is FixedVector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X.Raw, Y.Raw);
        public override string ToString() => $"({X.ToString()}, {Y.ToString()})";
    }
}
