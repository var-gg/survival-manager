using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

public sealed class GameSessionStateTests
{
    [Test]
    public void BindProfile_SeedsCanonicalArchetypeAndTraitIds()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());

        var canonicalArchetypes = lookup.GetCanonicalArchetypeIds().ToHashSet();
        Assert.That(session.Profile.Heroes, Has.Count.EqualTo(MetaBalanceDefaults.ExpeditionSquadCap));
        Assert.That(session.Profile.Heroes.All(hero => canonicalArchetypes.Contains(hero.ArchetypeId)), Is.True);
        Assert.That(session.Profile.Heroes.All(hero =>
        {
            lookup.TryGetTraitIds(hero.ArchetypeId, out var positives, out var negatives);
            return positives.Contains(hero.PositiveTraitId) && negatives.Contains(hero.NegativeTraitId);
        }), Is.True);
        Assert.That(session.Profile.Inventory.Any(item => !string.IsNullOrWhiteSpace(item.EquippedHeroId)), Is.True, "데모 프로필은 item variance를 확인할 수 있도록 최소 일부 장비를 들고 있어야 합니다.");
    }

    [Test]
    public void RewardPayloadIds_ResolveToCanonicalSampleContent()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());

        session.PrepareQuickBattleSmoke();
        session.SetLastBattleResult(true, "quick-win");

        var itemIds = lookup.GetCanonicalItemIds().ToHashSet();
        var augmentIds = lookup.GetCanonicalTemporaryAugmentIds().ToHashSet();

        Assert.That(session.PendingRewardChoices.Where(choice => choice.Kind == RewardChoiceKind.Item).All(choice => itemIds.Contains(choice.PayloadId)), Is.True);
        Assert.That(session.PendingRewardChoices.Where(choice => choice.Kind == RewardChoiceKind.TemporaryAugment).All(choice => augmentIds.Contains(choice.PayloadId)), Is.True);
    }

    [Test]
    public void Recruit_SucceedsWithEnoughGold_AndDeductsCost()
    {
        var session = CreateBoundSession();
        session.Profile.Currencies.Gold = MetaBalanceDefaults.RecruitCost;
        var heroCountBefore = session.Profile.Heroes.Count;

        var result = session.Recruit(0);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(0));
        Assert.That(session.Profile.Heroes.Count, Is.EqualTo(heroCountBefore + 1));
    }

    [Test]
    public void Recruit_FailsAtRosterCap_AndLeavesStateUnchanged()
    {
        var session = CreateBoundSession();
        session.Profile.Currencies.Gold = 99;
        while (session.Profile.Heroes.Count < MetaBalanceDefaults.TownRosterCap)
        {
            var template = session.Profile.Heroes[0];
            session.Profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = $"cap-{session.Profile.Heroes.Count + 1}",
                Name = $"Cap {session.Profile.Heroes.Count + 1}",
                ArchetypeId = template.ArchetypeId,
                RaceId = template.RaceId,
                ClassId = template.ClassId,
                PositiveTraitId = template.PositiveTraitId,
                NegativeTraitId = template.NegativeTraitId,
                EquippedItemIds = new List<string>()
            });
        }

        var heroCountBefore = session.Profile.Heroes.Count;
        var goldBefore = session.Profile.Currencies.Gold;
        var offerIdsBefore = session.RecruitOffers.Select(offer => offer.HeroId).ToArray();

        var result = session.Recruit(0);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("Town roster cap"));
        Assert.That(session.Profile.Heroes.Count, Is.EqualTo(heroCountBefore));
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(goldBefore));
        Assert.That(session.RecruitOffers.Select(offer => offer.HeroId).ToArray(), Is.EqualTo(offerIdsBefore));
    }

    [Test]
    public void RerollRecruitOffers_FailsWithoutGold_AndPreservesOfferList()
    {
        var session = CreateBoundSession();
        session.Profile.Currencies.Gold = 0;
        var before = session.RecruitOffers.Select(offer => offer.HeroId).ToArray();

        var result = session.RerollRecruitOffers();

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(0));
        Assert.That(session.RecruitOffers.Select(offer => offer.HeroId).ToArray(), Is.EqualTo(before));
    }

    [Test]
    public void RerollRecruitOffers_SucceedsWithGold_AndDeductsCost()
    {
        var session = CreateBoundSession();
        session.Profile.Currencies.Gold = MetaBalanceDefaults.RecruitRerollCost;
        var before = session.RecruitOffers.Select(offer => offer.HeroId).ToArray();

        var result = session.RerollRecruitOffers();

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(0));
        Assert.That(session.RecruitOffers, Has.Count.EqualTo(3));
        Assert.That(session.RecruitOffers.Select(offer => offer.HeroId).ToArray(), Is.Not.EqualTo(before));
    }

    private static GameSessionState CreateBoundSession()
    {
        var session = new GameSessionState(new RuntimeCombatContentLookup());
        session.BindProfile(new SaveProfile());
        return session;
    }
}
