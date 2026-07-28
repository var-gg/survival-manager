using System;
using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit surface View — item-level quality-floor refit.
/// UXML container: StandeePortrait / SelectedItemName / EquippedHeroLabel / EchoIcon / AffixList /
/// InventoryPool / RefitCostLabel. affix row는 실제 magnitude + percentile + min/max context를 표기한다.
/// </summary>
public sealed class EquipmentRefitView : IEquipmentRefitView
{
    private readonly VisualElement _standeePortrait;
    private readonly Label? _selectedItemName;
    private readonly Label? _equippedHeroLabel;
    private readonly VisualElement _echoIcon;
    private readonly VisualElement _affixList;
    private readonly VisualElement _inventoryPool;
    private readonly Label _refitCostLabel;
    private readonly Label _panelTitle;
    private readonly Label _operationSelectorLabel;
    private readonly Button _reforgeOperationButton;
    private readonly Button _sealOperationButton;
    private readonly Label _sealOperationReason;
    private readonly Label _craftStatusLabel;
    private readonly Label _quoteCostLabel;
    private readonly VisualElement _confirmation;
    private readonly Label _confirmationMessage;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;
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
        // wave-57 fix: USS의 .erp-root position absolute가 TemplateContainer size 0과 결합되어
        // 부분만 차지하던 issue. Recruit (.rcp-root { flex-grow: 1 }) 패턴 inline 강제로 USS 우회.
        _modalRoot.style.position = Position.Relative;
        _modalRoot.style.flexGrow = 1;
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
        _panelTitle = root.Q<Label>("RefitPanelTitle")
            ?? throw new ArgumentException("RefitPanelTitle 못 찾음");
        _operationSelectorLabel = root.Q<Label>("OperationSelectorLabel")
            ?? throw new ArgumentException("OperationSelectorLabel 못 찾음");
        _reforgeOperationButton = root.Q<Button>("RefitOperationReforgeButton")
            ?? throw new ArgumentException("RefitOperationReforgeButton 못 찾음");
        _sealOperationButton = root.Q<Button>("RefitOperationSealButton")
            ?? throw new ArgumentException("RefitOperationSealButton 못 찾음");
        _sealOperationReason = root.Q<Label>("SealUnavailableReason")
            ?? throw new ArgumentException("SealUnavailableReason 못 찾음");
        _craftStatusLabel = root.Q<Label>("CraftStatusLabel")
            ?? throw new ArgumentException("CraftStatusLabel 못 찾음");
        _quoteCostLabel = root.Q<Label>("QuoteCostLabel")
            ?? throw new ArgumentException("QuoteCostLabel 못 찾음");
        _confirmation = root.Q<VisualElement>("CraftConfirmation")
            ?? throw new ArgumentException("CraftConfirmation 못 찾음");
        _confirmationMessage = root.Q<Label>("CraftConfirmationMessage")
            ?? throw new ArgumentException("CraftConfirmationMessage 못 찾음");
        _confirmButton = root.Q<Button>("CraftConfirmButton")
            ?? throw new ArgumentException("CraftConfirmButton 못 찾음");
        _cancelButton = root.Q<Button>("CraftCancelButton")
            ?? throw new ArgumentException("CraftCancelButton 못 찾음");
        // item 컨텍스트 라벨 — 없어도 preview가 깨지지 않게 nullable
        _selectedItemName = root.Q<Label>("SelectedItemName");
        _equippedHeroLabel = root.Q<Label>("EquippedHeroLabel");
    }

    public void Bind(IEquipmentRefitActions actions)
    {
        _actions = actions;
        if (_refitButton != null)
        {
            _refitButton.clicked -= HandleCraftRequested;
            _refitButton.clicked += HandleCraftRequested;
        }
        _reforgeOperationButton.clicked -= HandleReforgeSelected;
        _reforgeOperationButton.clicked += HandleReforgeSelected;
        _sealOperationButton.clicked -= HandleSealSelected;
        _sealOperationButton.clicked += HandleSealSelected;
        _confirmButton.clicked -= HandleCraftConfirmed;
        _confirmButton.clicked += HandleCraftConfirmed;
        _cancelButton.clicked -= HandleCraftCancelled;
        _cancelButton.clicked += HandleCraftCancelled;
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
        _panelTitle.text = state.PanelTitle;
        _operationSelectorLabel.text = state.OperationSelectorLabel;
        _reforgeOperationButton.text = state.ReforgeOperationLabel;
        _sealOperationButton.text = state.SealOperationLabel;
        _reforgeOperationButton.SetEnabled(state.ReforgeOperationSelectable);
        _sealOperationButton.SetEnabled(state.SealOperationSelectable);
        _reforgeOperationButton.EnableInClassList(
            "erp-operation-selector__button--selected",
            state.SelectedOperation == CraftOperationKindValue.Reforge);
        _sealOperationButton.EnableInClassList(
            "erp-operation-selector__button--selected",
            state.SelectedOperation == CraftOperationKindValue.Seal);
        _sealOperationReason.text = state.SealOperationReason;
        _sealOperationReason.style.display = string.IsNullOrWhiteSpace(
            state.SealOperationReason)
            ? DisplayStyle.None
            : DisplayStyle.Flex;

        _craftStatusLabel.text = state.SelectedOperationStatusMessage;
        _quoteCostLabel.text = state.SelectedOperationCostLabel;
        _refitButton?.SetEnabled(state.SelectedOperationCanPurchase);
        if (_refitButton != null)
        {
            _refitButton.tooltip = state.SelectedOperationStatusMessage;
            _refitButton.style.display = state.ConfirmationVisible
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
        _confirmation.style.display = state.ConfirmationVisible
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        _confirmationMessage.text = state.ConfirmationMessage;
        _confirmButton.text = state.ConfirmLabel;
        _cancelButton.text = state.CancelLabel;
        _confirmButton.SetEnabled(
            state.ConfirmationVisible && state.SelectedOperationCanPurchase);

        if (state.EchoSprite != null) _echoIcon.style.backgroundImage = new StyleBackground(state.EchoSprite);
        _refitCostLabel.text = state.CraftActionLabel;

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
                var groupHeader = new Label(affix.GroupLabel);
                groupHeader.AddToClassList("erp-affix-group");
                groupHeader.AddToClassList($"erp-affix-group--{affix.GroupKey}");
                _affixList.Add(groupHeader);
                previousGroup = affix.GroupKey;
            }

            var row = new VisualElement();
            row.AddToClassList("erp-affix-row");
            row.AddToClassList("sm-affix-row");
            row.AddToClassList($"erp-affix-row--{affix.GroupKey}");
            row.AddToClassList($"sm-affix-row--{affix.GroupKey}");
            var icon = new VisualElement();
            icon.AddToClassList("erp-affix-row__icon");
            if (affix.IconSprite != null) icon.style.backgroundImage = new StyleBackground(affix.IconSprite);
            row.Add(icon);

            var content = new VisualElement();
            content.AddToClassList("erp-affix-row__content");
            var header = new VisualElement();
            header.AddToClassList("erp-affix-row__header");

            // affix 이름 — AffixDefinition.NameKey resolved
            var name = new Label(affix.Name);
            name.AddToClassList("erp-affix-row__name");
            header.Add(name);

            // Persisted roll quality와 legacy baseline fallback을 명시적으로 구분한다.
            var rollContext = new Label(affix.RollContextText);
            rollContext.AddToClassList("erp-affix-row__roll-context");
            header.Add(rollContext);
            content.Add(header);

            // modifier별 독립 line이라 tradeoff의 downside도 생략되거나 한 줄에 뭉개지지 않는다.
            var effects = new VisualElement();
            effects.AddToClassList("erp-affix-row__effects");
            foreach (var effectText in affix.EffectLines)
            {
                var effect = new Label(effectText);
                effect.AddToClassList("erp-affix-row__effect");
                effects.Add(effect);
            }
            content.Add(effects);
            row.Add(content);

            var lockButton = new Button
            {
                name = $"AffixLock_{affix.AffixId}",
                text = affix.LockLabel,
            };
            lockButton.AddToClassList("erp-affix-row__lock");
            lockButton.EnableInClassList(
                "erp-affix-row__lock--locked",
                affix.IsLocked);
            lockButton.SetEnabled(affix.LockToggleEnabled);
            lockButton.clicked += () =>
                _actions?.OnAffixLockToggled(affix.AffixId);
            row.Add(lockButton);

            row.tooltip = $"{affix.AffixId} [{affix.GroupKey}]";
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

    private void HandleReforgeSelected() =>
        _actions?.OnOperationSelected(CraftOperationKindValue.Reforge);

    private void HandleSealSelected() =>
        _actions?.OnOperationSelected(CraftOperationKindValue.Seal);

    private void HandleCraftRequested() => _actions?.OnCraftRequested();

    private void HandleCraftConfirmed() => _actions?.OnCraftConfirmed();

    private void HandleCraftCancelled() => _actions?.OnCraftCancelled();
}

public interface IEquipmentRefitActions
{
    void OnPoolItemSelected(string itemInstanceId);
    void OnOperationSelected(CraftOperationKindValue operation);
    void OnAffixLockToggled(string affixId);
    void OnCraftRequested();
    void OnCraftConfirmed();
    void OnCraftCancelled();
}

/// <summary>
/// EquipmentRefit View 계약 — presenter가 의존하는 표면(bind/modal/render)만. 콘크리트 EquipmentRefitView는
/// VisualElement에 묶이지만 presenter는 이 인터페이스만 알면 되어 headless 테스트에서 fake view로 구동한다.
/// </summary>
public interface IEquipmentRefitView
{
    void Bind(IEquipmentRefitActions actions);
    void BindClose(Action close);
    void Open();
    void Close();
    void Render(EquipmentRefitViewState state);
}
