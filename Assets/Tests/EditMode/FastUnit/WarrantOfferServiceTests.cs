using System.Linq;
using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class WarrantOfferServiceTests
{
    [Test]
    public void ComputeRejectionDeltas_NonPoliticalOrNoPledge_Empty()
    {
        // build축 서약(issuer 없음)·미서약은 정치 선택이 아니라 거절 면 없음.
        Assert.That(WarrantOfferService.ComputeRejectionDeltas(WarrantCatalog.PoliticalWarrantIds, WarrantCatalog.IntactId), Is.Empty);
        Assert.That(WarrantOfferService.ComputeRejectionDeltas(WarrantCatalog.PoliticalWarrantIds, string.Empty), Is.Empty);
    }

    [Test]
    public void ComputeRejectionDeltas_SolarumOrder_DingsNonOpposedRivals()
    {
        // solarum_order = solarum 사이드, pale_conclave 대립(slice 1). 나머지 제안(wolfpine/lattice)을 거절.
        var deltas = WarrantOfferService.ComputeRejectionDeltas(WarrantCatalog.PoliticalWarrantIds, WarrantCatalog.SolarumOrderId);
        var dinged = deltas.Select(d => d.FactionId).ToList();

        Assert.That(dinged, Does.Contain(WarrantCatalog.WolfpineId));
        Assert.That(dinged, Does.Contain(WarrantCatalog.LatticeId));
        // 사이드(issuer)와 이미 처리된 대립(opposed)은 거절로 다시 안 센다(slice 1 중복 방지).
        Assert.That(dinged, Does.Not.Contain(WarrantCatalog.SolarumId));
        Assert.That(dinged, Does.Not.Contain(WarrantCatalog.PaleConclaveId));
        Assert.That(deltas.All(d => d.Delta == -WarrantOfferService.RejectedOfferLoss), Is.True);
    }

    [Test]
    public void ComputeRejectionDeltas_EachRejectedFactionCountedOnce()
    {
        var deltas = WarrantOfferService.ComputeRejectionDeltas(WarrantCatalog.PoliticalWarrantIds, WarrantCatalog.WolfpineHuntId);
        Assert.That(deltas.Select(d => d.FactionId).Distinct().Count(), Is.EqualTo(deltas.Count));
        // wolfpine_hunt = wolfpine 사이드, solarum 대립 → pale/lattice만 거절(solarum 제외).
        var dinged = deltas.Select(d => d.FactionId).ToList();
        Assert.That(dinged, Does.Not.Contain(WarrantCatalog.SolarumId));
        Assert.That(dinged, Does.Not.Contain(WarrantCatalog.WolfpineId));
    }
}
