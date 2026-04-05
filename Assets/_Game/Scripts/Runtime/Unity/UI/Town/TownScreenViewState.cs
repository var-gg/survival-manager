using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity.UI.Town;

public sealed record TownRecruitCardViewState(
    string Title,
    string Body,
    string ActionLabel,
    bool IsEnabled);

public sealed record TownDeployButtonViewState(
    DeploymentAnchorId Anchor,
    string Label);

public sealed record TownScreenViewState(
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string CurrencySummary,
    string RosterText,
    string RecruitSummaryText,
    IReadOnlyList<TownRecruitCardViewState> RecruitCards,
    string SquadText,
    string DeployPreviewText,
    IReadOnlyList<TownDeployButtonViewState> DeployButtons,
    string TeamPostureButtonLabel,
    string StatusText,
    string RerollLabel,
    string SaveLabel,
    string LoadLabel,
    string DebugStartLabel,
    string QuickBattleLabel);
