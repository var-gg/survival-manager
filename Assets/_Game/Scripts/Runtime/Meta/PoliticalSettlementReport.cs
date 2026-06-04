using System;
using System.Collections.Generic;

namespace SM.Meta;

/// <summary>
/// 한 출격 정치 정산에서 한 세력 신뢰가 *왜* 움직였나(사유 분류). 표시 문구가 아니라 stable 분류 토큰 —
/// label layer(SM.Unity)가 한국어로 매핑한다(ID/label 분리, <see cref="PoliticalCombatCondition"/>.ReasonCode와 동렬).
/// </summary>
public enum PoliticalSettlementReason
{
    /// <summary>발행 세력 기준을 이행 → 신뢰 상승.</summary>
    KeptIssuer,

    /// <summary>발행 세력 기준을 위반/실패 → 신뢰 하락.</summary>
    BrokenIssuer,

    /// <summary>이 서약으로 거스른 세력 → 신뢰 하락.</summary>
    DefiedOpposed,

    /// <summary>같이 제안된 다른 세력 위임을 거절 → 신뢰 소폭 하락.</summary>
    RejectedOffer,
}

/// <summary>정치 정산 한 줄 — 어느 세력(FactionId) 신뢰가 얼마나(Delta), 왜(Reason) 바뀌었나.</summary>
public readonly record struct PoliticalSettlementLine(string FactionId, int Delta, PoliticalSettlementReason Reason);

/// <summary>
/// ADR-0028 정치 warrant 루프 — 한 출격이 만든 정치 정산을 *player-readable* 결과로 박제한다(GPT Pro 검수 #1 가독성).
/// 적용 자체는 settlement(SM.Unity)이 하지만, "무엇이 왜 바뀌었나"가 적용 시점에 사라져 player가 인과를 읽을 수 없었다
/// (정치 의미가 trust 누적치 뒤에 숨음). 이 report가 그 사유(issuer 이행/위반 + opposed 거스름 + offer 거절)를
/// 한 묶음으로 들고 reward 화면에 노출된다. 순수 데이터(ids+ints+사유) — 표시명은 label layer가 해석한다.
/// </summary>
public sealed record PoliticalSettlementReport(
    string PledgedWarrantId,
    string IssuerFactionId,
    string OpposedFactionId,
    WarrantOutcome Outcome,
    IReadOnlyList<PoliticalSettlementLine> Lines)
{
    public static readonly PoliticalSettlementReport Empty = new(
        string.Empty, string.Empty, string.Empty, WarrantOutcome.NotApplicable, Array.Empty<PoliticalSettlementLine>());

    /// <summary>정치 서약이 있었나(issuer 존재 + 정산 line 존재). 미서약/build축이면 false → 화면에서 정치 섹션 숨김.</summary>
    public bool HasPolitics => !string.IsNullOrEmpty(IssuerFactionId) && Lines.Count > 0;

    /// <summary>발행 세력 line(이행/위반) — headline 표시용. 없으면 null.</summary>
    public PoliticalSettlementLine? IssuerLine
    {
        get
        {
            foreach (var line in Lines)
            {
                if (line.Reason is PoliticalSettlementReason.KeptIssuer or PoliticalSettlementReason.BrokenIssuer)
                {
                    return line;
                }
            }

            return null;
        }
    }
}

/// <summary>
/// warrant 판정 결과 + offer 셋을 정치 정산 <see cref="PoliticalSettlementReport"/>로 조립하는 순수 함수.
/// delta 수치는 기존 서비스에 위임(<see cref="FactionTrustService"/> 이행/거스름, <see cref="WarrantOfferService"/> 거절)하고,
/// 각 line에 사유를 태깅해 한 묶음으로 합친다. settlement(SM.Unity)은 이 report의 line을 그대로 적용 + 화면 노출용으로 보관한다.
/// 두 delta 소스는 세력이 disjoint(거절은 issuer/opposed 제외)이므로 합쳐도 중복 없음.
/// </summary>
public static class PoliticalSettlementReporter
{
    /// <param name="outcome">warrant 판정 결과(fact-bag judge 산출, ADR-0027).</param>
    /// <param name="issuerFactionId">warrant 발행 세력. 비면 정치 결과 없음(Empty).</param>
    /// <param name="opposedFactionId">이 서약으로 거스른 세력. 비면 거스름 line 없음.</param>
    /// <param name="offeredWarrantIds">이번 출격에 제안된 서약들(거절 면 도출 소스). 현재는 placeholder.</param>
    /// <param name="pledgedWarrantId">실제 서약한 warrant id.</param>
    public static PoliticalSettlementReport Build(
        WarrantOutcome outcome,
        string issuerFactionId,
        string opposedFactionId,
        IEnumerable<string> offeredWarrantIds,
        string pledgedWarrantId)
    {
        if (string.IsNullOrWhiteSpace(issuerFactionId))
        {
            return PoliticalSettlementReport.Empty;
        }

        var lines = new List<PoliticalSettlementLine>();

        // 이행/거스름 — FactionTrustService가 issuer ±, opposed −를 산출. 사유는 faction id 매칭으로 태깅.
        foreach (var delta in FactionTrustService.ComputeDeltas(outcome, issuerFactionId, opposedFactionId))
        {
            var reason = string.Equals(delta.FactionId, issuerFactionId, StringComparison.Ordinal)
                ? (outcome == WarrantOutcome.Kept ? PoliticalSettlementReason.KeptIssuer : PoliticalSettlementReason.BrokenIssuer)
                : PoliticalSettlementReason.DefiedOpposed;
            lines.Add(new PoliticalSettlementLine(delta.FactionId, delta.Delta, reason));
        }

        // 거절 면(#5) — WarrantOfferService가 거절당한 세력 −를 산출(issuer/opposed 제외).
        foreach (var delta in WarrantOfferService.ComputeRejectionDeltas(offeredWarrantIds, pledgedWarrantId))
        {
            lines.Add(new PoliticalSettlementLine(delta.FactionId, delta.Delta, PoliticalSettlementReason.RejectedOffer));
        }

        return new PoliticalSettlementReport(pledgedWarrantId, issuerFactionId, opposedFactionId, outcome, lines);
    }
}
