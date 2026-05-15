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

    private IEquipmentRefitActions? _actions;

    public EquipmentRefitView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
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
    }

    public void Render(EquipmentRefitViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        // 좌측 컨텍스트 — 선택 item + 장착 hero (EquippedHeroId 파생)
        if (state.EquippedHeroPortrait != null)
            _standeePortrait.style.backgroundImage = new StyleBackground(state.EquippedHeroPortrait);
        if (_selectedItemName != null)
            _selectedItemName.text = $"{state.SelectedItemName}  ·  {state.SelectedItemSlotLabel}";
        if (_equippedHeroLabel != null)
            _equippedHeroLabel.text = state.EquippedHeroLabel;

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
            row.AddToClassList($"erp-affix-row--{affix.GroupKey}");
            if (affix.IsSelectedForReroll) row.AddToClassList("erp-affix-row--selected");

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
            if (item.IsSelected) row.AddToClassList("erp-pool-row--selected");

            var icon = new VisualElement();
            icon.AddToClassList("erp-pool-row__weapon-icon");
            if (item.IconSprite != null) icon.style.backgroundImage = new StyleBackground(item.IconSprite);
            row.Add(icon);

            var name = new Label(item.Name);
            name.AddToClassList("erp-pool-row__name");
            row.Add(name);

            var slot = new Label(item.SlotKey);
            slot.AddToClassList("erp-pool-row__slot");
            row.Add(slot);

            var gem = new VisualElement();
            gem.AddToClassList("erp-pool-row__gem");
            gem.AddToClassList($"erp-pool-row__gem--{item.RarityKey}");
            row.Add(gem);

            row.tooltip = $"{item.Name} · {item.SlotKey} · {item.RarityKey}";
            row.RegisterCallback<ClickEvent>(_ => _actions?.OnPoolItemSelected(item.ItemInstanceId));
            _inventoryPool.Add(row);
        }
    }
}

public interface IEquipmentRefitActions
{
    void OnAffixSelected(string affixId);
    void OnPoolItemSelected(string itemInstanceId);
    void OnRefitConfirmed();
}
