using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Persistence.Abstractions.Models;
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
    private readonly ContentTextResolver? _contentText;
    private string _selectedCategoryKey = "weapon";  // default selection
    private string _selectedItemInstanceId = string.Empty;

    public InventoryPresenter(
        GameSessionRoot root,
        InventoryView view,
        SpriteLoader? currencySprite = null,
        SpriteLoader? affixSprite = null,
        ContentTextResolver? contentText = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _currencySprite = currencySprite ?? (_ => null);
        _affixSprite = affixSprite ?? (_ => null);
        _contentText = contentText;
    }

    public void Initialize()
    {
        _view.Bind(this);
        _view.BindClose(Close);
        Refresh();
    }

    public void Open()
    {
        _view.Open();
        Refresh();
    }

    public void Close()
    {
        _view.Close();
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
        // TODO Sprint 2: SessionState.EquipItem(heroId, itemInstanceId) — hero 타깃 선택 UI 필요.
    }

    // OnSellItem 제거: GameSessionState에 sell API 없음 (audit §4.1 P1-3). sell API 신설은 별도 task.

    void IInventoryActions.OnCompareItem(string itemInstanceId)
    {
        // TODO Sprint 2: SessionState 통한 compare는 UI-only.
    }

    private InventoryViewState BuildState()
    {
        var session = _root.SessionState;
        var gold = session.Profile.Currencies.Gold;
        var echo = session.Profile.Currencies.Echo;

        var equippedItemIds = new HashSet<string>(
            session.Profile.Heroes.SelectMany(h => h.EquippedItemIds ?? Enumerable.Empty<string>())
                                  .Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal);
        var lookup = _root.CombatContentLookup;

        var entries = session.Profile.Inventory
            .Select(item =>
            {
                var iconKey = item.ItemBaseId;
                var rarityKey = "common";
                var weaponFamilyKey = "item";
                var slotKey = "item";
                if (lookup.TryGetItemDefinition(item.ItemBaseId, out var itemDef))
                {
                    iconKey = string.IsNullOrWhiteSpace(itemDef.IconId) ? item.ItemBaseId : itemDef.IconId;
                    rarityKey = itemDef.RarityTier.ToString().ToLowerInvariant();
                    weaponFamilyKey = ResolveFamilyKey(itemDef);
                    slotKey = ResolveSlotKey(itemDef.SlotType);
                }

                var itemState = new InventoryItemViewState(
                    ItemInstanceId: item.ItemInstanceId,
                    IconKey: iconKey,
                    RarityKey: rarityKey,
                    WeaponFamilyKey: weaponFamilyKey,
                    WeaponFamilyLabel: WeaponFamilyLabels.TryGetValue(weaponFamilyKey, out var lbl) ? lbl : weaponFamilyKey,
                    IsEquipped: equippedItemIds.Contains(item.ItemInstanceId),
                    IsSelected: false,
                    IconSprite: _affixSprite(iconKey) ?? _affixSprite(item.ItemBaseId));
                return new InventoryItemPresentation(item, slotKey, iconKey, itemState);
            })
            .ToList();

        var filteredEntries = entries
            .Where(e => CategoryMatches(_selectedCategoryKey, e.SlotKey))
            .ToList();
        if (filteredEntries.Count == 0 && !string.Equals(_selectedCategoryKey, "all", StringComparison.Ordinal))
        {
            _selectedCategoryKey = "all";
            filteredEntries = entries;
        }

        if (filteredEntries.Count > 0 &&
            (string.IsNullOrEmpty(_selectedItemInstanceId) ||
             !filteredEntries.Any(e => string.Equals(e.Record.ItemInstanceId, _selectedItemInstanceId, StringComparison.Ordinal))))
        {
            _selectedItemInstanceId = filteredEntries[0].Record.ItemInstanceId;
        }

        var selectedEntry = filteredEntries
            .FirstOrDefault(e => string.Equals(e.Record.ItemInstanceId, _selectedItemInstanceId, StringComparison.Ordinal));
        var items = filteredEntries
            .Select(e => e.ViewState with
            {
                IsSelected = string.Equals(e.Record.ItemInstanceId, _selectedItemInstanceId, StringComparison.Ordinal)
            })
            .ToList();

        return new InventoryViewState(
            Gold: gold,
            Echo: echo,
            GoldSprite: _currencySprite("gold"),
            EchoSprite: _currencySprite("echo"),
            Categories: BuildCategories(entries),
            Items: items,
            Detail: selectedEntry.Record != null ? BuildDetail(selectedEntry.Record, selectedEntry.IconKey) : null);
    }

    private InventoryDetailViewState BuildDetail(InventoryItemRecord item, string iconKey)
    {
        var name = item.ItemBaseId;
        var slotLabel = "item";
        var rarityKey = "common";
        var weaponFamilyLabel = "item";
        var setBonusTier = string.Empty;
        var crossLinks = new List<string>();
        if (_root.CombatContentLookup.TryGetItemDefinition(item.ItemBaseId, out var itemDef))
        {
            name = _contentText?.GetItemName(item.ItemBaseId) ?? itemDef.LegacyDisplayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = itemDef.Id;
            }

            slotLabel = ResolveSlotKey(itemDef.SlotType);
            rarityKey = itemDef.RarityTier.ToString().ToLowerInvariant();
            var familyKey = ResolveFamilyKey(itemDef);
            weaponFamilyLabel = WeaponFamilyLabels.TryGetValue(familyKey, out var label) ? label : familyKey;
            setBonusTier = string.IsNullOrWhiteSpace(itemDef.BudgetBand)
                ? "set bonus schema pending"
                : $"budget {itemDef.BudgetBand}";
            crossLinks.Add(slotLabel);
            crossLinks.Add(weaponFamilyLabel);
            if (!string.IsNullOrWhiteSpace(itemDef.CraftCategory))
            {
                crossLinks.Add(itemDef.CraftCategory);
            }
        }

        var affixes = item.AffixIds
            .Select(affixId =>
            {
                var group = "prefix";
                var valueRange = "—";
                if (_root.CombatContentLookup.TryGetAffixDefinition(affixId, out var affixDef))
                {
                    group = affixDef.Tier.ToString().ToLowerInvariant();
                    valueRange = $"{affixDef.ValueMin:0.#} ~ {affixDef.ValueMax:0.#}";
                }

                return new InventoryAffixRowViewState(
                    GroupKey: group,
                    Name: _contentText?.GetAffixName(affixId) ?? affixId,
                    ValueRange: valueRange);
            })
            .ToList();

        return new InventoryDetailViewState(
            ItemInstanceId: item.ItemInstanceId,
            IconSprite: _affixSprite(iconKey) ?? _affixSprite(item.ItemBaseId),
            Affixes: affixes,
            Name: name,
            SlotLabel: slotLabel,
            RarityKey: rarityKey,
            WeaponFamilyLabel: weaponFamilyLabel,
            SetBonusTier: setBonusTier,
            CrossLinks: crossLinks);
    }

    private IReadOnlyList<InventoryCategoryViewState> BuildCategories(IReadOnlyList<InventoryItemPresentation> entries)
    {
        const int rosterCap = 300;
        return CategoryCatalog
            .Select(c => new InventoryCategoryViewState(
                Key: c.Key,
                Label: c.Label,
                Count: c.Key == "all"
                    ? $"{entries.Count}/{rosterCap}"
                    : $"{entries.Count(e => CategoryMatches(c.Key, e.SlotKey))}/100",
                IconSprite: _affixSprite(c.IconKey),
                IsSelected: string.Equals(c.Key, _selectedCategoryKey, StringComparison.Ordinal)))
            .ToList();
    }

    private readonly record struct InventoryItemPresentation(
        InventoryItemRecord Record,
        string SlotKey,
        string IconKey,
        InventoryItemViewState ViewState);

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

    private static bool CategoryMatches(string categoryKey, string slotKey) =>
        string.Equals(categoryKey, "all", StringComparison.Ordinal) ||
        string.Equals(categoryKey, slotKey, StringComparison.Ordinal);

    private static string ResolveSlotKey(ItemSlotType slot) => slot switch
    {
        ItemSlotType.Weapon => "weapon",
        ItemSlotType.Armor => "armor",
        ItemSlotType.Accessory => "accessory",
        _ => "item",
    };

    private static string ResolveFamilyKey(ItemBaseDefinition item)
    {
        return item.SlotType switch
        {
            SM.Content.Definitions.ItemSlotType.Weapon when !string.IsNullOrWhiteSpace(item.WeaponFamilyTag) => item.WeaponFamilyTag,
            SM.Content.Definitions.ItemSlotType.Weapon when item.Id.Contains("shield", StringComparison.Ordinal) => "shield",
            SM.Content.Definitions.ItemSlotType.Weapon when item.Id.Contains("bow", StringComparison.Ordinal) => "bow",
            SM.Content.Definitions.ItemSlotType.Weapon when item.Id.Contains("focus", StringComparison.Ordinal) || item.Id.Contains("bead", StringComparison.Ordinal) => "focus",
            SM.Content.Definitions.ItemSlotType.Weapon => "blade",
            SM.Content.Definitions.ItemSlotType.Armor => "armor",
            SM.Content.Definitions.ItemSlotType.Accessory => "accessory",
            _ => "item",
        };
    }
}
