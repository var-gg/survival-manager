using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Theater 미리보기 — 시안 갤러리 V1 #7 (StoryArchive replay).
/// Battle replay이 아니라 cutscene 다시보기 surface — battle replay은 Battle scene 재호출이 별개.
/// 시안 SoT: `pindoc://town-ui-ux-시안-갤러리-v1` (7. Theater tab — StoryArchive replay)
/// 스케줄 SoT: `pindoc://flow-campaign-story-scene-schedule-v1` (Prologue + Ch1~Ch5 cutscene 진입표)
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

    /// <summary>
    /// Theater entry column — StoryArchive replay metadata.
    /// label = scene_id 표시명 / state = checked/selected/unwatched/locked / duration / completion percent / watched_at.
    /// 실 시스템: ReplayLedgerEntry (scene_id + duration + last_watched + completion_pct).
    /// </summary>
    private readonly struct Entry
    {
        public Entry(string label, string state, string duration, int completionPct, string watchedAt)
        { Label=label; State=state; Duration=duration; CompletionPct=completionPct; WatchedAt=watchedAt; }
        public string Label { get; }
        public string State { get; }            // checked / selected / unwatched / locked
        public string Duration { get; }         // "1:24" 형식
        public int CompletionPct { get; }       // 0-100
        public string WatchedAt { get; }        // "2026-05-12" 또는 "-" (locked / unwatched)
    }

    /// <summary>
    /// flow-campaign-story-scene-schedule-v1 기준 cutscene-only 진입표.
    /// chapter당 cutscene_* + 주요 dialogue_scene_* (media_cue 있는 것) 만 추렸음.
    /// V1 prototype 기준 unlock 진행도: Prologue 완료 + Ch1 진행 중 + Ch2~Ch5 locked.
    /// </summary>
    private static readonly Group[] Groups =
    {
        new("PROLOGUE", new[]
        {
            new Entry("prologue_runtime_short_opening",   "checked", "0:48", 100, "2026-05-08"),
            new Entry("prologue_runtime_short_ashglen",   "checked", "1:12", 100, "2026-05-08"),
            new Entry("prologue_runtime_short_pack_meet", "checked", "0:54", 100, "2026-05-09"),
            new Entry("prologue_runtime_short_gate",      "checked", "0:38", 100, "2026-05-09"),
        }),
        new("CHAPTER 1 — 재의 문", new[]
        {
            new Entry("chapter_intro_ashen_frontier", "checked",   "1:24", 100, "2026-05-10"),
            new Entry("scene_gate_warden",            "checked",   "0:42", 100, "2026-05-10"),
            new Entry("scene_priest_first_crack",     "selected",  "0:58",  62, "2026-05-13"),  // 일부 진행
            new Entry("scene_boss_wolfpine_defeat",   "checked",   "1:08", 100, "2026-05-12"),
            new Entry("scene_after_wolfpine",         "unwatched", "0:34",   0, "-"),
            new Entry("chapter_clear_ashen_frontier", "unwatched", "1:56",   0, "-"),
        }),
        new("CHAPTER 2 — 가라앉은 보루", new[]
        {
            new Entry("scene_sunken_bastion_intro",   "locked", "1:18", 0, "-"),
            new Entry("scene_aldric_journal",         "locked", "0:46", 0, "-"),
            new Entry("scene_purification_judicator", "locked", "1:32", 0, "-"),
            new Entry("scene_pack_names",             "locked", "1:04", 0, "-"),
        }),
        new("CHAPTER 3 — 무너진 묘역", new[]
        {
            new Entry("scene_ruined_crypts_intro", "locked", "1:22", 0, "-"),
            new Entry("scene_black_vellum_ledger", "locked", "0:52", 0, "-"),
            new Entry("scene_koohan_awakening",    "locked", "1:48", 0, "-"),
        }),
        new("CHAPTER 4 — 유리의 숲", new[]
        {
            new Entry("ch4_intro_glass_voice",      "locked", "1:36", 0, "-"),
            new Entry("scene_lyra_first_encounter", "locked", "1:14", 0, "-"),
            new Entry("ch4_clear_two_priests",      "locked", "2:08", 0, "-"),
        }),
        new("CHAPTER 5 — 변방 협곡 / 첫 도시", new[]
        {
            new Entry("ch5_intro_canyon_first_city",   "locked", "1:42", 0, "-"),
            new Entry("scene_baekgyu_first_encounter", "locked", "1:28", 0, "-"),
            new Entry("ch5_clear_new_table",           "locked", "2:24", 0, "-"),
        }),
    };

    [MenuItem("SM/Town/Theater 미리보기", false, 15)]
    public static void Open()
    {
        var window = GetWindow<TheaterPreviewBootstrap>("Theater 미리보기");
        window.minSize = new Vector2(1380f, 800f);
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

        InjectEntryList(root);
        InjectPlayerImage(root);
        InjectDetailBanner(root);
    }

    private void InjectEntryList(VisualElement root)
    {
        var list = root.Q<VisualElement>("EntryList");
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
                row.AddToClassList($"thp-entry--{e.State}");

                var thumb = new VisualElement(); thumb.AddToClassList("thp-entry__thumb");
                if (e.State != "locked" && thumbTex != null) thumb.style.backgroundImage = new StyleBackground(thumbTex);
                row.Add(thumb);

                var info = new VisualElement(); info.AddToClassList("thp-entry__info");
                // scene_id 표시명 (truncated)
                var titleLabel = new Label(TruncateLabel(e.Label, 22));
                titleLabel.AddToClassList("thp-entry__title-text");
                info.Add(titleLabel);
                // meta row: duration · completion · watched_at
                var metaRow = new VisualElement(); metaRow.AddToClassList("thp-entry__meta-row");
                var durLabel = new Label(e.Duration); durLabel.AddToClassList("thp-entry__meta-text"); metaRow.Add(durLabel);
                if (e.CompletionPct > 0 && e.CompletionPct < 100)
                {
                    var pctLabel = new Label($"· {e.CompletionPct}%");
                    pctLabel.AddToClassList("thp-entry__meta-text");
                    pctLabel.AddToClassList("thp-entry__meta-text--partial");
                    metaRow.Add(pctLabel);
                }
                if (!string.IsNullOrEmpty(e.WatchedAt) && e.WatchedAt != "-")
                {
                    var watchedLabel = new Label($"· {e.WatchedAt}");
                    watchedLabel.AddToClassList("thp-entry__meta-text");
                    watchedLabel.AddToClassList("thp-entry__meta-text--watched");
                    metaRow.Add(watchedLabel);
                }
                info.Add(metaRow);
                row.Add(info);

                if (e.State == "locked")
                {
                    var lk = new VisualElement(); lk.AddToClassList("thp-entry__lock"); row.Add(lk);
                }
                else if (e.State == "checked" || e.State == "selected")
                {
                    var ck = new VisualElement(); ck.AddToClassList("thp-entry__check"); row.Add(ck);
                }
                // unwatched 상태는 마커 없음 (default)
                group.Add(row);
            }
            list.Add(group);
        }
    }

    private void InjectPlayerImage(VisualElement root)
    {
        var img = root.Q<VisualElement>("PlayerImage");
        if (img == null) return;
        // 시안의 cutscene preview는 narrative 일러. backdrop을 placeholder로 사용 — 실제 art-pipeline에서
        // chapter별 cutscene thumbnail을 별도로 author해야 함.
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropDuskPath);
        if (tex != null) img.style.backgroundImage = new StyleBackground(tex);
    }

    private void InjectDetailBanner(VisualElement root)
    {
        var banner = root.Q<VisualElement>("DetailBanner");
        if (banner == null) return;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropDuskPath);
        if (tex != null) banner.style.backgroundImage = new StyleBackground(tex);
    }

    private static string TruncateLabel(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length > maxLen ? s.Substring(0, maxLen - 1) + "…" : s;
    }
}
