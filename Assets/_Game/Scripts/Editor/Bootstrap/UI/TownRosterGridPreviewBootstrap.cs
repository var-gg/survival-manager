using System;
using System.Collections.Generic;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/RosterGrid 미리보기 — Sprint 2 real-wire dev tool.
/// 시안 SoT: pindoc://town-ui-ux-시안-갤러리-v1 (V0 hub) + ux-surface-catalog#Town.RosterGrid.
///
/// 진입: real GameSessionRoot 우선 (PreviewSessionContext.EnsureSession → RosterGridPresenter.Initialize).
/// 실패 시 mock fallback. real path는 default profile의 Heroes (HeroInstanceRecord[]) 그대로 카드화.
/// </summary>
public sealed class TownRosterGridPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/RosterGridPreview.uxml";
    private const string HeroCardTemplatePath = "Assets/_Game/UI/Foundation/Components/HeroPortraitCard.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private RosterGridView? _view;
    private RosterGridPresenter? _presenter;

    [MenuItem("SM/Town/RosterGrid 미리보기", false, 8)]
    public static void Open()
    {
        var window = GetWindow<TownRosterGridPreviewBootstrap>("RosterGrid 미리보기");
        window.minSize = new Vector2(1280f, 720f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root에 surface preview 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        var heroCardTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HeroCardTemplatePath);
        var tokensSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var panelThemeSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);

        if (visualTree == null) { root.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }
        if (heroCardTemplate == null) { root.Add(new Label($"HeroPortraitCard 템플릿 못 찾음: {HeroCardTemplatePath}")); return; }

        if (tokensSheet != null) root.styleSheets.Add(tokensSheet);
        if (panelThemeSheet != null) root.styleSheets.Add(panelThemeSheet);

        visualTree.CloneTree(root);

        _view = new RosterGridView(root, heroCardTemplate);

        if (TryWireRealSession(_view))
        {
            return;
        }

        _view.Render(BuildMockViewState());
    }

    private bool TryWireRealSession(RosterGridView view)
    {
        try
        {
            var sessionRoot = PreviewSessionContext.EnsureSession();
            var contentText = PreviewSessionContext.CreateContentText(sessionRoot);
            _presenter = new RosterGridPresenter(sessionRoot, view, contentText);
            _presenter.Initialize();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RosterGridPreview] real-session wire 실패, mock fallback: {e.Message}");
            _presenter = null;
            return false;
        }
    }

    private RosterGridViewState BuildMockViewState()
    {
        // archetype matrix V1 활성 12명. roster 영속 필드만 — posture/state/last-node는
        // squad·run 컨텍스트라 카드 컬럼에서 제거 (runtime 모델 정합).
        var raw = new (string Name, string Archetype, string Family, string Rarity, int Equip, int Level, int Xp)[]
        {
            // 솔라룸 — 4명
            ("Iron Warden",     "전위 / 솔라룸",      "vanguard", "common", 3,  8, 24),
            ("Oath Slayer",     "결투가 / 솔라룸",    "striker",  "rare",   3, 14, 62),
            ("Longshot Hunter", "궁수 / 솔라룸",      "ranger",   "rare",   2, 11, 38),
            ("Dawn Priest",     "신비 / 솔라룸",      "mystic",   "epic",   3, 18, 84),
            // 이리솔 부족 — 4명
            ("Fang Bulwark",    "전위 / 이리솔",      "beastkin", "common", 1,  5, 12),
            ("Pack Raider",     "결투가 / 이리솔",    "beastkin", "epic",   3, 22, 56),
            ("Trail Scout",     "궁수 / 이리솔",      "beastkin", "rare",   2, 13, 44),
            ("Storm Shaman",    "신비 / 이리솔",      "mystic",   "rare",   2, 12, 30),
            // 회상 결사 — 4명
            ("Crypt Guardian",  "전위 / 회상 결사",   "vanguard", "rare",   3, 15, 68),
            ("Grave Reaver",    "결투가 / 회상 결사", "striker",  "rare",   2, 10, 22),
            ("Dread Marksman",  "궁수 / 회상 결사",   "ranger",   "epic",   3, 20, 76),
            ("Grave Hexer",     "신비 / 회상 결사",   "mystic",   "epic",   3, 19, 88),
        };

        var heroes = new List<RosterGridHeroCardViewState>(raw.Length);
        foreach (var r in raw)
        {
            heroes.Add(new RosterGridHeroCardViewState(
                HeroId: r.Name.ToLowerInvariant().Replace(' ', '_'),  // mock id
                DisplayName: r.Name,
                ArchetypeLabel: r.Archetype,
                FamilyKey: r.Family,
                RarityKey: r.Rarity,
                EquipSlots: r.Equip,
                Level: r.Level,
                XpPct: r.Xp));
        }

        return new RosterGridViewState(
            Heroes: heroes,
            RosterCap: 12,
            SelectedFilterKey: "all");
    }
}
