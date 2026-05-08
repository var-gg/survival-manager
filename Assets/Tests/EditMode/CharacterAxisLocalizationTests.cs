using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Battle;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class CharacterAxisLocalizationTests
{
    [Test]
    public void ContentTextResolver_ResolvesCharacterRoleAndRoleFamilyFallbacks()
    {
        var race = ScriptableObject.CreateInstance<RaceDefinition>();
        race.Id = "human";
        SetLegacyField(race, "legacyDisplayName", "Human");

        var @class = ScriptableObject.CreateInstance<ClassDefinition>();
        @class.Id = "duelist";
        SetLegacyField(@class, "legacyDisplayName", "Duelist");

        var archetype = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
        archetype.Id = "slayer";
        archetype.Race = race;
        archetype.Class = @class;
        SetLegacyField(archetype, "legacyDisplayName", "Slayer");

        var role = ScriptableObject.CreateInstance<RoleInstructionDefinition>();
        role.Id = "bruiser";
        role.RoleTag = "bruiser";
        SetLegacyField(role, "legacyDisplayName", "Bruiser");

        var character = ScriptableObject.CreateInstance<CharacterDefinition>();
        character.Id = "slayer";
        character.Race = race;
        character.Class = @class;
        character.DefaultArchetype = archetype;
        character.DefaultRoleInstruction = role;
        SetLegacyField(character, "legacyDisplayName", "Slayer Hero");

        var tactic = ScriptableObject.CreateInstance<TeamTacticDefinition>();
        tactic.Id = "team_tactic_standard_advance";
        SetLegacyField(tactic, "legacyDisplayName", "Standard Advance");

        var synergy = ScriptableObject.CreateInstance<SynergyDefinition>();
        synergy.Id = "synergy_duelist";
        SetLegacyField(synergy, "legacyDisplayName", "Duelist Bond");

        var lookup = new FakeCombatContentLookup(
            archetypes: new Dictionary<string, UnitArchetypeDefinition> { ["slayer"] = archetype },
            races: new Dictionary<string, RaceDefinition> { ["human"] = race },
            classes: new Dictionary<string, ClassDefinition> { ["duelist"] = @class },
            characters: new Dictionary<string, CharacterDefinition> { ["slayer"] = character },
            teamTactics: new Dictionary<string, TeamTacticDefinition> { ["team_tactic_standard_advance"] = tactic },
            synergies: new Dictionary<string, SynergyDefinition> { ["synergy_duelist"] = synergy },
            roleInstructions: new Dictionary<string, RoleInstructionDefinition> { ["bruiser"] = role });

        var go = new GameObject("LocalizationResolver");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var resolver = new ContentTextResolver(localization, lookup);

            Assert.That(resolver.GetCharacterName("slayer", "slayer"), Is.EqualTo("Slayer Hero"));
            Assert.That(resolver.GetRoleName("bruiser", "bruiser"), Is.EqualTo("Bruiser"));
            Assert.That(resolver.GetRoleFamilyName("duelist"), Is.EqualTo("Striker"));
            Assert.That(resolver.GetTeamTacticName("team_tactic_standard_advance"), Is.EqualTo("Standard Advance"));
            Assert.That(resolver.GetSynergyName("synergy_duelist"), Is.EqualTo("Duelist Bond"));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(race);
            Object.DestroyImmediate(@class);
            Object.DestroyImmediate(archetype);
            Object.DestroyImmediate(role);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(tactic);
            Object.DestroyImmediate(synergy);
        }
    }

    [Test]
    public void ContentTextResolver_UsesDefinedP09LabelForAppearanceOnlyCharacter()
    {
        var lookup = new FakeCombatContentLookup();
        var go = new GameObject("P09LabelResolver");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var resolver = new ContentTextResolver(localization, lookup);

            Assert.That(
                resolver.GetCharacterName("hero_dawn_priest", "priest"),
                Is.EqualTo("단린 (丹麟) / Dawn Priest"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BattleUnitMetadataFormatter_BuildsAxisSummaryFromCharacterHierarchy()
    {
        var race = ScriptableObject.CreateInstance<RaceDefinition>();
        race.Id = "human";
        SetLegacyField(race, "legacyDisplayName", "Human");

        var @class = ScriptableObject.CreateInstance<ClassDefinition>();
        @class.Id = "vanguard";
        SetLegacyField(@class, "legacyDisplayName", "Vanguard");

        var archetype = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
        archetype.Id = "warden";
        archetype.Race = race;
        archetype.Class = @class;
        SetLegacyField(archetype, "legacyDisplayName", "Warden");

        var role = ScriptableObject.CreateInstance<RoleInstructionDefinition>();
        role.Id = "anchor";
        role.RoleTag = "anchor";
        SetLegacyField(role, "legacyDisplayName", "Anchor");

        var character = ScriptableObject.CreateInstance<CharacterDefinition>();
        character.Id = "warden";
        character.Race = race;
        character.Class = @class;
        character.DefaultArchetype = archetype;
        character.DefaultRoleInstruction = role;
        SetLegacyField(character, "legacyDisplayName", "Warden Hero");

        var lookup = new FakeCombatContentLookup(
            archetypes: new Dictionary<string, UnitArchetypeDefinition> { ["warden"] = archetype },
            races: new Dictionary<string, RaceDefinition> { ["human"] = race },
            classes: new Dictionary<string, ClassDefinition> { ["vanguard"] = @class },
            characters: new Dictionary<string, CharacterDefinition> { ["warden"] = character },
            roleInstructions: new Dictionary<string, RoleInstructionDefinition> { ["anchor"] = role });

        var go = new GameObject("BattleMetadataFormatter");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var formatter = new BattleUnitMetadataFormatter(localization, lookup);
            var unit = new BattleUnitReadModel(
                Id: "ally.warden",
                Name: "Runtime Warden",
                Side: TeamSide.Ally,
                Anchor: DeploymentAnchorId.FrontCenter,
                RaceId: "human",
                ClassId: "vanguard",
                Position: new CombatVector2(0f, 0f),
                CurrentHealth: 10f,
                MaxHealth: 10f,
                IsAlive: true,
                ActionState: CombatActionState.Reposition,
                PendingActionType: null,
                TargetId: null,
                TargetName: null,
                WindupProgress: 0f,
                CooldownRemaining: 0f,
                CurrentEnergy: 0f,
                MaxEnergy: 100f,
                IsDefending: false,
                ArchetypeId: "warden",
                CharacterId: "warden",
                RoleInstructionId: "anchor",
                RoleTag: "anchor");

            var overhead = formatter.BuildOverhead(unit);
            var selected = formatter.BuildSelectedUnitPanel(unit);

            Assert.That(overhead.Header, Is.EqualTo("Warden Hero (Warden)"));
            Assert.That(overhead.Subtitle, Is.EqualTo("Human / Vanguard / Anchor"));
            Assert.That(selected.Body, Does.Contain("Character: Warden Hero"));
            Assert.That(selected.Body, Does.Contain("Role Family: Vanguard"));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(race);
            Object.DestroyImmediate(@class);
            Object.DestroyImmediate(archetype);
            Object.DestroyImmediate(role);
            Object.DestroyImmediate(character);
        }
    }

    [Test]
    public void GameSessionState_BindProfile_BackfillsMissingCharacterIdFromArchetype()
    {
        var race = ScriptableObject.CreateInstance<RaceDefinition>();
        race.Id = "human";

        var @class = ScriptableObject.CreateInstance<ClassDefinition>();
        @class.Id = "vanguard";

        var archetype = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
        archetype.Id = "warden";
        archetype.Race = race;
        archetype.Class = @class;

        var role = ScriptableObject.CreateInstance<RoleInstructionDefinition>();
        role.Id = "anchor";
        role.RoleTag = "anchor";

        var character = ScriptableObject.CreateInstance<CharacterDefinition>();
        character.Id = "warden";
        character.Race = race;
        character.Class = @class;
        character.DefaultArchetype = archetype;
        character.DefaultRoleInstruction = role;

        var lookup = new FakeCombatContentLookup(
            archetypes: new Dictionary<string, UnitArchetypeDefinition> { ["warden"] = archetype },
            races: new Dictionary<string, RaceDefinition> { ["human"] = race },
            classes: new Dictionary<string, ClassDefinition> { ["vanguard"] = @class },
            characters: new Dictionary<string, CharacterDefinition> { ["warden"] = character },
            roleInstructions: new Dictionary<string, RoleInstructionDefinition> { ["anchor"] = role });

        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "Hero",
                    ArchetypeId = "warden",
                    RaceId = string.Empty,
                    ClassId = string.Empty,
                    CharacterId = string.Empty,
                    EquippedItemIds = new List<string>(),
                }
            }
        });

        Assert.That(session.Profile.Heroes[0].CharacterId, Is.EqualTo("warden"));
        Assert.That(session.Profile.Heroes[0].RaceId, Is.EqualTo("human"));
        Assert.That(session.Profile.Heroes[0].ClassId, Is.EqualTo("vanguard"));

        Object.DestroyImmediate(race);
        Object.DestroyImmediate(@class);
        Object.DestroyImmediate(archetype);
        Object.DestroyImmediate(role);
        Object.DestroyImmediate(character);
    }

    private static void SetLegacyField(Object asset, string fieldName, string value)
    {
        asset.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(asset, value);
    }
}
