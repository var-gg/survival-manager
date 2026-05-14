using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit V1 surface View. UXML container: StandeePortrait / EchoIcon / AffixList / InventoryPool / RefitCostLabel.
/// </summary>
public sealed class EquipmentRefitView
{
    private readonly VisualElement _standeePortrait;
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
    }

    public void Bind(IEquipmentRefitActions actions)
    {
        _actions = actions;
    }

    public void Render(EquipmentRefitViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        if (state.StandeePortrait != null) _standeePortrait.style.backgroundImage = new StyleBackground(state.StandeePortrait);
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

            var glyph = new VisualElement();
            glyph.AddToClassList("erp-affix-row__glyph");
            row.Add(glyph);

            var tier = new VisualElement();
            tier.AddToClassList("erp-affix-row__tier");
            row.Add(tier);

            row.tooltip = $"{affix.IconKey} [{affix.GroupKey}]";
            row.RegisterCallback<ClickEvent>(_ => _actions?.OnAffixSelected(affix.IconKey));
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

            var icon = new VisualElement();
            icon.AddToClassList("erp-pool-row__weapon-icon");
            if (item.IconSprite != null) icon.style.backgroundImage = new StyleBackground(item.IconSprite);
            row.Add(icon);

            var glyph = new VisualElement();
            glyph.AddToClassList("erp-pool-row__glyph");
            row.Add(glyph);

            var gem = new VisualElement();
            gem.AddToClassList("erp-pool-row__gem");
            gem.AddToClassList($"erp-pool-row__gem--{item.RarityKey}");
            row.Add(gem);

            row.tooltip = $"{item.ItemInstanceId} ({item.RarityKey})";
            row.RegisterCallback<ClickEvent>(_ => _actions?.OnPoolItemSelected(item.ItemInstanceId));
            _inventoryPool.Add(row);
        }
    }
}

public interface IEquipmentRefitActions
{
    void OnAffixSelected(string affixKey);
    void OnPoolItemSelected(string itemInstanceId);
    void OnRefitConfirmed();
}
