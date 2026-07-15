using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Numerics;

namespace SM.Combat.Services;

/// <summary>
/// P0 positional consequence — "어디에 서 있는가"가 피해를 바꾸는 최소 규칙의 단일 진실.
/// (1) 전열 스크린: 후열은 (a)살아있는 전열 아군의 가드 반경 안에 있거나 (b)전열 아군이 "공격자→후열"
///     사선의 사이 구간(interpose)에 서 있으면 보호된다 — 받는 피해가 줄고, 일반 타게팅에서 덜 매력적인
///     표적이 된다(Dive 의도는 RoleBrain 경로로 이 보호를 의도적으로 우회한다 — 상성).
///     interpose 축은 전열이 교전하러 전진해도 "적이 그 가드를 지나쳐야 후열에 닿는" 위치 관계면 스크린을
///     유지시킨다 — 근접 반경만으론 개전 수 tick 만에 스크린이 영구 해체되던 동적 결손(2026-06-15 eb2e4557
///     정찰: 전 posture 스크린 발동 0)의 수술. 측면으로 도는 공격 축엔 가드가 사이에 없어 자연히 안 선다.
/// (2) 측면/후방: 피격자의 교전 시선(현재 표적 방향, 없으면 팀 전방) 대비 공격이 측면/후방에서 오면
///     피해가 늘어난다 — 협격·우회가 실익을 갖는 그림의 토대.
/// 판정은 전부 FixedVector2/Fixed32 결정 산술. 곱셈 체인 적용은 HitResolutionService가 소유한다.
/// </summary>
public static class BattleFormationConsequence
{
    /// <summary>스크린이 멀쩡한 후열이 받는 피해 경감 기본치.</summary>
    public const float ScreenMitigationBase = 0.12f;

    /// <summary>방진(rule.phalanx)이 수비측 스크린 경감에 더하는 팀-조건부 가산치.</summary>
    public const float PhalanxScreenMitigationBonus = 0.04f;

    /// <summary>interpose 스크린 폭(m) — 전열 가드가 "공격자→후열" 사선에서 이 수선거리 안이면 몸으로 막는다.
    /// 같은 레인 정면 사선(수선 ~0.5m)은 잡고, 완전 옆 레인 대각 사선(~1.26m)은 안 잡는 값.</summary>
    public const float InterposeScreenWidth = 1.1f;

    // InterposeScreenWidth² (Q16.16) — 수선거리 비교는 제곱으로(불필요한 sqrt 회피).
    private static readonly Fixed32 InterposeWidthSq = Fixed32.FromFloatQuantized(InterposeScreenWidth * InterposeScreenWidth);

    /// <summary>수비측 ProtectCarryBias(0..1)가 스크린 경감에 더하는 최대 보너스.</summary>
    public const float ScreenMitigationProtectCarryBonus = 0.08f;

    /// <summary>스크린이 멀쩡한 후열 표적의 타게팅 점수 페널티(거리 미터 등가). Dive 경로에는 미적용.</summary>
    public const float ScreenedTargetScorePenalty = 0.35f;

    /// <summary>측면(시선 대비 ~70° 초과) 공격 피해 보너스.</summary>
    public const float SideFlankDamageBonus = 0.06f;

    /// <summary>후방(시선 대비 ~110° 초과) 공격 피해 보너스.</summary>
    public const float RearFlankDamageBonus = 0.12f;

    // cos(70°)≈0.34 / cos(110°)≈-0.34 — 정규화 시선·공격 방향 dot 비교 임계 (Q16.16).
    private static readonly Fixed32 SideArcDotThreshold = Fixed32.FromRaw(22282);   // ≈ +0.34
    private static readonly Fixed32 RearArcDotThreshold = Fixed32.FromRaw(-22282);  // ≈ -0.34

    /// <summary>측면 판정 결과. 피해 보너스(0이면 정면)와 로그 note 토큰.</summary>
    public readonly record struct FlankArcResult(float DamageBonus, string NoteToken)
    {
        public static readonly FlankArcResult Frontal = new(0f, string.Empty);
        public bool IsFlanking => DamageBonus > 0f;
    }

