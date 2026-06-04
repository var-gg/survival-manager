using NUnit.Framework;
using SM.Meta;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class WarrantDisplayDefaultsTests
{
    [Test]
    public void FactionName_ResolvesSettledFactions()
    {
        Assert.That(WarrantDisplayDefaults.FactionName(WarrantCatalog.SolarumId), Is.EqualTo("솔라룸"));
        Assert.That(WarrantDisplayDefaults.FactionName(WarrantCatalog.WolfpineId), Is.EqualTo("이리솔 부족"));
        Assert.That(WarrantDisplayDefaults.FactionName(WarrantCatalog.PaleConclaveId), Is.EqualTo("회상 결사"));
        Assert.That(WarrantDisplayDefaults.FactionName(WarrantCatalog.LatticeId), Is.EqualTo("그물 결사"));
    }

    [Test]
    public void UnknownId_ReturnsEmpty()
    {
        Assert.That(WarrantDisplayDefaults.FactionName("faction_unknown"), Is.Empty);
        Assert.That(WarrantDisplayDefaults.WarrantName(string.Empty), Is.Empty);
    }

    [Test]
    public void KindName_SwiftAndIntact()
    {
        Assert.That(WarrantDisplayDefaults.KindName(WarrantKind.Swift), Is.EqualTo("속전"));
        Assert.That(WarrantDisplayDefaults.KindName(WarrantKind.Intact), Is.EqualTo("온전"));
    }

    [Test]
    public void SettlementReasonText_AllReasonsMapped()
    {
        Assert.That(WarrantDisplayDefaults.SettlementReasonText(PoliticalSettlementReason.KeptIssuer), Is.EqualTo("약속 이행"));
        Assert.That(WarrantDisplayDefaults.SettlementReasonText(PoliticalSettlementReason.BrokenIssuer), Is.EqualTo("약속 위반"));
        Assert.That(WarrantDisplayDefaults.SettlementReasonText(PoliticalSettlementReason.DefiedOpposed), Is.EqualTo("거스름"));
        Assert.That(WarrantDisplayDefaults.SettlementReasonText(PoliticalSettlementReason.RejectedOffer), Is.EqualTo("거절"));
    }

    [Test]
    public void SitePressureCause_MappedSitesAndContested_NonEmpty_UnknownEmpty()
    {
        // 모든 seed site + contested는 압력 산문이 있어야 선택 화면 헤더가 빈칸을 안 띄운다(coverage guard).
        foreach (var siteId in SiteWarrantOfferResolver.MappedSiteIds)
        {
            Assert.That(WarrantDisplayDefaults.SitePressureCause(siteId), Is.Not.Empty, siteId);
        }

        Assert.That(WarrantDisplayDefaults.SitePressureCause(SiteWarrantOfferResolver.ContestedCauseCode), Is.Not.Empty);
        Assert.That(WarrantDisplayDefaults.SitePressureCause("unknown_code"), Is.Empty);
    }

    [Test]
    public void ChannelText_SupportAndAlert()
    {
        Assert.That(WarrantDisplayDefaults.ChannelText(PoliticalChannel.AllySupport), Is.EqualTo("아군 후원"));
        Assert.That(WarrantDisplayDefaults.ChannelText(PoliticalChannel.EnemyAlertness), Is.EqualTo("적 경계"));
    }

    [Test]
    public void EveryPoliticalWarrant_HasNameAndIssuerName()
    {
        // 4 정치 warrant 모두 표시명 + issuer 세력 표시명이 있어야 선택 UI가 빈칸을 안 띄운다(coverage guard).
        foreach (var warrantId in WarrantCatalog.PoliticalWarrantIds)
        {
            Assert.That(WarrantDisplayDefaults.WarrantName(warrantId), Is.Not.Empty, warrantId);
            WarrantCatalog.TryResolve(warrantId, out var spec);
            Assert.That(WarrantDisplayDefaults.FactionName(spec.IssuerFactionId), Is.Not.Empty, spec.IssuerFactionId);
        }
    }
}
