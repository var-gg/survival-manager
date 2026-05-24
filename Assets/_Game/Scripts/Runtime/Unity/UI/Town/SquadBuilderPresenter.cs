using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

/// <summary>
/// TacticalSetup production surface — TownSquadBuilder/SquadBuilderPresenter는 legacy implementation alias.
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
    private readonly VisualElement _operationRows;
    private readonly Label _responseSummaryLabel;
    private readonly VisualElement _synergyChips;
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
        _operationRows = Require<VisualElement>(_panelRoot, "SquadBuilderOperationRows");
        _responseSummaryLabel = Require<Label>(_panelRoot, "SquadBuilderResponseSummaryLabel");
        _synergyChips = Require<VisualElement>(_panelRoot, "SquadBuilderSynergyChips");

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
        FindModalOverlay()?.BringToFront();
        _modalRoot.BringToFront();
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
        var wrapper = FindModalOverlay();
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
                heroLabel = ResolveHeroDisplayName(hero);
            }
            else
            {
                heroLabel = "비어있음";
            }

            entry.Button.text = $"{LocalizeAnchor(entry.Anchor)}\n{Shorten(heroLabel, 12)}";
            entry.Button.tooltip = heroLabel;
            entry.Button.EnableInClassList("sm-sqb-modal__anchor-button--selected", entry.Anchor == _selectedAnchor);
        }

        var selected = session.SelectedTeamPosture;
        foreach (var entry in _postureButtons)
        {
            entry.Button.EnableInClassList("sm-sqb-modal__posture-button--selected", entry.Posture == selected);
        }

        RenderRoster(session, loadout, anchorByHeroId);
        var selectedRow = RenderSelectedDetail(session, loadout, heroById, anchorByHeroId);
        RenderTacticalDecisionRows(session, anchorByHeroId.Count, selectedRow);
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

            var icon = new Label(row.IsDeployed ? "◆" : "◇");
            icon.AddToClassList("sm-sqb-modal__roster-icon");
            container.Add(icon);

            var copy = new VisualElement();
            copy.AddToClassList("sm-sqb-modal__roster-copy");

            var name = new Label(row.DisplayName);
            name.AddToClassList("sm-sqb-modal__roster-name");
            copy.Add(name);

            var meta = new Label($"{Shorten(row.MetaLabel, 22)} · {row.DeploymentLabel}");
            meta.AddToClassList("sm-sqb-modal__roster-meta");
            copy.Add(meta);

            var pips = new VisualElement();
            pips.AddToClassList("sm-sqb-modal__roster-pips");
            for (var i = 0; i < 5; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("sm-sqb-modal__roster-pip");
                pip.EnableInClassList("sm-sqb-modal__roster-pip--on", i < ResolveRosterPipCount(row));
                pips.Add(pip);
            }
            copy.Add(pips);
            container.Add(copy);

            _rosterList.Add(container);
        }
    }

    private static int ResolveRosterPipCount(SquadBuilderHeroRow row)
    {
        if (row.IsDeployed) return 5;
        return row.DeploymentLabel == "원정 후보" ? 4 : 3;
    }

    private SquadBuilderHeroRow? RenderSelectedDetail(
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
            return null;
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
        return row;
    }

    private void RenderTacticalDecisionRows(GameSessionState session, int deployedCount, SquadBuilderHeroRow? selectedRow)
    {
        _operationRows.Clear();
        _synergyChips.Clear();

        AddOperationRow("전열", selectedRow?.DeploymentLabel ?? LocalizeAnchor(_selectedAnchor));
        AddOperationRow("역할", selectedRow?.RoleLabel ?? "선택 없음");
        AddOperationRow("거리", selectedRow?.RangeLabel ?? "기본 교전 거리");
        AddOperationRow("편성", $"배치 {deployedCount}/6 · 원정 {session.ExpeditionSquadHeroIds.Count}/4");

        _responseSummaryLabel.text =
            $"{LocalizePosture(session.SelectedTeamPosture)} 기준. 확정 전투 예측이 아니라 현재 편성/콘텐츠 read model의 대응 힌트입니다.";

        AddDetailChip(_synergyChips, LocalizePosture(session.SelectedTeamPosture));
        if (selectedRow == null)
        {
            AddDetailChip(_synergyChips, "선택 없음");
            AddDetailChip(_synergyChips, "가짜 수치 없음");
            return;
        }

        AddDetailChip(_synergyChips, selectedRow.RoleLabel);
        AddDetailChip(_synergyChips, selectedRow.FormationLabel);
        AddDetailChip(_synergyChips, selectedRow.BiasLabel);
    }

    private void AddOperationRow(string key, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-sqb-modal__operation-row");

        var keyLabel = new Label(key);
        keyLabel.AddToClassList("sm-sqb-modal__operation-key");
        row.Add(keyLabel);

        var valueLabel = new Label(value);
        valueLabel.AddToClassList("sm-sqb-modal__operation-value");
        row.Add(valueLabel);

        _operationRows.Add(row);
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
        var operationAnchor = anchorByHeroId.TryGetValue(hero.HeroId, out var assignedAnchor)
            ? assignedAnchor
            : DeploymentAnchorId.FrontCenter;
        var activeBlueprint = ResolveActiveBlueprint(session);
        var roleInstructionId = ResolveRoleInstructionId(hero, operationAnchor, activeBlueprint);
        var fallbackRoleTag = ResolveDefaultRoleTag(hero.ClassId, operationAnchor);
        RoleInstructionDefinition? roleInstruction = null;
        if (!string.IsNullOrWhiteSpace(roleInstructionId)
            && _root.CombatContentLookup.TryGetRoleInstructionDefinition(roleInstructionId, out var resolvedRole))
        {
            roleInstruction = resolvedRole;
        }

        var behaviorProfile = ResolveBehaviorProfile(hero);
        var roleLabel = _contentText.GetRoleName(roleInstructionId, roleInstruction?.RoleTag ?? fallbackRoleTag);
        var formationLabel = LocalizeFormation(behaviorProfile?.FormationLine);
        var rangeLabel = LocalizeRange(behaviorProfile?.RangeDiscipline);
        var biasLabel = BuildBiasLabel(roleInstruction);

        return new SquadBuilderHeroRow(
            HeroId: hero.HeroId,
            DisplayName: ResolveHeroDisplayName(hero),
            MetaLabel: $"{className} / {raceName} · Lv {level} · XP {xpPct}%",
            LoadoutLabel: $"장비 {equippedItemCount} · 스킬 {equippedSkillCount} · 패시브 {passiveCount}",
            DeploymentLabel: deploymentLabel,
            RoleLabel: roleLabel,
            FormationLabel: formationLabel,
            RangeLabel: rangeLabel,
            BiasLabel: biasLabel,
            RarityLabel: hero.RecruitTier.ToString().ToLowerInvariant(),
            IsDeployed: anchorByHeroId.ContainsKey(hero.HeroId));
    }

    private SquadBlueprintRecord? ResolveActiveBlueprint(GameSessionState session)
    {
        return session.Profile.SquadBlueprints.FirstOrDefault(record =>
                   string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
               ?? session.Profile.SquadBlueprints.FirstOrDefault();
    }

    private string ResolveRoleInstructionId(
        HeroInstanceRecord hero,
        DeploymentAnchorId anchor,
        SquadBlueprintRecord? activeBlueprint)
    {
        if (activeBlueprint?.HeroRoleIds != null
            && activeBlueprint.HeroRoleIds.TryGetValue(hero.HeroId, out var roleInstructionId)
            && !string.IsNullOrWhiteSpace(roleInstructionId))
        {
            return roleInstructionId;
        }

        return ResolveDefaultRoleInstructionId(hero.ClassId, anchor);
    }

    private BehaviorProfileDefinition? ResolveBehaviorProfile(HeroInstanceRecord hero)
    {
        if (!string.IsNullOrWhiteSpace(hero.ArchetypeId)
            && _root.CombatContentLookup.TryGetArchetype(hero.ArchetypeId, out var archetype))
        {
            return archetype.BehaviorProfile;
        }

        if (!string.IsNullOrWhiteSpace(hero.HeroId)
            && _root.CombatContentLookup.TryGetArchetype(hero.HeroId, out var heroArchetype))
        {
            return heroArchetype.BehaviorProfile;
        }

        return null;
    }

    private static string ResolveDefaultRoleInstructionId(string classId, DeploymentAnchorId anchor)
        => ResolveDefaultRoleTag(classId, anchor);

    private static string ResolveDefaultRoleTag(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private static string BuildBiasLabel(RoleInstructionDefinition? roleInstruction)
    {
        if (roleInstruction == null)
        {
            return "기본 bias";
        }

        var protect = Mathf.Clamp01(roleInstruction.ProtectCarryBias);
        var pressure = Mathf.Clamp01(roleInstruction.BacklinePressureBias);
        var retreat = Mathf.Clamp01(roleInstruction.RetreatBias);
        if (protect >= pressure && protect >= retreat) return "보호 bias";
        if (pressure >= retreat) return "후열 압박";
        return "후퇴 성향";
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
        AddDetailChip(_selectedHeroTags, text);
    }

    private static void AddDetailChip(VisualElement parent, string text)
    {
        var tag = new Label(text);
        tag.AddToClassList("sm-sqb-modal__tag");
        parent.Add(tag);
    }

    private string ResolveHeroDisplayName(HeroInstanceRecord hero)
    {
        return !LooksLikeRawLocalizationKey(hero.Name)
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

    private VisualElement? FindModalOverlay()
    {
        for (var current = _modalRoot.parent; current != null; current = current.parent)
        {
            if (current.ClassListContains("town-hub__modal-overlay"))
            {
                return current;
            }
        }

        return _modalRoot.parent?.parent ?? _modalRoot.parent;
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..Math.Max(1, maxLength - 1)]}…";
    }

    private static bool LooksLikeRawLocalizationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("content.", StringComparison.Ordinal)
               || trimmed.StartsWith("ui.", StringComparison.Ordinal)
               || trimmed.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase);
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

    private static string LocalizeFormation(FormationLine? formation) => formation switch
    {
        FormationLine.Frontline => "전열",
        FormationLine.Midline => "중열",
        FormationLine.Backline => "후열",
        _ => "배치 기준",
    };

    private static string LocalizeRange(RangeDiscipline? range) => range switch
    {
        RangeDiscipline.Collapse => "압박 접근",
        RangeDiscipline.HoldBand => "거리 유지",
        RangeDiscipline.KiteBackward => "후퇴 카이팅",
        RangeDiscipline.SideStepHold => "측면 유지",
        RangeDiscipline.AnchorNearFrontline => "전열 근접",
        _ => "기본 교전 거리",
    };

    private sealed record SquadBuilderHeroRow(
        string HeroId,
        string DisplayName,
        string MetaLabel,
        string LoadoutLabel,
        string DeploymentLabel,
        string RoleLabel,
        string FormationLabel,
        string RangeLabel,
        string BiasLabel,
        string RarityLabel,
        bool IsDeployed);

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
