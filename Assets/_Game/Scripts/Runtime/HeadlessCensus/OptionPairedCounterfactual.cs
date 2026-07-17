namespace SM.HeadlessCensus;

/// <summary>같은 seed·build·placement·enemy에서 옵션 하나만 바꾼 pair.</summary>
public sealed record OptionPairedCounterfactual(
    string OptionId,
    string ComparatorId,
    string ContextId,
    int Seed,
    string PlacementId,
    bool IntendedContext,
    bool FullCensus,
    bool ExplicitTradeoffVisible,
    OptionOutcomeVector OptionOutcome,
    OptionOutcomeVector ComparatorOutcome,
    string OptionReplayHash,
    string ComparatorReplayHash);
