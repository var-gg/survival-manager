using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

/// <summary>냉시작 LLM wire DTO의 strict parse와 byte-deterministic canonical contract.</summary>
[Category("FastUnit")]
public sealed class LlmWireCanonicalSerializerFastTests
{
    public enum DecisionMutation
    {
        SelectedAction,
        IntentId,
        TrackTokenIds,
        ExpectedPayoff,
        EvidenceFactIds,
        NextAcquisitionPlan,
        AllowedSubstitutions,
        PivotConditions,
        DeclaredIntentConfidence,
        IntentRef,
        HypothesisSubjectKind,
        HypothesisSubjectId,
        HypothesisRelation,
        HypothesisTargetKind,
        HypothesisTargetId,
        HypothesisEvidenceRefs,
        HypothesisConfidence,
    }

    public enum RunReportMutation
    {
        DesireRetrospective,
        PayoffOrNearMiss,
        NextConcept,
        Complaints,
        EvaluationSentence,
        TelemetryEventIds,
        RetryIntent,
    }

    public enum ManifestMutation
    {
        PromptTemplateId,
        PromptTemplate,
        ColdStartBriefing,
        ModelSnapshotId,
        DecodingConfigCanonical,
    }

    [Test]
    public void CanonicalBytes_EqualDtos_AreByteStableAcrossRepeatedSerialization()
    {
        var first = Decision();
        var second = Decision();

        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(first),
            Is.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(first)));
        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(second),
            Is.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(first)));
        Assert.That(
            LlmWireCanonicalSerializer.Hash(second),
            Is.EqualTo(LlmWireCanonicalSerializer.Hash(first)));
    }

    [TestCase(DecisionMutation.SelectedAction)]
    [TestCase(DecisionMutation.IntentId)]
    [TestCase(DecisionMutation.TrackTokenIds)]
    [TestCase(DecisionMutation.ExpectedPayoff)]
    [TestCase(DecisionMutation.EvidenceFactIds)]
    [TestCase(DecisionMutation.NextAcquisitionPlan)]
    [TestCase(DecisionMutation.AllowedSubstitutions)]
    [TestCase(DecisionMutation.PivotConditions)]
    [TestCase(DecisionMutation.DeclaredIntentConfidence)]
    [TestCase(DecisionMutation.IntentRef)]
    [TestCase(DecisionMutation.HypothesisSubjectKind)]
    [TestCase(DecisionMutation.HypothesisSubjectId)]
    [TestCase(DecisionMutation.HypothesisRelation)]
    [TestCase(DecisionMutation.HypothesisTargetKind)]
    [TestCase(DecisionMutation.HypothesisTargetId)]
    [TestCase(DecisionMutation.HypothesisEvidenceRefs)]
    [TestCase(DecisionMutation.HypothesisConfidence)]
    public void Hash_AnyDecisionFieldMutation_ChangesHash(DecisionMutation mutation)
    {
        var original = Decision();
        var mutated = Mutate(original, mutation);

        Assert.That(
            LlmWireCanonicalSerializer.Hash(mutated),
            Is.Not.EqualTo(LlmWireCanonicalSerializer.Hash(original)));
    }

    [TestCase(RunReportMutation.DesireRetrospective)]
    [TestCase(RunReportMutation.PayoffOrNearMiss)]
    [TestCase(RunReportMutation.NextConcept)]
    [TestCase(RunReportMutation.Complaints)]
    [TestCase(RunReportMutation.EvaluationSentence)]
    [TestCase(RunReportMutation.TelemetryEventIds)]
    [TestCase(RunReportMutation.RetryIntent)]
    public void Hash_AnyRunReportFieldMutation_ChangesHash(RunReportMutation mutation)
    {
        var original = RunReport();
        var mutated = Mutate(original, mutation);

        Assert.That(
            LlmWireCanonicalSerializer.Hash(mutated),
            Is.Not.EqualTo(LlmWireCanonicalSerializer.Hash(original)));
    }

    [Test]
    public void CanonicalBytes_SetLikeCollectionsUseOrdinalOrder()
    {
        var decision = Decision();
        var reorderedDecision = decision with
        {
            DeclaredIntent = decision.DeclaredIntent with
            {
                TrackTokenIds = decision.DeclaredIntent.TrackTokenIds.Reverse().ToArray(),
                EvidenceFactIds = decision.DeclaredIntent.EvidenceFactIds.Reverse().ToArray(),
                AllowedSubstitutions = decision.DeclaredIntent.AllowedSubstitutions.Reverse().ToArray(),
                PivotConditions = decision.DeclaredIntent.PivotConditions.Reverse().ToArray(),
            },
            BuildHypotheses = decision.BuildHypotheses
                .Reverse()
                .Select(value => value with { EvidenceRefs = value.EvidenceRefs.Reverse().ToArray() })
                .ToArray(),
        };
        var report = RunReport();
        var reorderedReport = report with
        {
            Complaints = report.Complaints.Reverse().ToArray(),
            EvaluationSentences = report.EvaluationSentences
                .Select(value => value with { TelemetryEventIds = value.TelemetryEventIds.Reverse().ToArray() })
                .ToArray(),
        };

        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(reorderedDecision),
            Is.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(decision)));
        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(reorderedReport),
            Is.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(report)));
    }

    [Test]
    public void CanonicalBytes_EvaluationSentenceAuthoredOrderIsSemantic()
    {
        var report = RunReport();
        var reordered = report with
        {
            EvaluationSentences = report.EvaluationSentences.Reverse().ToArray(),
        };

        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(reordered),
            Is.Not.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(report)));
    }

    [Test]
    public void StrictParse_CanonicalJsonRoundTripsAllWireDtos()
    {
        var decision = Decision();
        var report = RunReport();
        var manifest = Manifest();

        var parsedDecision = LlmWireCanonicalSerializer.ParseDecisionResponse(
            LlmWireCanonicalSerializer.CanonicalJson(decision));
        var parsedReport = LlmWireCanonicalSerializer.ParseRunReportResponse(
            LlmWireCanonicalSerializer.CanonicalJson(report));
        var parsedManifest = LlmWireCanonicalSerializer.ParsePromptManifest(
            LlmWireCanonicalSerializer.CanonicalJson(manifest));

        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(parsedDecision),
            Is.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(decision)));
        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(parsedReport),
            Is.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(report)));
        Assert.That(
            LlmWireCanonicalSerializer.CanonicalBytes(parsedManifest),
            Is.EqualTo(LlmWireCanonicalSerializer.CanonicalBytes(manifest)));
    }

    [Test]
    public void StrictParse_RejectsMalformedMissingUnknownAndDuplicateFields()
    {
        var canonical = LlmWireCanonicalSerializer.CanonicalJson(Decision());
        var missing = canonical.Replace("\"selected_action\":\"deploy:a\",", string.Empty);
        var unknown = canonical.Insert(canonical.Length - 1, ",\"unexpected\":true");
        var duplicate = canonical.Replace(
            "\"selected_action\":\"deploy:a\"",
            "\"selected_action\":\"deploy:a\",\"selected_action\":\"deploy:b\"");

        Assert.That(
            () => LlmWireCanonicalSerializer.ParseDecisionResponse("{"),
            Throws.Exception);
        Assert.That(
            () => LlmWireCanonicalSerializer.ParseDecisionResponse(missing),
            Throws.Exception);
        Assert.That(
            () => LlmWireCanonicalSerializer.ParseDecisionResponse(unknown),
            Throws.Exception);
        Assert.That(
            () => LlmWireCanonicalSerializer.ParseDecisionResponse(duplicate),
            Throws.Exception);
    }

    [Test]
    public void CanonicalJson_DoubleUsesInvariantRoundTripFormat()
    {
        var decision = Decision() with
        {
            DeclaredIntent = Decision().DeclaredIntent with { Confidence = 0.30000000000000004d },
        };

        var json = LlmWireCanonicalSerializer.CanonicalJson(decision);
        var bytes = Encoding.UTF8.GetString(LlmWireCanonicalSerializer.CanonicalBytes(decision));

        Assert.That(json, Does.Contain("\"confidence\":0.30000000000000004"));
        Assert.That(bytes, Does.Contain("0.30000000000000004"));
    }

    [TestCase(ManifestMutation.PromptTemplateId)]
    [TestCase(ManifestMutation.PromptTemplate)]
    [TestCase(ManifestMutation.ColdStartBriefing)]
    [TestCase(ManifestMutation.ModelSnapshotId)]
    [TestCase(ManifestMutation.DecodingConfigCanonical)]
    public void PromptSchemaHash_ChangesIfManifestFieldChanges(ManifestMutation mutation)
    {
        var original = Manifest();
        var mutated = Mutate(original, mutation);

        Assert.That(mutated.PromptSchemaHash, Is.Not.EqualTo(original.PromptSchemaHash));
    }

    [Test]
    public void PromptSchemaHash_IsStableIfManifestFieldsDoNotChange()
    {
        var first = Manifest();
        var second = Manifest();

        Assert.That(first.PromptSchemaHash, Is.EqualTo(second.PromptSchemaHash));
        Assert.That(first.PromptSchemaHash, Is.EqualTo(LlmWireCanonicalSerializer.Hash(first)));
        Assert.That(
            LlmWireCanonicalSerializer.CanonicalJson(first),
            Does.Contain($"\"wire_schema_version\":\"{LlmPromptManifestV1.WireSchemaVersion}\""));
    }

    private static LlmDecisionResponseV1 Decision()
        => new(
            "deploy:a",
            new LlmDeclaredIntentV1(
                "intent-alpha",
                new[] { "track:b", "track:a" },
                "payoff:bleed-loop",
                new[] { "fact:2", "fact:1" },
                "acquire:bleed-enabler",
                new[] { "sub:poison", "sub:burn" },
                new[] { "pivot:no-payoff", "pivot:no-enabler" },
                0.75d),
            "intent-alpha",
            new[]
            {
                new LlmBuildHypothesisV1(
                    "hero",
                    "scout",
                    "enables",
                    "status",
                    "bleed",
                    new[] { "fact:2", "fact:1" },
                    0.625d),
                new LlmBuildHypothesisV1(
                    "formation",
                    "wedge",
                    "amplifies",
                    "role",
                    "frontline",
                    new[] { "fact:4", "fact:3" },
                    0.5d),
            });

    private static LlmRunReportResponseV1 RunReport()
        => new(
            "I pursued a bleed loop.",
            "The payoff arrived one battle late.",
            "Try a guard-break concept.",
            new[] { "complaint:reward-noise", "complaint:weak-preview" },
            new[]
            {
                new LlmEvaluationSentenceV1(
                    "The first payoff was legible.",
                    new[] { "event:2", "event:1" }),
                new LlmEvaluationSentenceV1(
                    "The near miss still suggested a retry.",
                    new[] { "event:4", "event:3" }),
            },
            "retry with a tighter acquisition plan");

    private static LlmPromptManifestV1 Manifest()
        => new(
            "cold-start-beta-v1",
            "Choose exactly one legal action and report intent.",
            "No prior run history is available.",
            "model-snapshot-001",
            "{\"temperature\":0,\"top_p\":1}");

    private static LlmDecisionResponseV1 Mutate(LlmDecisionResponseV1 value, DecisionMutation mutation)
    {
        var intent = value.DeclaredIntent;
        var hypothesis = value.BuildHypotheses[0];
        return mutation switch
        {
            DecisionMutation.SelectedAction => value with { SelectedAction = "deploy:b" },
            DecisionMutation.IntentId => value with { DeclaredIntent = intent with { IntentId = "intent-beta" } },
            DecisionMutation.TrackTokenIds => value with
            {
                DeclaredIntent = intent with { TrackTokenIds = intent.TrackTokenIds.Append("track:c").ToArray() },
            },
            DecisionMutation.ExpectedPayoff => value with
            {
                DeclaredIntent = intent with { ExpectedPayoff = "payoff:guard-break" },
            },
            DecisionMutation.EvidenceFactIds => value with
            {
                DeclaredIntent = intent with { EvidenceFactIds = intent.EvidenceFactIds.Append("fact:9").ToArray() },
            },
            DecisionMutation.NextAcquisitionPlan => value with
            {
                DeclaredIntent = intent with { NextAcquisitionPlan = "acquire:guard-break" },
            },
            DecisionMutation.AllowedSubstitutions => value with
            {
                DeclaredIntent = intent with
                {
                    AllowedSubstitutions = intent.AllowedSubstitutions.Append("sub:stun").ToArray(),
                },
            },
            DecisionMutation.PivotConditions => value with
            {
                DeclaredIntent = intent with { PivotConditions = intent.PivotConditions.Append("pivot:late").ToArray() },
            },
            DecisionMutation.DeclaredIntentConfidence => value with
            {
                DeclaredIntent = intent with { Confidence = 0.76d },
            },
            DecisionMutation.IntentRef => value with { IntentRef = "intent-beta" },
            DecisionMutation.HypothesisSubjectKind => ReplaceFirstHypothesis(
                value,
                hypothesis with { SubjectKind = "item" }),
            DecisionMutation.HypothesisSubjectId => ReplaceFirstHypothesis(
                value,
                hypothesis with { SubjectId = "blade" }),
            DecisionMutation.HypothesisRelation => ReplaceFirstHypothesis(
                value,
                hypothesis with { Relation = "amplifies" }),
            DecisionMutation.HypothesisTargetKind => ReplaceFirstHypothesis(
                value,
                hypothesis with { TargetKind = "role" }),
            DecisionMutation.HypothesisTargetId => ReplaceFirstHypothesis(
                value,
                hypothesis with { TargetId = "executioner" }),
            DecisionMutation.HypothesisEvidenceRefs => ReplaceFirstHypothesis(
                value,
                hypothesis with { EvidenceRefs = hypothesis.EvidenceRefs.Append("fact:9").ToArray() }),
            DecisionMutation.HypothesisConfidence => ReplaceFirstHypothesis(
                value,
                hypothesis with { Confidence = 0.626d }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null),
        };
    }

    private static LlmDecisionResponseV1 ReplaceFirstHypothesis(
        LlmDecisionResponseV1 value,
        LlmBuildHypothesisV1 replacement)
    {
        var hypotheses = value.BuildHypotheses.ToArray();
        hypotheses[0] = replacement;
        return value with { BuildHypotheses = hypotheses };
    }

    private static LlmRunReportResponseV1 Mutate(LlmRunReportResponseV1 value, RunReportMutation mutation)
    {
        var firstEvaluation = value.EvaluationSentences[0];
        return mutation switch
        {
            RunReportMutation.DesireRetrospective => value with { DesireRetrospective = "I changed desires." },
            RunReportMutation.PayoffOrNearMiss => value with { PayoffOrNearMiss = "No payoff arrived." },
            RunReportMutation.NextConcept => value with { NextConcept = "Try a summon concept." },
            RunReportMutation.Complaints => value with
            {
                Complaints = value.Complaints.Append("complaint:opaque-choice").ToArray(),
            },
            RunReportMutation.EvaluationSentence => ReplaceFirstEvaluation(
                value,
                firstEvaluation with { Sentence = "The payoff was not legible." }),
            RunReportMutation.TelemetryEventIds => ReplaceFirstEvaluation(
                value,
                firstEvaluation with
                {
                    TelemetryEventIds = firstEvaluation.TelemetryEventIds.Append("event:9").ToArray(),
                }),
            RunReportMutation.RetryIntent => value with { RetryIntent = "do not retry" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null),
        };
    }

    private static LlmRunReportResponseV1 ReplaceFirstEvaluation(
        LlmRunReportResponseV1 value,
        LlmEvaluationSentenceV1 replacement)
    {
        var evaluations = value.EvaluationSentences.ToArray();
        evaluations[0] = replacement;
        return value with { EvaluationSentences = evaluations };
    }

    private static LlmPromptManifestV1 Mutate(LlmPromptManifestV1 value, ManifestMutation mutation)
        => mutation switch
        {
            ManifestMutation.PromptTemplateId => value with { PromptTemplateId = "cold-start-beta-v2" },
            ManifestMutation.PromptTemplate => value with { PromptTemplate = value.PromptTemplate + " Be concise." },
            ManifestMutation.ColdStartBriefing => value with { ColdStartBriefing = "A new briefing." },
            ManifestMutation.ModelSnapshotId => value with { ModelSnapshotId = "model-snapshot-002" },
            ManifestMutation.DecodingConfigCanonical => value with
            {
                DecodingConfigCanonical = "{\"temperature\":0.1,\"top_p\":1}",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null),
        };
}
