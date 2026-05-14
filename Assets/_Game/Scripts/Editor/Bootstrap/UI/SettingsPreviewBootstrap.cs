using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Settings 미리보기 — 시안 갤러리 V1 #8 (Settings.Global cross-scene modal).
///
/// V1 scope: Boot.Title / Town hub topbar gear / Battle.SettingsPanel "Open Global Settings"에서 호출.
/// IA SoT: pindoc://ux-surface-catalog-v1-draft (Settings.Global P0, 4 tab)
///
/// 4 tab spec:
///   - Audio    — BGM/SE/일본어 Voice volume + voice mute
///   - Video    — resolution / fullscreen / vsync / fps cap
///   - Language — UI ko/en, voice ja off-able
///   - Controls — keybinding
///   + Save management (cross-tab footer section, V1 미정 — 본 mock에는 미노출)
/// </summary>
public sealed class SettingsPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/SettingsPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string SettingsSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Settings/settings_{0}.png";

    private readonly struct Cat
    {
        public Cat(string key, string label, string spriteKey, bool sel) { Key=key; Label=label; SpriteKey=spriteKey; Selected=sel; }
        public string Key { get; }
        public string Label { get; }
        public string SpriteKey { get; }  // sprite 파일명 (audio/display/help/control)
        public bool Selected { get; }
    }

    // ux-surface-catalog-v1-draft Settings.Global P0 4 tab.
    // sprite key는 기존 art-pipeline 자산 reuse — settings_video / settings_language sprite는
    // 미발행이라 display/help로 임시 매핑. art-pipeline regen 시 정합 sprite 추가.
    private static readonly Cat[] Categories =
    {
        new("audio",    "AUDIO",    "audio",   false),
        new("video",    "VIDEO",    "display", true),    // default selected (Video tab 시안)
        new("language", "LANGUAGE", "help",    false),   // placeholder sprite
        new("controls", "CONTROLS", "control", false),
    };

    private enum OptKind { Segmented, Dropdown, Slider, Toggle, SegSize }

    /// <summary>
    /// 4차 round column: 실 시스템 SettingProfile entry는 current_value + default_value + bound_keybind
    /// + dirty(변경 미저장) 상태를 가짐. mock은 default + dirty marker + per-row reset 버튼으로 시각화.
    /// </summary>
    private readonly struct Opt
    {
        public Opt(string label, OptKind kind, int current, int defaultV)
        { Label=label; Kind=kind; Value=current; Default=defaultV; }
        public string Label { get; }
        public OptKind Kind { get; }
        public int Value { get; }
        public int Default { get; }
        public bool IsDirty => Value != Default;  // 변경 미저장 marker
    }

    // Video tab (default selected) 옵션 — current value + default value 컬럼 추가.
    private static readonly Opt[] Options =
    {
        new("화질 프리셋",  OptKind.Segmented, /*current*/ 2, /*default*/ 1),  // 변경됨 (High)
        new("해상도",       OptKind.Dropdown,  0,  0),  // 기본값
        new("전체화면",     OptKind.Toggle,    1,  1),  // 기본값
        new("V-Sync",       OptKind.Toggle,    1,  0),  // 변경됨
        new("FPS 상한",     OptKind.Slider,    60, 60), // 기본값
        new("밝기",         OptKind.Slider,    72, 50), // 변경됨
        new("UI 스케일",    OptKind.Slider,    50, 50), // 기본값
        new("HDR",          OptKind.Toggle,    0,  0),  // 기본값
    };

    [MenuItem("SM/Town/Settings 미리보기", false, 16)]
    public static void Open()
    {
        var window = GetWindow<SettingsPreviewBootstrap>("Settings 미리보기");
        window.minSize = new Vector2(1280f, 720f);
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

        InjectSidebar(root);
        InjectOptions(root);
    }

    private void InjectSidebar(VisualElement root)
    {
        var bar = root.Q<VisualElement>("CategorySidebar");
        if (bar == null) return;
        bar.Clear();
        foreach (var c in Categories)
        {
            var row = new VisualElement(); row.AddToClassList("stp-sidebar__row");
            if (c.Selected) row.AddToClassList("stp-sidebar__row--selected");
            var icon = new VisualElement(); icon.AddToClassList("stp-sidebar__icon");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(SettingsSpriteFmt, c.SpriteKey));
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
            row.Add(icon);
            var lbl = new Label(c.Label); lbl.AddToClassList("stp-sidebar__label"); row.Add(lbl);
            bar.Add(row);
        }
    }

    private void InjectOptions(VisualElement root)
    {
        var list = root.Q<VisualElement>("OptionList");
        if (list == null) return;
        list.Clear();

        foreach (var o in Options)
        {
            var row = new VisualElement(); row.AddToClassList("stp-opt-row");
            if (o.IsDirty) row.AddToClassList("stp-opt-row--dirty");

            var lbl = new Label(o.Label); lbl.AddToClassList("stp-opt-row__label"); row.Add(lbl);

            // dirty indicator (asterisk before control)
            if (o.IsDirty)
            {
                var dirtyMark = new Label("*");
                dirtyMark.AddToClassList("stp-opt-row__dirty-mark");
                dirtyMark.tooltip = $"변경됨 (기본값: {o.Default})";
                row.Add(dirtyMark);
            }

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

            // per-row reset button — dirty 일 때만 활성 시각, 항상 노출
            var resetBtn = new Button { text = "↺" };
            resetBtn.AddToClassList("stp-opt-row__reset");
            if (!o.IsDirty) resetBtn.AddToClassList("stp-opt-row__reset--disabled");
            resetBtn.tooltip = o.IsDirty
                ? $"기본값으로 되돌리기 (default: {o.Default})"
                : "기본값 (변경 없음)";
            row.Add(resetBtn);

            list.Add(row);
        }
    }
}
