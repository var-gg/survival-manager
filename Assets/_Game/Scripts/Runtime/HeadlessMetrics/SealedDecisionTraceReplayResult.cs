namespace SM.HeadlessMetrics;

/// <summary>Strict sealed/replayed trace 비교의 첫 실패 분류.</summary>
public enum SealedDecisionTraceReplayDivergenceReason
{
    None = 0,
    SealedTraceMissing,
    ReplayedTraceMissing,
    HeaderMissing,
    MissingHeaderField,
    InvalidTraceSchemaVersion,
    InvalidCaptureSource,
    HeaderMismatch,
    MissingEntryData,
    DuplicateSeamKey,
    MissingEntry,
    ExtraEntry,
    OutOfOrderEntry,
    SeamKeyMismatch,
    PreStateHashMismatch,
    ObservationCanonicalBytesMismatch,
    ObservationHashMismatch,
    LegalActionSetHashMismatch,
    HistoryPrefixHashMismatch,
    RequestCanonicalBytesMismatch,
    RequestHashMismatch,
    ResponseCanonicalBytesMismatch,
    ResponseHashMismatch,
    SelectedActionMismatch,
    AppliedActionHashMismatch,
    ResultEventHashMismatch,
    PostStateHashMismatch,
    PreviousEntryHashMismatch,
    BrokenPreviousEntryHashChain,
    TerminalFailureMismatch,
    TraceManifestHashMismatch,
}

/// <summary>
/// sealed_llm_decision_trace_replay_match_rate와 strict failure 상태를 함께 보존한다.
/// rate가 1.0이어도 structural failure가 있으면 VerificationPassed는 false다.
/// </summary>
public sealed record SealedDecisionTraceReplayResult(
    bool VerificationPassed,
    double SealedLlmDecisionTraceReplayMatchRate,
    int SealedEntryCount,
    int ReplayedEntryCount,
    int ComparedEntryCount,
    int MatchedEntryCount,
    int UnmatchedEntryCount,
    int FirstDivergenceIndex,
    SealedDecisionTraceReplayDivergenceReason FirstDivergenceReason,
    string FirstDivergenceDetail);
