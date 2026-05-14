using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Recruit V1 surface ViewState — runtime offer 모델 (RecruitUnitPreview + RecruitOfferMetadata) 정합.
/// 4 slot pack (StandardA / StandardB / OnPlan / Protected) + Scout 35 Echo + Refresh free 1회 → 2/4/6 Gold cap.
///
/// 카드 컬럼 출처 (3-way audit §4.1 — mockup ↔ pindoc spec ↔ runtime 모델):
/// - ✅ offer 실 field: SlotType / Tier / PlanFit / PlanScore / GoldCost / ProtectedByPity / ScoutBias / FlexActive / FlexPassive
/// - ◐ archetype 파생 (offer field 아님): DisplayName / ClassKey / Tags(RecruitPlanTags) /
///   SigActive·SigPassive(LockedSignature*Skill — archetype 고정, offer가 굴리는 게 아님)
/// - ❌ 제거: HeroId(offer엔 hero 인스턴스 없음 → BlueprintId) · Formation(offer에 anchor/formation field 없음)
/// </summary>
public enum RecruitSlotType { StandardA, StandardB, OnPlan, Protected }
public enum RecruitTier { Common = 1, Rare = 2, Epic = 3 }
public enum RecruitPlanFit { OnPlan, Bridge, OffPlan }

public sealed record RecruitCandidateViewState(
    int SlotIndex,                          // 0..3 — Recruit(index) API
    string BlueprintId,                     // ← RecruitUnitPreview.UnitBlueprintId (archetype id, hero 인스턴스 아님)
    string DisplayName,                     // ◐ archetype NameKey resolved
    string ClassKey,                        // ◐ archetype Class.Id — vanguard / duelist / ranger / mystic
    RecruitSlotType SlotType,               // ✅ Metadata.SlotType
    RecruitTier Tier,                       // ✅ Metadata.Tier
    RecruitPlanFit PlanFit,                 // ✅ Metadata.PlanFit
    int PlanScore,                          // ✅ Metadata.PlanScore.Total (6-component breakdown 합)
    int GoldCost,                           // ✅ Metadata.GoldCost
    bool ProtectedByPity,                   // ✅ Metadata.ProtectedByPity — pity 보정으로 tier 보장된 offer
    bool ScoutBias,                         // ✅ Metadata.BiasedByScout — scout directive 영향 받은 slot
    IReadOnlyList<string> Tags,             // ◐ archetype RecruitPlanTags ≤3 (offer field 아님 — archetype 파생)
    string SigActive,                       // ◐ archetype LockedSignatureActiveSkill — archetype 고정
    string SigPassive,                      // ◐ archetype LockedSignaturePassiveSkill — archetype 고정
    string FlexActive,                      // ✅ offer.FlexActiveId — rolled (offer가 실제로 굴린 값)
    string FlexPassive,                     // ✅ offer.FlexPassiveId — rolled
    Texture2D? PortraitSprite,
    Texture2D? ClassSprite
);

public sealed record RecruitActionBarViewState(
    int ScoutEchoCost,                      // 35 — RecruitmentBalanceCatalog.ScoutEchoCost
    bool CanUseScout,                       // phase당 1회 — !RecruitPhaseState.ScoutUsedThisPhase
    string ScoutDirectiveLabel,             // RecruitPhaseState.PendingScoutDirective.Kind 표시명 (None="미지정")
    int FreeRefreshesRemaining,             // 0 또는 1
    int CurrentPaidRefreshCost              // 2 / 4 / 6 — paid escalation
);

public sealed record RecruitViewState(
    IReadOnlyList<RecruitCandidateViewState> Candidates,
    RecruitActionBarViewState ActionBar
);
