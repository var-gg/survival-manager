using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessCensus;

namespace SM.Tests.EditMode;

/// <summary>
/// BT4 빌드 문법 유추 채점기(<see cref="BuildGrammarInferenceScorer"/>)의 정직성 적대 검증.
/// 실 냉시작 LLM 토큰 투입 전, 채점기가 off-surface·미해결 증거·환각·back-dated·단일시스템 claim을
/// 거부하고 recall을 노출 컨셉 edge로 스코프함을 결정적으로 증명한다.
/// </summary>
[Category("FastUnit")]
public sealed class BuildGrammarInferenceScorerFastTests
{
    private static BuildGrammarTruthEdge Edge(
        string subjectKind,
        string subjectId,
        string relation,
        string targetKind,
        string targetId,
        bool actionable = true)
        => new(
            $"{subjectKind}:{subjectId}|{relation}|{targetKind}:{targetId}",
            subjectKind,
            subjectId,
            relation,
            targetKind,
            targetId,
            TruthValue: string.Empty,
            Actionable: actionable,
            FeedbackRequired: false,
            ExpectedFeedbackWitness: string.Empty);

    private static string Key(string subjectKind, string subjectId, string relation, string targetKind, string targetId)
        => $"{subjectKind}:{subjectId}|{relation}|{targetKind}:{targetId}";

    // 소형 world: blade→bleed(produces), vanguard→bulwark(pays_off)가 노출 컨셉.
    // blade→phys_power(amplifies)는 visible-true지만 컨셉 밖(recall 분모 제외 검증용).
    private static BuildGrammarTruthGraph World()
        => new(new[]
        {
            Edge("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed"),
            Edge("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark"),
            Edge("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power"),
        });

    private static readonly string[] Visible =
    {
        "skill:blade", "status:bleed", "tag:vanguard", "team_rule:bulwark", "stat:phys_power",
    };

    private static readonly string[] KnownFacts =
    {
        "fact:blade-bleed", "fact:vanguard-bulwark", "fact:blade-power", "fact:extra",
    };

    private static readonly string[] ConceptEdges =
    {
        Key("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed"),
        Key("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark"),
    };

    private static BuildHypothesisClaim Claim(
        string subjectKind,
        string subjectId,
        string relation,
        string targetKind,
        string targetId,
        params string[] evidence)
        => new(subjectKind, subjectId, relation, targetKind, targetId, evidence, 0.8d);

    private static BuildGrammarInferenceInput Input(int cutoff, params BuildConceptProposal[] proposals)
        => new(proposals, World(), Visible, KnownFacts, ConceptEdges, cutoff);

    [Test]
    public void Score_ExactConceptClaimsBeforePayoff_PerfectPrecisionRecallAndValidConcept()
    {
        var proposal = new BuildConceptProposal(
            "p-exact",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark", "fact:vanguard-bulwark"),
            },
            DeclaredAtDecisionIndex: 1,
            PayoffObservedAtDecisionIndex: 5);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.GuardViolationCount, Is.EqualTo(0), "정상 claim은 guard 위반 0.");
        Assert.That(result.PrecisionMin, Is.EqualTo(1.0d).Within(1e-9));
        Assert.That(result.RecallMin, Is.EqualTo(1.0d).Within(1e-9));
        Assert.That(result.ValidConceptCount, Is.EqualTo(1));
    }

    [Test]
    public void Score_OffSurfaceTargetToken_IsGuardRejectedNotScored()
    {
        // status:poison 은 visible token 밖 = no-cheat 위반.
        var proposal = new BuildConceptProposal(
            "p-offsurface",
            new[] { Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "poison", "fact:blade-bleed") },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.GuardViolationCount, Is.EqualTo(1));
        Assert.That(result.GuardViolations.Single(), Does.Contain("off_surface_target"));
        Assert.That(result.AdmissibleClaimCount, Is.EqualTo(0));
        Assert.That(result.ValidConceptCount, Is.EqualTo(0));
    }

    [Test]
    public void Score_UnresolvedEvidenceRef_IsGuardRejected()
    {
        var proposal = new BuildConceptProposal(
            "p-badevidence",
            new[] { Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:hallucinated") },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.GuardViolationCount, Is.EqualTo(1));
        Assert.That(result.GuardViolations.Single(), Does.Contain("unresolved_evidence"));
        Assert.That(result.AdmissibleClaimCount, Is.EqualTo(0));
    }

    [Test]
    public void Score_HallucinatedEdgeOverVisibleTokens_LowersPrecision()
    {
        // 양 endpoint 는 visible 이지만 truth graph 에 없는 edge = 환각. produces claimed=2 correct=1.
        var proposal = new BuildConceptProposal(
            "p-hallucinate",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("skill", "blade", BuildGrammarRelation.Produces, "team_rule", "bulwark", "fact:extra"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.GuardViolationCount, Is.EqualTo(0), "visible token 이면 guard 통과(환각은 precision 으로 벌점).");
        var produces = result.FamilyScores.Single(score => score.Relation == BuildGrammarRelation.Produces);
        Assert.That(produces.ClaimedCount, Is.EqualTo(2));
        Assert.That(produces.CorrectCount, Is.EqualTo(1));
        Assert.That(produces.Precision, Is.EqualTo(0.5d).Within(1e-9));
        Assert.That(result.PrecisionMin, Is.LessThanOrEqualTo(0.5d));
    }

    [Test]
    public void Score_ExtraVisibleTrueEdgeOutsideConcept_DoesNotLowerRecall()
    {
        // blade→phys_power(amplifies)는 visible-true지만 컨셉 밖 → amplifies 미노출 → recall 분모 제외.
        var proposal = new BuildConceptProposal(
            "p-conceptrecall",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark", "fact:vanguard-bulwark"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        var amplifies = result.FamilyScores.Single(score => score.Relation == BuildGrammarRelation.Amplifies);
        Assert.That(amplifies.Exposed, Is.False, "컨셉 밖 relation 은 노출 아님 → recall 게이트 제외.");
        Assert.That(result.RecallMin, Is.EqualTo(1.0d).Within(1e-9), "노출 컨셉 edge 전량 claim → recall 1.0 유지.");
    }

    [Test]
    public void Score_ProposalDeclaredAfterPayoff_IsNotValidConcept()
    {
        var proposal = new BuildConceptProposal(
            "p-backdated",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark", "fact:vanguard-bulwark"),
            },
            DeclaredAtDecisionIndex: 6,
            PayoffObservedAtDecisionIndex: 3);

        var result = BuildGrammarInferenceScorer.Score(Input(10, proposal));

        Assert.That(result.ValidConceptCount, Is.EqualTo(0), "payoff 후 선언 = 사후 합리화 → 무효.");
    }

    [Test]
    public void Score_ProposalBeyondProgressCutoff_IsNotValidConcept()
    {
        var proposal = new BuildConceptProposal(
            "p-late",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark", "fact:vanguard-bulwark"),
            },
            DeclaredAtDecisionIndex: 4,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.ValidConceptCount, Is.EqualTo(0), "진행도 30% cutoff 초과 선언 = 무효.");
    }

    [Test]
    public void Score_SingleEvidenceProposal_IsNotValidConcept()
    {
        var proposal = new BuildConceptProposal(
            "p-oneevidence",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark", "fact:blade-bleed"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.ValidConceptCount, Is.EqualTo(0), "distinct 증거 <2 = 무효(≥2 근거 인용 요구).");
    }

    [Test]
    public void Score_NullInput_Throws()
    {
        Assert.That(() => BuildGrammarInferenceScorer.Score(null), Throws.ArgumentNullException);
    }
}
