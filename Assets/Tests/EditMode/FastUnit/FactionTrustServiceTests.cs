using System.Linq;
using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class FactionTrustServiceTests
{
    [Test]
    public void ComputeDeltas_Empty_WhenNoIssuer()
    {
        // issuer 없는 warrant(build축/미서약) → 정치 변화 없음.
        Assert.That(FactionTrustService.ComputeDeltas(WarrantOutcome.Kept, "", "faction_b"), Is.Empty);
    }

    [Test]
    public void ComputeDeltas_NotApplicable_NoChange()
    {
        Assert.That(FactionTrustService.ComputeDeltas(WarrantOutcome.NotApplicable, "faction_a", "faction_b"), Is.Empty);
    }

    [Test]
    public void ComputeDeltas_Satisfied_IssuerGain_OpposedLoss()
    {
        var d = FactionTrustService.ComputeDeltas(WarrantOutcome.Kept, "faction_a", "faction_b");
        Assert.That(d, Has.Count.EqualTo(2));
        Assert.That(d.First(x => x.FactionId == "faction_a").Delta, Is.EqualTo(FactionTrustService.SatisfiedIssuerGain));
        Assert.That(d.First(x => x.FactionId == "faction_b").Delta, Is.EqualTo(-FactionTrustService.SatisfiedOpposedLoss));
    }

    [Test]
    public void ComputeDeltas_Satisfied_NoOpposed_OnlyIssuerGain()
    {
        var d = FactionTrustService.ComputeDeltas(WarrantOutcome.Kept, "faction_a", "");
        Assert.That(d, Has.Count.EqualTo(1));
        Assert.That(d[0].FactionId, Is.EqualTo("faction_a"));
        Assert.That(d[0].Delta, Is.EqualTo(FactionTrustService.SatisfiedIssuerGain));
    }

    [Test]
    public void ComputeDeltas_Failed_IssuerLossOnly()
    {
        // 위반/패배는 issuer 신뢰만 깎는다(opposed는 변화 없음 — 약속을 받은 쪽이 실망).
        foreach (var outcome in new[] { WarrantOutcome.Broken, WarrantOutcome.FailedMission })
        {
            var d = FactionTrustService.ComputeDeltas(outcome, "faction_a", "faction_b");
            Assert.That(d, Has.Count.EqualTo(1), outcome.ToString());
            Assert.That(d[0].FactionId, Is.EqualTo("faction_a"));
            Assert.That(d[0].Delta, Is.EqualTo(-FactionTrustService.FailedIssuerLoss));
        }
    }
}
