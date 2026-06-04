using System;
using System.Collections.Generic;

namespace SM.Meta;

/// <summary>
/// ADR-0028 정치 warrant 루프 — 출격 전 "어느 세력 기준에 서약하나"의 *거절* 면(GPT Pro 검수 #5).
/// 한 세력 위임을 수락하면 같이 제안된 다른 세력 위임을 거절한 것이다. "누구 편을 들었나"에 세력이 반응하도록
/// 거절당한 세력 신뢰를 소폭 깎는다(다극 회색 충돌의 gradient — slice 3 enemy alertness로 되먹임).
/// 사이드(issuer)·이미 처리된 대립(opposed, slice 1)은 제외해 중복을 막고 "거절"의 새 정보만 남긴다.
///
/// 순수 함수다. offer 소스(어느 site가 무엇을 제안하나)는 호출자가 주입 — 현재는 placeholder로 카탈로그의
/// 정치 warrant 전체(<see cref="WarrantCatalog.PoliticalWarrantIds"/>). 실제 per-site offer는 P2b content가 소유.
/// </summary>
public static class WarrantOfferService
{
    /// <summary>거절당한 세력 mandate 1건당 신뢰 하락. 작게 — 매 출격 다수 거절 가능, 누적 gradient.</summary>
    public const int RejectedOfferLoss = 1;

    /// <summary>
    /// 제안된 서약들 중 *서약하지 않은* 정치 세력을 거절로 본다. issuer/opposed 제외, 세력당 1회.
    /// 정치 서약을 안 했으면(미서약/build축) 빈 결과 — 거절 면 없음.
    /// </summary>
    public static IReadOnlyList<FactionTrustDelta> ComputeRejectionDeltas(
        IEnumerable<string> offeredWarrantIds,
        string pledgedWarrantId)
    {
        var deltas = new List<FactionTrustDelta>();
        if (offeredWarrantIds is null
            || !WarrantCatalog.TryResolve(pledgedWarrantId, out var pledged)
            || string.IsNullOrWhiteSpace(pledged.IssuerFactionId))
        {
            return deltas;
        }

        var excluded = new HashSet<string>(StringComparer.Ordinal) { pledged.IssuerFactionId };
        if (!string.IsNullOrWhiteSpace(pledged.OpposedFactionId))
        {
            excluded.Add(pledged.OpposedFactionId);
        }

        var counted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var offeredId in offeredWarrantIds)
        {
            if (string.Equals(offeredId, pledgedWarrantId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!WarrantCatalog.TryResolve(offeredId, out var offered)
                || string.IsNullOrWhiteSpace(offered.IssuerFactionId)
                || excluded.Contains(offered.IssuerFactionId)
                || !counted.Add(offered.IssuerFactionId))
            {
                continue;
            }

            deltas.Add(new FactionTrustDelta(offered.IssuerFactionId, -RejectedOfferLoss));
        }

        return deltas;
    }
}
