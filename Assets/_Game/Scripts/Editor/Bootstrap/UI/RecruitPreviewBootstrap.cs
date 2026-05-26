using System;
using System.Collections.Generic;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Recruit 미리보기 — Sprint 2 real-wire dev tool.
/// 카드 컬럼은 runtime offer 모델 (RecruitUnitPreview + RecruitOfferMetadata) 정합 — audit §4.1.
///
/// 진입: real GameSessionRoot 우선 (PreviewSessionContext.EnsureSession → RecruitPresenter.Initialize).
/// 실패 시 mock fallback (안전망). real path가 default profile의 4 RecruitOffers를 그대로 렌더.
/// </summary>
public sealed class RecruitPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/RecruitPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string ClassSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";
    private const string PortraitPathFmt = "Assets/Resources/_Game/Art/Characters/hero_{0}/portrait_full.png";

    private RecruitView? _view;
    private RecruitPresenter? _presenter;

    [MenuItem("SM/Town/Recruit 미리보기", false, 10)]
    public static void Open()
    {
        var window = GetWindow<RecruitPreviewBootstrap>("Recruit 미리보기");
        window.minSize = new Vector2(1280f, 760f);
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

        _view = new RecruitView(root);

        if (TryWireRealSession(_view))
        {
            return;
        }

        _view.Render(BuildMockViewState());
    }

    private bool TryWireRealSession(RecruitView view)
    {
        try
        {
            var sessionRoot = PreviewSessionContext.EnsureSession();
            var contentText = PreviewSessionContext.CreateContentText(sessionRoot);
            _presenter = new RecruitPresenter(
                sessionRoot,
                view,
                contentText,
                PreviewSessionContext.LoadClassSprite,
                PreviewSessionContext.LoadHeroPortrait);
            _presenter.Initialize();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RecruitPreview] real-session wire 실패, mock fallback: {e.Message}");
            _presenter = null;
            return false;
        }
    }

    private RecruitViewState BuildMockViewState()
    {
        // 4 mock candidate — V1 활성 archetype matrix에서 (blueprint id = archetype id, portrait path 키).
        // 컬럼 출처: offer 실 field (slot/tier/plan/planScore/gold/pity/scout) + archetype 파생 (class/tags/sig) + offer rolled (flex).
        var raw = new (string BlueprintId, string Name, string ClassKey, RecruitSlotType Slot, RecruitTier Tier, RecruitPlanFit Plan, int PlanScore, int Gold, bool Pity, bool Scout, string[] Tags, string SigA, string SigP, string FlexA, string FlexP)[]
        {
            ("pack_raider", "Pack Raider", "duelist",
                RecruitSlotType.StandardA, RecruitTier.Common, RecruitPlanFit.OffPlan, 6, 80, false, false,
                new[] { "#pack", "#burst", "#dive" },
                "어흥 분쇄", "피의 광기", "돌격 강타", "출혈 추적"),
            ("dawn_priest", "Dawn Priest", "mystic",
                RecruitSlotType.StandardB, RecruitTier.Rare, RecruitPlanFit.Bridge, 21, 150, false, false,
                new[] { "#heal", "#light", "#protect" },
                "새벽 기도", "빛의 은신", "성광 세례", "마나 격류"),
            ("trail_scout", "Trail Scout", "ranger",
                RecruitSlotType.OnPlan, RecruitTier.Rare, RecruitPlanFit.OnPlan, 38, 150, false, true,
                new[] { "#scout", "#mobile", "#finisher" },
                "추적의 화살", "숲의 은신", "연사 세례", "예리한 눈"),
            ("grave_hexer", "Grave Hexer", "mystic",
                RecruitSlotType.Protected, RecruitTier.Epic, RecruitPlanFit.OnPlan, 44, 280, true, false,
                new[] { "#hex", "#dot", "#control" },
                "망령의 저주", "죽음의 친화", "어둠 화살", "무덤 시야"),
        };

        var candidates = new List<RecruitCandidateViewState>(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var r = raw[i];
            candidates.Add(new RecruitCandidateViewState(
                SlotIndex: i,
                BlueprintId: r.BlueprintId,
                DisplayName: r.Name,
                ClassKey: r.ClassKey,
                ClassLabel: DescribeClass(r.ClassKey),
                SlotType: r.Slot,
                Tier: r.Tier,
                PlanFit: r.Plan,
                PlanScore: r.PlanScore,
                GoldCost: r.Gold,
                ProtectedByPity: r.Pity,
                ScoutBias: r.Scout,
                Tags: r.Tags,
                SigActive: r.SigA,
                SigPassive: r.SigP,
                FlexActive: r.FlexA,
                FlexPassive: r.FlexP,
                PortraitSprite: AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(PortraitPathFmt, r.BlueprintId)),
                ClassSprite: AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ClassSpriteFmt, r.ClassKey)),
                IsSelected: i == 2,
                StateChips: BuildMockStateChips(r.Slot, r.Plan, r.Pity, r.Scout)));
        }

        var actionBar = new RecruitActionBarViewState(
            ScoutEchoCost: 35,
            CanUseScout: true,
            ScoutDirectiveLabel: "후열",   // PendingScoutDirective.Kind = Backline 예시
            FreeRefreshesRemaining: 0,    // 이미 free 사용
            CurrentPaidRefreshCost: 4);    // 2→4→6 cap의 2회차

        var selected = candidates[2];
        var wallet = new RecruitWalletViewState(GoldHeld: 18, EchoHeld: 92);
        var detail = new RecruitSelectedCandidateDetailViewState(
            SlotIndex: selected.SlotIndex,
            DisplayName: selected.DisplayName,
            ClassLabel: selected.ClassLabel,
            ClassKey: selected.ClassKey,
            TierLabel: DescribeMockTier(selected.Tier),
            PlanFitLabel: DescribeMockPlanFit(selected.PlanFit),
            PlanScoreLabel: $"+{selected.PlanScore}",
            CostLabel: $"{selected.GoldCost} 골드",
            CanAfford: wallet.GoldHeld >= selected.GoldCost,
            StateChips: selected.StateChips,
            Tags: selected.Tags,
            SkillSummary: $"고정 액티브 — {selected.SigActive}\n고정 패시브 — {selected.SigPassive}\n유연 액티브 — {selected.FlexActive}\n유연 패시브 — {selected.FlexPassive}",
            PortraitSprite: selected.PortraitSprite,
            ClassSprite: selected.ClassSprite,
            Metrics: BuildMockMetrics(selected),
            SkillPips: BuildMockSkillPips(selected));
        var pressure = new RecruitRosterPressureViewState(
            RosterCountLabel: "8 / 12 명",
            NeedLabel: "보강 여지 있음",
            PlanSummaryLabel: "이번 후보 · 계획 적합 2 · 보호 1 · 정찰 1",
            NeedChips: new[] { "사격 보강 필요", "전열 보강 필요" });

        return new RecruitViewState(candidates, actionBar, wallet, detail, pressure);
    }

    private static string DescribeMockTier(RecruitTier tier) => tier switch
    {
        RecruitTier.Common => "일반",
        RecruitTier.Rare   => "희귀",
        RecruitTier.Epic   => "영웅",
        _ => tier.ToString(),
    };

    private static string DescribeMockPlanFit(RecruitPlanFit plan) => plan switch
    {
        RecruitPlanFit.OnPlan  => "계획 적합",
        RecruitPlanFit.Bridge  => "연결",
        RecruitPlanFit.OffPlan => "계획 외",
        _ => plan.ToString(),
    };

    private static IReadOnlyList<string> BuildMockStateChips(
        RecruitSlotType slot,
        RecruitPlanFit plan,
        bool pity,
        bool scout)
    {
        var chips = new List<string>(4);
        if (slot == RecruitSlotType.OnPlan || plan == RecruitPlanFit.OnPlan) chips.Add("계획 적합");
        if (slot == RecruitSlotType.Protected) chips.Add("보호");
        if (pity) chips.Add("보정");
        if (scout) chips.Add("정찰");
        return chips;
    }

    private static IReadOnlyList<RecruitDecisionMetricViewState> BuildMockMetrics(RecruitCandidateViewState selected)
    {
        return new[]
        {
            new RecruitDecisionMetricViewState("계획", Mathf.Clamp(58 + selected.PlanScore, 0, 100), "plan"),
            new RecruitDecisionMetricViewState("보장", selected.ProtectedByPity ? 88 : 42 + ((int)selected.Tier * 14), "safety"),
            new RecruitDecisionMetricViewState("운용", Mathf.Clamp(46 + (selected.Tags.Count * 10) + (selected.ScoutBias ? 10 : 0), 0, 100), "signal"),
        };
    }

    private static IReadOnlyList<RecruitSkillPipViewState> BuildMockSkillPips(RecruitCandidateViewState selected)
    {
        return new[]
        {
            new RecruitSkillPipViewState("SA", selected.SigActive, "sig"),
            new RecruitSkillPipViewState("SP", selected.SigPassive, "sig"),
            new RecruitSkillPipViewState("FA", selected.FlexActive, "flex"),
            new RecruitSkillPipViewState("FP", selected.FlexPassive, "flex"),
        };
    }

    private static string DescribeClass(string classKey) => classKey switch
    {
        "vanguard" => "전열 방패",
        "duelist" => "돌파 결투",
        "ranger" => "후열 사격",
        "mystic" => "주문 지원",
        _ => classKey,
    };
}
