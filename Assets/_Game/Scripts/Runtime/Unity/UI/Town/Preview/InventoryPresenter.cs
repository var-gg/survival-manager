using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Results;
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
    private readonly SpriteLoader _itemIconSprite;
    private readonly SpriteLoader _affixIconSprite;
    private readonly ContentTextResolver? _contentText;
    private string _selectedCategoryKey = "weapon";  // default selection
    private string _selectedItemInstanceId = string.Empty;
    private string _targetHeroId = string.Empty;
    private string _lastEquipStatus = string.Empty;

    public InventoryPresenter(
        GameSessionRoot root,
        InventoryView view,
        SpriteLoader? currencySprite = null,
        SpriteLoader? itemIconSprite = null,
        ContentTextResolver? contentText = null,
        SpriteLoader? affixIconSprite = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _currencySprite = currencySprite ?? (_ => null);
        _itemIconSprite = itemIconSprite ?? (_ => null);
        _affixIconSprite = affixIconSprite ?? itemIconSprite ?? (_ => null);
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

    public void SetTargetHero(string heroId)
    {
        _targetHeroId = heroId ?? string.Empty;
        _lastEquipStatus = string.Empty;
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
        if (string.IsNullOrWhiteSpace(_targetHeroId))
        {
            _lastEquipStatus = "영웅을 먼저 선택하면 장착할 수 있습니다.";
            Refresh();
            return;
        }

        var result = _root.SessionState.EquipItem(_targetHeroId, itemInstanceId);
        _lastEquipStatus = result.IsSuccess
            ? "장착 완료"
            : result.Error ?? "장착 실패";
        Refresh();
    }

    // OnSellItem 제거: GameSessionState에 sell API 없음 (audit §4.1 P1-3). sell API 신설은 별도 task.

    void IInventoryActions.OnCompareItem(string itemInstanceId)
    {
        _selectedItemInstanceId = itemInstanceId;
        Refresh();
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
                var weaponFamilyKey = "item";
                var slotKey = "item";
                var presentation = EquipmentPresentationPolicy.Build(slotKey, weaponFamilyKey, "Common", "Baseline", Array.Empty<string>());
                if (lookup.TryGetItemDefinition(item.ItemBaseId, out var itemDef))
                {
                    iconKey = string.IsNullOrWhiteSpace(itemDef.IconId) ? item.ItemBaseId : itemDef.IconId;
                    weaponFamilyKey = ResolveFamilyKey(itemDef);
                    slotKey = ResolveSlotKey(itemDef.SlotType);
                    presentation = EquipmentPresentationPolicy.Build(
                        slotKey,
                        weaponFamilyKey,
                        itemDef.RarityTier.ToString(),
                        itemDef.IdentityKind.ToString(),
                        EnumerateCraftOperations(itemDef));
                }

                var itemState = new InventoryItemViewState(
                    ItemInstanceId: item.ItemInstanceId,
                    IconKey: iconKey,
                    RarityKey: presentation.RarityKey,
                    RawRarityKey: presentation.RawRarityKey,
                    SlotKey: presentation.SlotKey,
                    SlotLabel: presentation.SlotLabel,
                    WeaponFamilyKey: presentation.FamilyKey,
                    WeaponFamilyLabel: presentation.FamilyLabel,
                    IdentityKey: presentation.IdentityKey,
                    IdentityLabel: presentation.IdentityLabel,
                    ShowsIdentityBadge: presentation.ShowsIdentityBadge,
                    CanRefit: presentation.CanRefit,
                    IsLaunchSupportedRarity: presentation.IsLaunchSupportedRarity,
                    IsEquipped: equippedItemIds.Contains(item.ItemInstanceId),
                    IsSelected: false,
                    IconSprite: _itemIconSprite(iconKey) ?? _itemIconSprite(item.ItemBaseId) ?? _affixIconSprite(iconKey));
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
            Detail: selectedEntry.Record != null ? BuildDetail(selectedEntry.Record, selectedEntry.IconKey) : null,
            Compare: BuildCompare(selectedEntry.Record));
    }

    private InventoryCompareLaneViewState? BuildCompare(InventoryItemRecord? selectedItem)
    {
        if (selectedItem == null)
        {
            return null;
        }

        var session = _root.SessionState;
        var targetHero = session.Profile.Heroes.FirstOrDefault(hero =>
            string.Equals(hero.HeroId, _targetHeroId, StringComparison.Ordinal));
        var selectedSummary = BuildCompareItemSummary(selectedItem);
        var equippedItem = targetHero == null
            ? null
            : session.Profile.Inventory.FirstOrDefault(item =>
                targetHero.EquippedItemIds.Contains(item.ItemInstanceId, StringComparer.Ordinal)
                && string.Equals(BuildCompareItemSummary(item).SlotKey, selectedSummary.SlotKey, StringComparison.Ordinal));
        var equippedSummary = BuildCompareItemSummary(equippedItem);
        var canEquip = targetHero != null && CanEquipSelectedItem(selectedItem, targetHero.HeroId);
        var status = BuildEquipStatus(selectedItem, targetHero, canEquip);

        return new InventoryCompareLaneViewState(
            TargetHeroId: targetHero?.HeroId ?? string.Empty,
            TargetHeroLabel: targetHero != null ? ResolveHeroName(targetHero) : "선택 영웅 없음",
            TargetHeroMetaLabel: targetHero != null
                ? $"{_contentText?.GetClassName(targetHero.ClassId) ?? targetHero.ClassId} / {_contentText?.GetRaceName(targetHero.RaceId) ?? targetHero.RaceId}"
                : "마을 hero card나 roster에서 대상을 선택",
            SelectedItemLabel: selectedSummary.Name,
            SelectedItemMetaLabel: selectedSummary.MetaLabel,
            EquippedItemLabel: equippedSummary.Name,
            EquippedItemMetaLabel: equippedSummary.MetaLabel,
            EquippedOwnerLabel: selectedSummary.OwnerLabel,
            Rows: BuildCompareRows(selectedSummary, equippedSummary),
            EquipCta: new InventoryEquipCtaViewState(
                CanEquip: canEquip,
                Label: "장착",
                StatusText: status,
                TooltipText: canEquip ? "선택 영웅에게 장착" : status));
    }

    private IReadOnlyList<InventoryCompareRowViewState> BuildCompareRows(
        InventoryCompareItemSummary selected,
        InventoryCompareItemSummary equipped)
    {
        return new[]
        {
            new InventoryCompareRowViewState("slot", "Slot", selected.SlotLabel, equipped.SlotLabel, CompareTone(selected.SlotKey, equipped.SlotKey)),
            new InventoryCompareRowViewState("rarity", "Rarity", selected.RarityKey, equipped.RarityKey, CompareTone(selected.RarityKey, equipped.RarityKey)),
            new InventoryCompareRowViewState("family", "Family", selected.WeaponFamilyLabel, equipped.WeaponFamilyLabel, CompareTone(selected.WeaponFamilyKey, equipped.WeaponFamilyKey)),
            new InventoryCompareRowViewState("affix", "Affix", $"{selected.AffixCount}", $"{equipped.AffixCount}", CompareTone(selected.AffixCount.ToString(), equipped.AffixCount.ToString())),
        };
    }

    private bool CanEquipSelectedItem(InventoryItemRecord selectedItem, string targetHeroId)
    {
        return string.IsNullOrWhiteSpace(selectedItem.EquippedHeroId)
            || string.Equals(selectedItem.EquippedHeroId, targetHeroId, StringComparison.Ordinal);
    }

    private string BuildEquipStatus(InventoryItemRecord selectedItem, HeroInstanceRecord? targetHero, bool canEquip)
    {
        if (!string.IsNullOrWhiteSpace(_lastEquipStatus))
        {
            return _lastEquipStatus;
        }

        if (targetHero == null)
        {
            return "대상 영웅을 선택하면 장착할 수 있습니다.";
        }

        if (!canEquip)
        {
            return "다른 영웅이 장착 중인 아이템입니다.";
        }

        return string.IsNullOrWhiteSpace(selectedItem.EquippedHeroId)
            ? "현재 장비와 슬롯/희귀도/계열만 비교합니다."
            : "이미 대상 영웅이 장착 중입니다.";
    }

    private InventoryDetailViewState BuildDetail(InventoryItemRecord item, string iconKey)
    {
        var name = item.ItemBaseId;
        var slotLabel = "item";
        var slotKey = "item";
        var rarityKey = "common";
        var rawRarityKey = "common";
        var weaponFamilyKey = string.Empty;
        var weaponFamilyLabel = "item";
        var identityKey = "baseline";
        var identityLabel = string.Empty;
        var showsIdentityBadge = false;
        var canRefit = true;
        var isLaunchSupportedRarity = true;
        var setBonusTier = string.Empty;
        var crossLinks = new List<string>();
        if (_root.CombatContentLookup.TryGetItemDefinition(item.ItemBaseId, out var itemDef))
        {
            name = _contentText?.GetItemName(item.ItemBaseId) ?? itemDef.LegacyDisplayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = itemDef.Id;
            }

            slotKey = ResolveSlotKey(itemDef.SlotType);
            var familyKey = ResolveFamilyKey(itemDef);
            var presentation = EquipmentPresentationPolicy.Build(
                slotKey,
                familyKey,
                itemDef.RarityTier.ToString(),
                itemDef.IdentityKind.ToString(),
                EnumerateCraftOperations(itemDef));
            slotLabel = presentation.SlotLabel;
            rarityKey = presentation.RarityKey;
            rawRarityKey = presentation.RawRarityKey;
            weaponFamilyKey = presentation.FamilyKey;
            weaponFamilyLabel = presentation.FamilyLabel;
            identityKey = presentation.IdentityKey;
            identityLabel = presentation.IdentityLabel;
            showsIdentityBadge = presentation.ShowsIdentityBadge;
            canRefit = presentation.CanRefit;
            isLaunchSupportedRarity = presentation.IsLaunchSupportedRarity;
            setBonusTier = string.IsNullOrWhiteSpace(itemDef.BudgetBand)
                ? "set bonus schema pending"
                : $"budget {itemDef.BudgetBand}";
            crossLinks.Add(slotLabel);
            if (!string.IsNullOrWhiteSpace(weaponFamilyLabel))
            {
                crossLinks.Add(weaponFamilyLabel);
            }
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
            IconSprite: _itemIconSprite(iconKey) ?? _itemIconSprite(item.ItemBaseId) ?? _affixIconSprite(iconKey),
            Affixes: affixes,
            Name: name,
            SlotKey: slotKey,
            SlotLabel: slotLabel,
            RarityKey: rarityKey,
            RawRarityKey: rawRarityKey,
            WeaponFamilyKey: weaponFamilyKey,
            WeaponFamilyLabel: weaponFamilyLabel,
            IdentityKey: identityKey,
            IdentityLabel: identityLabel,
            ShowsIdentityBadge: showsIdentityBadge,
            CanRefit: canRefit,
            IsLaunchSupportedRarity: isLaunchSupportedRarity,
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
                IconSprite: _itemIconSprite(c.IconKey) ?? _affixIconSprite(c.IconKey),
                IsSelected: string.Equals(c.Key, _selectedCategoryKey, StringComparison.Ordinal)))
            .ToList();
    }

    private readonly record struct InventoryItemPresentation(
        InventoryItemRecord Record,
        string SlotKey,
        string IconKey,
        InventoryItemViewState ViewState);

    private readonly record struct InventoryCompareItemSummary(
        string Name,
        string MetaLabel,
        string SlotKey,
        string SlotLabel,
        string RarityKey,
        string WeaponFamilyKey,
        string WeaponFamilyLabel,
        string OwnerLabel,
        int AffixCount);

    private readonly record struct CategoryCatalogEntry(string Key, string Label, string IconKey);

    private static readonly CategoryCatalogEntry[] CategoryCatalog =
    {
        new("all",       "ALL",       "blade"),
        new("weapon",    "WEAPON",    "weapon"),
        new("armor",     "ARMOR",     "armor"),
        new("accessory", "ACCESSORY", "accessory"),
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

    private static IEnumerable<string> EnumerateCraftOperations(ItemBaseDefinition item)
    {
        return item.AllowedCraftOperations?.Select(operation => operation.ToString()) ?? Array.Empty<string>();
    }

    private InventoryCompareItemSummary BuildCompareItemSummary(InventoryItemRecord? item)
    {
        if (item == null)
        {
            return new InventoryCompareItemSummary(
                Name: "비어 있음",
                MetaLabel: "대상 슬롯 장비 없음",
                SlotKey: string.Empty,
                SlotLabel: "-",
                RarityKey: "-",
                WeaponFamilyKey: string.Empty,
                WeaponFamilyLabel: "-",
                OwnerLabel: "미장착",
                AffixCount: 0);
        }

        var name = item.ItemBaseId;
        var slotKey = "item";
        var slotLabel = "item";
        var rarityKey = "common";
        var familyKey = string.Empty;
        var familyLabel = "item";
        if (_root.CombatContentLookup.TryGetItemDefinition(item.ItemBaseId, out var itemDef))
        {
            name = _contentText?.GetItemName(item.ItemBaseId) ?? itemDef.LegacyDisplayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = itemDef.Id;
            }

            slotKey = ResolveSlotKey(itemDef.SlotType);
            familyKey = ResolveFamilyKey(itemDef);
            var presentation = EquipmentPresentationPolicy.Build(
                slotKey,
                familyKey,
                itemDef.RarityTier.ToString(),
                itemDef.IdentityKind.ToString(),
                EnumerateCraftOperations(itemDef));
            slotLabel = presentation.SlotLabel;
            rarityKey = presentation.RarityKey;
            familyKey = presentation.FamilyKey;
            familyLabel = presentation.FamilyLabel;
        }

        return new InventoryCompareItemSummary(
            Name: name,
            MetaLabel: $"{slotLabel} / {rarityKey} / {familyLabel}",
            SlotKey: slotKey,
            SlotLabel: slotLabel,
            RarityKey: rarityKey,
            WeaponFamilyKey: familyKey,
            WeaponFamilyLabel: familyLabel,
            OwnerLabel: ResolveOwnerLabel(item),
            AffixCount: item.AffixIds?.Count ?? 0);
    }

    private string ResolveOwnerLabel(InventoryItemRecord item)
    {
        if (string.IsNullOrWhiteSpace(item.EquippedHeroId))
        {
            return "미장착";
        }

        var owner = _root.SessionState.Profile.Heroes.FirstOrDefault(hero =>
            string.Equals(hero.HeroId, item.EquippedHeroId, StringComparison.Ordinal));
        return owner != null ? ResolveHeroName(owner) : item.EquippedHeroId;
    }

    private string ResolveHeroName(HeroInstanceRecord hero)
    {
        var name = _contentText?.GetCharacterName(hero.CharacterId, hero.ArchetypeId);
        return string.IsNullOrWhiteSpace(name) ? hero.HeroId : name;
    }

    private static string CompareTone(string selectedValue, string equippedValue)
    {
        if (string.IsNullOrWhiteSpace(equippedValue) || string.Equals(equippedValue, "-", StringComparison.Ordinal))
        {
            return "new";
        }

        return string.Equals(selectedValue, equippedValue, StringComparison.OrdinalIgnoreCase)
            ? "same"
            : "different";
    }
}
