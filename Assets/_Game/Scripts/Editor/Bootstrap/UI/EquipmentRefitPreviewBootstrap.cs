using System;
using System.Collections.Generic;
using SM.Core.Content;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Equipment Refit 미리보기 — Sprint 2 real-wire dev tool.
/// 시안 SoT: pindoc://town-ui-ux-시안-갤러리-v1 (2. Equipment Refit modal)
///
/// 진입: real GameSessionRoot 우선 (Profile.Inventory + AffixDefinition 기반 affix row).
/// 실패 시 mock fallback (8 inventory item + 5 affix row demo).
/// </summary>
public sealed class EquipmentRefitPreviewBootstrap : EditorWindow
{
    // 디자인시스템 이식: preview 중복 UXML 대신 production 패널 UXML 을 직접 렌더 → 캡처 = 실제 게임 패널 (divergence 해소).
    private const string VisualTreePath = "Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string PortraitPath = "Assets/Resources/_Game/Art/Characters/hero_dawn_priest/portrait_full.png";
    private const string EchoIconPath = "Assets/_Game/UI/Foundation/Sprites/Currency/currency_echo.png";
    private const string AffixSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";

    private EquipmentRefitView? _view;
    private EquipmentRefitPresenter? _presenter;

    [MenuItem("SM/Town/Equipment Refit 미리보기", false, 11)]
    public static void Open()
    {
        var window = GetWindow<EquipmentRefitPreviewBootstrap>("Equipment Refit 미리보기");
        window.minSize = new Vector2(1320f, 780f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root에 surface preview 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null) { root.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        if (tokens != null) root.styleSheets.Add(tokens);
        if (theme != null) root.styleSheets.Add(theme);

        visualTree.CloneTree(root);

        _view = new EquipmentRefitView(root);

        if (TryWireRealSession(_view))
        {
            return;
        }

        _view.Render(BuildMockViewState());
    }

    private bool TryWireRealSession(EquipmentRefitView view)
    {
        try
        {
            var sessionRoot = PreviewSessionContext.EnsureSession();
            var contentText = PreviewSessionContext.CreateContentText(sessionRoot);
            _presenter = new EquipmentRefitPresenter(
                sessionRoot.SessionState,
                sessionRoot.CombatContentLookup,
                view,
                contentText.GetItemName,
                contentText.GetAffixName,
                contentText.GetCharacterName,
                itemIconSprite: PreviewSessionContext.LoadItemSprite,
                currencySprite: PreviewSessionContext.LoadCurrencySprite,
                portraitLoader: PreviewSessionContext.LoadHeroPortrait,
                affixIconSprite: PreviewSessionContext.LoadAffixSprite,
                uiText: (table, key, fallback, arguments) =>
                    sessionRoot.Localization.LocalizeOrFallback(
                        table,
                        key,
                        fallback,
                        arguments));
            _presenter.Initialize();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EquipmentRefitPreview] real-session wire 실패, mock fallback: {e.Message}");
            _presenter = null;
            return false;
        }
    }

