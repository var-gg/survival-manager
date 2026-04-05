using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity.UI.Expedition;

public sealed record ExpeditionNodeCardViewState(
    string Title,
    string RewardSummary,
    string ActionLabel,
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
    string PositionText,
    string MapText,
    string RewardText,
    string SquadText,
    IReadOnlyList<ExpeditionNodeCardViewState> NodeCards,
    IReadOnlyList<ExpeditionDeployButtonViewState> DeployButtons,
    string TeamPostureButtonLabel,
    string StatusText,
    string NextBattleLabel,
    string ReturnTownLabel);
