using System.Linq;
using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

/// <summary>
/// ADR-0028 #b — warrant offer가 *site의 정치 맥락*에서 도출되는지 검증. 핵심은 데이터 경로:
/// 등록 site는 압력 세력 + site별 offer, 미등록 site는 전역 정치 카탈로그 graceful fallback(비회귀).
/// </summary>
[Category("FastUnit")]
public sealed class SiteWarrantOfferResolverTests
{
    [Test]
    public void MappedSite_ResolvesPressureFactionAndSiteOffers()
    {
        var offer = SiteWarrantOfferResolver.Resolve(SiteWarrantOfferResolver.WolfpineTrailSiteId);

        Assert.That(offer.PressureFactionId, Is.EqualTo(WarrantCatalog.WolfpineId));
        Assert.That(offer.OfferedWarrantIds, Does.Contain(WarrantCatalog.WolfpineHuntId));
        Assert.That(offer.OfferedWarrantIds, Does.Contain(WarrantCatalog.SolarumOrderId));
        Assert.That(offer.CauseCode, Is.EqualTo(SiteWarrantOfferResolver.WolfpineTrailSiteId));
    }

    [Test]
    public void UnmappedSite_FallsBackToGlobalPoliticalCatalog()
    {
        var offer = SiteWarrantOfferResolver.Resolve("site_unmapped_xyz");

        Assert.That(offer.PressureFactionId, Is.Empty, "미등록 site는 특정 압력 세력 없음.");
        Assert.That(offer.OfferedWarrantIds, Is.EqualTo(WarrantCatalog.PoliticalWarrantIds));
        Assert.That(offer.CauseCode, Is.EqualTo(SiteWarrantOfferResolver.ContestedCauseCode));
    }

    [Test]
    public void NullOrEmptySite_FallsBack()
    {
        Assert.That(SiteWarrantOfferResolver.Resolve(string.Empty).OfferedWarrantIds, Is.EqualTo(WarrantCatalog.PoliticalWarrantIds));
        Assert.That(SiteWarrantOfferResolver.Resolve(null!).OfferedWarrantIds, Is.EqualTo(WarrantCatalog.PoliticalWarrantIds));
    }

    [Test]
    public void EveryMappedSite_OffersValidPoliticalWarrants_AndPressureFactionIssuesOne()
    {
        // 모든 seed site: 제안 warrant ≥2(거절 면 비-퇴화) + 전부 유효 정치 warrant + 압력 세력이 그중 하나의 issuer.
        Assert.That(SiteWarrantOfferResolver.MappedSiteIds, Is.Not.Empty);
        foreach (var siteId in SiteWarrantOfferResolver.MappedSiteIds)
        {
            var offer = SiteWarrantOfferResolver.Resolve(siteId);
            Assert.That(offer.PressureFactionId, Is.Not.Empty, siteId);
            Assert.That(offer.OfferedWarrantIds.Count, Is.GreaterThanOrEqualTo(2), $"{siteId}: 거절 면이 비-퇴화하려면 ≥2 offer.");

            foreach (var warrantId in offer.OfferedWarrantIds)
            {
                Assert.That(WarrantCatalog.PoliticalWarrantIds, Does.Contain(warrantId), $"{siteId}: {warrantId}");
                Assert.That(WarrantCatalog.TryResolve(warrantId, out _), Is.True, warrantId);
            }

            Assert.That(
                offer.OfferedWarrantIds.Any(id => WarrantCatalog.TryResolve(id, out var spec) && spec.IssuerFactionId == offer.PressureFactionId),
                Is.True,
                $"{siteId}: 압력 세력({offer.PressureFactionId}) 위임이 offer에 있어야(자기 장소에서 자기 기준을 내건다).");
        }
    }
}
