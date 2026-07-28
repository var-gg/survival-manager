using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Town.Preview;
using UnityEngine;
using UnityEngine.TestTools;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PassiveBoardPresenterFastTests
{
    [Test]
    public void ToggleRefusal_ReachesViewWithPlayerFacingReason_WithoutScene()
    {
        var presenter = CreatePresenter(SceneNames.Battle, out var view);
        var actions = (IPassiveBoardActions)presenter;

        actions.OnNodeSelected("node_1");
        actions.OnToggleActivateClicked();

        Assert.That(view.LastState, Is.Not.Null);
        Assert.That(
            view.LastState!.Detail.AvailableLabel,
            Is.EqualTo("Passive nodes can be changed only in Town."));
    }

    [Test]
    public void ToggleInvariant_RendersGenericMessageWithoutDiagnostic_WhileBoundaryStillLogs()
    {
        var presenter = CreatePresenter(SceneNames.Town, out var view);
        var actions = (IPassiveBoardActions)presenter;
        LogAssert.Expect(
            LogType.Error,
            new Regex("cause='passive\\.node_missing'.*missing-node", RegexOptions.CultureInvariant));

        actions.OnNodeSelected("missing-node");
        actions.OnToggleActivateClicked();

        Assert.That(view.LastState, Is.Not.Null);
        Assert.That(
            view.LastState!.Detail.AvailableLabel,
            Is.EqualTo("The passive node could not be changed. Please try again."));
        Assert.That(view.LastState.Detail.AvailableLabel, Does.Not.Contain("missing-node"));
        Assert.That(view.LastState.Detail.AvailableLabel, Does.Not.Contain("passive.node_missing"));
    }

    private static PassiveBoardPresenter CreatePresenter(
        string currentScene,
        out RecordingPassiveBoardView view)
    {
        var lookup = EditorFreeCombatContentFixture.CreateTownBuildLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "passive_presenter_test",
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "hero-1",
                    ArchetypeId = "vanguard_archetype",
                    RaceId = "human",
                    ClassId = "vanguard",
                    EquippedItemIds = new List<string>(),
                },
            },
            HeroLoadouts = new List<HeroLoadoutRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    PassiveBoardId = "board_vanguard",
                    SelectedPassiveNodeIds = new List<string>(),
                },
            },
        });
        session.SetCurrentScene(currentScene);

        view = new RecordingPassiveBoardView();
        var presenter = new PassiveBoardPresenter(session, lookup, view);
        presenter.Initialize();
        presenter.SetSelectedHero("hero-1");
        return presenter;
    }

    private sealed class RecordingPassiveBoardView : IPassiveBoardView
    {
        public PassiveBoardViewState? LastState { get; private set; }

        public void Bind(IPassiveBoardActions actions)
        {
        }

        public void BindClose(Action close)
        {
        }

        public void Open()
        {
        }

        public void Close()
        {
        }

        public void Render(PassiveBoardViewState state)
        {
            LastState = state;
        }
    }
}
