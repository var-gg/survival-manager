using NUnit.Framework;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CampaignEncounterSeedFastTests
{
    [Test]
    public void Derive_IsStableForSameCampaignAndNode()
    {
        var campaignSeed = CampaignEncounterSeed.FromCampaignIdentity("profile-alpha");

        var first = CampaignEncounterSeed.Derive(campaignSeed, "node-boss");
        var second = CampaignEncounterSeed.Derive(campaignSeed, "node-boss");

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first, Is.GreaterThan(0));
    }

    [Test]
    public void Derive_ChangesWhenCampaignOrNodeChanges()
    {
        var campaignSeed = CampaignEncounterSeed.FromCampaignIdentity("profile-alpha");
        var otherCampaignSeed = CampaignEncounterSeed.FromCampaignIdentity("profile-beta");
        var baseline = CampaignEncounterSeed.Derive(campaignSeed, "node-boss");

        Assert.That(
            CampaignEncounterSeed.Derive(otherCampaignSeed, "node-boss"),
            Is.Not.EqualTo(baseline));
        Assert.That(
            CampaignEncounterSeed.Derive(campaignSeed, "node-elite"),
            Is.Not.EqualTo(baseline));
    }

    [Test]
    public void Apply_IgnoresAttemptAndReequipContextDrift()
    {
        var campaignSeed = CampaignEncounterSeed.FromCampaignIdentity("profile-alpha");
        var firstAttempt = CreateContext("compile-before-refit", 17);
        var laterAttemptAfterRefit = CreateContext("compile-after-refit", 991);

        var first = CampaignEncounterSeed.Apply(firstAttempt, campaignSeed);
        var later = CampaignEncounterSeed.Apply(laterAttemptAfterRefit, campaignSeed);

        Assert.That(later.BattleSeed, Is.EqualTo(first.BattleSeed),
            "attempt count and loadout/refit context changes must not reroll an encounter.");
        Assert.That(first.BattleContextHash, Is.EqualTo(firstAttempt.BattleContextHash),
            "seed stamping must not rewrite the replay/loadout context hash.");
        Assert.That(later.BattleContextHash, Is.EqualTo(laterAttemptAfterRefit.BattleContextHash));
    }

    private static BattleContextState CreateContext(string contextHash, int legacySeed)
        => new(
            "chapter",
            "site",
            3,
            "node-boss",
            legacySeed,
            contextHash,
            "reward-boss",
            4,
            true,
            "faction",
            "overlay");
}
