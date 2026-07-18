using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>Fail-closed file exchange source. It never substitutes a scripted or fallback answer.</summary>
public sealed class LiveColdStartFileHandshakeSource : ISealedDecisionSource
{
    public const int MaximumAttemptLimit = 3;

    private readonly string _exchangeDirectory;
    private readonly LlmPromptManifestV1 _manifest;
    private readonly HashSet<string> _allowedAgentKinds;
    private readonly TimeSpan _decisionTimeout;
    private readonly TimeSpan _runReportTimeout;
    private readonly TimeSpan _pollInterval;
    private readonly int _attemptLimit;

    public LiveColdStartFileHandshakeSource(
        string exchangeDirectory,
        LlmPromptManifestV1 manifest,
        IReadOnlyCollection<string> allowedAgentKinds,
        TimeSpan decisionTimeout,
        TimeSpan runReportTimeout,
        TimeSpan pollInterval,
        int attemptLimit = MaximumAttemptLimit)
    {
        if (string.IsNullOrWhiteSpace(exchangeDirectory))
        {
            throw new ArgumentException("Exchange directory is required.", nameof(exchangeDirectory));
        }

        _exchangeDirectory = Path.GetFullPath(exchangeDirectory);
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _ = LlmWireCanonicalSerializer.CanonicalBytes(_manifest);
        if (allowedAgentKinds == null || allowedAgentKinds.Count == 0
            || allowedAgentKinds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-blank agent kind is required.", nameof(allowedAgentKinds));
        }

        _allowedAgentKinds = new HashSet<string>(allowedAgentKinds, StringComparer.Ordinal);
        _decisionTimeout = RequirePositive(decisionTimeout, nameof(decisionTimeout));
        _runReportTimeout = RequirePositive(runReportTimeout, nameof(runReportTimeout));
        _pollInterval = RequirePositive(pollInterval, nameof(pollInterval));
        if (attemptLimit is < 1 or > MaximumAttemptLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptLimit), "Attempt limit must be in [1,3].");
        }

        _attemptLimit = attemptLimit;
    }

    public LlmDecisionResponseV1 RequestDecision(SealedLlmDecisionRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var legalKeys = request.PolicyObservation != null
            ? SealedLlmPromptRenderer.LegalActionKeys(request.SeamKey, request.PolicyObservation)
            : SealedLlmPromptRenderer.LegalActionKeys(request.SeamKey, request.RosterObservation);
        var deploymentActionSpace = string.Equals(
            request.SeamKey.SeamType,
            SealedLlmSeamTypes.Deployment,
            StringComparison.Ordinal)
            ? SealedLlmPromptRenderer.DeploymentActionSpace(request.PolicyObservation)
            : null;
        var prompt = request.PolicyObservation != null
            ? SealedLlmPromptRenderer.Render(request.SeamKey, request.PolicyObservation, legalKeys, _manifest)
            : SealedLlmPromptRenderer.Render(request.SeamKey, request.RosterObservation, legalKeys, _manifest);
        var requestHash = SealedDecisionTraceHash.ComputeCanonicalPayloadHash(request.RequestCanonicalBytes);
        var prefix = Prefix("d", request.SeamKey.DecisionIndex);
        WriteRequest(prefix, request.SeamKey, requestHash, legalKeys, deploymentActionSpace, prompt);

        for (var attempt = 1; attempt <= _attemptLimit; attempt++)
        {
            var envelope = ReadResponse(prefix, attempt, request.SeamKey, requestHash, _decisionTimeout);
            LlmDecisionResponseV1 response;
            try
            {
                response = LlmWireCanonicalSerializer.ParseDecisionResponse(envelope.RawResponseJson);
            }
            catch (Exception exception)
            {
                if (attempt == _attemptLimit)
                {
                    throw new InvalidOperationException(
                        $"Live decision remained unparseable after {_attemptLimit.ToString(CultureInfo.InvariantCulture)} attempts; no response was sealed.",
                        exception);
                }

                WriteReject(prefix, attempt, request.SeamKey, requestHash,
                    SealedLlmExchangeEnvelope.StrictParseReason, exception.Message);
                continue;
            }

            try
            {
                PrevalidateAction(request, response.SelectedAction);
                return response;
            }
            catch (SealedLlmActionDecodeException exception)
            {
                if (attempt == _attemptLimit)
                {
                    return response;
                }

                WriteReject(prefix, attempt, request.SeamKey, requestHash,
                    SealedLlmExchangeEnvelope.ActionDecodeReason, exception.Message);
            }
        }

        throw new InvalidOperationException("Decision attempt loop exhausted unexpectedly.");
    }

    public LlmRunReportResponseV1 RequestRunReport(SealedLlmRunReportRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var prompt = SealedLlmPromptRenderer.RenderRunReport(
            request.SeamKey,
            request.StatusToken,
            _manifest);
        var requestHash = SealedDecisionTraceHash.ComputeCanonicalPayloadHash(request.RequestCanonicalBytes);
        var prefix = Prefix("r", request.SeamKey.DecisionIndex);
        WriteRequest(
            prefix,
            request.SeamKey,
            requestHash,
            Array.Empty<string>(),
            null,
            prompt);

        for (var attempt = 1; attempt <= _attemptLimit; attempt++)
        {
            var envelope = ReadResponse(prefix, attempt, request.SeamKey, requestHash, _runReportTimeout);
            try
            {
                return LlmWireCanonicalSerializer.ParseRunReportResponse(envelope.RawResponseJson);
            }
            catch (Exception exception)
            {
                if (attempt == _attemptLimit)
                {
                    throw new InvalidOperationException(
                        $"Live run report remained unparseable after {_attemptLimit.ToString(CultureInfo.InvariantCulture)} attempts; no report was sealed.",
                        exception);
                }

                WriteReject(prefix, attempt, request.SeamKey, requestHash,
                    SealedLlmExchangeEnvelope.StrictParseReason, exception.Message);
            }
        }

        throw new InvalidOperationException("Run-report attempt loop exhausted unexpectedly.");
    }

    private void WriteRequest(
        string prefix,
        SealedDecisionSeamKey seamKey,
        string requestHash,
        IReadOnlyList<string> legalKeys,
        SealedLlmDeploymentActionSpaceV1 deploymentActionSpace,
        string prompt)
    {
        Directory.CreateDirectory(_exchangeDirectory);
        var promptFile = prefix + ".prompt.md";
        SealedLlmExchangeEnvelope.WritePrompt(Path.Combine(_exchangeDirectory, promptFile), prompt);
        SealedLlmExchangeEnvelope.WriteRequest(
            Path.Combine(_exchangeDirectory, prefix + ".request.json"),
            new SealedLlmExchangeRequestV1(
                SealedLlmExchangeRequestV1.CurrentSchemaVersion,
                seamKey,
                requestHash,
                legalKeys,
                deploymentActionSpace,
                promptFile,
                _attemptLimit));
    }

    private SealedLlmExchangeResponseV1 ReadResponse(
        string prefix,
        int attempt,
        SealedDecisionSeamKey seamKey,
        string requestHash,
        TimeSpan timeout)
    {
        var path = Path.Combine(
            _exchangeDirectory,
            $"{prefix}.a{attempt.ToString(CultureInfo.InvariantCulture)}.response.json");
        WaitForFinalFile(path, timeout);
        var envelope = SealedLlmExchangeEnvelope.ReadResponse(path);
        if (!Equals(envelope.SeamKey, seamKey))
        {
            throw new InvalidOperationException(
                $"Exchange response seam mismatch: expected={Format(seamKey)} actual={Format(envelope.SeamKey)}.");
        }

        if (!string.Equals(envelope.RequestCanonicalHash, requestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Exchange response request_canonical_hash mismatch for {Format(seamKey)}.");
        }

        if (!_allowedAgentKinds.Contains(envelope.AgentKind))
        {
            throw new InvalidOperationException(
                $"Exchange response agent_kind '{envelope.AgentKind}' is not allowed for this capture mode.");
        }

        return envelope;
    }

    private void WriteReject(
        string prefix,
        int attempt,
        SealedDecisionSeamKey seamKey,
        string requestHash,
        string reasonKind,
        string errorText)
    {
        SealedLlmExchangeEnvelope.WriteReject(
            Path.Combine(
                _exchangeDirectory,
                $"{prefix}.a{attempt.ToString(CultureInfo.InvariantCulture)}.reject.json"),
            new SealedLlmExchangeRejectV1(
                SealedLlmExchangeRejectV1.CurrentSchemaVersion,
                seamKey,
                requestHash,
                reasonKind,
                string.IsNullOrWhiteSpace(errorText) ? reasonKind : errorText,
                attempt + 1));
    }

    private static void PrevalidateAction(SealedLlmDecisionRequest request, string selectedAction)
    {
        switch (request.SeamKey.SeamType)
        {
            case SealedLlmSeamTypes.Deployment:
                _ = SealedLlmActionCodec.DecodeDeployment(
                    RequirePolicyObservation(request),
                    selectedAction);
                break;
            case SealedLlmSeamTypes.Reward:
                _ = SealedLlmActionCodec.DecodeReward(
                    RequirePolicyObservation(request),
                    selectedAction);
                break;
            case SealedLlmSeamTypes.Recruit:
                _ = SealedLlmActionCodec.DecodeRecruit(
                    RequireRosterObservation(request),
                    selectedAction);
                break;
            case SealedLlmSeamTypes.Passive:
                _ = SealedLlmActionCodec.DecodePassive(
                    RequireRosterObservation(request),
                    selectedAction);
                break;
            case SealedLlmSeamTypes.Refit:
                _ = SealedLlmActionCodec.DecodeRefit(
                    RequireRosterObservation(request),
                    selectedAction);
                break;
            default:
                throw new InvalidOperationException($"Unknown live decision seam '{request.SeamKey.SeamType}'.");
        }
    }

    private static HeadlessPolicyObservation RequirePolicyObservation(SealedLlmDecisionRequest request)
        => request.PolicyObservation
           ?? throw new InvalidOperationException(
               $"Seam '{request.SeamKey.SeamType}' requires a policy observation.");

    private static HeadlessRosterPolicyObservation RequireRosterObservation(SealedLlmDecisionRequest request)
        => request.RosterObservation
           ?? throw new InvalidOperationException(
               $"Seam '{request.SeamKey.SeamType}' requires a roster observation.");

    private void WaitForFinalFile(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            if (stopwatch.Elapsed >= timeout)
            {
                throw new TimeoutException($"Timed out waiting for exchange response '{Path.GetFileName(path)}'.");
            }

            Thread.Sleep(_pollInterval);
        }
    }

    private static string Prefix(string kind, int decisionIndex)
        => kind + decisionIndex.ToString("D3", CultureInfo.InvariantCulture);

    private static string Format(SealedDecisionSeamKey seamKey)
        => seamKey == null
            ? "null"
            : $"{seamKey.DecisionIndex.ToString(CultureInfo.InvariantCulture)}:{seamKey.SeamType}:{seamKey.Ordinal.ToString(CultureInfo.InvariantCulture)}";

    private static TimeSpan RequirePositive(TimeSpan value, string path)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(path, "Duration must be positive.");
        }

        return value;
    }
}
