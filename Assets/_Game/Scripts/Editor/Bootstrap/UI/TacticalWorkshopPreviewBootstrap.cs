using System;
using System.Collections.Generic;
using System.Linq;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Tactical Workshop 미리보기 — Sprint 2 real-wire dev tool.
///
/// 진입: real GameSessionRoot 우선 (Profile.ActiveBlueprint의 anchor 6 + SelectedTeamPosture).
/// per-unit tactic은 P1-1 미반영 — Presenter가 빈 리스트 반환 (runtime model 자체 부재).
/// 실패 시 mock fallback (시각 demo는 mock이 더 풍부).
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
    private TacticalWorkshopPresenter? _presenter;

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

        if (TryWireRealSession(_view))
        {
            return;
        }

        _view.Render(BuildMockViewState());
    }

    private bool TryWireRealSession(TacticalWorkshopView view)
    {
        try
        {
            var sessionRoot = PreviewSessionContext.EnsureSession();
            var contentText = new SM.Unity.ContentTextResolver(sessionRoot.Localization, sessionRoot.CombatContentLookup);
            _presenter = new TacticalWorkshopPresenter(
                sessionRoot.SessionState,
                sessionRoot.CombatContentLookup,
                view,
                contentText.GetCharacterName,
                contentText.GetRoleName,
                contentText.GetSynergyName,
                postureSprite: PreviewSessionContext.LoadPostureSprite,
                threatSprite: PreviewSessionContext.LoadThreatSprite,
                classSprite: PreviewSessionContext.LoadClassSprite);
            _presenter.Initialize();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TacticalWorkshopPreview] real-session wire 실패, mock fallback: {e.Message}");
            _presenter = null;
            return false;
        }
    }

    private TacticalWorkshopViewState BuildMockViewState()
    {
        return new TacticalWorkshopViewState(
            Anchors: BuildMockAnchors(),
            Postures: BuildMockPostures(),
            SelectedPostureId: "StandardAdvance",
            SynergyChips: BuildMockSynergyChips(),
            SynergyEmptyText: string.Empty,
            Threats: BuildMockThreats(),
            Tactics: BuildMockTactics(),
            DeployChipLabel: "배치 4/6",
            PostureChipLabel: "태세 · 표준 전진",
            AnswerChipLabel: "위협 답수 6/8",
            AnswerChipWarn: true);
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
                SpriteKey: p.SpriteKey,
                Sprite: LoadPostureSprite(p.SpriteKey),
                KoLabel: p.KoLabel,
                IsSelected: p.Id == "StandardAdvance"))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopSynergyChipViewState> BuildMockSynergyChips()
    {
        // mock — 활성/비활성 텍스트 칩 시각 분기 확인용 (프로덕션은 SquadSynergyPreview 결과).
        return new[]
        {
            new TacticalWorkshopSynergyChipViewState("synergy_solarum", "솔라룸", "2/2", IsActive: true),
            new TacticalWorkshopSynergyChipViewState("synergy_vanguard", "전위", "2/2", IsActive: true),
            new TacticalWorkshopSynergyChipViewState("synergy_ranger", "궁수", "1/2", IsActive: false),
        };
    }

    private IReadOnlyList<TacticalWorkshopThreatViewState> BuildMockThreats()
    {
        // mock answered/partial/unanswered demo — 시각 분기 확인용.
        var states = new[] { "answered", "answered", "unanswered", "answered", "partial", "answered", "partial", "unanswered" };
        var i = 0;
        return TacticalWorkshopPresenter.Threats
            .Select(t => new TacticalWorkshopThreatViewState(
                LaneId: t.Id,
                SpriteKey: t.SpriteKey,
                Sprite: LoadThreatSprite(t.SpriteKey),
                KoLabel: t.KoLabel,
                AnswerState: states[i++]))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopHeroTacticViewState> BuildMockTactics()
    {
        // 4 deploy hero × runtime 실재 요약 — RoleInstruction(anchor/role/bias 3 float) +
        // BehaviorProfile(formation/range). 가짜 condition→action→target RuleSet 폐기 (audit §4.1 P1-1).
        return new[]
        {
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "warden", DisplayName: "Iron Warden",
                AnchorLabel: "전열 상단", RoleLabel: "전열 수호",
                FormationLabel: "전열", RangeLabel: "근접 고수",
                DirectiveLabel: "기본(자동)",
                Biases: new[]
                {
                    new TacticalWorkshopBiasViewState("캐리 보호", 0.72f),
                    new TacticalWorkshopBiasViewState("후열 압박", 0.10f),
                    new TacticalWorkshopBiasViewState("후퇴", 0.15f),
                }),
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "slayer", DisplayName: "Oath Slayer",
                AnchorLabel: "전열 중앙", RoleLabel: "결투 돌격",
                FormationLabel: "전열", RangeLabel: "거리 좁히기",
                DirectiveLabel: "기본(자동)",
                Biases: new[]
                {
                    new TacticalWorkshopBiasViewState("캐리 보호", 0.18f),
                    new TacticalWorkshopBiasViewState("후열 압박", 0.65f),
                    new TacticalWorkshopBiasViewState("후퇴", 0.12f),
                }),
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "hunter", DisplayName: "Longshot Hunter",
                AnchorLabel: "후열 중앙", RoleLabel: "원거리 사격",
                FormationLabel: "후열", RangeLabel: "밴드 유지",
                DirectiveLabel: "기본(자동)",
                Biases: new[]
                {
                    new TacticalWorkshopBiasViewState("캐리 보호", 0.30f),
                    new TacticalWorkshopBiasViewState("후열 압박", 0.48f),
                    new TacticalWorkshopBiasViewState("후퇴", 0.52f),
                }),
            new TacticalWorkshopHeroTacticViewState(
                HeroId: "priest", DisplayName: "Dawn Priest",
                AnchorLabel: "후열 하단", RoleLabel: "치유 지원",
                FormationLabel: "후열", RangeLabel: "밴드 유지",
                DirectiveLabel: "기본(자동)",
                Biases: new[]
                {
                    new TacticalWorkshopBiasViewState("캐리 보호", 0.86f),
                    new TacticalWorkshopBiasViewState("후열 압박", 0.05f),
                    new TacticalWorkshopBiasViewState("후퇴", 0.58f),
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
