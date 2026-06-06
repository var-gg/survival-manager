using System;
using System.Globalization;

namespace SM.Core.Numerics
{
    /// <summary>
    /// 결정적 각도 — binary angle measurement(BAM). full turn = 2^32(uint 전역)이라 덧셈/뺄셈이 uint
    /// 오버플로로 자연 wrap한다(모듈로 2π 무료, 누적 오차 없음). 상위 <see cref="SinLut.IndexBits"/> bit가
    /// <see cref="SinLut"/> 인덱스, 하위 bit가 보간 fraction이다(ADR-0029 §LUT/angle).
    ///
    /// <para>런타임에서 degrees/radians/π/MathF를 쓰지 않는다 — 저작 degree는 ingress에서 turn으로 bake한다
    /// (<see cref="FromDegreesQuantized"/>는 tool/ingress 전용, double은 양자화에만).</para>
    /// </summary>
    public readonly struct AngleTurn32 : IEquatable<AngleTurn32>
    {
        public readonly uint Raw; // full turn = 2^32

        private AngleTurn32(uint raw) => Raw = raw;

        public static AngleTurn32 FromRaw(uint raw) => new(raw);

        public static readonly AngleTurn32 Zero = new(0u);
        public static readonly AngleTurn32 QuarterTurn = new(0x40000000u);      // 2^30 = 90°
        public static readonly AngleTurn32 HalfTurn = new(0x80000000u);         // 2^31 = 180°
        public static readonly AngleTurn32 ThreeQuarterTurn = new(0xC0000000u); // 3·2^30 = 270°

        /// <summary>Ingress 전용(tool/저작): turn 분수 → BAM. sim 내부 호출 금지. double은 양자화에만 쓴다.</summary>
        public static AngleTurn32 FromTurnsQuantized(double turns)
        {
            var frac = turns - Math.Floor(turns);        // [0, 1)
            return new((uint)(frac * 4294967296.0));     // × 2^32
        }

        /// <summary>Ingress 전용: 저작 degree → BAM. sim 내부 호출 금지.</summary>
        public static AngleTurn32 FromDegreesQuantized(double degrees) => FromTurnsQuantized(degrees / 360.0);

        public static AngleTurn32 operator +(AngleTurn32 a, AngleTurn32 b) => new(unchecked(a.Raw + b.Raw));
        public static AngleTurn32 operator -(AngleTurn32 a, AngleTurn32 b) => new(unchecked(a.Raw - b.Raw));
        public static AngleTurn32 operator -(AngleTurn32 a) => new(unchecked(0u - a.Raw)); // 반사(reflection)

        public static bool operator ==(AngleTurn32 a, AngleTurn32 b) => a.Raw == b.Raw;
        public static bool operator !=(AngleTurn32 a, AngleTurn32 b) => a.Raw != b.Raw;

        public bool Equals(AngleTurn32 other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is AngleTurn32 other && other.Raw == Raw;
        public override int GetHashCode() => Raw.GetHashCode();

        public override string ToString()
            => (Raw / 4294967296.0 * 360.0).ToString("0.###", CultureInfo.InvariantCulture) + "°";
    }
}
