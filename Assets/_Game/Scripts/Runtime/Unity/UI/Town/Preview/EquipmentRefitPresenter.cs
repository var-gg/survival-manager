using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Results;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit Presenter — selected item의 quality floor + affix list + inventory pool → ViewState.
///
/// Sprint 3 wire: Profile.Inventory (InventoryItemRecord[]) → pool. selected item의 AffixIds → affix list.
/// ItemBaseDefinition.WeaponFamilyTag / RarityTier / IdentityKind를 공통 presentation policy로 변환.
///
/// affix group (implicit/prefix/suffix)은 AffixDefinition.Tier에서 read.
///
/// 워크플로우: 사용자가 item 선택 + 다음 effective floor 구매 → SessionState.RefitItem → stat 즉시 반영.
/// </summary>
public sealed class EquipmentRefitPresenter : IEquipmentRefitActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);
    public delegate string TextResolver(
        string tableCollection,
        string entryKey,
        string fallback,
        params object[] arguments);

    // headless conformance(Phase 2 Stage 2): GameSessionRoot(MonoBehaviour) 대신 순수 GameSessionState +
    // ICombatContentLookup, 콘크리트 EquipmentRefitView 대신 IEquipmentRefitView, ContentTextResolver
    // (→GameLocalizationController MonoBehaviour) 대신 이름 resolver delegate를 받아 씬·엔진 없이 구동.
    private readonly GameSessionState _session;
    private readonly ICombatContentLookup _lookup;
    private readonly IEquipmentRefitView _view;
    private readonly Func<string, string> _itemName;
    private readonly Func<string, string> _affixName;
    private readonly Func<string, string, string> _characterName;
    private readonly SpriteLoader _itemIconSprite;
    private readonly SpriteLoader _affixIconSprite;
    private readonly SpriteLoader _currencySprite;
    private readonly SpriteLoader _portraitLoader;
    private readonly EquipmentRefitText _text;
    private readonly HashSet<string> _sealedAffixIds = new(StringComparer.Ordinal);
    private string _selectedItemInstanceId = string.Empty;
    private CraftOperationKindValue _selectedOperation = CraftOperationKindValue.Reforge;
    private bool _confirmationVisible;
    private OperationFailure? _operationFailure;

    public EquipmentRefitPresenter(
        GameSessionState session,
        ICombatContentLookup lookup,
        IEquipmentRefitView view,
        Func<string, string> itemName,
        Func<string, string> affixName,
        Func<string, string, string> characterName,
        SpriteLoader? itemIconSprite = null,
        SpriteLoader? currencySprite = null,
        SpriteLoader? portraitLoader = null,
        SpriteLoader? affixIconSprite = null,
        TextResolver? uiText = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _itemName = itemName ?? throw new ArgumentNullException(nameof(itemName));
        _affixName = affixName ?? throw new ArgumentNullException(nameof(affixName));
        _characterName = characterName ?? throw new ArgumentNullException(nameof(characterName));
        _itemIconSprite = itemIconSprite ?? (_ => null);
        _affixIconSprite = affixIconSprite ?? itemIconSprite ?? (_ => null);
        _currencySprite = currencySprite ?? (_ => null);
        _portraitLoader = portraitLoader ?? (_ => null);
        _text = new EquipmentRefitText(uiText);
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

    void IEquipmentRefitActions.OnPoolItemSelected(string itemInstanceId)
    {
        _selectedItemInstanceId = itemInstanceId;
        _selectedOperation = CraftOperationKindValue.Reforge;
        _sealedAffixIds.Clear();
        _confirmationVisible = false;
        _operationFailure = null;
        Refresh();
    }

    void IEquipmentRefitActions.OnOperationSelected(CraftOperationKindValue operation)
    {
        if (operation is not (CraftOperationKindValue.Reforge or CraftOperationKindValue.Seal))
        {
            return;
        }

        var state = BuildState();
        if ((operation == CraftOperationKindValue.Reforge && !state.ReforgeOperationSelectable)
            || (operation == CraftOperationKindValue.Seal && !state.SealOperationSelectable))
        {
            return;
        }

        _selectedOperation = operation;
        _confirmationVisible = false;
        _operationFailure = null;
        Refresh();
    }

    void IEquipmentRefitActions.OnAffixLockToggled(string affixId)
    {
        var selectedItem = ResolveSelectedItem();
        if (_selectedOperation != CraftOperationKindValue.Seal
            || selectedItem == null
            || !selectedItem.AffixIds.Contains(affixId, StringComparer.Ordinal))
        {
            return;
        }

        if (!_sealedAffixIds.Add(affixId))
        {
            _sealedAffixIds.Remove(affixId);
        }

        _confirmationVisible = false;
        _operationFailure = null;
        Refresh();
    }

    void IEquipmentRefitActions.OnCraftRequested()
    {
        var state = BuildState();
        if (!state.SelectedOperationCanPurchase)
        {
            return;
        }

        _confirmationVisible = true;
        _operationFailure = null;
        Refresh();
    }

    void IEquipmentRefitActions.OnCraftCancelled()
    {
        _confirmationVisible = false;
        Refresh();
    }

    void IEquipmentRefitActions.OnCraftConfirmed()
    {
        var state = BuildState();
        var selectedItem = ResolveSelectedItem();
        if (!_confirmationVisible
            || !state.SelectedOperationCanPurchase
            || selectedItem == null)
        {
            _confirmationVisible = false;
            Refresh();
            return;
        }

        var sealedAffixIds = selectedItem.AffixIds
            .Where(_sealedAffixIds.Contains)
            .ToArray();
        var result = _selectedOperation == CraftOperationKindValue.Seal
            ? _session.SealItem(selectedItem.ItemInstanceId, sealedAffixIds)
            : _session.RefitItem(selectedItem.ItemInstanceId);

        _confirmationVisible = false;
        _operationFailure = result.IsSuccess ? null : result.Failure;
        Refresh();
    }

    private SM.Persistence.Abstractions.Models.InventoryItemRecord? ResolveSelectedItem()
    {
        var inventory = _session.Profile.Inventory;
        if (!string.IsNullOrEmpty(_selectedItemInstanceId))
        {
            var match = inventory.FirstOrDefault(i =>
                string.Equals(i.ItemInstanceId, _selectedItemInstanceId, StringComparison.Ordinal));
            if (match != null) return match;
        }
        // wave-visual-qa: 첫 inventory item이 Common이면 affix list가 1-2줄로 빈약 보임.
        // affix 가장 많은 item을 default selected로 — 시연 cut의 시각 quality 보장.
        return inventory
            .OrderByDescending(i => i.AffixIds?.Count ?? 0)
            .FirstOrDefault();
    }

    public EquipmentRefitViewState BuildState()
    {
        var session = _session;
        var inventory = session.Profile.Inventory;
        var lookup = _lookup;
        var selectedItem = ResolveSelectedItem();
        if (selectedItem != null && string.IsNullOrEmpty(_selectedItemInstanceId))
        {
            _selectedItemInstanceId = selectedItem.ItemInstanceId;
        }

        var noItemQuote = SM.Meta.Services.RefitQuote.Unavailable(
            OperationFailure.Refusal(
                SessionOperationFailureCodes.RefitItemSelectionRequired,
                "Select an item before requesting a Refit quote."));
        var refitQuote = selectedItem == null
            ? noItemQuote
            : session.GetRefitQuote(selectedItem.ItemInstanceId);
        var canonicalSealedAffixIds = selectedItem?.AffixIds
            .Where(_sealedAffixIds.Contains)
            .ToArray()
            ?? Array.Empty<string>();
        var sealOperationQuote = selectedItem == null
            ? noItemQuote
            : session.GetSealQuote(selectedItem.ItemInstanceId, Array.Empty<string>());
        var sealQuote = selectedItem == null
            ? noItemQuote
            : session.GetSealQuote(selectedItem.ItemInstanceId, canonicalSealedAffixIds);
        var reforgeOperationSelectable = refitQuote.CanPurchase;
        var sealOperationSelectable = sealOperationQuote.CanPurchase;
        if (_selectedOperation == CraftOperationKindValue.Reforge
            && !reforgeOperationSelectable
            && sealOperationSelectable)
        {
            _selectedOperation = CraftOperationKindValue.Seal;
        }
        else if (_selectedOperation == CraftOperationKindValue.Seal
                 && !sealOperationSelectable
                 && reforgeOperationSelectable)
        {
            _selectedOperation = CraftOperationKindValue.Reforge;
        }

        var refitPurchaseBlockFailure = selectedItem == null
            ? noItemQuote.Failure
            : session.GetRefitPurchaseBlockFailure(selectedItem.ItemInstanceId);
        var sealPurchaseBlockFailure = selectedItem == null
            ? noItemQuote.Failure
            : session.GetSealPurchaseBlockFailure(
                selectedItem.ItemInstanceId,
                canonicalSealedAffixIds);
        var selectedQuote = _selectedOperation == CraftOperationKindValue.Seal
            ? sealQuote
            : refitQuote;
        var selectedPurchaseBlockFailure = _selectedOperation == CraftOperationKindValue.Seal
            ? sealPurchaseBlockFailure
            : refitPurchaseBlockFailure;
        var selectedOperationCanPurchase = selectedQuote.CanPurchase
                                           && selectedPurchaseBlockFailure == null;

        // Pool — Profile.Inventory 전체. ItemBaseDefinition으로 이름 / slot / rarity 보강.
        var pool = inventory
            .Select(item =>
            {
                var slotKey = "weapon";
                var iconKey = item.ItemBaseId;
                var familyKey = string.Empty;
                var presentation = EquipmentPresentationPolicy.Build(slotKey, familyKey, "Common", "Baseline", Array.Empty<string>());
                if (lookup.TryGetItemDefinition(item.ItemBaseId, out var baseDef))
                {
                    slotKey = ResolveSlotKey(baseDef.SlotType);
                    familyKey = ResolveFamilyKey(baseDef);
                    iconKey = string.IsNullOrWhiteSpace(baseDef.IconId) ? item.ItemBaseId : baseDef.IconId;
                    presentation = EquipmentPresentationPolicy.Build(
                        slotKey,
                        familyKey,
                        InventoryItemGradePresentation.Resolve(item, baseDef).ToString(),
                        baseDef.IdentityKind.ToString(),
                        EnumerateCraftOperations(baseDef));
                }
                return new EquipmentRefitPoolRowViewState(
                    ItemInstanceId: item.ItemInstanceId,
                    Name: _itemName(item.ItemBaseId),
                    SlotKey: presentation.SlotKey,
                    SlotLabel: presentation.SlotLabel,
                    FamilyKey: presentation.FamilyKey,
                    FamilyLabel: presentation.FamilyLabel,
                    IconSprite: _itemIconSprite(iconKey) ?? _itemIconSprite(item.ItemBaseId) ?? _affixIconSprite(iconKey),
                    RarityKey: presentation.RarityKey,
                    RawRarityKey: presentation.RawRarityKey,
                    IdentityKey: presentation.IdentityKey,
                    IdentityLabel: presentation.IdentityLabel,
                    ShowsIdentityBadge: presentation.ShowsIdentityBadge,
                    CanRefit: presentation.CanRefit,
                    IsLaunchSupportedRarity: presentation.IsLaunchSupportedRarity,
                    IsSelected: string.Equals(item.ItemInstanceId, _selectedItemInstanceId, StringComparison.Ordinal));
            })
            .ToList();

        // Affix list — selected item의 AffixIds. group은 AffixDefinition.Tier에서 read (index 추정 폐기).
        // persisted roll이면 runtime과 같은 package scaling을 표시한다.
        // legacy save fallback은 definition package를 표시하되 roll quality로 가장하지 않는다.
        var affixes = new List<EquipmentRefitAffixRowViewState>();
        if (selectedItem != null)
        {
            for (var i = 0; i < selectedItem.AffixIds.Count; i++)
            {
                var affixId = selectedItem.AffixIds[i];
                var group = "prefix";
                var category = "utility";
                IReadOnlyList<string> effectLines = Array.Empty<string>();
                var rollContextText = "—";
                if (lookup.TryGetAffixDefinition(affixId, out var affixDef))
                {
                    group = affixDef.Tier.ToString().ToLowerInvariant();
                    category = affixDef.Category.ToString().ToLowerInvariant();
                    var readout = AffixMagnitudePresentation.Build(
                        selectedItem,
                        affixDef);
                    effectLines = readout.Effects
                        .Select(_text.FormatAffixEffect)
                        .ToArray();
                    rollContextText = _text.FormatAffixRollContext(readout);
                }
                affixes.Add(new EquipmentRefitAffixRowViewState(
                    AffixId: affixId,
                    GroupKey: group,
                    GroupLabel: _text.AffixGroupHeader(group),
                    CategoryKey: category,
                    Name: _affixName(affixId),
                    EffectLines: effectLines,
                    RollContextText: rollContextText,
                    IconSprite: _affixIconSprite(affixId),
                    IsLocked: _sealedAffixIds.Contains(affixId),
                    LockToggleEnabled: _selectedOperation == CraftOperationKindValue.Seal
                                       && sealOperationSelectable,
                    LockLabel: _text.LockLabel(_sealedAffixIds.Contains(affixId))));
            }
        }

        // 좌측 컨텍스트 — 선택 item 정체성 + 장착 hero (InventoryItemRecord.EquippedHeroId 파생).
        var selectedItemName = "—";
        var selectedSlotLabel = "—";
        var selectedRarityKey = "common";
        var selectedFamilyKey = string.Empty;
        var selectedFamilyLabel = string.Empty;
        var selectedIdentityKey = "baseline";
        var selectedIdentityLabel = string.Empty;
        var selectedShowsIdentityBadge = false;
        var selectedCanRefit = false;
        var equippedHeroLabel = _text.UnequippedLabel;
        Texture2D? equippedHeroPortrait = null;
        if (selectedItem != null)
        {
            selectedCanRefit = refitQuote.CanPurchase;
            selectedItemName = _itemName(selectedItem.ItemBaseId);
            if (lookup.TryGetItemDefinition(selectedItem.ItemBaseId, out var baseDef))
            {
                var presentation = EquipmentPresentationPolicy.Build(
                    ResolveSlotKey(baseDef.SlotType),
                    ResolveFamilyKey(baseDef),
                    InventoryItemGradePresentation.Resolve(selectedItem, baseDef).ToString(),
                    baseDef.IdentityKind.ToString(),
                    EnumerateCraftOperations(baseDef));
                selectedSlotLabel = presentation.SlotLabel;
                selectedRarityKey = presentation.RarityKey;
                selectedFamilyKey = presentation.FamilyKey;
                selectedFamilyLabel = presentation.FamilyLabel;
                selectedIdentityKey = presentation.IdentityKey;
                selectedIdentityLabel = presentation.IdentityLabel;
                selectedShowsIdentityBadge = presentation.ShowsIdentityBadge;
                selectedCanRefit = presentation.CanRefit && refitQuote.CanPurchase;
            }
            if (!string.IsNullOrEmpty(selectedItem.EquippedHeroId))
            {
                var hero = session.Profile.Heroes
                    .FirstOrDefault(h => string.Equals(h.HeroId, selectedItem.EquippedHeroId, StringComparison.Ordinal));
                // hero.Name은 SessionProfileSync가 raw archetype.Id ("warden") 박아둠.
                // ContentTextResolver로 character → archetype localized 표시명 fallback chain 사용.
                var heroName = hero != null
                    ? _characterName(hero.CharacterId, hero.ArchetypeId)
                    : selectedItem.EquippedHeroId;
                equippedHeroLabel = _text.Equipped(heroName);
                // uxqa1: EquippedHeroId는 save instance id(hero-1/GUID)라 포트레잇 해석 불가 —
                // 스탠디가 영구 빈 박스로 렌더되던 결함. 이름과 같은 CharacterId→ArchetypeId 키 사용.
                var portraitKey = !string.IsNullOrWhiteSpace(hero?.CharacterId) ? hero!.CharacterId
                    : !string.IsNullOrWhiteSpace(hero?.ArchetypeId) ? hero!.ArchetypeId
                    : selectedItem.EquippedHeroId;
                equippedHeroPortrait = _portraitLoader(portraitKey);
            }
        }

        return new EquipmentRefitViewState(
            SelectedItemName: selectedItemName,
            SelectedItemSlotLabel: selectedSlotLabel,
            SelectedItemRarityKey: selectedRarityKey,
            SelectedItemFamilyKey: selectedFamilyKey,
            SelectedItemFamilyLabel: selectedFamilyLabel,
            SelectedItemIdentityKey: selectedIdentityKey,
            SelectedItemIdentityLabel: selectedIdentityLabel,
            SelectedItemShowsIdentityBadge: selectedShowsIdentityBadge,
            SelectedItemCanRefit: selectedCanRefit,
            EquippedHeroLabel: equippedHeroLabel,
            EquippedHeroPortrait: equippedHeroPortrait,
            EchoSprite: _currencySprite("echo"),
            CurrentQualityPercent: ToPercent(refitQuote.CurrentPercentileQ64),
            NextFloorPercent: ToPercent(refitQuote.TargetFloorQ64),
            RefitCost: refitQuote.EchoCost,
            RefitMaxed: refitQuote.RefitMaxed,
            RefitStatusMessage: _text.BuildOperationStatus(
                refitQuote,
                CraftOperationKindValue.Reforge,
                lockedAffixCount: 0,
                totalAffixCount: selectedItem?.AffixIds.Count ?? 0,
                purchaseBlockFailure: refitPurchaseBlockFailure),
            Affixes: affixes,
            Pool: pool,
            SelectedOperation: _selectedOperation,
            ReforgeOperationSelectable: reforgeOperationSelectable,
            SealOperationSelectable: sealOperationSelectable,
            SealOperationReason: sealOperationSelectable
                ? string.Empty
                : _text.SealUnavailable(
                    _text.LocalizeFailure(
                        sealOperationQuote.Failure,
                        CraftOperationKindValue.Seal,
                        sealOperationQuote)),
            SelectedOperationCanPurchase: selectedOperationCanPurchase,
            SelectedOperationCost: selectedQuote.EchoCost,
            SelectedOperationCostLabel: _text.CostLabel(selectedQuote.EchoCost),
            SelectedOperationStatusMessage: _operationFailure != null
                ? _text.LocalizeFailure(
                    _operationFailure,
                    _selectedOperation,
                    selectedQuote)
                : _text.BuildOperationStatus(
                    selectedQuote,
                    _selectedOperation,
                    canonicalSealedAffixIds.Length,
                    selectedItem?.AffixIds.Count ?? 0,
                    selectedPurchaseBlockFailure),
            ConfirmationVisible: _confirmationVisible && selectedOperationCanPurchase,
            PanelTitle: _text.PanelTitle,
            OperationSelectorLabel: _text.OperationSelectorLabel,
            ReforgeOperationLabel: _text.ReforgeOperationLabel,
            SealOperationLabel: _text.SealOperationLabel,
            CraftActionLabel: _text.BuildCraftActionLabel(
                _selectedOperation,
                selectedQuote.EchoCost),
            ConfirmationMessage: _text.Confirmation(
                selectedQuote.EchoCost,
                _selectedOperation),
            ConfirmLabel: _text.ConfirmLabel,
            CancelLabel: _text.CancelLabel);
    }

    private static double ToPercent(ulong probabilityQ64)
        => probabilityQ64 / (double)ulong.MaxValue * 100d;

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
            ItemSlotType.Weapon when !string.IsNullOrWhiteSpace(item.WeaponFamilyTag) => item.WeaponFamilyTag,
            ItemSlotType.Weapon when item.Id.Contains("shield", StringComparison.Ordinal) => "shield",
            ItemSlotType.Weapon when item.Id.Contains("bow", StringComparison.Ordinal) => "bow",
            ItemSlotType.Weapon when item.Id.Contains("focus", StringComparison.Ordinal) || item.Id.Contains("bead", StringComparison.Ordinal) => "focus",
            ItemSlotType.Weapon => "blade",
            _ => string.Empty,
        };
    }

    private static IEnumerable<string> EnumerateCraftOperations(ItemBaseDefinition item)
    {
        return item.AllowedCraftOperations?.Select(operation => operation.ToString()) ?? Array.Empty<string>();
    }
}
