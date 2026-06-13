using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AugmentOfferScheduleSessionFastTests
{
    [Test]
    public void ScheduledEncounter_PresentsAugmentOfferAndReloadRestoresPendingChoices()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4);

        var originalPendingIds = session.PendingRewardChoices.Select(choice => choice.PayloadId).ToArray();
        Assert.That(session.HasPendingRewardSettlement, Is.True);
        Assert.That(session.PendingRewardChoices.Select(choice => choice.Kind), Is.All.EqualTo(RewardChoiceKind.TemporaryAugment));
        Assert.That(originalPendingIds, Has.Length.EqualTo(3));

        var reloaded = GameSessionTestFactory.Create(lookup);
        reloaded.BindProfile(CloneProfile(session.Profile));
        reloaded.SetCurrentScene(SceneNames.Town);

        Assert.That(reloaded.HasPendingRewardSettlement, Is.True);
        Assert.That(reloaded.PendingRewardChoices.Select(choice => choice.Kind), Is.All.EqualTo(RewardChoiceKind.TemporaryAugment));
        Assert.That(reloaded.PendingRewardChoices.Select(choice => choice.PayloadId), Is.EqualTo(originalPendingIds),
            "pending reward resume는 같은 run context와 seed에서 같은 augment offer를 재생성해야 한다.");
    }

    private static GameSessionState CreateBoundSession(ICombatContentLookup lookup)
    {
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "Hero One", "vanguard"),
                CreateHero("hero-2", "Hero Two", "ranger"),
                CreateHero("hero-3", "Hero Three", "duelist"),
                CreateHero("hero-4", "Hero Four", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static SaveProfile CloneProfile(SaveProfile profile)
    {
        var root = Path.Combine(Path.GetTempPath(), "SM_AugmentOfferScheduleSessionFastTests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonSaveRepository(root);
            repository.Save(profile);
            return repository.LoadOrCreate(profile.ProfileId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static HeroInstanceRecord CreateHero(string heroId, string name, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = name,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            CurrentHp = 100,
            MaxHp = 100,
            EquippedItemIds = new List<string>(),
        };
    }
}
