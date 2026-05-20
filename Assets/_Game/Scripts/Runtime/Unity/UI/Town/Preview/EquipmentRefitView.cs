using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit V1 surface View — item-centric refit (audit §4.1 P1-2).
/// UXML container: StandeePortrait / SelectedItemName / EquippedHeroLabel / EchoIcon / AffixList /
/// InventoryPool / RefitCostLabel. affix row는 이름 + 값 범위 (instance 확정 roll 미저장 → 범위 표기).
/// </summary>
public sealed class EquipmentRefitView
{
    private readonly VisualElement _standeePortrait;
    private readonly Label? _selectedItemName;
    private readonly Label? _equippedHeroLabel;
    private readonly VisualElement _echoIcon;
    private readonly VisualElement _affixList;
    private readonly VisualElement _inventoryPool;
    private readonly Label _refitCostLabel;
    private readonly VisualElement? _modalRoot;
    private readonly Button? _closeButton;
    private readonly Button? _refitButton;

    private IEquipmentRefitActions? _actions;

    public void BindClose(Action close)
    {
        if (_closeButton == null || close == null) return;
        _closeButton.clicked += close;
    }

    public void Open()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.Flex;
        _modalRoot.RemoveFromClassList("sm-modal-anim--enter");
        var wrapper = _modalRoot.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.None;
        _modalRoot.AddToClassList("sm-modal-anim--enter");
        var wrapper = _modalRoot.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.None;
    }

    public EquipmentRefitView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _modalRoot = root.Q<VisualElement>("ErpRoot");
        _closeButton = root.Q<Button>(className: "erp-header__close");
        _refitButton = root.Q<Button>(className: "erp-refit-cta");
        _refitButton?.AddToClassList("sm-operation");
        _refitButton?.AddToClassList("sm-operation--refit");
        _standeePortrait = root.Q<VisualElement>("StandeePortrait")
            ?? throw new ArgumentException("StandeePortrait 못 찾음");
        _echoIcon = root.Q<VisualElement>("EchoIcon")
            ?? throw new ArgumentException("EchoIcon 못 찾음");
        _affixList = root.Q<VisualElement>("AffixList")
            ?? throw new ArgumentException("AffixList 못 찾음");
        _inventoryPool = root.Q<VisualElement>("InventoryPool")
            ?? throw new ArgumentException("InventoryPool 못 찾음");
        _refitCostLabel = root.Q<Label>("RefitCostLabel")
            ?? throw new ArgumentException("RefitCostLabel 못 찾음");
        // item 컨텍스트 라벨 — 없어도 preview가 깨지지 않게 nullable
        _selectedItemName = root.Q<Label>("SelectedItemName");
        _equippedHeroLabel = root.Q<Label>("EquippedHeroLabel");
    }

    public void Bind(IEquipmentRefitActions actions)
    {
        _actions = actions;
        if (_refitButton != null)
        {
            _refitButton.clicked -= HandleRefitClicked;
            _refitButton.clicked += HandleRefitClicked;
        }
    }

    public void Render(EquipmentRefitViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        // 좌측 컨텍스트 — 선택 item + 장착 hero (EquippedHeroId 파생)
        if (state.EquippedHeroPortrait != null)
            _standeePortrait.style.backgroundImage = new StyleBackground(state.EquippedHeroPortrait);
        if (_selectedItemName != null)
        {
            var familyText = string.IsNullOrWhiteSpace(state.SelectedItemFamilyLabel)
                ? string.Empty
                : $" · {state.SelectedItemFamilyLabel}";
            var identityText = state.SelectedItemShowsIdentityBadge
                ? $" · {state.SelectedItemIdentityLabel}"
                : string.Empty;
            _selectedItemName.text = $"{state.SelectedItemName}  ·  {state.SelectedItemSlotLabel}{familyText}{identityText}";
        }
        if (_equippedHeroLabel != null)
            _equippedHeroLabel.text = state.EquippedHeroLabel;
        _refitButton?.SetEnabled(state.SelectedItemCanRefit);

        if (state.EchoSprite != null) _echoIcon.style.backgroundImage = new StyleBackground(state.EchoSprite);
        _refitCostLabel.text = $"REFIT (-{state.RefitCost} Echo)";

        RenderAffixList(state.Affixes);
        RenderPool(state.Pool);
    }

    private void RenderAffixList(IReadOnlyList<EquipmentRefitAffixRowViewState> affixes)
    {
        _affixList.Clear();
        string? previousGroup = null;
        foreach (var affix in affixes)
        {
            if (previousGroup != affix.GroupKey)
            {
                var header = new Label(affix.GroupKey.ToUpperInvariant());
                header.AddToClassList("erp-affix-group");
                header.AddToClassList($"erp-affix-group--{affix.GroupKey}");
                _affixList.Add(header);
                previousGroup = affix.GroupKey;
            }

            var row = new VisualElement();
            row.AddToClassList("erp-affix-row");
            row.AddToClassList("sm-affix-row");
            row.AddToClassList($"erp-affix-row--{affix.GroupKey}");
            row.AddToClassList($"sm-affix-row--{affix.GroupKey}");
            if (affix.IsSelectedForReroll)
            {
                row.AddToClassList("erp-affix-row--selected");
                row.AddToClassList("sm-affix-row--selected");
                row.AddToClassList("sm-affix-row--refit-target");
                row.AddToClassList("sm-item-state");
            }

            var icon = new VisualElement();
            icon.AddToClassList("erp-affix-row__icon");
            if (affix.IconSprite != null) icon.style.backgroundImage = new StyleBackground(affix.IconSprite);
            row.Add(icon);

            // affix 이름 — AffixDefinition.NameKey resolved
            var name = new Label(affix.Name);
            name.AddToClassList("erp-affix-row__name");
            row.Add(name);

            // 값 범위 — AffixDefinition.ValueMin~ValueMax (instance 확정 roll 미저장)
            var value = new Label(affix.ValueRange);
            value.AddToClassList("erp-affix-row__value");
            row.Add(value);

            row.tooltip = $"{affix.AffixId} [{affix.GroupKey}]";
            row.RegisterCallback<ClickEvent>(_ => _actions?.OnAffixSelected(affix.AffixId));
            _affixList.Add(row);
        }
    }

    private void RenderPool(IReadOnlyList<EquipmentRefitPoolRowViewState> pool)
    {
        _inventoryPool.Clear();
        foreach (var item in pool)
        {
            var row = new VisualElement();
            row.AddToClassList("erp-pool-row");
            row.AddToClassList("sm-item-cell");
            if (item.IsSelected)
            {
                row.AddToClassList("erp-pool-row--selected");
                row.AddToClassList("sm-item-cell--selected");
                row.AddToClassList("sm-item-state");
            }

            var icon = new VisualElement();
            icon.AddToClassList("erp-pool-row__weapon-icon");
            icon.AddToClassList("sm-item-icon");
            if (item.IconSprite != null) icon.style.backgroundImage = new StyleBackground(item.IconSprite);
            row.Add(icon);

            var name = new Label(item.Name);
            name.AddToClassList("erp-pool-row__name");
            row.Add(name);

            var slot = new Label(item.SlotLabel);
            slot.AddToClassList("erp-pool-row__slot");
            slot.AddToClassList("sm-item-badge");
            slot.AddToClassList("sm-item-badge--slot");
            slot.AddToClassList($"erp-pool-row__slot--{item.SlotKey}");
            row.Add(slot);

            if (!string.IsNullOrWhiteSpace(item.FamilyKey))
            {
                var family = new Label(item.FamilyLabel);
                family.AddToClassList("erp-pool-row__family");
                family.AddToClassList("sm-item-badge");
                family.AddToClassList("sm-item-badge--family");
                row.Add(family);
            }

            if (item.ShowsIdentityBadge)
            {
                var identity = new Label(item.IdentityLabel);
                identity.AddToClassList("erp-pool-row__identity");
                identity.AddToClassList("sm-item-identity");
                identity.AddToClassList($"sm-item-identity--{item.IdentityKey}");
                row.Add(identity);
            }

            var rarity = new VisualElement();
            rarity.AddToClassList("erp-pool-row__rarity");
            rarity.AddToClassList("sm-item-rarity");
            rarity.AddToClassList($"sm-item-rarity--{item.RarityKey}");
            row.Add(rarity);

            var rarityTooltip = item.IsLaunchSupportedRarity
                ? item.RarityKey
                : $"{item.RawRarityKey} as {item.RarityKey}";
            row.tooltip = $"{item.Name} · {item.SlotKey} · {item.FamilyKey} · {rarityTooltip}";
            row.RegisterCallback<ClickEvent>(_ => _actions?.OnPoolItemSelected(item.ItemInstanceId));
            _inventoryPool.Add(row);
        }
    }

    private void HandleRefitClicked()
    {
        _actions?.OnRefitConfirmed();
    }
}

public interface IEquipmentRefitActions
{
    void OnAffixSelected(string affixId);
    void OnPoolItemSelected(string itemInstanceId);
    void OnRefitConfirmed();
}
