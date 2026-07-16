using System;
using System.IO;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class IntentTraceArtifactWriterFastTests
{
    [Test]
    public void SameTrace_WritesByteIdenticalJsonlInTimelineOrder()
    {
        var firstRoot = PrepareDirectory("intent-trace-writer-a");
        var secondRoot = PrepareDirectory("intent-trace-writer-b");
        try
        {
            var records = new[]
            {
                CreateRecord(1, "keep", false),
                CreateRecord(0, "advance", true),
            };
            var firstPath = IntentTraceArtifactWriter.Write(firstRoot, records);
            var secondPath = IntentTraceArtifactWriter.Write(secondRoot, records);

            Assert.That(File.ReadAllBytes(secondPath), Is.EqualTo(File.ReadAllBytes(firstPath)));
            var lines = File.ReadAllLines(firstPath);
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Does.Contain("\"decision_index\":0"));
            Assert.That(lines[0], Does.Contain("\"is_commit\":true"));
            Assert.That(lines[1], Does.Contain("\"reason\":\"keep\""));
        }
        finally
        {
            Directory.Delete(firstRoot, true);
            Directory.Delete(secondRoot, true);
        }
    }

    private static IntentTraceRecord CreateRecord(int decisionIndex, string reason, bool isCommit)
        => IntentTraceRecord.Create(
            "intent-run",
            "campaign-000000",
            new PlayerVisibleTimelinePoint(0, 0, decisionIndex),
            "concept-commit-v1",
            "coverage",
            decisionIndex == 0 ? "deployment" : "reward",
            decisionIndex == 0 ? "0:hero-1" : "option:0",
            reason,
            milestoneAdvanced: isCommit,
            scarceResourceInvested: false,
            isCommit: isCommit,
            new IntentCommitConditionSnapshot(true, true, true, isCommit, true, true, isCommit),
            new IntentHypothesisSnapshot(
                "identity->payoff",
                new[] { new PlayerVisibleEvidenceRef("fact-a"), new PlayerVisibleEvidenceRef("fact-b") },
                0.7d,
                "verify:next",
                "beat.payoff",
                new[] { "acquire:next" },
                "track_unavailable",
                decisionIndex,
                -1),
            new IntentStateSnapshot(
                "coverage-intent",
                "coverage",
                decisionIndex + 1,
                0,
                isCommit ? decisionIndex : 0,
                "active",
                0,
                1,
                isCommit ? new[] { "milestone-1" } : new[] { "milestone-1" }));

    private static string PrepareDirectory(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "sm-intent-trace-writer-fast-tests", name);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);
        return path;
    }
}
