using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PermanentAugmentProgressionTests
{
    [Test]
    public void ResolvePendingUnlock_ReturnsSingleMatchingPermanentByFamily()
    {
        var definitions = new[]
        {
            CreateAugmentEntry("temp_hunt", false, "hunt_line"),
            CreateAugmentEntry("perm_hunt", true, "hunt_line"),
            CreateAugmentEntry("perm_ward", true, "ward_line"),
        };

        var result = PermanentAugmentProgressionService.ResolvePendingUnlock(
            "temp_hunt",
            definitions,
            new List<string>());

        Assert.That(result.HasUnlock, Is.True);
        Assert.That(result.UnlockAugmentId, Is.EqualTo("perm_hunt"));
        Assert.That(result.FamilyId, Is.EqualTo("hunt_line"));
    }

    [Test]
    public void ResolvePendingUnlock_ReturnsNone_WhenPermanentIsAlreadyKnown()
    {
        var definitions = new[]
        {
            CreateAugmentEntry("temp_hunt", false, "hunt_line"),
            CreateAugmentEntry("perm_hunt", true, "hunt_line"),
        };

        var result = PermanentAugmentProgressionService.ResolvePendingUnlock(
            "temp_hunt",
            definitions,
            new List<string> { "perm_hunt" });

        Assert.That(result.HasUnlock, Is.False);
        Assert.That(result.UnlockAugmentId, Is.Empty);
    }

    [Test]
    public void ApplyRewardChoice_FirstTemporaryAugmentUnlocksPermanentOnReturnToTown()
    {
        var session = CreateSession();
        session.BeginNewExpedition();
        session.SetLastBattleResult(victory: true, summary: "Victory");

        var temporaryChoiceIndex = session.PendingRewardChoices
            .ToList()
            .FindIndex(choice => choice.Kind == RewardChoiceKind.TemporaryAugment);
        Assert.That(temporaryChoiceIndex, Is.GreaterThanOrEqualTo(0), "임시 증강 보상 카드가 필요합니다.");

        var temporaryAugmentId = session.PendingRewardChoices[temporaryChoiceIndex].PayloadId;
        Assert.That(session.PreviewPermanentUnlockFromTemporaryAugment(temporaryAugmentId), Is.EqualTo("perm_hunt"));

        Assert.That(session.ApplyRewardChoice(temporaryChoiceIndex), Is.True);
        Assert.That(session.ActiveRun, Is.Not.Null);
        Assert.That(session.ActiveRun!.Overlay.FirstSelectedTemporaryAugmentId, Is.EqualTo(temporaryAugmentId));
        Assert.That(session.ActiveRun.Overlay.PendingPermanentUnlockId, Is.EqualTo("perm_hunt"));
        Assert.That(session.Profile.UnlockedPermanentAugmentIds, Does.Not.Contain("perm_hunt"));

        session.ReturnToTownAfterReward();

        Assert.That(session.Profile.UnlockedPermanentAugmentIds, Does.Contain("perm_hunt"));
        Assert.That(session.LastPermanentUnlockSummary.HasValue, Is.True);
    }

    private static GameSessionState CreateSession()
    {
        var firstPlayableSlice = new FirstPlayableSliceDefinition
        {
            TemporaryAugmentIds = new List<string> { "augment_gold_barrage" }.AsReadOnly(),
            PermanentAugmentIds = new List<string> { "perm_hunt" }.AsReadOnly(),
            PermanentAugmentCap = 1,
        };
        var augments = new Dictionary<string, AugmentDefinition>
        {
            ["augment_gold_barrage"] = CreateAugment("augment_gold_barrage", false, "hunt_line"),
            ["perm_hunt"] = CreateAugment("perm_hunt", true, "hunt_line"),
        };
        var lookup = new FakeCombatContentLookup(firstPlayableSlice: firstPlayableSlice, augments: augments);
        var session = new GameSessionState(lookup);
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

    private static AugmentDefinition CreateAugment(string id, bool isPermanent, string familyId)
    {
        var augment = ScriptableObject.CreateInstance<AugmentDefinition>();
        augment.Id = id;
        augment.IsPermanent = isPermanent;
        augment.FamilyId = familyId;
        augment.NameKey = id;
        return augment;
    }

    private static AugmentCatalogEntry CreateAugmentEntry(string id, bool isPermanent, string familyId)
    {
        return new AugmentCatalogEntry(
            id,
            "combat",
            familyId,
            1,
            isPermanent,
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new CombatModifierPackage(id, ModifierSource.Augment, Array.Empty<StatModifier>()));
    }
}
