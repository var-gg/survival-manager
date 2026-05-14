using System.Collections.Generic;
using System.Linq;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Tactical Workshop 미리보기 — Sprint 1 presenter 패턴 dev tool.
///
/// Bootstrap = mock ViewState injection. 실 GameSessionRoot 없이 View 직접 render.
/// 패턴: 모든 mock surface가 같은 구조 — ViewState (runtime asmdef) + View (runtime asmdef) + Bootstrap (editor).
/// Production: GameSessionRoot + Presenter가 ViewState 빌드 → View.Render 동일 코드 path.
///
/// 시안 SoT: pindoc://town-ui-ux-시안-갤러리-v1 (1. Tactical Workshop modal)
/// 워크플로우 SoT: docs/02_design/ui/town-profile-binding-workflow.md
/// </summary>
public sealed class TacticalWorkshopPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/TacticalWorkshopPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string PostureSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Posture/posture_{0}.png";
    private const string ThreatSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Threat/threat_{0}.png";
    private const string ClassSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";

    private TacticalWorkshopView? _view;

    [MenuItem("SM/Town/Tactical Workshop 미리보기", false, 9)]
    public static void Open()
    {
        var window = GetWindow<TacticalWorkshopPreviewBootstrap>("Tactical Workshop 미리보기");
        window.minSize = new Vector2(1440f, 820f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root에 surface preview 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null) { root.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        var tokensSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var panelThemeSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        if (tokensSheet != null) root.styleSheets.Add(tokensSheet);
        if (panelThemeSheet != null) root.styleSheets.Add(panelThemeSheet);

        visualTree.CloneTree(root);

        _view = new TacticalWorkshopView(root);
        _view.Render(BuildMockViewState());
    }

    private TacticalWorkshopViewState BuildMockViewState()
    {
        return new TacticalWorkshopViewState(
            Anchors: BuildMockAnchors(),
            Postures: BuildMockPostures(),
            SelectedPostureId: "StandardAdvance",
            SynergyChips: BuildMockSynergyChips(),
            Threats: BuildMockThreats(),
            Tactics: BuildMockTactics());
    }

    private IReadOnlyList<TacticalWorkshopAnchorViewState> BuildMockAnchors()
    {
        // mock fixture: 4 filled + 2 empty (FrontBottom, BackTop).
        // archetype-matrix V1 — class별 sprite mapping.
        return new[]
        {
            new TacticalWorkshopAnchorViewState("FrontTop",    "warden",  LoadClassSprite("vanguard")),
            new TacticalWorkshopAnchorViewState("FrontCenter", "slayer",  LoadClassSprite("duelist")),
            new TacticalWorkshopAnchorViewState("FrontBottom", string.Empty, null),
            new TacticalWorkshopAnchorViewState("BackTop",     string.Empty, null),
            new TacticalWorkshopAnchorViewState("BackCenter",  "hunter",  LoadClassSprite("ranger")),
            new TacticalWorkshopAnchorViewState("BackBottom",  "priest",  LoadClassSprite("mystic")),
        };
    }

    private IReadOnlyList<TacticalWorkshopPostureViewState> BuildMockPostures()
    {
        // Presenter의 static catalog 재사용 — fixture와 spec 1 source.
        return TacticalWorkshopPresenter.Postures
            .Select(p => new TacticalWorkshopPostureViewState(
                PostureId: p.Id,
                Sprite: LoadPostureSprite(p.SpriteKey),
                KoLabel: p.KoLabel,
                IsSelected: p.Id == "StandardAdvance"))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopSynergyChipViewState> BuildMockSynergyChips()
    {
        return TacticalWorkshopPresenter.Synergies
            .Select(s => new TacticalWorkshopSynergyChipViewState(
                SynergyId: s.Id,
                Group: s.Group,
                Sprite: s.Group == "class" ? LoadClassSprite(s.SpriteKey) : null,
                KoLabel: s.KoLabel))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopThreatViewState> BuildMockThreats()
    {
        // mock answered/unanswered demo — 시각 분기 확인용.
        var states = new[] { "answered", "answered", "unanswered", "answered", string.Empty, "answered", string.Empty, "unanswered" };
        var i = 0;
        return TacticalWorkshopPresenter.Threats
            .Select(t => new TacticalWorkshopThreatViewState(
                LaneId: t.Id,
                Sprite: LoadThreatSprite(t.SpriteKey),
                KoLabel: t.KoLabel,
                AnswerState: states[i++]))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopHeroTacticViewState> BuildMockTactics()
    {
        // 4 deploy hero × N rule per hero. pindoc V1 wiki-combat-posture-tactic-v1 condition/action/target enum.
        return new[]
        {
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "warden",
                DisplayName: "Iron Warden",
                PostureLabel: "전열 사수",
                Rules: new[]
                {
                    new TacticalWorkshopRuleViewState(1, "SelfHpBelow",  "ActiveSkill", "Self"),
                    new TacticalWorkshopRuleViewState(2, "EnemyInRange", "BasicAttack", "FirstEnemyInRange"),
                    new TacticalWorkshopRuleViewState(3, "Fallback",     "WaitDefend",  "Self"),
                }),
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "slayer",
                DisplayName: "Oath Slayer",
                PostureLabel: "표준 전진",
                Rules: new[]
                {
                    new TacticalWorkshopRuleViewState(1, "EnemyExposed", "ActiveSkill", "MostExposedEnemy"),
                    new TacticalWorkshopRuleViewState(2, "EnemyInRange", "BasicAttack", "FirstEnemyInRange"),
                    new TacticalWorkshopRuleViewState(3, "Fallback",     "WaitDefend",  "Self"),
                }),
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "hunter",
                DisplayName: "Longshot Hunter",
                PostureLabel: "표준 전진",
                Rules: new[]
                {
                    new TacticalWorkshopRuleViewState(1, "LowestHpEnemy", "ActiveSkill", "LowestHpEnemy"),
                    new TacticalWorkshopRuleViewState(2, "EnemyInRange",  "BasicAttack", "FirstEnemyInRange"),
                    new TacticalWorkshopRuleViewState(3, "Fallback",      "WaitDefend",  "Self"),
                }),
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "priest",
                DisplayName: "Dawn Priest",
                PostureLabel: "캐리 보호",
                Rules: new[]
                {
                    new TacticalWorkshopRuleViewState(1, "AllyHpBelow",  "ActiveSkill", "LowestHpAlly"),
                    new TacticalWorkshopRuleViewState(2, "EnemyInRange", "BasicAttack", "NearestEnemy"),
                    new TacticalWorkshopRuleViewState(3, "Fallback",     "WaitDefend",  "Self"),
                }),
        };
    }

    private static Texture2D? LoadPostureSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(PostureSpriteFmt, key));
    private static Texture2D? LoadThreatSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ThreatSpriteFmt, key));
    private static Texture2D? LoadClassSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ClassSpriteFmt, key));
}
