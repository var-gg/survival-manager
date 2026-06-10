using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Numerics;

namespace SM.Combat.Services;

/// <summary>
/// P0 positional consequence — "어디에 서 있는가"가 피해를 바꾸는 최소 규칙의 단일 진실.
/// (1) 전열 스크린: 살아있는 전열 아군의 가드 반경 안에 있는 후열은 받는 피해가 줄고, 일반 타게팅에서
///     덜 매력적인 표적이 된다(Dive 의도는 RoleBrain 경로로 이 보호를 의도적으로 우회한다 — 상성).
/// (2) 측면/후방: 피격자의 교전 시선(현재 표적 방향, 없으면 팀 전방) 대비 공격이 측면/후방에서 오면
///     피해가 늘어난다 — 협격·우회가 실익을 갖는 그림의 토대.
/// 판정은 전부 FixedVector2/Fixed32 결정 산술. 곱셈 체인 적용은 HitResolutionService가 소유한다.
/// </summary>
public static class BattleFormationConsequence
{
    /// <summary>스크린이 멀쩡한 후열이 받는 피해 경감 기본치.</summary>
    public const float ScreenMitigationBase = 0.12f;

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
    /// 후열 노출 술어 — 살아있는 전열 아군의 FrontlineGuardRadius 안에 있으면 보호(=비노출).
    /// 타게팅(BacklineExposedEnemy selector/filter)과 피해 경감이 같은 진실을 공유한다.
    /// </summary>
    public static bool IsBacklineExposed(BattleState state, UnitSnapshot target)
    {
        if (target.Behavior.FormationLine != FormationLine.Backline)
        {
            return false;
        }

        foreach (var ally in state.GetTeam(target.Side))
        {
            if (!ally.IsAlive || ally.Id == target.Id || ally.Behavior.FormationLine != FormationLine.Frontline)
            {
                continue;
            }

            var guardRadius = Fixed32.FromFloatQuantized(ally.Behavior.FrontlineGuardRadius);
            if (ally.FixedPosition.DistanceSquaredTo(target.FixedPosition) <= guardRadius * guardRadius)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>스크린이 멀쩡한 후열인가 — 노출 술어의 보호 측 읽기.</summary>
    public static bool IsScreenedBackline(BattleState state, UnitSnapshot target)
    {
        return target.Behavior.FormationLine == FormationLine.Backline && !IsBacklineExposed(state, target);
    }

    /// <summary>
    /// 스크린 피해 경감률(0이면 미적용). 수비측 전술의 ProtectCarryBias가 보호 두께를 키운다 —
    /// 같은 배치라도 전술 선택이 결과를 바꾸는 P1 레버 훅.
    /// </summary>
    public static float ResolveScreenMitigation(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        if (target.Side == actor.Side || !IsScreenedBackline(state, target))
        {
            return 0f;
        }

        var context = state.GetTacticContext(target.Side);
        return ScreenMitigationBase + (ScreenMitigationProtectCarryBonus * context.ProtectCarryBias);
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
