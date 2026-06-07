using System;
using System.Globalization;

namespace SM.Core.Numerics
{
    /// <summary>
    /// HP·barrier·damage 규모값용 Q16.16 wide 타입(Int64 backing, ADR-0029 / Phase 1). ceiling ≤ 1,000,000
    /// (오너 가정)이라 Int32(±32768)를 넘어 Int64가 필요하다. 허용 곱은 <c>Hp64 × Fixed32</c>(배수·퍼센트)뿐이며
    /// <see cref="FixedMath.MulWideByFixed"/>를 경유한다 — <c>Hp64 × Hp64</c>는 제공하지 않는다(Wide×Wide
    /// 오버플로). 포화(saturate)는 도메인 사용처에서 <see cref="Clamp"/>로 명시 처리한다(raw 연산자는 wrap 없는
    /// 결정적 정수 산술). <see cref="ToFloat"/>는 read-model egress 전용이다.
    /// </summary>
    public readonly struct Hp64 : IEquatable<Hp64>, IComparable<Hp64>
    {
        public readonly long Raw; // Q16.16

        private Hp64(long raw) => Raw = raw;

        public static Hp64 FromRaw(long raw) => new(raw);
        public static Hp64 FromInt(int value) => new((long)value << Fixed32.FractionBits);
        public static Hp64 FromFixed(Fixed32 value) => new(value.Raw);

        /// <summary>Ingress 전용(stat/저작 float → HP 규모 양자화, truncate-toward-zero). Fixed32(±32768)를 넘는
        /// 규모값을 받으므로 long 경유다. authoritative HP 분기 입력 금지 — Phase 4 stat 경계에서만 호출한다.</summary>
        public static Hp64 FromFloatQuantized(float value) => new((long)(value * Fixed32.OneRaw));

        public static readonly Hp64 Zero = new(0);

        public static Hp64 operator +(Hp64 a, Hp64 b) => new(a.Raw + b.Raw);
        public static Hp64 operator -(Hp64 a, Hp64 b) => new(a.Raw - b.Raw);
        public static Hp64 operator -(Hp64 a) => new(-a.Raw);

        // 허용된 유일한 곱: Wide × Fixed32(배수/퍼센트). Wide × Wide는 의도적으로 부재(Int128 없음).
        public static Hp64 operator *(Hp64 a, Fixed32 scale) => new(FixedMath.MulWideByFixed(a.Raw, scale.Raw));
        public static Hp64 operator *(Fixed32 scale, Hp64 a) => new(FixedMath.MulWideByFixed(a.Raw, scale.Raw));

        public static bool operator ==(Hp64 a, Hp64 b) => a.Raw == b.Raw;
        public static bool operator !=(Hp64 a, Hp64 b) => a.Raw != b.Raw;
        public static bool operator <(Hp64 a, Hp64 b) => a.Raw < b.Raw;
        public static bool operator >(Hp64 a, Hp64 b) => a.Raw > b.Raw;
        public static bool operator <=(Hp64 a, Hp64 b) => a.Raw <= b.Raw;
        public static bool operator >=(Hp64 a, Hp64 b) => a.Raw >= b.Raw;

        public static Hp64 Abs(Hp64 v) => new(v.Raw < 0 ? -v.Raw : v.Raw);
        public static Hp64 Min(Hp64 a, Hp64 b) => a.Raw <= b.Raw ? a : b;
        public static Hp64 Max(Hp64 a, Hp64 b) => a.Raw >= b.Raw ? a : b;
        public static Hp64 Clamp(Hp64 v, Hp64 lo, Hp64 hi)
            => v.Raw < lo.Raw ? lo : (v.Raw > hi.Raw ? hi : v);

        /// <summary>Egress 전용: read-model HP 표시값. 권위 분기 입력 금지.</summary>
        public float ToFloat() => Raw / (float)Fixed32.OneRaw;

        public bool Equals(Hp64 other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is Hp64 other && other.Raw == Raw;
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Hp64 other) => Raw.CompareTo(other.Raw);

        public override string ToString()
            => (Raw / (double)Fixed32.OneRaw).ToString("0.######", CultureInfo.InvariantCulture);
    }
}
