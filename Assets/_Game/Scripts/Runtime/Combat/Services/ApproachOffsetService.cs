using System;
using SM.Combat.Model;

namespace SM.Combat.Services;

/// <summary>
/// Phase 2 결정적 접근 offset — engagement slot lease의 대체물(마스터 플랜 "spread around target without
/// slots"). 같은 타겟을 노리는 근접 공격자들을 (rolePriority, stableId)로 정렬해 index별 고정 각도
/// (정면/±45°/±80°, 5번째 이후 stableId 해시 측면)로 흩는다. lease·게이트·overflow ring이 없다:
/// 이 점은 "서고 싶은 자리" 제안일 뿐 공격 적법성과 무관하고(사거리 규칙만 유효), 막히면 호출부가
/// 기존 직선 추격으로 폴백한다. 집합은 평가 시점 truth에서 매번 재계산되므로 이동 타겟을 상태 없이
/// 추종한다 — 직렬화 불필요, 동seed 재현. cos/sin은 리터럴 상수라 플랫폼 삼각함수 분기가 없다.
/// </summary>
public static class ApproachOffsetService
{
    // 구 EngagementSlotService.RequiresSlotting과 동일한 근접 교전 술어.
    private const float MaxMeleeCombatReach = 1.6f;
    private const float MaxMeleeRangeThreshold = 1.8f;
    private const float MeleeRangeBuffer = 0.25f;

    /// <summary>정지점의 edge 간격(m) — 마스터 플랜 radius = targetR + attackerR + 0.15.</summary>
    private const float ApproachEdgeGap = 0.15f;
    private const float ObstructionPadding = 0.03f;
    private const float ArenaHalfWidth = 8f;
    private const float ArenaHalfHeight = 3.2f;

    // index → 고정 각도 팬(타겟→공격자 진영 축 기준): 정면, +45°, -45°, +80°, -80°.
    private static readonly (float Cos, float Sin)[] OffsetFan =
    {
        (1f, 0f),
        (0.7071068f, 0.7071068f),
        (0.7071068f, -0.7071068f),
        (0.1736482f, 0.9848078f),
        (0.1736482f, -0.9848078f),
    };

    // 6번째 이후 공격자: ±110° 측면(부호는 stableId 해시) — "nearest open side by stableId hash".
    private const float OverflowCos = -0.3420201f;
    private const float OverflowSin = 0.9396926f;

    public static bool IsMeleeEngagement(UnitSnapshot actor, FloatRange rangeBand)
    {
        return actor.CombatReach <= MaxMeleeCombatReach
               && rangeBand.ClampedMax <= Math.Max(MaxMeleeRangeThreshold, actor.CombatReach + MeleeRangeBuffer);
    }

    /// <summary>
    /// 접근 산개 의도 — offset 각도에서 유도한다(구 슬롯 arc roll 대체: step 의존 roll이 없어져 의도가
    /// 깜빡이지 않는다). Dive 의도는 각도와 무관하게 BacklineDive로 읽힌다.
    /// </summary>
    public static PositioningIntentKind ResolvePositioningIntent(BattleState state, UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand)
    {
        if (target.Side == actor.Side || !IsMeleeEngagement(actor, rangeBand))
        {
            return PositioningIntentKind.MaintainRange;
        }

        if (actor.CurrentCombatIntent.Type == CombatIntentType.Dive)
        {
            return PositioningIntentKind.BacklineDive;
        }

        // 라벨은 월드 Y 부호 기준(구 슬롯 arc 컨벤션: 양 팀 모두 월드 +Y = FlankLeft).
        var fan = ResolveFan(state, actor, target);
        var worldSin = (actor.Side == TeamSide.Ally ? -1f : 1f) * fan.Sin;
        if (worldSin > 0.01f)
        {
            return PositioningIntentKind.FlankLeft;
        }

        return worldSin < -0.01f ? PositioningIntentKind.FlankRight : PositioningIntentKind.Frontline;
    }

    /// <summary>
    /// 근접 공격자의 "서고 싶은 자리". 막혀 있으면 null — 호출부는 기존 직선 추격을 쓴다(마스터 플랜
    /// "if obstructed: ignore offset and use normal pursuit").
    /// </summary>
    public static CombatVector2? TryResolveDesiredApproachPoint(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        var fan = ResolveFan(state, actor, target);

        // base 축 = 타겟에서 공격자 진영 쪽. rotate((bx, 0), θ) = (bx·cosθ, bx·sinθ).
        var baseX = actor.Side == TeamSide.Ally ? -1f : 1f;
        var direction = new CombatVector2(baseX * fan.Cos, baseX * fan.Sin);
        var radius = target.NavigationRadius + actor.NavigationRadius + ApproachEdgeGap;
        var raw = target.Position + (direction * radius);
        var point = new CombatVector2(
            Math.Clamp(raw.X, -ArenaHalfWidth, ArenaHalfWidth),
            Math.Clamp(raw.Y, -ArenaHalfHeight, ArenaHalfHeight));
        return IsObstructed(state, actor, target, point) ? null : point;
    }

    private static (float Cos, float Sin) ResolveFan(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        var index = ResolveAttackerIndex(state, actor, target);
        if (index < OffsetFan.Length)
        {
            return OffsetFan[index];
        }

        return (OverflowCos, StableHashSign(actor.Id.Value) * OverflowSin);
    }

    // 같은 타겟의 근접 공격자 집합에서 (rolePriority, stableId) 정렬상 actor 앞에 서는 수 = index.
    // 정면(0)은 전열 본대(vanguard)가, 측면은 후순위 공격자가 가져간다. 집합은 타겟 전환 때만 바뀐다
    // (sticky target) — lease 없이도 목적지가 매 틱 진동하지 않는 이유.
    private static int ResolveAttackerIndex(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        var actorPriority = RolePriority(actor);
        var index = 0;
        foreach (var peer in state.GetTeam(actor.Side))
        {
            if (!peer.IsAlive || peer.Id == actor.Id || peer.AttackRange > MaxMeleeRangeThreshold)
            {
                continue;
            }

            if (peer.CurrentTargetId != target.Id && peer.PendingTargetId != target.Id)
            {
                continue;
            }

            var priorityDelta = RolePriority(peer) - actorPriority;
            if (priorityDelta < 0
                || (priorityDelta == 0 && string.CompareOrdinal(peer.Id.Value, actor.Id.Value) < 0))
            {
                index++;
            }
        }

        return index;
    }

    private static int RolePriority(UnitSnapshot unit) => unit.Definition.ClassId switch
    {
        "vanguard" => 0,
        "duelist" => 1,
        _ => 2,
    };

    private static bool IsObstructed(BattleState state, UnitSnapshot actor, UnitSnapshot target, CombatVector2 point)
    {
        foreach (var obstacle in state.AllUnits)
        {
            if (!obstacle.IsAlive || obstacle.Id == actor.Id || obstacle.Id == target.Id)
            {
                continue;
            }

            var clearance = actor.NavigationRadius + obstacle.NavigationRadius + ObstructionPadding;
            if (point.DistanceTo(obstacle.Position) < clearance)
            {
                return true;
            }
        }

        return false;
    }

    private static int StableHashSign(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return (hash & 1) == 0 ? 1 : -1;
        }
    }
}
