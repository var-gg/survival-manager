using System.Collections.Generic;

namespace SM.Unity.UI.Reward;

public sealed record RewardChoiceCardViewState(
    string Title,
    string Body,
    string KindText,
    string ContextText,
    string ActionLabel,
    bool IsEnabled);

public sealed record RewardScreenViewState(
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string RunDeltaText,
    string BuildContextText,
    string ChoicesHeaderText,
    IReadOnlyList<RewardChoiceCardViewState> Choices,
    string StatusText,
    string ReturnTownLabel);
