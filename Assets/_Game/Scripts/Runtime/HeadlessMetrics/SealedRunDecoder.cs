using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>BT4/BT5/BT10이 공유할 수 있는 sealed run shape와 header cohort key.</summary>
public sealed record SealedRunShape(
    bool Valid,
    string FailureReason,
    bool CertificationEligible,
    bool HasTerminalFailure,
    string RunId,
    string BuildManifestHash,
    string ContentManifestHash,
    string VisibleSurfaceHash,
    string PromptSchemaHash,
    string ScorerConfigHash,
    string TraceManifestHash);

public sealed record DecodedSealedDecision(
    SealedDecisionSeamKey SeamKey,
    string SeamType,
    LlmDecisionResponseV1 Response,
    SealedObservationJoinView Join,
    bool TerminalFailure);

public sealed record DecodedSealedRun(
    SealedDecisionTraceV1? Trace,
    SealedRunShape Shape,
    IReadOnlyList<DecodedSealedDecision> Decisions,
    LlmRunReportResponseV1? RunReport);

/// <summary>sealed trace integrity, response frame decode, canonical round-trip을 단일 소유한다.</summary>
public static class SealedRunDecoder
{
    private const string DecisionCanonicalSchema = "LlmDecisionResponseV1";
    private const string DeclaredIntentCanonicalSchema = "LlmDeclaredIntentV1";
    private const string BuildHypothesisCanonicalSchema = "LlmBuildHypothesisV1";
    private const string RunReportCanonicalSchema = "LlmRunReportResponseV1";
    private const string EvaluationSentenceCanonicalSchema = "LlmEvaluationSentenceV1";

    private const string RunReportSeam = "run_report";

    private static readonly HashSet<string> DecisionSeams = new(StringComparer.Ordinal)
    {
        "deployment",
        "reward",
        "recruit",
        "level_node",
        "refit",
    };

    public static DecodedSealedRun Decode(SealedDecisionTraceV1 trace)
    {
        var failure = string.Empty;
        var header = trace?.Header;
        var entries = trace?.Entries;
        var decisions = new List<DecodedSealedDecision>();
        LlmRunReportResponseV1 runReport = null;

        void Fail(string reason)
        {
            if (failure.Length == 0)
            {
                failure = reason ?? "unknown_failure";
            }
        }

        if (trace == null)
        {
            Fail("trace_missing");
        }
        else if (header == null)
        {
            Fail("header_missing");
        }
        else if (!header.CertificationEligible)
        {
            Fail("capture_source_not_live_cold_start_llm");
        }

        if (entries == null)
        {
            Fail("entries_missing");
        }
        else if (entries.Count < 2)
        {
            Fail("entry_count_less_than_two");
        }

        var runReportIndexes = new List<int>();
        var terminalSeen = false;
        if (entries != null)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry?.SeamKey == null)
                {
                    Fail($"entry_or_seam_missing_at_{index}");
                    continue;
                }

                if (entry.SeamKey.DecisionIndex != index)
                {
                    Fail($"decision_index_not_contiguous_at_{index}");
                }

                if (entry.SeamKey.Ordinal != 0)
                {
                    Fail($"seam_ordinal_not_zero_at_{index}");
                }

                if (string.Equals(entry.SeamKey.SeamType, RunReportSeam, StringComparison.Ordinal))
                {
                    runReportIndexes.Add(index);
                    continue;
                }

                if (!DecisionSeams.Contains(entry.SeamKey.SeamType))
                {
                    Fail($"unknown_decision_seam_at_{index}:{entry.SeamKey.SeamType}");
                }

                if (terminalSeen)
                {
                    Fail($"decision_after_terminal_failure_at_{index}");
                }

