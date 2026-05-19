using System.Linq;
using NUnit.Framework;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// task-run-node-battle-payload-v1 — RunBattlePayload deterministic contract & fail-closed builder.
/// </summary>
[Category("FastUnit")]
public sealed class RunBattlePayloadFastTests
{
    [Test]
    public void TryBuild_FromValidInput_ProducesDeterministicPayload()
    {
        var input = CreateValidInput();

        Assert.That(RunBattlePayloadBuilder.TryBuild(input, out var first), Is.True);
        Assert.That(RunBattlePayloadBuilder.TryBuild(input, out var second), Is.True);

        // 같은 input → 같은 payload. record value equality는 IReadOnlyList<string>에 reference
        // equality를 적용하므로 fieldwise + sequence로 검증한다 (deterministic 의미 보존).
        Assert.That(first.RunId, Is.EqualTo(second.RunId));
        Assert.That(first.ChapterId, Is.EqualTo(second.ChapterId));
        Assert.That(first.SiteId, Is.EqualTo(second.SiteId));
        Assert.That(first.SiteNodeIndex, Is.EqualTo(second.SiteNodeIndex));
        Assert.That(first.EncounterId, Is.EqualTo(second.EncounterId));
        Assert.That(first.ExpeditionNodeId, Is.EqualTo(second.ExpeditionNodeId));
        Assert.That(first.SquadSnapshotId, Is.EqualTo(second.SquadSnapshotId));
        Assert.That(first.StageCandidatePathHash, Is.EqualTo(second.StageCandidatePathHash));
        Assert.That(first.NodeOverlayHash, Is.EqualTo(second.NodeOverlayHash));
        Assert.That(first.BattleContextHash, Is.EqualTo(second.BattleContextHash));
        Assert.That(first.RewardBiasPercent, Is.EqualTo(second.RewardBiasPercent));
        Assert.That(first.ThreatPressurePercent, Is.EqualTo(second.ThreatPressurePercent));
        Assert.That(first.AffinityBoostPercent, Is.EqualTo(second.AffinityBoostPercent));
        Assert.That(first.ResolvedModifierIds.SequenceEqual(second.ResolvedModifierIds), Is.True);

        Assert.That(first.HasAnyModifier, Is.True);
        Assert.That(first.IsValid, Is.True);
        Assert.That(first.BattleContextHash, Is.EqualTo(input.BattleContextHash));
        Assert.That(first.ResolvedModifierIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void TryBuild_FromMissingEncounter_FailsClosed()
    {
        var input = CreateValidInput() with { EncounterId = string.Empty };

        Assert.That(RunBattlePayloadBuilder.TryBuild(input, out var payload), Is.False);
        Assert.That(payload, Is.EqualTo(RunBattlePayload.Empty));
        Assert.That(payload.IsValid, Is.False);
    }

    [Test]
    public void TryBuild_FromMissingBattleContextHash_FailsClosed()
    {
        var input = CreateValidInput() with { BattleContextHash = "   " };

        Assert.That(RunBattlePayloadBuilder.TryBuild(input, out var payload), Is.False);
        Assert.That(payload.SiteNodeIndex, Is.EqualTo(-1));
    }

    [Test]
    public void DifferentSiteNodeIndex_DoesNotDriftUnrelatedFields()
    {
        var a = CreateValidInput();
        var b = a with { SiteNodeIndex = 2, BattleContextHash = "ctx_stage2" };

        Assert.That(RunBattlePayloadBuilder.TryBuild(a, out var payloadA), Is.True);
        Assert.That(RunBattlePayloadBuilder.TryBuild(b, out var payloadB), Is.True);

        Assert.That(payloadA.RunId, Is.EqualTo(payloadB.RunId));
        Assert.That(payloadA.ChapterId, Is.EqualTo(payloadB.ChapterId));
        Assert.That(payloadA.SiteId, Is.EqualTo(payloadB.SiteId));
        Assert.That(payloadA.EncounterId, Is.EqualTo(payloadB.EncounterId));
        Assert.That(payloadA.SquadSnapshotId, Is.EqualTo(payloadB.SquadSnapshotId));
        Assert.That(payloadA.RewardBiasPercent, Is.EqualTo(payloadB.RewardBiasPercent));
        Assert.That(payloadA.SiteNodeIndex, Is.Not.EqualTo(payloadB.SiteNodeIndex));
        Assert.That(payloadA.BattleContextHash, Is.Not.EqualTo(payloadB.BattleContextHash));
    }

    [Test]
    public void ResolvedModifierIds_NullOrBlank_AreFilteredOut()
    {
        var input = CreateValidInput() with
        {
            ResolvedModifierIds = new[] { "mod_a", string.Empty, " ", "mod_b" },
        };

        Assert.That(RunBattlePayloadBuilder.TryBuild(input, out var payload), Is.True);
        Assert.That(payload.ResolvedModifierIds.Count, Is.EqualTo(2));
        Assert.That(payload.ResolvedModifierIds[0], Is.EqualTo("mod_a"));
        Assert.That(payload.ResolvedModifierIds[1], Is.EqualTo("mod_b"));
    }

    private static RunBattlePayloadInput CreateValidInput()
    {
        return new RunBattlePayloadInput(
            RunId: "run_test_001",
            ChapterId: "chapter_alpha",
            SiteId: "site_alpha_gate",
            SiteNodeIndex: 0,
            EncounterId: "encounter_skirmish_01",
            ExpeditionNodeId: "node_0_skirmish",
            SquadSnapshotId: "squad_test_001",
            StageCandidatePathHash: "spch_aaa",
            NodeOverlayHash: "noh_bbb",
            BattleContextHash: "ctx_ccc",
            RewardBiasPercent: 15,
            ThreatPressurePercent: 5,
            AffinityBoostPercent: 10,
            ResolvedModifierIds: new[] { "mod_a", "mod_b" });
    }
}
