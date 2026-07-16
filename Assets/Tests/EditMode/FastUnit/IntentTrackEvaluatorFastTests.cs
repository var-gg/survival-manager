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

    [TestCase(false, false, false, false, IntentTrackGapKind.Agency)]
    [TestCase(true, false, true, false, IntentTrackGapKind.Surface)]
    [TestCase(true, false, false, false, IntentTrackGapKind.Policy)]
    [TestCase(true, true, false, false, IntentTrackGapKind.Combat)]
    [TestCase(true, true, true, true, IntentTrackGapKind.None)]
    public void GapClassifier_EmitsMutuallyExclusiveFailureKind(
        bool trackAvailable,
        bool policyRealized,
        bool relevantSurfaceGap,
        bool payoffWitnessed,
        string expected)
    {
        Assert.That(
            IntentTrackGapClassifier.Classify(trackAvailable, policyRealized, relevantSurfaceGap, payoffWitnessed),
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
            Array.Empty<string>(),
            Array.Empty<IntentTrackTagCount>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            new[] { "item:item_goal" },
            true);

    private static IntentTrackRunRecord Run(string runId, bool trackAvailable, bool policyRealized)
        => new()
        {
            RunId = runId,
            ConceptId = "anchor_test",
            ConceptKind = "owner_anchor",
            AvailabilityTier = "core",
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
            evaluatorVersion: IntentTrackSearchResult.CurrentEvaluatorVersion);
}
