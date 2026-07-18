using System;
using System.Collections.Generic;
using System.Linq;
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

        string selectedAction;
        IReadOnlyList<string> evidenceFactIds;
        switch (request.SeamKey.SeamType)
        {
            case SealedLlmSeamTypes.Deployment:
            {
                var decision = _referencePolicy.DecideDeployment(RequirePolicyObservation(request));
                selectedAction = SealedLlmActionCodec.EncodeDeployment(decision);
                evidenceFactIds = decision.EvidenceFactIds;
                break;
            }
            case SealedLlmSeamTypes.Reward:
            {
                var decision = _referencePolicy.DecideReward(RequirePolicyObservation(request));
                selectedAction = SealedLlmActionCodec.EncodeReward(decision);
                evidenceFactIds = decision.EvidenceFactIds;
                break;
            }
            case SealedLlmSeamTypes.Recruit:
            {
                var decision = _referenceRosterPolicy.DecideRecruit(RequireRosterObservation(request));
                selectedAction = SealedLlmActionCodec.EncodeRecruit(decision);
                evidenceFactIds = decision.EvidenceFactIds;
                break;
            }
            case SealedLlmSeamTypes.Passive:
            {
                var decision = _referenceRosterPolicy.DecidePassiveAllocation(RequireRosterObservation(request));
                selectedAction = SealedLlmActionCodec.EncodePassive(decision);
                evidenceFactIds = decision.EvidenceFactIds;
                break;
            }
            case SealedLlmSeamTypes.Refit:
            {
                var decision = _referenceRosterPolicy.DecideRefit(RequireRosterObservation(request));
                selectedAction = SealedLlmActionCodec.EncodeRefit(decision);
                evidenceFactIds = decision.EvidenceFactIds;
                break;
            }
            default:
                throw new InvalidOperationException(
                    $"Synthetic stand-in does not recognize seam '{request.SeamKey.SeamType}'.");
        }

        var declaredIntent = new LlmDeclaredIntentV1(
            FixedDeclaredIntent.IntentId,
            FixedDeclaredIntent.TrackTokenIds,
            FixedDeclaredIntent.ExpectedPayoff,
            (evidenceFactIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            FixedDeclaredIntent.NextAcquisitionPlan,
            FixedDeclaredIntent.AllowedSubstitutions,
            FixedDeclaredIntent.PivotConditions,
            FixedDeclaredIntent.Confidence);

        return new LlmDecisionResponseV1(
            selectedAction,
            declaredIntent,
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
