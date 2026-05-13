using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Permanent Augment 미리보기 — 시안 갤러리 V1 #4.
/// 12 motif grid (3x4) + selected detail + equip slot + stat compare + Equip CTA.
/// </summary>
public sealed class PermanentAugmentPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/PermanentAugmentPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string AugmentSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Augment/augment_{0}.png";

    private static readonly string[] AugmentMotifs =
    {
        "flame", "serpent", "star",
        "shield", "moon", "horn",
        "blade", "thorn", "wing",
        "void", "seal", "eye",
    };
    private const int SelectedIndex = 4; // mockup의 highlighted moon

    private static readonly (string label, string before, string after, bool up)[] StatRows =
    {
        ("ATK", "415", "488", true),
        ("DEF", "278", "233", false),
        ("HP", "1860", "2145", true),
        ("CRIT", "14.2%", "17.8%", true),
        ("DODGE", "6.3%", "4.9%", false),
    };

    [MenuItem("SM/Town/Permanent Augment 미리보기", false, 12)]
    public static void Open()
    {
        var window = GetWindow<PermanentAugmentPreviewBootstrap>("Permanent Augment 미리보기");
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
        InjectAugmentGrid();
        InjectStatCompare();
        InjectEquipSlot();
    }

    private void InjectAugmentGrid()
    {
        var grid = rootVisualElement.Q<VisualElement>("AugmentGrid");
        if (grid == null) return;
        grid.Clear();
        for (var i = 0; i < AugmentMotifs.Length; i++)
        {
            var cell = new VisualElement();
            cell.AddToClassList("pap-augment-cell");
            if (i == SelectedIndex) cell.AddToClassList("pap-augment-cell--selected");
            if (i >= 9) cell.AddToClassList("pap-augment-cell--locked"); // 마지막 3개 locked
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AugmentSpriteFmt, AugmentMotifs[i]));
            if (tex != null) cell.style.backgroundImage = new StyleBackground(tex);
            grid.Add(cell);
        }
    }

    private void InjectEquipSlot()
    {
        var slot = rootVisualElement.Q<VisualElement>("EquipSlot");
        if (slot == null) return;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AugmentSpriteFmt, AugmentMotifs[0])); // flame as currently equipped
        if (tex != null) slot.style.backgroundImage = new StyleBackground(tex);
    }

    private void InjectStatCompare()
    {
        var container = rootVisualElement.Q<VisualElement>("StatCompare");
        if (container == null) return;
        container.Clear();
        foreach (var (label, before, after, up) in StatRows)
        {
            var row = new VisualElement();
            row.AddToClassList("pap-stat-row");

            var lbl = new Label(label); lbl.AddToClassList("pap-stat-row__label"); row.Add(lbl);
            var bf = new Label(before); bf.AddToClassList("pap-stat-row__before"); row.Add(bf);
            var arrow = new VisualElement();
            arrow.AddToClassList("pap-stat-row__arrow");
            if (!up) arrow.style.backgroundColor = new StyleColor(new Color(0.85f, 0.35f, 0.32f, 0.95f));
            row.Add(arrow);
            var af = new Label(after); af.AddToClassList("pap-stat-row__after"); row.Add(af);

            container.Add(row);
        }
    }
}
