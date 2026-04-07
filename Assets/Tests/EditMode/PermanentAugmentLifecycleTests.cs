using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PermanentAugmentLifecycleTests
{
    private static readonly FakeCombatContentLookup SharedLookup = CreateLookup();

    [Test]
    public void EquipPermanentAugment_RequiresUnlockedCandidate()
    {
        var session = CreateBoundSession();

        var result = session.EquipPermanentAugment("perm_aug_001");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("해금"));
    }

    [Test]
    public void EquipPermanentAugment_EquipsUnlockedCandidate()
    {
        var session = CreateBoundSession();
        session.UnlockPermanentAugmentCandidate("perm_aug_001");

        var result = session.EquipPermanentAugment("perm_aug_001");

        Assert.That(result.IsSuccess, Is.True, result.Error);
        var loadout = session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => record.BlueprintId == session.Profile.ActiveBlueprintId);
        Assert.That(loadout, Is.Not.Null);
        Assert.That(loadout!.EquippedAugmentIds, Is.EqualTo(new[] { "perm_aug_001" }));
    }

    [Test]
    public void EquipPermanentAugment_ReplacesExistingSelection_AndClampsToOne()
    {
        var session = CreateBoundSession();
        session.UnlockPermanentAugmentCandidate("perm_aug_001");
        session.UnlockPermanentAugmentCandidate("perm_aug_002");
        Assert.That(session.EquipPermanentAugment("perm_aug_001").IsSuccess, Is.True);

        var result = session.EquipPermanentAugment("perm_aug_002");

        Assert.That(result.IsSuccess, Is.True, result.Error);
        var loadout = session.Profile.PermanentAugmentLoadouts
            .First(record => record.BlueprintId == session.Profile.ActiveBlueprintId);
        Assert.That(loadout.EquippedAugmentIds, Has.Count.EqualTo(1));
        Assert.That(loadout.EquippedAugmentIds[0], Is.EqualTo("perm_aug_002"));
    }

    [Test]
    public void UnequipPermanentAugment_RemovesFromLoadout()
    {
        var session = CreateBoundSession();
        session.UnlockPermanentAugmentCandidate("perm_aug_001");
        Assert.That(session.EquipPermanentAugment("perm_aug_001").IsSuccess, Is.True);

        var result = session.UnequipPermanentAugment("perm_aug_001");

        Assert.That(result.IsSuccess, Is.True, result.Error);
        var loadout = session.Profile.PermanentAugmentLoadouts
            .First(record => record.BlueprintId == session.Profile.ActiveBlueprintId);
        Assert.That(loadout.EquippedAugmentIds, Is.Empty);
    }

    [Test]
    public void BindProfile_FiltersLegacySlotTokens_AndClampsLegacyLoadout()
    {
        var profile = CreateProfile();
        profile.UnlockedPermanentAugmentIds.AddRange(new[] { "perm-slot-1", "perm_aug_001", "perm_aug_001" });
        profile.PermanentAugmentLoadouts.Add(new PermanentAugmentLoadoutRecord
        {
            BlueprintId = profile.ActiveBlueprintId,
            EquippedAugmentIds = new List<string> { "perm-slot-2", "perm_aug_001", "perm_aug_002" }
        });

        var session = new GameSessionState(SharedLookup);
        session.BindProfile(profile);
        session.SetCurrentScene(SceneNames.Town);

        Assert.That(session.Profile.UnlockedPermanentAugmentIds, Is.EqualTo(new[] { "perm_aug_001", "perm_aug_002" }));
        var loadout = session.Profile.PermanentAugmentLoadouts.First(record => record.BlueprintId == profile.ActiveBlueprintId);
        Assert.That(loadout.EquippedAugmentIds, Has.Count.EqualTo(1));
        Assert.That(loadout.EquippedAugmentIds[0], Is.EqualTo("perm_aug_001"));
        Assert.That(session.PermanentAugmentSlotCount, Is.EqualTo(1));
    }

    private static GameSessionState CreateBoundSession()
    {
        var session = new GameSessionState(SharedLookup);
        session.BindProfile(CreateProfile());
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static SaveProfile CreateProfile()
    {
        return new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "Test Hero 1",
                    ArchetypeId = "test_archetype_a",
                    RaceId = "test_race",
                    ClassId = "test_class",
                    EquippedItemIds = new List<string>(),
                }
            }
        };
    }

    private static FakeCombatContentLookup CreateLookup()
    {
        var firstPlayableSlice = new SM.Meta.Model.FirstPlayableSliceDefinition
        {
            PermanentAugmentIds = new List<string> { "perm_aug_001", "perm_aug_002" }.AsReadOnly(),
            PermanentAugmentCap = 1,
        };
        var augments = new Dictionary<string, AugmentDefinition>
        {
            ["perm_aug_001"] = CreateAugment("perm_aug_001", "hunt_line"),
            ["perm_aug_002"] = CreateAugment("perm_aug_002", "ward_line"),
        };
        return new FakeCombatContentLookup(firstPlayableSlice: firstPlayableSlice, augments: augments);
    }

    private static AugmentDefinition CreateAugment(string id, string familyId)
    {
        var augment = ScriptableObject.CreateInstance<AugmentDefinition>();
        augment.Id = id;
        augment.IsPermanent = true;
        augment.FamilyId = familyId;
        augment.NameKey = id;
        return augment;
    }
}
