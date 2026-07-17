using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class IntentTrackEvaluatorFastTests
{
    private static readonly ConceptContract Contract = new(
        new[] { "owned:item:item_goal" },
        new[] { "acquire:item:item_goal" },
        "telemetry.damage_applied",
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        "core",
        new[] { "pivot:goal-unavailable" });

    [Test]
    public void Evaluate_HandBuiltOfferStream_DistinguishesAvailableAndUnavailableTrack()
    {
        var available = Evaluate(Window(0, GoalChoice()));
        var unavailable = Evaluate(Window(0, IntentTrackChoice.NoOp("miss")));

        Assert.That(available.TrackAvailable, Is.True);
        Assert.That(available.FirstProgressTime, Is.EqualTo(0));
        Assert.That(available.RealizationTime, Is.EqualTo(0));
        Assert.That(available.ChoicePath, Is.EqualTo(new[] { "goal" }));
        Assert.That(unavailable.TrackAvailable, Is.False);
        Assert.That(unavailable.RealizationTime, Is.EqualTo(-1));
    }

    [Test]
    public void Evaluate_ExactlyFourConsecutiveNoProgressWindows_IsStarvationBoundary()
    {
        var threeThenProgress = Evaluate(
            Window(0, IntentTrackChoice.NoOp("wait-0")),
            Window(1, IntentTrackChoice.NoOp("wait-1")),
            Window(2, IntentTrackChoice.NoOp("wait-2")),
            Window(3, GoalChoice()));
        var fourThenProgress = Evaluate(
            Window(0, IntentTrackChoice.NoOp("wait-0")),
            Window(1, IntentTrackChoice.NoOp("wait-1")),
            Window(2, IntentTrackChoice.NoOp("wait-2")),
            Window(3, IntentTrackChoice.NoOp("wait-3")),
            Window(4, GoalChoice()));

        Assert.That(threeThenProgress.TrackAvailable, Is.True);
        Assert.That(threeThenProgress.MaxAgencyDrought, Is.EqualTo(3));
        Assert.That(threeThenProgress.Starved, Is.False);
        Assert.That(fourThenProgress.TrackAvailable, Is.True);
        Assert.That(fourThenProgress.MaxAgencyDrought, Is.EqualTo(4));
        Assert.That(fourThenProgress.Starved, Is.True);
    }

    [Test]
    public void AnchorEvaluate_FirstVariantUnavailableSecondVariantAvailable_ReturnsAnchorAvailable()
    {
        var initial = IntentTrackState.Empty with
        {
            DeployedTagCounts = new[] { new IntentTrackTagCount("human", 1) },
        };
        var result = IntentTrackAnchorEvaluator.Evaluate(new IntentTrackAnchorSearchInput(
            "anchor_or_witness",
            new[]
            {
                new IntentTrackVariantSearchInput(
                    "variant-01-unavailable",
                    ContractWithIdentity("build.contains_tag:human", "owned:item:item_missing")),
                new IntentTrackVariantSearchInput(
                    "variant-02-available",
                    ContractWithIdentity("build.contains_tag:human", "owned:item:item_goal")),
            },
            initial,
            new[] { Window(0, GoalChoice()) },
            new[] { IntentTrackLeverId.Deployment, IntentTrackLeverId.Reward },
            0,
            1));

        Assert.That(result.TrackAvailable, Is.True);
        Assert.That(result.SelectedVariantId, Is.EqualTo("variant-02-available"));
        Assert.That(result.VariantResults.Single(value => value.VariantId == "variant-01-unavailable").AvailabilityKind,
            Is.EqualTo(IntentTrackVariantAvailabilityKind.TrueUnavailable));
        Assert.That(result.VariantResults.Single(value => value.VariantId == "variant-02-available").AvailabilityKind,
            Is.EqualTo(IntentTrackVariantAvailabilityKind.V1Track));
        Assert.That(result.PredicateCacheHitCount, Is.GreaterThan(0));
    }

    [Test]
    public void AnchorEvaluate_PassiveRequiredWithClosedLevelNodeLever_IsLeverPending()
    {
        var contract = ContractWithIdentity("owned:passive:passive_future") with
        {
            PivotConditions = new[] { "acquisition_path_unavailable:level_node" },
        };
        var result = IntentTrackAnchorEvaluator.Evaluate(new IntentTrackAnchorSearchInput(
            "anchor_pending_witness",
            new[] { new IntentTrackVariantSearchInput("variant-passive", contract) },
            IntentTrackState.Empty,
            Array.Empty<IntentTrackAgencyWindow>(),
            new[] { IntentTrackLeverId.Deployment, IntentTrackLeverId.Reward },
            0,
            0));

        var variant = result.VariantResults.Single();
        Assert.That(result.TrackAvailable, Is.False);
        Assert.That(result.LeverPendingVariantCount, Is.EqualTo(1));
        Assert.That(result.TrueUnavailableVariantCount, Is.Zero);
        Assert.That(variant.AvailabilityKind, Is.EqualTo(IntentTrackVariantAvailabilityKind.LeverPending));
        Assert.That(variant.PendingLeverIds, Is.EqualTo(new[] { IntentTrackLeverId.LevelNode }));
    }

    [Test]
    public void PredicateEvaluator_AllIdentityKindsAreExplicitAndUnknownKindThrows()
    {
        var state = IntentTrackState.Empty with
        {
            DeployedTagCounts = new[] { new IntentTrackTagCount("human", 4) },
            OwnedComponentIds = new[] { "item:item_goal" },
            ActiveEffectIds = new[] { "status:slow" },
            ActiveTeamRuleIds = new[] { "rule.phalanx" },
            Formation = ExposureFormation(),
        };
        var predicates = new[]
        {
            "build.count_tag(human)>=4",
            "build.contains_tag:human",
            "owned:item:item_goal",
            "effect.ready:status:slow",
            "build.team_rule=rule.phalanx",
            "formation.flank_rear_exposure_score>=4",
        };
        var results = predicates.Select(value => IntentTrackPredicateEvaluator.EvaluateIdentityPredicate(value, state)).ToArray();

        Assert.That(results.All(value => value.Satisfied), Is.True);
        Assert.That(results.Select(value => value.PredicateKind), Is.EquivalentTo(new[]
        {
            IntentTrackPredicateEvaluator.BuildTagCountKind,
            IntentTrackPredicateEvaluator.BuildTagPresenceKind,
            IntentTrackPredicateEvaluator.OwnedComponentKind,
            IntentTrackPredicateEvaluator.EffectReadyKind,
            IntentTrackPredicateEvaluator.TeamRuleKind,
            IntentTrackPredicateEvaluator.FormationKind,
        }));
        Assert.Throws<NotSupportedException>(() =>
            IntentTrackPredicateEvaluator.RequireSupportedIdentityPredicate("future.identity=true"));
    }

    [Test]
    public void Evaluate_IronLineVariantOne_ReportsAllThreePredicatesSatisfied()
    {
        var state = IntentTrackState.Empty with
        {
            DeployedTagCounts = new[] { new IntentTrackTagCount("human", 4) },
            ActiveTeamRuleIds = new[] { "rule.phalanx" },
            Formation = ExposureFormation(),
        };
        var result = IntentTrackEvaluator.Evaluate(new IntentTrackSearchInput(
            ContractWithIdentity(
                "build.count_tag(human)>=4",
                "build.team_rule=rule.phalanx",
                "formation.flank_rear_exposure_score>=4"),
            state,
            Array.Empty<IntentTrackAgencyWindow>(),
            new[] { IntentTrackLeverId.Deployment, IntentTrackLeverId.Reward },
            0,
            0));

        Assert.That(result.TrackAvailable, Is.True);
        Assert.That(result.IdentityPredicateResults.Select(value => value.Predicate), Is.EqualTo(new[]
        {
            "build.count_tag(human)>=4",
            "build.team_rule=rule.phalanx",
            "formation.flank_rear_exposure_score>=4",
        }));
        Assert.That(result.IdentityPredicateResults.All(value => value.Satisfied), Is.True);
    }

    [Test]
    public void Calculate_PolicyCaptureDenominator_ContainsOnlyTrackAvailableRuns()
    {
        var report = Calculate(new[]
        {
            Run("run-a", trackAvailable: true, policyRealized: true),
            Run("run-b", trackAvailable: true, policyRealized: false),
            Run("run-c", trackAvailable: false, policyRealized: true),
        });

        var anchor = report.OwnerAnchorSummaries.Single();
        Assert.That(anchor.CaptureDenominator, Is.EqualTo(2));
        Assert.That(anchor.PolicyRealizedCount, Is.EqualTo(1));
        Assert.That(anchor.PolicyCaptureRate, Is.EqualTo(0.5d));
    }

    [TestCase(false, false, false, false, false, IntentTrackGapKind.Agency)]
    [TestCase(false, true, false, false, false, IntentTrackGapKind.LeverPending)]
    [TestCase(true, false, false, true, false, IntentTrackGapKind.Surface)]
    [TestCase(true, false, false, false, false, IntentTrackGapKind.Policy)]
    [TestCase(true, false, true, false, false, IntentTrackGapKind.Combat)]
    [TestCase(true, false, true, true, true, IntentTrackGapKind.None)]
    public void GapClassifier_EmitsMutuallyExclusiveFailureKind(
        bool trackAvailable,
        bool leverPending,
        bool policyRealized,
        bool relevantSurfaceGap,
        bool payoffWitnessed,
        string expected)
    {
        Assert.That(
            IntentTrackGapClassifier.Classify(trackAvailable, leverPending, policyRealized, relevantSurfaceGap, payoffWitnessed),
            Is.EqualTo(expected));
    }

    [Test]
    public void OneSidedWilsonLowerBound_MatchesKnownTwelveOfSixteenValue()
    {
        Assert.That(
            IntentTrackMetricsCalculator.OneSidedWilsonLowerBound(12, 16),
            Is.EqualTo(0.5452365088315324d).Within(1e-15d));
    }

    [Test]
    public void Calculate_EquivalentInputOrders_SerializeByteIdentically()
    {
        var rows = new[]
        {
            Run("run-b", trackAvailable: true, policyRealized: false),
            Run("run-a", trackAvailable: true, policyRealized: true),
            Run("run-c", trackAvailable: false, policyRealized: false),
        };
        var first = Encoding.UTF8.GetBytes(HeadlessMetricJson.Serialize(Calculate(rows)));
        var second = Encoding.UTF8.GetBytes(HeadlessMetricJson.Serialize(Calculate(rows.Reverse())));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void HeadlessPoliciesAssembly_CannotReachIntentTrackEvaluator()
    {
        var asmdef = File.ReadAllText(Path.Combine(
            "Assets", "_Game", "Scripts", "Runtime", "HeadlessPolicies", "SM.HeadlessPolicies.asmdef"));
        Assert.That(asmdef, Does.Not.Contain("SM.HeadlessCensus"));
        foreach (var path in Directory.EnumerateFiles(
                     Path.Combine("Assets", "_Game", "Scripts", "Runtime", "HeadlessPolicies"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            Assert.That(File.ReadAllText(path), Does.Not.Contain("IntentTrackEvaluator"), path);
        }
    }

    private static IntentTrackSearchResult Evaluate(params IntentTrackAgencyWindow[] windows)
        => IntentTrackEvaluator.Evaluate(new IntentTrackSearchInput(
            Contract,
            IntentTrackState.Empty,
            windows,
            new[] { IntentTrackLeverId.Reward },
            0,
            windows.Length));

    private static IntentTrackAgencyWindow Window(int index, IntentTrackChoice choice)
        => new(index, IntentTrackLeverId.Reward, $"reward-{index}", index, new[] { choice });

    private static IntentTrackChoice GoalChoice()
        => new(
            "goal",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<IntentTrackRosterMember>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "item:item_goal" },
            0,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<string>(),
            Array.Empty<IntentTrackTagCount>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            new[] { "item:item_goal" },
            true);

    private static ConceptContract ContractWithIdentity(params string[] predicates)
        => Contract with
        {
            IdentityPredicates = predicates,
            ProgressMilestones = Array.Empty<string>(),
            PivotConditions = Array.Empty<string>(),
        };

    private static FormationFeatures ExposureFormation()
        => new(
            FrontlineCount: 2,
            ProtectedSlotCount: 0,
            SideExposureCount: 2,
            RearExposureCount: 1,
            FlankRearExposureScore: 5d,
            SupportDistance: 1d,
            BacklineAccessibility: 1d);

    private static IntentTrackRunRecord Run(string runId, bool trackAvailable, bool policyRealized)
        => new()
        {
            RunId = runId,
            ConceptId = "anchor_test",
            ConceptKind = "owner_anchor",
            AvailabilityTier = "core",
            RepresentativeVariantId = "variant-a",
            SelectedTrackVariantId = trackAvailable ? "variant-a" : string.Empty,
            AgencyWindowCount = 6,
            BattleCount = 4,
            TrackAvailable = trackAvailable,
            FirstProgressTime = trackAvailable ? 1 : -1,
            OracleRealizationTime = trackAvailable ? 2 : -1,
            MaxAgencyDrought = trackAvailable ? 1 : 4,
            Starved = !trackAvailable,
            PolicyCommitted = true,
            PolicyRealized = policyRealized,
            PolicyRealizationWindowIndex = policyRealized ? 2 : -1,
            RealizedBeforeFinalTwentyPercent = policyRealized,
            PayoffRunway = policyRealized ? 2 : 0,
            PayoffWitnessed = policyRealized,
            GapKind = policyRealized
                ? IntentTrackGapKind.None
                : trackAvailable ? IntentTrackGapKind.Policy : IntentTrackGapKind.Agency,
            VariantCount = 1,
            LeverPendingVariantCount = 0,
            TrueUnavailableVariantCount = trackAvailable ? 0 : 1,
            PredicateEvaluationCount = 1,
            PredicateCacheHitCount = 0,
            VariantResults = new[]
            {
                new IntentTrackVariantRunRecord(
                    "variant-a",
                    "core",
                    trackAvailable
                        ? IntentTrackVariantAvailabilityKind.V1Track
                        : IntentTrackVariantAvailabilityKind.TrueUnavailable,
                    Array.Empty<string>(),
                    trackAvailable,
                    trackAvailable ? 1 : -1,
                    trackAvailable ? 2 : -1,
                    trackAvailable ? 1 : 4,
                    !trackAvailable,
                    1,
                    trackAvailable ? 1 : 0,
                    Array.Empty<string>(),
                    new[]
                    {
                        new IntentTrackPredicateDiagnosticRecord(
                            "owned:item:item_goal",
                            IntentTrackPredicateEvaluator.OwnedComponentKind,
                            trackAvailable),
                    }),
            },
        };

    private static IntentTrackReport Calculate(IEnumerable<IntentTrackRunRecord> rows)
        => IntentTrackMetricsCalculator.Calculate(
            rows,
            seedBase: 1701,
            seedCount: 3,
            ownerAnchorCount: 1,
            systemMedoidCatalogCount: 0,
            systemMedoidSampleCount: 0,
            enabledLeverIds: new[] { IntentTrackLeverId.Deployment, IntentTrackLeverId.Reward },
            agencyWindowDefinition: "test",
            v1LeverCaveat: "test",
            rightSizeNote: "test",
            predicateCoverage: new IntentTrackPredicateCoverage(
                OwnerVariantCount: 1,
                SystemVariantCount: 0,
                UniqueIdentityPredicateCount: 1,
                PredicateKinds: new[] { IntentTrackPredicateEvaluator.OwnedComponentKind },
                UnevaluablePredicateCount: 0),
            evaluatorVersion: IntentTrackSearchResult.CurrentEvaluatorVersion);
}
