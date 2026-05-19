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

public sealed record RewardSettlementSummaryViewState(
    string TitleText,
    string SiteKeyText,
    string SiteValueText,
    string StageKeyText,
    string StageValueText,
    string EncounterKeyText,
    string EncounterValueText,
    string CommitIdKeyText,
    string CommitIdValueText,
    string RewardBiasChipText,
    string ThreatPressureChipText,
    string AffinityBoostChipText,
    bool HasAnyModifier)
{
    public static readonly RewardSettlementSummaryViewState Empty = new(
        TitleText: "Settlement",
        SiteKeyText: "Site",
        SiteValueText: "-",
        StageKeyText: "Stage",
        StageValueText: "-",
        EncounterKeyText: "Encounter",
        EncounterValueText: "-",
        CommitIdKeyText: "Commit",
        CommitIdValueText: "-",
        RewardBiasChipText: string.Empty,
        ThreatPressureChipText: string.Empty,
        AffinityBoostChipText: string.Empty,
        HasAnyModifier: false);
}

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
    bool ReturnTownIsPrimary,
    RewardSettlementSummaryViewState SettlementSummary);
