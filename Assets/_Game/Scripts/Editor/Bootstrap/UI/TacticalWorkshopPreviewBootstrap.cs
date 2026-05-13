using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Tactical Workshop 미리보기 — 시안 갤러리 V1 (pindoc) mockup 정합.
/// 3-column modal (Anchor Pad / Posture+Tactic / Live Preview + Threat-Answer).
/// 기존 Assets/_Game/UI/TacticalWorkshop/TacticalWorkshop.uxml은 건드리지 않음 (in-flight Codex 작업).
/// </summary>
public sealed class TacticalWorkshopPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/TacticalWorkshopPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string PostureSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Posture/posture_{0}.png";
    private static readonly string[] PostureNames =
    {
        "hold_line",
        "standard_advance",
        "protect_carry",
        "collapse_weak_side",
        "all_in_backline",
    };

    private const string ThreatSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Threat/threat_{0}.png";
    private static readonly string[] ThreatNames =
    {
        "burst", "sustain", "control", "swarm",
        "dive",  "pierce",  "heal",    "summon",
    };

    [MenuItem("SM/Town/Tactical Workshop 미리보기", false, 9)]
    public static void Open()
    {
        var window = GetWindow<TacticalWorkshopPreviewBootstrap>("Tactical Workshop 미리보기");
        window.minSize = new Vector2(1440f, 820f);
    }

    private void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        var tokensSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var panelThemeSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);

        if (visualTree == null) { rootVisualElement.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        if (tokensSheet != null) rootVisualElement.styleSheets.Add(tokensSheet);
        if (panelThemeSheet != null) rootVisualElement.styleSheets.Add(panelThemeSheet);

        visualTree.CloneTree(rootVisualElement);

        InjectPostureCards();
        InjectTacticRows();
    }

    private void InjectPostureCards()
    {
        var row = rootVisualElement.Q<VisualElement>("PostureCardRow");
        if (row == null) return;
        row.Clear();

        for (var i = 0; i < PostureNames.Length; i++)
        {
            var name = PostureNames[i];
            var path = string.Format(PostureSpriteFmt, name);
            var card = new VisualElement();
            card.AddToClassList("twp-posture-card");
            if (i == 1) card.AddToClassList("twp-posture-card--selected"); // StandardAdvance default 선택
            var sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (sprite != null)
            {
                card.style.backgroundImage = new StyleBackground(sprite);
            }
            row.Add(card);
        }
    }

    private void InjectTacticRows()
    {
        var container = rootVisualElement.Q<VisualElement>("TacticPresetRows");
        if (container == null) return;
        container.Clear();

        // 4 lead character 행 + 3 threat 아이콘 (mockup의 abstract glyph + per-unit icon strip 표현)
        var threatPath = string.Format(ThreatSpriteFmt, ThreatNames[2]); // control 등 placeholder
        var threatTex = AssetDatabase.LoadAssetAtPath<Texture2D>(threatPath);
        for (var i = 0; i < 4; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("twp-tactic-row");

            var portrait = new VisualElement();
            portrait.AddToClassList("twp-tactic-row__portrait");
            row.Add(portrait);

            var glyph = new VisualElement();
            glyph.AddToClassList("twp-tactic-row__glyph");
            row.Add(glyph);

            // 3개 작은 threat icon
            for (var k = 0; k < 3; k++)
            {
                var icon = new VisualElement();
                icon.AddToClassList("twp-tactic-row__icon");
                var iconPath = string.Format(ThreatSpriteFmt, ThreatNames[(i + k) % ThreatNames.Length]);
                var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (iconTex != null) icon.style.backgroundImage = new StyleBackground(iconTex);
                row.Add(icon);
            }

            container.Add(row);
        }
    }
}
