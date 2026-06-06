using System;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class EngagementSlotServiceTests
{
    private static UnitSnapshot MakeUnit(
        string id,
        TeamSide side,
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter,
        string classId = "vanguard",
        float attackRange = 1.2f,
        BehaviorProfile? behavior = null,
        FootprintProfile? footprint = null)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(
            id,
            classId: classId,
            anchor: anchor,
            attackRange: attackRange,
            behavior: behavior,
            footprint: footprint);
        var unit = new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, anchor),
            BattleFactory.ResolveSpawnPosition(side, anchor));
        unit.SetActionState(CombatActionState.AcquireTarget);
        return unit;
    }

    private static BattleState MakeState(UnitSnapshot[] allies, UnitSnapshot[] enemies, int seed = 7)
    {
        return new BattleState(
            allies, enemies,
            TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds, seed);
    }

    // ── RequiresSlotting ──

    [Test]
    public void RequiresSlotting_TrueForMelee_FalseForRanged()
    {
        var melee = MakeUnit("melee", TeamSide.Ally, attackRange: 1.2f);
        var ranged = MakeUnit("ranged", TeamSide.Ally, attackRange: 4.0f);

        var meleeBand = new FloatRange(0.5f, 1.4f);
        var rangedBand = new FloatRange(3.0f, 5.0f);

        Assert.That(EngagementSlotService.RequiresSlotting(melee, meleeBand), Is.True,
            "Melee unit with short range band should require slotting");
        Assert.That(EngagementSlotService.RequiresSlotting(ranged, rangedBand), Is.False,
            "Ranged unit with long range band should not require slotting");
    }

    // ── Single attacker ──

    [Test]
    public void SingleAttacker_GetsSlotIndex0_NotOverflow()
    {
        var attacker = MakeUnit("attacker", TeamSide.Ally, attackRange: 1.2f);
        var target = MakeUnit("target", TeamSide.Enemy, attackRange: 1.2f);
        attacker.SetPosition(new CombatVector2(-1f, 0f));
        target.SetPosition(new CombatVector2(1f, 0f));
        attacker.SetCurrentTarget(target.Id);

        var state = MakeState(new[] { attacker }, new[] { target });
        var meleeBand = new FloatRange(0.5f, 1.4f);
        var slot = EngagementSlotService.Resolve(state, attacker, target, meleeBand);

        Assert.That(slot, Is.Not.Null, "Melee attacker should get a slot");
        Assert.That(slot!.SlotIndex, Is.EqualTo(0), "Single attacker gets slot 0");
        Assert.That(slot.IsOverflow, Is.False, "Single attacker should not overflow");
        Assert.That(slot.TargetId, Is.EqualTo(target.Id));
    }

    // ── Multiple attackers distribute ──

    [Test]
    public void MultipleAttackers_GetDistinctSlots()
    {
        var attacker1 = MakeUnit("attacker_a", TeamSide.Ally, attackRange: 1.2f);
        var attacker2 = MakeUnit("attacker_b", TeamSide.Ally, attackRange: 1.2f);
        var target = MakeUnit("target", TeamSide.Enemy, attackRange: 1.2f);
        attacker1.SetPosition(new CombatVector2(-1f, 0.3f));
        attacker2.SetPosition(new CombatVector2(-1f, -0.3f));
        target.SetPosition(new CombatVector2(1f, 0f));
        attacker1.SetCurrentTarget(target.Id);
        attacker2.SetCurrentTarget(target.Id);

        var state = MakeState(new[] { attacker1, attacker2 }, new[] { target });
        var meleeBand = new FloatRange(0.5f, 1.4f);

        var slot1 = EngagementSlotService.Resolve(state, attacker1, target, meleeBand);
        var slot2 = EngagementSlotService.Resolve(state, attacker2, target, meleeBand);

        Assert.That(slot1, Is.Not.Null);
        Assert.That(slot2, Is.Not.Null);

        var pos1 = slot1!.Position;
        var pos2 = slot2!.Position;
        var slotSeparation = pos1.DistanceTo(pos2);
        Assert.That(slotSeparation, Is.GreaterThan(0.1f),
            "Two attackers should be assigned to different positions around the target");
    }

    // ── Overflow ──

    [Test]
    public void Overflow_ThirdAttacker_OnTwoSlotTarget_IsMarkedOverflow()
    {
        var footprint = new FootprintProfile(
            NavigationRadius: 0.3f,
            SeparationRadius: 0.35f,
            CombatReach: 1.0f,
            PreferredRangeBand: new FloatRange(0.5f, 1.2f),
            EngagementSlotCount: 2,
            EngagementSlotRadius: 0.5f,
            BodySizeCategory: BodySizeCategory.Medium,
            HeadAnchorHeight: 1.5f);

        var a1 = MakeUnit("a_1", TeamSide.Ally, attackRange: 1.2f);
        var a2 = MakeUnit("a_2", TeamSide.Ally, attackRange: 1.2f);
        var a3 = MakeUnit("a_3", TeamSide.Ally, attackRange: 1.2f);
        var target = MakeUnit("target", TeamSide.Enemy, attackRange: 1.2f, footprint: footprint);

        a1.SetPosition(new CombatVector2(-1f, 0.5f));
        a2.SetPosition(new CombatVector2(-1f, 0f));
        a3.SetPosition(new CombatVector2(-1f, -0.5f));
        target.SetPosition(new CombatVector2(1f, 0f));
        a1.SetCurrentTarget(target.Id);
        a2.SetCurrentTarget(target.Id);
        a3.SetCurrentTarget(target.Id);

        var state = MakeState(new[] { a1, a2, a3 }, new[] { target });
        var meleeBand = new FloatRange(0.5f, 1.2f);

        var slots = new[]
        {
            EngagementSlotService.Resolve(state, a1, target, meleeBand),
            EngagementSlotService.Resolve(state, a2, target, meleeBand),
            EngagementSlotService.Resolve(state, a3, target, meleeBand),
        };

        var overflowCount = 0;
        foreach (var slot in slots)
        {
            Assert.That(slot, Is.Not.Null);
            if (slot!.IsOverflow)
            {
                overflowCount++;
            }
        }

        Assert.That(overflowCount, Is.EqualTo(1),
            "With 3 attackers on a 2-slot target, exactly 1 should overflow");
    }

    // ── Ranged returns null ──

    [Test]
    public void Resolve_RangedUnit_ReturnsNull()
    {
        var ranged = MakeUnit("ranged", TeamSide.Ally, attackRange: 4.0f);
        var target = MakeUnit("target", TeamSide.Enemy);
        ranged.SetPosition(new CombatVector2(-3f, 0f));
        target.SetPosition(new CombatVector2(1f, 0f));
        ranged.SetCurrentTarget(target.Id);

        var state = MakeState(new[] { ranged }, new[] { target });
        var rangedBand = new FloatRange(3.0f, 5.0f);

        var slot = EngagementSlotService.Resolve(state, ranged, target, rangedBand);

        Assert.That(slot, Is.Null, "Ranged unit should not get engagement slot");
    }

    [Test]
    public void PositioningIntent_IsSeedDeterministic_AndCanAlterSlotArc()
    {
        var attacker = MakeUnit("duelist_attacker", TeamSide.Ally, classId: "duelist", attackRange: 1.2f);
        var target = MakeUnit("target", TeamSide.Enemy, attackRange: 1.2f);
        attacker.SetPosition(new CombatVector2(-2f, 0f));
        target.SetPosition(new CombatVector2(0f, 0f));
        attacker.SetCurrentTarget(target.Id);
        var band = new FloatRange(0.8f, 1.2f);

        var intentA = EngagementSlotService.ResolvePositioningIntent(MakeState(new[] { attacker }, new[] { target }, seed: 11), attacker, target, band);
        var intentA2 = EngagementSlotService.ResolvePositioningIntent(MakeState(new[] { attacker }, new[] { target }, seed: 11), attacker, target, band);
        var intentB = EngagementSlotService.ResolvePositioningIntent(MakeState(new[] { attacker }, new[] { target }, seed: 51), attacker, target, band);

        Assert.That(intentA2, Is.EqualTo(intentA));
        Assert.That(intentB, Is.Not.EqualTo(intentA));

        var state = MakeState(new[] { attacker }, new[] { target }, seed: 11);
        var front = EngagementSlotService.Resolve(state, attacker, target, band, PositioningIntentKind.Frontline);
        var back = EngagementSlotService.Resolve(state, attacker, target, band, PositioningIntentKind.BacklineDive);
        Assert.That(front, Is.Not.Null);
        Assert.That(back, Is.Not.Null);
        Assert.That(front!.Position.X, Is.LessThan(target.Position.X));
        Assert.That(back!.Position.X, Is.GreaterThan(target.Position.X));
    }

    [Test]
    public void SlotPositionMoveAndSlotLoss_RecordReplanReasons()
    {
        var attacker = MakeUnit("attacker", TeamSide.Ally, attackRange: 1.2f);
        var target = MakeUnit("target", TeamSide.Enemy, attackRange: 1.2f);
        attacker.SetPosition(new CombatVector2(-2f, 0f));
        target.SetPosition(new CombatVector2(0f, 0f));
        attacker.SetCurrentTarget(target.Id);
        var state = MakeState(new[] { attacker }, new[] { target });
        var band = new FloatRange(0.8f, 1.2f);

        var slot = EngagementSlotService.Resolve(state, attacker, target, band, PositioningIntentKind.Frontline);
        Assert.That(slot, Is.Not.Null);
        attacker.SetEngagementSlot(slot);

        var shifted = slot! with { Position = slot.Position + new CombatVector2(0.35f, 0f) };
        attacker.SetEngagementSlot(shifted);
        Assert.That(attacker.PositioningReplanReason, Is.EqualTo(ReevaluationReason.TargetMoved));

        attacker.SetEngagementSlot(null);
        Assert.That(attacker.PositioningReplanReason, Is.EqualTo(ReevaluationReason.SlotLost));

        var evaluated = TacticEvaluator.Evaluate(state, attacker);
        Assert.That(evaluated.Target, Is.EqualTo(target));
        Assert.That(evaluated.SlotAssignment, Is.Not.Null);
        Assert.That(evaluated.PositioningIntent, Is.Not.EqualTo(PositioningIntentKind.None));
    }

    // ── Stage A: slot hysteresis (GPT Pro) ──

    [Test]
    public void StickyCommitment_TracksMovingTarget_WithoutRethrashingKey()
    {
        var attacker = MakeUnit("attacker", TeamSide.Ally, attackRange: 1.2f);
        var target = MakeUnit("target", TeamSide.Enemy, attackRange: 1.2f);
        attacker.SetPosition(new CombatVector2(-1f, 0f));
        target.SetPosition(new CombatVector2(1f, 0f));
        attacker.SetCurrentTarget(target.Id);
        var state = MakeState(new[] { attacker }, new[] { target });
        var band = new FloatRange(0.5f, 1.4f);

        var slot1 = EngagementSlotService.Resolve(state, attacker, target, band);
        Assert.That(slot1, Is.Not.Null);
        attacker.SetEngagementSlot(slot1);

        // Target drifts: the committed slot must FOLLOW it, not re-thrash its angular identity.
        target.SetPosition(new CombatVector2(1.4f, 0.3f));
        var slot2 = EngagementSlotService.Resolve(state, attacker, target, band);

        Assert.That(slot2, Is.Not.Null);
        Assert.That(slot2!.SlotIndex, Is.EqualTo(slot1!.SlotIndex), "angular slot identity is frozen under the lease");
        Assert.That(slot2.SlotRing, Is.EqualTo(slot1.SlotRing));
        Assert.That(slot2.LocalOffset.DistanceTo(slot1.LocalOffset), Is.LessThan(1e-4f), "the key (target-relative offset) does not change");
        Assert.That(slot2.Position.DistanceTo(target.Position + slot1.LocalOffset), Is.LessThan(1e-4f), "absolute position tracks the moved target");
    }

    [Test]
    public void StickyCommitment_TransientNeighborDoesNotRethrash()
    {
        var attacker = MakeUnit("attacker", TeamSide.Ally, attackRange: 1.2f);
        var bystander = MakeUnit("bystander", TeamSide.Ally, attackRange: 1.2f); // a body, not an attacker
        var target = MakeUnit("target", TeamSide.Enemy, attackRange: 1.2f);
        attacker.SetPosition(new CombatVector2(-1f, 0f));
        bystander.SetPosition(new CombatVector2(-3f, 2f));
        target.SetPosition(new CombatVector2(1f, 0f));
        attacker.SetCurrentTarget(target.Id);
        var state = MakeState(new[] { attacker, bystander }, new[] { target });
        var band = new FloatRange(0.5f, 1.4f);

        var slot1 = EngagementSlotService.Resolve(state, attacker, target, band);
        Assert.That(slot1, Is.Not.Null);
        attacker.SetEngagementSlot(slot1);

        // Park a neighbour right on the committed slot — the OLD gate (IsSlotStillClear) would invalidate the
        // slot here and re-thrash it every step. The lease must hold the commitment regardless.
        bystander.SetPosition(slot1!.Position);
        var slot2 = EngagementSlotService.Resolve(state, attacker, target, band);

        Assert.That(slot2, Is.Not.Null);
        Assert.That(slot2!.SlotIndex, Is.EqualTo(slot1.SlotIndex), "a transient neighbour brushing the slot must NOT re-thrash the commitment");
        Assert.That(slot2.LocalOffset.DistanceTo(slot1.LocalOffset), Is.LessThan(1e-4f));
    }

    [Test]
    public void Commitment_ReleasesOnHardRetarget()
    {
        var attacker = MakeUnit("attacker", TeamSide.Ally, attackRange: 1.2f);
        var targetA = MakeUnit("target_a", TeamSide.Enemy, attackRange: 1.2f);
        var targetB = MakeUnit("target_b", TeamSide.Enemy, attackRange: 1.2f);
        attacker.SetPosition(new CombatVector2(-1f, 0f));
        targetA.SetPosition(new CombatVector2(1f, 0.5f));
        targetB.SetPosition(new CombatVector2(1f, -0.5f));
        attacker.SetCurrentTarget(targetA.Id);
        var state = MakeState(new[] { attacker }, new[] { targetA, targetB });
        var band = new FloatRange(0.5f, 1.4f);

        var slotA = EngagementSlotService.Resolve(state, attacker, targetA, band);
        Assert.That(slotA, Is.Not.Null);
        attacker.SetEngagementSlot(slotA);
        Assert.That(slotA!.TargetId, Is.EqualTo(targetA.Id));

        // Retarget to B → the A-commitment is dropped and a fresh slot is computed for B.
        var slotB = EngagementSlotService.Resolve(state, attacker, targetB, band);
        Assert.That(slotB, Is.Not.Null);
        Assert.That(slotB!.TargetId, Is.EqualTo(targetB.Id), "a different target releases the old slot commitment");
        Assert.That(slotB.CommitTick, Is.EqualTo(state.StepIndex), "fresh commitment stamped on retarget");
    }
}
