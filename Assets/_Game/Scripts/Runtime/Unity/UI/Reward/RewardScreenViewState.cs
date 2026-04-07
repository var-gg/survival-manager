using System.Collections.Generic;
using SM.Unity.UI;

namespace SM.Unity.UI.Reward;

public sealed record RewardChoiceCardViewState(
    string Title,
    string Body,
    string KindText,
    string ContextText,
    string ActionLabel,
    string Tooltip,
    bool IsEnabled);

public sealed record RewardScreenViewState(
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string HelpButtonLabel,
    HelpStripViewState Help,
    string SummaryTitle,
    string RunDeltaText,
    string BuildContextTitle,
    string BuildContextText,
    string ChoicesHeaderText,
    IReadOnlyList<RewardChoiceCardViewState> Choices,
    string StatusText,
    string ReturnTownLabel,
    string ReturnTownTooltip,
    bool CanReturnToTown,
    bool ReturnTownIsPrimary);
