using System;
using System.Globalization;

namespace SM.Core.Numerics
{
    /// <summary>
    /// positioning 스코어 누산·정렬·tie-break 전용 Q16.16 wide 값(Int64 backing, ADR-0029 / Phase 1).
    /// overlap²×12 + nav×420 류 합산이 Int32(±32768)를 넘으므로 누산기는 Int64여야 한다. 개별 항의 곱은
    /// <see cref="Fixed32"/>에서 끝낸 뒤 <see cref="FromFixed"/>로 widen해 누산한다 — 그래서 곱셈 연산자를
    /// 제공하지 않는다(누산 전용). 권위 분기·정렬에만 쓰고 표시 float로 오가지 않는다.
    /// </summary>
    public readonly struct Score64 : IEquatable<Score64>, IComparable<Score64>
    {
        public readonly long Raw; // Q16.16

        private Score64(long raw) => Raw = raw;

        public static Score64 FromRaw(long raw) => new(raw);
        public static Score64 FromInt(int value) => new((long)value << Fixed32.FractionBits);

        /// <summary>Fixed32 항을 Int64로 widen해 누산에 투입한다(곱은 Fixed32 단계에서 이미 종료).</summary>
        public static Score64 FromFixed(Fixed32 value) => new(value.Raw);

        public static readonly Score64 Zero = new(0);

        public static Score64 operator +(Score64 a, Score64 b) => new(a.Raw + b.Raw);
        public static Score64 operator -(Score64 a, Score64 b) => new(a.Raw - b.Raw);
        public static Score64 operator -(Score64 a) => new(-a.Raw);

        public static bool operator ==(Score64 a, Score64 b) => a.Raw == b.Raw;
        public static bool operator !=(Score64 a, Score64 b) => a.Raw != b.Raw;
        public static bool operator <(Score64 a, Score64 b) => a.Raw < b.Raw;
        public static bool operator >(Score64 a, Score64 b) => a.Raw > b.Raw;
        public static bool operator <=(Score64 a, Score64 b) => a.Raw <= b.Raw;
        public static bool operator >=(Score64 a, Score64 b) => a.Raw >= b.Raw;

        public static Score64 Min(Score64 a, Score64 b) => a.Raw <= b.Raw ? a : b;
        public static Score64 Max(Score64 a, Score64 b) => a.Raw >= b.Raw ? a : b;

        public bool Equals(Score64 other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is Score64 other && other.Raw == Raw;
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Score64 other) => Raw.CompareTo(other.Raw);

        public override string ToString()
            => (Raw / (double)Fixed32.OneRaw).ToString("0.######", CultureInfo.InvariantCulture);
    }
}
