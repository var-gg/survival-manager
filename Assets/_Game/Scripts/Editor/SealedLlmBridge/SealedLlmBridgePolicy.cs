using System;
using System.Collections.Generic;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>
/// Pure policy adapter that canonicalizes each player-visible decision, obtains one strict wire response, and leaves
/// game-state completion to the caller through <see cref="SealedDecisionTraceBuilder.Complete"/>.
/// </summary>
public sealed class SealedLlmBridgePolicy : IHeadlessPolicy, IHeadlessRosterPolicy, IHeadlessPrepPolicy
{
    public const string PolicyId = "sealed-llm-bridge-v1";

    private readonly ISealedDecisionSource _source;
    private readonly SealedDecisionTraceBuilder _builder;
    private readonly LlmPromptManifestV1 _manifest;

    public SealedLlmBridgePolicy(
        ISealedDecisionSource source,
        SealedDecisionTraceBuilder builder,
        LlmPromptManifestV1 manifest)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        if (!string.Equals(_builder.PromptSchemaHash, _manifest.PromptSchemaHash, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Trace header PromptSchemaHash must match the bridge prompt manifest.",
                nameof(builder));
        }
    }

    public string Id => PolicyId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
        => DecidePolicy(
            SealedLlmSeamTypes.Deployment,
            observation,
            SealedLlmLegalActionSet.DeploymentLegalActionSetHash,
            SealedLlmActionCodec.DecodeDeployment,
            HeadlessPolicyGuard.ValidateDeploymentDecision);

    public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
        => DecidePolicy(
            SealedLlmSeamTypes.Reward,
            observation,
            SealedLlmLegalActionSet.RewardLegalActionSetHash,
            SealedLlmActionCodec.DecodeReward,
            HeadlessPolicyGuard.ValidateRewardDecision);

    public HeadlessPrepDecision DecidePrep(HeadlessPolicyObservation observation)
        => DecidePolicy(
            SealedLlmSeamTypes.Prep,
            observation,
            SealedLlmLegalActionSet.PrepLegalActionSetHash,
            SealedLlmActionCodec.DecodePrep,
            HeadlessPrepPolicyGuard.ValidateDecision);

    public HeadlessRecruitDecision DecideRecruit(HeadlessRosterPolicyObservation observation)
        => DecideRoster(
            SealedLlmSeamTypes.Recruit,
            observation,
            SealedLlmLegalActionSet.RecruitLegalActionSetHash,
            SealedLlmActionCodec.DecodeRecruit,
            HeadlessRosterPolicyGuard.ValidateRecruitDecision);

    public HeadlessPassiveDecision DecidePassiveAllocation(HeadlessRosterPolicyObservation observation)
        => DecideRoster(
            SealedLlmSeamTypes.Passive,
            observation,
            SealedLlmLegalActionSet.PassiveLegalActionSetHash,
            SealedLlmActionCodec.DecodePassive,
            HeadlessRosterPolicyGuard.ValidatePassiveDecision);

    public HeadlessRefitDecision DecideRefit(HeadlessRosterPolicyObservation observation)
        => DecideRoster(
            SealedLlmSeamTypes.Refit,
            observation,
            SealedLlmLegalActionSet.RefitLegalActionSetHash,
            SealedLlmActionCodec.DecodeRefit,
            HeadlessRosterPolicyGuard.ValidateRefitDecision);

    /// <summary>
    /// Requests and seals the terminal report. The caller supplies the canonical report-request bytes and the final
    /// game-state hash; no live transport or report-request codec is introduced by the capture core.
    /// </summary>
    public LlmRunReportResponseV1 SealRunReport(
        byte[] requestCanonicalBytes,
        string finalStateHash,
        string statusToken = "completed")
    {
        if (requestCanonicalBytes == null) throw new ArgumentNullException(nameof(requestCanonicalBytes));
        var request = new SealedLlmRunReportRequest(
            _builder.NextRunReportSeamKey(),
            requestCanonicalBytes,
            statusToken);
        var response = _source.RequestRunReport(request)
                       ?? throw new InvalidOperationException("Decision source returned a null run report.");
        var responseBytes = LlmWireCanonicalSerializer.CanonicalBytes(response);
        _builder.SealRunReport(request, responseBytes, finalStateHash);
        return response;
    }

    private TDecision DecidePolicy<TDecision>(
        string seamType,
        HeadlessPolicyObservation observation,
        Func<HeadlessPolicyObservation, string> legalActionSetHash,
        Func<HeadlessPolicyObservation, string, TDecision> decode,
        Action<HeadlessPolicyObservation, TDecision> validate)
    {
        if (observation == null) throw new ArgumentNullException(nameof(observation));
        var observationBytes = SealedLlmObservationCodec.CanonicalBytes(observation);
        var historyPrefixHash = _builder.CurrentHistoryPrefixHash;
        var requestBytes = SealedLlmRequestCodec.RequestCanonicalBytes(
            _manifest,
            observationBytes,
            historyPrefixHash);
        var seamKey = _builder.Begin(
            seamType,
            observationBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(observationBytes),
            legalActionSetHash(observation),
            historyPrefixHash,
            requestBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(requestBytes));
        var request = SealedLlmDecisionRequest.ForPolicy(seamKey, requestBytes, observation);
        return FinishDecision(request, observation, decode, validate);
    }

    private TDecision DecideRoster<TDecision>(
        string seamType,
        HeadlessRosterPolicyObservation observation,
        Func<HeadlessRosterPolicyObservation, string> legalActionSetHash,
        Func<HeadlessRosterPolicyObservation, string, TDecision> decode,
        Action<HeadlessRosterPolicyObservation, TDecision> validate)
    {
        if (observation == null) throw new ArgumentNullException(nameof(observation));
        var observationBytes = SealedLlmObservationCodec.CanonicalBytes(observation);
        var historyPrefixHash = _builder.CurrentHistoryPrefixHash;
        var requestBytes = SealedLlmRequestCodec.RequestCanonicalBytes(
            _manifest,
            observationBytes,
            historyPrefixHash);
        var seamKey = _builder.Begin(
            seamType,
            observationBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(observationBytes),
            legalActionSetHash(observation),
            historyPrefixHash,
            requestBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(requestBytes));
        var request = SealedLlmDecisionRequest.ForRoster(seamKey, requestBytes, observation);
        return FinishDecision(request, observation, decode, validate);
    }

    private TDecision FinishDecision<TObservation, TDecision>(
        SealedLlmDecisionRequest request,
        TObservation observation,
        Func<TObservation, string, TDecision> decode,
        Action<TObservation, TDecision> validate)
    {
        var response = _source.RequestDecision(request)
                       ?? throw new InvalidOperationException("Decision source returned a null response.");
        var responseBytes = LlmWireCanonicalSerializer.CanonicalBytes(response);
        _builder.RecordResponse(
            request.SeamKey,
            responseBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(responseBytes),
            response.SelectedAction);

        try
        {
            var decision = AttachDeclaredEvidence(
                decode(observation, response.SelectedAction),
                response.DeclaredIntent?.EvidenceFactIds);
            validate(observation, decision);
            return decision;
        }
        catch (SealedLlmActionDecodeException exception)
        {
            _builder.MarkTerminalFailure(request.SeamKey);
            throw new SealedLlmTerminalFailureException(request.SeamKey, exception);
        }
        catch (InvalidOperationException exception)
        {
            _builder.MarkTerminalFailure(request.SeamKey);
            throw new SealedLlmTerminalFailureException(request.SeamKey, exception);
        }
    }

    private static TDecision AttachDeclaredEvidence<TDecision>(
        TDecision decision,
        IReadOnlyList<string> evidenceFactIds)
    {
        var evidence = evidenceFactIds ?? Array.Empty<string>();
        object result = decision switch
        {
            HeadlessDeploymentDecision value => new HeadlessDeploymentDecision(
                value.Placements,
                value.Rationale,
                value.EstimatedValue,
                evidence),
            HeadlessPrepDecision value => new HeadlessPrepDecision(
                value.Placements,
                value.EquipmentAssignments,
                value.Rationale,
                value.EstimatedValue,
                evidence),
            HeadlessRewardDecision value => new HeadlessRewardDecision(
                value.OptionIndex,
                value.Rationale,
                value.EstimatedValue,
                evidence),
            HeadlessRecruitDecision value => new HeadlessRecruitDecision(
                value.OfferIndex,
                value.Rationale,
                value.EstimatedValue,
                evidence),
            HeadlessPassiveDecision value => new HeadlessPassiveDecision(
                value.HeroId,
                value.BoardId,
                value.NodeId,
                value.Rationale,
                value.EstimatedValue,
                evidence),
            HeadlessRefitDecision value => new HeadlessRefitDecision(
                value.ItemInstanceId,
                value.AffixSlotIndex,
                value.Rationale,
                value.EstimatedValue,
                evidence),
            _ => throw new InvalidOperationException(
                $"Unsupported sealed LLM decision type '{typeof(TDecision).FullName}'."),
        };
        return (TDecision)result;
    }
}
