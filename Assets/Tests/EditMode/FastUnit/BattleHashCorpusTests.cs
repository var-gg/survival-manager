using NUnit.Framework;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 0 cross-platform 골든 코퍼스 검증 (ADR-0029). <see cref="BattleHashCorpus"/>가 같은 backend에서
/// 결정적 코퍼스를 내고, 모든 seed와 final 라인을 포함하는지 확인한다. 현 sim은 float이라 same-binary
/// 재현만 보증한다(cross-platform 비교는 Fixed 마이그레이션 후 CI backend matrix가 수행).
/// </summary>
[Category("FastUnit")]
public sealed class BattleHashCorpusTests
{
    [Test]
    public void Generate_IsDeterministic_AcrossTwoRuns()
    {
        var first = BattleHashCorpus.Generate();
        var second = BattleHashCorpus.Generate();

        Assert.That(first, Is.EqualTo(second), "hash 코퍼스가 두 실행 간 갈라졌다(same-binary 비결정).");
        Assert.That(first.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Generate_CoversAllSeeds_AndFinalLines()
    {
        var corpus = BattleHashCorpus.Generate();

        foreach (var seed in BattleHashCorpus.DefaultSeeds)
        {
            StringAssert.Contains($"seed={seed}", corpus);
        }

        StringAssert.Contains("final:", corpus);
        StringAssert.Contains(BattleStateCanonicalHash.SchemaVersion, corpus);
    }
}
