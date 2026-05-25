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
    private readonly Label? _titleLabel;
    private readonly Label? _subtitleLabel;
    private readonly VisualElement _categorySidebar;
    private readonly VisualElement _itemGrid;
    private readonly VisualElement _detailIcon;
    private readonly Image _detailIconImage;
    private readonly VisualElement _detailAffixes;
    private readonly Label? _detailNameLabel;
    private readonly Label? _detailMetaLabel;
    private readonly Label? _detailSetBonusLabel;
    private readonly VisualElement? _detailCrossLinks;
    private readonly VisualElement? _compareLane;
    private readonly Label? _compareTargetHeroLabel;
    private readonly Label? _compareTargetMetaLabel;
    private readonly Label? _compareSelectedLabel;
    private readonly Label? _compareSelectedMetaLabel;
    private readonly VisualElement? _compareSelectedIcon;
    private readonly Label? _compareEquippedLabel;
    private readonly Label? _compareEquippedMetaLabel;
    private readonly VisualElement? _compareEquippedIcon;
    private readonly Label? _compareOwnerLabel;
    private readonly Label? _compareEquipStatusLabel;
    private readonly VisualElement? _compareRows;
    private readonly VisualElement? _modalRoot;
    private readonly Button? _closeButton;
    private readonly Button? _equipButton;
    private readonly Button? _compareButton;

    private IInventoryActions? _actions;
    private string _currentDetailItemInstanceId = string.Empty;

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
        var scope = _modalRoot ?? root;
        _closeButton = scope.Q<Button>(className: "inv-currency__close");   // UXML currency header에 X 버튼
        _equipButton = scope.Q<Button>(className: "inv-detail__btn--equip");
        _compareButton = scope.Q<Button>(className: "inv-detail__btn--compare");
        _titleLabel = scope.Q<Label>("InventoryTitleLabel");
        _subtitleLabel = scope.Q<Label>("InventorySubtitleLabel");
        _goldIcon = scope.Q<VisualElement>("GoldIcon")
            ?? throw new ArgumentException("GoldIcon 못 찾음");
        _echoIcon = scope.Q<VisualElement>("EchoIcon")
            ?? throw new ArgumentException("EchoIcon 못 찾음");
        _categorySidebar = scope.Q<VisualElement>("CategorySidebar")
            ?? throw new ArgumentException("CategorySidebar 못 찾음");
        _itemGrid = scope.Q<VisualElement>("ItemGrid")
            ?? throw new ArgumentException("ItemGrid 못 찾음");
        _detailIcon = scope.Q<VisualElement>("DetailIcon")
            ?? throw new ArgumentException("DetailIcon 못 찾음");
        _detailIcon.AddToClassList("sm-item-icon");
        _detailIconImage = new Image
        {
            pickingMode = PickingMode.Ignore,
            scaleMode = ScaleMode.ScaleToFit
        };
        _detailIconImage.AddToClassList("inv-detail__icon-image");
        _detailIcon.Add(_detailIconImage);
        _detailAffixes = scope.Q<VisualElement>("DetailAffixes")
            ?? throw new ArgumentException("DetailAffixes 못 찾음");
        _detailNameLabel = scope.Q<Label>("DetailNameLabel");
        _detailMetaLabel = scope.Q<Label>("DetailMetaLabel");
        _detailSetBonusLabel = scope.Q<Label>("DetailSetBonusLabel");
        _detailCrossLinks = scope.Q<VisualElement>("DetailCrossLinks");
        _compareLane = scope.Q<VisualElement>("CompareLane");
        _compareTargetHeroLabel = scope.Q<Label>("CompareTargetHeroLabel");
        _compareTargetMetaLabel = scope.Q<Label>("CompareTargetMetaLabel");
        _compareSelectedLabel = scope.Q<Label>("CompareSelectedItemLabel");
        _compareSelectedMetaLabel = scope.Q<Label>("CompareSelectedItemMetaLabel");
        _compareSelectedIcon = scope.Q<VisualElement>("CompareSelectedItemIcon");
        _compareEquippedLabel = scope.Q<Label>("CompareEquippedItemLabel");
        _compareEquippedMetaLabel = scope.Q<Label>("CompareEquippedItemMetaLabel");
        _compareEquippedIcon = scope.Q<VisualElement>("CompareEquippedItemIcon");
        _compareOwnerLabel = scope.Q<Label>("CompareOwnerLabel");
        _compareEquipStatusLabel = scope.Q<Label>("CompareEquipStatusLabel");
        _compareRows = scope.Q<VisualElement>("CompareRows");

        // Currency amount labels — 옛 mock의 hardcoded text는 UXML에 있음 ("9,876,543" 등).
        // V1: currency 부모 element 안의 inv-currency__amount Label 두 개.
        var amounts = scope.Query<Label>(className: "inv-currency__amount").ToList();
        _goldAmount = amounts.Count > 0 ? amounts[0] : null!;
        _echoAmount = amounts.Count > 1 ? amounts[1] : null!;
    }

    public void Bind(IInventoryActions actions)
    {
        _actions = actions;
        if (_equipButton != null)
        {
            _equipButton.clicked -= HandleEquipClicked;
            _equipButton.clicked += HandleEquipClicked;
        }
        if (_compareButton != null)
        {
            _compareButton.clicked -= HandleCompareClicked;
            _compareButton.clicked += HandleCompareClicked;
        }
    }

    public void Render(InventoryViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        // Currency header
        if (_titleLabel != null) _titleLabel.text = "장비 비교";
        if (_subtitleLabel != null)
        {
            var target = state.Compare?.TargetHeroLabel ?? "대상 영웅 없음";
            _subtitleLabel.text = $"{target} / {state.Items.Count}개 후보 / 슬롯·희귀도·계열 비교";
        }

        if (state.GoldSprite != null) _goldIcon.style.backgroundImage = new StyleBackground(state.GoldSprite);
        if (state.EchoSprite != null) _echoIcon.style.backgroundImage = new StyleBackground(state.EchoSprite);
        if (_goldAmount != null) _goldAmount.text = state.Gold.ToString("N0");
        if (_echoAmount != null) _echoAmount.text = state.Echo.ToString("N0");

        RenderSidebar(state.Categories);
        RenderGrid(state.Items);
        RenderDetail(state.Detail);
        RenderCompare(state.Compare);
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
            cell.AddToClassList("sm-item-cell");
            cell.AddToClassList("sm-hover-raise");   // 콘솔급 motion — hover raise
            cell.AddToClassList($"inv-grid__cell--{item.RarityKey}");
            if (!string.IsNullOrWhiteSpace(item.WeaponFamilyKey))
                cell.AddToClassList($"inv-grid__cell--{item.WeaponFamilyKey}");
            if (item.IsSelected) cell.AddToClassList("inv-grid__cell--selected");
            if (item.IsSelected) cell.AddToClassList("sm-item-cell--selected");
            if (item.IsEquipped) cell.AddToClassList("sm-item-cell--equipped");

            var iconLayer = new VisualElement();
            iconLayer.AddToClassList("inv-grid__cell-icon");
            iconLayer.AddToClassList("sm-item-icon");
            if (item.IconSprite != null) iconLayer.style.backgroundImage = new StyleBackground(item.IconSprite);
            cell.Add(iconLayer);

            var copy = new VisualElement();
            copy.AddToClassList("inv-grid__cell-copy");
            var name = new Label(item.Name);
            name.AddToClassList("inv-grid__cell-name");
            copy.Add(name);
            var meta = new Label(item.MetaLabel);
            meta.AddToClassList("inv-grid__cell-meta");
            copy.Add(meta);
            cell.Add(copy);

            var rarity = new VisualElement();
            rarity.AddToClassList("inv-grid__cell-rarity");
            rarity.AddToClassList("sm-item-rarity");
            rarity.AddToClassList($"sm-item-rarity--{item.RarityKey}");
            cell.Add(rarity);

            var familyBadge = new Label(item.WeaponFamilyLabel);
            familyBadge.AddToClassList("inv-grid__cell-family");
            familyBadge.AddToClassList("sm-item-badge");
            familyBadge.AddToClassList("sm-item-badge--family");
            familyBadge.AddToClassList($"inv-grid__cell-family--{item.WeaponFamilyKey}");
            familyBadge.style.display = string.IsNullOrWhiteSpace(item.WeaponFamilyKey)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            cell.Add(familyBadge);

            if (item.ShowsIdentityBadge)
            {
                var identity = new Label(FormatIdentityLabel(item.IdentityLabel));
                identity.AddToClassList("inv-grid__cell-identity");
                identity.AddToClassList("sm-item-identity");
                identity.AddToClassList($"sm-item-identity--{item.IdentityKey}");
                cell.Add(identity);
            }

            if (item.IsEquipped)
            {
                var eq = new VisualElement();
                eq.AddToClassList("inv-grid__cell-equipped");
                eq.AddToClassList("sm-item-state");
                cell.Add(eq);
            }

            var rarityTooltip = item.IsLaunchSupportedRarity
                ? FormatRarityLabel(item.RarityKey)
                : $"{item.RawRarityKey} -> {FormatRarityLabel(item.RarityKey)}";
            cell.tooltip = $"{item.IconKey} ({item.SlotKey}/{item.WeaponFamilyKey}) · {rarityTooltip}{(item.IsEquipped ? " · equipped" : "")}";
            cell.RegisterCallback<ClickEvent>(_ => _actions?.OnItemSelected(item.ItemInstanceId));
            _itemGrid.Add(cell);
        }
    }

    private void RenderDetail(InventoryDetailViewState? detail)
    {
        if (detail == null)
        {
            _currentDetailItemInstanceId = string.Empty;
            _detailAffixes.Clear();
            if (_detailNameLabel != null) _detailNameLabel.text = string.Empty;
            if (_detailMetaLabel != null) _detailMetaLabel.text = string.Empty;
            if (_detailSetBonusLabel != null) _detailSetBonusLabel.text = string.Empty;
            _detailIconImage.image = null;
            _detailCrossLinks?.Clear();
            ApplyEquipCta(null);
            return;
        }

        _currentDetailItemInstanceId = detail.ItemInstanceId;
        if (detail.IconSprite != null)
        {
            _detailIcon.style.backgroundImage = new StyleBackground(detail.IconSprite);
            _detailIconImage.image = detail.IconSprite;
        }
        if (_detailNameLabel != null) _detailNameLabel.text = detail.Name;
        if (_detailMetaLabel != null)
        {
            _detailMetaLabel.text = string.IsNullOrWhiteSpace(detail.WeaponFamilyLabel)
                ? $"{detail.SlotLabel} / {FormatRarityLabel(detail.RarityKey)}"
                : $"{detail.SlotLabel} / {FormatRarityLabel(detail.RarityKey)} / {detail.WeaponFamilyLabel}";
        }
        if (_detailSetBonusLabel != null)
        {
            _detailSetBonusLabel.text = detail.SetBonusTier;
            _detailSetBonusLabel.style.display = string.IsNullOrWhiteSpace(detail.SetBonusTier)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
        RenderCrossLinks(detail.CrossLinks);
        ApplyEquipCta(null);

        _detailAffixes.Clear();
        RenderDetailProfileRows(detail);
        string? previousGroup = null;
        foreach (var affix in detail.Affixes)
        {
            if (previousGroup != affix.GroupKey)
            {
                var header = new Label(FormatAffixGroupLabel(affix.GroupKey));
                header.AddToClassList("inv-detail__affix-group");
                header.AddToClassList($"inv-detail__affix-group--{affix.GroupKey}");
                _detailAffixes.Add(header);
                previousGroup = affix.GroupKey;
            }

            var row = new VisualElement();
            row.AddToClassList("inv-detail__affix-row");
            row.AddToClassList("sm-affix-row");
            row.AddToClassList($"inv-detail__affix-row--{affix.GroupKey}");
            row.AddToClassList($"sm-affix-row--{affix.GroupKey}");

            var nameEl = new Label(affix.Name);
            nameEl.AddToClassList("inv-detail__affix-name");
            row.Add(nameEl);

            var value = new Label(affix.ValueRange);
            value.AddToClassList("inv-detail__affix-value");
            row.Add(value);

            _detailAffixes.Add(row);
        }
    }

    private void RenderCompare(InventoryCompareLaneViewState? compare)
    {
        if (_compareLane == null)
        {
            return;
        }

        _compareLane.style.display = compare == null ? DisplayStyle.None : DisplayStyle.Flex;
        _compareRows?.Clear();
        if (compare == null)
        {
            ApplyEquipCta(null);
            return;
        }

        if (_compareTargetHeroLabel != null) _compareTargetHeroLabel.text = compare.TargetHeroLabel;
        if (_compareTargetMetaLabel != null) _compareTargetMetaLabel.text = compare.TargetHeroMetaLabel;
        if (_compareSelectedLabel != null) _compareSelectedLabel.text = compare.SelectedItemLabel;
        if (_compareSelectedMetaLabel != null) _compareSelectedMetaLabel.text = compare.SelectedItemMetaLabel;
        if (_compareSelectedIcon != null && compare.SelectedItemIconSprite != null)
        {
            _compareSelectedIcon.style.backgroundImage = new StyleBackground(compare.SelectedItemIconSprite);
            _detailIcon.style.backgroundImage = new StyleBackground(compare.SelectedItemIconSprite);
            _detailIconImage.image = compare.SelectedItemIconSprite;
        }

        if (_compareEquippedLabel != null) _compareEquippedLabel.text = compare.EquippedItemLabel;
        if (_compareEquippedMetaLabel != null) _compareEquippedMetaLabel.text = compare.EquippedItemMetaLabel;
        if (_compareEquippedIcon != null && compare.EquippedItemIconSprite != null)
        {
            _compareEquippedIcon.style.backgroundImage = new StyleBackground(compare.EquippedItemIconSprite);
        }

        if (_compareOwnerLabel != null) _compareOwnerLabel.text = compare.EquippedOwnerLabel;
        if (_compareEquipStatusLabel != null) _compareEquipStatusLabel.text = compare.EquipCta.StatusText;

        if (_compareRows != null)
        {
            foreach (var rowState in compare.Rows)
            {
                var row = new VisualElement();
                row.AddToClassList("inv-compare__row");
                row.AddToClassList($"inv-compare__row--{rowState.ToneKey}");

                var key = new Label(rowState.Label);
                key.AddToClassList("inv-compare__row-key");
                row.Add(key);

                var selected = new Label(rowState.SelectedValue);
                selected.AddToClassList("inv-compare__row-selected");
                row.Add(selected);

                var equipped = new Label(rowState.EquippedValue);
                equipped.AddToClassList("inv-compare__row-equipped");
                row.Add(equipped);

                _compareRows.Add(row);
            }
        }

        ApplyEquipCta(compare.EquipCta);
    }

    private void ApplyEquipCta(InventoryEquipCtaViewState? cta)
    {
        if (_equipButton != null)
        {
            _equipButton.SetEnabled(cta?.CanEquip == true);
            _equipButton.tooltip = cta?.TooltipText ?? string.Empty;
            _equipButton.text = cta?.Label ?? "장착";
        }

        if (_compareButton != null)
        {
            _compareButton.SetEnabled(!string.IsNullOrEmpty(_currentDetailItemInstanceId));
            _compareButton.text = "비교";
        }
    }

    private void RenderDetailProfileRows(InventoryDetailViewState detail)
    {
        var header = new Label("장비 정보");
        header.AddToClassList("inv-detail__affix-group");
        header.AddToClassList("inv-detail__affix-group--profile");
        _detailAffixes.Add(header);

        AddDetailMetricRow("슬롯", detail.SlotLabel);
        AddDetailMetricRow("희귀도", FormatRarityLabel(detail.RarityKey));
        if (!string.IsNullOrWhiteSpace(detail.WeaponFamilyLabel))
        {
            AddDetailMetricRow("계열", detail.WeaponFamilyLabel);
        }

        if (detail.ShowsIdentityBadge)
        {
            AddDetailMetricRow("정체성", FormatIdentityLabel(detail.IdentityLabel));
        }

        AddDetailMetricRow("재가공", detail.CanRefit ? "가능" : "잠김");
    }

    private void AddDetailMetricRow(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var row = new VisualElement();
        row.AddToClassList("inv-detail__affix-row");
        row.AddToClassList("sm-affix-row");
        row.AddToClassList("inv-detail__affix-row--profile");

        var nameEl = new Label(label);
        nameEl.AddToClassList("inv-detail__affix-name");
        row.Add(nameEl);

        var valueEl = new Label(value);
        valueEl.AddToClassList("inv-detail__affix-value");
        row.Add(valueEl);

        _detailAffixes.Add(row);
    }

    private void RenderCrossLinks(System.Collections.Generic.IReadOnlyList<string>? crossLinks)
    {
        if (_detailCrossLinks == null)
        {
            return;
        }

        _detailCrossLinks.Clear();
        _detailCrossLinks.style.display = crossLinks == null || crossLinks.Count == 0
            ? DisplayStyle.None
            : DisplayStyle.Flex;
        if (crossLinks == null)
        {
            return;
        }

        foreach (var link in crossLinks)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var chip = new Label(link);
            chip.AddToClassList("sm-cd-tag");
            chip.AddToClassList("sm-item-badge");
            _detailCrossLinks.Add(chip);
        }
    }

    private void HandleEquipClicked()
    {
        if (!string.IsNullOrEmpty(_currentDetailItemInstanceId))
            _actions?.OnEquipItem(_currentDetailItemInstanceId);
    }

    private void HandleCompareClicked()
    {
        if (!string.IsNullOrEmpty(_currentDetailItemInstanceId))
            _actions?.OnCompareItem(_currentDetailItemInstanceId);
    }

    private static string FormatRarityLabel(string rarityKey)
    {
        return (rarityKey ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "common" => "일반",
            "rare" => "희귀",
            "epic" => "영웅",
            "magic" => "희귀",
            "legendary" => "영웅",
            "-" => "-",
            "" => "-",
            _ => "일반",
        };
    }

    private static string FormatIdentityLabel(string identityLabel)
    {
        return (identityLabel ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "NAMED" => "이름 있는 장비",
            "SIGNATURE" => "전용 장비",
            _ => identityLabel,
        };
    }

    private static string FormatAffixGroupLabel(string groupKey)
    {
        return (groupKey ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "implicit" => "고유 속성",
            "prefix" => "접두 속성",
            "suffix" => "접미 속성",
            "profile" => "장비 정보",
            _ => "속성",
        };
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
