using System.Collections.Generic;
using SM.Core.Ids;

namespace SM.Combat.Model;

/// <summary>
/// Phase 2 팀 블랙보드 — 0.5s(5 step)마다 갱신되는 팀 단위 전술 진실(FocusMark / carry / frontline breach /
/// 약측 lane). Phase 1이 per-tick proxy로 위조하던 개념들의 영속 소유자다(gpt-pro 마스터 플랜: "FocusMark /
/// carry / breach를 proxy로 위조 금지 — Phase 2에서 진짜로 교체"). 모든 값은 갱신 시점 battle truth의 순수
/// 함수라 직렬화가 필요 없고(replay = seed 재시뮬), 동seed 재실행에서 동일하게 재구성된다. 갱신은
/// <see cref="BattleState"/>가 소유한다 — static mutable 상태 없음.
/// </summary>
public sealed record TeamBlackboard(
    TeamSide Side,
    int ComputedAtStep,
    EntityId? FocusMarkId,
    int FocusMarkScore,
    EntityId? CarryId,
    IReadOnlyList<EntityId> FrontlineBreachers,
    int WeakSideLane,
    int StableWeakSideLane)
{
    public static TeamBlackboard CreateEmpty(TeamSide side)
        => new(side, int.MinValue, null, 0, null, System.Array.Empty<EntityId>(), 0, 0);

    public bool HasBeenComputed => ComputedAtStep != int.MinValue;

    public bool IsFrontlineBreached => FrontlineBreachers.Count > 0;

    public bool IsBreacher(EntityId id)
    {
        for (var i = 0; i < FrontlineBreachers.Count; i++)
        {
            if (FrontlineBreachers[i] == id)
            {
                return true;
            }
        }

        return false;
    }
}