    /// <summary>
    /// 후열 노출 술어(공격자-무관) — 근접 가드 성분만 본다(살아있는 전열 아군의 FrontlineGuardRadius 안이면
    /// 보호). 공격 축이 없는 팀 지각(AiPerceptionBlackboard)용 보수적 근사. 공격자가 특정되는 소비처는
    /// interpose까지 보는 <see cref="IsBacklineExposedFrom"/>을 쓴다.
    /// </summary>
    public static bool IsBacklineExposed(BattleState state, UnitSnapshot target)
    {
        return target.Behavior.FormationLine == FormationLine.Backline
            && ResolveScreeningGuard(state, target, attacker: null) == null;
    }

    /// <summary>
    /// 후열 노출 술어(공격자-상대적) — 근접 가드 OR interpose(가드가 공격자→표적 사선의 사이 대역).
    /// 타게팅(BacklineExposedEnemy selector/filter)과 피해 경감이 같은 진실을 공유한다.
    /// </summary>
    public static bool IsBacklineExposedFrom(BattleState state, UnitSnapshot attacker, UnitSnapshot target)
    {
        return target.Behavior.FormationLine == FormationLine.Backline
            && ResolveScreeningGuard(state, target, attacker) == null;
    }

    /// <summary>스크린이 멀쩡한 후열인가(공격자-무관, 근접 가드 성분만) — 노출 술어의 보호 측 읽기.</summary>
    public static bool IsScreenedBackline(BattleState state, UnitSnapshot target)
    {
        return target.Behavior.FormationLine == FormationLine.Backline
            && ResolveScreeningGuard(state, target, attacker: null) != null;
    }

    /// <summary>스크린이 멀쩡한 후열인가(공격자-상대적) — 이 공격자의 축에 대해 근접 가드 OR interpose.</summary>
    public static bool IsScreenedBacklineFrom(BattleState state, UnitSnapshot attacker, UnitSnapshot target)
    {
        return target.Behavior.FormationLine == FormationLine.Backline
            && ResolveScreeningGuard(state, target, attacker) != null;
    }

    /// <summary>
    /// 스크린 피해 경감률(0이면 미적용). 수비측 전술의 ProtectCarryBias가 보호 두께를 키운다 —
    /// 같은 배치라도 전술 선택이 결과를 바꾸는 P1 레버 훅.
    /// </summary>
    public static float ResolveScreenMitigation(BattleState state, UnitSnapshot actor, UnitSnapshot target)
        => ResolveScreenMitigation(state, actor, target, out _);

    /// <summary>
    /// 피해 경감과 그 경감을 만든 스크리너를 한 번의 판정으로 돌려준다. 스크리너 귀속은 팀 unit 순서의
    /// 첫 유효 가드로 고정되며, HitResolutionService가 이 결과를 상태 방출 seam으로 전달한다.
    /// </summary>
    internal static float ResolveScreenMitigation(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        out UnitSnapshot? screeningUnit)
    {
        screeningUnit = target.Side == actor.Side || target.Behavior.FormationLine != FormationLine.Backline
            ? null
            : ResolveScreeningGuard(state, target, actor);
        if (screeningUnit == null)
        {
            return 0f;
        }

        var context = state.GetTacticContext(target.Side);
        var phalanxBonus = state.TeamRuleSet.Has(target.Side, TeamRuleSet.PhalanxRuleId)
            ? PhalanxScreenMitigationBonus
            : 0f;
        return ScreenMitigationBase
            + (ScreenMitigationProtectCarryBonus * context.ProtectCarryBias)
            + phalanxBonus;
    }

