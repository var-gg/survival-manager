using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// 동적 augment offer 코어(AugmentOfferService.BuildOffer) de-orphan + 검증. reward flow 가
/// 하드코딩 대신 이 점수 기반 빌더를 쓰도록 와이어됐으므로(SessionProfileSync.ResolveRewardAugmentId),
/// cap·family dedup·permanent 제외/억제·tag 점수·mutual exclusion 거동을 격리 검증한다.
/// </summary>
[Category("FastUnit")]
public sealed class AugmentOfferServiceTests
{
    private static AugmentCatalogEntry Aug(
        string id,
        string familyId,
        int tier,
        string[]? tags = null,
        string[]? mutualExclusion = null,
        bool isPermanent = false,
        bool suppressIfPermanent = false,
        string category = "combat")
    {
        return new AugmentCatalogEntry(
            id,
            category,
            familyId,
            tier,
            isPermanent,
            suppressIfPermanent,
            tags ?? Array.Empty<string>(),
            mutualExclusion ?? Array.Empty<string>(),
            new CombatModifierPackage(id, ModifierSource.Augment, Array.Empty<StatModifier>()));
    }

    private static Dictionary<string, AugmentCatalogEntry> Catalog(params AugmentCatalogEntry[] entries)
        => entries.ToDictionary(entry => entry.Id, entry => entry, StringComparer.Ordinal);

    [Test]
    public void BuildOffer_CapsAtMaxChoices_AndDedupesByFamily()
    {
        var catalog = Catalog(
            Aug("a1", "fam_a", 3),
            Aug("a2", "fam_a", 2), // same family as a1 — must be deduped
            Aug("b1", "fam_b", 3),
            Aug("c1", "fam_c", 3),
            Aug("d1", "fam_d", 3));

        var offer = AugmentOfferService.BuildOffer(catalog, Array.Empty<string>(), Array.Empty<string>(), maxChoices: 3);

        Assert.That(offer.Count, Is.EqualTo(3), "Should respect maxChoices");
        Assert.That(offer.Select(entry => entry.FamilyId).Distinct().Count(), Is.EqualTo(offer.Count), "One offer per family");
    }

    [Test]
    public void BuildOffer_ExcludesPermanentEquippedById()
    {
        var catalog = Catalog(Aug("a1", "fam_a", 3), Aug("b1", "fam_b", 3));

        var offer = AugmentOfferService.BuildOffer(catalog, Array.Empty<string>(), new[] { "a1" }, maxChoices: 5);

        Assert.That(offer.Select(entry => entry.Id), Does.Not.Contain("a1"), "Permanently-equipped augment must not be offered");
        Assert.That(offer.Select(entry => entry.Id), Does.Contain("b1"));
    }

    [Test]
    public void BuildOffer_SuppressesFamilyOfPermanentEquipped()
    {
        var catalog = Catalog(
            Aug("perm_a", "fam_a", 3, isPermanent: true),
            Aug("temp_a", "fam_a", 3, suppressIfPermanent: true), // same family as a permanently-equipped augment
            Aug("b1", "fam_b", 3));

        var offer = AugmentOfferService.BuildOffer(catalog, Array.Empty<string>(), new[] { "perm_a" }, maxChoices: 5);

        Assert.That(offer.Select(entry => entry.Id), Does.Not.Contain("temp_a"), "Family of permanently-equipped augment is suppressed");
        Assert.That(offer.Select(entry => entry.Id), Does.Contain("b1"));
    }

    [Test]
    public void BuildOffer_RanksTagMatchingHigher()
    {
        var catalog = Catalog(
            Aug("match", "fam_a", 1, tags: new[] { "guard" }), // tier 1 + active-tag match
            Aug("plain", "fam_b", 1));                          // tier 1, no tag

        var offer = AugmentOfferService.BuildOffer(catalog, new[] { "guard" }, Array.Empty<string>(), maxChoices: 2);

        Assert.That(offer[0].Id, Is.EqualTo("match"), "Augment matching the active build tag should rank first");
    }

    [Test]
    public void BuildOffer_FiltersMutualExclusionAgainstActiveTags()
    {
        var catalog = Catalog(
            Aug("safe", "fam_a", 2),
            Aug("conflict", "fam_b", 3, mutualExclusion: new[] { "active_tag" }));

        var offer = AugmentOfferService.BuildOffer(catalog, new[] { "active_tag" }, Array.Empty<string>(), maxChoices: 5);

        Assert.That(offer.Select(entry => entry.Id), Does.Not.Contain("conflict"), "Mutually-exclusive augment against an active tag must be filtered");
        Assert.That(offer.Select(entry => entry.Id), Does.Contain("safe"));
    }
}
