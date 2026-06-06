using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Tests.EditMode;

/// <summary>
/// Q5 (GPT Pro): melee engagers pick the nearest reachable enemy rather than chasing a far focus target,
/// with focus-fire preserved only locally (within a small distance band of the nearest). Ranged units and
/// forced targeting keep strict focus-fire. This is the finisher for the residual focus-fire-pile-up
/// treadmill once the movement layer (A/B/C) is clean.
/// </summary>
[Category("FastUnit")]
public sealed class MeleeNearestEngagementOverrideTests
{
    private static UnitSnapshot MakeUnit(string id, TeamSide side, string classId = "vanguard", float attackRange = 1.2f, float hp = 50f)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(id, classId: classId, attackRange: attackRange, hp: hp);
        return new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(side, DeploymentAnchorId.FrontCenter));
    }

    private static BattleState MakeState(UnitSnapshot[] allies, UnitSnapshot[] enemies)
    {
        return new BattleState(allies, enemies, TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance, BattleSimulator.DefaultFixedStepSeconds, 7);
    }

    [Test]
    public void Melee_DoesNotChaseFarLowHpFocus_PastANearerEnemy()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, attackRange: 1.2f);
        var farLowHp = MakeUnit("far_lowhp", TeamSide.Enemy, hp: 50f);
        var nearFullHp = MakeUnit("near_fullhp", TeamSide.Enemy, hp: 50f);
        farLowHp.TakeDamage(45f); // ratio 0.1 → focus-fire (lowest HP%) picks it, but it is far
        actor.SetPosition(new CombatVector2(0f, 0f));
        farLowHp.SetPosition(new CombatVector2(5f, 0f));
        nearFullHp.SetPosition(new CombatVector2(1.0f, 0f));

        var state = MakeState(new[] { actor }, new[] { farLowHp, nearFullHp });
        var selected = TargetScoringService.SelectTarget(state, actor, TargetSelectorType.LowestHpEnemy, BattleActionType.BasicAttack, null);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected!.Id.Value, Is.EqualTo("near_fullhp"), "melee engages the nearer enemy instead of chasing the far low-HP focus");
    }

    [Test]
    public void Melee_KeepsLocalFocus_WhenWithinBandOfNearest()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, attackRange: 1.2f);
        var focusLowHp = MakeUnit("focus_lowhp", TeamSide.Enemy, hp: 50f);
        var nearFullHp = MakeUnit("near_fullhp", TeamSide.Enemy, hp: 50f);
        focusLowHp.TakeDamage(45f); // lowest HP% → focus pick
        actor.SetPosition(new CombatVector2(0f, 0f));
        focusLowHp.SetPosition(new CombatVector2(1.3f, 0f));   // slightly farther...
        nearFullHp.SetPosition(new CombatVector2(1.1f, 0.4f)); // ...than this full-HP enemy, but within the focus band

        var state = MakeState(new[] { actor }, new[] { focusLowHp, nearFullHp });
        var selected = TargetScoringService.SelectTarget(state, actor, TargetSelectorType.LowestHpEnemy, BattleActionType.BasicAttack, null);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected!.Id.Value, Is.EqualTo("focus_lowhp"), "a low-HP focus that is near-equidistant to the nearest is finished off (local focus-fire)");
    }

    [Test]
    public void Ranged_KeepsFocus_NoNearestOverride()
    {
        var ranger = MakeUnit("ranger", TeamSide.Ally, classId: "ranger", attackRange: 5.6f);
        var farLowHp = MakeUnit("far_lowhp", TeamSide.Enemy, hp: 50f);
        var nearFullHp = MakeUnit("near_fullhp", TeamSide.Enemy, hp: 50f);
        farLowHp.TakeDamage(45f);
        ranger.SetPosition(new CombatVector2(0f, 0f));
        farLowHp.SetPosition(new CombatVector2(5f, 0f));
        nearFullHp.SetPosition(new CombatVector2(1.0f, 0f));

        var state = MakeState(new[] { ranger }, new[] { farLowHp, nearFullHp });
        var selected = TargetScoringService.SelectTarget(state, ranger, TargetSelectorType.LowestHpEnemy, BattleActionType.BasicAttack, null);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected!.Id.Value, Is.EqualTo("far_lowhp"), "ranged units keep strict focus-fire (no nearest override)");
    }
}
