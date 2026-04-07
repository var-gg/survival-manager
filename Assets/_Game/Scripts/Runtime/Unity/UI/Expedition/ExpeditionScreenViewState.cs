using System.Collections.Generic;
using SM.Combat.Model;
using SM.Unity.UI;

namespace SM.Unity.UI.Expedition;

public sealed record ExpeditionNodeCardViewState(
    string Title,
    string RewardSummary,
    string ActionLabel,
    string Tooltip,
    bool IsVisible,
    bool IsSelectable,
    bool IsSelected,
    bool IsCurrent,
    bool IsCompleted);

public sealed record ExpeditionDeployButtonViewState(
    DeploymentAnchorId Anchor,
    string Label);

public sealed record ExpeditionScreenViewState(
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string HelpButtonLabel,
    HelpStripViewState Help,
    string MapTitle,
    string PositionText,
    string MapText,
    string SelectedRouteTitle,
    string RewardText,
    string SquadTitle,
    string SquadText,
    string RoutesTitle,
    IReadOnlyList<ExpeditionNodeCardViewState> NodeCards,
    IReadOnlyList<ExpeditionDeployButtonViewState> DeployButtons,
    string TeamPostureButtonLabel,
    string PrimaryActionsTitle,
    string WarningActionsTitle,
    string StatusText,
    string NextBattleLabel,
    string NextBattleTooltip,
    string ReturnTownLabel,
    string ReturnTownTooltip);
