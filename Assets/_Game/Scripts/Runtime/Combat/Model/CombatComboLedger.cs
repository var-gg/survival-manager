using System;
using System.Collections.Generic;
using SM.Core.Ids;

namespace SM.Combat.Model;

/// <summary>
/// 활성 프라이머 윈도우 하나 — <c>SourceId</c> 팀이 <c>TargetId</c> 에 세팅 디버프(<c>StatusId</c>)를
/// 건 시점부터 <c>ExpiresStep</c>(미포함) 전까지 후속 타격이 콤보를 소비할 수 있다.
/// </summary>
public sealed record ComboPrimerWindow(
    int ChainId,
    EntityId SourceId,
    TeamSide SourceSide,
    EntityId TargetId,
    string StatusId,
    int AppliedStep,
    int ExpiresStep);

/// <summary>
/// 콤보 연쇄의 전투 단위 상태(활성 프라이머 / 연쇄 ICD / ChainId 할당). seed 재시뮬로 재구성되므로
/// 직렬화하지 않는다. 프라이머 목록은 적용 순서 List 라 조회·소비 순서가 결정론적이고, ICD 사전은
/// 키 조회만 한다(열거 없음 — 순서 비결정성 무관). 규칙(윈도우 폭·레지스트리·beat 발행)은
/// <see cref="Services.CombatComboService"/> 소관이다.
/// </summary>
public sealed class CombatComboLedger
{
    private readonly List<ComboPrimerWindow> _primers = new();
    private readonly Dictionary<string, int> _chainLastConsumedStep = new(StringComparer.Ordinal);
    private int _nextChainId = 1;

    public int AllocateChainId() => _nextChainId++;

    /// <summary>해당 표적·status 의 활성 프라이머(있으면). 재적용 시 기존 연쇄 유지 판정에 쓴다.</summary>
    public ComboPrimerWindow? FindActivePrimer(EntityId targetId, string statusId, int step)
    {
        foreach (var primer in _primers)
        {
            if (primer.TargetId == targetId
                && string.Equals(primer.StatusId, statusId, StringComparison.Ordinal)
                && primer.ExpiresStep > step)
            {
                return primer;
            }
        }

        return null;
    }

    /// <summary>
    /// 소비 후보 — 해당 표적에 걸린, 소비자 팀이 연 프라이머 중 가장 오래된 것(적용 순서 우선).
    /// 한 타격은 프라이머 하나만 소비한다(beat 스팸 방지).
    /// </summary>
    public ComboPrimerWindow? FindOldestActivePrimer(EntityId targetId, TeamSide consumerSide, int step)
    {
        foreach (var primer in _primers)
        {
            if (primer.TargetId == targetId
                && primer.SourceSide == consumerSide
                && primer.ExpiresStep > step)
            {
                return primer;
            }
        }

        return null;
    }

    public void AddPrimer(ComboPrimerWindow primer) => _primers.Add(primer);

    public void RemovePrimer(ComboPrimerWindow primer) => _primers.Remove(primer);

    public void PruneExpired(int step) => _primers.RemoveAll(primer => primer.ExpiresStep <= step);

    public bool IsChainOnCooldown(string chainKey, int step, int icdTicks)
    {
        return _chainLastConsumedStep.TryGetValue(chainKey, out var consumedStep)
               && step - consumedStep < icdTicks;
    }

    public void MarkChainConsumed(string chainKey, int step) => _chainLastConsumedStep[chainKey] = step;
}
