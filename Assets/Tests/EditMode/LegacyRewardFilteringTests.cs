using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class LegacyRewardFilteringTests
{
    private static readonly HashSet<RewardChoiceKind> AllowedKinds = new()
    {
        RewardChoiceKind.Gold,
        RewardChoiceKind.Echo,
        RewardChoiceKind.Item,
        RewardChoiceKind.TemporaryAugment,
    };

    [Test]
    public void PendingRewardChoices_VictoryPath_StayWithinNormalLaneWhitelist()
    {
        var session = CreateSession();
        session.BeginNewExpedition();
        session.SetLastBattleResult(victory: true, summary: "Victory");

        Assert.That(session.PendingRewardChoices, Is.Not.Empty);
        Assert.That(session.PendingRewardChoices.All(choice => AllowedKinds.Contains(choice.Kind)), Is.True);
        Assert.That(session.PendingRewardChoices.Select(choice => choice.Kind), Has.None.EqualTo(RewardChoiceKind.PermanentAugmentSlot));
    }

    [Test]
    public void PendingRewardChoices_DefeatPath_StayWithinNormalLaneWhitelist()
    {
        var session = CreateSession();
        session.BeginNewExpedition();
        session.SetLastBattleResult(victory: false, summary: "Defeat");

        Assert.That(session.PendingRewardChoices, Is.Not.Empty);
        Assert.That(session.PendingRewardChoices.All(choice => AllowedKinds.Contains(choice.Kind)), Is.True);
        Assert.That(session.PendingRewardChoices.Select(choice => choice.Kind), Has.None.EqualTo(RewardChoiceKind.PermanentAugmentSlot));
    }

    private static GameSessionState CreateSession()
    {
        var session = GameSessionTestFactory.Create();
        session.BindProfile(new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "Test Hero",
                    ArchetypeId = "test_archetype",
                    RaceId = "test_race",
                    ClassId = "test_class",
                    EquippedItemIds = new List<string>(),
                }
            }
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }
}
