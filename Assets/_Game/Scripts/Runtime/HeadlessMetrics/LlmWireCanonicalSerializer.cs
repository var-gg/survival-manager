using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SM.HeadlessMetrics;

/// <summary>
/// LLM wire DTO의 canonical frame bytes, FNV-1a hash, canonical JSON과 strict JSON parse를 단독 소유한다.
/// 모든 scalar는 <c>LengthPrefixedStableHash.AppendPart</c>의 UTF-8 byte-length frame을 사용한다.
/// </summary>
public static class LlmWireCanonicalSerializer
{
    private const string DecisionCanonicalSchema = "LlmDecisionResponseV1";
    private const string DeclaredIntentCanonicalSchema = "LlmDeclaredIntentV1";
    private const string BuildHypothesisCanonicalSchema = "LlmBuildHypothesisV1";
    private const string RunReportCanonicalSchema = "LlmRunReportResponseV1";
    private const string EvaluationSentenceCanonicalSchema = "LlmEvaluationSentenceV1";
    private const string PromptManifestCanonicalSchema = "LlmPromptManifestV1";

    /// <summary>
    /// TrackTokenIds, EvidenceFactIds, AllowedSubstitutions, PivotConditions, BuildHypotheses와
    /// 각 hypothesis의 EvidenceRefs는 set-like이므로 ordinal 정렬한다. 중복 원소는 보존한다.
    /// </summary>
    public static byte[] CanonicalBytes(LlmDecisionResponseV1 value)
    {
        ValidateDecision(value);

        using var payload = new MemoryStream();
        Append(payload, DecisionCanonicalSchema);
        Append(payload, value.SelectedAction);
        Append(payload, CanonicalDeclaredIntentBytes(value.DeclaredIntent));
        Append(payload, value.IntentRef);

        var hypotheses = OrderedHypotheses(value.BuildHypotheses, nameof(value.BuildHypotheses));
        Append(payload, Invariant(hypotheses.Length));
        foreach (var hypothesis in hypotheses)
        {
            Append(payload, CanonicalBuildHypothesisBytes(hypothesis));
        }

        return payload.ToArray();
    }

    /// <summary>
    /// Complaints와 각 TelemetryEventIds는 set-like이므로 ordinal 정렬한다.
    /// EvaluationSentences만 authored evaluation sequence이므로 입력 순서를 보존한다.
    /// </summary>
    public static byte[] CanonicalBytes(LlmRunReportResponseV1 value)
    {
        ValidateRunReport(value);

        using var payload = new MemoryStream();
        Append(payload, RunReportCanonicalSchema);
        Append(payload, value.DesireRetrospective);
        Append(payload, value.PayoffOrNearMiss);
        Append(payload, value.NextConcept);
        AppendSortedStrings(payload, value.Complaints, nameof(value.Complaints));
        Append(payload, Invariant(value.EvaluationSentences.Count));
        for (var index = 0; index < value.EvaluationSentences.Count; index++)
        {
            Append(payload, CanonicalEvaluationSentenceBytes(value.EvaluationSentences[index]));
        }

        Append(payload, value.RetryIntent);
        return payload.ToArray();
    }

    public static byte[] CanonicalBytes(LlmPromptManifestV1 value)
    {
        ValidatePromptManifest(value);

        using var payload = new MemoryStream();
        Append(payload, PromptManifestCanonicalSchema);
        Append(payload, value.PromptTemplateId);
        Append(payload, value.PromptTemplate);
        Append(payload, LlmPromptManifestV1.WireSchemaVersion);
        Append(payload, value.ColdStartBriefing);
        Append(payload, value.ModelSnapshotId);
        Append(payload, value.DecodingConfigCanonical);
        return payload.ToArray();
    }

    public static string Hash(LlmDecisionResponseV1 value)
        => LengthPrefixedStableHash.Compute(CanonicalBytes(value));

    public static string Hash(LlmRunReportResponseV1 value)
        => LengthPrefixedStableHash.Compute(CanonicalBytes(value));

