using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessCensus;

namespace SM.Tests.EditMode;

/// <summary>
/// BT4 빌드 문법 유추 채점기(<see cref="BuildGrammarInferenceScorer"/>)의 정직성 적대 검증.
/// 실 냉시작 LLM 토큰 투입 전, 채점기가 off-surface·미해결 증거·back-dated claim을 거부하고 well-formed
/// 환각은 precision FP로 보존하며, 유효 컨셉과 recall을 정직하게 스코프함을 결정적으로 증명한다.
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

    // 소형 world: blade→bleed(produces), blade→phys_power(amplifies)가 연결된 노출 컨셉.
    // vanguard→bulwark(pays_off)와 bleed→rupture(amplifies)는 visible-true지만 컨셉 밖이다.
    private static BuildGrammarTruthGraph World()
        => new(new[]
        {
            Edge("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed"),
            Edge("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark"),
            Edge("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power"),
            Edge("status", "bleed", BuildGrammarRelation.Amplifies, "combat_effect", "rupture"),
        });

    private static readonly string[] Visible =
    {
        "skill:blade", "status:bleed", "tag:vanguard", "team_rule:bulwark", "stat:phys_power",
        "combat_effect:rupture",
    };

    private static readonly string[] KnownFacts =
    {
        "fact:blade-bleed", "fact:vanguard-bulwark", "fact:blade-power", "fact:bleed-rupture", "fact:extra",
    };

    private static readonly string[] ConceptEdges =
    {
        Key("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed"),
        Key("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power"),
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
        => Input(cutoff, BuildGrammarInferenceCaptureSource.SyntheticStandIn, proposals);

    private static BuildGrammarInferenceInput Input(
        int cutoff,
        BuildGrammarInferenceCaptureSource captureSource,
        params BuildConceptProposal[] proposals)
        => new(proposals, World(), Visible, KnownFacts, ConceptEdges, cutoff, captureSource);

    [Test]
    public void Score_ExactConceptClaimsBeforePayoff_PerfectPrecisionRecallAndValidConcept()
    {
        var proposal = new BuildConceptProposal(
            "p-exact",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power", "fact:blade-power"),
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
    public void Score_WellFormedHallucinatedEdge_IsPrecisionFalsePositiveNotHardRejected()
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
        Assert.That(result.AdmissibleClaimCount, Is.EqualTo(2), "well-formed 환각도 precision 표본에서 보존해야 한다.");
        var produces = result.FamilyScores.Single(score => score.Relation == BuildGrammarRelation.Produces);
        Assert.That(produces.ClaimedCount, Is.EqualTo(2));
        Assert.That(produces.CorrectCount, Is.EqualTo(1));
        Assert.That(produces.Precision, Is.EqualTo(0.5d).Within(1e-9));
        Assert.That(result.PrecisionMin, Is.LessThanOrEqualTo(0.5d));
    }

    [Test]
    public void Score_ExtraVisibleTrueEdgeOutsideConcept_DoesNotLowerRecall()
    {
        // vanguard→bulwark(pays_off)는 visible-true지만 컨셉 밖 → pays_off 미노출 → recall 분모 제외.
        var proposal = new BuildConceptProposal(
            "p-conceptrecall",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power", "fact:blade-power"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        var paysOff = result.FamilyScores.Single(score => score.Relation == BuildGrammarRelation.PaysOff);
        Assert.That(paysOff.Exposed, Is.False, "컨셉 밖 relation 은 노출 아님 → recall 게이트 제외.");
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
                Claim("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power", "fact:blade-power"),
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
                Claim("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power", "fact:blade-power"),
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
                Claim("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power", "fact:blade-bleed"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.ValidConceptCount, Is.EqualTo(0), "distinct 증거 <2 = 무효(≥2 근거 인용 요구).");
    }

    [Test]
    public void Score_ConnectedMultiKindClaimsWithinOneSystemFamily_AreNotValidConcept()
    {
        // skill→status→combat_effect는 kind가 셋이어도 모두 StatusCombat family다.
        var proposal = new BuildConceptProposal(
            "p-one-family",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("status", "bleed", BuildGrammarRelation.Amplifies, "combat_effect", "rupture", "fact:bleed-rupture"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.GuardViolationCount, Is.EqualTo(0));
        Assert.That(result.ValidConceptCount, Is.EqualTo(0), "ontology kind 수로 gameplay system 수를 부풀리면 안 된다.");
    }

    [Test]
    public void Score_DisconnectedTruthMatchedEdges_AreNotValidConcept()
    {
        var proposal = new BuildConceptProposal(
            "p-disconnected",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("tag", "vanguard", BuildGrammarRelation.PaysOff, "team_rule", "bulwark", "fact:vanguard-bulwark"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.GuardViolationCount, Is.EqualTo(0));
        Assert.That(result.ValidConceptCount, Is.EqualTo(0), "공유 endpoint 없는 정답 edge를 한 컨셉으로 합치면 안 된다.");
    }

    [Test]
    public void Score_HallucinatedClaimEvidence_DoesNotQualifyMatchedConceptEvidence()
    {
        var proposal = new BuildConceptProposal(
            "p-unrelated-evidence",
            new[]
            {
                Claim("skill", "blade", BuildGrammarRelation.Produces, "status", "bleed", "fact:blade-bleed"),
                Claim("skill", "blade", BuildGrammarRelation.Amplifies, "stat", "phys_power", "fact:blade-bleed"),
                Claim("status", "bleed", BuildGrammarRelation.Produces, "team_rule", "bulwark", "fact:extra"),
            },
            DeclaredAtDecisionIndex: 0,
            PayoffObservedAtDecisionIndex: -1);

        var result = BuildGrammarInferenceScorer.Score(Input(2, proposal));

        Assert.That(result.GuardViolationCount, Is.EqualTo(0));
        Assert.That(result.ValidConceptCount, Is.EqualTo(0), "환각 claim의 fact로 matched graph 증거 문턱을 채우면 안 된다.");
    }

    [TestCase(BuildGrammarInferenceCaptureSource.SyntheticStandIn, false)]
    [TestCase(BuildGrammarInferenceCaptureSource.LiveColdStartLlm, true)]
    public void Score_CaptureSource_DerivesCertificationEligibility(
        BuildGrammarInferenceCaptureSource captureSource,
        bool expectedEligible)
    {
        var result = BuildGrammarInferenceScorer.Score(Input(2, captureSource));

        Assert.That(result.CaptureSource, Is.EqualTo(captureSource));
        Assert.That(result.CertificationEligible, Is.EqualTo(expectedEligible));
    }

    [Test]
    public void Score_NullInput_Throws()
    {
        Assert.That(() => BuildGrammarInferenceScorer.Score(null), Throws.ArgumentNullException);
    }
}
