using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 0 관측 오라클 검증 (ADR-0029). <see cref="BattleStateCanonicalHash"/>가
/// (1) 같은 seed 2회 실행에서 byte-identical hash 스트림을 내고, (2) state 변화에 민감하며,
/// (3) 안정된 hex16 + schema 태그를 낸다는 것을 확인한다. 현 sim은 아직 float이므로 이 테스트는
/// same-binary 재현만 보증한다(cross-platform 보증은 Fixed 마이그레이션 이후). 관측 전용이라
/// sim behavior는 건드리지 않는다.
/// </summary>
[Category("FastUnit")]
public sealed class BattleStateCanonicalHashTests
{
    [TestCase(7)]
    [TestCase(23)]
    [TestCase(42)]
    public void HashStream_IsIdentical_AcrossTwoRuns_SameSeed(int seed)
    {
        var first = HashSequence(BuildBattle(seed));
        var second = HashSequence(BuildBattle(seed));

        Assert.That(first, Is.EqualTo(second), $"seed={seed}: canonical hash 스트림이 두 실행 간 갈라졌다.");
        Assert.That(first.Count, Is.GreaterThan(1), $"seed={seed}: 스텝이 진행되지 않았다.");
    }

    [Test]
    public void Hash_Changes_WhenStateAdvances()
    {
        var sim = BuildBattle(7);
        var before = BattleStateCanonicalHash.Compute(sim.State);
        sim.Step(); // 첫 스텝에서 유닛은 접근/타이머 진행 → 상태가 바뀐다.
        var after = BattleStateCanonicalHash.Compute(sim.State);

        Assert.That(after, Is.Not.EqualTo(before), "state가 진행됐는데 canonical hash가 그대로다(민감도 결함).");
    }

    [Test]
    public void Hash_IsHex16_AndSchemaTagged()
    {
        var hash = BattleStateCanonicalHash.Compute(BuildBattle(7).State);

        Assert.That(hash, Does.Match("^[0-9a-f]{16}$"), "FNV-1a 64-bit hex16 형식이 아니다.");
        Assert.That(BattleStateCanonicalHash.SchemaVersion, Is.EqualTo("CanonicalStateHashV1"));
    }

    private static List<string> HashSequence(BattleSimulator sim)
    {
        var hashes = new List<string> { BattleStateCanonicalHash.Compute(sim.State) };
        var guard = 0;
        while (!sim.IsFinished && guard++ < 2000)
        {
            sim.Step();
            hashes.Add(BattleStateCanonicalHash.Compute(sim.State));
        }

        return hashes;
    }

    private static BattleSimulator BuildBattle(int seed)
    {
        var ally = CombatTestFactory.CreateUnit(
            "ally_blade",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            moveSpeed: 2.1f,
            attackRange: 1.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_blade",
            race: "undead",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            moveSpeed: 1.9f,
            attackRange: 1.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        return new BattleSimulator(CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: seed), 200);
    }
}