    public static string Hash(LlmPromptManifestV1 value)
        => LengthPrefixedStableHash.Compute(CanonicalBytes(value));

    /// <summary>
    /// Strict parser가 받는 snake_case canonical JSON을 생성한다. Set-like collection은 canonical order로 쓴다.
    /// double은 모든 표면에서 invariant <c>R</c> round-trip format 하나만 사용한다.
    /// </summary>
    public static string CanonicalJson(LlmDecisionResponseV1 value)
    {
        ValidateDecision(value);
        return WriteJson(writer => WriteDecision(writer, value));
    }

    public static string CanonicalJson(LlmRunReportResponseV1 value)
    {
        ValidateRunReport(value);
        return WriteJson(writer => WriteRunReport(writer, value));
    }

    public static string CanonicalJson(LlmPromptManifestV1 value)
    {
        ValidatePromptManifest(value);
        return WriteJson(writer => WritePromptManifest(writer, value));
    }

    public static LlmDecisionResponseV1 ParseDecisionResponse(string json)
        => ParseDecision(ParseObject(json, nameof(json)), "$decision");

    public static LlmRunReportResponseV1 ParseRunReportResponse(string json)
        => ParseRunReport(ParseObject(json, nameof(json)), "$run_report");

    public static LlmPromptManifestV1 ParsePromptManifest(string json)
        => ParseManifest(ParseObject(json, nameof(json)), "$prompt_manifest");

    private static byte[] CanonicalDeclaredIntentBytes(LlmDeclaredIntentV1 value)
    {
        ValidateDeclaredIntent(value, nameof(value));

        using var payload = new MemoryStream();
        Append(payload, DeclaredIntentCanonicalSchema);
        Append(payload, value.IntentId);
        AppendSortedStrings(payload, value.TrackTokenIds, nameof(value.TrackTokenIds));
        Append(payload, value.ExpectedPayoff);
        AppendSortedStrings(payload, value.EvidenceFactIds, nameof(value.EvidenceFactIds));
        Append(payload, value.NextAcquisitionPlan);
        AppendSortedStrings(payload, value.AllowedSubstitutions, nameof(value.AllowedSubstitutions));
        AppendSortedStrings(payload, value.PivotConditions, nameof(value.PivotConditions));
        Append(payload, RoundTrip(value.Confidence));
        return payload.ToArray();
    }

    private static byte[] CanonicalBuildHypothesisBytes(LlmBuildHypothesisV1 value)
    {
        ValidateBuildHypothesis(value, nameof(value));

        using var payload = new MemoryStream();
        Append(payload, BuildHypothesisCanonicalSchema);
        Append(payload, value.SubjectKind);
        Append(payload, value.SubjectId);
        Append(payload, value.Relation);
        Append(payload, value.TargetKind);
        Append(payload, value.TargetId);
        AppendSortedStrings(payload, value.EvidenceRefs, nameof(value.EvidenceRefs));
        Append(payload, RoundTrip(value.Confidence));
        return payload.ToArray();
    }

    private static byte[] CanonicalEvaluationSentenceBytes(LlmEvaluationSentenceV1 value)
    {
        ValidateEvaluationSentence(value, nameof(value));

        using var payload = new MemoryStream();
        Append(payload, EvaluationSentenceCanonicalSchema);
        Append(payload, value.Sentence);
        AppendSortedStrings(payload, value.TelemetryEventIds, nameof(value.TelemetryEventIds));
        return payload.ToArray();
    }

