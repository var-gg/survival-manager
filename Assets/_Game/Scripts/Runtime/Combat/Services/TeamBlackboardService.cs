using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Combat.Services;

/// <summary>
/// Phase 2 팀 블랙보드 계산기 — <see cref="TeamBlackboard"/>를 갱신 시점 battle truth에서 결정적으로
/// 계산한다(RNG/시간 없음, tie-break은 stableId ordinal). 갱신 cadence와 보관은 <see cref="BattleState"/>가
/// 소유하고, 이 서비스는 순수 함수만 제공한다.
///
/// FocusMark는 마스터 플랜 점수표 + 히스테리시스(기존 마크를 +30 이상 이겨야 교체)로 표적 깜빡임 없이
/// 팀 화력이 한 곳을 본다. carry는 ProtectCarry posture의 보호 대상(최고 원거리/캐스터 화력, tie stableId).
/// breach는 "적 비-vanguard가 아군 vanguard 라인 뒤로 들어와 후열 3m 안" — vanguard Peel의 진짜 트리거.
/// 약측 lane은 CollapseWeakSide의 시프트 신호(1.0s = 2회 연속 갱신 안정 시 확정).
/// </summary>
public static class TeamBlackboardService
{
    /// <summary>0.5s @ 0.1s tick — gpt-pro 마스터 플랜의 team blackboard 갱신 주기.</summary>
    public const int CadenceSteps = 5;

    // === FocusMark 점수표 (마스터 플랜 기본값 — BALANCE는 V1 authority 튜닝 대상) ===
    private const int FocusMarkedStatusScore = 50;
    private const int FocusLowHpScore = 40;
    private const float FocusLowHpRatio = 0.35f;
    private const int FocusMysticScore = 35;
    private const int FocusRangerScore = 30;
    private const int FocusIsolatedScore = 25;
    private const float FocusIsolationDistance = 2.0f;
    private const int FocusProtectedPenalty = 50;
    private const float FocusProtectedRadius = 1.5f;
    private const int FocusUnreachablePenalty = 40;
    private const float FocusReachableSoonBuffer = 2.0f;
    private const int FocusReachableAllyQuorum = 3;
    private const int FocusSwitchHysteresis = 30;

    private const float CarryRangedThreshold = 1.8f; // melee 경계와 동일 — 원거리/캐스터만 carry 후보
    private const float BreachBacklineRadius = 3.0f;
    private const float WeakLaneBandY = 0.9f;         // |y| > 0.9 → top/bottom lane, 아니면 center

    public static TeamBlackboard Compute(BattleState state, TeamSide side, TeamBlackboard previous)
    {
        var carry = ResolveCarry(state, side);
        var (focusMarkId, focusMarkScore) = ResolveFocusMark(state, side, previous);
        var breachers = ResolveFrontlineBreachers(state, side, carry);
        var weakLane = ResolveWeakSideLane(state, side);
        var stableWeakLane = weakLane != 0 && weakLane == previous.WeakSideLane ? weakLane : 0;
        return new TeamBlackboard(
            side,
            state.StepIndex,
            focusMarkId,
            focusMarkScore,
            carry?.Id,
            breachers,
            weakLane,
            stableWeakLane);
    }

