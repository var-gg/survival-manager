using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Tactical Workshop 프로덕션 ViewState — GameSessionState → View 변환의 중간 스냅샷.
///
/// pindoc V1 wiki SoT + runtime 모델 정합 (audit §4.1 P1-1):
/// - 6 anchor (deployment-and-anchors.md): Front × 3 + Back × 3, 4 deploy. anchor pad는
///   **read-only reference** — anchor 편집은 SquadBuilder(전술 설정) 책임 (audit §2.2).
/// - 5 posture (wiki-combat-posture-tactic-v1): HoldLine / StandardAdvance / ProtectCarry / CollapseWeakSide / AllInBackline.
/// - synergy chip: content SoT(snapshot.SynergyCatalog) 기반 SquadSynergyPreview 결과 — 정적 family 목록 폐기.
/// - threat lane: SquadCounterCoveragePreview.Dimensions 8차원과 id 일치 — 정적 lane 어휘 폐기.
/// - per-unit tactic: runtime 실재는 `RoleInstructionDefinition`(RoleTag + bias 3 float) +
///   `BehaviorProfileDefinition`(FormationLine + RangeDiscipline) + P1 타겟 지시. condition→action→target
///   rule chain 모델은 runtime에 없음 — 가짜 RuleSet을 만들지 않는다.
///
/// Texture2D는 caller가 pre-resolve (Editor: AssetDatabase, Runtime: null 허용 — USS art class fallback).
///
/// Codex legacy `SM.Unity.UI.TacticalWorkshop` 네임스페이스와 충돌 피하기 위해
/// `SM.Unity.UI.Town.Preview` namespace 사용 — V1 redesign surface 묶음 자리.
/// </summary>
public sealed record TacticalWorkshopAnchorViewState(
    string AnchorId,
    string AssignedHeroId,
    Texture2D? AssignedFigure,
    string ClassKey = ""
);

public sealed record TacticalWorkshopPostureViewState(
    string PostureId,
    string SpriteKey,          // USS art class 키 (twp-posture-card--art-{key})
    Texture2D? Sprite,
    string KoLabel,
    bool IsSelected
);

/// <summary>발동 시너지 칩 — SquadSynergyPreview 결과 1건. CountLabel은 "현재/경계" (예: 2/3).</summary>
public sealed record TacticalWorkshopSynergyChipViewState(
    string SynergyId,
    string KoLabel,
    string CountLabel,
    bool IsActive
);

/// <summary>위협 답수 lane — LaneId는 SquadCounterCoveragePreview.Dimensions와 일치.
/// AnswerState: "" (미평가) / answered / partial / unanswered.</summary>
public sealed record TacticalWorkshopThreatViewState(
    string LaneId,
    string SpriteKey,          // USS art class 키 (twp-threat-chip--art-{key})
    Texture2D? Sprite,
    string KoLabel,
    string AnswerState
);

/// <summary>
/// RoleInstructionDefinition의 bias float 1개 (ProtectCarryBias / BacklinePressureBias / RetreatBias). 0..1.
/// </summary>
public sealed record TacticalWorkshopBiasViewState(
    string Label,
    float Value
);

/// <summary>
/// per-unit tactic = runtime 실재 요약. 배치 anchor + RoleInstruction(RoleTag + bias) +
/// BehaviorProfile(Formation + Range) + P1 타겟 지시(유일한 편집 지점 — 클릭 cycle).
/// 가짜 condition→action→target RuleSet 폐기 (audit §4.1 P1-1 — runtime에 rule chain 모델 없음).
/// </summary>
public sealed record TacticalWorkshopHeroTacticViewState(
    string HeroId,
    string DisplayName,
    string AnchorLabel,        // ← 현재 DeploymentAnchorId (배치 위치)
    string RoleLabel,          // ← RoleInstructionDefinition.RoleTag (역할)
    string FormationLabel,     // ← BehaviorProfileDefinition.FormationLine (전열/중열/후열)
    string RangeLabel,         // ← BehaviorProfileDefinition.RangeDiscipline (거리 규율)
    string DirectiveLabel,     // ← PlayerTargetDirective 표시명 (클릭 cycle 편집)
    IReadOnlyList<TacticalWorkshopBiasViewState> Biases   // RoleInstruction bias 3 float
);

public sealed record TacticalWorkshopViewState(
    IReadOnlyList<TacticalWorkshopAnchorViewState> Anchors,
    IReadOnlyList<TacticalWorkshopPostureViewState> Postures,
    string SelectedPostureId,
    IReadOnlyList<TacticalWorkshopSynergyChipViewState> SynergyChips,
    string SynergyEmptyText,   // 칩이 없을 때 이유 안내 (칩 존재 시 빈 문자열)
    IReadOnlyList<TacticalWorkshopThreatViewState> Threats,
    IReadOnlyList<TacticalWorkshopHeroTacticViewState> Tactics,
    string DeployChipLabel,    // 헤더 command strip — "배치 N/6"
    string PostureChipLabel,   // 헤더 command strip — "태세 · {표시명}"
    string AnswerChipLabel,    // 헤더 command strip — "위협 답수 N/8" (미평가 시 "위협 답수 —")
    bool AnswerChipWarn        // 취약(gap) lane 존재 시 true → warn 스타일
);
