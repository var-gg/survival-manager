using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Town;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class TownCharacterSheetFormatterTests
{
    [Test]
    public void TownCharacterSheetFormatter_BuildsStableFivePanelSheetForSelectedHero()
    {
        var fixture = CreateFixture();
        var go = new GameObject("TownCharacterSheetFormatter");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var resolver = new ContentTextResolver(localization, fixture.Lookup);
            var formatter = new TownCharacterSheetFormatter(localization, resolver, fixture.Lookup);
            var session = CreateSession(fixture.Lookup);
            session.SetTeamPosture(TeamPostureType.HoldLine);
            session.SetTeamTactic("team_tactic_hold_line");

            var selectedHero = session.Profile.Heroes[0];
            var selectedItem = session.Profile.Inventory[0];
            var selectedNode = fixture.PassiveNode;
            var dismissRefund = new DismissRefundResult(3, 7);

            var sheet = formatter.Build(session, selectedHero, selectedItem, selectedNode, 40, 30, 60, dismissRefund);

            Assert.That(sheet.Overview.Title, Is.EqualTo("Overview"));
            Assert.That(sheet.Overview.Body, Does.Contain("Warden Hero (Iron Warden)"));
            Assert.That(sheet.Overview.Body, Does.Contain("Tactic: Hold Line"));

            Assert.That(sheet.Loadout.Body, Does.Contain("Weapon: Guardian Shield"));
            Assert.That(sheet.Loadout.Body, Does.Contain("Signature Active: Power Strike"));
            Assert.That(sheet.Loadout.Body, Does.Contain("Flex Passive: Anchored"));

            Assert.That(sheet.Passives.Body, Does.Contain("Board: Vanguard Board"));
            Assert.That(sheet.Passives.Body, Does.Contain("Node Count: 1/5"));

            Assert.That(sheet.Synergy.Body, Does.Contain("Human Bond: 2 units (2/4) 2 reached, next 4"));
            Assert.That(sheet.Synergy.Body, Does.Contain("Vanguard Oath: 2 units (2/3) 2 reached, next 3"));

            Assert.That(sheet.Progression.Body, Does.Contain("Blueprint Permanent: Legacy Oath"));
            Assert.That(sheet.Progression.Body, Does.Contain("Passive Progress: 1 active / 5 max / 2 unlocked"));
        }
        finally
        {
            fixture.Dispose();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TownCharacterSheetFormatter_UsesConsistentEmptyStateWhenHeroIsMissing()
    {
        var fixture = CreateFixture();
        var go = new GameObject("TownCharacterSheetFormatterEmpty");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var resolver = new ContentTextResolver(localization, fixture.Lookup);
            var formatter = new TownCharacterSheetFormatter(localization, resolver, fixture.Lookup);
            var session = CreateSession(fixture.Lookup);

            var sheet = formatter.Build(session, null, null, null, 0, 0, 0, new DismissRefundResult(0, 0));

            Assert.That(sheet.Overview.Body, Is.EqualTo(sheet.Loadout.Body));
            Assert.That(sheet.Overview.Body, Is.EqualTo(sheet.Passives.Body));
            Assert.That(sheet.Overview.Body, Is.EqualTo(sheet.Synergy.Body));
            Assert.That(sheet.Overview.Body, Is.EqualTo(sheet.Progression.Body));
        }
        finally
        {
            fixture.Dispose();
            Object.DestroyImmediate(go);
        }
    }

    private static GameSessionState CreateSession(ICombatContentLookup lookup)
    {
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile
        {
            ActiveBlueprintId = "blueprint.main",
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "Warden Runtime",
                    CharacterId = "warden",
                    ArchetypeId = "warden",
                    RaceId = "human",
                    ClassId = "vanguard",
                    FlexActiveId = "skill_warden_utility",
                    FlexPassiveId = "skill_vanguard_support_1",
                    EquippedItemIds = new List<string> { "item-instance-weapon", "item-instance-armor", "item-instance-accessory" },
                    RetrainState = new UnitRetrainState { RetrainCount = 1, TotalEchoSpent = 30 },
                },
                new()
                {
                    HeroId = "hero-2",
                    Name = "Guardian Runtime",
                    CharacterId = "guardian",
                    ArchetypeId = "guardian",
                    RaceId = "human",
                    ClassId = "vanguard",
                    EquippedItemIds = new List<string>(),
                },
            },
            Inventory = new List<InventoryItemRecord>
            {
                new() { ItemInstanceId = "item-instance-weapon", ItemBaseId = "item_guardian_shield", EquippedHeroId = "hero-1" },
                new() { ItemInstanceId = "item-instance-armor", ItemBaseId = "item_warden_armor", EquippedHeroId = "hero-1" },
                new() { ItemInstanceId = "item-instance-accessory", ItemBaseId = "item_warden_trinket", EquippedHeroId = "hero-1" },
            },
            HeroLoadouts = new List<HeroLoadoutRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    PassiveBoardId = "board_vanguard",
                    SelectedPassiveNodeIds = new List<string> { "passive_vanguard_small_01" },
                },
            },
            HeroProgressions = new List<HeroProgressionRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    UnlockedPassiveNodeIds = new List<string> { "passive_vanguard_small_01", "passive_vanguard_small_02" },
                },
            },
            UnlockedPermanentAugmentIds = new List<string> { "augment_perm_legacy_oath" },
            PermanentAugmentLoadouts = new List<PermanentAugmentLoadoutRecord>
            {
                new()
                {
                    BlueprintId = "blueprint.main",
                    EquippedAugmentIds = new List<string> { "augment_perm_legacy_oath" },
                },
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static Fixture CreateFixture()
    {
        var race = ScriptableObject.CreateInstance<RaceDefinition>();
        race.Id = "human";
        SetLegacyField(race, "legacyDisplayName", "Human");

        var @class = ScriptableObject.CreateInstance<ClassDefinition>();
        @class.Id = "vanguard";
        SetLegacyField(@class, "legacyDisplayName", "Vanguard");

        var warden = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
        warden.Id = "warden";
        warden.Race = race;
        warden.Class = @class;
        warden.RoleTag = "anchor";
        SetLegacyField(warden, "legacyDisplayName", "Iron Warden");

        var guardian = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
        guardian.Id = "guardian";
        guardian.Race = race;
        guardian.Class = @class;
        guardian.RoleTag = "anchor";
        SetLegacyField(guardian, "legacyDisplayName", "Crypt Guardian");

        var character = ScriptableObject.CreateInstance<CharacterDefinition>();
        character.Id = "warden";
        character.Race = race;
        character.Class = @class;
        character.DefaultArchetype = warden;
        SetLegacyField(character, "legacyDisplayName", "Warden Hero");

        var guardianCharacter = ScriptableObject.CreateInstance<CharacterDefinition>();
        guardianCharacter.Id = "guardian";
        guardianCharacter.Race = race;
        guardianCharacter.Class = @class;
        guardianCharacter.DefaultArchetype = guardian;
        SetLegacyField(guardianCharacter, "legacyDisplayName", "Guardian Hero");

        var role = ScriptableObject.CreateInstance<RoleInstructionDefinition>();
        role.Id = "anchor";
        role.RoleTag = "anchor";
        SetLegacyField(role, "legacyDisplayName", "Anchor");

        var tactic = ScriptableObject.CreateInstance<TeamTacticDefinition>();
        tactic.Id = "team_tactic_hold_line";
        SetLegacyField(tactic, "legacyDisplayName", "Hold Line");

        var synergyHuman = ScriptableObject.CreateInstance<SynergyDefinition>();
        synergyHuman.Id = "synergy_human";
        SetLegacyField(synergyHuman, "legacyDisplayName", "Human Bond");

        var synergyVanguard = ScriptableObject.CreateInstance<SynergyDefinition>();
        synergyVanguard.Id = "synergy_vanguard";
        SetLegacyField(synergyVanguard, "legacyDisplayName", "Vanguard Oath");

        var signatureActive = CreateSkill("skill_power_strike", "Power Strike");
        var flexActive = CreateSkill("skill_warden_utility", "Shield Bash");
        var flexPassive = CreateSkill("skill_vanguard_support_1", "Anchored");
        var signaturePassive = CreateSkill("skill_vanguard_passive_1", "Vanguard's Resolve");

        var board = ScriptableObject.CreateInstance<PassiveBoardDefinition>();
        board.Id = "board_vanguard";
        SetLegacyField(board, "legacyDisplayName", "Vanguard Board");

        var node = ScriptableObject.CreateInstance<PassiveNodeDefinition>();
        node.Id = "passive_vanguard_small_01";
        node.BoardId = "board_vanguard";
        SetLegacyField(node, "legacyDisplayName", "Shield Wall");
        board.Nodes = new List<PassiveNodeDefinition> { node };

        var weapon = CreateItem("item_guardian_shield", ItemSlotType.Weapon, "Guardian Shield");
        var armor = CreateItem("item_warden_armor", ItemSlotType.Armor, "Warden Armor");
        var accessory = CreateItem("item_warden_trinket", ItemSlotType.Accessory, "Warden Trinket");

        var permanent = ScriptableObject.CreateInstance<AugmentDefinition>();
        permanent.Id = "augment_perm_legacy_oath";
        permanent.IsPermanent = true;
        SetLegacyField(permanent, "legacyDisplayName", "Legacy Oath");

        var slice = new FirstPlayableSliceDefinition
        {
            SynergyGrammar = new List<SynergyGrammarEntry>
            {
                new() { FamilyId = "synergy_human", MinorThreshold = 2, MajorThreshold = 4 },
                new() { FamilyId = "synergy_vanguard", MinorThreshold = 2, MajorThreshold = 3 },
            }.AsReadOnly(),
        };

        return new Fixture(
            new FakeCombatContentLookup(
                firstPlayableSlice: slice,
                archetypes: new Dictionary<string, UnitArchetypeDefinition>
                {
                    ["warden"] = warden,
                    ["guardian"] = guardian,
                },
                races: new Dictionary<string, RaceDefinition> { ["human"] = race },
                classes: new Dictionary<string, ClassDefinition> { ["vanguard"] = @class },
                characters: new Dictionary<string, CharacterDefinition>
                {
                    ["warden"] = character,
                    ["guardian"] = guardianCharacter,
                },
                teamTactics: new Dictionary<string, TeamTacticDefinition> { ["team_tactic_hold_line"] = tactic },
                synergies: new Dictionary<string, SynergyDefinition>
                {
                    ["synergy_human"] = synergyHuman,
                    ["synergy_vanguard"] = synergyVanguard,
                },
                roleInstructions: new Dictionary<string, RoleInstructionDefinition> { ["anchor"] = role },
                skills: new Dictionary<string, SkillDefinitionAsset>
                {
                    ["skill_power_strike"] = signatureActive,
                    ["skill_warden_utility"] = flexActive,
                    ["skill_vanguard_support_1"] = flexPassive,
                    ["skill_vanguard_passive_1"] = signaturePassive,
                },
                passiveBoards: new Dictionary<string, PassiveBoardDefinition> { ["board_vanguard"] = board },
                passiveNodes: new Dictionary<string, PassiveNodeDefinition> { ["passive_vanguard_small_01"] = node },
                items: new Dictionary<string, ItemBaseDefinition>
                {
                    ["item_guardian_shield"] = weapon,
                    ["item_warden_armor"] = armor,
                    ["item_warden_trinket"] = accessory,
                },
                augments: new Dictionary<string, AugmentDefinition> { ["augment_perm_legacy_oath"] = permanent }),
            node,
            race,
            @class,
            warden,
            guardian,
            character,
            guardianCharacter,
            role,
            tactic,
            synergyHuman,
            synergyVanguard,
            signatureActive,
            flexActive,
            flexPassive,
            signaturePassive,
            board,
            node,
            weapon,
            armor,
            accessory,
            permanent);
    }

    private static SkillDefinitionAsset CreateSkill(string id, string legacyDisplayName)
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
        skill.Id = id;
        SetLegacyField(skill, "legacyDisplayName", legacyDisplayName);
        return skill;
    }

    private static ItemBaseDefinition CreateItem(string id, ItemSlotType slotType, string legacyDisplayName)
    {
        var item = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        item.Id = id;
        item.SlotType = slotType;
        SetLegacyField(item, "legacyDisplayName", legacyDisplayName);
        return item;
    }

    private static void SetLegacyField(Object asset, string fieldName, string value)
    {
        asset.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(asset, value);
    }

    private sealed class Fixture
    {
        private readonly Object[] _assets;

        public Fixture(FakeCombatContentLookup lookup, PassiveNodeDefinition passiveNode, params Object[] assets)
        {
            Lookup = lookup;
            PassiveNode = passiveNode;
            _assets = assets;
        }

        public FakeCombatContentLookup Lookup { get; }
        public PassiveNodeDefinition PassiveNode { get; }

        public void Dispose()
        {
            foreach (var asset in _assets)
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
