using System;
using System.Globalization;

namespace SM.Core.Numerics
{
    /// <summary>
    /// energy·resource 풀용 Q16.16 wide 타입(Int64 backing, ADR-0029 / Phase 1). 계약은 <see cref="Hp64"/>와
    /// 동일하나 단위가 달라 별 타입으로 둔다 — HP와 energy를 더하는 실수를 컴파일 단계에서 차단한다(units-of-measure).
    /// 허용 곱은 <c>Resource64 × Fixed32</c>뿐(<see cref="FixedMath.MulWideByFixed"/> 경유), <c>Wide × Wide</c>
    /// 금지. 포화는 <see cref="Clamp"/>로 명시 처리하고, <see cref="ToFloat"/>는 read-model egress 전용이다.
    /// </summary>
    public readonly struct Resource64 : IEquatable<Resource64>, IComparable<Resource64>
    {
        public readonly long Raw; // Q16.16

        private Resource64(long raw) => Raw = raw;

        public static Resource64 FromRaw(long raw) => new(raw);
        public static Resource64 FromInt(int value) => new((long)value << Fixed32.FractionBits);
        public static Resource64 FromFixed(Fixed32 value) => new(value.Raw);

        /// <summary>Ingress 전용(stat/저작 float → resource 규모 양자화, truncate-toward-zero). authoritative
        /// resource 분기 입력 금지 — Phase 4 stat/content 경계에서만 호출한다.</summary>
        public static Resource64 FromFloatQuantized(float value) => new((long)(value * Fixed32.OneRaw));

        public static readonly Resource64 Zero = new(0);

        public static Resource64 operator +(Resource64 a, Resource64 b) => new(a.Raw + b.Raw);
        public static Resource64 operator -(Resource64 a, Resource64 b) => new(a.Raw - b.Raw);
        public static Resource64 operator -(Resource64 a) => new(-a.Raw);

        // 허용된 유일한 곱: Wide × Fixed32(regen·decay 배수). Wide × Wide는 의도적으로 부재.
        public static Resource64 operator *(Resource64 a, Fixed32 scale) => new(FixedMath.MulWideByFixed(a.Raw, scale.Raw));
        public static Resource64 operator *(Fixed32 scale, Resource64 a) => new(FixedMath.MulWideByFixed(a.Raw, scale.Raw));

        public static bool operator ==(Resource64 a, Resource64 b) => a.Raw == b.Raw;
        public static bool operator !=(Resource64 a, Resource64 b) => a.Raw != b.Raw;
        public static bool operator <(Resource64 a, Resource64 b) => a.Raw < b.Raw;
        public static bool operator >(Resource64 a, Resource64 b) => a.Raw > b.Raw;
        public static bool operator <=(Resource64 a, Resource64 b) => a.Raw <= b.Raw;
        public static bool operator >=(Resource64 a, Resource64 b) => a.Raw >= b.Raw;

        public static Resource64 Abs(Resource64 v) => new(v.Raw < 0 ? -v.Raw : v.Raw);
        public static Resource64 Min(Resource64 a, Resource64 b) => a.Raw <= b.Raw ? a : b;
        public static Resource64 Max(Resource64 a, Resource64 b) => a.Raw >= b.Raw ? a : b;
        public static Resource64 Clamp(Resource64 v, Resource64 lo, Resource64 hi)
            => v.Raw < lo.Raw ? lo : (v.Raw > hi.Raw ? hi : v);

        /// <summary>Egress 전용: read-model 리소스 표시값. 권위 분기 입력 금지.</summary>
        public float ToFloat() => Raw / (float)Fixed32.OneRaw;

        public bool Equals(Resource64 other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is Resource64 other && other.Raw == Raw;
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Resource64 other) => Raw.CompareTo(other.Raw);

        public override string ToString()
            => (Raw / (double)Fixed32.OneRaw).ToString("0.######", CultureInfo.InvariantCulture);
    }
}
