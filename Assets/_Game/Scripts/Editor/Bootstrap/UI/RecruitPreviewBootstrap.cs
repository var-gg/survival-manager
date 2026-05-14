using System.Collections.Generic;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Recruit 미리보기 — Sprint 1 presenter 패턴 dev tool.
/// 카드 컬럼은 runtime offer 모델 (RecruitUnitPreview + RecruitOfferMetadata) 정합 — audit §4.1.
/// </summary>
public sealed class RecruitPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/RecruitPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string ClassSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";
    private const string PortraitPathFmt = "Assets/Resources/_Game/Art/Characters/hero_{0}/portrait_full.png";

    private RecruitView? _view;

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
        _view.Render(BuildMockViewState());
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
                ClassSprite: AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ClassSpriteFmt, r.ClassKey))));
        }

        var actionBar = new RecruitActionBarViewState(
            ScoutEchoCost: 35,
            CanUseScout: true,
            ScoutDirectiveLabel: "후열",   // PendingScoutDirective.Kind = Backline 예시
            FreeRefreshesRemaining: 0,    // 이미 free 사용
            CurrentPaidRefreshCost: 4);    // 2→4→6 cap의 2회차

        return new RecruitViewState(candidates, actionBar);
    }
}
