using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
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
    private readonly Label _rosterCountLabel;
    private readonly VisualElement _rosterList;
    private readonly Label _selectedAnchorLabel;
    private readonly Label _selectedHeroName;
    private readonly Label _selectedHeroMeta;
    private readonly Label _selectedHeroLoadout;
    private readonly VisualElement _selectedHeroTags;
    private readonly (DeploymentAnchorId Anchor, Button Button)[] _anchorButtons;
    private readonly (TeamPostureType Posture, Button Button)[] _postureButtons;
    private DeploymentAnchorId _selectedAnchor = DeploymentAnchorId.FrontCenter;
    private string _statusText = "편성 상태를 확인하세요.";
    private bool _isOpen;

    public SquadBuilderPresenter(VisualElement panelRoot, GameSessionRoot root, ContentTextResolver contentText)
    {
        _panelRoot = panelRoot ?? throw new ArgumentNullException(nameof(panelRoot));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));

        _modalRoot = Require<VisualElement>(_panelRoot, "SquadBuilderRoot");
        _closeButton = Require<Button>(_panelRoot, "SquadBuilderCloseButton");
        _statusLabel = Require<Label>(_panelRoot, "SquadBuilderStatusLabel");
        _rosterCountLabel = Require<Label>(_panelRoot, "SquadBuilderRosterCountLabel");
        _rosterList = Require<VisualElement>(_panelRoot, "SquadBuilderRosterList");
        _selectedAnchorLabel = Require<Label>(_panelRoot, "SquadBuilderSelectedAnchorLabel");
        _selectedHeroName = Require<Label>(_panelRoot, "SquadBuilderSelectedHeroName");
        _selectedHeroMeta = Require<Label>(_panelRoot, "SquadBuilderSelectedHeroMeta");
        _selectedHeroLoadout = Require<Label>(_panelRoot, "SquadBuilderSelectedHeroLoadout");
        _selectedHeroTags = Require<VisualElement>(_panelRoot, "SquadBuilderSelectedHeroTags");

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
        _selectedAnchor = anchor;
        _root.SessionState.CycleDeploymentAssignment(anchor);
        _statusText = $"배치 갱신: {LocalizeAnchor(anchor)}";
        Render();
    }

    private void OnPostureClicked(TeamPostureType posture)
    {
        _root.SessionState.SetTeamPosture(posture);
        _statusText = $"팀 태세 갱신: {LocalizePosture(posture)}";
        Render();
    }

    private void Render()
    {
        _modalRoot.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _modalRoot.EnableInClassList("sm-sqb-modal--open", _isOpen);
        _modalRoot.EnableInClassList("sm-sqb-modal--closed", !_isOpen);
        _modalRoot.EnableInClassList("sm-modal-anim--enter", !_isOpen);
        // hub의 town-hub__modal-overlay wrapper도 토글 — UXML inline display:none 강제 default closed 우회.
        var wrapper = _modalRoot.parent?.parent;
        if (wrapper != null) wrapper.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (!_isOpen) return;

        var session = _root.SessionState;
        var loadout = _root.ProfileQueries.GetLoadoutView(_root.ActiveProfileId);
        var heroById = session.Profile.Heroes.ToDictionary(h => h.HeroId, StringComparer.Ordinal);
        var anchorByHeroId = BuildAnchorByHeroId(loadout);

        foreach (var entry in _anchorButtons)
        {
            var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == entry.Anchor);
            var heroId = deployment?.HeroId ?? string.Empty;
            string heroLabel;
            if (!string.IsNullOrEmpty(heroId) && heroById.TryGetValue(heroId, out var hero))
            {
                heroLabel = !string.IsNullOrEmpty(hero.Name)
                    ? hero.Name
                    : ResolveHeroArchetypeName(hero);
            }
            else
            {
                heroLabel = "비어있음";
            }

            entry.Button.text = $"{LocalizeAnchor(entry.Anchor)}\n{heroLabel}";
            entry.Button.EnableInClassList("sm-sqb-modal__anchor-button--selected", entry.Anchor == _selectedAnchor);
        }

        var selected = session.SelectedTeamPosture;
        foreach (var entry in _postureButtons)
        {
            entry.Button.EnableInClassList("sm-sqb-modal__posture-button--selected", entry.Posture == selected);
        }

        RenderRoster(session, loadout, anchorByHeroId);
        RenderSelectedDetail(session, loadout, heroById, anchorByHeroId);
        _statusLabel.text = $"{_statusText} · 현재 팀 태세: {LocalizePosture(selected)}";
    }

    private void RenderRoster(
        GameSessionState session,
        SM.Meta.Model.LoadoutView? loadout,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId)
    {
        _rosterList.Clear();
        var expeditionIds = loadout?.ExpeditionSquadHeroIds ?? Array.Empty<string>();
        var expeditionSet = new HashSet<string>(expeditionIds, StringComparer.Ordinal);
        var rows = session.Profile.Heroes
            .OrderByDescending(hero => anchorByHeroId.ContainsKey(hero.HeroId))
            .ThenByDescending(hero => expeditionSet.Contains(hero.HeroId))
            .ThenBy(hero => ResolveHeroDisplayName(hero), StringComparer.Ordinal)
            .Select(hero => BuildHeroRow(session, hero, anchorByHeroId, expeditionSet))
            .ToArray();

        _rosterCountLabel.text = $"· {rows.Length}";
        foreach (var row in rows)
        {
            var container = new VisualElement { name = $"SquadBuilderRosterRow_{SanitizeName(row.HeroId)}" };
            container.AddToClassList("sm-sqb-modal__roster-row");
            container.EnableInClassList("sm-sqb-modal__roster-row--deployed", row.IsDeployed);

            var name = new Label(row.DisplayName);
            name.AddToClassList("sm-sqb-modal__roster-name");
            container.Add(name);

            var meta = new Label($"{row.MetaLabel} · {row.DeploymentLabel}");
            meta.AddToClassList("sm-sqb-modal__roster-meta");
            container.Add(meta);

            _rosterList.Add(container);
        }
    }

    private void RenderSelectedDetail(
        GameSessionState session,
        SM.Meta.Model.LoadoutView? loadout,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId)
    {
        var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == _selectedAnchor);
        var heroId = deployment?.HeroId ?? string.Empty;
        _selectedAnchorLabel.text = $"선택 앵커 · {LocalizeAnchor(_selectedAnchor)}";
        _selectedHeroTags.Clear();

        if (string.IsNullOrWhiteSpace(heroId) || !heroById.TryGetValue(heroId, out var hero))
        {
            _selectedHeroName.text = "비어있음";
            _selectedHeroMeta.text = "이 anchor에는 hero가 없습니다.";
            _selectedHeroLoadout.text = "formation board의 anchor를 누르면 기존 순환 규칙으로 배치가 갱신됩니다.";
            AddDetailTag("empty");
            AddDetailTag(LocalizePosture(session.SelectedTeamPosture));
            return;
        }

        var row = BuildHeroRow(
            session,
            hero,
            anchorByHeroId,
            new HashSet<string>(loadout?.ExpeditionSquadHeroIds ?? Array.Empty<string>(), StringComparer.Ordinal));
        _selectedHeroName.text = row.DisplayName;
        _selectedHeroMeta.text = row.MetaLabel;
        _selectedHeroLoadout.text = row.LoadoutLabel;
        AddDetailTag(row.DeploymentLabel);
        AddDetailTag(row.RarityLabel);
        AddDetailTag($"팀 태세 {LocalizePosture(session.SelectedTeamPosture)}");
    }

    private SquadBuilderHeroRow BuildHeroRow(
        GameSessionState session,
        HeroInstanceRecord hero,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        HashSet<string> expeditionSet)
    {
        var progression = session.Profile.HeroProgressions
            .FirstOrDefault(entry => string.Equals(entry.HeroId, hero.HeroId, StringComparison.Ordinal));
        var loadout = session.Profile.HeroLoadouts
            .FirstOrDefault(entry => string.Equals(entry.HeroId, hero.HeroId, StringComparison.Ordinal));
        var level = progression?.Level ?? 1;
        var xpPct = progression != null ? Mathf.Clamp(progression.Experience % 100, 0, 100) : 0;
        var equippedItemCount = loadout?.EquippedItemInstanceIds.Count(id => !string.IsNullOrWhiteSpace(id))
            ?? hero.EquippedItemIds.Count(id => !string.IsNullOrWhiteSpace(id));
        var equippedSkillCount = loadout?.EquippedSkillInstanceIds.Count(id => !string.IsNullOrWhiteSpace(id)) ?? 0;
        var passiveCount = loadout?.SelectedPassiveNodeIds.Count(id => !string.IsNullOrWhiteSpace(id)) ?? 0;
        var className = string.IsNullOrWhiteSpace(hero.ClassId) ? "class 미정" : _contentText.GetClassName(hero.ClassId);
        var raceName = string.IsNullOrWhiteSpace(hero.RaceId) ? "race 미정" : _contentText.GetRaceName(hero.RaceId);
        var deploymentLabel = anchorByHeroId.TryGetValue(hero.HeroId, out var anchor)
            ? LocalizeAnchor(anchor)
            : expeditionSet.Contains(hero.HeroId)
                ? "원정 후보"
                : "대기";

        return new SquadBuilderHeroRow(
            HeroId: hero.HeroId,
            DisplayName: ResolveHeroDisplayName(hero),
            MetaLabel: $"{className} / {raceName} · Lv {level} · XP {xpPct}%",
            LoadoutLabel: $"장비 {equippedItemCount} · 스킬 {equippedSkillCount} · 패시브 {passiveCount}",
            DeploymentLabel: deploymentLabel,
            RarityLabel: hero.RecruitTier.ToString().ToLowerInvariant(),
            IsDeployed: anchorByHeroId.ContainsKey(hero.HeroId));
    }

    private static IReadOnlyDictionary<string, DeploymentAnchorId> BuildAnchorByHeroId(SM.Meta.Model.LoadoutView? loadout)
    {
        var result = new Dictionary<string, DeploymentAnchorId>(StringComparer.Ordinal);
        if (loadout == null) return result;

        foreach (var deployment in loadout.Deployments)
        {
            if (!string.IsNullOrWhiteSpace(deployment.HeroId))
            {
                result[deployment.HeroId] = deployment.Anchor;
            }
        }

        return result;
    }

    private void AddDetailTag(string text)
    {
        var tag = new Label(text);
        tag.AddToClassList("sm-sqb-modal__tag");
        _selectedHeroTags.Add(tag);
    }

    private string ResolveHeroDisplayName(HeroInstanceRecord hero)
    {
        return !string.IsNullOrWhiteSpace(hero.Name)
            ? hero.Name
            : ResolveHeroArchetypeName(hero);
    }

    private string ResolveHeroArchetypeName(HeroInstanceRecord hero)
    {
        return !string.IsNullOrWhiteSpace(hero.ArchetypeId)
            ? _contentText.GetArchetypeName(hero.ArchetypeId)
            : hero.HeroId;
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "empty";
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
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

    private sealed record SquadBuilderHeroRow(
        string HeroId,
        string DisplayName,
        string MetaLabel,
        string LoadoutLabel,
        string DeploymentLabel,
        string RarityLabel,
        bool IsDeployed);

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
