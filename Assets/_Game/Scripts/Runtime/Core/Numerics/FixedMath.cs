namespace SM.Core.Numerics
{
    /// <summary>
    /// <see cref="Fixed32"/> 보조 수학 (ADR-0029 / Phase 1). 전부 순수 정수 — float/double/MathF를 쓰지
    /// 않아 cross-platform bit-exact가 성립한다. sin/cos turn-LUT는 후속 슬라이스에서 같은 namespace에 추가.
    /// </summary>
    public static class FixedMath
    {
        /// <summary>
        /// 오른쪽 시프트를 truncate-toward-zero로 수행한다. C# 산술 우시프트(<c>&gt;&gt;</c>)는 음수에서
        /// floor(−∞ 방향)라 정수 나눗셈(0방향 절단)과 어긋나므로, 모든 fixed mul이 이 helper를 공유해
        /// 부호 무관 단일 반올림 규칙을 보장한다. <c>ShiftRightTrunc(-x) == -ShiftRightTrunc(x)</c>.
        /// </summary>
        public static long ShiftRightTrunc(long value, int bits)
            => value >= 0 ? value >> bits : -((-value) >> bits);

        /// <summary>
        /// Wide(Q16.16/Int64) × <see cref="Fixed32"/>(Q16.16/Int32) → Q16.16/Int64, truncate-toward-zero.
        /// 중간곱은 <c>long × long</c>으로 끝나 Int128이 필요없다 — 범위 예산(Hp64 ceiling 1e6 → raw 6.6e10,
        /// mult ≤ 4 → raw 2.6e5, 곱 ≈ 1.7e16 &lt; Int64 9.2e18, ~536× 여유)이 무오버플로를 보장한다.
        /// <c>Wide × Wide</c>(≈4.3e21 &gt; Int64)는 의도적으로 제공하지 않는다.
        /// <see cref="Hp64"/>/<see cref="Resource64"/>의 유일한 곱 경로다(ADR-0029 §Range budget).
        /// </summary>
        public static long MulWideByFixed(long wideRaw, int fixedRaw)
            => ShiftRightTrunc(wideRaw * fixedRaw, Fixed32.FractionBits);

        /// <summary>
        /// 정수 floor 제곱근(순수 비트 방식 — float 미사용, 모든 플랫폼에서 동일 결과). 음수/0 → 0.
        /// </summary>
        public static long ISqrt(long n)
        {
            if (n <= 0)
            {
                return 0;
            }

            long bit = 1L << 62;
            while (bit > n)
            {
                bit >>= 2;
            }

            long result = 0;
            while (bit != 0)
            {
                if (n >= result + bit)
                {
                    n -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }

                bit >>= 2;
            }

            return result;
        }

        /// <summary>Q16.16 제곱근. 음수 입력 → 0. <c>result_raw = isqrt(raw &lt;&lt; 16)</c>(floor).</summary>
        public static Fixed32 Sqrt(Fixed32 value)
            => value.Raw <= 0
                ? Fixed32.Zero
                : Fixed32.FromRaw((int)ISqrt((long)value.Raw << Fixed32.FractionBits));

        /// <summary>비음 정수 거듭제곱(예: AOE 감쇠 0.75^index). exp &lt;= 0 → One.</summary>
        public static Fixed32 PowInt(Fixed32 baseValue, int exp)
        {
            if (exp <= 0)
            {
                return Fixed32.One;
            }

            var result = Fixed32.One;
            for (var i = 0; i < exp; i++)
            {
                result *= baseValue;
            }

            return result;
        }

        /// <summary>
        /// 결정적 sin: <see cref="AngleTurn32"/>(BAM) → Fixed32. <see cref="SinLut"/> 테이블 상위
        /// <see cref="SinLut.IndexBits"/> bit 인덱스 + 하위 fraction 선형보간(truncate-toward-zero,
        /// <see cref="ShiftRightTrunc"/>). float/MathF 미사용 → cross-platform bit-exact. <c>SinTurns(-x)</c>는
        /// <c>-SinTurns(x)</c>와 노드에서 정확, 비노드에서 ±1 ULP 일치한다(보간 절단의 양자화 artifact — 함수는
        /// 순수 정수라 결정성엔 무관).
        /// </summary>
        public static Fixed32 SinTurns(AngleTurn32 angle)
        {
            var t = angle.Raw;
            var index = (int)(t >> SinLut.IndexShift);                  // 상위 IndexBits: 0..EntryCount-1
            long frac = t & SinLut.InterpFractionMask;                  // 하위 IndexShift bit
            var a = SinLut.Values[index];
            var b = SinLut.Values[(index + 1) & (SinLut.EntryCount - 1)];
            var interp = ShiftRightTrunc((long)(b - a) * frac, SinLut.IndexShift);
            return Fixed32.FromRaw((int)(a + interp));
        }

        /// <summary>결정적 cos = sin(angle + ¼turn). <see cref="SinTurns"/>와 동일 테이블이라 sin²+cos²≈1이 타이트하다.</summary>
        public static Fixed32 CosTurns(AngleTurn32 angle) => SinTurns(angle + AngleTurn32.QuarterTurn);
    }
}
