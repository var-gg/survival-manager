using System.Collections.Generic;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Battle;

public enum BattleUnitDetailTab
{
    Overview = 0,
    Skills = 1,
    Status = 2,
    Record = 3,
}

public sealed record BattleRosterUnitViewState(
    string UnitId,
    string DisplayName,
    string StatusText,
    float HealthNormalized,
    bool IsAlive,
    bool IsSelected,
    Texture2D? Portrait);

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
    BattleSettingsViewState Settings,
    IReadOnlyList<BattleRosterUnitViewState>? AllyRoster = null,
    IReadOnlyList<BattleRosterUnitViewState>? EnemyRoster = null,
    BattleSelectedUnitViewState? SelectedUnit = null);
