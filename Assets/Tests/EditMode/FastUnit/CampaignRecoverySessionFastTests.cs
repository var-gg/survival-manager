using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class CampaignRecoverySessionFastTests
{
    [Test]
    public void LegacyProfileWithoutRecoveryCounters_NormalizesToZero()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        var progress = new CampaignProgressRecord
        {
            RewardedRevisitCountsByChapter = null!,
            DefeatConsolationCountsByChapter = null!,
        };

        session.BindProfile(new SaveProfile { CampaignProgress = progress });

        Assert.That(session.Profile.CampaignProgress.RewardedRevisitCountsByChapter, Is.Empty);
        Assert.That(session.Profile.CampaignProgress.DefeatConsolationCountsByChapter, Is.Empty);
    }

    [Test]
    public void RewardedRevisitCounter_IsPersistentAndPooledAcrossSitesInChapter()
    {
        var session = CreateSession();
        session.Profile.CampaignProgress.ClearedSiteIds = new List<string>
        {
            "site_alpha_gate",
            "site_alpha_depths",
            "site_beta_watch",
        };

        session.Profile.CampaignProgress.SelectedSiteId = "site_alpha_gate";
        session.BeginNewExpedition();
        Assert.That(session.ActiveRun!.Overlay.RewardedRevisitIndex, Is.EqualTo(1));
        SettleOneVictory(session);
        session.AbandonExpeditionRun();

        session.Profile.CampaignProgress.SelectedSiteId = "site_alpha_depths";
        session.BeginNewExpedition();
        Assert.That(session.ActiveRun!.Overlay.RewardedRevisitIndex, Is.EqualTo(2));
        SettleOneVictory(session);
        Assert.That(
            session.Profile.CampaignProgress.RewardedRevisitCountsByChapter["chapter_alpha"],
            Is.EqualTo(2),
            "same-chapter site hopping shares one rewarded-revisit counter.");
        session.AbandonExpeditionRun();

        session.Profile.CampaignProgress.SelectedChapterId = "chapter_beta";
        session.Profile.CampaignProgress.SelectedSiteId = "site_beta_watch";
        session.BeginNewExpedition();
        Assert.That(session.ActiveRun!.Overlay.RewardedRevisitIndex, Is.EqualTo(1));
        SettleOneVictory(session);
        Assert.That(
            session.Profile.CampaignProgress.RewardedRevisitCountsByChapter["chapter_beta"],
            Is.EqualTo(1),
            "a newly entered chapter owns a fresh recovery budget.");
    }

    [Test]
    public void FifthRewardedRevisit_PaysZeroXpItemsGoldAndEcho()
    {
        var session = CreateSession();
        session.Profile.CampaignProgress.ClearedSiteIds.Add("site_alpha_gate");
        session.Profile.CampaignProgress.RewardedRevisitCountsByChapter["chapter_alpha"] = 4;
        session.BeginNewExpedition();

        var goldBefore = session.Profile.Currencies.Gold;
        var echoBefore = session.Profile.Currencies.Echo;
        var inventoryBefore = session.Profile.Inventory.Count;
        var experienceBefore = session.Profile.HeroProgressions.Single().Experience;
        session.MarkBattleResolved(
            victory: true,
            stepCount: 8,
            eventCount: 4,
            finalUnits: OneSurvivingHero());

        Assert.That(session.ActiveRun!.Overlay.RewardedRevisitIndex, Is.EqualTo(5));
        Assert.That(session.ActiveRun.Overlay.RevisitItemRollsGranted, Is.EqualTo(0));
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(goldBefore));
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBefore));
        Assert.That(session.Profile.Inventory.Count, Is.EqualTo(inventoryBefore));
        Assert.That(session.Profile.HeroProgressions.Single().Experience, Is.EqualTo(experienceBefore));
        Assert.That(session.LastAutomaticLootBundle, Is.Not.Null);
        Assert.That(session.LastAutomaticLootBundle!.Entries, Is.Empty);

        Assert.That(session.ApplyRewardChoice(0), Is.True);
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(goldBefore));
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBefore));
        Assert.That(session.Profile.Inventory.Count, Is.EqualTo(inventoryBefore));
    }

    [Test]
    public void RewardedRevisits_ApplyExactFourThreeTwoOneItemRollQuotas()
    {
        var session = CreateSession();
        session.Profile.CampaignProgress.ClearedSiteIds.Add("site_alpha_gate");
        var expectedRolls = new[] { 4, 3, 2, 1 };

        foreach (var expected in expectedRolls)
        {
            session.BeginNewExpedition();
            var inventoryBefore = session.Profile.Inventory.Count;
            SettleOneVictory(session);

            Assert.That(session.ActiveRun!.Overlay.RevisitItemRollsGranted, Is.EqualTo(expected));
            Assert.That(session.Profile.Inventory.Count - inventoryBefore, Is.EqualTo(expected));
            session.AbandonExpeditionRun();
        }
    }

    [Test]
    public void FirstClear_KeepsFullXpAutomaticLootAndRewardChoice()
    {
        var session = CreateSession();
        var goldBefore = session.Profile.Currencies.Gold;
        var inventoryBefore = session.Profile.Inventory.Count;

        session.BeginNewExpedition();
        Assert.That(session.ActiveRun!.Overlay.RewardedRevisitIndex, Is.EqualTo(0));
        session.MarkBattleResolved(
            victory: true,
            stepCount: 8,
            eventCount: 4,
            finalUnits: OneSurvivingHero());

        Assert.That(session.Profile.HeroProgressions.Single().Experience, Is.EqualTo(50));
        Assert.That(session.Profile.Currencies.Gold - goldBefore, Is.EqualTo(8));
        Assert.That(session.Profile.Inventory.Count - inventoryBefore, Is.EqualTo(1));
        Assert.That(session.PendingRewardChoices.Count, Is.EqualTo(3));
        Assert.That(session.ApplyRewardChoice(0), Is.True);
        Assert.That(session.LastRewardApplicationSummary.HasValue, Is.True);
    }

    [Test]
    public void DefeatConsolation_GrantsPersistentEchoAcrossRunTerminationAndStopsAfterTwo()
    {
        var session = CreateSession();

        AssertDefeatEcho(session, expectedChoiceEcho: 10, expectedWalletEcho: 10);
        AssertDefeatEcho(session, expectedChoiceEcho: 5, expectedWalletEcho: 15);
        AssertDefeatEcho(session, expectedChoiceEcho: 0, expectedWalletEcho: 15);

        Assert.That(
            session.Profile.CampaignProgress.DefeatConsolationCountsByChapter["chapter_alpha"],
            Is.EqualTo(3));
        Assert.That(
            session.Profile.RewardLedger.Count(entry =>
                entry.RewardType == RewardType.TemporaryAugment.ToString()),
            Is.EqualTo(0),
            "story defeat consolation no longer stores a run-scope temporary augment.");
    }

    private static void AssertDefeatEcho(
        GameSessionState session,
        int expectedChoiceEcho,
        int expectedWalletEcho)
    {
        session.BeginNewExpedition();
        session.MarkBattleResolved(victory: false, stepCount: 5, eventCount: 2);

        Assert.That(session.PendingRewardChoices.Count, Is.EqualTo(1));
        Assert.That(session.PendingRewardChoices[0].Kind, Is.EqualTo(RewardChoiceKind.Echo));
        Assert.That(session.PendingRewardChoices[0].EchoAmount, Is.EqualTo(expectedChoiceEcho));
        Assert.That(session.ApplyRewardChoice(0), Is.True);
        session.ReturnToTownAfterReward();

        Assert.That(session.ActiveRun, Is.Null);
        Assert.That(session.HasActiveExpeditionRun, Is.False);
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(expectedWalletEcho));
    }

    private static void SettleOneVictory(GameSessionState session)
    {
        session.MarkBattleResolved(
            victory: true,
            stepCount: 8,
            eventCount: 4,
            finalUnits: OneSurvivingHero());
        Assert.That(session.ApplyRewardChoice(0), Is.True);
        session.ReturnToTownAfterReward();
    }

    private static GameSessionState CreateSession()
    {
        var baseLookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var snapshot = baseLookup.Snapshot with
        {
            DropTables = CreateDropTables(),
        };
        var session = GameSessionTestFactory.Create(new FakeCombatContentLookup(snapshot: snapshot));
        session.BindProfile(new SaveProfile
        {
            ProfileId = "campaign-recovery",
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "Hero 1",
                    ArchetypeId = "vanguard_archetype",
                    RaceId = "human",
                    ClassId = "vanguard",
                    EquippedItemIds = new List<string>(),
                },
            },
            HeroProgressions = new List<HeroProgressionRecord>
            {
                new() { HeroId = "hero-1", Level = 1, Experience = 0 },
            },
            CampaignProgress = new CampaignProgressRecord
            {
                SelectedChapterId = "chapter_alpha",
                SelectedSiteId = "site_alpha_gate",
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static IReadOnlyDictionary<string, DropTableTemplate> CreateDropTables()
    {
        return new Dictionary<string, DropTableTemplate>(StringComparer.Ordinal)
        {
            ["drop.skirmish"] = BattleDropTable(
                "drop.skirmish",
                "reward_source_skirmish",
                "item_recovery_skirmish",
                8),
            ["drop.elite"] = BattleDropTable(
                "drop.elite",
                "reward_source_elite",
                "item_recovery_elite",
                12),
            ["drop.boss"] = BattleDropTable(
                "drop.boss",
                "reward_source_boss",
                "item_recovery_boss",
                16),
            ["drop.extract"] = new DropTableTemplate(
                "drop.extract",
                "reward_source_extract",
                new[]
                {
                    new LootBundleEntryTemplate(
                        "echo_extract",
                        RewardType.Echo,
                        40,
                        RarityBracketValue.Advanced,
                        1,
                        false,
                        Array.Empty<string>()),
                }),
        };
    }

    private static DropTableTemplate BattleDropTable(
        string id,
        string sourceId,
        string itemId,
        int guaranteedGold)
    {
        return new DropTableTemplate(
            id,
            sourceId,
            new[]
            {
                new LootBundleEntryTemplate(
                    $"gold_{sourceId}",
                    RewardType.Gold,
                    guaranteedGold,
                    RarityBracketValue.Common,
                    1,
                    true,
                    Array.Empty<string>()),
                new LootBundleEntryTemplate(
                    itemId,
                    RewardType.Item,
                    1,
                    RarityBracketValue.Common,
                    1,
                    false,
                    Array.Empty<string>()),
            });
    }

    private static IReadOnlyList<BattleUnitReadModel> OneSurvivingHero()
    {
        return new[]
        {
            new BattleUnitReadModel(
                Id: "ally_0_hero-1",
                Name: "Hero 1",
                Side: TeamSide.Ally,
                Anchor: DeploymentAnchorId.FrontCenter,
                RaceId: "human",
                ClassId: "vanguard",
                Position: new CombatVector2(0f, 0f),
                CurrentHealth: 100f,
                MaxHealth: 100f,
                IsAlive: true,
                ActionState: CombatActionState.AcquireTarget,
                PendingActionType: null,
                TargetId: null,
                TargetName: null,
                WindupProgress: 0f,
                CooldownRemaining: 0f,
                CurrentEnergy: 0f,
                MaxEnergy: 100f,
                IsDefending: false),
        };
    }
}