    private static (EntityId? MarkId, int Score) ResolveFocusMark(BattleState state, TeamSide side, TeamBlackboard previous)
    {
        var best = state.GetOpponents(side)
            .Where(enemy => enemy.IsAlive)
            .Select(enemy => new { Unit = enemy, Score = ScoreFocusCandidate(state, side, enemy) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Unit.HealthRatio)
            .ThenBy(x => x.Unit.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (best == null)
        {
            return (null, 0);
        }

        // 히스테리시스: 기존 마크가 살아있으면 현재 상태로 재점수하고, 새 후보가 +30 이상 이길 때만 교체
        // (마스터 플랜 "switch only if newScore >= oldScore + 30 OR old target dead/invalid").
        if (previous.FocusMarkId is { } previousId
            && state.FindUnit(previousId) is { IsAlive: true } previousMark
            && best.Unit.Id != previousId)
        {
            var previousScore = ScoreFocusCandidate(state, side, previousMark);
            if (best.Score < previousScore + FocusSwitchHysteresis)
            {
                return (previousId, previousScore);
            }
        }

        return (best.Unit.Id, best.Score);
    }

    private static int ScoreFocusCandidate(BattleState state, TeamSide side, UnitSnapshot target)
    {
        var score = 0;
        if (target.HasStatus("marked"))
        {
            score += FocusMarkedStatusScore;
        }

        if (target.HealthRatio <= FocusLowHpRatio)
        {
            score += FocusLowHpScore;
        }

        if (target.Definition.ClassId == "mystic")
        {
            score += FocusMysticScore;
        }
        else if (target.Definition.ClassId == "ranger")
        {
            score += FocusRangerScore;
        }

        // 호위 축: 표적의 아군 vanguard가 바짝 붙어 있으면 보호(-50), 멀리 떨어졌으면 고립(+25).
        var nearestOwnVanguard = state.GetTeam(target.Side)
            .Where(ally => ally.IsAlive && ally.Id != target.Id && ally.Definition.ClassId == "vanguard")
            .Select(ally => ally.Position.DistanceTo(target.Position))
            .DefaultIfEmpty(float.MaxValue)
            .Min();
        if (nearestOwnVanguard <= FocusProtectedRadius)
        {
            score -= FocusProtectedPenalty;
        }
        else if (nearestOwnVanguard > FocusIsolationDistance)
        {
            score += FocusIsolatedScore;
        }

        // 도달성: 우리 팀 3+명이 곧 닿지 못하는 표적은 집중 대상이 아니다("outside range of 3+ allies").
        var ownLiving = state.GetTeam(side).Where(ally => ally.IsAlive).ToList();
        if (ownLiving.Count >= FocusReachableAllyQuorum)
        {
            var reachableSoon = ownLiving.Count(ally =>
                MovementResolver.ComputeEdgeDistance(ally, target) <= ally.AttackRange + FocusReachableSoonBuffer);
            if (reachableSoon < FocusReachableAllyQuorum)
            {
                score -= FocusUnreachablePenalty;
            }
        }

        // (+20 combo primer 가중은 Phase 3 CombatBeat 도입 시 추가한다.)
        return score;
    }

    private static UnitSnapshot? ResolveCarry(BattleState state, TeamSide side)
    {
        // 마스터 플랜: "highest ranged/caster DPS score, tie stableId". DPS proxy = max(PhysPower, MagPower).
        return state.GetTeam(side)
            .Where(ally => ally.IsAlive && ally.AttackRange > CarryRangedThreshold)
            .OrderByDescending(ally => MathF.Max(ally.PhysPower, ally.MagPower))
            .ThenBy(ally => ally.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static IReadOnlyList<EntityId> ResolveFrontlineBreachers(BattleState state, TeamSide side, UnitSnapshot? carry)
    {
        var backliners = state.GetTeam(side)
            .Where(ally => ally.IsAlive
                           && (ally.Behavior.FormationLine == FormationLine.Backline
                               || (carry != null && ally.Id == carry.Id)))
            .ToList();
        if (backliners.Count == 0)
        {
            return Array.Empty<EntityId>();
        }

        // 아군 vanguard 라인 = 가장 전진한 생존 vanguard의 전진축 좌표. vanguard가 없으면 라인 부재 —
        // 후열 근접 위협만으로 breach로 판정한다(라인이 무너진 상태가 곧 상시 breach).
        var direction = side == TeamSide.Ally ? 1f : -1f;
        float? vanguardLineAdvance = null;
        foreach (var ally in state.GetTeam(side))
        {
            if (!ally.IsAlive || ally.Definition.ClassId != "vanguard")
            {
                continue;
            }

            var advance = ally.Position.X * direction;
            if (vanguardLineAdvance == null || advance > vanguardLineAdvance.Value)
            {
                vanguardLineAdvance = advance;
            }
        }

        List<EntityId>? breachers = null;
        foreach (var enemy in state.GetOpponents(side)
                     .Where(enemy => enemy.IsAlive && enemy.Definition.ClassId != "vanguard")
                     .OrderBy(enemy => enemy.Id.Value, StringComparer.Ordinal))
        {
            var behindLine = vanguardLineAdvance == null
                             || (enemy.Position.X * direction) < vanguardLineAdvance.Value;
            if (!behindLine)
            {
                continue;
            }

            if (backliners.Any(ally => ally.Position.DistanceTo(enemy.Position) <= BreachBacklineRadius))
            {
                (breachers ??= new List<EntityId>()).Add(enemy.Id);
            }
        }

        return breachers ?? (IReadOnlyList<EntityId>)Array.Empty<EntityId>();
    }

    private static int ResolveWeakSideLane(BattleState state, TeamSide side)
    {
        var topWeakness = ScoreLaneWeakness(state, side, 1);
        var bottomWeakness = ScoreLaneWeakness(state, side, -1);
        if (topWeakness > bottomWeakness)
        {
            return 1;
        }

        return bottomWeakness > topWeakness ? -1 : 0;
    }

    private static float ScoreLaneWeakness(BattleState state, TeamSide side, int lane)
    {
        var enemyCount = 0;
        var enemyMissingSum = 0f;
        var enemyVanguards = 0;
        foreach (var enemy in state.GetOpponents(side))
        {
            if (!enemy.IsAlive || LaneOf(enemy.Position.Y) != lane)
            {
                continue;
            }

            enemyCount++;
            enemyMissingSum += 1f - Math.Clamp(enemy.HealthRatio, 0f, 1f);
            if (enemy.Definition.ClassId == "vanguard")
            {
                enemyVanguards++;
            }
        }

        var ownCount = state.GetTeam(side).Count(ally => ally.IsAlive && LaneOf(ally.Position.Y) == lane);
        var enemyAvgMissing = enemyCount == 0 ? 0f : enemyMissingSum / enemyCount;

        // 마스터 플랜 sideScore의 약측 해석: 적이 적고, 물렁하고, 탱이 없고, 아군 국지 우위가 있는 lane이 약측.
        return (enemyAvgMissing * 30f)
               - (enemyCount * 20f)
               - (enemyVanguards * 25f)
               + ((ownCount - enemyCount) * 20f);
    }

    private static int LaneOf(float y) => y > WeakLaneBandY ? 1 : y < -WeakLaneBandY ? -1 : 0;
}
