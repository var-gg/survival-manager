namespace SM.Unity.UI.Battle;

public sealed record BattleSettingsViewState(
    bool IsVisible,
    string OverheadLabel,
    string DamageTextLabel,
    string TeamSummaryLabel,
    string DebugOverlayLabel,
    string StatusText);

public sealed record BattleShellViewState(
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string AllyHpText,
    string EnemyHpText,
    string LogText,
    string ResultText,
    string SpeedText,
    string StatusText,
    string PauseLabel,
    string ContinueLabel,
    string RebattleLabel,
    bool CanRebattle,
    string ReturnTownLabel,
    bool CanReturnTownDirect,
    string SettingsLabel,
    float ProgressNormalized,
    bool ShowTeamSummary,
    bool CanContinue,
    BattleSettingsViewState Settings,
    BattleSelectedUnitViewState? SelectedUnit = null);
