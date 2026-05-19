using NUnit.Framework;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// task-reward-settlement-commit-v1 acceptance #1: pending reward의 RewardCommitId가
/// deterministic하게 생성되고 (same input → same id), 다른 입력에서 다른 id를 만들어
/// commit-once + reload idempotence의 dedup key로 작동한다.
/// </summary>
[Category("FastUnit")]
public sealed class RewardCommitIdServiceFastTests
{
    private const string AnyHash = "deadbeef1234567890abcdef1234567890abcdef1234567890abcdef12345678";

    [Test]
    public void Compute_SameInput_ProducesIdenticalId()
    {
        var first = RewardCommitIdService.Compute(AnyHash, "victory");
        var second = RewardCommitIdService.Compute(AnyHash, "victory");

        Assert.That(first, Is.EqualTo(second), "same input → same id (deterministic).");
        Assert.That(first.Length, Is.EqualTo(64), "SHA256 hex digest는 64 char.");
    }

    [Test]
    public void Compute_DifferentBattleContextHash_ProducesDifferentId()
    {
        var a = RewardCommitIdService.Compute(AnyHash, "victory");
        var b = RewardCommitIdService.Compute("0000000000000000000000000000000000000000000000000000000000000000", "victory");

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void Compute_DifferentOutcome_ProducesDifferentId()
    {
        var victory = RewardCommitIdService.Compute(AnyHash, "victory");
        var defeat = RewardCommitIdService.Compute(AnyHash, "defeat");
        var extract = RewardCommitIdService.Compute(AnyHash, "extract");

        Assert.That(victory, Is.Not.EqualTo(defeat));
        Assert.That(victory, Is.Not.EqualTo(extract));
        Assert.That(defeat, Is.Not.EqualTo(extract));
    }

    [Test]
    public void Compute_OutcomeNormalizesCaseAndWhitespace()
    {
        var canonical = RewardCommitIdService.Compute(AnyHash, "victory");
        var upper = RewardCommitIdService.Compute(AnyHash, "VICTORY");
        var spaced = RewardCommitIdService.Compute(AnyHash, "  victory  ");

        Assert.That(upper, Is.EqualTo(canonical));
        Assert.That(spaced, Is.EqualTo(canonical));
    }

    [Test]
    public void Compute_BlankOutcome_FallsBackToUnknown()
    {
        var blankExplicit = RewardCommitIdService.Compute(AnyHash, "unknown");
        var blankImplicit = RewardCommitIdService.Compute(AnyHash, "");

        Assert.That(blankImplicit, Is.EqualTo(blankExplicit));
    }

    [Test]
    public void Compute_EmptyHash_Throws()
    {
        Assert.That(() => RewardCommitIdService.Compute("", "victory"), Throws.ArgumentException);
        Assert.That(() => RewardCommitIdService.Compute("   ", "victory"), Throws.ArgumentException);
    }
}
