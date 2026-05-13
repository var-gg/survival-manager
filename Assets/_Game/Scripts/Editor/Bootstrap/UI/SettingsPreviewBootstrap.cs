using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Settings 미리보기 — 시안 갤러리 V1 #8.
/// 5 category sidebar (Display/Audio/Control/Account/Help) + 옵션 리스트 + Apply/Cancel/Reset.
/// </summary>
public sealed class SettingsPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/SettingsPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string SettingsSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Settings/settings_{0}.png";

    private readonly struct Cat
    {
        public Cat(string key, string label, bool sel) { Key=key; Label=label; Selected=sel; }
        public string Key { get; }
        public string Label { get; }
        public bool Selected { get; }
    }
    private static readonly Cat[] Categories =
    {
        new("display", "DISPLAY", true),
        new("audio",   "AUDIO", false),
        new("control", "CONTROL", false),
        new("account", "ACCOUNT", false),
        new("help",    "HELP", false),
    };

    private enum OptKind { Segmented, Dropdown, Slider, Toggle, SegSize }
    private readonly struct Opt
    {
        public Opt(string label, OptKind kind, int v) { Label=label; Kind=kind; Value=v; }
        public string Label { get; }
        public OptKind Kind { get; }
        public int Value { get; }
    }
    private static readonly Opt[] Options =
    {
        new("화질 프리셋", OptKind.Segmented, 2),
        new("해상도",      OptKind.Dropdown,  0),
        new("FPS",         OptKind.Slider,    60),
        new("밝기",        OptKind.Slider,    72),
        new("UI 스케일",   OptKind.Slider,    50),
        new("V-Sync",      OptKind.Toggle,    1),
        new("HDR",         OptKind.Toggle,    0),
        new("자막 크기",   OptKind.SegSize,   1),
    };

    [MenuItem("SM/Town/Settings 미리보기", false, 16)]
    public static void Open()
    {
        var window = GetWindow<SettingsPreviewBootstrap>("Settings 미리보기");
        window.minSize = new Vector2(1280f, 720f);
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

        InjectSidebar();
        InjectOptions();
    }

    private void InjectSidebar()
    {
        var bar = rootVisualElement.Q<VisualElement>("CategorySidebar");
        if (bar == null) return;
        bar.Clear();
        foreach (var c in Categories)
        {
            var row = new VisualElement(); row.AddToClassList("stp-sidebar__row");
            if (c.Selected) row.AddToClassList("stp-sidebar__row--selected");
            var icon = new VisualElement(); icon.AddToClassList("stp-sidebar__icon");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(SettingsSpriteFmt, c.Key));
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
            row.Add(icon);
            var lbl = new Label(c.Label); lbl.AddToClassList("stp-sidebar__label"); row.Add(lbl);
            bar.Add(row);
        }
    }

    private void InjectOptions()
    {
        var list = rootVisualElement.Q<VisualElement>("OptionList");
        if (list == null) return;
        list.Clear();

        foreach (var o in Options)
        {
            var row = new VisualElement(); row.AddToClassList("stp-opt-row");
            var lbl = new Label(o.Label); lbl.AddToClassList("stp-opt-row__label"); row.Add(lbl);
            var ctrl = new VisualElement(); ctrl.AddToClassList("stp-opt-row__control");

            switch (o.Kind)
            {
                case OptKind.Slider:
                    var bar = new VisualElement(); bar.AddToClassList("stp-opt-row__slider-bar");
                    var fill = new VisualElement(); fill.AddToClassList("stp-opt-row__slider-fill");
                    fill.style.width = new StyleLength(new Length(o.Value, LengthUnit.Percent));
                    bar.Add(fill);
                    var knob = new VisualElement(); knob.AddToClassList("stp-opt-row__slider-knob"); bar.Add(knob);
                    ctrl.Add(bar);
                    break;
                case OptKind.Toggle:
                    var tg = new VisualElement(); tg.AddToClassList("stp-opt-row__toggle");
                    if (o.Value == 1) tg.AddToClassList("stp-opt-row__toggle--on");
                    var kn = new VisualElement(); kn.AddToClassList("stp-opt-row__toggle-knob"); tg.Add(kn);
                    ctrl.Add(tg);
                    break;
                case OptKind.Segmented:
                case OptKind.SegSize:
                    var seg = new VisualElement(); seg.AddToClassList("stp-opt-row__seg");
                    var count = o.Kind == OptKind.Segmented ? 4 : 3;
                    for (var i = 0; i < count; i++)
                    {
                        var cell = new VisualElement(); cell.AddToClassList("stp-opt-row__seg-cell");
                        if (i == o.Value) cell.AddToClassList("stp-opt-row__seg-cell--selected");
                        seg.Add(cell);
                    }
                    ctrl.Add(seg);
                    break;
                case OptKind.Dropdown:
                    var dd = new VisualElement(); dd.AddToClassList("stp-opt-row__seg");
                    var c1 = new VisualElement(); c1.AddToClassList("stp-opt-row__seg-cell"); c1.style.width = 120;
                    c1.AddToClassList("stp-opt-row__seg-cell--selected");
                    dd.Add(c1);
                    ctrl.Add(dd);
                    break;
            }
            row.Add(ctrl);
            list.Add(row);
        }
    }
}
