using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
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
    private string _selectedItemInstanceId = string.Empty;

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
        SpriteLoader? affixIconSprite = null)
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
        Refresh();
    }

    void IEquipmentRefitActions.OnRefitConfirmed()
    {
        if (string.IsNullOrEmpty(_selectedItemInstanceId))
            return;
        _session.RefitItem(_selectedItemInstanceId);
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
        // 값은 instance 확정 roll 미저장 (AffixIds = definition id) → ValueMin~ValueMax 범위 표기.
        var affixes = new List<EquipmentRefitAffixRowViewState>();
        if (selectedItem != null)
        {
            for (var i = 0; i < selectedItem.AffixIds.Count; i++)
            {
                var affixId = selectedItem.AffixIds[i];
                var group = "prefix";
                var category = "utility";
                var valueRange = "—";
                if (lookup.TryGetAffixDefinition(affixId, out var affixDef))
                {
                    group = affixDef.Tier.ToString().ToLowerInvariant();
                    category = affixDef.Category.ToString().ToLowerInvariant();
                    valueRange = $"{affixDef.ValueMin:0.#} ~ {affixDef.ValueMax:0.#}";
                }
                affixes.Add(new EquipmentRefitAffixRowViewState(
                    AffixId: affixId,
                    GroupKey: group,
                    CategoryKey: category,
                    Name: _affixName(affixId),
                    ValueRange: valueRange,
                    IconSprite: _affixIconSprite(affixId)));
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
        var refitQuote = selectedItem == null
            ? SM.Meta.Services.RefitQuote.Unavailable("재정비할 장비를 선택하세요.")
            : session.GetRefitQuote(selectedItem.ItemInstanceId);
        var equippedHeroLabel = "미장착";
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
                equippedHeroLabel = $"장착: {heroName}";
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
            RefitStatusMessage: BuildRefitStatus(refitQuote),
            Affixes: affixes,
            Pool: pool);
    }

    private static double ToPercent(ulong probabilityQ64)
        => probabilityQ64 / (double)ulong.MaxValue * 100d;

    private static string BuildRefitStatus(SM.Meta.Services.RefitQuote quote)
    {
        if (quote.RefitMaxed)
        {
            return quote.Reason.Contains("grade", StringComparison.OrdinalIgnoreCase)
                ? "이 등급은 개선 가능한 품질 구간이 없습니다."
                : "최대 Refit 품질에 도달했습니다.";
        }

        return quote.CanPurchase
            ? $"품질 {ToPercent(quote.CurrentPercentileQ64):0.0}% → 보장 바닥 {ToPercent(quote.TargetFloorQ64):0.0}%"
            : quote.Reason;
    }

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
