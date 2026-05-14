using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Contracts;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Recruit V1 Presenter — GameSessionState.RecruitOffers (RecruitUnitPreview 4 slot) → ViewState.
///
/// 카드 컬럼 출처 (audit §4.1 — 3-way 정합):
/// - offer 실 field: SlotType / Tier / PlanFit / PlanScore / GoldCost / ProtectedByPity / BiasedByScout / FlexActiveId / FlexPassiveId
/// - archetype 파생: DisplayName / Class / RecruitPlanTags / LockedSignature*Skill
///   (signature는 archetype 고정 — offer가 굴리는 건 flex 2개뿐).
///
/// 워크플로우: 카드 클릭 → SessionState.Recruit(slotIndex) → Profile.Heroes에 새 hero 추가 → BattleTest에서 deploy 가능.
/// </summary>
public sealed class RecruitPresenter : IRecruitActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    public const int ScoutEchoCostV1 = 35;  // RecruitmentBalanceCatalog.ScoutEchoCost

    private readonly GameSessionRoot _root;
    private readonly RecruitView _view;
    private readonly ContentTextResolver _contentText;
    private readonly SpriteLoader _classSprite;
    private readonly SpriteLoader _portraitLoader;

    public RecruitPresenter(
        GameSessionRoot root,
        RecruitView view,
        ContentTextResolver contentText,
        SpriteLoader classSprite,
        SpriteLoader portraitLoader)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _classSprite = classSprite ?? throw new ArgumentNullException(nameof(classSprite));
        _portraitLoader = portraitLoader ?? throw new ArgumentNullException(nameof(portraitLoader));
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    void IRecruitActions.OnRecruitConfirmed(int slotIndex)
    {
        _root.SessionState.Recruit(slotIndex);
        Refresh();
    }

    void IRecruitActions.OnScoutClicked()
    {
        // TODO Sprint 3: scout = 6-kind ScoutDirective (Frontline/Backline/Physical/Magical/Support/SynergyTag).
        // 실 surface는 directive 선택 UI가 필요 — preview는 PendingScoutDirective 표시만.
        // TownScreenPresenter.ResolveScoutDirective 패턴 참고.
    }

    void IRecruitActions.OnRefreshClicked()
    {
        _root.SessionState.RerollRecruitOffers();
        Refresh();
    }

    private RecruitViewState BuildState()
    {
        var session = _root.SessionState;
        var phase = session.RecruitPhase;
        var scout = phase.PendingScoutDirective;
        var actionBar = new RecruitActionBarViewState(
            ScoutEchoCost: ScoutEchoCostV1,
            CanUseScout: session.CanUseScout,
            ScoutDirectiveLabel: DescribeScoutDirective(
                scout?.Kind ?? ScoutDirectiveKind.None,
                scout?.SynergyTagId ?? string.Empty),
            FreeRefreshesRemaining: phase.FreeRefreshesRemaining,
            CurrentPaidRefreshCost: session.CurrentRecruitRefreshCost);

        var offers = session.RecruitOffers;
        var candidates = new List<RecruitCandidateViewState>(offers.Count);
        for (var i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            _root.CombatContentLookup.TryGetArchetype(offer.UnitBlueprintId, out var archetype);
            var classKey = archetype?.Class?.Id ?? string.Empty;
            // Tags = archetype RecruitPlanTags 파생 (offer엔 tag 리스트 없음). ≤3.
            var tags = archetype?.RecruitPlanTags?
                .Where(t => t != null && !string.IsNullOrEmpty(t.Id))
                .Select(t => t.Id)
                .Take(3)
                .ToList() ?? new List<string>();

            candidates.Add(new RecruitCandidateViewState(
                SlotIndex: i,
                BlueprintId: offer.UnitBlueprintId,
                DisplayName: _contentText.GetArchetypeName(offer.UnitBlueprintId),
                ClassKey: classKey,
                SlotType: MapSlotType(offer.Metadata.SlotType),
                Tier: MapTier(offer.Metadata.Tier),
                PlanFit: MapPlanFit(offer.Metadata.PlanFit),
                PlanScore: offer.Metadata.PlanScore?.Total ?? 0,
                GoldCost: offer.Metadata.GoldCost,
                ProtectedByPity: offer.Metadata.ProtectedByPity,
                ScoutBias: offer.Metadata.BiasedByScout,
                Tags: tags,
                // signature는 archetype 고정 (offer field 아님) — LockedSignature*Skill
                SigActive: ResolveSkillName(archetype?.LockedSignatureActiveSkill?.Id),
                SigPassive: ResolveSkillName(archetype?.LockedSignaturePassiveSkill?.Id),
                // flex는 offer가 실제로 굴린 값 (FlexActiveId / FlexPassiveId)
                FlexActive: ResolveSkillName(offer.FlexActiveId),
                FlexPassive: ResolveSkillName(offer.FlexPassiveId),
                PortraitSprite: _portraitLoader(offer.UnitBlueprintId),
                ClassSprite: string.IsNullOrEmpty(classKey) ? null : _classSprite(classKey)));
        }

        return new RecruitViewState(
            Candidates: candidates,
            ActionBar: actionBar);
    }

    private string ResolveSkillName(string? skillId)
        => string.IsNullOrEmpty(skillId) ? "—" : _contentText.GetSkillName(skillId);

    private static string DescribeScoutDirective(ScoutDirectiveKind kind, string synergyTagId) => kind switch
    {
        ScoutDirectiveKind.Frontline  => "전열",
        ScoutDirectiveKind.Backline   => "후열",
        ScoutDirectiveKind.Physical   => "물리",
        ScoutDirectiveKind.Magical    => "마법",
        ScoutDirectiveKind.Support    => "지원",
        ScoutDirectiveKind.SynergyTag => string.IsNullOrEmpty(synergyTagId) ? "시너지" : synergyTagId,
        _ => "미지정",
    };

    private static RecruitSlotType MapSlotType(RecruitOfferSlotType slotType) => slotType switch
    {
        RecruitOfferSlotType.OnPlan    => RecruitSlotType.OnPlan,
        RecruitOfferSlotType.Protected => RecruitSlotType.Protected,
        RecruitOfferSlotType.StandardB => RecruitSlotType.StandardB,
        _ => RecruitSlotType.StandardA,
    };

    private static RecruitTier MapTier(SM.Core.Contracts.RecruitTier tier) => tier switch
    {
        SM.Core.Contracts.RecruitTier.Rare => RecruitTier.Rare,
        SM.Core.Contracts.RecruitTier.Epic => RecruitTier.Epic,
        _ => RecruitTier.Common,
    };

    private static RecruitPlanFit MapPlanFit(CandidatePlanFit plan) => plan switch
    {
        CandidatePlanFit.OnPlan => RecruitPlanFit.OnPlan,
        CandidatePlanFit.Bridge => RecruitPlanFit.Bridge,
        _ => RecruitPlanFit.OffPlan,
    };
}
