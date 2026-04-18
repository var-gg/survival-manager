using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.Sandbox;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class TownBuildHotPathTests
{
    // FakeCombatContentLookup은 Resources.LoadAll을 호출하지 않으므로
    // GUI 모드에서도 에디터 freeze 없이 빠르게 실행된다.
    private static readonly FakeCombatContentLookup SharedLookup = CreateSharedLookup();

    // ──────────────────────────────────────────────
    // EquipItem / UnequipItem
    // ──────────────────────────────────────────────

    [Test]
    public void EquipItem_Success_SetsEquippedHeroIdAndHeroList()
    {
        var session = CreateBoundSession();
        var hero = session.Profile.Heroes[0];
        var item = AddInventoryItem(session, "item_001", "base_sword");

        var result = session.EquipItem(hero.HeroId, item.ItemInstanceId);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(item.EquippedHeroId, Is.EqualTo(hero.HeroId));
        Assert.That(hero.EquippedItemIds, Does.Contain(item.ItemInstanceId));
    }

    [Test]
    public void UnequipItem_Success_ClearsEquippedState()
    {
        var session = CreateBoundSession();
        var hero = session.Profile.Heroes[0];
        var item = AddInventoryItem(session, "item_002", "base_sword");
        session.EquipItem(hero.HeroId, item.ItemInstanceId);

        var result = session.UnequipItem(hero.HeroId, item.ItemInstanceId);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(item.EquippedHeroId, Is.Empty);
        Assert.That(hero.EquippedItemIds, Does.Not.Contain(item.ItemInstanceId));
    }

    [Test]
    public void EquipItem_AlreadyEquippedByOtherHero_Fails()
    {
        var session = CreateBoundSession();
        var hero1 = session.Profile.Heroes[0];
        var hero2 = session.Profile.Heroes[1];
        var item = AddInventoryItem(session, "item_003", "base_sword");
        session.EquipItem(hero1.HeroId, item.ItemInstanceId);

        var result = session.EquipItem(hero2.HeroId, item.ItemInstanceId);

        Assert.That(result.IsSuccess, Is.False, "다른 유닛에 장착된 아이템은 장착 불가");
    }

    [Test]
    public void EquipItem_NonexistentItem_Fails()
    {
        var session = CreateBoundSession();
        var hero = session.Profile.Heroes[0];

        var result = session.EquipItem(hero.HeroId, "nonexistent_item");

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void EquipItem_NonexistentHero_Fails()
    {
        var session = CreateBoundSession();
        var item = AddInventoryItem(session, "item_004", "base_sword");

        var result = session.EquipItem("nonexistent_hero", item.ItemInstanceId);

        Assert.That(result.IsSuccess, Is.False);
    }

    // ──────────────────────────────────────────────
    // RefitItem
    // ──────────────────────────────────────────────

    [Test]
    public void RefitItem_Success_ChangesAffixAndDeductsEcho()
    {
        // RefitItem uses GetFirstPlayableSlice() for available affixes.
        // Provide a slice with test affix IDs.
        var slice = new FirstPlayableSliceDefinition
        {
            AffixIds = new List<string> { "affix_a", "affix_b", "affix_c", "affix_d" }.AsReadOnly()
        };
        var lookup = new FakeCombatContentLookup(firstPlayableSlice: slice);
        var session = CreateBoundSession(lookup);
        var item = AddInventoryItem(session, "item_010", "base_sword", new List<string> { "affix_a", "affix_b" });
        session.Profile.Currencies.Echo = 100;
        var echoBefore = session.Profile.Currencies.Echo;

        var result = session.RefitItem(item.ItemInstanceId, 0);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(session.Profile.Currencies.Echo, Is.LessThan(echoBefore), "Echo가 차감되어야 함");
    }

    [Test]
    public void RefitItem_InsufficientEcho_Fails()
    {
        var session = CreateBoundSession();
        var item = AddInventoryItem(session, "item_011", "base_sword", new List<string> { "affix_a" });
        session.Profile.Currencies.Echo = 0;

        var result = session.RefitItem(item.ItemInstanceId, 0);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("Echo"));
    }

    // ──────────────────────────────────────────────
    // SelectPassiveBoard / TogglePassiveNode
    // ──────────────────────────────────────────────

    [Test]
    public void SelectPassiveBoard_CreatesLoadoutRecord()
    {
        var session = CreateBoundSession();
        var hero = session.Profile.Heroes[0];

        var result = session.SelectPassiveBoard(hero.HeroId, "board_vanguard");

        Assert.That(result.IsSuccess, Is.True, result.Error);

        var loadout = session.Profile.HeroLoadouts.FirstOrDefault(
            r => r.HeroId == hero.HeroId);
        Assert.That(loadout, Is.Not.Null, "HeroLoadoutRecord가 생성되어야 함");
        Assert.That(loadout!.PassiveBoardId, Is.EqualTo("board_vanguard"));

        var selection = session.Profile.PassiveSelections.FirstOrDefault(
            s => s.HeroId == hero.HeroId);
        Assert.That(selection, Is.Not.Null, "PassiveSelectionRecord가 생성되어야 함");
        Assert.That(selection!.BoardId, Is.EqualTo("board_vanguard"));
    }

    [Test]
    public void TogglePassiveNode_AddsAndRemovesNode()
    {
        var session = CreateBoundSession();
        var hero = session.Profile.Heroes[0];
        session.SelectPassiveBoard(hero.HeroId, "board_vanguard");

        // Toggle ON
        var onResult = session.TogglePassiveNode(hero.HeroId, "node_1");
        Assert.That(onResult.IsSuccess, Is.True, onResult.Error);

        var loadout = session.Profile.HeroLoadouts.First(r => r.HeroId == hero.HeroId);
        Assert.That(loadout.SelectedPassiveNodeIds, Does.Contain("node_1"));

        // Toggle OFF
        var offResult = session.TogglePassiveNode(hero.HeroId, "node_1");
        Assert.That(offResult.IsSuccess, Is.True, offResult.Error);
        Assert.That(loadout.SelectedPassiveNodeIds, Does.Not.Contain("node_1"));
    }

    [Test]
    public void TogglePassiveNode_WithoutBoard_Fails()
    {
        var session = CreateBoundSession();
        var hero = session.Profile.Heroes[0];
        // No SelectPassiveBoard called

        var result = session.TogglePassiveNode(hero.HeroId, "node_1");

        Assert.That(result.IsSuccess, Is.False, "보드를 선택하지 않으면 노드 토글 불가");
    }

    [Test]
    public void TogglePassiveNode_SyncsToPassiveSelectionRecord()
    {
        var session = CreateBoundSession();
        var hero = session.Profile.Heroes[0];
        session.SelectPassiveBoard(hero.HeroId, "board_vanguard");

        session.TogglePassiveNode(hero.HeroId, "node_a");
        session.TogglePassiveNode(hero.HeroId, "node_b");

        var selection = session.Profile.PassiveSelections.First(s => s.HeroId == hero.HeroId);
        Assert.That(selection.SelectedNodeIds, Does.Contain("node_a"));
        Assert.That(selection.SelectedNodeIds, Does.Contain("node_b"));
    }

    [Test]
    public void PrepareQuickBattleSmoke_UsesConfiguredAllySlots_WhenPresent()
    {
        var session = CreateBoundSession();
        var config = ScriptableObject.CreateInstance<CombatSandboxConfig>();
        try
        {
            config.AllySlots = new List<CombatSandboxAllySlot>
            {
                new() { HeroId = "hero-2", Anchor = DeploymentAnchorId.BackBottom },
                new() { HeroId = "hero-1", Anchor = DeploymentAnchorId.FrontTop },
            };

            session.PrepareQuickBattleSmoke(config);

            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.FrontTop), Is.EqualTo("hero-1"));
            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.BackBottom), Is.EqualTo("hero-2"));
            Assert.That(session.BattleDeployHeroIds, Is.EqualTo(new[] { "hero-1", "hero-2" }));
            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.FrontCenter), Is.Null);
            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.BackCenter), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    // ── helpers ──

    private static GameSessionState CreateBoundSession(ICombatContentLookup? lookup = null)
    {
        var session = new GameSessionState(lookup ?? SharedLookup);
        // Pre-populate heroes to skip SeedDemoProfile() which requires real content data.
        var profile = new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1", Name = "Test Hero 1",
                    ArchetypeId = "test_archetype_a", RaceId = "test_race", ClassId = "test_class",
                    EquippedItemIds = new List<string>(),
                },
                new()
                {
                    HeroId = "hero-2", Name = "Test Hero 2",
                    ArchetypeId = "test_archetype_b", RaceId = "test_race", ClassId = "test_class",
                    EquippedItemIds = new List<string>(),
                },
            },
        };
        session.BindProfile(profile);
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static FakeCombatContentLookup CreateSharedLookup()
    {
        var board = CreatePassiveBoard(
            "board_vanguard",
            "test_class",
            CreatePassiveNode("node_1", "board_vanguard", 0),
            CreatePassiveNode("node_a", "board_vanguard", 1),
            CreatePassiveNode("node_b", "board_vanguard", 2));
        var passiveBoards = new Dictionary<string, PassiveBoardDefinition>
        {
            [board.Id] = board,
        };
        var passiveNodes = board.Nodes.ToDictionary(node => node.Id, node => node, StringComparer.Ordinal);
        var firstPlayableSlice = new FirstPlayableSliceDefinition
        {
            AffixIds = new List<string> { "affix_a", "affix_b", "affix_c", "affix_d" }.AsReadOnly(),
            PassiveBoardIds = new List<string> { board.Id }.AsReadOnly(),
        };

        return new FakeCombatContentLookup(
            firstPlayableSlice: firstPlayableSlice,
            passiveBoards: passiveBoards,
            passiveNodes: passiveNodes);
    }

    private static PassiveBoardDefinition CreatePassiveBoard(string id, string classId, params PassiveNodeDefinition[] nodes)
    {
        var board = ScriptableObject.CreateInstance<PassiveBoardDefinition>();
        board.Id = id;
        board.ClassId = classId;
        board.NameKey = id;
        board.Nodes = nodes.ToList();
        return board;
    }

    private static PassiveNodeDefinition CreatePassiveNode(string id, string boardId, int depth)
    {
        var node = ScriptableObject.CreateInstance<PassiveNodeDefinition>();
        node.Id = id;
        node.BoardId = boardId;
        node.BoardDepth = depth;
        node.NameKey = id;
        return node;
    }

    private static InventoryItemRecord AddInventoryItem(
        GameSessionState session,
        string instanceId,
        string baseId,
        List<string>? affixIds = null)
    {
        var item = new InventoryItemRecord
        {
            ItemInstanceId = instanceId,
            ItemBaseId = baseId,
            AffixIds = affixIds ?? new List<string>(),
        };
        session.Profile.Inventory.Add(item);
        return item;
    }
}