    // 스크린 가드 탐색 — 두 성분의 OR:
    // (1) 근접 가드(종전 규칙): 전열 아군이 후열의 guardRadius 안. 공격 축 불요.
    // (2) interpose(공격자-상대적): 전열 아군 G가 "표적 T→공격자 A" 축에서 사이 구간(along ∈ (0, |A-T|))에
    //     있고 수선거리² ≤ InterposeWidthSq. 전진 교전 중인 전열도 사선을 막고 있으면 스크린이 유지된다.
    // 산술 노트: |along| ≤ 아레나 대각(~17m), along²·거리² ≤ ~297 — Q16.16 범위 내(FixedVector2 스케일 계약).
    private static UnitSnapshot? ResolveScreeningGuard(BattleState state, UnitSnapshot target, UnitSnapshot? attacker)
    {
        var hasAxis = false;
        var axisDir = default(FixedVector2);
        var axisLength = Fixed32.Zero;
        if (attacker != null)
        {
            var toAttacker = attacker.FixedPosition - target.FixedPosition;
            var lenSq = toAttacker.LengthSquared;
            if (lenSq.Raw > FixedVector2.DefaultNormalizeEpsSq.Raw)
            {
                axisLength = FixedMath.Sqrt(lenSq);
                axisDir = toAttacker.NormalizeOrFallback(ResolveTeamForward(target.Side));
                hasAxis = true;
            }
        }

        foreach (var ally in state.GetTeam(target.Side))
        {
            if (!ally.IsAlive || ally.Id == target.Id || ally.Behavior.FormationLine != FormationLine.Frontline)
            {
                continue;
            }

            var toGuard = ally.FixedPosition - target.FixedPosition;

            var guardRadius = Fixed32.FromFloatQuantized(ally.Behavior.FrontlineGuardRadius);
            if (toGuard.LengthSquared <= guardRadius * guardRadius)
            {
                return ally;
            }

            if (hasAxis)
            {
                var along = toGuard.Dot(axisDir);
                if (along.Raw > 0 && along.Raw < axisLength.Raw)
                {
                    // perpSq = |G-T|² - along² — 양자화 오차로 미세 음수 가능(가드가 정확히 사선 위) → 성립 방향으로 무해.
                    var perpSq = toGuard.LengthSquared - (along * along);
                    if (perpSq.Raw <= InterposeWidthSq.Raw)
                    {
                        return ally;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 측면/후방 판정. 피격자 시선 = 현재 표적 방향(없으면 팀 전방). 시선과 "피격자→공격자" 방향의
    /// dot이 측면/후방 임계 아래면 보너스. 자기 표적에게 맞으면 dot=+1(정면)이라 보너스 없음.
    /// </summary>
    public static FlankArcResult ResolveFlankArc(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        if (target.Side == actor.Side)
        {
            return FlankArcResult.Frontal;
        }

        var teamForward = ResolveTeamForward(target.Side);
        var facing = ResolveDefenderFacing(state, target, teamForward);
        var toAttacker = (actor.FixedPosition - target.FixedPosition).NormalizeOrFallback(facing);
        var dot = facing.Dot(toAttacker);
        if (dot <= RearArcDotThreshold)
        {
            return new FlankArcResult(RearFlankDamageBonus, "rear");
        }

        return dot <= SideArcDotThreshold
            ? new FlankArcResult(SideFlankDamageBonus, "flank")
            : FlankArcResult.Frontal;
    }

    private static FixedVector2 ResolveDefenderFacing(BattleState state, UnitSnapshot target, FixedVector2 teamForward)
    {
        var engaged = state.FindUnit(target.CurrentTargetId);
        if (engaged is { IsAlive: true })
        {
            return (engaged.FixedPosition - target.FixedPosition).NormalizeOrFallback(teamForward);
        }

        return teamForward;
    }

    private static FixedVector2 ResolveTeamForward(TeamSide side)
    {
        // BattlefieldLayout: 아군 음수 X에서 +X로 전진, 적군은 반대.
        return side == TeamSide.Ally
            ? new FixedVector2(Fixed32.One, Fixed32.Zero)
            : new FixedVector2(-Fixed32.One, Fixed32.Zero);
    }
}
