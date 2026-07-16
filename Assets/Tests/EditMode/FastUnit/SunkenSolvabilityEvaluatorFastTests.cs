using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SunkenSolvabilityEvaluatorFastTests
{
    [Test]
    public void Evaluate_HighOracleAndTwentyPointRegret_ClassifiesPolicyProblem()
    {
        var snapshots = Snapshots(4);
        var candidates = new List<SunkenOracleCandidateRecord>();
        foreach (var snapshot in snapshots)
        {
            candidates.Add(Candidate(snapshot.SampleId, "build-a", "placement-a", won: true));
            candidates.Add(Candidate(snapshot.SampleId, "build-b", "placement-b", won: true));
            candidates.Add(Candidate(snapshot.SampleId, "chosen", "chosen", won: false, chosen: true));
        }

        var report = Evaluate(snapshots, candidates);

        Assert.That(report.SameStateOracleWinRate, Is.EqualTo(1d));
        Assert.That(report.SelectionRegret, Is.EqualTo(1d));
        Assert.That(report.DecisionCell, Is.EqualTo(SunkenSolvabilityEvaluator.PolicyProblemCell));
    }

    [Test]
    public void Evaluate_LowSameStateAndHighLookback_ClassifiesHorizonProblem()
    {
        var snapshots = Snapshots(4);
        var candidates = new List<SunkenOracleCandidateRecord>();
        foreach (var snapshot in snapshots)
        {
            candidates.Add(Candidate(snapshot.SampleId, "same", "p", won: false));
            candidates.Add(Candidate(
                snapshot.SampleId,
                "lookback",
                "p",
                won: true,
                scope: SunkenOracleCandidateRecord.LookbackScope,
                added: "priest"));
        }

        var report = Evaluate(snapshots, candidates);

        Assert.That(report.SameStateOracleWinRate, Is.Zero);
        Assert.That(report.OneSiteLookbackOracle, Is.EqualTo(1d));
        Assert.That(report.AvailabilityGap, Is.EqualTo(1d));
        Assert.That(report.DecisionCell, Is.EqualTo(SunkenSolvabilityEvaluator.HorizonProblemCell));
    }

    [Test]
    public void Evaluate_LowSameStateAndLowLookback_ClassifiesEncounterWall()
    {
        var snapshots = Snapshots(4);
        var candidates = new List<SunkenOracleCandidateRecord>();
        for (var index = 0; index < snapshots.Count; index++)
        {
            candidates.Add(Candidate(snapshots[index].SampleId, "same", "p", won: false));
            candidates.Add(Candidate(
                snapshots[index].SampleId,
                "lookback",
                "p",
                won: index == 0,
                scope: SunkenOracleCandidateRecord.LookbackScope));
        }

        var report = Evaluate(snapshots, candidates);

        Assert.That(report.OneSiteLookbackOracle, Is.EqualTo(0.25d));
        Assert.That(report.DecisionCell, Is.EqualTo(SunkenSolvabilityEvaluator.EncounterWallCell));
    }

    [Test]
    public void Evaluate_SixtyPercentSameState_ClassifiesMixedCell()
    {
        var snapshots = Snapshots(5);
        var candidates = new List<SunkenOracleCandidateRecord>();
        for (var index = 0; index < snapshots.Count; index++)
        {
            candidates.Add(Candidate(snapshots[index].SampleId, "same", "p", won: index < 3));
            candidates.Add(Candidate(snapshots[index].SampleId, "chosen", "chosen", won: index < 3, chosen: true));
        }

        var report = Evaluate(snapshots, candidates);

        Assert.That(report.SameStateOracleWinRate, Is.EqualTo(0.6d).Within(0.000001d));
        Assert.That(report.DecisionCell, Is.EqualTo(SunkenSolvabilityEvaluator.MixedCell));
    }

    [Test]
    public void Evaluate_OneMissedWinningBuildAndPlacement_ClassifiesPuzzleLock()
    {
        var snapshots = Snapshots(4);
        var candidates = new List<SunkenOracleCandidateRecord>();
        foreach (var snapshot in snapshots)
        {
            candidates.Add(Candidate(snapshot.SampleId, "hidden-counter", "only-placement", won: true));
            candidates.Add(Candidate(snapshot.SampleId, "chosen", "chosen", won: false, chosen: true));
        }

        var report = Evaluate(snapshots, candidates);

        Assert.That(report.PuzzleLockSignal, Is.True);
        Assert.That(report.WinningBuildCount, Is.EqualTo(1));
        Assert.That(report.DecisionCell, Is.EqualTo(SunkenSolvabilityEvaluator.PuzzleLockCell));
    }

    [Test]
    public void ArtifactWriter_IsByteDeterministic_AndEmitsAllThreeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm-sunken-diagnosis-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var snapshots = Snapshots(1);
            var candidates = new[] { Candidate(snapshots[0].SampleId, "build", "placement", won: true) };
            var report = Evaluate(snapshots, candidates);
            var first = SunkenSolvabilityArtifactWriter.Write(root, snapshots, candidates, report);
            var firstSnapshotBytes = File.ReadAllBytes(first.ArrivalSnapshotsPath);
            var firstCandidateBytes = File.ReadAllBytes(first.OracleCandidatesPath);
            var firstReportBytes = File.ReadAllBytes(first.DiagnosisReportPath);

            var second = SunkenSolvabilityArtifactWriter.Write(root, snapshots, candidates, report);

            Assert.That(File.ReadAllBytes(second.ArrivalSnapshotsPath), Is.EqualTo(firstSnapshotBytes));
            Assert.That(File.ReadAllBytes(second.OracleCandidatesPath), Is.EqualTo(firstCandidateBytes));
            Assert.That(File.ReadAllBytes(second.DiagnosisReportPath), Is.EqualTo(firstReportBytes));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static SunkenSolvabilityReport Evaluate(
        IReadOnlyList<SunkenArrivalSnapshotRecord> snapshots,
        IReadOnlyList<SunkenOracleCandidateRecord> candidates)
        => SunkenSolvabilityEvaluator.Evaluate("run", "site_sunken_bastion", snapshots, candidates, "test");

    private static IReadOnlyList<SunkenArrivalSnapshotRecord> Snapshots(int count)
    {
        var result = new List<SunkenArrivalSnapshotRecord>(count);
        for (var index = 0; index < count; index++)
        {
            result.Add(new SunkenArrivalSnapshotRecord
            {
                SampleId = $"sample-{index:D2}",
                PolicyId = "policy",
            });
        }

        return result;
    }

    private static SunkenOracleCandidateRecord Candidate(
        string sampleId,
        string build,
        string placement,
        bool won,
        bool chosen = false,
        string scope = SunkenOracleCandidateRecord.SameStateScope,
        string added = "")
        => new()
        {
            SampleId = sampleId,
            Scope = scope,
            CandidateId = $"{build}|{placement}",
            BuildId = build,
            PlacementId = placement,
            CounterFamilyId = "mixed",
            SiteCompleted = won,
            BattleWinRate = won ? 1d : 0d,
            FinalTeamHpFraction = won ? 0.5d : 0d,
            IsPolicyChoice = chosen,
            AddedRosterArchetypeId = added,
        };
}
