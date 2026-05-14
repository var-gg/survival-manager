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
        // 5 affix line — implicit 1 + prefix 2 + suffix 2
        var affixRaw = new (string Icon, string Group)[]
        {
            ("atk",         "implicit"),
            ("crit",        "prefix"),
            ("armor",       "prefix"),
            ("speed",       "suffix"),
            ("resist_phys", "suffix"),
        };
        var affixes = new List<EquipmentRefitAffixRowViewState>(affixRaw.Length);
        for (var i = 0; i < affixRaw.Length; i++)
        {
            var (icon, group) = affixRaw[i];
            affixes.Add(new EquipmentRefitAffixRowViewState(
                GroupKey: group,
                IconKey: icon,
                IconSprite: LoadAffixSprite(icon),
                IsSelectedForReroll: i == 1));  // mock: 2번째 affix가 reroll 대상
        }

        // 8 inventory pool item
        var poolRaw = new (string Icon, string Rarity)[]
        {
            ("atk", "epic"),
            ("crit", "rare"),
            ("armor", "rare"),
            ("speed", "common"),
            ("resist_phys", "rare"),
            ("lifesteal", "epic"),
            ("pierce", "common"),
            ("block", "rare"),
        };
        var pool = new List<EquipmentRefitPoolRowViewState>(poolRaw.Length);
        for (var i = 0; i < poolRaw.Length; i++)
        {
            var (icon, rarity) = poolRaw[i];
            pool.Add(new EquipmentRefitPoolRowViewState(
                ItemInstanceId: $"mock_pool_{i:D2}",
                IconKey: icon,
                IconSprite: LoadAffixSprite(icon),
                RarityKey: rarity));
        }

        return new EquipmentRefitViewState(
            StandeePortrait: AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath),
            EchoSprite: AssetDatabase.LoadAssetAtPath<Texture2D>(EchoIconPath),
            RefitCost: EquipmentRefitPresenter.RefitEchoCost,
            Affixes: affixes,
            Pool: pool);
    }

    private static Texture2D? LoadAffixSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, key));
}
