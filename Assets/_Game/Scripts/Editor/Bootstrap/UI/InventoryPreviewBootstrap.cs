using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Inventory 미리보기 — 시안 갤러리 V1 #6.
/// currency header / 5 category sidebar / 5x4 weapon grid / selected item detail.
/// </summary>
public sealed class InventoryPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/InventoryPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string AffixSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";
    private const string CurrencySpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Currency/currency_{0}.png";
    private const string ClassSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";

    private readonly struct Category
    {
        public Category(string label, string count, string icon, bool sel) { Label=label; Count=count; IconKey=icon; Selected=sel; }
        public string Label { get; }
        public string Count { get; }
        public string IconKey { get; }
        public bool Selected { get; }
    }
    private static readonly Category[] Categories =
    {
        new("ALL",        "128/200", "atk",       false),
        new("WEAPON",     "48/100",  "pierce",    true),
        new("ARMOR",      "36/100",  "armor",     false),
        new("ACCESSORY",  "25/100",  "amplify",   false),
        new("CONSUMABLE", "89/200",  "heal",      false),
        new("BLUEPRINT",  "18/100",  "link",      false),
    };

    private static readonly (string icon, string rarity)[] GridItems =
    {
        ("atk","epic"), ("crit","epic"), ("pierce","rare"), ("speed","epic"), ("magic_atk","epic"),
        ("armor","rare"), ("crit","rare"), ("pierce","epic"), ("speed","rare"), ("magic_atk","rare"),
        ("atk","common"), ("armor","common"), ("pierce","common"), ("speed","common"), ("magic_atk","common"),
        ("crit","rare"), ("armor","epic"), ("pierce","rare"), ("speed","epic"), ("magic_atk","common"),
    };

    [MenuItem("SM/Town/Inventory 미리보기", false, 14)]
    public static void Open()
    {
        var window = GetWindow<InventoryPreviewBootstrap>("Inventory 미리보기");
        window.minSize = new Vector2(1400f, 820f);
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

        var goldIcon = rootVisualElement.Q<VisualElement>("GoldIcon");
        var goldTex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(CurrencySpriteFmt, "gold"));
        if (goldIcon != null && goldTex != null) goldIcon.style.backgroundImage = new StyleBackground(goldTex);

        var echoIcon = rootVisualElement.Q<VisualElement>("EchoIcon");
        var echoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(CurrencySpriteFmt, "echo"));
        if (echoIcon != null && echoTex != null) echoIcon.style.backgroundImage = new StyleBackground(echoTex);

        InjectSidebar();
        InjectGrid();
        InjectDetail();
    }

    private void InjectSidebar()
    {
        var sidebar = rootVisualElement.Q<VisualElement>("CategorySidebar");
        if (sidebar == null) return;
        sidebar.Clear();
        foreach (var c in Categories)
        {
            var row = new VisualElement(); row.AddToClassList("inv-sidebar__row");
            if (c.Selected) row.AddToClassList("inv-sidebar__row--selected");
            var icon = new VisualElement(); icon.AddToClassList("inv-sidebar__row-icon");
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, c.IconKey));
            if (iconTex != null) icon.style.backgroundImage = new StyleBackground(iconTex);
            row.Add(icon);
            var info = new VisualElement(); info.AddToClassList("inv-sidebar__row-info");
            var name = new Label(c.Label); name.AddToClassList("inv-sidebar__row-name"); info.Add(name);
            var count = new Label(c.Count); count.AddToClassList("inv-sidebar__row-count"); info.Add(count);
            row.Add(info);
            sidebar.Add(row);
        }
    }

    private void InjectGrid()
    {
        var grid = rootVisualElement.Q<VisualElement>("ItemGrid");
        if (grid == null) return;
        grid.Clear();
        foreach (var (icon, rarity) in GridItems)
        {
            var cell = new VisualElement();
            cell.AddToClassList("inv-grid__cell");
            cell.AddToClassList($"inv-grid__cell--{rarity}");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, icon));
            if (tex != null) cell.style.backgroundImage = new StyleBackground(tex);
            grid.Add(cell);
        }
    }

    private void InjectDetail()
    {
        var icon = rootVisualElement.Q<VisualElement>("DetailIcon");
        var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, "atk"));
        if (icon != null && iconTex != null) icon.style.backgroundImage = new StyleBackground(iconTex);

        var affixes = rootVisualElement.Q<VisualElement>("DetailAffixes");
        if (affixes == null) return;
        affixes.Clear();
        var rows = new[] { ("ATK", "+256"), ("CRIT", "+18.7%"), ("SPEED", "+128"), ("LIFESTEAL", "+9.6%") };
        foreach (var (label, value) in rows)
        {
            var row = new VisualElement(); row.AddToClassList("inv-detail__affix-row");
            var g = new VisualElement(); g.AddToClassList("inv-detail__affix-glyph"); row.Add(g);
            var v = new Label(value); v.AddToClassList("inv-detail__affix-value"); row.Add(v);
            affixes.Add(row);
        }
    }
}
