using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 1 (action choreography seam C1): the deterministic sim emits a typed
/// <see cref="BattleCombatEventIntent"/> channel as a sibling of <see cref="BattleMotionIntent"/>.
/// A <see cref="CombatEventIntentStatus.Started"/> intent is stamped at the windup-begin tick carrying
/// the predicted <see cref="BattleCombatEventIntent.ContactTick"/>; a
/// <see cref="CombatEventIntentStatus.Contacted"/> intent (same <see cref="ActionInstanceId"/>) lands at
/// the resolve tick with per-target <see cref="BattleContactIntent"/>s. Gameplay invariance is guarded
/// by <c>BattleDeterminismBaselineTests</c>; these assert the new channel's content, pairing, and
/// determinism.
/// </summary>
[Category("FastUnit")]
public sealed class BattleCombatEventIntentEmissionTests
{
    [Test]
    public void Windup_EmitsStartedIntent_WithIdAndPredictedContactTick()
    {
        var started = CollectIntents(BuildMelee1v1(7))
            .Where(intent => intent.Status == CombatEventIntentStatus.Started)
            .ToList();

        Assert.That(started, Is.Not.Empty, "expected at least one Started intent during a melee close.");
        foreach (var intent in started)
        {
            Assert.That(intent.ActionInstanceId.IsValid, Is.True, "Started intent must carry a valid ActionInstanceId.");
            Assert.That(intent.Kind, Is.EqualTo(CombatEventKind.BasicAttack));
            Assert.That(intent.WindupStartTick, Is.EqualTo(intent.StepIndex), "Started intent is stamped at its windup-begin tick.");
            Assert.That(intent.ContactTick, Is.GreaterThan(intent.WindupStartTick), "contact must land after windup begins.");
            Assert.That(intent.Contacts == null || intent.Contacts.Count == 0, Is.True, "Started carries no resolved contacts yet.");
        }
    }

    [Test]
    public void Contact_EmitsContactedIntent_PairedToStarted_AtPredictedTick()
    {
        var intents = CollectIntents(BuildMelee1v1(7));
        var startedById = intents
            .Where(intent => intent.Status == CombatEventIntentStatus.Started)
            .ToDictionary(intent => intent.ActionInstanceId);
        var contacted = intents.Where(intent => intent.Status == CombatEventIntentStatus.Contacted).ToList();

        Assert.That(contacted, Is.Not.Empty, "expected at least one Contacted intent.");
        foreach (var intent in contacted)
        {
            Assert.That(startedById.ContainsKey(intent.ActionInstanceId), Is.True,
                "every Contacted intent must pair to a Started intent by ActionInstanceId (J9).");
            var started = startedById[intent.ActionInstanceId];
            Assert.That(intent.ContactTick, Is.EqualTo(started.ContactTick),
                "Contacted ContactTick must equal the prediction made at windup.");
            Assert.That(intent.StepIndex, Is.EqualTo(intent.ContactTick),
                "the Contacted intent is reported in the step equal to its canonical ContactTick.");
            Assert.That(intent.Contacts, Is.Not.Null);
            Assert.That(intent.Contacts.Count, Is.GreaterThan(0), "a damaging contact carries at least one BattleContactIntent.");
            foreach (var contact in intent.Contacts)
            {
                Assert.That(contact.TargetId, Is.Not.Null, "a melee contact has a target.");
                Assert.That(contact.ContactTick, Is.EqualTo(intent.ContactTick));
            }
        }
    }

    [Test]
    public void CombatEventIntents_AreDeterministic_AcrossRuns()
    {
        var first = Serialize(CollectIntents(BuildMelee1v1(42)));
        var second = Serialize(CollectIntents(BuildMelee1v1(42)));

        Assert.That(first, Is.EqualTo(second), "combat-event-intent stream diverged across identical runs.");
        Assert.That(first, Is.Not.Empty);
    }

    private static List<BattleCombatEventIntent> CollectIntents(BattleSimulator simulator)
    {
        var intents = new List<BattleCombatEventIntent>();
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            if (step.CombatEventIntents != null)
            {
                intents.AddRange(step.CombatEventIntents);
            }
        }

        return intents;
    }

    private static string Serialize(IReadOnlyList<BattleCombatEventIntent> intents)
    {
        return string.Join("\n", intents.Select(intent => string.Join(
            ":",
            intent.StepIndex,
            intent.ActionInstanceId.Value,
            intent.ActorId.Value,
            intent.Kind,
            intent.Status,
            intent.WindupStartTick,
            intent.ContactTick,
            intent.Contacts?.Count ?? 0)));
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
}
