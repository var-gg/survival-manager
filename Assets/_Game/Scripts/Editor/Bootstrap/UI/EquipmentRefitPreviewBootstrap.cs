using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Equipment Refit 미리보기 — 시안 갤러리 V1 #2.
/// 3-section: standee + 3 hex slot / 5 affix list + Echo CTA / 8 inventory pool.
/// </summary>
public sealed class EquipmentRefitPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/EquipmentRefitPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string PortraitPath = "Assets/Resources/_Game/Art/Characters/hero_dawn_priest/portrait_full.png";
    private const string EchoIconPath = "Assets/_Game/UI/Foundation/Sprites/Currency/currency_echo.png";
    private const string AffixSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";

    private static readonly string[] AffixSamples = { "atk", "crit", "armor", "speed", "resist_phys" };
    private static readonly string[] PoolIcons = { "atk", "crit", "armor", "speed", "resist_phys", "lifesteal", "pierce", "block" };
    private static readonly string[] PoolGems = { "epic", "rare", "rare", "common", "rare", "epic", "common", "rare" };

    [MenuItem("SM/Town/Equipment Refit 미리보기", false, 11)]
    public static void Open()
    {
        var window = GetWindow<EquipmentRefitPreviewBootstrap>("Equipment Refit 미리보기");
        window.minSize = new Vector2(1320f, 780f);
    }

    private void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null) { rootVisualElement.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        if (tokens != null) rootVisualElement.styleSheets.Add(tokens);
        if (theme != null) rootVisualElement.styleSheets.Add(theme);

        visualTree.CloneTree(rootVisualElement);

        var portrait = rootVisualElement.Q<VisualElement>("StandeePortrait");
        var portraitTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath);
        if (portrait != null && portraitTex != null) portrait.style.backgroundImage = new StyleBackground(portraitTex);

        var echoIcon = rootVisualElement.Q<VisualElement>("EchoIcon");
        var echoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(EchoIconPath);
        if (echoIcon != null && echoTex != null) echoIcon.style.backgroundImage = new StyleBackground(echoTex);

        InjectAffixList();
        InjectInventoryPool();
    }

    private void InjectAffixList()
    {
        var list = rootVisualElement.Q<VisualElement>("AffixList");
        if (list == null) return;
        list.Clear();
        for (var i = 0; i < AffixSamples.Length; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("erp-affix-row");
            if (i == 1) row.AddToClassList("erp-affix-row--selected"); // 두 번째 selected (mockup의 highlight)

            var icon = new VisualElement();
            icon.AddToClassList("erp-affix-row__icon");
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, AffixSamples[i]));
            if (iconTex != null) icon.style.backgroundImage = new StyleBackground(iconTex);
            row.Add(icon);

            var glyph = new VisualElement();
            glyph.AddToClassList("erp-affix-row__glyph");
            row.Add(glyph);

            var tier = new VisualElement();
            tier.AddToClassList("erp-affix-row__tier");
            row.Add(tier);

            list.Add(row);
        }
    }

    private void InjectInventoryPool()
    {
        var pool = rootVisualElement.Q<VisualElement>("InventoryPool");
        if (pool == null) return;
        pool.Clear();
        for (var i = 0; i < PoolIcons.Length; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("erp-pool-row");

            var icon = new VisualElement();
            icon.AddToClassList("erp-pool-row__weapon-icon");
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, PoolIcons[i]));
            if (iconTex != null) icon.style.backgroundImage = new StyleBackground(iconTex);
            row.Add(icon);

            var glyph = new VisualElement();
            glyph.AddToClassList("erp-pool-row__glyph");
            row.Add(glyph);

            var gem = new VisualElement();
            gem.AddToClassList("erp-pool-row__gem");
            gem.AddToClassList($"erp-pool-row__gem--{PoolGems[i]}");
            row.Add(gem);

            pool.Add(row);
        }
    }
}
