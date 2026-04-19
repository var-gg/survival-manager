using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using CoreEntityId = SM.Core.Ids.EntityId;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Battle;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleScreenPresenterReadabilityTests
{
    [Test]
    public void BuildState_UsesCompactSemanticStatus_AndDecisiveLog()
    {
        var go = new GameObject("BattleScreenPresenterReadabilityTests");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var presenter = new BattleScreenPresenter(localization, GameSessionTestFactory.Create(), BattlePresentationOptions.CreateDefault());
            var step = new BattleSimulationStep(
                42,
                4.2f,
                new[]
                {
                    CreateUnit("ally", TeamSide.Ally, CombatActionState.ExecuteAction, "enemy", "Enemy", 0.68f),
                    CreateUnit("enemy", TeamSide.Enemy, CombatActionState.Recover, "ally", "Ally", 0f),
                },
                new List<BattleEvent>
                {
                    new(42, 4.2f, new CoreEntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new CoreEntityId("enemy"), "Enemy", 18f)
                },
                true,
                TeamSide.Ally);

            var state = presenter.BuildState(
                step,
                recentLogs: new List<BattleEvent>(step.Events),
                decisiveTimeline: new List<string> { "3.8s | Enemy down" },
                totalEventCount: 1,
                isPaused: false,
                playbackSpeed: 1f,
                isBattleFinished: true,
                showSettings: false,
                progressNormalized: 1f,
                settingsStatusText: string.Empty,
                canReplay: true,
                canRebattle: true);

            Assert.That(state.StatusText, Does.Contain("Result"));
            Assert.That(state.ReplayLabel, Is.EqualTo("Replay"));
            Assert.That(state.LogText, Does.Contain("Enemy down"));
            Assert.That(state.AllyHpText, Does.Contain("Allies"));
            Assert.That(state.ShowPlaybackControls, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static BattleUnitReadModel CreateUnit(string id, TeamSide side, CombatActionState actionState, string? targetId, string? targetName, float windupProgress)
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: side == TeamSide.Ally ? "Ally" : "Enemy",
            Side: side,
            Anchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f),
            CurrentHealth: 24f,
            MaxHealth: 24f,
            IsAlive: true,
            ActionState: actionState,
            PendingActionType: BattleActionType.BasicAttack,
            TargetId: targetId,
            TargetName: targetName,
            WindupProgress: windupProgress,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f,
            IsDefending: false);
    }
}
