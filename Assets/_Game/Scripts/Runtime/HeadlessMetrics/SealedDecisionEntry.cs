namespace SM.HeadlessMetrics;

/// <summary>한 decision seam을 식별하는 exact composite key.</summary>
public sealed record SealedDecisionSeamKey(
    int DecisionIndex,
    string SeamType,
    int Ordinal);

/// <summary>
/// LLM이 본 byte payload와 게임이 소비한 action/result 상태를 함께 봉인한 ordered trace entry.
/// 불법 action 또는 schema-invalid response는 fallback하지 않고 <see cref="TerminalFailure"/>로 끝낸다.
/// </summary>
public sealed record SealedDecisionEntry(
    SealedDecisionSeamKey SeamKey,
    string PreStateHash,
    byte[] ObservationCanonicalBytes,
    string ObservationHash,
    string LegalActionSetHash,
    string HistoryPrefixHash,
    byte[] RequestCanonicalBytes,
    string RequestHash,
    byte[] ResponseCanonicalBytes,
    string ResponseHash,
    string SelectedAction,
    string AppliedActionHash,
    string ResultEventHash,
    string PostStateHash,
    string PreviousEntryHash,
    bool TerminalFailure);
