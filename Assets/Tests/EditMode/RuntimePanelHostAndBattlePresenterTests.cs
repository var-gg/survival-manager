using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI;
using SM.Unity.UI.Battle;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class RuntimePanelHostAndBattlePresenterTests
{
    [Test]
    public void RuntimePanelHost_SetVisible_DoesNotRebuildRoot()
    {
        var go = new GameObject("RuntimePanelHostTest");
        try
        {
            var host = go.AddComponent<RuntimePanelHost>();

            host.EnsureReady();
            var initialRoot = host.Root;
            var initialBuildCount = host.RootBuildCount;

            host.SetVisible(false);
            Assert.That(host.Root.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(host.Root.visible, Is.False);

            host.SetVisible(true);
            Assert.That(host.Root.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(host.Root.visible, Is.True);
            Assert.That(host.RootBuildCount, Is.EqualTo(initialBuildCount));
            Assert.That(ReferenceEquals(initialRoot, host.Root), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BattleScreenPresenter_BuildLoadingState_UsesFallbackLabels()
    {
        var go = new GameObject("BattlePresenterLocalization");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var session = new GameSessionState(new FakeCombatContentLookup());
            var presenter = new BattleScreenPresenter(localization, session, BattlePresentationOptions.CreateDefault());

            var state = presenter.BuildLoadingState();

            Assert.That(state.Title, Is.EqualTo("Battle"));
            Assert.That(state.LocaleKoLabel, Is.EqualTo("한국어"));
            Assert.That(state.LocaleEnLabel, Is.EqualTo("English"));
            Assert.That(state.Settings.OverheadLabel.Contains("ui.battle"), Is.False);
            Assert.That(state.Settings.DebugOverlayLabel.Contains("ui.battle"), Is.False);
            Assert.That(state.HelpButtonLabel, Is.EqualTo("Help"));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LocalizationController_Reports_Missing_Key_After_Initialization()
    {
        var go = new GameObject("LocalizationControllerMissingKey");
        try
        {
            var controller = go.AddComponent<GameLocalizationController>();
            SetInitialized(controller, true);

            LogAssert.Expect(LogType.Warning, "[Localization] Missing localized entry 'UI_Battle:ui.battle.missing_test'. Player-facing UI is using fallback text.");

            var localized = controller.LocalizeOrFallback(GameLocalizationTables.UIBattle, "ui.battle.missing_test", "Fallback Label");

            Assert.That(localized, Is.EqualTo("Fallback Label"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BattleScreenPresenter_BuildState_RendersCombatSummary_WithoutRawLocalizationKeys()
    {
        var go = new GameObject("BattlePresenterState");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var session = new GameSessionState(new FakeCombatContentLookup());
            session.SetCurrentScene(SceneNames.Battle);
            var presenter = new BattleScreenPresenter(localization, session, BattlePresentationOptions.CreateDefault());

            var step = CreateBattleStep();
            var recentLogs = new List<BattleEvent>
            {
                new(
                    StepIndex: 1,
                    TimeSeconds: 0.5f,
                    ActorId: new SM.Core.Ids.EntityId("ally"),
                    ActorName: "Ally",
                    ActionType: BattleActionType.BasicAttack,
                    LogCode: BattleLogCode.BasicAttackDamage,
                    TargetId: new SM.Core.Ids.EntityId("enemy"),
                    TargetName: "Enemy",
                    Value: 12f)
            };

            var state = presenter.BuildState(
                step,
                recentLogs,
                decisiveTimeline: new List<string>(),
                totalEventCount: recentLogs.Count,
                isPaused: false,
                playbackSpeed: 2f,
                isBattleFinished: false,
                showSettings: true,
                progressNormalized: 0.25f,
                settingsStatusText: string.Empty,
                canReplay: true,
                canRebattle: false);

            Assert.That(state.LogText, Does.Contain("hit"));
            Assert.That(state.LogText.Contains("combat.log.damage"), Is.False);
            Assert.That(state.SpeedText, Is.EqualTo("Authored Expedition Battle"));
            Assert.That(state.StatusText.Contains("ui.battle.status"), Is.False);
            Assert.That(state.Settings.TeamSummaryLabel, Does.Contain("Team Summary"));
            Assert.That(state.Settings.IsVisible, Is.True);
            Assert.That(state.AllyHpText, Does.Contain("Allies 1/1"));
            Assert.That(state.ShowPlaybackControls, Is.False);
            Assert.That(state.ShowSmokeActions, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BattleScreenPresenter_NormalLane_Blocks_DirectReturn_And_Rebattle()
    {
        var go = new GameObject("BattlePresenterRunLoop");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var session = new GameSessionState(new FakeCombatContentLookup());
            var presenter = new BattleScreenPresenter(localization, session, BattlePresentationOptions.CreateDefault());

            var finishedState = presenter.BuildState(
                CreateBattleStep(),
                recentLogs: new List<BattleEvent>(),
                decisiveTimeline: new List<string>(),
                totalEventCount: 0,
                isPaused: false,
                playbackSpeed: 1f,
                isBattleFinished: true,
                showSettings: false,
                progressNormalized: 1f,
                settingsStatusText: string.Empty,
                canReplay: true,
                canRebattle: false);

            Assert.That(finishedState.CanRebattle, Is.False);
            Assert.That(finishedState.CanReplay, Is.True);
            Assert.That(finishedState.CanReturnTownDirect, Is.False);
            Assert.That(finishedState.ShowPlaybackControls, Is.False);
            Assert.That(finishedState.ShowSmokeActions, Is.False);

            session.PrepareQuickBattleSmoke();
            finishedState = presenter.BuildState(
                CreateBattleStep(),
                recentLogs: new List<BattleEvent>(),
                decisiveTimeline: new List<string>(),
                totalEventCount: 0,
                isPaused: false,
                playbackSpeed: 1f,
                isBattleFinished: true,
                showSettings: false,
                progressNormalized: 1f,
                settingsStatusText: string.Empty,
                canReplay: true,
                canRebattle: true);

            Assert.That(finishedState.CanRebattle, Is.True);
            Assert.That(finishedState.CanReturnTownDirect, Is.True);
            Assert.That(finishedState.ShowPlaybackControls, Is.True);
            Assert.That(finishedState.ShowSmokeActions, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static BattleSimulationStep CreateBattleStep()
    {
        var state = BattleFactory.Create(
            new[] { CreateLoadout("ally", TeamSide.Ally, "Ally") },
            new[] { CreateLoadout("enemy", TeamSide.Enemy, "Enemy") });
        var simulator = new BattleSimulator(state, BattleSimulator.DefaultMaxSteps);
        return simulator.CurrentStep;
    }

    private static void SetInitialized(GameLocalizationController controller, bool value)
    {
        var backingField = typeof(GameLocalizationController).GetField("<IsInitialized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(backingField, Is.Not.Null);
        backingField!.SetValue(controller, value);
    }

    private static BattleUnitLoadout CreateLoadout(string id, TeamSide side, string name)
    {
        return new BattleUnitLoadout(
            Id: id,
            Name: name,
            RaceId: "human",
            ClassId: side == TeamSide.Ally ? "vanguard" : "ranger",
            PreferredAnchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            BaseStats: new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 100f,
                [StatKey.PhysPower] = 12f,
                [StatKey.AttackRange] = 1.5f,
                [StatKey.MoveSpeed] = 2f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.5f,
                [StatKey.LeashDistance] = 6f,
            },
            RuleChains: new[]
            {
                new UnitRuleChain(
                    "basic",
                    new[]
                    {
                        new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy)
                    })
            },
            Skills: new BattleSkillSpec[0]);
    }
}
