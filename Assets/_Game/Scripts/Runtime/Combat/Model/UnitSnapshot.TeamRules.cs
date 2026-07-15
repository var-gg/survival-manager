using System;
using SM.Core.Stats;

namespace SM.Combat.Model;

/// <summary>
/// UnitSnapshot의 상위 시너지 규칙 adapter. 기존 대형 aggregate 본문에 규칙별 보조 로직을 더하지 않고,
/// runtime stat/status truth는 같은 partial aggregate 안에서만 변이한다.
/// </summary>
public sealed partial class UnitSnapshot
{
    internal void AddStatModifier(StatModifier modifier)
    {
        Stats.AddModifier(modifier);
    }

    /// <summary>
    /// 전투 종료까지 유지되는 status/buff 채널. int.MaxValue tick sentinel은 timer 감소에서 제외되며,
    /// 상태 자체(Stacks/Magnitude)가 canonical hash에 들어가므로 영구 규칙 효과도 재현 상태에 남는다.
    /// </summary>
    internal void ApplyPermanentStatus(
        StatusApplicationSpec spec,
        string sourceActorId = "",
        string sourceSkillId = "",
        string sourceApplicationId = "")
    {
        ApplyStatus(spec, sourceActorId, sourceSkillId, sourceApplicationId);
        var statusIndex = _statuses.FindIndex(status => string.Equals(
            status.StatusId,
            spec.StatusId,
            StringComparison.Ordinal));
        if (statusIndex < 0)
        {
            return;
        }

        _statuses[statusIndex] = _statuses[statusIndex] with
        {
            RemainingTicks = int.MaxValue,
            DurationTicks = int.MaxValue,
        };
    }

    private int GetStatusStackCount(string statusId)
    {
        var maxStacks = 0;
        foreach (var status in _statuses)
        {
            if (string.Equals(status.StatusId, statusId, StringComparison.Ordinal))
            {
                maxStacks = Math.Max(maxStacks, status.Stacks);
            }
        }

        return maxStacks;
    }

    private float GetBloodrushMultiplier()
    {
        return 1f
            + (GetStatusStackCount(TeamRuleSet.BloodrushStatusId) * TeamRuleSet.BloodrushTempoPerStack);
    }
}
