using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Inventory V1 surface View — UXML root에서 named container 캡처, Render(ViewState) 시 재구축.
/// container: GoldIcon / EchoIcon / CategorySidebar / ItemGrid / DetailIcon / DetailAffixes.
/// sprite 로드 안 함 — caller가 Texture2D pre-resolve.
/// </summary>
public sealed class InventoryView
{
    private readonly VisualElement _goldIcon;
    private readonly VisualElement _echoIcon;
    private readonly Label _goldAmount;
    private readonly Label _echoAmount;
    private readonly VisualElement _categorySidebar;
    private readonly VisualElement _itemGrid;
    private readonly VisualElement _detailIcon;
    private readonly VisualElement _detailAffixes;
    private readonly VisualElement? _modalRoot;
    private readonly Button? _closeButton;

    private IInventoryActions? _actions;

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

    public InventoryView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _modalRoot = root.Q<VisualElement>("InvRoot");
        _closeButton = root.Q<Button>(className: "inv-header__close");   // UXML에 없으면 null OK
        _goldIcon = root.Q<VisualElement>("GoldIcon")
            ?? throw new ArgumentException("GoldIcon 못 찾음");
        _echoIcon = root.Q<VisualElement>("EchoIcon")
            ?? throw new ArgumentException("EchoIcon 못 찾음");
        _categorySidebar = root.Q<VisualElement>("CategorySidebar")
            ?? throw new ArgumentException("CategorySidebar 못 찾음");
        _itemGrid = root.Q<VisualElement>("ItemGrid")
            ?? throw new ArgumentException("ItemGrid 못 찾음");
        _detailIcon = root.Q<VisualElement>("DetailIcon")
            ?? throw new ArgumentException("DetailIcon 못 찾음");
        _detailAffixes = root.Q<VisualElement>("DetailAffixes")
            ?? throw new ArgumentException("DetailAffixes 못 찾음");

        // Currency amount labels — 옛 mock의 hardcoded text는 UXML에 있음 ("9,876,543" 등).
        // V1: currency 부모 element 안의 inv-currency__amount Label 두 개.
        var amounts = root.Query<Label>(className: "inv-currency__amount").ToList();
        _goldAmount = amounts.Count > 0 ? amounts[0] : null!;
        _echoAmount = amounts.Count > 1 ? amounts[1] : null!;
    }

    public void Bind(IInventoryActions actions)
    {
        _actions = actions;
    }

    public void Render(InventoryViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        // Currency header
        if (state.GoldSprite != null) _goldIcon.style.backgroundImage = new StyleBackground(state.GoldSprite);
        if (state.EchoSprite != null) _echoIcon.style.backgroundImage = new StyleBackground(state.EchoSprite);
        if (_goldAmount != null) _goldAmount.text = state.Gold.ToString("N0");
        if (_echoAmount != null) _echoAmount.text = state.Echo.ToString("N0");

        RenderSidebar(state.Categories);
        RenderGrid(state.Items);
        RenderDetail(state.Detail);
    }

    private void RenderSidebar(System.Collections.Generic.IReadOnlyList<InventoryCategoryViewState> categories)
    {
        _categorySidebar.Clear();
        foreach (var c in categories)
        {
            var row = new VisualElement();
            row.AddToClassList("inv-sidebar__row");
            if (c.IsSelected) row.AddToClassList("inv-sidebar__row--selected");

            var icon = new VisualElement();
            icon.AddToClassList("inv-sidebar__row-icon");
            if (c.IconSprite != null) icon.style.backgroundImage = new StyleBackground(c.IconSprite);
            row.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("inv-sidebar__row-info");
            var name = new Label(c.Label);
            name.AddToClassList("inv-sidebar__row-name");
            info.Add(name);
            var count = new Label(c.Count);
            count.AddToClassList("inv-sidebar__row-count");
            info.Add(count);
            row.Add(info);

            row.RegisterCallback<ClickEvent>(_ => _actions?.OnCategorySelected(c.Key));
            _categorySidebar.Add(row);
        }
    }

    private void RenderGrid(System.Collections.Generic.IReadOnlyList<InventoryItemViewState> items)
    {
        _itemGrid.Clear();
        foreach (var item in items)
        {
            var cell = new VisualElement();
            cell.AddToClassList("inv-grid__cell");
            cell.AddToClassList("sm-hover-raise");   // 콘솔급 motion — hover raise
            cell.AddToClassList($"inv-grid__cell--{item.RarityKey}");

            var iconLayer = new VisualElement();
            iconLayer.AddToClassList("inv-grid__cell-icon");
            if (item.IconSprite != null) iconLayer.style.backgroundImage = new StyleBackground(item.IconSprite);
            cell.Add(iconLayer);

            var familyBadge = new Label(item.WeaponFamilyLabel);
            familyBadge.AddToClassList("inv-grid__cell-family");
            familyBadge.AddToClassList($"inv-grid__cell-family--{item.WeaponFamilyKey}");
            cell.Add(familyBadge);

            if (item.IsEquipped)
            {
                var eq = new VisualElement();
                eq.AddToClassList("inv-grid__cell-equipped");
                cell.Add(eq);
            }

            cell.tooltip = $"{item.IconKey} ({item.WeaponFamilyKey}) · {item.RarityKey}{(item.IsEquipped ? " · equipped" : "")}";
            cell.RegisterCallback<ClickEvent>(_ => _actions?.OnItemSelected(item.ItemInstanceId));
            _itemGrid.Add(cell);
        }
    }

    private void RenderDetail(InventoryDetailViewState? detail)
    {
        if (detail == null)
        {
            _detailAffixes.Clear();
            return;
        }

        if (detail.IconSprite != null) _detailIcon.style.backgroundImage = new StyleBackground(detail.IconSprite);

        _detailAffixes.Clear();
        string? previousGroup = null;
        foreach (var affix in detail.Affixes)
        {
            if (previousGroup != affix.GroupKey)
            {
                var header = new Label(affix.GroupKey.ToUpperInvariant());
                header.AddToClassList("inv-detail__affix-group");
                header.AddToClassList($"inv-detail__affix-group--{affix.GroupKey}");
                _detailAffixes.Add(header);
                previousGroup = affix.GroupKey;
            }

            var row = new VisualElement();
            row.AddToClassList("inv-detail__affix-row");
            row.AddToClassList($"inv-detail__affix-row--{affix.GroupKey}");

            var nameEl = new Label(affix.Name);
            nameEl.AddToClassList("inv-detail__affix-name");
            row.Add(nameEl);

            var value = new Label(affix.ValueRange);
            value.AddToClassList("inv-detail__affix-value");
            row.Add(value);

            _detailAffixes.Add(row);
        }
    }
}

/// <summary>
/// View → Presenter event interface. V1 Inventory: category 선택, item 선택, equip/compare 액션.
/// sell은 GameSessionState에 API가 없어 제거 (audit §4.1 P1-3 — sell API 신설은 별도 task).
/// </summary>
public interface IInventoryActions
{
    void OnCategorySelected(string categoryKey);
    void OnItemSelected(string itemInstanceId);
    void OnEquipItem(string itemInstanceId);
    void OnCompareItem(string itemInstanceId);
}
