using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>
/// Sealed decision exchange port shared by capture, replay, and live sources.
/// A future <c>SealedTraceSource</c> can consume sealed response bytes and a live source can perform
/// transport without changing <see cref="SealedLlmBridgePolicy"/>; both may ignore the reference observation
/// carried only for <see cref="SyntheticStandInSource"/>.
/// </summary>
public interface ISealedDecisionSource
{
    LlmDecisionResponseV1 RequestDecision(SealedLlmDecisionRequest request);

    LlmRunReportResponseV1 RequestRunReport(SealedLlmRunReportRequest request);
}
