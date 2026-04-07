using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattleCameraFramingPolicyTests
{
    [Test]
    public void BootstrapFrame_IsWiderThan_PassiveFrame_ForSameStep()
    {
        var step = new BattleSimulationStep(
            0,
            0f,
            new[]
            {
                CreateUnit("ally", TeamSide.Ally, new CombatVector2(-2f, 0f)),
                CreateUnit("enemy", TeamSide.Enemy, new CombatVector2(2f, 0f)),
            },
            System.Array.Empty<BattleEvent>(),
            false,
            null);

        var policy = new BattleCameraFramingPolicy();
        var bootstrap = policy.BuildBootstrapFrame(step);
        var passive = policy.BuildPassiveFrame(step);

        Assert.That(bootstrap.ZoomHeight, Is.GreaterThan(passive.ZoomHeight));
    }

    [Test]
    public void PassiveFrame_IgnoresDeadOutlier()
    {
        var step = new BattleSimulationStep(
            0,
            0f,
            new[]
            {
                CreateUnit("ally", TeamSide.Ally, new CombatVector2(-1f, 0f)),
                CreateUnit("enemy", TeamSide.Enemy, new CombatVector2(1f, 0f)),
                CreateUnit("dead", TeamSide.Enemy, new CombatVector2(99f, 0f), isAlive: false),
            },
            System.Array.Empty<BattleEvent>(),
            false,
            null);

        var frame = new BattleCameraFramingPolicy().BuildPassiveFrame(step);

        Assert.That(frame.GroundCenter.x, Is.EqualTo(0f).Within(1.5f));
    }

    private static BattleUnitReadModel CreateUnit(string id, TeamSide side, CombatVector2 position, bool isAlive = true)
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: id,
            Side: side,
            Anchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: position,
            CurrentHealth: isAlive ? 20f : 0f,
            MaxHealth: 20f,
            IsAlive: isAlive,
            ActionState: isAlive ? CombatActionState.AcquireTarget : CombatActionState.Dead,
            PendingActionType: BattleActionType.BasicAttack,
            TargetId: null,
            TargetName: null,
            WindupProgress: 0f,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f);
    }
}
