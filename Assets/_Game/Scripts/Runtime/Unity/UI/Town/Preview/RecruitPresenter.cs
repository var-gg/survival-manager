using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Contracts;
using SM.Meta.Model;
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
    private int _selectedSlotIndex = -1;

    public RecruitPresenter(
        GameSessionRoot root,
        RecruitView view,
        ContentTextResolver contentText,
        SpriteLoader? classSprite = null,
        SpriteLoader? portraitLoader = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        // production hub modal에서는 sprite loader 미주입 (Resources/Addressables 미설치) — null fallback.
        _classSprite = classSprite ?? (_ => null);
        _portraitLoader = portraitLoader ?? (_ => null);
    }

    public void Initialize()
    {
        _view.Bind(this);
        _view.BindClose(Close);
        Refresh();
    }

    public void Open()
    {
        _view.Open();
        Refresh();
    }

    public void Close()
    {
        _view.Close();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    void IRecruitActions.OnRecruitConfirmed(int slotIndex)
    {
        _root.SessionState.Recruit(slotIndex);
        _selectedSlotIndex = -1;
        Refresh();
    }

    void IRecruitActions.OnCandidateSelected(int slotIndex)
    {
        _selectedSlotIndex = slotIndex;
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
        if (offers.Count > 0 && (_selectedSlotIndex < 0 || _selectedSlotIndex >= offers.Count))
        {
            _selectedSlotIndex = FindDefaultSelectedSlot(offers);
        }

        var candidates = new List<RecruitCandidateViewState>(offers.Count);
        for (var i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            _root.CombatContentLookup.TryGetArchetype(offer.UnitBlueprintId, out var archetype);
            var classKey = archetype?.Class?.Id ?? string.Empty;
            // Tags = archetype RecruitPlanTags 파생 (offer엔 tag 리스트 없음). ≤3.
            // raw t.Id는 영문(duelist/frontline 등) → DescribePlanTag로 한국어 매핑.
            var tags = archetype?.RecruitPlanTags?
                .Where(t => t != null && !string.IsNullOrEmpty(t.Id))
                .Select(t => DescribePlanTag(t.Id, t.LegacyDisplayName))
                .Take(3)
                .ToList() ?? new List<string>();

            candidates.Add(new RecruitCandidateViewState(
                SlotIndex: i,
                BlueprintId: offer.UnitBlueprintId,
                DisplayName: _contentText.GetArchetypeName(offer.UnitBlueprintId),
                ClassKey: classKey,
                ClassLabel: DescribeClass(classKey),
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
                ClassSprite: string.IsNullOrEmpty(classKey) ? null : _classSprite(classKey),
                IsSelected: i == _selectedSlotIndex,
                StateChips: BuildStateChips(offer)));
        }

        var selected = candidates.FirstOrDefault(candidate => candidate.IsSelected)
            ?? candidates.FirstOrDefault();
        var wallet = new RecruitWalletViewState(
            GoldHeld: session.Profile.Currencies.Gold,
            EchoHeld: session.Profile.Currencies.Echo);
        return new RecruitViewState(
            Candidates: candidates,
            ActionBar: actionBar,
            Wallet: wallet,
            SelectedCandidateDetail: BuildSelectedDetail(selected, wallet),
            RosterPressure: BuildRosterPressure(session, candidates));
    }

    private RecruitSelectedCandidateDetailViewState? BuildSelectedDetail(
        RecruitCandidateViewState? candidate,
        RecruitWalletViewState wallet)
    {
        if (candidate == null)
        {
            return null;
        }

        return new RecruitSelectedCandidateDetailViewState(
            SlotIndex: candidate.SlotIndex,
            DisplayName: candidate.DisplayName,
            ClassLabel: candidate.ClassLabel,
            ClassKey: candidate.ClassKey,
            TierLabel: DescribeTier(candidate.Tier),
            PlanFitLabel: DescribePlanFit(candidate.PlanFit),
            PlanScoreLabel: candidate.PlanScore > 0 ? $"+{candidate.PlanScore}" : candidate.PlanScore.ToString(),
            CostLabel: $"{candidate.GoldCost} 골드",
            CanAfford: wallet.GoldHeld >= candidate.GoldCost,
            StateChips: candidate.StateChips,
            Tags: candidate.Tags,
            SkillSummary: BuildSkillSummary(candidate),
            PortraitSprite: candidate.PortraitSprite,
            ClassSprite: candidate.ClassSprite,
            Metrics: BuildDecisionMetrics(candidate),
            SkillPips: BuildSkillPips(candidate));
    }

    private static string BuildSkillSummary(RecruitCandidateViewState candidate)
    {
        return $"고정 액티브 — {candidate.SigActive}\n고정 패시브 — {candidate.SigPassive}\n유연 액티브 — {candidate.FlexActive}\n유연 패시브 — {candidate.FlexPassive}";
    }

    private static IReadOnlyList<RecruitDecisionMetricViewState> BuildDecisionMetrics(RecruitCandidateViewState candidate)
    {
        var planBase = candidate.PlanFit switch
        {
            RecruitPlanFit.OnPlan => 76,
            RecruitPlanFit.Bridge => 62,
            _ => 42,
        };
        var signalBase = 46 + (candidate.Tags.Count * 10) + (candidate.ScoutBias ? 10 : 0);
        var safetyBase = candidate.SlotType == RecruitSlotType.Protected || candidate.ProtectedByPity
            ? 88
            : 42 + ((int)candidate.Tier * 14);

        return new[]
        {
            new RecruitDecisionMetricViewState("계획", ClampPercent(planBase + candidate.PlanScore), "plan"),
            new RecruitDecisionMetricViewState("보장", ClampPercent(safetyBase), "safety"),
            new RecruitDecisionMetricViewState("운용", ClampPercent(signalBase), "signal"),
        };
    }

    private static IReadOnlyList<RecruitSkillPipViewState> BuildSkillPips(RecruitCandidateViewState candidate)
    {
        return new[]
        {
            new RecruitSkillPipViewState("고·액", candidate.SigActive, "sig"),
            new RecruitSkillPipViewState("고·패", candidate.SigPassive, "sig"),
            new RecruitSkillPipViewState("유·액", candidate.FlexActive, "flex"),
            new RecruitSkillPipViewState("유·패", candidate.FlexPassive, "flex"),
        };
    }

    private RecruitRosterPressureViewState BuildRosterPressure(
        GameSessionState session,
        IReadOnlyList<RecruitCandidateViewState> candidates)
    {
        const int rosterSoftCap = 12;
        var heroes = session.Profile.Heroes;
        var classCounts = heroes
            .GroupBy(hero => hero.ClassId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var needChips = new[] { "vanguard", "duelist", "ranger", "mystic" }
            .OrderBy(key => classCounts.TryGetValue(key, out var count) ? count : 0)
            .ThenBy(key => key, StringComparer.Ordinal)
            .Take(2)
            .Select(DescribeClassNeed)
            .ToList();
        var onPlanCount = candidates.Count(candidate => candidate.PlanFit == RecruitPlanFit.OnPlan);
        var protectedCount = candidates.Count(candidate => candidate.ProtectedByPity || candidate.SlotType == RecruitSlotType.Protected);
        var scoutCount = candidates.Count(candidate => candidate.ScoutBias);

        return new RecruitRosterPressureViewState(
            RosterCountLabel: $"{heroes.Count} / {rosterSoftCap} 명",
            NeedLabel: heroes.Count >= rosterSoftCap ? "로스터 압박 높음" : "보강 여지 있음",
            PlanSummaryLabel: $"이번 후보 · 계획 적합 {onPlanCount} · 보호 {protectedCount} · 정찰 {scoutCount}",
            NeedChips: needChips);
    }

    private static IReadOnlyList<string> BuildStateChips(RecruitUnitPreview offer)
    {
        var chips = new List<string>(4);
        if (offer.Metadata.PlanFit == CandidatePlanFit.OnPlan || offer.Metadata.SlotType == RecruitOfferSlotType.OnPlan)
        {
            chips.Add("계획 적합");
        }
        if (offer.Metadata.SlotType == RecruitOfferSlotType.Protected)
        {
            chips.Add("보호");
        }
        if (offer.Metadata.ProtectedByPity)
        {
            chips.Add("보정");
        }
        if (offer.Metadata.BiasedByScout)
        {
            chips.Add("정찰");
        }

        return chips;
    }

    private string ResolveSkillName(string? skillId)
        => string.IsNullOrEmpty(skillId) ? "—" : _contentText.GetSkillName(skillId);

    private static int ClampPercent(int value)
        => value < 0 ? 0 : value > 100 ? 100 : value;

    private static string DescribeClass(string classKey) => classKey switch
    {
        "vanguard" => "전열 방패",
        "duelist" => "돌파 결투",
        "ranger" => "후열 사격",
        "mystic" => "주문 지원",
        _ when string.IsNullOrWhiteSpace(classKey) => "분류 미상",
        _ => classKey,
    };

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

    private static string DescribeTier(RecruitTier tier) => tier switch
    {
        RecruitTier.Common => "일반",
        RecruitTier.Rare   => "희귀",
        RecruitTier.Epic   => "영웅",
        _ => tier.ToString(),
    };

    private static string DescribePlanFit(RecruitPlanFit plan) => plan switch
    {
        RecruitPlanFit.OnPlan  => "계획 적합",
        RecruitPlanFit.Bridge  => "연결",
        RecruitPlanFit.OffPlan => "계획 외",
        _ => plan.ToString(),
    };

    private static string DescribeClassNeed(string classKey) => classKey switch
    {
        "vanguard" => "전열 보강 필요",
        "duelist"  => "결투 보강 필요",
        "ranger"   => "사격 보강 필요",
        "mystic"   => "주문 보강 필요",
        _ => $"{classKey} 보강",
    };

    // RecruitPlanTags raw id → 한국어. 데이터 측 LegacyDisplayName이 한국어이면 그걸 우선.
    private static string DescribePlanTag(string tagId, string legacyDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(legacyDisplayName) && !IsAsciiOnly(legacyDisplayName))
        {
            return legacyDisplayName;
        }
        return tagId switch
        {
            "vanguard"      => "전열",
            "duelist"       => "결투",
            "ranger"        => "사격",
            "mystic"        => "주문",
            "frontline"     => "전열 배치",
            "backline"      => "후열 배치",
            "burst"         => "폭발",
            "magical"       => "마법",
            "physical"      => "물리",
            "pierce"        => "관통",
            "shield_skill"  => "방패술",
            "support"       => "지원",
            "skirmish"      => "교전",
            "control"       => "통제",
            "sustain"       => "지속",
            "anti_magic"    => "대마법",
            "anti_armor"    => "대장갑",
            _ => tagId,
        };
    }

    private static bool IsAsciiOnly(string value)
    {
        foreach (var ch in value)
        {
            if (ch > 127) return false;
        }
        return true;
    }

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

    private static int FindDefaultSelectedSlot(IReadOnlyList<RecruitUnitPreview> offers)
    {
        for (var i = 0; i < offers.Count; i++)
        {
            if (offers[i].Metadata.SlotType == RecruitOfferSlotType.OnPlan
                || offers[i].Metadata.PlanFit == CandidatePlanFit.OnPlan)
            {
                return i;
            }
        }

        return 0;
    }
}
