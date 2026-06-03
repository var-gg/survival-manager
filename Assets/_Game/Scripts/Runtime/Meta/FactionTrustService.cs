using System.Collections.Generic;

namespace SM.Meta;

/// <summary>warrant 정치 결과가 한 세력 trust를 얼마나 바꾸는가. (FactionId, Delta).</summary>
public readonly record struct FactionTrustDelta(string FactionId, int Delta);

/// <summary>
/// warrant 이행/위반(<see cref="WarrantOutcome"/>)을 세력 trust delta로 번역하는 순수 함수.
/// ludonarrative 루프 정치적 전환(ADR-0028): warrant는 "어느 세력 기준을 수락하고 누구를 거스르나"이고,
/// 그 결과가 세력 평판을 바꾼다. delta 계산은 SM.Meta 순수 — 적용(SaveProfile.FactionStanding)은 SM.Unity settlement.
/// satisfied(Kept) → issuer +, opposed −. failed(Broken/FailedMission) → issuer −. (betrayed/public·deniable는 후속.)
/// </summary>
public static class FactionTrustService
{
    public const int SatisfiedIssuerGain = 2;
    public const int SatisfiedOpposedLoss = 1;
    public const int FailedIssuerLoss = 2;

    /// <param name="outcome">warrant 판정 결과(fact-bag judge 산출, ADR-0027).</param>
    /// <param name="issuerFactionId">warrant를 발행한 세력. 비면 정치 결과 없음(빈 리스트).</param>
    /// <param name="opposedFactionId">이 warrant로 거스른 세력. 비면 opposed delta 없음.</param>
    public static IReadOnlyList<FactionTrustDelta> ComputeDeltas(
        WarrantOutcome outcome, string issuerFactionId, string opposedFactionId)
    {
        var deltas = new List<FactionTrustDelta>();
        if (string.IsNullOrWhiteSpace(issuerFactionId))
        {
            return deltas; // 정치 issuer 없는 warrant(또는 미서약) — 평판 변화 없음.
        }

        switch (outcome)
        {
            case WarrantOutcome.Kept: // satisfied
                deltas.Add(new FactionTrustDelta(issuerFactionId, SatisfiedIssuerGain));
                if (!string.IsNullOrWhiteSpace(opposedFactionId))
                {
                    deltas.Add(new FactionTrustDelta(opposedFactionId, -SatisfiedOpposedLoss));
                }
                break;

            case WarrantOutcome.Broken:
            case WarrantOutcome.FailedMission: // failed
                deltas.Add(new FactionTrustDelta(issuerFactionId, -FailedIssuerLoss));
                break;

            // NotApplicable: 정치 warrant 아님 — 변화 없음.
        }

        return deltas;
    }
}
