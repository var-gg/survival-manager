using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit V1 Presenter — selected item의 affix 5 line + inventory pool → ViewState.
///
/// Sprint 3 wire: Profile.Inventory (InventoryItemRecord[]) → pool. selected item의 AffixIds → affix list.
/// ItemBaseDefinition.WeaponFamilyTag / RarityTier로 pool gem 색상. RefitItem edit wire.
///
/// affix group (implicit/prefix/suffix) 분류는 V1 floor 규약 (implicit 1 + prefix 2 + suffix 2) 순서로 추정 —
/// 정확한 prefix/suffix flag은 AffixDefinition에서 read해야 하나 본 Sprint는 index 기반 추정으로 wire.
///
/// 워크플로우: 사용자가 affix 선택 + Refit → SessionState.RefitItem → BattleTest stat 즉시 반영.
/// </summary>
public sealed class EquipmentRefitPresenter : IEquipmentRefitActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    /// <summary>refit 고정 비용 (item-and-affix-system.md V1).</summary>
    public const int RefitEchoCost = 15;

    private readonly GameSessionRoot _root;
    private readonly EquipmentRefitView _view;
    private readonly ContentTextResolver _contentText;
    private readonly SpriteLoader _affixSprite;
    private readonly SpriteLoader _currencySprite;
    private readonly SpriteLoader _portraitLoader;
    private int _selectedAffixIndex = -1;
    private string _selectedItemInstanceId = string.Empty;

    public EquipmentRefitPresenter(
        GameSessionRoot root,
        EquipmentRefitView view,
        ContentTextResolver contentText,
        SpriteLoader? affixSprite = null,
        SpriteLoader? currencySprite = null,
        SpriteLoader? portraitLoader = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        // production hub modal에서는 sprite loader 미주입 — null fallback (후속 task에서 Resources/Addressables 적용).
        _affixSprite = affixSprite ?? (_ => null);
        _currencySprite = currencySprite ?? (_ => null);
        _portraitLoader = portraitLoader ?? (_ => null);
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

    void IEquipmentRefitActions.OnAffixSelected(string affixId)
    {
        // selected item의 AffixIds에서 index 찾기 (RefitItem은 affixSlotIndex를 받음).
        var item = ResolveSelectedItem();
        if (item != null)
        {
            _selectedAffixIndex = item.AffixIds.FindIndex(id => string.Equals(id, affixId, StringComparison.Ordinal));
        }
        Refresh();
    }

    void IEquipmentRefitActions.OnPoolItemSelected(string itemInstanceId)
    {
        _selectedItemInstanceId = itemInstanceId;
        _selectedAffixIndex = -1;
        Refresh();
    }

    void IEquipmentRefitActions.OnRefitConfirmed()
    {
        if (string.IsNullOrEmpty(_selectedItemInstanceId) || _selectedAffixIndex < 0)
            return;
        _root.SessionState.RefitItem(_selectedItemInstanceId, _selectedAffixIndex);
        Refresh();
    }

    private SM.Persistence.Abstractions.Models.InventoryItemRecord? ResolveSelectedItem()
    {
        var inventory = _root.SessionState.Profile.Inventory;
        if (!string.IsNullOrEmpty(_selectedItemInstanceId))
        {
            var match = inventory.FirstOrDefault(i =>
                string.Equals(i.ItemInstanceId, _selectedItemInstanceId, StringComparison.Ordinal));
            if (match != null) return match;
        }
        return inventory.Count > 0 ? inventory[0] : null;
    }

    private EquipmentRefitViewState BuildState()
    {
        var session = _root.SessionState;
        var inventory = session.Profile.Inventory;
        var lookup = _root.CombatContentLookup;

        // Pool — Profile.Inventory 전체. ItemBaseDefinition으로 이름 / slot / rarity 보강.
        var pool = inventory
            .Select(item =>
            {
                var slotKey = "weapon";
                var rarityKey = "common";
                if (lookup.TryGetItemDefinition(item.ItemBaseId, out var baseDef))
                {
                    slotKey = baseDef.SlotType.ToString().ToLowerInvariant();
                    rarityKey = baseDef.RarityTier.ToString().ToLowerInvariant();
                }
                return new EquipmentRefitPoolRowViewState(
                    ItemInstanceId: item.ItemInstanceId,
                    Name: _contentText.GetItemName(item.ItemBaseId),
                    SlotKey: slotKey,
                    IconSprite: _affixSprite(item.ItemBaseId),
                    RarityKey: rarityKey,
                    IsSelected: string.Equals(item.ItemInstanceId, _selectedItemInstanceId, StringComparison.Ordinal));
            })
            .ToList();

        // Affix list — selected item의 AffixIds. group은 AffixDefinition.Tier에서 read (index 추정 폐기).
        // 값은 instance 확정 roll 미저장 (AffixIds = definition id) → ValueMin~ValueMax 범위 표기.
        var selectedItem = ResolveSelectedItem();
        var affixes = new List<EquipmentRefitAffixRowViewState>();
        if (selectedItem != null)
        {
            for (var i = 0; i < selectedItem.AffixIds.Count; i++)
            {
                var affixId = selectedItem.AffixIds[i];
                var group = "prefix";
                var valueRange = "—";
                if (lookup.TryGetAffixDefinition(affixId, out var affixDef))
                {
                    group = affixDef.Tier.ToString().ToLowerInvariant();
                    valueRange = $"{affixDef.ValueMin:0.#} ~ {affixDef.ValueMax:0.#}";
                }
                affixes.Add(new EquipmentRefitAffixRowViewState(
                    AffixId: affixId,
                    GroupKey: group,
                    Name: _contentText.GetAffixName(affixId),
                    ValueRange: valueRange,
                    IconSprite: _affixSprite(affixId),
                    IsSelectedForReroll: i == _selectedAffixIndex));
            }
        }

        // 좌측 컨텍스트 — 선택 item 정체성 + 장착 hero (InventoryItemRecord.EquippedHeroId 파생).
        var selectedItemName = "—";
        var selectedSlotLabel = "—";
        var selectedRarityKey = "common";
        var equippedHeroLabel = "미장착";
        Texture2D? equippedHeroPortrait = null;
        if (selectedItem != null)
        {
            selectedItemName = _contentText.GetItemName(selectedItem.ItemBaseId);
            if (lookup.TryGetItemDefinition(selectedItem.ItemBaseId, out var baseDef))
            {
                selectedSlotLabel = SlotLabelKo(baseDef.SlotType);
                selectedRarityKey = baseDef.RarityTier.ToString().ToLowerInvariant();
            }
            if (!string.IsNullOrEmpty(selectedItem.EquippedHeroId))
            {
                var hero = session.Profile.Heroes
                    .FirstOrDefault(h => string.Equals(h.HeroId, selectedItem.EquippedHeroId, StringComparison.Ordinal));
                var heroName = !string.IsNullOrEmpty(hero?.Name) ? hero!.Name : selectedItem.EquippedHeroId;
                equippedHeroLabel = $"장착: {heroName}";
                equippedHeroPortrait = _portraitLoader(selectedItem.EquippedHeroId);
            }
        }

        return new EquipmentRefitViewState(
            SelectedItemName: selectedItemName,
            SelectedItemSlotLabel: selectedSlotLabel,
            SelectedItemRarityKey: selectedRarityKey,
            EquippedHeroLabel: equippedHeroLabel,
            EquippedHeroPortrait: equippedHeroPortrait,
            EchoSprite: _currencySprite("echo"),
            RefitCost: RefitEchoCost,
            Affixes: affixes,
            Pool: pool);
    }

    private static string SlotLabelKo(ItemSlotType slot) => slot switch
    {
        ItemSlotType.Weapon    => "무기",
        ItemSlotType.Armor     => "방어구",
        ItemSlotType.Accessory => "장신구",
        _ => slot.ToString(),
    };
}
