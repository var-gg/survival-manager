using System.Collections.Generic;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Battle;

/// <summary>
/// 유닛 상세창 탭.
///
/// 2026-07-31 에 7 개에서 5 개로 줄였다. 뺀 것은 <c>Tactic</c> 과 <c>Record</c> 다.
/// Tactic 은 밀집도·폭·깊이·측면 성향 같은 <b>설계용 다이얼</b>이라 플레이어가 읽을 정보가 아니었고,
/// 게다가 같은 다이얼이 개요 탭에도 <b>중복</b>으로 떠 있었다.
/// Record 는 화면 하단 전투 기록 피드와 겹쳤다.
/// 탭이 일곱 개면 그건 전투 팝업이 아니라 설정 대화상자다.
/// </summary>
public enum BattleUnitDetailTab
{
    Overview = 0,
    Stats = 1,
    Skills = 2,
    Equipment = 3,
    Status = 4,
}

public sealed record BattleRosterUnitViewState(
    string UnitId,
    string DisplayName,
    string StatusText,
    float HealthNormalized,
    bool IsAlive,
    bool IsSelected,
    Texture2D? Portrait,
    string HealthText = "");

public sealed record BattleCombatantTokenViewState(
    string UnitId,
    string DisplayName,
    string ActionText,
    float HealthNormalized,
    bool IsAlly,
    bool IsActive,
    bool IsDown);

public sealed record BattleTacticalReadoutRowViewState(
    string Label,
    string Value,
    float NormalizedValue,
    string Tone);

public sealed record BattleDebugFoldoutViewState(
    string EncounterId,
    string SiteNodeIndexText,
    string BattleContextHash)
{
    public static readonly BattleDebugFoldoutViewState Empty = new("-", "-", "-");
}

public sealed record BattleSettingsViewState(
    bool IsVisible,
    string Title,
    string DisplaySectionTitle,
    string OverheadLabel,
    string OverheadTooltip,
    string DamageTextLabel,
    string DamageTextTooltip,
    string TeamSummaryLabel,
    string TeamSummaryTooltip,
    string DebugSectionTitle,
    string DebugOverlayLabel,
    string DebugOverlayTooltip,
    bool ShowDebugSection,
    string StatusText);

public sealed record BattleShellViewState(
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string HelpButtonLabel,
    HelpStripViewState Help,
    string SummaryTitle,
    string SummaryToggleLabel,
    string SummaryToggleTooltip,
    bool IsSummaryExpanded,
    string AllyTitle,
    string AllyHpText,
    string EnemyTitle,
    string EnemyHpText,
    string LogTitle,
    string LogText,
    string ResultText,
    string SpeedText,
    string StatusText,
    string PlaybackGroupTitle,
    string Speed05Label,
    string Speed1Label,
    string Speed2Label,
    string Speed4Label,
    string PauseLabel,
    string PauseTooltip,
    bool ShowPlaybackControls,
    bool CanChangeSpeed,
    bool CanPause,
    string ContinueGroupTitle,
    string ContinueLabel,
    bool ShowContinueAction,
    string ContinueTooltip,
    string ReplayLabel,
    string ReplayTooltip,
    bool CanReplay,
    string RebattleLabel,
    string RebattleTooltip,
    bool CanRebattle,
    string ReturnTownLabel,
    string ReturnTownTooltip,
    bool CanReturnTownDirect,
    string SmokeGroupTitle,
    bool ShowSmokeActions,
    string UtilityGroupTitle,
    string SettingsLabel,
    string SettingsTooltip,
    float ProgressNormalized,
    bool ShowProgressTrack,
    bool ShowTeamSummary,
    bool CanContinue,
    string BattleIntelTitle,
    string TacticalReadoutTitle,
    IReadOnlyList<BattleTacticalReadoutRowViewState>? TacticalReadoutRows,
    IReadOnlyList<BattleCombatantTokenViewState>? CombatantTokens,
    BattleSettingsViewState Settings,
    IReadOnlyList<BattleRosterUnitViewState>? AllyRoster = null,
    IReadOnlyList<BattleRosterUnitViewState>? EnemyRoster = null,
    BattleSelectedUnitViewState? SelectedUnit = null);
