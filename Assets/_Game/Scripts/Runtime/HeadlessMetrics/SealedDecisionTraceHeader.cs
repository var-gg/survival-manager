namespace SM.HeadlessMetrics;

/// <summary>
/// Sealed trace의 capture 출처. sibling assembly의
/// SM.HeadlessCensus.BuildGrammarInferenceCaptureSource와 의도적으로 평행한 metrics-local enum이다.
/// </summary>
public enum SealedDecisionTraceCaptureSource
{
    SyntheticStandIn = 0,
    LiveColdStartLlm = 1,
}

/// <summary>한 sealed decision run을 build/content/prompt/scorer configuration에 결속하는 header.</summary>
public sealed record SealedDecisionTraceHeader(
    string TraceSchemaVersion,
    string RunId,
    string ScenarioId,
    int CampaignSeed,
    string BuildManifestHash,
    string ContentManifestHash,
    string VisibleSurfaceHash,
    string PromptSchemaHash,
    string ScorerConfigHash,
    SealedDecisionTraceCaptureSource CaptureSource)
{
    /// <summary>Synthetic stand-in trace는 결과와 무관하게 BT 인증 집계에 들어갈 수 없다.</summary>
    public bool CertificationEligible
        => CaptureSource == SealedDecisionTraceCaptureSource.LiveColdStartLlm;
}