    private EquipmentRefitViewState BuildMockViewState()
    {
        // affix row — AffixDefinition.Tier 기준 group + 실제 magnitude/percentile/min-max context preview.
        var affixRaw = new (string AffixId, string Group, string Name, string Magnitude, string Icon)[]
        {
            ("affix_atk_implicit", "implicit", "기본 공격력",  "12 · 57% [8 ~ 15]",       "atk"),
            ("affix_crit_chance",  "prefix",   "치명타 확률",  "8 · 80% [4 ~ 9]",         "crit"),
            ("affix_armor_flat",   "prefix",   "방어도",        "20 · 44% [12 ~ 30]",      "armor"),
            ("affix_atk_speed",    "suffix",   "공격 속도",     "0.12 · 70% [0.05 ~ 0.15]", "speed"),
            ("affix_resist_phys",  "suffix",   "물리 저항",     "9 · 38% [6 ~ 14]",        "resist_phys"),
        };
        var affixes = new List<EquipmentRefitAffixRowViewState>(affixRaw.Length);
        foreach (var a in affixRaw)
        {
            affixes.Add(new EquipmentRefitAffixRowViewState(
                AffixId: a.AffixId,
                GroupKey: a.Group,
                CategoryKey: "utility",
                Name: a.Name,
                MagnitudeText: a.Magnitude,
                IconSprite: LoadAffixSprite(a.Icon),
                IsLocked: false,
                LockToggleEnabled: true,
                LockLabel: "잠그기"));
        }

        // 8 inventory pool item — ItemBaseDefinition 이름 / slot / rarity.
        var poolRaw = new (string Name, string Slot, string Icon, string Rarity, bool Selected)[]
        {
            ("강철 장검",   "weapon",    "atk",         "epic",   true),
            ("수호 흉갑",   "armor",     "armor",       "rare",   false),
            ("신속의 단검", "weapon",    "speed",       "rare",   false),
            ("사냥꾼 활",   "weapon",    "crit",        "common", false),
            ("마력 매개체", "accessory", "resist_phys", "rare",   false),
            ("흡혈 부적",   "accessory", "lifesteal",   "epic",   false),
            ("관통 창",     "weapon",    "pierce",      "common", false),
            ("성벽 방패",   "armor",     "block",       "rare",   false),
        };
        var pool = new List<EquipmentRefitPoolRowViewState>(poolRaw.Length);
        for (var i = 0; i < poolRaw.Length; i++)
        {
            var p = poolRaw[i];
            var familyKey = p.Slot == "weapon" ? (p.Icon == "armor" ? "shield" : "blade") : string.Empty;
            var presentation = EquipmentPresentationPolicy.Build(p.Slot, familyKey, p.Rarity, "Baseline", Array.Empty<string>());
            pool.Add(new EquipmentRefitPoolRowViewState(
                ItemInstanceId: $"mock_pool_{i:D2}",
                Name: p.Name,
                SlotKey: presentation.SlotKey,
                SlotLabel: presentation.SlotLabel,
                FamilyKey: presentation.FamilyKey,
                FamilyLabel: presentation.FamilyLabel,
                IconSprite: PreviewSessionContext.LoadItemSprite(string.IsNullOrWhiteSpace(presentation.FamilyKey) ? p.Slot : presentation.FamilyKey) ?? LoadAffixSprite(p.Icon),
                RarityKey: presentation.RarityKey,
                RawRarityKey: presentation.RawRarityKey,
                IdentityKey: presentation.IdentityKey,
                IdentityLabel: presentation.IdentityLabel,
                ShowsIdentityBadge: presentation.ShowsIdentityBadge,
                CanRefit: presentation.CanRefit,
                IsLaunchSupportedRarity: presentation.IsLaunchSupportedRarity,
                IsSelected: p.Selected));
        }

        return new EquipmentRefitViewState(
            SelectedItemName: "강철 장검",
            SelectedItemSlotLabel: "무기",
            SelectedItemRarityKey: "epic",
            SelectedItemFamilyKey: "blade",
            SelectedItemFamilyLabel: "검",
            SelectedItemIdentityKey: "baseline",
            SelectedItemIdentityLabel: "",
            SelectedItemShowsIdentityBadge: false,
            SelectedItemCanRefit: true,
            EquippedHeroLabel: "장착: Dawn Priest",
            EquippedHeroPortrait: AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath),
            EchoSprite: AssetDatabase.LoadAssetAtPath<Texture2D>(EchoIconPath),
            CurrentQualityPercent: 41.7d,
            NextFloorPercent: 58.4d,
            RefitCost: 24,
            RefitMaxed: false,
            RefitStatusMessage: "품질 41.7% → 보장 바닥 58.4%",
            Affixes: affixes,
            Pool: pool,
            SelectedOperation: CraftOperationKindValue.Reforge,
            ReforgeOperationSelectable: true,
            SealOperationSelectable: true,
            SealOperationReason: string.Empty,
            SelectedOperationCanPurchase: true,
            SelectedOperationCost: 24,
            SelectedOperationCostLabel: "24 잔향",
            SelectedOperationStatusMessage: "품질 41.7% → 보장 바닥 58.4% · service quote 24 잔향",
            ConfirmationVisible: false,
            PanelTitle: "장비 재련",
            OperationSelectorLabel: "작업 선택",
            ReforgeOperationLabel: "재련",
            SealOperationLabel: "봉인",
            CraftActionLabel: "재련 (-24 잔향)",
            ConfirmationMessage: "재련에 잔향 24을 사용합니까? 기존 굴림은 되돌릴 수 없습니다.",
            ConfirmLabel: "확인",
            CancelLabel: "취소");
    }

    private static Texture2D? LoadAffixSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, key));
}
