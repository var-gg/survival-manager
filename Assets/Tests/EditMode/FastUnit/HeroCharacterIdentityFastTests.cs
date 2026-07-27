using System;
using System.Collections.Generic;
using NUnit.Framework;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class HeroCharacterIdentityFastTests
{
    [Test]
    public void DisplayLabel_UsesAuthoredPersonAndJob_WithoutInstanceIdFallback()
    {
        var hero = new HeroInstanceRecord
        {
            HeroId = "hero-1",
            Name = "content.character.warden.name",
            CharacterId = "warden",
            ArchetypeId = "warden",
        };

        var label = HeroDisplayLabelFormatter.ResolvePersonAndJob(
            hero,
            (_, _) => "철위 (鐵衛)",
            _ => "감시자");

        Assert.That(label, Is.EqualTo("철위 (鐵衛) · 감시자"));
        Assert.That(label, Does.Not.Contain(hero.HeroId));
    }

    [Test]
    public void DisplayLabel_RejectsRawKeysAndStableIds()
    {
        var hero = new HeroInstanceRecord
        {
            HeroId = "hero-1",
            Name = "content.character.warden.name",
            CharacterId = "warden",
            ArchetypeId = "warden",
        };

        var label = HeroDisplayLabelFormatter.ResolvePersonAndJob(
            hero,
            (_, _) => "content.character.warden.name",
            _ => "warden");

        Assert.That(label, Is.EqualTo("—"));
    }

    [Test]
    public void DisplayLabel_DoesNotRepeatExactPersonJobAlias()
    {
        var hero = new HeroInstanceRecord
        {
            HeroId = "hero-penitent",
            Name = "content.character.bastion_penitent.name",
            CharacterId = "bastion_penitent",
            ArchetypeId = "bastion_penitent",
        };

        var label = HeroDisplayLabelFormatter.ResolvePersonAndJob(
            hero,
            (_, _) => "Bastion Penitent",
            _ => "Bastion Penitent");

        Assert.That(label, Is.EqualTo("Bastion Penitent"),
            "An exact localized person/job alias carries the same context without rendering a duplicate label.");
    }

    [Test]
    public void BindProfile_BackfillsHeadSaveCharacterIdentity()
    {
        var snapshot = EditorFreeCombatContentFixture.CreateSnapshot() with
        {
            Characters = new Dictionary<string, CharacterTemplate>(StringComparer.Ordinal)
            {
                ["warden"] = new(
                    "warden",
                    "human",
                    "vanguard",
                    "warden",
                    "role_vanguard_front",
                    DominantHand.Right),
            },
        };
        var session = GameSessionTestFactory.Create(new FakeCombatContentLookup(snapshot: snapshot));
        var profile = new SaveProfile
        {
            ProfileId = "head-save",
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "content.archetype.warden.name",
                    ArchetypeId = "warden",
                    RaceId = "human",
                    ClassId = "vanguard",
                },
            },
        };

        session.BindProfile(profile);

        Assert.That(profile.Heroes[0].CharacterId, Is.EqualTo("warden"));
        Assert.That(profile.Heroes[0].Name, Is.EqualTo("content.character.warden.name"));
        Assert.That(profile.Heroes[0].ArchetypeId, Is.EqualTo("warden"));
    }
}
