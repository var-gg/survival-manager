using System.Linq;
using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class WarrantOptionBuilderTests
{
    [Test]
    public void Build_PoliticalWarrants_OneOptionPerFactionWarrant()
    {
        var options = WarrantOptionBuilder.Build(WarrantCatalog.PoliticalWarrantIds, _ => 0);
        Assert.That(options.Count, Is.EqualTo(WarrantCatalog.PoliticalWarrantIds.Count));
        Assert.That(options.All(o => !string.IsNullOrWhiteSpace(o.IssuerFactionId)), Is.True);
    }

    [Test]
    public void Build_ExcludesNonPoliticalWarrants()
    {
        // build축 서약(issuer 없음)을 직접 제안해도 옵션에서 빠진다(정치 선택만 제안).
        var options = WarrantOptionBuilder.Build(new[] { WarrantCatalog.IntactId, WarrantCatalog.SolarumOrderId }, _ => 0);
        Assert.That(options.Count, Is.EqualTo(1));
        Assert.That(options[0].WarrantId, Is.EqualTo(WarrantCatalog.SolarumOrderId));
    }

    [Test]
    public void Build_CarriesIssuerOpposedAndKind()
    {
        var option = WarrantOptionBuilder.Build(new[] { WarrantCatalog.SolarumOrderId }, _ => 0).Single();
        Assert.That(option.IssuerFactionId, Is.EqualTo(WarrantCatalog.SolarumId));
        Assert.That(option.OpposedFactionId, Is.EqualTo(WarrantCatalog.PaleConclaveId));
        Assert.That(option.Kind, Is.EqualTo(WarrantKind.Intact));
    }

    [Test]
    public void Build_PreviewShowsSupportWhenIssuerTrusted()
    {
        // 현재 standing 기준 미리보기 — issuer 신뢰가 높으면 지원 조건이 선택 시점에 보인다(GPT Pro #1 가독성).
        var option = WarrantOptionBuilder.Build(
            new[] { WarrantCatalog.SolarumOrderId },
            factionId => factionId == WarrantCatalog.SolarumId ? PoliticalCombatConditionService.SupportTrustThreshold : 0).Single();

        Assert.That(option.PreviewConditions.Any(c => c.Channel == PoliticalChannel.AllySupport), Is.True);
    }

    [Test]
    public void Build_PreviewShowsAlertWhenOpposedHostile()
    {
        var option = WarrantOptionBuilder.Build(
            new[] { WarrantCatalog.SolarumOrderId },
            factionId => factionId == WarrantCatalog.PaleConclaveId ? PoliticalCombatConditionService.AlertStandingThreshold : 0).Single();

        Assert.That(option.PreviewConditions.Any(c => c.Channel == PoliticalChannel.EnemyAlertness), Is.True);
    }

    [Test]
    public void Build_NullLookup_OptionsWithEmptyPreview()
    {
        var options = WarrantOptionBuilder.Build(WarrantCatalog.PoliticalWarrantIds, null);
        Assert.That(options.Count, Is.EqualTo(WarrantCatalog.PoliticalWarrantIds.Count));
        Assert.That(options.All(o => o.PreviewConditions.Count == 0), Is.True);
    }

    [Test]
    public void Build_DeduplicatesWarrantIds()
    {
        var options = WarrantOptionBuilder.Build(new[] { WarrantCatalog.SolarumOrderId, WarrantCatalog.SolarumOrderId }, _ => 0);
        Assert.That(options.Count, Is.EqualTo(1));
    }
}
