using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 1 (GPT Pro J9/J23 — ActionInstanceId identity &amp; persistence). Ids are allocated
/// monotonically, exactly once per accepted BeginWindup, and never reused after cancel / death /
/// target-loss / range-loss. Because allocation happens in the deterministic action order, replaying
/// the same seed reproduces the same id-at-step sequence — the property a mid-battle save/replay relies
/// on (there is no hidden allocator state outside the deterministic step stream).
/// </summary>
[Category("FastUnit")]
public sealed class ActionInstanceIdPersistenceTests
{
    [Test]
    public void ActionInstanceIds_AreContiguous_Monotonic_AndNeverReused()
    {
        var ids = CollectStartedIds(BuildMelee1v1(7));

        Assert.That(ids, Is.Not.Empty, "expected accepted windups to allocate ids.");
        Assert.That(ids, Is.Unique, "an ActionInstanceId must never be reused (J9/J23).");
        Assert.That(ids, Is.Ordered.Ascending, "ids are allocated monotonically in deterministic action order.");
        Assert.That(ids.First(), Is.EqualTo(1L), "allocation starts at 1.");
        Assert.That(ids.Last(), Is.EqualTo(ids.Count), "contiguous 1..N: allocation happens only at accepted BeginWindup (one Started per id).");
    }

    [TestCase(7)]
    [TestCase(42)]
    public void IdSequence_IsIdentical_AcrossReplay_SameSeed(int seed)
    {
        var first = CollectStampedStartedIds(BuildMelee1v1(seed));
        var second = CollectStampedStartedIds(BuildMelee1v1(seed));

        Assert.That(first, Is.EqualTo(second),
            $"seed={seed}: replay must reproduce the same (step, id) sequence — no hidden allocator state (J23).");
        Assert.That(first, Is.Not.Empty);
    }

    [TestCase(13)]
    [TestCase(31)]
    public void NoReuse_EvenThroughCancels_InSkirmish(int seed)
    {
        // A 2v2 skirmish exercises retarget / range-loss cancels (Canceled intents) interleaved with
        // resolves; uniqueness + monotonicity must still hold across the whole battle.
        var ids = CollectStartedIds(BuildSkirmish2v2(seed));

        Assert.That(ids, Is.Not.Empty);
        Assert.That(ids, Is.Unique);
        Assert.That(ids, Is.Ordered.Ascending);
    }

    private static List<long> CollectStartedIds(BattleSimulator simulator)
    {
        return CollectStampedStartedIds(simulator).Select(pair => pair.Id).ToList();
    }

    private static List<(int Step, long Id)> CollectStampedStartedIds(BattleSimulator simulator)
    {
        var ids = new List<(int Step, long Id)>();
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            if (step.CombatEventIntents == null)
            {
                continue;
            }

            foreach (var intent in step.CombatEventIntents)
            {
                if (intent.Status == CombatEventIntentStatus.Started)
                {
                    ids.Add((step.StepIndex, intent.ActionInstanceId.Value));
                }
            }
        }

        return ids;
    }

    private static BattleSimulator BuildMelee1v1(int seed)
    {
        var ally = CombatTestFactory.CreateUnit(
            "ally_blade", anchor: DeploymentAnchorId.FrontCenter, hp: 60f,
            moveSpeed: 2.1f, attackRange: 1.2f, attackWindup: 0.3f, attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_blade", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 60f,
            moveSpeed: 1.9f, attackRange: 1.2f, attackWindup: 0.3f, attackCooldown: 0.5f);
        return new BattleSimulator(CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: seed), 200);
    }

    private static BattleSimulator BuildSkirmish2v2(int seed)
    {
        var allies = new[]
        {
            CombatTestFactory.CreateUnit("ally_van", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 70f, attackRange: 1.2f),
            CombatTestFactory.CreateUnit("ally_duel", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 55f, attack: 6f, attackRange: 1.2f),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateUnit("enemy_van", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 70f, attackRange: 1.2f),
            CombatTestFactory.CreateUnit("enemy_duel", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 55f, attack: 6f, attackRange: 1.2f),
        };
        return new BattleSimulator(CombatTestFactory.CreateBattleState(allies, enemies, seed: seed), 250);
    }
}
