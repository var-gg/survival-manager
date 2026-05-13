using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Theater 미리보기 — 시안 갤러리 V1 #7.
/// 좌 entry list (chapter grouped) / 중 replay player / 우 chapter detail + Replay CTA.
/// </summary>
public sealed class TheaterPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/TheaterPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string BackdropDuskPath = "Assets/_Game/UI/Backdrops/town_frontier_village_dusk.png";

    private readonly struct Group
    {
        public Group(string title, Entry[] entries) { Title=title; Entries=entries; }
        public string Title { get; }
        public Entry[] Entries { get; }
    }
    private readonly struct Entry
    {
        public Entry(string label, string state) { Label=label; State=state; }
        public string Label { get; }
        public string State { get; }
    }
    private static readonly Group[] Groups =
    {
        new("PROLOGUE", new[] { new Entry("inception", "checked"), new Entry("frontier_gate", "checked") }),
        new("CHAPTER 1", new[] { new Entry("ashen", "selected"), new Entry("wolfpine", "checked"), new Entry("trail", "checked"), new Entry("crypt", "checked"), new Entry("dawn", "checked") }),
        new("CHAPTER 2", new[] { new Entry("locked_a", "locked"), new Entry("locked_b", "locked") }),
    };

    [MenuItem("SM/Town/Theater 미리보기", false, 15)]
    public static void Open()
    {
        var window = GetWindow<TheaterPreviewBootstrap>("Theater 미리보기");
        window.minSize = new Vector2(1380f, 800f);
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

        InjectEntryList();
        InjectPlayerImage();
        InjectDetailBanner();
    }

    private void InjectEntryList()
    {
        var list = rootVisualElement.Q<VisualElement>("EntryList");
        if (list == null) return;
        list.Clear();
        var thumbTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropDuskPath);
        foreach (var g in Groups)
        {
            var group = new VisualElement(); group.AddToClassList("thp-entry-group");
            var gt = new Label(g.Title); gt.AddToClassList("thp-entry-group__title"); group.Add(gt);
            foreach (var e in g.Entries)
            {
                var row = new VisualElement(); row.AddToClassList("thp-entry");
                if (e.State == "selected") row.AddToClassList("thp-entry--selected");
                if (e.State == "locked") row.AddToClassList("thp-entry--locked");

                var thumb = new VisualElement(); thumb.AddToClassList("thp-entry__thumb");
                if (thumbTex != null) thumb.style.backgroundImage = new StyleBackground(thumbTex);
                row.Add(thumb);

                var info = new VisualElement(); info.AddToClassList("thp-entry__info");
                var t = new VisualElement(); t.AddToClassList("thp-entry__title-glyph"); info.Add(t);
                var m = new VisualElement(); m.AddToClassList("thp-entry__meta-glyph"); info.Add(m);
                row.Add(info);

                if (e.State == "locked") {
                    var lk = new VisualElement(); lk.AddToClassList("thp-entry__lock"); row.Add(lk);
                } else {
                    var ck = new VisualElement(); ck.AddToClassList("thp-entry__check"); row.Add(ck);
                }
                group.Add(row);
            }
            list.Add(group);
        }
    }

    private void InjectPlayerImage()
    {
        var img = rootVisualElement.Q<VisualElement>("PlayerImage");
        if (img == null) return;
        // mockup의 cutscene preview는 narrative 일러. backdrop을 placeholder로 사용.
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropDuskPath);
        if (tex != null) img.style.backgroundImage = new StyleBackground(tex);
    }

    private void InjectDetailBanner()
    {
        var banner = rootVisualElement.Q<VisualElement>("DetailBanner");
        if (banner == null) return;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropDuskPath);
        if (tex != null) banner.style.backgroundImage = new StyleBackground(tex);
    }
}
