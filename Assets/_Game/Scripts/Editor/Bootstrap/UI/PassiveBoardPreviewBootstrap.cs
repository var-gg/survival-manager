using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Passive Board 미리보기 — 시안 갤러리 V1 #5.
/// 4 class tab + hex node tree + node detail + Activate CTA + 6/45 NODE POINTS footer.
/// </summary>
public sealed class PassiveBoardPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/PassiveBoardPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string ClassSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";
    private const string AffixSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";

    private readonly struct TabSpec
    {
        public TabSpec(string key, string label, bool sel) { Key=key; Label=label; Selected=sel; }
        public string Key { get; }
        public string Label { get; }
        public bool Selected { get; }
    }
    private static readonly TabSpec[] Tabs =
    {
        new("vanguard", "VANGUARD", false),
        new("duelist",  "DUELIST",  true),
        new("ranger",   "RANGER",   false),
        new("mystic",   "MYSTIC",   false),
    };

    private readonly struct NodeSpec
    {
        public NodeSpec(float left, float top, string state, string iconKey)
        { Left=left; Top=top; State=state; IconKey=iconKey; }
        public float Left { get; }
        public float Top { get; }
        public string State { get; } // root / active / available / locked
        public string IconKey { get; }
    }

    /// <summary> mockup의 hex tree 대략 — 중앙 root + inner ring(active 6) + outer ring(locked) </summary>
    private static readonly NodeSpec[] Nodes =
    {
        // Center root
        new(0.46f, 0.40f, "root", "crit"),
        // Inner ring (active)
        new(0.30f, 0.16f, "active", "atk"),
        new(0.62f, 0.16f, "active", "speed"),
        new(0.14f, 0.40f, "active", "armor"),
        new(0.78f, 0.40f, "active", "pierce"),
        new(0.30f, 0.64f, "active", "lifesteal"),
        new(0.62f, 0.64f, "active", "heal"),
        // Outer ring (locked)
        new(0.08f, 0.10f, "locked", "atk"),
        new(0.85f, 0.10f, "locked", "speed"),
        new(0.02f, 0.40f, "locked", "armor"),
        new(0.90f, 0.40f, "locked", "pierce"),
        new(0.08f, 0.70f, "locked", "lifesteal"),
        new(0.85f, 0.70f, "locked", "heal"),
        new(0.46f, 0.10f, "locked", "block"),
        new(0.46f, 0.78f, "locked", "thorn"),
    };

    [MenuItem("SM/Town/Passive Board 미리보기", false, 13)]
    public static void Open()
    {
        var window = GetWindow<PassiveBoardPreviewBootstrap>("Passive Board 미리보기");
        window.minSize = new Vector2(1280f, 760f);
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

        InjectTabs();
        InjectBoardNodes();
        InjectDetailIcon();
    }

    private void InjectTabs()
    {
        var tabs = rootVisualElement.Q<VisualElement>("ClassTabs");
        if (tabs == null) return;
        tabs.Clear();
        foreach (var t in Tabs)
        {
            var btn = new VisualElement(); btn.AddToClassList("pbp-tab");
            if (t.Selected) btn.AddToClassList("pbp-tab--selected");
            var icon = new VisualElement(); icon.AddToClassList("pbp-tab__icon");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ClassSpriteFmt, t.Key));
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
            btn.Add(icon);
            var lbl = new Label(t.Label); lbl.AddToClassList("pbp-tab__label"); btn.Add(lbl);
            tabs.Add(btn);
        }
    }

    private void InjectBoardNodes()
    {
        var canvas = rootVisualElement.Q<VisualElement>("BoardCanvas");
        if (canvas == null) return;
        canvas.Clear();
        foreach (var n in Nodes)
        {
            var node = new VisualElement(); node.AddToClassList("pbp-board__node");
            node.AddToClassList($"pbp-board__node--{n.State}");
            node.style.left = new StyleLength(new Length(n.Left * 100f, LengthUnit.Percent));
            node.style.top = new StyleLength(new Length(n.Top * 100f, LengthUnit.Percent));
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, n.IconKey));
            if (tex != null) node.style.backgroundImage = new StyleBackground(tex);
            canvas.Add(node);
        }
    }

    private void InjectDetailIcon()
    {
        var icon = rootVisualElement.Q<VisualElement>("DetailIcon");
        if (icon == null) return;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, "crit"));
        if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
    }
}
