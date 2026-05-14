using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Inventory V1 Presenter — `GameSessionRoot.SessionState.Profile` → InventoryViewState 변환.
///
/// Sprint 1 scaffold. Profile.Inventory + Profile.Currencies read는 wire. equip/sell/compare 액션은
/// Sprint 2에서 SessionState API 보강 후 wire.
///
/// 워크플로우: 사용자가 inventory에서 item equip → SessionState.EquipItem → BattleTest는 새 affix로 stat 계산.
/// </summary>
public sealed class InventoryPresenter : IInventoryActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    private readonly GameSessionRoot _root;
    private readonly InventoryView _view;
    private readonly SpriteLoader _currencySprite;
    private readonly SpriteLoader _affixSprite;
    private string _selectedCategoryKey = "weapon";  // default selection
    private string _selectedItemInstanceId = string.Empty;

    public InventoryPresenter(
        GameSessionRoot root,
        InventoryView view,
        SpriteLoader currencySprite,
        SpriteLoader affixSprite)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _currencySprite = currencySprite ?? throw new ArgumentNullException(nameof(currencySprite));
        _affixSprite = affixSprite ?? throw new ArgumentNullException(nameof(affixSprite));
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    void IInventoryActions.OnCategorySelected(string categoryKey)
    {
        _selectedCategoryKey = categoryKey;
        Refresh();
    }

    void IInventoryActions.OnItemSelected(string itemInstanceId)
    {
        _selectedItemInstanceId = itemInstanceId;
        Refresh();
    }

    void IInventoryActions.OnEquipItem(string itemInstanceId)
    {
        // TODO Sprint 2: SessionState.EquipItem(heroId, slotType, itemInstanceId) API 추가 후 wire.
    }

    void IInventoryActions.OnSellItem(string itemInstanceId)
    {
        // TODO Sprint 2: SessionState.SellItem or dismiss API.
    }

    void IInventoryActions.OnCompareItem(string itemInstanceId)
    {
        // TODO Sprint 2: SessionState 통한 compare는 UI-only.
    }

    private InventoryViewState BuildState()
    {
        var session = _root.SessionState;
        var gold = session.Profile.Currencies.Gold;
        var echo = session.Profile.Currencies.Echo;

        // Sprint 2: Profile.Inventory (InventoryItemRecord[]) → ViewState 기본 wire.
        // ItemBaseId / ItemInstanceId + equipped state read. rarity / weapon family / affix는 Sprint 3
        // (ItemDefinition lookup via CombatContentLookup 필요).
        var equippedItemIds = new HashSet<string>(
            session.Profile.Heroes.SelectMany(h => h.EquippedItemIds ?? Enumerable.Empty<string>())
                                  .Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal);

        var items = session.Profile.Inventory
            .Select(item => new InventoryItemViewState(
                ItemInstanceId: item.ItemInstanceId,
                IconKey: item.ItemBaseId,  // TODO Sprint 3: ItemDefinition.IconKey
                RarityKey: "common",       // TODO Sprint 3: ItemDefinition.Rarity
                WeaponFamilyKey: "blade",  // TODO Sprint 3: ItemDefinition.WeaponFamilyTag
                WeaponFamilyLabel: WeaponFamilyLabels.TryGetValue("blade", out var lbl) ? lbl : "blade",
                IsEquipped: equippedItemIds.Contains(item.ItemInstanceId),
                IconSprite: _affixSprite(item.ItemBaseId)))
            .ToList();

        return new InventoryViewState(
            Gold: gold,
            Echo: echo,
            GoldSprite: _currencySprite("gold"),
            EchoSprite: _currencySprite("echo"),
            Categories: BuildCategories(items.Count),
            Items: items,
            Detail: null);  // TODO Sprint 3: selected item detail
    }

    private IReadOnlyList<InventoryCategoryViewState> BuildCategories(int totalItems)
    {
        // V1: ALL + 3 equipment slot. Sprint 2: Profile.Inventory 집계 — ALL은 total, slot별 count는 Sprint 3.
        // weapon/armor/accessory split은 ItemDefinition.SlotType lookup 필요.
        const int rosterCap = 300;
        return CategoryCatalog
            .Select(c => new InventoryCategoryViewState(
                Key: c.Key,
                Label: c.Label,
                Count: c.Key == "all" ? $"{totalItems}/{rosterCap}" : "?/100",
                IconSprite: _affixSprite(c.IconKey),
                IsSelected: string.Equals(c.Key, _selectedCategoryKey, StringComparison.Ordinal)))
            .ToList();
    }

    private readonly record struct CategoryCatalogEntry(string Key, string Label, string IconKey);

    private static readonly CategoryCatalogEntry[] CategoryCatalog =
    {
        new("all",       "ALL",       "atk"),
        new("weapon",    "WEAPON",    "pierce"),
        new("armor",     "ARMOR",     "armor"),
        new("accessory", "ACCESSORY", "amplify"),
    };

    /// <summary>weapon family → 한국어 표시명. art-pipeline V1 weapon family 4종.</summary>
    public static readonly IReadOnlyDictionary<string, string> WeaponFamilyLabels = new Dictionary<string, string>
    {
        { "shield", "방패" },
        { "blade",  "검"  },
        { "bow",    "활"  },
        { "focus",  "매개체" },
    };

    public static IReadOnlyList<(string Key, string Label, string IconKey)> Categories
        => CategoryCatalog.Select(c => (c.Key, c.Label, c.IconKey)).ToList();
}
