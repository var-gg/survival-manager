using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Tactical Workshop V1 surface ViewState — profile binding workflow의 Sprint 1.
///
/// pindoc V1 wiki SoT:
/// - 6 anchor (deployment-and-anchors.md): Front × 3 + Back × 3, 4 deploy.
/// - 5 posture (wiki-combat-posture-tactic-v1): HoldLine / StandardAdvance / ProtectCarry / CollapseWeakSide / AllInBackline.
/// - 7 synergy family (wiki-combat-synergy-v1): race 3 + class 4.
/// - 8 threat lane (wiki-combat-counter-topology-v1): ArmorFrontline 등.
/// - per-unit tactic은 V1 read-only RuleSet (N rules per hero).
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

public sealed record TacticalWorkshopRuleViewState(
    int Priority,
    string ConditionId,
    string ActionId,
    string TargetId
);

public sealed record TacticalWorkshopHeroTacticViewState(
    string HeroId,
    string DisplayName,
    string PostureLabel,
    IReadOnlyList<TacticalWorkshopRuleViewState> Rules
);

public sealed record TacticalWorkshopViewState(
    IReadOnlyList<TacticalWorkshopAnchorViewState> Anchors,
    IReadOnlyList<TacticalWorkshopPostureViewState> Postures,
    string SelectedPostureId,
    IReadOnlyList<TacticalWorkshopSynergyChipViewState> SynergyChips,
    IReadOnlyList<TacticalWorkshopThreatViewState> Threats,
    IReadOnlyList<TacticalWorkshopHeroTacticViewState> Tactics
);
