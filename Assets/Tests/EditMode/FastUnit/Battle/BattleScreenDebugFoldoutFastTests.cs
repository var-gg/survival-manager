using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Battle;
using UnityEngine;

namespace SM.Tests.EditMode.FastUnit.Battle;

[Category("BatchOnly")]
public sealed class BattleScreenDebugFoldoutFastTests
{
    [Test]
    public void BuildDebugFoldoutState_NoActiveRun_ReturnsEmptySentinel()
    {
        var go = new GameObject("BattleScreenDebugFoldoutFastTests_NoRun");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var session = GameSessionTestFactory.Create();
            var presenter = new BattleScreenPresenter(localization, session, BattlePresentationOptions.CreateDefault());

            var state = presenter.BuildDebugFoldoutState();

            Assert.That(state, Is.Not.Null);
            Assert.That(state.EncounterId, Is.EqualTo("-"));
            Assert.That(state.SiteNodeIndexText, Is.EqualTo("-"));
            Assert.That(state.BattleContextHash, Is.EqualTo("-"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BuildDebugFoldoutState_WithOverlayTrace_RendersTraceFields()
    {
        var go = new GameObject("BattleScreenDebugFoldoutFastTests_WithRun");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var session = GameSessionTestFactory.Create();
            var blueprint = new SquadBlueprintState(
                BlueprintId: "blueprint.test",
                DisplayName: "Debug Foldout Test",
                TeamPosture: TeamPostureType.StandardAdvance,
                TeamTacticId: string.Empty,
                DeploymentAssignments: new Dictionary<DeploymentAnchorId, string>(),
                ExpeditionSquadHeroIds: System.Array.Empty<string>(),
                HeroRoleIds: new Dictionary<string, string>());
            var activeRun = RunStateService.StartRun("exp-test", blueprint, isQuickBattle: false);
            activeRun = activeRun with
            {
                Overlay = activeRun.Overlay with
                {
                    SiteNodeIndex = 3,
                    EncounterId = "enc-authored-foo",
                    BattleContextHash = "abc123def456"
                }
            };

            typeof(GameSessionState)
                .GetProperty("ActiveRun", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(session, activeRun);

            var presenter = new BattleScreenPresenter(localization, session, BattlePresentationOptions.CreateDefault());
            var state = presenter.BuildDebugFoldoutState();

            Assert.That(state.EncounterId, Is.EqualTo("enc-authored-foo"));
            Assert.That(state.SiteNodeIndexText, Is.EqualTo("3"));
            Assert.That(state.BattleContextHash, Is.EqualTo("abc123def456"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
