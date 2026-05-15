using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Tactical Workshop V1 surface ViewState — profile binding workflow의 Sprint 1.
///
/// pindoc V1 wiki SoT + runtime 모델 정합 (audit §4.1 P1-1):
/// - 6 anchor (deployment-and-anchors.md): Front × 3 + Back × 3, 4 deploy. anchor pad는
///   **read-only reference** — anchor 편집은 SquadBuilder 책임 (audit §2.2).
/// - 5 posture (wiki-combat-posture-tactic-v1): HoldLine / StandardAdvance / ProtectCarry / CollapseWeakSide / AllInBackline.
/// - 7 synergy family (wiki-combat-synergy-v1): race 3 + class 4.
/// - 8 threat lane (wiki-combat-counter-topology-v1): ArmorFrontline 등.
/// - per-unit tactic: runtime 실재는 `RoleInstructionDefinition`(Anchor + RoleTag + bias 3 float) +
///   `BehaviorProfileDefinition`(FormationLine + RangeDiscipline + 튜닝 float). condition→action→target
///   rule chain 모델은 runtime에 없음 — 가짜 RuleSet 폐기, anchor/role/bias 요약으로 재정의.
///
/// 본 ViewState는 profile (GameSessionState.Profile) → View 변환의 중간 데이터.
/// Texture2D는 caller가 pre-resolve (Editor: AssetDatabase, Runtime: Resources/Addressables).
///
/// Codex legacy `SM.Unity.UI.TacticalWorkshop` 네임스페이스와 충돌 피하기 위해
/// `SM.Unity.UI.Town.Preview` namespace 사용 — V1 redesign surface 묶음 자리.
/// </summary>
public sealed record TacticalWorkshopAnchorViewState(
    string AnchorId,
    string AssignedHeroId,
    Texture2D? AssignedFigure
);

public sealed record TacticalWorkshopPostureViewState(
    string PostureId,
    Texture2D? Sprite,
    string KoLabel,
    bool IsSelected
);

public sealed record TacticalWorkshopSynergyChipViewState(
    string SynergyId,
    string Group,
    Texture2D? Sprite,
    string KoLabel
);

public sealed record TacticalWorkshopThreatViewState(
    string LaneId,
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
/// per-unit "tactic" = runtime 실재 요약 — read-only display.
/// `RoleInstructionDefinition`(Anchor + RoleTag + bias) + `BehaviorProfileDefinition`(Formation + Range).
/// 가짜 condition→action→target RuleSet 폐기 (audit §4.1 P1-1 — runtime에 rule chain 모델 없음).
/// </summary>
public sealed record TacticalWorkshopHeroTacticViewState(
    string HeroId,
    string DisplayName,
    string AnchorLabel,        // ← RoleInstructionDefinition.Anchor (배치 위치)
    string RoleLabel,          // ← RoleInstructionDefinition.RoleTag (역할)
    string FormationLabel,     // ← BehaviorProfileDefinition.FormationLine (전열/중열/후열)
    string RangeLabel,         // ← BehaviorProfileDefinition.RangeDiscipline (거리 규율)
    IReadOnlyList<TacticalWorkshopBiasViewState> Biases   // RoleInstruction bias 3 float
);

public sealed record TacticalWorkshopViewState(
    IReadOnlyList<TacticalWorkshopAnchorViewState> Anchors,
    IReadOnlyList<TacticalWorkshopPostureViewState> Postures,
    string SelectedPostureId,
    IReadOnlyList<TacticalWorkshopSynergyChipViewState> SynergyChips,
    IReadOnlyList<TacticalWorkshopThreatViewState> Threats,
    IReadOnlyList<TacticalWorkshopHeroTacticViewState> Tactics
);
