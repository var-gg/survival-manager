using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity.UI.Town;

/// <summary>
/// 전술 설정(SquadBuilder) 순수 ViewState — presenter BuildState()가 세션 truth에서 계산한
/// 표시용 read model. VisualElement/Texture 참조 없음 (포트레잇은 CharacterId만 싣고 View가 resolve).
/// EquipmentRefitViewState와 같은 패턴: 헤드리스 테스트가 이 record만 단언하면 화면 로직이 검증된다.
/// </summary>
public sealed record SquadBuilderHeroRowViewState(
    string HeroId,
    string DisplayName,
    string MetaLabel,        // "{class} / {race} · Lv N · XP n%"
    string LoadoutLabel,     // "장비 n · 스킬 n · 패시브 n"
    string DeploymentLabel,  // anchor 한국어 라벨 / "원정 후보" / "대기"
    string RoleLabel,
    string FormationLabel,
    string RangeLabel,
    string BiasLabel,
    string ClassKey,         // vanguard/duelist/ranger/mystic/unknown — USS class icon variant
    string RarityLabel,
    bool IsDeployed,
    string CharacterId,      // View가 포트레잇 resolve에 쓰는 키 (비면 글리프 fallback)
    int PipCount,            // 배치=5, 원정 후보=4, 대기=3
    string Glyph);           // 이름 첫 글자 (이름 없으면 ◆/◇)

/// <summary>formation board anchor 슬롯 6개 — HeroRow가 null이면 빈 슬롯.</summary>
public sealed record SquadBuilderAnchorSlotViewState(
    DeploymentAnchorId Anchor,
    string ShortLabel,       // "전 상"/"전 중"/... anchor badge용 축약 라벨
    SquadBuilderHeroRowViewState? HeroRow,
    bool IsSelected);

public sealed record SquadBuilderPostureViewState(
    TeamPostureType Posture,
    bool IsSelected);

/// <summary>선택 anchor 디테일 — 빈 슬롯이면 안내 문구 + "empty"/posture 태그.</summary>
public sealed record SquadBuilderSelectedDetailViewState(
    string SelectedAnchorLabel,
    string Name,
    string Meta,
    string Loadout,
    IReadOnlyList<string> Tags);

public sealed record SquadBuilderOperationRowViewState(
    string Key,
    string Value);

/// <summary>P1 유닛별 타겟 지시 — 선택 anchor에 영웅이 있을 때만 존재. cycle은 View 버튼 → 액션.</summary>
public sealed record SquadBuilderTargetDirectiveViewState(
    string HeroId,
    string DirectiveLabel);

/// <summary>활성/진행 중 시너지 칩 — IsActive=false는 muted 표시. 0건 안내는 SynergyEmptyText가 대신한다.</summary>
public sealed record SquadBuilderSynergyChipViewState(
    string Text,             // "{시너지명} {n}/{bound}"
    bool IsActive);

public sealed record SquadBuilderViewState(
    bool IsOpen,
    IReadOnlyList<SquadBuilderAnchorSlotViewState> AnchorSlots,
    IReadOnlyList<SquadBuilderPostureViewState> Postures,
    IReadOnlyList<SquadBuilderHeroRowViewState> RosterRows,   // 정렬: 배치 > 원정 > 이름 Ordinal
    int RosterCount,
    SquadBuilderSelectedDetailViewState SelectedDetail,
    IReadOnlyList<SquadBuilderOperationRowViewState> OperationRows,
    SquadBuilderTargetDirectiveViewState? TargetDirective,
    IReadOnlyList<SquadBuilderSynergyChipViewState> SynergyChips,
    string SynergyEmptyText,          // 칩 0건일 때 안내 칩 문구 (칩 있으면 empty)
    string ResponseSummary,           // posture 기준 + 대응 강함/취약 + disclaimer
    string DeploymentChipLabel,       // "배치 X/6"
    string PostureChipLabel,          // "태세 {posture}"
    string RiskChipLabel,             // "위험 점수 N" / 배치 0이면 "위험 점수 —"
    string StatusText);               // "{상태문구} · 현재 팀 태세: {posture}"