                terminalSeen |= entry.TerminalFailure;
            }

            if (runReportIndexes.Count != 1)
            {
                Fail($"run_report_count_{runReportIndexes.Count}");
            }
            else if (runReportIndexes[0] != entries.Count - 1)
            {
                Fail("run_report_not_last");
            }
        }

        if (trace != null)
        {
            try
            {
                var integrity = SealedDecisionTraceReplayVerifier.Verify(trace, trace);
                if (!integrity.VerificationPassed)
                {
                    Fail($"trace_integrity:{integrity.FirstDivergenceReason}:{integrity.FirstDivergenceDetail}");
                }
            }
            catch (Exception exception) when (IsCanonicalFailure(exception))
            {
                Fail($"trace_integrity_exception:{exception.Message}");
            }
        }

        if (entries != null)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry?.SeamKey == null)
                {
                    continue;
                }

                if (string.Equals(entry.SeamKey.SeamType, RunReportSeam, StringComparison.Ordinal))
                {
                    if (runReportIndexes.Count == 1)
                    {
                        try
                        {
                            runReport = DecodeRunReportRoundTrip(entry.ResponseCanonicalBytes);
                        }
                        catch (Exception exception) when (IsCanonicalFailure(exception))
                        {
                            Fail($"run_report_decode_at_{index}:{exception.Message}");
                        }
                    }

                    continue;
                }

                try
                {
                    var response = DecodeDecisionRoundTrip(entry.ResponseCanonicalBytes);
                    var join = entry.ObservationCanonicalBytes == null
                        ? UnavailableJoin("observation_bytes_missing")
                        : SealedObservationJoinReader.Read(
                            entry.ObservationCanonicalBytes,
                            entry.SeamKey.SeamType);
                    decisions.Add(new DecodedSealedDecision(
                        entry.SeamKey,
                        entry.SeamKey.SeamType,
                        response,
                        join,
                        entry.TerminalFailure));
                }
                catch (Exception exception) when (IsCanonicalFailure(exception))
                {
                    Fail($"decision_decode_at_{index}:{exception.Message}");
                }
            }
        }

        var manifestHash = string.Empty;
        if (trace?.Header != null && trace.Entries != null)
        {
            try
            {
                manifestHash = SealedDecisionTraceHash.ComputeManifest(trace);
            }
            catch (Exception exception) when (IsCanonicalFailure(exception))
            {
                Fail($"manifest_compute:{exception.Message}");
            }
        }

        var shape = new SealedRunShape(
            failure.Length == 0,
            failure,
            header?.CertificationEligible == true,
            entries?.Any(entry => entry?.TerminalFailure == true) == true,
            header?.RunId ?? string.Empty,
            header?.BuildManifestHash ?? string.Empty,
            header?.ContentManifestHash ?? string.Empty,
            header?.VisibleSurfaceHash ?? string.Empty,
            header?.PromptSchemaHash ?? string.Empty,
            header?.ScorerConfigHash ?? string.Empty,
            manifestHash);
        return new DecodedSealedRun(trace, shape, decisions.ToArray(), runReport);
    }

    private static LlmDecisionResponseV1 DecodeDecisionRoundTrip(byte[] bytes)
    {
        var decoded = DecodeDecision(bytes);
        var parsed = LlmWireCanonicalSerializer.ParseDecisionResponse(
            LlmWireCanonicalSerializer.CanonicalJson(decoded));
        RequireSameBytes(bytes, LlmWireCanonicalSerializer.CanonicalBytes(parsed), "decision");
        return parsed;
    }

    private static LlmRunReportResponseV1 DecodeRunReportRoundTrip(byte[] bytes)
    {
        var decoded = DecodeRunReport(bytes);
        var parsed = LlmWireCanonicalSerializer.ParseRunReportResponse(
            LlmWireCanonicalSerializer.CanonicalJson(decoded));
        RequireSameBytes(bytes, LlmWireCanonicalSerializer.CanonicalBytes(parsed), "run_report");
        return parsed;
    }

    private static LlmDecisionResponseV1 DecodeDecision(byte[] bytes)
    {
        var reader = new SealedCanonicalFrameReader(bytes, "decision");
        reader.RequireSchema(DecisionCanonicalSchema);
        var selectedAction = reader.ReadString("selected_action");
        var declaredIntent = DecodeDeclaredIntent(reader.ReadBytes("declared_intent"));
        var intentRef = reader.ReadString("intent_ref");
        var hypothesisCount = reader.ReadCount("build_hypothesis_count");
        var hypotheses = new LlmBuildHypothesisV1[hypothesisCount];
        for (var index = 0; index < hypotheses.Length; index++)
        {
            hypotheses[index] = DecodeBuildHypothesis(reader.ReadBytes($"build_hypotheses[{index}]"));
        }

        reader.RequireEnd();
        return new LlmDecisionResponseV1(selectedAction, declaredIntent, intentRef, hypotheses);
    }

    private static LlmDeclaredIntentV1 DecodeDeclaredIntent(byte[] bytes)
    {
        var reader = new SealedCanonicalFrameReader(bytes, "declared_intent");
        reader.RequireSchema(DeclaredIntentCanonicalSchema);
        var result = new LlmDeclaredIntentV1(
            reader.ReadString("intent_id"),
            reader.ReadStrings("track_token_ids"),
            reader.ReadString("expected_payoff"),
            reader.ReadStrings("evidence_fact_ids"),
            reader.ReadString("next_acquisition_plan"),
            reader.ReadStrings("allowed_substitutions"),
            reader.ReadStrings("pivot_conditions"),
            reader.ReadDouble("confidence"));
        reader.RequireEnd();
        return result;
    }

    private static LlmBuildHypothesisV1 DecodeBuildHypothesis(byte[] bytes)
    {
        var reader = new SealedCanonicalFrameReader(bytes, "build_hypothesis");
        reader.RequireSchema(BuildHypothesisCanonicalSchema);
        var result = new LlmBuildHypothesisV1(
            reader.ReadString("subject_kind"),
            reader.ReadString("subject_id"),
            reader.ReadString("relation"),
            reader.ReadString("target_kind"),
            reader.ReadString("target_id"),
            reader.ReadStrings("evidence_refs"),
            reader.ReadDouble("confidence"));
        reader.RequireEnd();
        return result;
    }

    private static LlmRunReportResponseV1 DecodeRunReport(byte[] bytes)
    {
        var reader = new SealedCanonicalFrameReader(bytes, "run_report");
        reader.RequireSchema(RunReportCanonicalSchema);
        var desireRetrospective = reader.ReadString("desire_retrospective");
        var payoffOrNearMiss = reader.ReadString("payoff_or_near_miss");
        var nextConcept = reader.ReadString("next_concept");
        var complaints = reader.ReadStrings("complaints");
        var sentenceCount = reader.ReadCount("evaluation_sentence_count");
        var sentences = new LlmEvaluationSentenceV1[sentenceCount];
        for (var index = 0; index < sentences.Length; index++)
        {
            sentences[index] = DecodeEvaluationSentence(
                reader.ReadBytes($"evaluation_sentences[{index}]"));
        }

        var retryIntent = reader.ReadString("retry_intent");
        reader.RequireEnd();
        return new LlmRunReportResponseV1(
            desireRetrospective,
            payoffOrNearMiss,
            nextConcept,
            complaints,
            sentences,
            retryIntent);
    }

    private static LlmEvaluationSentenceV1 DecodeEvaluationSentence(byte[] bytes)
    {
        var reader = new SealedCanonicalFrameReader(bytes, "evaluation_sentence");
        reader.RequireSchema(EvaluationSentenceCanonicalSchema);
        var result = new LlmEvaluationSentenceV1(
            reader.ReadString("sentence"),
            reader.ReadStrings("telemetry_event_ids"));
        reader.RequireEnd();
        return result;
    }

    private static void RequireSameBytes(byte[] expected, byte[] actual, string scope)
    {
        if (expected == null || actual == null || !expected.AsSpan().SequenceEqual(actual.AsSpan()))
        {
            throw new FormatException($"{scope} canonical response is not byte-identical after round-trip.");
        }
    }

    private static SealedObservationJoinView UnavailableJoin(string reason)
        => new(
            false,
            reason,
            Array.Empty<string>(),
            Array.Empty<SealedRewardOptionRow>(),
            Array.Empty<SealedRecruitOfferRow>(),
            Array.Empty<SealedPassiveNodeRow>(),
            Array.Empty<SealedRefitItemRow>());

    private static bool IsCanonicalFailure(Exception exception)
        => exception is ArgumentException
            or FormatException
            or InvalidOperationException
            or ArithmeticException;
}
