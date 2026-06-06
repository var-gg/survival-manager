using System;
using System.Globalization;

namespace SM.Core.Numerics
{
    /// <summary>
    /// 결정론적 Q16.16 고정소수점 (ADR-0029 / Phase 1 numeric foundation). Int32 raw backing이며 mul/div는
    /// <c>long</c> 중간값으로 오버플로를 피한다. 반올림은 전 연산 <b>truncate-toward-zero</b>로 통일한다
    /// (mul은 <see cref="FixedMath.ShiftRightTrunc"/>, div는 C# 정수 나눗셈이 이미 0방향 절단).
    ///
    /// <para>기하·정규화 벡터·작은 배수·퍼센트·각 분수용이다. 규모값(HP/damage/energy)은 ±32768를 넘을 수
    /// 있어 <c>Hp64</c>/<c>Resource64</c>를, positioning 스코어 누산은 <c>Score64</c>를 쓴다. 범용
    /// <c>Wide×Wide</c>는 만들지 않는다(Int128 부재).</para>
    ///
    /// <para><see cref="FromFloatQuantized"/>/<see cref="ToFloat"/>는 ingress/egress 경계 전용이다 —
    /// authoritative sim 내부에서 float로 오가지 않는다(lint 대상).</para>
    /// </summary>
    public readonly struct Fixed32 : IEquatable<Fixed32>, IComparable<Fixed32>
    {
        public const int FractionBits = 16;
        public const int OneRaw = 1 << FractionBits; // 65536

        public readonly int Raw;

        private Fixed32(int raw) => Raw = raw;

        public static Fixed32 FromRaw(int raw) => new(raw);
        public static Fixed32 FromInt(int value) => new(value * OneRaw);

        public static readonly Fixed32 Zero = new(0);
        public static readonly Fixed32 One = new(OneRaw);

        /// <summary>Ingress 전용: 저작 float를 truncate-toward-zero로 양자화(C# float→int가 0방향 절단).
        /// sim 내부 호출 금지.</summary>
        public static Fixed32 FromFloatQuantized(float value) => new((int)(value * OneRaw));

        /// <summary>Egress 전용: 표시/연출용 float 변환. authoritative 분기 입력 금지.</summary>
        public float ToFloat() => Raw / (float)OneRaw;

        public static Fixed32 operator +(Fixed32 a, Fixed32 b) => new(a.Raw + b.Raw);
        public static Fixed32 operator -(Fixed32 a, Fixed32 b) => new(a.Raw - b.Raw);
        public static Fixed32 operator -(Fixed32 a) => new(-a.Raw);

        public static Fixed32 operator *(Fixed32 a, Fixed32 b)
            => new((int)FixedMath.ShiftRightTrunc((long)a.Raw * b.Raw, FractionBits));

        // C# 정수 나눗셈은 이미 truncate-toward-zero라 mul과 반올림 규칙이 일치한다. div0 → Zero(현 CombatVector2 동작 보존).
        public static Fixed32 operator /(Fixed32 a, Fixed32 b)
            => b.Raw == 0 ? Zero : new((int)(((long)a.Raw << FractionBits) / b.Raw));

        public static bool operator ==(Fixed32 a, Fixed32 b) => a.Raw == b.Raw;
        public static bool operator !=(Fixed32 a, Fixed32 b) => a.Raw != b.Raw;
        public static bool operator <(Fixed32 a, Fixed32 b) => a.Raw < b.Raw;
        public static bool operator >(Fixed32 a, Fixed32 b) => a.Raw > b.Raw;
        public static bool operator <=(Fixed32 a, Fixed32 b) => a.Raw <= b.Raw;
        public static bool operator >=(Fixed32 a, Fixed32 b) => a.Raw >= b.Raw;

        public static Fixed32 Abs(Fixed32 v) => new(v.Raw < 0 ? -v.Raw : v.Raw);
        public static Fixed32 Min(Fixed32 a, Fixed32 b) => a.Raw <= b.Raw ? a : b;
        public static Fixed32 Max(Fixed32 a, Fixed32 b) => a.Raw >= b.Raw ? a : b;
        public static int Sign(Fixed32 v) => v.Raw > 0 ? 1 : (v.Raw < 0 ? -1 : 0);
        public static Fixed32 Clamp(Fixed32 v, Fixed32 lo, Fixed32 hi)
            => v.Raw < lo.Raw ? lo : (v.Raw > hi.Raw ? hi : v);

        public bool Equals(Fixed32 other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is Fixed32 other && other.Raw == Raw;
        public override int GetHashCode() => Raw;
        public int CompareTo(Fixed32 other) => Raw.CompareTo(other.Raw);

        public override string ToString() => ToFloat().ToString("0.######", CultureInfo.InvariantCulture);
    }
}
