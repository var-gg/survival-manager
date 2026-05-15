using System.Collections.Generic;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Equipment Refit 미리보기 — Sprint 1 presenter 패턴 dev tool.
/// 시안 SoT: pindoc://town-ui-ux-시안-갤러리-v1 (2. Equipment Refit modal)
/// </summary>
public sealed class EquipmentRefitPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/EquipmentRefitPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string PortraitPath = "Assets/Resources/_Game/Art/Characters/hero_dawn_priest/portrait_full.png";
    private const string EchoIconPath = "Assets/_Game/UI/Foundation/Sprites/Currency/currency_echo.png";
    private const string AffixSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";

    private EquipmentRefitView? _view;

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
        _view.Render(BuildMockViewState());
    }

    private EquipmentRefitViewState BuildMockViewState()
    {
        // affix row — AffixDefinition.Tier 기준 group. 값은 instance 확정 roll 미저장 →
        // AffixDefinition.ValueMin~ValueMax 범위 표기 (가짜 rolled value 아님 — audit §4.1 P1-2).
        var affixRaw = new (string AffixId, string Group, string Name, string Range, string Icon, bool Selected)[]
        {
            ("affix_atk_implicit", "implicit", "기본 공격력",  "8 ~ 15",      "atk",         false),
            ("affix_crit_chance",  "prefix",   "치명타 확률",  "4 ~ 9",       "crit",        true),
            ("affix_armor_flat",   "prefix",   "방어도",        "12 ~ 30",     "armor",       false),
            ("affix_atk_speed",    "suffix",   "공격 속도",     "0.05 ~ 0.15", "speed",       false),
            ("affix_resist_phys",  "suffix",   "물리 저항",     "6 ~ 14",      "resist_phys", false),
        };
        var affixes = new List<EquipmentRefitAffixRowViewState>(affixRaw.Length);
        foreach (var a in affixRaw)
        {
            affixes.Add(new EquipmentRefitAffixRowViewState(
                AffixId: a.AffixId,
                GroupKey: a.Group,
                Name: a.Name,
                ValueRange: a.Range,
                IconSprite: LoadAffixSprite(a.Icon),
                IsSelectedForReroll: a.Selected));
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
            pool.Add(new EquipmentRefitPoolRowViewState(
                ItemInstanceId: $"mock_pool_{i:D2}",
                Name: p.Name,
                SlotKey: p.Slot,
                IconSprite: LoadAffixSprite(p.Icon),
                RarityKey: p.Rarity,
                IsSelected: p.Selected));
        }

        return new EquipmentRefitViewState(
            SelectedItemName: "강철 장검",
            SelectedItemSlotLabel: "무기",
            SelectedItemRarityKey: "epic",
            EquippedHeroLabel: "장착: Dawn Priest",
            EquippedHeroPortrait: AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath),
            EchoSprite: AssetDatabase.LoadAssetAtPath<Texture2D>(EchoIconPath),
            RefitCost: EquipmentRefitPresenter.RefitEchoCost,
            Affixes: affixes,
            Pool: pool);
    }

    private static Texture2D? LoadAffixSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, key));
}
