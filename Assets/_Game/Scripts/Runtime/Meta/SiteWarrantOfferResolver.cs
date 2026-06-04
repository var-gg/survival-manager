using System;
using System.Collections.Generic;

namespace SM.Meta;

/// <summary>
/// 한 site의 정치 맥락 — 어느 세력(<see cref="PressureFactionId"/>)이 이 장소에 압력을 행사하고,
/// 어떤 서약(<see cref="OfferedWarrantIds"/>)이 제안되며, 왜(<see cref="CauseCode"/>). stable id/code만 —
/// 표시명·산문은 label layer가 cause code로 해석한다(ID/label 분리).
/// </summary>
public sealed record SiteWarrantOffer(
    string SiteId,
    string PressureFactionId,
    IReadOnlyList<string> OfferedWarrantIds,
    string CauseCode);

/// <summary>
/// ADR-0028 #b — warrant offer를 *site의 정치 맥락*에서 도출한다(GPT Pro 검수: warrant가 "런 시작 전 고르는
/// 계약 카드"가 아니라 "이 장소에서 발생한 정치 사건"으로 읽히게). 이전엔 offer 소스가 전역
/// <see cref="WarrantCatalog.PoliticalWarrantIds"/>(placeholder)라 site와 무관했다.
///
/// 목적은 콘텐츠 완성도가 아니라 *데이터 경로*다 — 모든 offer가 이 resolver를 통과한다. 미등록 site는
/// 전역 정치 카탈로그로 graceful fallback(기존 behavior 보존), 압력 세력 없음. 정치 지리 seed는 reskin
/// settled(analysis-narrative-reskin-4-faction-root-draft)에 grounded: Wolfpine Trail=이리솔,
/// 묘역=회상 결사, 옛 왕도 관문=솔라룸, 격자 노드=그물 결사. 전체 10-site authoring은 world bible 후속.
/// 순수 함수 — site id(string)만 받아 stable id를 낸다. SM.Meta 경계 유지.
/// </summary>
public static class SiteWarrantOfferResolver
{
    /// <summary>미등록 site — 여러 세력 위임이 얽힌 generic 압력(특정 압력 세력 없음).</summary>
    public const string ContestedCauseCode = "site.contested";

    // 정치 지리 seed. 각 site는 압력 세력의 위임 + 그에 도전/대립하는 위임을 제안 — "여기서 누구 편에 서나".
    private static readonly IReadOnlyDictionary<string, SiteWarrantOffer> SiteOffers =
        new Dictionary<string, SiteWarrantOffer>(StringComparer.Ordinal)
        {
            // 이리솔 부족의 사냥터 — 솔라룸의 질서가 변경까지 손을 뻗는다.
            [WolfpineTrailSiteId] = new(
                WolfpineTrailSiteId,
                WarrantCatalog.WolfpineId,
                new[] { WarrantCatalog.WolfpineHuntId, WarrantCatalog.SolarumOrderId },
                WolfpineTrailSiteId),

            // 회상 결사의 묘역 — 솔라룸의 정화가 기억을 쫓는다.
            [RuinedCryptsSiteId] = new(
                RuinedCryptsSiteId,
                WarrantCatalog.PaleConclaveId,
                new[] { WarrantCatalog.PaleConclaveVigilId, WarrantCatalog.SolarumOrderId },
                RuinedCryptsSiteId),

            // 솔라룸 옛 왕도의 관문 — 변경 세력이 그 권위에 도전한다.
            [AshenGateSiteId] = new(
                AshenGateSiteId,
                WarrantCatalog.SolarumId,
                new[] { WarrantCatalog.SolarumOrderId, WarrantCatalog.WolfpineHuntId },
                AshenGateSiteId),

            // 그물 결사의 격자 노드 — 솔라룸이 격자를 무기로 탐한다.
            [WorldscarDepthsSiteId] = new(
                WorldscarDepthsSiteId,
                WarrantCatalog.LatticeId,
                new[] { WarrantCatalog.LatticePrecisionId, WarrantCatalog.SolarumOrderId },
                WorldscarDepthsSiteId),
        };

    public const string WolfpineTrailSiteId = "site_wolfpine_trail";
    public const string RuinedCryptsSiteId = "site_ruined_crypts";
    public const string AshenGateSiteId = "site_ashen_gate";
    public const string WorldscarDepthsSiteId = "site_worldscar_depths";

    /// <summary>이 site의 정치 offer. 미등록이면 전역 정치 카탈로그 fallback(압력 세력 없음).</summary>
    public static SiteWarrantOffer Resolve(string siteId)
    {
        if (!string.IsNullOrWhiteSpace(siteId) && SiteOffers.TryGetValue(siteId, out var offer))
        {
            return offer;
        }

        return new SiteWarrantOffer(siteId ?? string.Empty, string.Empty, WarrantCatalog.PoliticalWarrantIds, ContestedCauseCode);
    }

    /// <summary>정치 지리 seed가 등록한 site id(coverage 테스트용).</summary>
    public static IReadOnlyCollection<string> MappedSiteIds => (IReadOnlyCollection<string>)SiteOffers.Keys;
}
