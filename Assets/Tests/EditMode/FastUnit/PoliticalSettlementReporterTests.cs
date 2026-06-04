using System.Linq;
using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

/// <summary>
/// ADR-0028 #1 가독성 — 정치 정산 report 조립(이행/거스름 + 거절 사유 태깅)을 검증.
/// delta 수치는 FactionTrustService/WarrantOfferService 책임이고 여기서는 *사유 분류 + 합성*만 본다.
/// </summary>
[Category("FastUnit")]
public sealed class PoliticalSettlementReporterTests
{
    [Test]
    public void Kept_TagsIssuerGainAndOpposedDefiance()
    {
        // SolarumOrder: issuer=Solarum, opposed=Pale. 단독 offer(거절 면 없음)로 이행 면만 본다.
        var report = PoliticalSettlementReporter.Build(
            WarrantOutcome.Kept,
            WarrantCatalog.SolarumId,
            WarrantCatalog.PaleConclaveId,
            new[] { WarrantCatalog.SolarumOrderId },
            WarrantCatalog.SolarumOrderId);

        Assert.That(report.HasPolitics, Is.True);
        var issuer = report.Lines.Single(line => line.FactionId == WarrantCatalog.SolarumId);
        Assert.That(issuer.Delta, Is.EqualTo(FactionTrustService.SatisfiedIssuerGain));
        Assert.That(issuer.Reason, Is.EqualTo(PoliticalSettlementReason.KeptIssuer));

        var opposed = report.Lines.Single(line => line.FactionId == WarrantCatalog.PaleConclaveId);
        Assert.That(opposed.Delta, Is.EqualTo(-FactionTrustService.SatisfiedOpposedLoss));
        Assert.That(opposed.Reason, Is.EqualTo(PoliticalSettlementReason.DefiedOpposed));
    }

    [Test]
    public void Broken_TagsIssuerLossOnly_NoOpposedLine()
    {
        var report = PoliticalSettlementReporter.Build(
            WarrantOutcome.Broken,
            WarrantCatalog.SolarumId,
            WarrantCatalog.PaleConclaveId,
            new[] { WarrantCatalog.SolarumOrderId },
            WarrantCatalog.SolarumOrderId);

        Assert.That(report.HasPolitics, Is.True);
        var issuer = report.Lines.Single();
        Assert.That(issuer.FactionId, Is.EqualTo(WarrantCatalog.SolarumId));
        Assert.That(issuer.Delta, Is.EqualTo(-FactionTrustService.FailedIssuerLoss));
        Assert.That(issuer.Reason, Is.EqualTo(PoliticalSettlementReason.BrokenIssuer));
    }

    [Test]
    public void FailedMission_TagsBrokenIssuer()
    {
        var report = PoliticalSettlementReporter.Build(
            WarrantOutcome.FailedMission,
            WarrantCatalog.WolfpineId,
            WarrantCatalog.SolarumId,
            new[] { WarrantCatalog.WolfpineHuntId },
            WarrantCatalog.WolfpineHuntId);

        var issuer = report.Lines.Single();
        Assert.That(issuer.Reason, Is.EqualTo(PoliticalSettlementReason.BrokenIssuer));
        Assert.That(issuer.Delta, Is.LessThan(0));
    }

    [Test]
    public void FullOfferSet_TagsRejectedFactions_ExcludingIssuerAndOpposed()
    {
        // 4 정치 warrant 전체 제안, SolarumOrder 서약(issuer=Solarum, opposed=Pale).
        // 거절: Wolfpine·Lattice(−1 each). Solarum(issuer)·Pale(opposed)는 제외(중복 방지).
        var report = PoliticalSettlementReporter.Build(
            WarrantOutcome.Kept,
            WarrantCatalog.SolarumId,
            WarrantCatalog.PaleConclaveId,
            WarrantCatalog.PoliticalWarrantIds,
            WarrantCatalog.SolarumOrderId);

        var rejected = report.Lines
            .Where(line => line.Reason == PoliticalSettlementReason.RejectedOffer)
            .Select(line => line.FactionId)
            .ToList();
        Assert.That(rejected, Is.EquivalentTo(new[] { WarrantCatalog.WolfpineId, WarrantCatalog.LatticeId }));
        Assert.That(
            report.Lines.Where(line => line.Reason == PoliticalSettlementReason.RejectedOffer).Select(line => line.Delta),
            Is.All.EqualTo(-WarrantOfferService.RejectedOfferLoss));

        // issuer/opposed가 거절 면에 섞이지 않는다(이행 면에서만 등장).
        Assert.That(rejected, Does.Not.Contain(WarrantCatalog.SolarumId));
        Assert.That(rejected, Does.Not.Contain(WarrantCatalog.PaleConclaveId));
    }

    [Test]
    public void IssuerLine_ReturnsKeptOrBrokenLine()
    {
        var kept = PoliticalSettlementReporter.Build(
            WarrantOutcome.Kept, WarrantCatalog.SolarumId, WarrantCatalog.PaleConclaveId,
            new[] { WarrantCatalog.SolarumOrderId }, WarrantCatalog.SolarumOrderId);

        Assert.That(kept.IssuerLine, Is.Not.Null);
        Assert.That(kept.IssuerLine!.Value.FactionId, Is.EqualTo(WarrantCatalog.SolarumId));
        Assert.That(kept.IssuerLine!.Value.Reason, Is.EqualTo(PoliticalSettlementReason.KeptIssuer));
    }

    [Test]
    public void NoIssuer_ReturnsEmpty()
    {
        var report = PoliticalSettlementReporter.Build(
            WarrantOutcome.Kept, string.Empty, string.Empty,
            WarrantCatalog.PoliticalWarrantIds, string.Empty);

        Assert.That(report, Is.SameAs(PoliticalSettlementReport.Empty));
        Assert.That(report.HasPolitics, Is.False);
        Assert.That(report.IssuerLine, Is.Null);
    }

    [Test]
    public void NotApplicableWithNoMovement_HasPoliticsFalse()
    {
        // outcome=NotApplicable면 trust delta 0 + 단독 offer라 거절 0 → line 없음 → 정치 섹션 숨김 신호.
        var report = PoliticalSettlementReporter.Build(
            WarrantOutcome.NotApplicable,
            WarrantCatalog.SolarumId,
            WarrantCatalog.PaleConclaveId,
            new[] { WarrantCatalog.SolarumOrderId },
            WarrantCatalog.SolarumOrderId);

        Assert.That(report.Lines, Is.Empty);
        Assert.That(report.HasPolitics, Is.False);
    }
}