    private static void WriteDecision(JsonTextWriter writer, LlmDecisionResponseV1 value)
    {
        writer.WriteStartObject();
        WriteStringProperty(writer, "selected_action", value.SelectedAction);
        writer.WritePropertyName("declared_intent");
        WriteDeclaredIntent(writer, value.DeclaredIntent);
        WriteStringProperty(writer, "intent_ref", value.IntentRef);
        writer.WritePropertyName("build_hypotheses");
        writer.WriteStartArray();
        foreach (var hypothesis in OrderedHypotheses(value.BuildHypotheses, nameof(value.BuildHypotheses)))
        {
            WriteBuildHypothesis(writer, hypothesis);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteDeclaredIntent(JsonTextWriter writer, LlmDeclaredIntentV1 value)
    {
        writer.WriteStartObject();
        WriteStringProperty(writer, "intent_id", value.IntentId);
        WriteStringArrayProperty(writer, "track_token_ids", value.TrackTokenIds, sorted: true);
        WriteStringProperty(writer, "expected_payoff", value.ExpectedPayoff);
        WriteStringArrayProperty(writer, "evidence_fact_ids", value.EvidenceFactIds, sorted: true);
        WriteStringProperty(writer, "next_acquisition_plan", value.NextAcquisitionPlan);
        WriteStringArrayProperty(writer, "allowed_substitutions", value.AllowedSubstitutions, sorted: true);
        WriteStringArrayProperty(writer, "pivot_conditions", value.PivotConditions, sorted: true);
        WriteDoubleProperty(writer, "confidence", value.Confidence);
        writer.WriteEndObject();
    }

    private static void WriteBuildHypothesis(JsonTextWriter writer, LlmBuildHypothesisV1 value)
    {
        writer.WriteStartObject();
        WriteStringProperty(writer, "subject_kind", value.SubjectKind);
        WriteStringProperty(writer, "subject_id", value.SubjectId);
        WriteStringProperty(writer, "relation", value.Relation);
        WriteStringProperty(writer, "target_kind", value.TargetKind);
        WriteStringProperty(writer, "target_id", value.TargetId);
        WriteStringArrayProperty(writer, "evidence_refs", value.EvidenceRefs, sorted: true);
        WriteDoubleProperty(writer, "confidence", value.Confidence);
        writer.WriteEndObject();
    }

    private static void WriteRunReport(JsonTextWriter writer, LlmRunReportResponseV1 value)
    {
        writer.WriteStartObject();
        WriteStringProperty(writer, "desire_retrospective", value.DesireRetrospective);
        WriteStringProperty(writer, "payoff_or_near_miss", value.PayoffOrNearMiss);
        WriteStringProperty(writer, "next_concept", value.NextConcept);
        WriteStringArrayProperty(writer, "complaints", value.Complaints, sorted: true);
        writer.WritePropertyName("evaluation_sentences");
        writer.WriteStartArray();
        for (var index = 0; index < value.EvaluationSentences.Count; index++)
        {
            WriteEvaluationSentence(writer, value.EvaluationSentences[index]);
        }

        writer.WriteEndArray();
        WriteStringProperty(writer, "retry_intent", value.RetryIntent);
        writer.WriteEndObject();
    }

    private static void WriteEvaluationSentence(JsonTextWriter writer, LlmEvaluationSentenceV1 value)
    {
        writer.WriteStartObject();
        WriteStringProperty(writer, "sentence", value.Sentence);
        WriteStringArrayProperty(writer, "telemetry_event_ids", value.TelemetryEventIds, sorted: true);
        writer.WriteEndObject();
    }

    private static void WritePromptManifest(JsonTextWriter writer, LlmPromptManifestV1 value)
    {
        writer.WriteStartObject();
        WriteStringProperty(writer, "prompt_template_id", value.PromptTemplateId);
        WriteStringProperty(writer, "prompt_template", value.PromptTemplate);
        WriteStringProperty(writer, "wire_schema_version", LlmPromptManifestV1.WireSchemaVersion);
        WriteStringProperty(writer, "cold_start_briefing", value.ColdStartBriefing);
        WriteStringProperty(writer, "model_snapshot_id", value.ModelSnapshotId);
        WriteStringProperty(writer, "decoding_config_canonical", value.DecodingConfigCanonical);
        writer.WriteEndObject();
    }

    private static LlmDecisionResponseV1 ParseDecision(JObject value, string path)
    {
        RequireExactProperties(
            value,
            path,
            "selected_action",
            "declared_intent",
            "intent_ref",
            "build_hypotheses");

        return new LlmDecisionResponseV1(
            RequiredString(value, "selected_action", path),
            ParseDeclaredIntent(RequiredObject(value, "declared_intent", path), $"{path}.declared_intent"),
            RequiredString(value, "intent_ref", path),
            ParseBuildHypotheses(RequiredArray(value, "build_hypotheses", path), $"{path}.build_hypotheses"));
    }

    private static LlmDeclaredIntentV1 ParseDeclaredIntent(JObject value, string path)
    {
        RequireExactProperties(
            value,
            path,
            "intent_id",
            "track_token_ids",
            "expected_payoff",
            "evidence_fact_ids",
            "next_acquisition_plan",
            "allowed_substitutions",
            "pivot_conditions",
            "confidence");

        return new LlmDeclaredIntentV1(
            RequiredString(value, "intent_id", path),
            ParseStrings(RequiredArray(value, "track_token_ids", path), $"{path}.track_token_ids"),
            RequiredString(value, "expected_payoff", path),
            ParseStrings(RequiredArray(value, "evidence_fact_ids", path), $"{path}.evidence_fact_ids"),
            RequiredString(value, "next_acquisition_plan", path),
            ParseStrings(RequiredArray(value, "allowed_substitutions", path), $"{path}.allowed_substitutions"),
            ParseStrings(RequiredArray(value, "pivot_conditions", path), $"{path}.pivot_conditions"),
            RequiredDouble(value, "confidence", path));
    }

    private static IReadOnlyList<LlmBuildHypothesisV1> ParseBuildHypotheses(JArray value, string path)
    {
        var result = new LlmBuildHypothesisV1[value.Count];
        for (var index = 0; index < value.Count; index++)
        {
            if (value[index] is not JObject item)
            {
                throw SchemaError($"{path}[{index}] must be an object.");
            }

            var itemPath = $"{path}[{index}]";
            RequireExactProperties(
                item,
                itemPath,
                "subject_kind",
                "subject_id",
                "relation",
                "target_kind",
                "target_id",
                "evidence_refs",
                "confidence");
            result[index] = new LlmBuildHypothesisV1(
                RequiredString(item, "subject_kind", itemPath),
                RequiredString(item, "subject_id", itemPath),
                RequiredString(item, "relation", itemPath),
                RequiredString(item, "target_kind", itemPath),
                RequiredString(item, "target_id", itemPath),
                ParseStrings(RequiredArray(item, "evidence_refs", itemPath), $"{itemPath}.evidence_refs"),
                RequiredDouble(item, "confidence", itemPath));
        }

        return result;
    }

    private static LlmRunReportResponseV1 ParseRunReport(JObject value, string path)
    {
        RequireExactProperties(
            value,
            path,
            "desire_retrospective",
            "payoff_or_near_miss",
            "next_concept",
            "complaints",
            "evaluation_sentences",
            "retry_intent");

        return new LlmRunReportResponseV1(
            RequiredString(value, "desire_retrospective", path),
            RequiredString(value, "payoff_or_near_miss", path),
            RequiredString(value, "next_concept", path),
            ParseStrings(RequiredArray(value, "complaints", path), $"{path}.complaints"),
            ParseEvaluationSentences(
                RequiredArray(value, "evaluation_sentences", path),
                $"{path}.evaluation_sentences"),
            RequiredString(value, "retry_intent", path));
    }

    private static IReadOnlyList<LlmEvaluationSentenceV1> ParseEvaluationSentences(JArray value, string path)
    {
        var result = new LlmEvaluationSentenceV1[value.Count];
        for (var index = 0; index < value.Count; index++)
        {
            if (value[index] is not JObject item)
            {
                throw SchemaError($"{path}[{index}] must be an object.");
            }

            var itemPath = $"{path}[{index}]";
            RequireExactProperties(item, itemPath, "sentence", "telemetry_event_ids");
            result[index] = new LlmEvaluationSentenceV1(
                RequiredString(item, "sentence", itemPath),
                ParseStrings(
                    RequiredArray(item, "telemetry_event_ids", itemPath),
                    $"{itemPath}.telemetry_event_ids"));
        }

        return result;
    }

    private static LlmPromptManifestV1 ParseManifest(JObject value, string path)
    {
        RequireExactProperties(
            value,
            path,
            "prompt_template_id",
            "prompt_template",
            "wire_schema_version",
            "cold_start_briefing",
            "model_snapshot_id",
            "decoding_config_canonical");

        var wireSchemaVersion = RequiredString(value, "wire_schema_version", path);
        if (!string.Equals(wireSchemaVersion, LlmPromptManifestV1.WireSchemaVersion, StringComparison.Ordinal))
        {
            throw SchemaError(
                $"{path}.wire_schema_version must equal '{LlmPromptManifestV1.WireSchemaVersion}'.");
        }

        return new LlmPromptManifestV1(
            RequiredString(value, "prompt_template_id", path),
            RequiredString(value, "prompt_template", path),
            RequiredString(value, "cold_start_briefing", path),
            RequiredString(value, "model_snapshot_id", path),
            RequiredString(value, "decoding_config_canonical", path));
    }

    private static JObject ParseObject(string json, string argumentName)
    {
        if (json == null) throw new ArgumentNullException(argumentName);

        var settings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Load,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Load,
        };
        var token = JToken.Parse(json, settings);
        if (ContainsComment(token))
        {
            throw SchemaError("JSON comments are not allowed.");
        }

        return token as JObject
               ?? throw SchemaError("The JSON root must be an object.");
    }

    private static bool ContainsComment(JToken value)
    {
        if (value.Type == JTokenType.Comment)
        {
            return true;
        }

        if (value is not JContainer container)
        {
            return false;
        }

        foreach (var child in container.Children())
        {
            if (ContainsComment(child))
            {
                return true;
            }
        }

        return false;
    }

    private static void RequireExactProperties(JObject value, string path, params string[] expected)
    {
        var expectedNames = new HashSet<string>(expected, StringComparer.Ordinal);
        foreach (var property in value.Properties())
        {
            if (!expectedNames.Contains(property.Name))
            {
                throw SchemaError($"Unknown field '{path}.{property.Name}'.");
            }
        }

        foreach (var propertyName in expected)
        {
            if (value.Property(propertyName) == null)
            {
                throw SchemaError($"Missing required field '{path}.{propertyName}'.");
            }
        }
    }

    private static string RequiredString(JObject value, string propertyName, string path)
    {
        var token = value[propertyName];
        if (token?.Type != JTokenType.String)
        {
            throw SchemaError($"{path}.{propertyName} must be a non-null string.");
        }

        return token.Value<string>();
    }

    private static JObject RequiredObject(JObject value, string propertyName, string path)
        => value[propertyName] as JObject
           ?? throw SchemaError($"{path}.{propertyName} must be an object.");

    private static JArray RequiredArray(JObject value, string propertyName, string path)
        => value[propertyName] as JArray
           ?? throw SchemaError($"{path}.{propertyName} must be an array.");

    private static IReadOnlyList<string> ParseStrings(JArray value, string path)
    {
        var result = new string[value.Count];
        for (var index = 0; index < value.Count; index++)
        {
            if (value[index]?.Type != JTokenType.String)
            {
                throw SchemaError($"{path}[{index}] must be a non-null string.");
            }

            result[index] = value[index].Value<string>();
        }

        return result;
    }

    private static double RequiredDouble(JObject value, string propertyName, string path)
    {
        var token = value[propertyName];
        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
        {
            throw SchemaError($"{path}.{propertyName} must be a JSON number.");
        }

        var result = token.Value<double>();
        RequireFinite(result, $"{path}.{propertyName}");
        return result;
    }

    private static void ValidateDecision(LlmDecisionResponseV1 value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        RequireString(value.SelectedAction, nameof(value.SelectedAction));
        ValidateDeclaredIntent(value.DeclaredIntent, nameof(value.DeclaredIntent));
        RequireString(value.IntentRef, nameof(value.IntentRef));
        OrderedHypotheses(value.BuildHypotheses, nameof(value.BuildHypotheses));
    }

    private static void ValidateDeclaredIntent(LlmDeclaredIntentV1 value, string path)
    {
        if (value == null) throw new ArgumentException($"{path} is required.", path);
        RequireString(value.IntentId, $"{path}.{nameof(value.IntentId)}");
        ValidateStrings(value.TrackTokenIds, $"{path}.{nameof(value.TrackTokenIds)}");
        RequireString(value.ExpectedPayoff, $"{path}.{nameof(value.ExpectedPayoff)}");
        ValidateStrings(value.EvidenceFactIds, $"{path}.{nameof(value.EvidenceFactIds)}");
        RequireString(value.NextAcquisitionPlan, $"{path}.{nameof(value.NextAcquisitionPlan)}");
        ValidateStrings(value.AllowedSubstitutions, $"{path}.{nameof(value.AllowedSubstitutions)}");
        ValidateStrings(value.PivotConditions, $"{path}.{nameof(value.PivotConditions)}");
        RequireFinite(value.Confidence, $"{path}.{nameof(value.Confidence)}");
    }

    private static void ValidateBuildHypothesis(LlmBuildHypothesisV1 value, string path)
    {
        if (value == null) throw new ArgumentException($"{path} is required.", path);
        RequireString(value.SubjectKind, $"{path}.{nameof(value.SubjectKind)}");
        RequireString(value.SubjectId, $"{path}.{nameof(value.SubjectId)}");
        RequireString(value.Relation, $"{path}.{nameof(value.Relation)}");
        RequireString(value.TargetKind, $"{path}.{nameof(value.TargetKind)}");
        RequireString(value.TargetId, $"{path}.{nameof(value.TargetId)}");
        ValidateStrings(value.EvidenceRefs, $"{path}.{nameof(value.EvidenceRefs)}");
        RequireFinite(value.Confidence, $"{path}.{nameof(value.Confidence)}");
    }

    private static void ValidateRunReport(LlmRunReportResponseV1 value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        RequireString(value.DesireRetrospective, nameof(value.DesireRetrospective));
        RequireString(value.PayoffOrNearMiss, nameof(value.PayoffOrNearMiss));
        RequireString(value.NextConcept, nameof(value.NextConcept));
        ValidateStrings(value.Complaints, nameof(value.Complaints));
        if (value.EvaluationSentences == null)
        {
            throw new ArgumentException("EvaluationSentences is required.", nameof(value.EvaluationSentences));
        }

        for (var index = 0; index < value.EvaluationSentences.Count; index++)
        {
            ValidateEvaluationSentence(
                value.EvaluationSentences[index],
                $"{nameof(value.EvaluationSentences)}[{index}]");
        }

        RequireString(value.RetryIntent, nameof(value.RetryIntent));
    }

    private static void ValidateEvaluationSentence(LlmEvaluationSentenceV1 value, string path)
    {
        if (value == null) throw new ArgumentException($"{path} is required.", path);
        RequireString(value.Sentence, $"{path}.{nameof(value.Sentence)}");
        ValidateStrings(value.TelemetryEventIds, $"{path}.{nameof(value.TelemetryEventIds)}");
    }

    private static void ValidatePromptManifest(LlmPromptManifestV1 value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        RequireString(value.PromptTemplateId, nameof(value.PromptTemplateId));
        RequireString(value.PromptTemplate, nameof(value.PromptTemplate));
        RequireString(value.ColdStartBriefing, nameof(value.ColdStartBriefing));
        RequireString(value.ModelSnapshotId, nameof(value.ModelSnapshotId));
        RequireString(value.DecodingConfigCanonical, nameof(value.DecodingConfigCanonical));
    }

    private static void ValidateStrings(IReadOnlyList<string> values, string path)
    {
        if (values == null) throw new ArgumentException($"{path} is required.", path);
        for (var index = 0; index < values.Count; index++)
        {
            RequireString(values[index], $"{path}[{index}]");
        }
    }

    private static void RequireString(string value, string path)
    {
        if (value == null) throw new ArgumentException($"{path} cannot be null.", path);
    }

    private static void RequireFinite(double value, string path)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentException($"{path} must be finite.", path);
        }
    }

    private static LlmBuildHypothesisV1[] OrderedHypotheses(
        IReadOnlyList<LlmBuildHypothesisV1> values,
        string path)
    {
        if (values == null) throw new ArgumentException($"{path} is required.", path);

        var result = new LlmBuildHypothesisV1[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            ValidateBuildHypothesis(values[index], $"{path}[{index}]");
            result[index] = values[index];
        }

        Array.Sort(result, CompareHypotheses);
        return result;
    }

    private static int CompareHypotheses(LlmBuildHypothesisV1 left, LlmBuildHypothesisV1 right)
    {
        var result = StringComparer.Ordinal.Compare(left.SubjectKind, right.SubjectKind);
        if (result != 0) return result;
        result = StringComparer.Ordinal.Compare(left.SubjectId, right.SubjectId);
        if (result != 0) return result;
        result = StringComparer.Ordinal.Compare(left.Relation, right.Relation);
        if (result != 0) return result;
        result = StringComparer.Ordinal.Compare(left.TargetKind, right.TargetKind);
        if (result != 0) return result;
        result = StringComparer.Ordinal.Compare(left.TargetId, right.TargetId);
        if (result != 0) return result;
        result = CompareSortedStrings(left.EvidenceRefs, right.EvidenceRefs);
        if (result != 0) return result;
        return StringComparer.Ordinal.Compare(RoundTrip(left.Confidence), RoundTrip(right.Confidence));
    }

    private static int CompareSortedStrings(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var orderedLeft = OrderedStrings(left, nameof(left));
        var orderedRight = OrderedStrings(right, nameof(right));
        var commonCount = Math.Min(orderedLeft.Length, orderedRight.Length);
        for (var index = 0; index < commonCount; index++)
        {
            var result = StringComparer.Ordinal.Compare(orderedLeft[index], orderedRight[index]);
            if (result != 0) return result;
        }

        return orderedLeft.Length.CompareTo(orderedRight.Length);
    }

    private static string[] OrderedStrings(IReadOnlyList<string> values, string path)
    {
        ValidateStrings(values, path);
        var result = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            result[index] = values[index];
        }

        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    private static void AppendSortedStrings(Stream payload, IReadOnlyList<string> values, string path)
    {
        var ordered = OrderedStrings(values, path);
        Append(payload, Invariant(ordered.Length));
        foreach (var value in ordered)
        {
            Append(payload, value);
        }
    }

    private static void WriteStringProperty(JsonTextWriter writer, string propertyName, string value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteValue(value);
    }

    private static void WriteStringArrayProperty(
        JsonTextWriter writer,
        string propertyName,
        IReadOnlyList<string> values,
        bool sorted)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        var source = sorted ? OrderedStrings(values, propertyName) : CopyStrings(values, propertyName);
        foreach (var value in source)
        {
            writer.WriteValue(value);
        }

        writer.WriteEndArray();
    }

    private static string[] CopyStrings(IReadOnlyList<string> values, string path)
    {
        ValidateStrings(values, path);
        var result = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            result[index] = values[index];
        }

        return result;
    }

    private static void WriteDoubleProperty(JsonTextWriter writer, string propertyName, double value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteRawValue(RoundTrip(value));
    }

    private static string WriteJson(Action<JsonTextWriter> write)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new JsonTextWriter(output)
        {
            CloseOutput = false,
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.None,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        };
        write(writer);
        writer.Flush();
        return output.ToString();
    }

    private static void Append(Stream payload, string value)
        => LengthPrefixedStableHash.AppendPart(payload, value);

    private static void Append(Stream payload, byte[] value)
        => LengthPrefixedStableHash.AppendPart(payload, value);

    private static string Invariant(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string RoundTrip(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private static JsonSerializationException SchemaError(string message)
        => new(message);
}
