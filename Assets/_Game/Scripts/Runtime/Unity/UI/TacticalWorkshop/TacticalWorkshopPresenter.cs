using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.TacticalWorkshop;

public sealed class TacticalWorkshopPresenter
{
    private readonly VisualElement _panelRoot;
    private readonly VisualElement _modalRoot;
    private readonly Label _eyebrowLabel;
    private readonly Label _titleLabel;
    private readonly Button _closeButton;
    private readonly IReadOnlyList<Button> _postureButtons;
    private readonly IReadOnlyList<Label> _postureTitles;
    private readonly IReadOnlyList<Label> _postureBodies;
    private readonly IReadOnlyList<Button> _scenarioTabs;
    private readonly IReadOnlyList<VisualElement> _scenarioPreviews;
    private readonly IReadOnlyList<Label> _tacticTexts;

    private int _selectedPostureIndex = 1;
    private int _selectedScenarioIndex;
    private bool _isOpen;

    public TacticalWorkshopPresenter(VisualElement panelRoot)
    {
        _panelRoot = panelRoot ?? throw new ArgumentNullException(nameof(panelRoot));
        _modalRoot = Require<VisualElement>(_panelRoot, "TacticalWorkshopRoot");
        _eyebrowLabel = Require<Label>(_panelRoot, "TacticalWorkshopEyebrowLabel");
        _titleLabel = Require<Label>(_panelRoot, "TacticalWorkshopTitleLabel");
        _closeButton = Require<Button>(_panelRoot, "TacticalWorkshopCloseButton");

        var initialState = TacticalWorkshopViewState.CreateDefault(_selectedPostureIndex, _selectedScenarioIndex);
        _postureButtons = BuildElements<Button, TacticalWorkshopPostureViewState>(initialState.Postures, posture => posture.ButtonName);
        _postureTitles = BuildElements<Label, TacticalWorkshopPostureViewState>(initialState.Postures, posture => posture.TitleName);
        _postureBodies = BuildElements<Label, TacticalWorkshopPostureViewState>(initialState.Postures, posture => posture.BodyName);
        _scenarioTabs = BuildElements<Button, TacticalWorkshopScenarioViewState>(initialState.Scenarios, scenario => scenario.TabName);
        _scenarioPreviews = BuildElements<VisualElement, TacticalWorkshopScenarioViewState>(initialState.Scenarios, scenario => scenario.PreviewName);
        _tacticTexts = BuildElements<Label, TacticalWorkshopTacticStripViewState>(initialState.Tactics, tactic => tactic.TextName);

        _modalRoot.focusable = true;
        _panelRoot.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
        _closeButton.clicked += Close;

        for (var i = 0; i < _postureButtons.Count; i++)
        {
            var index = i;
            _postureButtons[i].clicked += () => SelectPosture(index);
        }

        for (var i = 0; i < _scenarioTabs.Count; i++)
        {
            var index = i;
            _scenarioTabs[i].clicked += () => SelectScenario(index);
        }

        Render();
    }

    public void Open()
    {
        _isOpen = true;
        Render();
        _modalRoot.Focus();
    }

    public void Close()
    {
        _isOpen = false;
        Render();
    }

    private void SelectPosture(int index)
    {
        _selectedPostureIndex = index;
        Render();
    }

    private void SelectScenario(int index)
    {
        _selectedScenarioIndex = index;
        Render();
    }

    private void Render()
    {
        var state = TacticalWorkshopViewState.CreateDefault(_selectedPostureIndex, _selectedScenarioIndex);
        _modalRoot.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _modalRoot.EnableInClassList("sm-tw-modal--open", _isOpen);
        _modalRoot.EnableInClassList("sm-tw-modal--closed", !_isOpen);

        _eyebrowLabel.text = state.Eyebrow;
        _titleLabel.text = state.Title;

        for (var i = 0; i < state.Postures.Count; i++)
        {
            var posture = state.Postures[i];
            _postureButtons[i].EnableInClassList(TacticalWorkshopViewState.SelectedPostureClass, posture.IsSelected);
            _postureTitles[i].text = posture.Title;
            _postureBodies[i].text = posture.Body;
        }

        for (var i = 0; i < state.Scenarios.Count; i++)
        {
            var scenario = state.Scenarios[i];
            _scenarioTabs[i].text = scenario.Label;
            _scenarioTabs[i].EnableInClassList(TacticalWorkshopViewState.SelectedScenarioTabClass, scenario.IsSelected);
            _scenarioPreviews[i].EnableInClassList(TacticalWorkshopViewState.SelectedPreviewClass, scenario.IsSelected);
        }

        for (var i = 0; i < state.Tactics.Count; i++)
        {
            _tacticTexts[i].text = state.Tactics[i].Text;
        }
    }

    private void HandleKeyDown(KeyDownEvent evt)
    {
        if (!_isOpen || evt.keyCode != KeyCode.Escape)
        {
            return;
        }

        Close();
        evt.StopPropagation();
    }

    private IReadOnlyList<T> BuildElements<T, TState>(
        IReadOnlyList<TState> states,
        Func<TState, string> nameSelector)
        where T : VisualElement
    {
        var elements = new T[states.Count];
        for (var i = 0; i < states.Count; i++)
        {
            elements[i] = Require<T>(_panelRoot, nameSelector(states[i]));
        }

        return elements;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
