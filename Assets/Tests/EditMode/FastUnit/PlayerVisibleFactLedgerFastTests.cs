using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PlayerVisibleFactLedgerFastTests
{
    [Test]
    public void TimelinePoint_OrdersCampaignThenSiteThenDecision()
    {
        var earlierSiteWithLaterDecision = new PlayerVisibleTimelinePoint(0, 1, 9);
        var laterSiteWithEarlierDecision = new PlayerVisibleTimelinePoint(0, 2, 0);
        var laterCampaign = new PlayerVisibleTimelinePoint(1, 0, 0);

        Assert.That(earlierSiteWithLaterDecision.CompareTo(laterSiteWithEarlierDecision), Is.LessThan(0));
        Assert.That(laterSiteWithEarlierDecision.CompareTo(laterCampaign), Is.LessThan(0));
    }

    [Test]
    public void SameObservationContent_ProducesStableFactIdSetAcrossTimelinePoints()
    {
        var first = CreateObservationFacts(new PlayerVisibleTimelinePoint(0, 0, 0));
        var repeated = CreateObservationFacts(new PlayerVisibleTimelinePoint(0, 4, 9));

        Assert.That(
            repeated.Select(value => value.FactId),
            Is.EqualTo(first.Select(value => value.FactId)));
        Assert.That(
            repeated.Select(value => value.ContentHash),
            Is.EqualTo(first.Select(value => value.ContentHash)));
    }

    [Test]
    public void SameSeedLedger_WritesByteIdentically()
    {
        var firstRoot = PrepareDirectory("byte-identical-a");
        var secondRoot = PrepareDirectory("byte-identical-b");
        try
        {
            var facts = CreateObservationFacts(new PlayerVisibleTimelinePoint(0, 0, 0));
            var decision = CreateDecision(
                new PlayerVisibleTimelinePoint(0, 0, 0),
                facts.Select(value => value.FactId));

            var firstPath = PlayerVisibleFactLedgerArtifactWriter.Write(firstRoot, facts, new[] { decision });
            var secondPath = PlayerVisibleFactLedgerArtifactWriter.Write(secondRoot, facts, new[] { decision });

            Assert.That(File.ReadAllBytes(secondPath), Is.EqualTo(File.ReadAllBytes(firstPath)));
            Assert.That(File.ReadAllText(firstPath), Does.Contain("\"entry_kind\":\"fact\""));
            Assert.That(File.ReadAllText(firstPath), Does.Contain("\"entry_kind\":\"decision\""));
        }
        finally
        {
            DeleteDirectory(firstRoot);
            DeleteDirectory(secondRoot);
        }
    }

    [Test]
    public void DecisionEvidence_MustBeNonEmptyAndResolvableAtDecisionTime()
    {
        var facts = CreateObservationFacts(new PlayerVisibleTimelinePoint(0, 0, 0));
        var valid = CreateDecision(
            new PlayerVisibleTimelinePoint(0, 0, 0),
            facts.Select(value => value.FactId));

        Assert.DoesNotThrow(() => PlayerVisibleFactLedgerAuditor.ValidateDecision(facts, valid));
        var audit = PlayerVisibleFactLedgerAuditor.Audit(facts, new[] { valid });
        Assert.That(audit.PostDecisionInformationReferenceCount, Is.Zero);
        Assert.That(audit.UnsupportedCertainClaimCount, Is.Zero);
        Assert.That(audit.NonUiSemanticInternalFieldReferenceCount, Is.Zero);
        Assert.That(audit.OracleOrTruthLeakCount, Is.Zero);
        Assert.That(audit.ToBt2Observations(), Has.All.Matches<H100GateEvaluator.ExternalObservation>(
            observation => observation.Value == 0d));

        var empty = CreateDecision(new PlayerVisibleTimelinePoint(0, 0, 0), Array.Empty<string>());
        Assert.Throws<PlayerVisibleProvenanceException>(
            () => PlayerVisibleFactLedgerAuditor.ValidateDecision(facts, empty));

        var unknown = CreateDecision(new PlayerVisibleTimelinePoint(0, 0, 0), new[] { "fact-missing" });
        Assert.Throws<PlayerVisibleProvenanceException>(
            () => PlayerVisibleFactLedgerAuditor.ValidateDecision(facts, unknown));
    }

    [Test]
    public void PostDecisionReference_IsDetectedAndRejected()
    {
        var futureFacts = CreateObservationFacts(new PlayerVisibleTimelinePoint(0, 1, 2));
        var decision = CreateDecision(
            new PlayerVisibleTimelinePoint(0, 0, 1),
            new[] { futureFacts[0].FactId });

        var audit = PlayerVisibleFactLedgerAuditor.Audit(futureFacts, new[] { decision });

        Assert.That(audit.PostDecisionInformationReferenceCount, Is.EqualTo(1));
        Assert.That(audit.UnsupportedCertainClaimCount, Is.EqualTo(1));
        Assert.Throws<PlayerVisibleProvenanceException>(
            () => PlayerVisibleFactLedgerAuditor.ValidateDecision(futureFacts, decision));
    }

    private static IReadOnlyList<PlayerVisibleFactRecord> CreateObservationFacts(PlayerVisibleTimelinePoint observedAt)
        => new[]
        {
            PlayerVisibleFactRecord.Create(
                "run-seed-1701",
                "campaign-000000",
                observedAt,
                PlayerVisibleUiSource.RunSeedDisplay,
                "current_decision",
                "uses_seed",
                "1701",
                "seed fixed before decision",
                string.Empty,
                "run context",
                "decision seed 1701"),
            PlayerVisibleFactRecord.Create(
                "run-seed-1701",
                "campaign-000000",
                observedAt,
                PlayerVisibleUiSource.TownRoster,
                "expedition_roster",
                "shows_ordered_heroes",
                "hero-a|hero-b|hero-c|hero-d",
                "current expedition squad",
                "count=4",
                "town roster",
                "hero-a, hero-b, hero-c, hero-d"),
        }.OrderBy(value => value.FactId, StringComparer.Ordinal).ToArray();

    private static PlayerVisibleDecisionRecord CreateDecision(
        PlayerVisibleTimelinePoint decidedAt,
        IEnumerable<string> evidenceFactIds)
        => PlayerVisibleDecisionRecord.Create(
            "run-seed-1701",
            "campaign-000000",
            decidedAt,
            "greedy-v1",
            "deployment",
            "0:hero-a|1:hero-b|2:hero-c|3:hero-d",
            "visible roster order and anchors",
            1d,
            evidenceFactIds);

    private static string PrepareDirectory(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), "sm-player-visible-ledger-fast-tests", suffix);
        DeleteDirectory(path);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
