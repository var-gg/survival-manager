using System;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>
/// Deterministic capture stand-in that mirrors an injected scripted policy through the exact wire response shape.
/// It performs no inference: the concrete reference choice is encoded as selected_action and all other wire fields
/// are fixed constants, making the paired witness transparent at the game-effect boundary.
/// </summary>
public sealed class SyntheticStandInSource : ISealedDecisionSource
{
    public const string IntentReference = "synthetic-stand-in:paired-reference";

    private static readonly LlmDeclaredIntentV1 FixedDeclaredIntent = new(
        "synthetic-stand-in",
        Array.Empty<string>(),
        "mirror the injected scripted reference policy",
        Array.Empty<string>(),
        "follow the next scripted reference decision",
        Array.Empty<string>(),
        Array.Empty<string>(),
        1d);

    private static readonly LlmRunReportResponseV1 FixedRunReport = new(
        "synthetic stand-in has no inferred desire",
        "paired scripted-policy witness only",
        "repeat the injected scripted reference",
        Array.Empty<string>(),
        Array.Empty<LlmEvaluationSentenceV1>(),
        "none");

    private readonly IHeadlessPolicy _referencePolicy;
    private readonly IHeadlessRosterPolicy _referenceRosterPolicy;

    public SyntheticStandInSource(
        IHeadlessPolicy referencePolicy,
        IHeadlessRosterPolicy referenceRosterPolicy)
    {
        _referencePolicy = referencePolicy ?? throw new ArgumentNullException(nameof(referencePolicy));
        _referenceRosterPolicy = referenceRosterPolicy
                                 ?? throw new ArgumentNullException(nameof(referenceRosterPolicy));
    }

    public LlmDecisionResponseV1 RequestDecision(SealedLlmDecisionRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var selectedAction = request.SeamKey.SeamType switch
        {
            SealedLlmSeamTypes.Deployment => SealedLlmActionCodec.EncodeDeployment(
                _referencePolicy.DecideDeployment(RequirePolicyObservation(request))),
            SealedLlmSeamTypes.Reward => SealedLlmActionCodec.EncodeReward(
                _referencePolicy.DecideReward(RequirePolicyObservation(request))),
            SealedLlmSeamTypes.Recruit => SealedLlmActionCodec.EncodeRecruit(
                _referenceRosterPolicy.DecideRecruit(RequireRosterObservation(request))),
            SealedLlmSeamTypes.Passive => SealedLlmActionCodec.EncodePassive(
                _referenceRosterPolicy.DecidePassiveAllocation(RequireRosterObservation(request))),
            SealedLlmSeamTypes.Refit => SealedLlmActionCodec.EncodeRefit(
                _referenceRosterPolicy.DecideRefit(RequireRosterObservation(request))),
            _ => throw new InvalidOperationException(
                $"Synthetic stand-in does not recognize seam '{request.SeamKey.SeamType}'."),
        };

        return new LlmDecisionResponseV1(
            selectedAction,
            FixedDeclaredIntent,
            IntentReference,
            Array.Empty<LlmBuildHypothesisV1>());
    }

    public LlmRunReportResponseV1 RequestRunReport(SealedLlmRunReportRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        return FixedRunReport;
    }

    private static HeadlessPolicyObservation RequirePolicyObservation(SealedLlmDecisionRequest request)
        => request.PolicyObservation
           ?? throw new InvalidOperationException(
               $"Seam '{request.SeamKey.SeamType}' requires a policy observation.");

    private static HeadlessRosterPolicyObservation RequireRosterObservation(SealedLlmDecisionRequest request)
        => request.RosterObservation
           ?? throw new InvalidOperationException(
               $"Seam '{request.SeamKey.SeamType}' requires a roster observation.");
}
