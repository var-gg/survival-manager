using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PlacementAttributionEvaluatorFastTests
{
    [Test]
    public void HandTrace_AttributesMaterialOutcomeToTypedTacticalBeat()
    {
        var baseline = Record("base", "pair", PlacementAttributionComparisonKind.ProfileTransition, true, "enemy", -0.2f);
        var candidate = Record("candidate", "pair", PlacementAttributionComparisonKind.ProfileTransition, false, "ally", 0.2f)
            with { Channels = Channels(FormationChannelIds.Flank) };

        var report = PlacementAttributionEvaluator.Evaluate("run", new[] { baseline, candidate });
        var pair = report.PairAttributions.Single();

        Assert.That(pair.Component, Is.EqualTo(PlacementAttributionEvaluator.ComponentTactical));
        Assert.That(pair.PlayerVisibleExplainable, Is.True);
        Assert.That(report.Components.PolicyNoisePairCount, Is.Zero);
        Assert.That(report.Components.PolicyNoiseShare, Is.Zero);
        Assert.That(pair.ChannelDeltas.Single(value => value.ChannelId == FormationChannelIds.Flank).EventCountDelta,
            Is.EqualTo(1));
    }

    [Test]
    public void SameRoleAdjacentSwap_PreservesFullFormationFeatureSnapshot()
    {
        var baseline = new[]
        {
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontBottom,
            DeploymentAnchorId.BackCenter,
        };
        var candidate = new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontBottom,
            DeploymentAnchorId.BackCenter,
        };
        var baselineFeatures = FormationFeatureClassifier.Classify(baseline);
        var candidateFeatures = FormationFeatureClassifier.Classify(candidate);

        Assert.That(candidateFeatures, Is.EqualTo(baselineFeatures),
            "the first two role slots represent same-role units on adjacent anchors in this witness");

        var report = PlacementAttributionEvaluator.Evaluate("run", new[]
        {
            Record("base", "semantic", PlacementAttributionComparisonKind.SemanticAdjacentSwap, true, "enemy", -0.2f)
                with { FormationFeatures = Snapshot(baselineFeatures), AnchorIdsByMemberIndex = baseline.Select(value => (int)value).ToArray() },
            Record("candidate", "semantic", PlacementAttributionComparisonKind.SemanticAdjacentSwap, false, "ally", 0.2f)
                with { FormationFeatures = Snapshot(candidateFeatures), AnchorIdsByMemberIndex = candidate.Select(value => (int)value).ToArray() },
        });

        Assert.That(report.PairAttributions.Single().SemanticFeaturesPreserved, Is.True);
        Assert.That(report.SemanticSwap.FeatureInvariantViolationCount, Is.Zero);
    }

    [Test]
    public void AnchorSweep_DetectsBuildIndependentDominanceAcrossEightCompositionsAndThreeFamilies()
    {
        var records = new List<PlacementAttributionBattleRecord>();
        for (var composition = 0; composition < 8; composition++)
        {
            for (var family = 0; family < 3; family++)
            {
                var pair = $"c{composition}|f{family}";
                records.Add(Record($"{pair}|used", pair, PlacementAttributionComparisonKind.AnchorSweep, true, "ally", 0.2f)
                    with
                    {
                        CompositionId = $"composition-{composition}",
                        EncounterFamilyId = $"family-{family}",
                        AnchorIdsByMemberIndex = new[] { 0, 1, 2, 3 },
                    });
                records.Add(Record($"{pair}|unused", pair, PlacementAttributionComparisonKind.AnchorSweep, false, "enemy", -0.2f)
                    with
                    {
                        CompositionId = $"composition-{composition}",
                        EncounterFamilyId = $"family-{family}",
                        AnchorIdsByMemberIndex = new[] { 1, 2, 3, 4 },
                    });
            }
        }

        var report = PlacementAttributionEvaluator.Evaluate("run", records);
        var anchor = report.AnchorDominance.Single(value => value.AnchorId == 0);

        Assert.That(anchor.BuildIndependentDominance, Is.True);
        Assert.That(anchor.EvaluableCompositionCount, Is.EqualTo(8));
        Assert.That(anchor.PositiveEncounterFamilyCount, Is.EqualTo(3));
        Assert.That(report.ProConditions.Single(value => value.ConditionId == "pro_3_build_independent_anchor_dominance").Triggered,
            Is.True);
    }

    [Test]
    public void Evaluation_SameInput_IsByteIdentical()
    {
        var records = new[]
        {
            Record("base", "pair", PlacementAttributionComparisonKind.ProfileTransition, true, "enemy", -0.2f),
            Record("candidate", "pair", PlacementAttributionComparisonKind.ProfileTransition, false, "ally", 0.2f)
                with { Trace = new PlacementTraceSummary(7, 0.75d, "ally-a->enemy-b", 2, 1, 4.5d, 0.1d) },
        };

        var first = HeadlessMetricJson.Serialize(PlacementAttributionEvaluator.Evaluate("deterministic-run", records));
        var second = HeadlessMetricJson.Serialize(PlacementAttributionEvaluator.Evaluate("deterministic-run", records.Reverse().ToArray()));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void StageFourEvaluators_RetainKnownCausalAndPlacementResults()
    {
        var baseline = FormationRecord("default", 1, true, "enemy", -0.1f, null);
        var secondBaseline = FormationRecord("default-2", 2, true, "ally", 0.1f, null);
        var candidate = FormationRecord("candidate", 1, false, "ally", 0.2f, FormationChannelIds.ScreenBlock);
        var secondCandidate = FormationRecord("candidate-2", 2, false, "ally", 0.2f, FormationChannelIds.ScreenBlock);
        var placement = PlacementLeverageEvaluator.Evaluate(new[] { baseline, secondBaseline, candidate, secondCandidate });
        var causal = FormationCausalEvaluator.Evaluate(new[]
        {
            baseline,
            candidate with { IsPolicyChoice = true },
        });

        Assert.That(placement.Records.Single().WinRateLeverage, Is.EqualTo(0.5d));
        Assert.That(causal.EventLogs.Single(value => value.BattleId == "candidate"
                                                          && value.ChannelId == FormationChannelIds.ScreenBlock).Causal,
            Is.True);
    }

    private static PlacementAttributionBattleRecord Record(
        string battleId,
        string pairingId,
        string comparisonKind,
        bool isBaseline,
        string winner,
        float power)
        => new()
        {
            RunId = "run",
            BattleId = battleId,
            PairingId = pairingId,
            ComparisonKind = comparisonKind,
            CompositionId = "composition",
            ConceptVariantId = "concept",
            EncounterFamilyId = "family",
            ScenarioId = "scenario",
            Seed = 1701,
            BattleSeed = 41701,
            PlacementVariantId = battleId,
            IsBaseline = isBaseline,
            SemanticPreservationExpected = comparisonKind == PlacementAttributionComparisonKind.SemanticAdjacentSwap,
            FormationProfileId = isBaseline ? "fortified_line" : "forward_spear",
            FormationFeatures = new PlacementAttributionBattleRecord.FormationFeatureSnapshot(3, 1, 1, 0, 1d, 2d, 0.5d),
            AnchorIdsByMemberIndex = new[] { 0, 1, 2, 4 },
            WinnerSide = winner,
            NormalizedFinalPowerDifference = power,
            FixedStepSeconds = 0.05f,
            Channels = Channels(null),
            Trace = PlacementTraceSummary.Empty,
        };

    private static IReadOnlyList<PlacementAttributionBattleRecord.ChannelTrace> Channels(string? firedChannel)
        => FormationChannelIds.All.Select(channelId =>
            new PlacementAttributionBattleRecord.ChannelTrace(
                channelId,
                string.Equals(channelId, firedChannel, StringComparison.Ordinal),
                string.Equals(channelId, firedChannel, StringComparison.Ordinal) ? 1 : 0)).ToArray();

    private static PlacementAttributionBattleRecord.FormationFeatureSnapshot Snapshot(FormationFeatures value)
        => new(
            value.FrontlineCount,
            value.ProtectedSlotCount,
            value.SideExposureCount,
            value.RearExposureCount,
            value.FlankRearExposureScore,
            value.SupportDistance,
            value.BacklineAccessibility);

    private static FormationBattleRecord FormationRecord(
        string battleId,
        int seed,
        bool isDefault,
        string winner,
        float power,
        string? firedChannel)
        => new()
        {
            RunId = "run",
            BattleId = battleId,
            PairingId = "pair",
            PlacementSetId = "set",
            PlacementVariantId = isDefault ? "default" : "candidate",
            PolicyId = "competent-formation-v1",
            Seed = seed,
            IsDefaultPlacement = isDefault,
            WinnerSide = winner,
            NormalizedFinalPowerDifference = power,
            Channels = FormationChannelIds.All.Select(channelId =>
            {
                var fired = string.Equals(channelId, firedChannel, StringComparison.Ordinal);
                return new FormationBattleRecord.ChannelEvidence(
                    channelId,
                    fired,
                    fired,
                    fired,
                    fired ? 1 : 0,
                    fired ? $"typed {channelId}=1" : string.Empty);
            }).ToArray(),
        };
}
