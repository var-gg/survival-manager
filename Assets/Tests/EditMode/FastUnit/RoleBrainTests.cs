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

    [Test]
    public void Vanguard_HoldLine_HoldsTheLine_DoesNotChaseDistantEnemy_UnderHoldLinePosture()
    {
        var vanguard = CombatTestFactory.CreateUnit(
            "ally_vanguard",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 200f,
            moveSpeed: 1.9f,
            attackRange: 1.2f,
            attackCooldown: 0.9f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_dummy",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 300f,
            defense: 3f,
            moveSpeed: 0f,
            attackRange: 1.2f);

        var state = CombatTestFactory.CreateBattleState(
            new[] { vanguard }, new[] { enemy }, allyPosture: TeamPostureType.HoldLine);
        var v = state.Allies[0];
        var e = state.Enemies[0];
        // Park the (near-stationary) enemy far across the arena, well beyond the hold radius.
        e.SetPosition(new CombatVector2(v.AnchorPosition.X + 10f, v.AnchorPosition.Y));

        var sim = new BattleSimulator(state, 60);
        var minEdge = float.MaxValue;
        for (var i = 0; i < 60 && !sim.IsFinished; i++)
        {
            sim.Step();
            if (v.IsAlive && e.IsAlive)
            {
                minEdge = System.MathF.Min(minEdge, MovementResolver.ComputeEdgeDistance(v, e));
            }
        }

        // Under HoldLine posture the vanguard holds its frontline anchor band — it never closes the distance to a
        // far enemy (a StandardAdvance vanguard would chase it across the arena). It engages whatever comes to it.
        Assert.That(v.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.HoldLine),
            "a vanguard under HoldLine posture should hold the line");
        Assert.That(minEdge, Is.GreaterThan(5.0f),
            "HoldLine vanguard should hold near its anchor, not chase a distant enemy across the arena");
    }

    // ── Duelist Dive ──
    // ally duelist + ally vanguard (support) vs enemy vanguard (nearest) + enemy backline ranger (dive target).
    // Spawn-advance is skipped so the geometry holds at step 0; the enemy vanguard is the melee-nearest enemy, so
    // a Dive intent that targets the ranger proves the narrow retarget override.
    private static (BattleState State, UnitSnapshot Duelist, UnitSnapshot AllyVanguard, UnitSnapshot EnemyVanguard, UnitSnapshot EnemyRanger)
        BuildDiveScenario(TeamPostureType allyPosture, bool protectRanger = false)
    {
        var duelist = CombatTestFactory.CreateUnit("ally_duelist", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter, hp: 60f, moveSpeed: 2.1f, attackRange: 1.2f, attackWindup: 0.1f, attackCooldown: 0.7f);
        var allyVanguard = CombatTestFactory.CreateUnit("ally_vanguard", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 120f, moveSpeed: 1.7f, attackRange: 1.2f, attackCooldown: 0.9f);
        var enemyVanguard = CombatTestFactory.CreateUnit("enemy_vanguard", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 120f, moveSpeed: 1.7f, attackRange: 1.2f, attackCooldown: 0.9f);
        var enemyRanger = CombatTestFactory.CreateUnit("enemy_ranger", race: "undead", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, hp: 40f, moveSpeed: 1.9f, attackRange: 5f, attackWindup: 0.1f, attackCooldown: 0.5f);

        var state = CombatTestFactory.CreateBattleState(
            new[] { duelist, allyVanguard }, new[] { enemyVanguard, enemyRanger },
            allyPosture: allyPosture, enemyPosture: TeamPostureType.StandardAdvance, seed: 7);

        var d = state.Allies[0];
        var av = state.Allies[1];
        var ev = state.Enemies[0];
        var er = state.Enemies[1];

        d.SetPosition(new CombatVector2(0f, 0f));
        av.SetPosition(new CombatVector2(-0.8f, 0.6f)); // allied frontliner = dive support proxy
        if (protectRanger)
        {
            er.SetPosition(new CombatVector2(3.2f, 0f));
            ev.SetPosition(new CombatVector2(3.0f, 0f)); // bodyguards the ranger (within DiveProtectedRadius)
        }
        else
        {
            ev.SetPosition(new CombatVector2(2.0f, 0f)); // nearest enemy to the duelist
            er.SetPosition(new CombatVector2(4.5f, 0f)); // exposed backline ranger, within dive path/depth
        }

        foreach (var u in state.AllUnits)
        {
            u.SetActionState(CombatActionState.AcquireTarget); // skip spawn-advance; hold the geometry at step 0
        }

        return (state, d, av, ev, er);
    }

    [Test]
    public void Duelist_DiveIntent_OverridesMeleeNearest_ToBacklineRanger()
    {
        var (state, duelist, _, enemyVanguard, enemyRanger) = BuildDiveScenario(TeamPostureType.AllInBackline);
        var sim = new BattleSimulator(state, 60);
        sim.Step();

        Assert.That(duelist.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive),
            "an exposed backline ranger under an aggressive posture should open a dive window");
        Assert.That(duelist.CurrentCombatIntent.TargetId, Is.EqualTo(enemyRanger.Id));
        Assert.That(duelist.CurrentTargetId, Is.EqualTo(enemyRanger.Id),
            "the dive must override the begin-gate/movement target, not only the intent's target id");
        Assert.That(duelist.Position.DistanceTo(enemyVanguard.Position),
            Is.LessThan(duelist.Position.DistanceTo(enemyRanger.Position)),
            "proves an override of melee-nearest: the enemy vanguard is closer than the dived ranger");

        var initialToRanger = duelist.Position.DistanceTo(enemyRanger.Position);
        for (var i = 0; i < 8; i++)
        {
            sim.Step();
        }

        Assert.That(duelist.Position.DistanceTo(enemyRanger.Position), Is.LessThan(initialToRanger),
            "the diving duelist should close on the backline ranger");
    }

    [Test]
    public void Duelist_StandardAdvance_UsesMeleeNearest_NoSilentDive()
    {
        var (state, duelist, _, enemyVanguard, _) = BuildDiveScenario(TeamPostureType.StandardAdvance);
        var sim = new BattleSimulator(state, 60);
        sim.Step();

        Assert.That(duelist.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive),
            "default StandardAdvance must not silently become an assassin meta");
        Assert.That(duelist.CurrentTargetId, Is.EqualTo(enemyVanguard.Id),
            "under StandardAdvance the duelist uses melee-nearest (the enemy vanguard)");
    }

    [Test]
    public void Duelist_DiveWindowRejected_WhenBacklineTargetProtectedByVanguard()
    {
        var (state, duelist, _, enemyVanguard, _) = BuildDiveScenario(TeamPostureType.AllInBackline, protectRanger: true);
        var sim = new BattleSimulator(state, 60);
        sim.Step();

        Assert.That(duelist.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive),
            "a bodyguarded backline target is not worth diving");
        Assert.That(duelist.CurrentTargetId, Is.EqualTo(enemyVanguard.Id),
            "with the dive rejected, the duelist falls back to melee-nearest");
    }

    [Test]
    public void Duelist_DiveCommit_KeepsBacklineTarget_UntilCommitExpires()
    {
        var (state, duelist, _, _, _) = BuildDiveScenario(TeamPostureType.AllInBackline);
        var sim = new BattleSimulator(state, 60);
        sim.Step();
        Assert.That(duelist.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        var targetAtEntry = duelist.CurrentCombatIntent.TargetId;
        var commitUntil = duelist.CurrentCombatIntent.CommitUntilStep;

        for (var i = 0; i < 5; i++)
        {
            sim.Step(); // total 6 steps, < DiveCommitSteps (12)
        }

        Assert.That(duelist.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive),
            "the dive should hold its committed intent before commit expiry");
        Assert.That(duelist.CurrentCombatIntent.TargetId, Is.EqualTo(targetAtEntry),
            "the committed dive target should not jitter");
        Assert.That(commitUntil, Is.GreaterThan(state.StepIndex));
    }

    [Test]
    public void Duelist_Dive_HardInterrupts_WhenDiveTargetDies()
    {
        var (state, duelist, _, _, enemyRanger) = BuildDiveScenario(TeamPostureType.AllInBackline);
        var sim = new BattleSimulator(state, 60);
        sim.Step();
        Assert.That(duelist.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));

        enemyRanger.TakeDamage(999f); // kill the dive target
        sim.Step();

        Assert.That(duelist.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive),
            "the dive hard-interrupts the moment its target dies");
        Assert.That(duelist.CurrentTargetId, Is.Not.EqualTo(enemyRanger.Id));
    }

    // ── Vanguard Peel ──
    // ally vanguard (front) + ally ranger (backline, protected) vs enemy vanguard (the melee-nearest enemy, front)
    // + enemy duelist (a diver threatening the ally ranger). A Peel intent that targets the diver proves the
    // override. With threatFar=true the diver is out of intercept range, so the vanguard holds the line instead.
    private static (BattleState State, UnitSnapshot Vanguard, UnitSnapshot AllyRanger, UnitSnapshot EnemyVanguard, UnitSnapshot EnemyDuelist)
        BuildPeelScenario(bool threatFar = false)
    {
        var vanguard = CombatTestFactory.CreateUnit("ally_vanguard", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 200f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f);
        var allyRanger = CombatTestFactory.CreateUnit("ally_ranger", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, hp: 40f, moveSpeed: 1.8f, attackRange: 5f, attackCooldown: 0.5f);
        var enemyVanguard = CombatTestFactory.CreateUnit("enemy_vanguard", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 200f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f);
        var enemyDuelist = CombatTestFactory.CreateUnit("enemy_duelist", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.BackTop, hp: 60f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f);

        var state = CombatTestFactory.CreateBattleState(
            new[] { vanguard, allyRanger }, new[] { enemyVanguard, enemyDuelist },
            allyPosture: TeamPostureType.HoldLine, enemyPosture: TeamPostureType.StandardAdvance, seed: 7);

        var v = state.Allies[0];
        var ar = state.Allies[1];
        var ev = state.Enemies[0];
        var ed = state.Enemies[1];

        v.SetPosition(new CombatVector2(0f, 0f));
        ev.SetPosition(new CombatVector2(0.8f, 0f)); // melee-nearest enemy to the vanguard (frontline clash)
        if (threatFar)
        {
            ar.SetPosition(new CombatVector2(-5f, 0f));
            ed.SetPosition(new CombatVector2(-4.5f, 0f)); // threatens the ranger, but > intercept distance from the vanguard
        }
        else
        {
            ar.SetPosition(new CombatVector2(-2.2f, 0f));
            ed.SetPosition(new CombatVector2(-1.5f, 0f)); // dived onto the ranger, within the vanguard's intercept range
        }

        foreach (var u in state.AllUnits)
        {
            u.SetActionState(CombatActionState.AcquireTarget); // skip spawn-advance; hold the geometry at step 0
        }

        return (state, v, ar, ev, ed);
    }

    [Test]
    public void Vanguard_PeelIntent_OverridesNearest_WhenBacklineAllyThreatened()
    {
        var (state, vanguard, allyRanger, enemyVanguard, enemyDuelist) = BuildPeelScenario();
        var sim = new BattleSimulator(state, 60);
        sim.Step();

        Assert.That(vanguard.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Peel),
            "a diver threatening a backline ally within reach should trigger a peel");
        Assert.That(vanguard.CurrentCombatIntent.TargetId, Is.EqualTo(enemyDuelist.Id));
        Assert.That(vanguard.CurrentCombatIntent.ProtectAllyId, Is.EqualTo(allyRanger.Id));
        Assert.That(vanguard.CurrentTargetId, Is.EqualTo(enemyDuelist.Id),
            "the peel must override the begin-gate/movement target, not only the intent's target id");
        Assert.That(vanguard.Position.DistanceTo(enemyVanguard.Position),
            Is.LessThan(vanguard.Position.DistanceTo(enemyDuelist.Position)),
            "proves an override of melee-nearest: the enemy vanguard is closer than the peeled diver");
    }

    [Test]
    public void Vanguard_Peel_MovesTowardThreat_NotRetreatingToAlly()
    {
        var (state, vanguard, _, _, enemyDuelist) = BuildPeelScenario();
        var sim = new BattleSimulator(state, 60);
        sim.Step();
        Assert.That(vanguard.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Peel));

        var initialToThreat = vanguard.Position.DistanceTo(enemyDuelist.Position);
        for (var i = 0; i < 5; i++)
        {
            sim.Step();
        }

        Assert.That(vanguard.Position.DistanceTo(enemyDuelist.Position), Is.LessThan(initialToThreat),
            "the peeling vanguard closes on the diver (intercept), it does not retreat to the protected ally");
    }

    [Test]
    public void Vanguard_NoPeel_WhenThreatOutsideInterceptDistance()
    {
        var (state, vanguard, _, _, _) = BuildPeelScenario(threatFar: true);
        var sim = new BattleSimulator(state, 60);
        sim.Step();

        Assert.That(vanguard.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Peel),
            "the vanguard cannot peel a threat it is too far to intercept");
        Assert.That(vanguard.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.HoldLine),
            "with no peel and a HoldLine posture, the vanguard holds the line");
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
