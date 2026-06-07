using SM.Core.Numerics;

namespace SM.Combat.Model;

/// <summary>
/// CombatVector2(float) ↔ FixedVector2(Q16.16) 경계 변환 (ADR-0029 / Phase 3.2). sim의 공간 권위는
/// <see cref="FixedVector2"/>이고, <c>CombatVector2</c>는 ingress(저작 좌표)·egress(read-model/presentation/hash)
/// projection이다. implicit 변환을 두지 않고 아래 두 명시 함수로만 경계를 건넌다 — <see cref="FixedVector2.FromFloatQuantized"/>
/// 호출(ingress)을 이 한 곳에 모아 경계 누수를 리뷰에서 감사 가능하게 한다.
/// </summary>
public static class SpatialProjection
{
    /// <summary>Ingress: 저작/외부 float 좌표 → 고정소수 권위(단 한 번 양자화).</summary>
    public static FixedVector2 QuantizeToFixed(CombatVector2 v) => FixedVector2.FromFloatQuantized(v.X, v.Y);

    /// <summary>Egress: 고정소수 권위 → read-model/표시용 float projection.</summary>
    public static CombatVector2 ToReadModelVector2(FixedVector2 v) => new(v.X.ToFloat(), v.Y.ToFloat());
}
