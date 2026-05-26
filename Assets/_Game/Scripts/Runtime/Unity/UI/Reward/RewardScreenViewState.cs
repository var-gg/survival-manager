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
    bool HasAnyModifier,
    string ThreatBandLabelText = "")
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
        HasAnyModifier: false,
        ThreatBandLabelText: string.Empty);
}

public sealed record RewardProgressionRowViewState(
    string KeyText,
    string ValueText,
    string ToneKey);

// wave-28-survivor GPT Pro patch: squad 4명 survivor row (portrait glyph + HP/MaxHP + Level/Exp).
public sealed record RewardSurvivorRowViewState(
    string HeroId,
    string DisplayName,
    string PortraitGlyph,        // ◆ ⚔ ♟ etc — class별 또는 generic
    string HpText,                // "62 / 80"
    float HpPercent,              // 0.0~1.0
    string ExpText,               // "Lv 4 · 320 / 500"
    string StatusChipText,        // "생존" / "기절" / "EXP +120" (PVE 결과)
    string StatusChipKind);       // "victory" / "downed" / "exp-gain"

public sealed record RewardTimelineTickViewState(
    string StepText,
    string DetailText,
    bool IsComplete,
    bool IsCurrent);

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
    string ProgressionTitle,
    string TimelineTitle,
    string ChoicesHeaderText,
    IReadOnlyList<RewardChoiceCardViewState> Choices,
    string StatusText,
    string ReturnTownLabel,
    string ReturnTownTooltip,
    bool CanReturnToTown,
    bool ReturnTownIsPrimary,
    RewardSettlementSummaryViewState SettlementSummary,
    IReadOnlyList<RewardProgressionRowViewState> ProgressionRows,
    IReadOnlyList<RewardTimelineTickViewState> TimelineTicks,
    // wave-28-survivor GPT Pro patch: squad 4명 survivor list (portrait + HP/EXP).
    // default empty이면 View가 survivor section을 hide.
    IReadOnlyList<RewardSurvivorRowViewState>? SurvivorRows = null);
