using System;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

/// <summary>
/// SquadBuilder modal V1 — anchor 6 (Front 3 + Back 3) + posture 5 편집 (audit §2.2).
/// Town hub bottom toolbar에서 진입. TacticalWorkshop과 같은 panel-overlay 패턴.
///
/// V1 동작:
/// - anchor 버튼 클릭 → SessionState.CycleDeploymentAssignment(anchor) (다음 hero로 cycle, 빈 슬롯 포함)
/// - posture 버튼 클릭 → SessionState.SetTeamPosture(posture)
/// - ESC 또는 close 버튼으로 닫기
///
/// 후속 (별도 task):
/// - drag-drop hero card → anchor 직접 배치
/// - posture별 movement preview / threat coverage hint
/// </summary>
public sealed class SquadBuilderPresenter
{
    private readonly VisualElement _panelRoot;
    private readonly GameSessionRoot _root;
    private readonly ContentTextResolver _contentText;
    private readonly VisualElement _modalRoot;
    private readonly Button _closeButton;
    private readonly Label _statusLabel;
    private readonly (DeploymentAnchorId Anchor, Button Button)[] _anchorButtons;
    private readonly (TeamPostureType Posture, Button Button)[] _postureButtons;
    private bool _isOpen;

    public SquadBuilderPresenter(VisualElement panelRoot, GameSessionRoot root, ContentTextResolver contentText)
    {
        _panelRoot = panelRoot ?? throw new ArgumentNullException(nameof(panelRoot));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));

        _modalRoot = Require<VisualElement>(_panelRoot, "SquadBuilderRoot");
        _closeButton = Require<Button>(_panelRoot, "SquadBuilderCloseButton");
        _statusLabel = Require<Label>(_panelRoot, "SquadBuilderStatusLabel");

        _anchorButtons = new[]
        {
            (DeploymentAnchorId.FrontTop, Require<Button>(_panelRoot, "SquadBuilderAnchor_FrontTop")),
            (DeploymentAnchorId.FrontCenter, Require<Button>(_panelRoot, "SquadBuilderAnchor_FrontCenter")),
            (DeploymentAnchorId.FrontBottom, Require<Button>(_panelRoot, "SquadBuilderAnchor_FrontBottom")),
            (DeploymentAnchorId.BackTop, Require<Button>(_panelRoot, "SquadBuilderAnchor_BackTop")),
            (DeploymentAnchorId.BackCenter, Require<Button>(_panelRoot, "SquadBuilderAnchor_BackCenter")),
            (DeploymentAnchorId.BackBottom, Require<Button>(_panelRoot, "SquadBuilderAnchor_BackBottom")),
        };

        _postureButtons = new[]
        {
            (TeamPostureType.HoldLine, Require<Button>(_panelRoot, "SquadBuilderPosture_HoldLine")),
            (TeamPostureType.StandardAdvance, Require<Button>(_panelRoot, "SquadBuilderPosture_StandardAdvance")),
            (TeamPostureType.ProtectCarry, Require<Button>(_panelRoot, "SquadBuilderPosture_ProtectCarry")),
            (TeamPostureType.CollapseWeakSide, Require<Button>(_panelRoot, "SquadBuilderPosture_CollapseWeakSide")),
            (TeamPostureType.AllInBackline, Require<Button>(_panelRoot, "SquadBuilderPosture_AllInBackline")),
        };

        _modalRoot.focusable = true;
        _panelRoot.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
        _closeButton.clicked += Close;
        foreach (var entry in _anchorButtons)
        {
            var anchor = entry.Anchor;
            entry.Button.clicked += () => OnAnchorClicked(anchor);
        }
        foreach (var entry in _postureButtons)
        {
            var posture = entry.Posture;
            entry.Button.clicked += () => OnPostureClicked(posture);
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

    private void OnAnchorClicked(DeploymentAnchorId anchor)
    {
        _root.SessionState.CycleDeploymentAssignment(anchor);
        _statusLabel.text = $"앵커 갱신: {LocalizeAnchor(anchor)}";
        Render();
    }

    private void OnPostureClicked(TeamPostureType posture)
    {
        _root.SessionState.SetTeamPosture(posture);
        _statusLabel.text = $"자세 갱신: {LocalizePosture(posture)}";
        Render();
    }

    private void Render()
    {
        _modalRoot.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _modalRoot.EnableInClassList("sm-sqb-modal--open", _isOpen);
        _modalRoot.EnableInClassList("sm-sqb-modal--closed", !_isOpen);
        // Foundation atom — sm-modal-anim tier 1 (modal scale + opacity transition).
        // 닫혀 있을 때만 enter class 적용 (opacity 0 + scale 0.96), 열릴 때 제거 → ease-out transition.
        _modalRoot.EnableInClassList("sm-modal-anim--enter", !_isOpen);

        if (!_isOpen) return;

        var session = _root.SessionState;
        var loadout = _root.ProfileQueries.GetLoadoutView(_root.ActiveProfileId);
        var heroById = session.Profile.Heroes.ToDictionary(h => h.HeroId, StringComparer.Ordinal);

        foreach (var entry in _anchorButtons)
        {
            var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == entry.Anchor);
            var heroId = deployment?.HeroId ?? string.Empty;
            string heroLabel;
            if (!string.IsNullOrEmpty(heroId) && heroById.TryGetValue(heroId, out var hero))
            {
                heroLabel = !string.IsNullOrEmpty(hero.Name)
                    ? hero.Name
                    : _contentText.GetArchetypeName(hero.HeroId);
            }
            else
            {
                heroLabel = "비어있음";
            }

            entry.Button.text = $"{LocalizeAnchor(entry.Anchor)}\n{heroLabel}";
        }

        var selected = session.SelectedTeamPosture;
        foreach (var entry in _postureButtons)
        {
            entry.Button.EnableInClassList("sm-sqb-modal__posture-button--selected", entry.Posture == selected);
        }
    }

    private void HandleKeyDown(KeyDownEvent evt)
    {
        if (!_isOpen || evt.keyCode != KeyCode.Escape) return;
        Close();
        evt.StopPropagation();
    }

    private static string LocalizeAnchor(DeploymentAnchorId anchor) => anchor switch
    {
        DeploymentAnchorId.FrontTop => "전열 상",
        DeploymentAnchorId.FrontCenter => "전열 중",
        DeploymentAnchorId.FrontBottom => "전열 하",
        DeploymentAnchorId.BackTop => "후열 상",
        DeploymentAnchorId.BackCenter => "후열 중",
        DeploymentAnchorId.BackBottom => "후열 하",
        _ => anchor.ToString(),
    };

    private static string LocalizePosture(TeamPostureType posture) => posture switch
    {
        TeamPostureType.HoldLine => "전열 사수",
        TeamPostureType.StandardAdvance => "표준 전진",
        TeamPostureType.ProtectCarry => "캐리 보호",
        TeamPostureType.CollapseWeakSide => "약측 무너뜨리기",
        TeamPostureType.AllInBackline => "후열 깊이 침투",
        _ => posture.ToString(),
    };

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
