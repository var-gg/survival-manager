using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 1 role-drama acceptance. The tactical brain (RoleBrain) chooses a CombatIntent per unit, and the
/// movement executor reads it so each role behaves distinctly and readably. This suite pins the first role
/// behavior — ranger AnchorFire — and grows per role as the drama lands.
/// </summary>
[Category("FastUnit")]
public sealed class RoleBrainTests
{
    private static UnitSnapshotPair BuildRangerVsDummy(float edgeStart, float rangerRange = 5f)
    {
        var ranger = CombatTestFactory.CreateUnit(
            "ally_ranger",
            classId: "ranger",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 40f,
            moveSpeed: 1.8f,
            attackRange: rangerRange,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var dummy = CombatTestFactory.CreateUnit(
            "enemy_dummy",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 300f,
            defense: 2f,
            moveSpeed: 0f,
            attackRange: 1.2f);

        var state = CombatTestFactory.CreateBattleState(new[] { ranger }, new[] { dummy }, seed: 7);
        var r = state.Allies[0];
        var d = state.Enemies[0];
        // Place the (stationary) dummy at a chosen edge distance straight ahead of the ranger.
        d.SetPosition(new CombatVector2(r.Position.X + r.NavigationRadius + d.NavigationRadius + edgeStart, r.Position.Y));
        return new UnitSnapshotPair(state, r, d);
    }

    // Backline unit + a frontline vanguard ally (closer to the enemy) + a stationary far enemy. The backliner
    // is genuinely "behind a frontline", so it holds; a lone backliner instead advances (covered implicitly by
    // the fire-in-range test and the no-standoff guarantee). state.Allies[0] = backliner.
    private static (BattleState State, UnitSnapshot Backliner, UnitSnapshot Enemy) BuildBacklineBehindFrontline(
        string classId, float attackRange, float edgeStart)
    {
        var backliner = CombatTestFactory.CreateUnit(
            "ally_backliner",
            classId: classId,
            anchor: DeploymentAnchorId.BackCenter,
            hp: 40f,
            moveSpeed: 1.8f,
            attackRange: attackRange,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var frontline = CombatTestFactory.CreateUnit(
            "ally_frontline",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 200f,
            moveSpeed: 1.7f,
            attackRange: 1.2f,
            attackCooldown: 0.9f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_dummy",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontTop,
            hp: 400f,
            defense: 3f,
            moveSpeed: 0f,
            attackRange: 1.2f);

        var state = CombatTestFactory.CreateBattleState(new[] { backliner, frontline }, new[] { enemy }, seed: 7);
        var b = state.Allies[0];
        var e = state.Enemies[0];
        e.SetPosition(new CombatVector2(b.Position.X + b.NavigationRadius + e.NavigationRadius + edgeStart, b.Position.Y));
        return (state, b, e);
    }

    [Test]
    public void Ranger_AnchorFire_HoldsBacklineAnchor_BehindFrontline()
    {
        var (state, ranger, dummy) = BuildBacklineBehindFrontline("ranger", attackRange: 5f, edgeStart: 8f);
        var sim = new BattleSimulator(state, 40);

        var minEdge = float.MaxValue;
        for (var i = 0; i < 40 && !sim.IsFinished; i++)
        {
            sim.Step();
            if (ranger.IsAlive && dummy.IsAlive)
            {
                minEdge = System.MathF.Min(minEdge, MovementResolver.ComputeEdgeDistance(ranger, dummy));
            }
        }

        // With a frontline vanguard closer to the enemy, the ranger holds its backline anchor and never closes
        // into its 5m attack range against a passive enemy — the frontline engages, the ranger waits for targets.
        Assert.That(ranger.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.AnchorFire),
            "a backline ranger's tactical intent should be AnchorFire");
        Assert.That(minEdge, Is.GreaterThan(5.5f),
            "AnchorFire ranger advanced into range of a passive enemy — it should hold behind the frontline");
    }

    [Test]
    public void Ranger_AnchorFire_FiresFromAnchor_WhenEnemyIsInRange()
    {
        // Enemy starts inside the ranger's range, so the ranger fires (stand and shoot) — the "does not chase"
        // invariant is covered by HoldsBacklineAnchor above; here we only require that an in-range enemy is shot.
        var (state, ranger, dummy) = BuildRangerVsDummy(edgeStart: 3f);
        var sim = new BattleSimulator(state, 40);

        var fired = false;
        for (var i = 0; i < 40 && !sim.IsFinished; i++)
        {
            var step = sim.Step();
            if (step.Events.Any(ev =>
                    ev.ActorName == "ally_ranger"
                    && ev.LogCode == BattleLogCode.BasicAttackDamage
                    && ev.Value > 0f))
            {
                fired = true;
            }
        }

        Assert.That(fired, Is.True, "AnchorFire ranger should fire at an in-range enemy from its anchor");
    }

    [Test]
    public void Mystic_SupportAnchor_HoldsBacklineAnchor_BehindFrontline()
    {
        // A backline mystic behind a frontline vanguard basic-attacks the nearest enemy. With SupportAnchor it
        // holds the backline anchor instead of walking forward to poke a distant enemy — the support line stays
        // back while the frontline engages. (Its heal-pursuit of an ally is unaffected: the hold is gated to
        // enemy targets only — see Healer_Supports_Lowest_Health_Ally — and a lone/frontmost support advances so
        // a back-line standoff never locks up.)
        var (state, mystic, dummy) = BuildBacklineBehindFrontline("mystic", attackRange: 2.4f, edgeStart: 6f);

        var sim = new BattleSimulator(state, 40);
        var minEdge = float.MaxValue;
        for (var i = 0; i < 40 && !sim.IsFinished; i++)
        {
            sim.Step();
            if (mystic.IsAlive && dummy.IsAlive)
            {
                minEdge = System.MathF.Min(minEdge, MovementResolver.ComputeEdgeDistance(mystic, dummy));
            }
        }

        Assert.That(mystic.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.SupportAnchor),
            "a backline mystic's tactical intent should be SupportAnchor");
        Assert.That(minEdge, Is.GreaterThan(2.9f),
            "SupportAnchor mystic advanced into range of a passive enemy — it should hold behind the frontline");
    }

    private readonly struct UnitSnapshotPair
    {
        public UnitSnapshotPair(BattleState state, UnitSnapshot ranger, UnitSnapshot dummy)
        {
            State = state;
            Ranger = ranger;
            Dummy = dummy;
        }

        public BattleState State { get; }
        public UnitSnapshot Ranger { get; }
        public UnitSnapshot Dummy { get; }

        public void Deconstruct(out BattleState state, out UnitSnapshot ranger, out UnitSnapshot dummy)
        {
            state = State;
            ranger = Ranger;
            dummy = Dummy;
        }
    }
}
