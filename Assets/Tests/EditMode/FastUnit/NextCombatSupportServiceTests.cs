using System.Linq;
using NUnit.Framework;
using SM.Core.Stats;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class NextCombatSupportServiceTests
{
    [Test]
    public void Resolve_NoIssuer_ReturnsNull()
    {
        Assert.That(NextCombatSupportService.Resolve("", NextCombatSupportService.SupportTrustThreshold + 10), Is.Null);
    }

    [Test]
    public void Resolve_TrustBelowThreshold_ReturnsNull()
    {
        Assert.That(NextCombatSupportService.Resolve("faction_a", NextCombatSupportService.SupportTrustThreshold - 1), Is.Null);
    }

    [Test]
    public void Resolve_TrustAtThreshold_GrantsSupport()
    {
        var grant = NextCombatSupportService.Resolve("faction_a", NextCombatSupportService.SupportTrustThreshold);
        Assert.That(grant, Is.Not.Null);
        Assert.That(grant!.FactionId, Is.EqualTo("faction_a"));
        Assert.That(grant.Trust, Is.EqualTo(NextCombatSupportService.SupportTrustThreshold));
    }

    [Test]
    public void BuildPackage_CarriesSelfDescribingSourceId_AndFlatStatBonuses()
    {
        var grant = NextCombatSupportService.Resolve("faction_north_council", NextCombatSupportService.SupportTrustThreshold)!;
        var package = NextCombatSupportService.BuildPackage(grant);

        Assert.That(package.SourceId, Is.EqualTo("faction_support:faction_north_council"));
        var hp = package.Modifiers.Single(m => m.Stat == StatKey.MaxHealth);
        var power = package.Modifiers.Single(m => m.Stat == StatKey.PhysPower);
        Assert.That(hp.Op, Is.EqualTo(ModifierOp.Flat));
        Assert.That(hp.Value, Is.EqualTo(NextCombatSupportService.SupportMaxHealthBonus));
        Assert.That(power.Op, Is.EqualTo(ModifierOp.Flat));
        Assert.That(power.Value, Is.EqualTo(NextCombatSupportService.SupportPhysPowerBonus));
    }

    [Test]
    public void ResolveSupportPackages_HighTrustIssuer_ProducesOnePackage()
    {
        // warrant_council_mandate 발행 세력 = faction_north_council. 신뢰가 임계 이상이면 지원 1건.
        var packages = NextCombatSupportService.ResolveSupportPackages(
            WarrantCatalog.CouncilMandateId,
            factionId => factionId == "faction_north_council" ? NextCombatSupportService.SupportTrustThreshold : 0);

        Assert.That(packages.Count, Is.EqualTo(1));
        Assert.That(packages[0].SourceId, Is.EqualTo("faction_support:faction_north_council"));
    }

    [Test]
    public void ResolveSupportPackages_LowTrust_ProducesNothing()
    {
        var packages = NextCombatSupportService.ResolveSupportPackages(
            WarrantCatalog.CouncilMandateId,
            _ => NextCombatSupportService.SupportTrustThreshold - 1);

        Assert.That(packages, Is.Empty);
    }

    [Test]
    public void ResolveSupportPackages_NonPoliticalWarrant_ProducesNothing()
    {
        // build축 서약(issuer 없음)은 신뢰가 아무리 높아도 지원 없음.
        var packages = NextCombatSupportService.ResolveSupportPackages(
            WarrantCatalog.IntactId,
            _ => 999);

        Assert.That(packages, Is.Empty);
    }

    [Test]
    public void ResolveSupportPackages_NoPledgedWarrant_ProducesNothing()
    {
        var packages = NextCombatSupportService.ResolveSupportPackages(
            string.Empty,
            _ => 999);

        Assert.That(packages, Is.Empty);
    }
}
