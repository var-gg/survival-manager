using System;
using System.Collections.Generic;
using System.Linq;
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
        SpriteLoader affixSprite,
        SpriteLoader currencySprite,
        SpriteLoader portraitLoader)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _affixSprite = affixSprite ?? throw new ArgumentNullException(nameof(affixSprite));
        _currencySprite = currencySprite ?? throw new ArgumentNullException(nameof(currencySprite));
        _portraitLoader = portraitLoader ?? throw new ArgumentNullException(nameof(portraitLoader));
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

    void IEquipmentRefitActions.OnAffixSelected(string affixKey)
    {
        // affixKey는 affixId. selected item의 AffixIds에서 index 찾기.
        var item = ResolveSelectedItem();
        if (item != null)
        {
            _selectedAffixIndex = item.AffixIds.FindIndex(id => string.Equals(id, affixKey, StringComparison.Ordinal));
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

        // Pool — Profile.Inventory 전체. ItemBaseDefinition으로 weapon family / rarity 보강.
        var pool = inventory
            .Select(item =>
            {
                var rarityKey = "common";
                if (_root.CombatContentLookup.TryGetItemDefinition(item.ItemBaseId, out var baseDef))
                {
                    rarityKey = baseDef.RarityTier.ToString().ToLowerInvariant();
                }
                return new EquipmentRefitPoolRowViewState(
                    ItemInstanceId: item.ItemInstanceId,
                    IconKey: item.ItemBaseId,
                    IconSprite: _affixSprite(item.ItemBaseId),
                    RarityKey: rarityKey);
            })
            .ToList();

        // Affix list — selected item의 AffixIds. V1 floor 규약: implicit 1 + prefix 2 + suffix 2.
        // index 기반 group 추정 (0=implicit, 1~2=prefix, 3+=suffix). 정확한 flag은 Sprint 3 cont.
        var selectedItem = ResolveSelectedItem();
        var affixes = new List<EquipmentRefitAffixRowViewState>();
        if (selectedItem != null)
        {
            for (var i = 0; i < selectedItem.AffixIds.Count; i++)
            {
                var affixId = selectedItem.AffixIds[i];
                var group = i == 0 ? "implicit" : i <= 2 ? "prefix" : "suffix";
                affixes.Add(new EquipmentRefitAffixRowViewState(
                    GroupKey: group,
                    IconKey: affixId,
                    IconSprite: _affixSprite(affixId),
                    IsSelectedForReroll: i == _selectedAffixIndex));
            }
        }

        return new EquipmentRefitViewState(
            StandeePortrait: null,  // TODO Sprint 3 cont: selected hero portrait
            EchoSprite: _currencySprite("echo"),
            RefitCost: RefitEchoCost,
            Affixes: affixes,
            Pool: pool);
    }
}
