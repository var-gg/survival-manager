using System;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessCensus;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class OptionTrapOracleFastTests
{
    [Test]
    public void EligibleNoOp_IsAutomaticConfirmGradeAndConfirmedOnlyWithFullEvidence()
    {
        var contract = Contract("affix:no-op", potentialUniqueUnlockCount: 0);
        var report = Evaluate(
            contract,
            new[] { Witness(contract, fired: 0, changed: false, fullCensus: true) },
            DominatedPairs(contract.OptionId, fullCensus: true),
            new[] { Continuation(contract.OptionId, uniqueAdvantage: false) });

        var evidence = report.Evidence.Single();
        Assert.That(evidence.MechanicalDefectCodes, Does.Contain("eligible_no_effect"));
        Assert.That(evidence.AutomaticConfirmGrade, Is.True);
        Assert.That(evidence.ConfirmedTrap, Is.True,
            "자동 확정급 결함도 full pair·positive witness 부재·continuation 부재를 모두 충족할 때만 confirmed다");
    }

    [Test]
    public void OppositeDeltaSign_IsDetectedAsMechanicalDefect()
    {
        var contract = Contract("affix:reversed", potentialUniqueUnlockCount: 1);
        var witness = Witness(contract, fired: 1, changed: true, fullCensus: true) with
        {
            ActualDeltaDirection = OptionDeltaDirection.Negative,
            PositiveWitness = false,
        };

        var evidence = Evaluate(contract, new[] { witness }).Evidence.Single();

        Assert.That(evidence.MechanicalDefectCodes, Does.Contain("sign_reversal"));
        Assert.That(evidence.AutomaticConfirmGrade, Is.True);
        Assert.That(evidence.ConfirmedTrap, Is.False, "고유 unlock과 paired/continuation 부재를 무시해 확정하면 안 된다");
    }

    [Test]
    public void WeaknessOutsideIntendedContext_IsNotATrap()
    {
        var contract = Contract("item:status-amplifier", potentialUniqueUnlockCount: 0);
        var outside = DominatedPairs(contract.OptionId, fullCensus: false)
            .Select(value => value with { IntendedContext = false })
            .ToArray();

        var evidence = Evaluate(contract, pairs: outside).Evidence.Single();

        Assert.That(evidence.IntendedPairCount, Is.Zero);
        Assert.That(evidence.ConfirmedTrap, Is.False);
        Assert.That(evidence.OwnerVerdictRequired, Is.False);
    }

    [Test]
    public void VisibleHighRiskTradeoff_IsNotATrap()
    {
        var contract = Contract("augment:glass-cannon", potentialUniqueUnlockCount: 0, hasTradeoff: true);
        var evidence = Evaluate(
            contract,
            pairs: DominatedPairs(contract.OptionId, fullCensus: true),
            continuations: new[] { Continuation(contract.OptionId, uniqueAdvantage: false) }).Evidence.Single();

        Assert.That(evidence.HasVisibleTradeoff, Is.True);
        Assert.That(evidence.ConfirmedTrap, Is.False);
        Assert.That(evidence.CandidateStatus, Is.EqualTo("screened_healthy"));
    }

    [Test]
    public void ContinuationAdvantage_RescuesImmediateEnablerWeakness()
    {
        var contract = Contract("passive:enabler", potentialUniqueUnlockCount: 0);
        var evidence = Evaluate(
            contract,
            pairs: DominatedPairs(contract.OptionId, fullCensus: true),
            continuations: new[] { Continuation(contract.OptionId, uniqueAdvantage: true) }).Evidence.Single();

        Assert.That(evidence.RescuedEnabler, Is.True);
        Assert.That(evidence.ContinuationUniqueAdvantage, Is.True);
        Assert.That(evidence.ConfirmedTrap, Is.False);
        Assert.That(evidence.CandidateStatus, Is.EqualTo("rescued_enabler"));
    }

    [Test]
    public void DominantMirror_RaisesOwnerReviewTrigger()
    {
        var contract = Contract("skill:dominant", potentialUniqueUnlockCount: 1);
        var pairs = Enumerable.Range(0, 20).Select(index => new OptionPairedCounterfactual(
            contract.OptionId,
            "skill:sibling",
            $"intended-{index:D2}",
            index,
            "medoid-00",
            IntendedContext: true,
            FullCensus: false,
            ExplicitTradeoffVisible: false,
            OptionOutcome: Strong,
            ComparatorOutcome: Weak,
            OptionReplayHash: $"option-{index:D2}",
            ComparatorReplayHash: $"comparator-{index:D2}"))
            .ToArray();

        var evidence = Evaluate(contract, pairs: pairs).Evidence.Single();

        Assert.That(evidence.BugGradeDominant, Is.True);
        Assert.That(evidence.OptionNonWorseRate, Is.EqualTo(1d));
        Assert.That(evidence.OptionStrictlyBetterRate, Is.EqualTo(1d));
        Assert.That(evidence.MedianPairedWinUplift, Is.EqualTo(1d));
        Assert.That(evidence.OwnerVerdictRequired, Is.True);
    }

    [Test]
    public void RepeatedEvaluationAndSerialization_AreByteIdentical()
    {
        var contract = Contract("affix:stable", potentialUniqueUnlockCount: 0);
        var input = Input(
            contract,
            new[] { Witness(contract, fired: 1, changed: true, fullCensus: false) },
            DominatedPairs(contract.OptionId, fullCensus: false),
            new[] { Continuation(contract.OptionId, uniqueAdvantage: false) });

        var first = OptionTrapArtifactWriter.Serialize(OptionTrapOracleEvaluator.Evaluate(input));
        var second = OptionTrapArtifactWriter.Serialize(OptionTrapOracleEvaluator.Evaluate(input));

        Assert.That(second, Is.EqualTo(first));
        Assert.That(OptionTrapOracleEvaluator.Evaluate(input).ReproductionHash,
            Is.EqualTo(OptionTrapOracleEvaluator.Evaluate(input).ReproductionHash));
    }

    private static readonly OptionOutcomeVector Weak = new(0d, 0.25d, 0.25d, 0d, 0d, 0d);
    private static readonly OptionOutcomeVector Strong = new(1d, 0.75d, 0.75d, 1d, 1d, 0d);

    private static OptionWitnessContract Contract(
        string optionId,
        int potentialUniqueUnlockCount,
        bool hasTradeoff = false)
    {
        var separator = optionId.IndexOf(':');
        var kind = optionId.Substring(0, separator);
        var id = optionId.Substring(separator + 1);
        return new OptionWitnessContract(
            optionId,
            kind,
            id,
            "not_selected",
            $"group:{kind}",
            "same-cost",
            new[] { $"{kind}:sibling" },
            new[] { "tag:intended" },
            Array.Empty<string>(),
            new[] { "reward" },
            new[]
            {
                new OptionWitnessPromise(
                    "promise-0",
                    BuildGrammarRelation.Amplifies,
                    "stat",
                    "phys_power",
                    "stat=phys_power;operation=Flat;value=1;tag=",
                    "telemetry.damage_applied",
                    OptionDeltaDirection.Positive,
                    1d),
            },
            potentialUniqueUnlockCount,
            hasTradeoff,
            PromiseCoverageComplete: true,
            ComparatorCoverageComplete: true,
            StructuralTrapCandidate: false,
            StructuralDominanceCandidate: false);
    }

    private static OptionMechanicalWitness Witness(
        OptionWitnessContract contract,
        int fired,
        bool changed,
        bool fullCensus)
        => new(
            contract.OptionId,
            contract.Promises[0].PromiseId,
            "intended-probe",
            Eligible: true,
            FiredCount: fired,
            StateChanged: changed,
            ActualDeltaDirection: changed ? OptionDeltaDirection.Positive : OptionDeltaDirection.Zero,
            StackRuleMatches: true,
            TargetRuleMatches: true,
            PrerequisiteReachable: true,
            CostConsumed: false,
            StateHashBefore: "before",
            StateHashAfter: changed ? "after" : "before",
            FullCensus: fullCensus,
            PositiveWitness: changed,
            Note: "hand-built witness");

    private static OptionPairedCounterfactual[] DominatedPairs(string optionId, bool fullCensus)
        => Enumerable.Range(0, 20).Select(index => new OptionPairedCounterfactual(
            optionId,
            "same-cost-sibling",
            $"intended-{index:D2}",
            index,
            $"placement-{index % 4:D2}",
            IntendedContext: true,
            FullCensus: fullCensus,
            ExplicitTradeoffVisible: false,
            OptionOutcome: Weak,
            ComparatorOutcome: Strong,
            OptionReplayHash: $"option-{index:D2}",
            ComparatorReplayHash: $"comparator-{index:D2}"))
            .ToArray();

    private static OptionContinuationComparison Continuation(string optionId, bool uniqueAdvantage)
        => new(
            optionId,
            "continuation-0",
            Measured: true,
            WithOptionTrackAvailable: uniqueAdvantage,
            WithoutOptionTrackAvailable: false,
            WithOptionBestScore: uniqueAdvantage ? 2d : 0d,
            WithoutOptionBestScore: 0d,
            UniqueOptionAdvantage: uniqueAdvantage,
            WithOptionChoicePath: uniqueAdvantage ? new[] { "option", "payoff" } : Array.Empty<string>(),
            WithoutOptionChoicePath: Array.Empty<string>());

    private static OptionTrapReport Evaluate(
        OptionWitnessContract contract,
        OptionMechanicalWitness[]? witnesses = null,
        OptionPairedCounterfactual[]? pairs = null,
        OptionContinuationComparison[]? continuations = null)
        => OptionTrapOracleEvaluator.Evaluate(Input(contract, witnesses, pairs, continuations));

    private static OptionTrapOracleInput Input(
        OptionWitnessContract contract,
        OptionMechanicalWitness[]? witnesses,
        OptionPairedCounterfactual[]? pairs,
        OptionContinuationComparison[]? continuations)
        => new(
            new[] { contract },
            witnesses ?? Array.Empty<OptionMechanicalWitness>(),
            pairs ?? Array.Empty<OptionPairedCounterfactual>(),
            continuations ?? Array.Empty<OptionContinuationComparison>(),
            new OptionTrapSamplingPlan(
                1708,
                4,
                8,
                12,
                360,
                "fixed-seed ordinal sample",
                "contract predicates only",
                "flagged candidates only"));
}
