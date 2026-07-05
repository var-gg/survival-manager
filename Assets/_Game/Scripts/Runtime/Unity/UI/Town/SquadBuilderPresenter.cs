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
    // CharacterId → 초상화 흉상 Texture. null이면 이니셜 글리프 fallback. (다른 형제 surface와 동일 패턴)
    private readonly Func<string, Texture2D?>? _portraitSprite;
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
    // wave-58 mockup-align header chip strip + footer CTA (optional, UXML 미존재 시 null fallback)
    private readonly Label? _deploymentChip;
    private readonly Label? _postureChip;
    private readonly Label? _riskChip;
    private readonly Button? _resetButton;
    private readonly Button? _confirmButton;
    private readonly (DeploymentAnchorId Anchor, Button Button)[] _anchorButtons;
    private readonly (TeamPostureType Posture, Button Button)[] _postureButtons;
    private DeploymentAnchorId _selectedAnchor = DeploymentAnchorId.FrontCenter;
    private string _statusText = "편성 상태를 확인하세요.";
    private bool _isOpen;

    public SquadBuilderPresenter(
        VisualElement panelRoot,
        GameSessionRoot root,
        ContentTextResolver contentText,
        Func<string, Texture2D?>? portraitSprite = null)
    {
        _panelRoot = panelRoot ?? throw new ArgumentNullException(nameof(panelRoot));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _portraitSprite = portraitSprite;

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
        // wave-58: nullable — 이전 UXML build에서도 panel 부팅 깨지지 않게 optional.
        _deploymentChip = _panelRoot.Q<Label>("SquadBuilderDeploymentChip");
        _postureChip = _panelRoot.Q<Label>("SquadBuilderPostureChip");
        _riskChip = _panelRoot.Q<Label>("SquadBuilderRiskChip");
        _resetButton = _panelRoot.Q<Button>("SquadBuilderResetButton");
        _confirmButton = _panelRoot.Q<Button>("SquadBuilderConfirmButton");

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
        if (_resetButton != null) _resetButton.clicked += OnResetClicked;
        if (_confirmButton != null) _confirmButton.clicked += OnConfirmClicked;

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
        var expeditionSet = new HashSet<string>(loadout?.ExpeditionSquadHeroIds ?? Array.Empty<string>(), StringComparer.Ordinal);

        foreach (var entry in _anchorButtons)
        {
            var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == entry.Anchor);
            var heroId = deployment?.HeroId ?? string.Empty;
            SquadBuilderHeroRow? row = null;
            if (!string.IsNullOrEmpty(heroId) && heroById.TryGetValue(heroId, out var hero))
            {
                row = BuildHeroRow(session, hero, anchorByHeroId, expeditionSet);
            }

            RenderAnchorButton(entry.Button, entry.Anchor, row);
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
        RenderResponseSummary(session, anchorByHeroId, heroById);
        RenderSynergyChips(anchorByHeroId, heroById);
        _statusLabel.text = $"{_statusText} · 현재 팀 태세: {LocalizePosture(selected)}";

        // wave-58 mockup chip strip update — 배치 X/6, 태세, 위험 점수 (heuristic).
        var deployedCount = loadout?.Deployments?.Count(d => !string.IsNullOrEmpty(d.HeroId)) ?? 0;
        const int deploymentCap = 6;
        if (_deploymentChip != null) _deploymentChip.text = $"배치 {deployedCount}/{deploymentCap}";
        if (_postureChip != null) _postureChip.text = $"태세 {LocalizePosture(selected)}";
        if (_riskChip != null)
        {
            // V1 단순 heuristic: 배치 미달 + posture 위험성 가중. 정식 risk score는 wave-59+ Atlas/Encounter wire.
            var riskScore = Math.Max(0, deploymentCap - deployedCount) * 3 + PostureRiskWeight(selected);
            _riskChip.text = deployedCount == 0 ? "위험 점수 —" : $"위험 점수 {riskScore}";
        }
    }

    private static int PostureRiskWeight(TeamPostureType posture) => posture switch
    {
        TeamPostureType.HoldLine => 4,
        TeamPostureType.StandardAdvance => 8,
        TeamPostureType.ProtectCarry => 6,
        TeamPostureType.CollapseWeakSide => 12,
        TeamPostureType.AllInBackline => 14,
        _ => 8,
    };

    private void OnResetClicked()
    {
        var session = _root.SessionState;
        foreach (var entry in _anchorButtons)
        {
            session.AssignHeroToAnchor(entry.Anchor, null);
        }
        session.SetTeamPosture(TeamPostureType.StandardAdvance);
        _selectedAnchor = DeploymentAnchorId.FrontCenter;
        _statusText = "편성을 초기화했습니다.";
        Render();
    }

    private void OnConfirmClicked()
    {
        _root.SaveProfile();
        _statusText = "편성을 저장하고 출정 준비를 마쳤습니다.";
        Render();
        Close();
    }

    private void RenderRoster(
        GameSessionState session,
        SM.Meta.Model.LoadoutView? loadout,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId)
    {
        _rosterList.Clear();
        var expeditionSet = new HashSet<string>(loadout?.ExpeditionSquadHeroIds ?? Array.Empty<string>(), StringComparer.Ordinal);
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

            var rosterPortraitTex = ResolvePortrait(row);
            var icon = new Label(rosterPortraitTex != null ? string.Empty : BuildRosterGlyph(row));
            icon.AddToClassList("sm-sqb-modal__roster-icon");
            if (rosterPortraitTex != null)
            {
                icon.style.backgroundImage = new StyleBackground(rosterPortraitTex);
                icon.AddToClassList("sm-sqb-modal__roster-icon--art");
            }
            else
            {
                AddClassIconClass(icon, row.ClassKey);
            }
            container.Add(icon);

            var copy = new VisualElement();
            copy.AddToClassList("sm-sqb-modal__roster-copy");

            var name = new Label(row.DisplayName);
            name.AddToClassList("sm-sqb-modal__roster-name");
            copy.Add(name);

            var meta = new Label($"{Shorten(row.MetaLabel, 18)}\n{row.DeploymentLabel}");
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

    private void RenderAnchorButton(Button button, DeploymentAnchorId anchor, SquadBuilderHeroRow? row)
    {
        button.Clear();
        button.text = row?.DisplayName ?? "비어있음";
        // 채워진 슬롯도 클릭 = 다음 동료로 순환이라는 멘탈 모델을 항상 노출(빈 슬롯에서만 안내되던 문제 보완).
        button.tooltip = row != null
            ? $"{row.DisplayName} · 클릭하면 다음 동료로 순환"
            : $"{LocalizeAnchor(anchor)} · 클릭하면 동료 배치";

        var card = new VisualElement();
        card.AddToClassList("sm-sqb-modal__anchor-card");
        card.EnableInClassList("sm-sqb-modal__anchor-card--front", anchor.IsFrontRow());
        card.EnableInClassList("sm-sqb-modal__anchor-card--back", !anchor.IsFrontRow());
        card.EnableInClassList("sm-sqb-modal__anchor-card--empty", row == null);
        // wave-29 GPT Pro patch: --occupied modifier — formation board의 "deployed 4 + empty 2" 위계 명시화.
        card.EnableInClassList("sm-sqb-modal__anchor-card--occupied", row != null);

        // wave-29: anchor button 자체에도 --empty 토글 (selected와 별개 → USS variant 가능).
        button.EnableInClassList("sm-sqb-modal__anchor-button--empty", row == null);
        button.EnableInClassList("sm-sqb-modal__anchor-button--occupied", row != null);

        var anchorBadge = new Label(ShortAnchorLabel(anchor));
        anchorBadge.AddToClassList("sm-sqb-modal__anchor-badge");
        card.Add(anchorBadge);

        var portrait = new VisualElement();
        portrait.AddToClassList("sm-sqb-modal__anchor-portrait");
        var anchorPortraitTex = row != null ? ResolvePortrait(row) : null;
        if (anchorPortraitTex != null)
        {
            // 실제 흉상이 있으면 figure를 그리고 글리프는 숨김 — 편성 보드에서 누가 어디 있는지 얼굴로 식별.
            portrait.style.backgroundImage = new StyleBackground(anchorPortraitTex);
            portrait.AddToClassList("sm-sqb-modal__anchor-portrait--art");
        }
        else
        {
            if (row != null)
            {
                AddClassIconClass(portrait, row.ClassKey);
            }

            var glyph = new Label(row != null ? BuildRosterGlyph(row) : "+");
            glyph.AddToClassList("sm-sqb-modal__anchor-glyph");
            portrait.Add(glyph);
        }
        card.Add(portrait);

        var name = new Label(row != null ? Shorten(row.DisplayName, 14) : "비어있음");
        name.AddToClassList("sm-sqb-modal__anchor-name");
        card.Add(name);

        var role = new Label(row != null ? Shorten(row.RoleLabel, 16) : "배치 대기");
        role.AddToClassList("sm-sqb-modal__anchor-role");
        card.Add(role);

        var pipRow = new VisualElement();
        pipRow.AddToClassList("sm-sqb-modal__anchor-pips");
        for (var i = 0; i < 5; i++)
        {
            var pip = new VisualElement();
            pip.AddToClassList("sm-sqb-modal__anchor-pip");
            pip.EnableInClassList("sm-sqb-modal__anchor-pip--on", row != null && i < ResolveRosterPipCount(row));
            pipRow.Add(pip);
        }
        card.Add(pipRow);

        button.Add(card);
    }

    private static int ResolveRosterPipCount(SquadBuilderHeroRow row)
    {
        if (row.IsDeployed) return 5;
        return row.DeploymentLabel == "원정 후보" ? 4 : 3;
    }

    private static string BuildRosterGlyph(SquadBuilderHeroRow row)
    {
        var name = row.DisplayName?.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            return name[..1];
        }

        return row.IsDeployed ? "◆" : "◇";
    }

    private static void AddClassIconClass(VisualElement element, string classKey)
    {
        element.AddToClassList("sm-sqb-modal__class-icon");
        element.AddToClassList($"sm-sqb-modal__class-icon--{classKey}");
    }

    private static string ShortAnchorLabel(DeploymentAnchorId anchor) => anchor switch
    {
        DeploymentAnchorId.FrontTop => "전 상",
        DeploymentAnchorId.FrontCenter => "전 중",
        DeploymentAnchorId.FrontBottom => "전 하",
        DeploymentAnchorId.BackTop => "후 상",
        DeploymentAnchorId.BackCenter => "후 중",
        DeploymentAnchorId.BackBottom => "후 하",
        _ => "배치",
    };

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
        AddDetailTag(row.FormationLabel);
        AddDetailTag(row.BiasLabel);
        AddDetailTag(row.RarityLabel);
        AddDetailTag($"팀 태세 {LocalizePosture(session.SelectedTeamPosture)}");
        return row;
    }

    private void RenderTacticalDecisionRows(GameSessionState session, int deployedCount, SquadBuilderHeroRow? selectedRow)
    {
        _operationRows.Clear();

        AddOperationRow("전열", selectedRow?.DeploymentLabel ?? LocalizeAnchor(_selectedAnchor));
        AddOperationRow("역할", selectedRow?.RoleLabel ?? "선택 없음");
        // P1 유닛별 타겟 지시 — 클릭 cycle. 세션 SetHeroTargetDirective → 로드아웃 compile hash까지 흐른다.
        if (selectedRow != null)
        {
            AddTargetDirectiveRow(session, selectedRow.HeroId);
        }
        AddOperationRow("거리", selectedRow?.RangeLabel ?? "기본 교전 거리");
        AddOperationRow("편성", $"배치 {deployedCount}/6 · 원정 {session.ExpeditionSquadHeroIds.Count}/4");
    }

    // 배치된 분대의 활성 시너지를 표면화 — 전투/밸런스가 쓰는 content.SynergyCatalog 와 동일 SoT.
    // (이전엔 이 자리 SquadBuilderSynergyChips 에 posture/역할 chip 이 들어가 이름과 내용이 어긋났음.
    //  per-hero formation/bias 는 선택 영웅 디테일 태그로 이동.)
    private void RenderSynergyChips(
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById)
    {
        _synergyChips.Clear();

        if (anchorByHeroId.Count == 0)
        {
            AddSynergyChip("배치하면 시너지가 표시됩니다", active: false, muted: true);
            return;
        }

        if (!_root.CombatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            AddSynergyChip("시너지 데이터를 불러오지 못했습니다", active: false, muted: true);
            return;
        }

        var deployedTags = new List<IReadOnlyList<string>>(anchorByHeroId.Count);
        foreach (var heroId in anchorByHeroId.Keys)
        {
            if (!heroById.TryGetValue(heroId, out var hero))
            {
                continue;
            }

            var tags = new List<string> { hero.RaceId, hero.ClassId };
            if (!string.IsNullOrWhiteSpace(hero.ArchetypeId)
                && snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var template)
                && template.RecruitPlanTags != null)
            {
                tags.AddRange(template.RecruitPlanTags);
            }

            deployedTags.Add(tags);
        }

        var surfaces = SquadSynergyPreview.Evaluate(deployedTags, snapshot.SynergyCatalog)
            .Where(surface => surface.CurrentCount > 0)
            .ToList();

        if (surfaces.Count == 0)
        {
            AddSynergyChip("활성 시너지 없음 · 같은 세력/직업 2명 이상 배치", active: false, muted: true);
            return;
        }

        foreach (var surface in surfaces)
        {
            var name = _contentText.GetSynergyName(surface.SynergyId);
            var bound = surface.IsActive
                ? surface.ActiveThreshold
                : (surface.NextThreshold > 0 ? surface.NextThreshold : surface.ActiveThreshold);
            AddSynergyChip($"{name} {surface.CurrentCount}/{bound}", active: surface.IsActive, muted: !surface.IsActive);
        }
    }

    private void AddSynergyChip(string text, bool active, bool muted)
    {
        var chip = new Label(text);
        chip.AddToClassList("sm-sqb-modal__synergy-chip");
        chip.EnableInClassList("sm-sqb-modal__synergy-chip--active", active);
        chip.EnableInClassList("sm-sqb-modal__synergy-chip--muted", muted);
        _synergyChips.Add(chip);
    }

    // 응답("대응") 요약 — posture 기준 + 배치 분대의 카운터 커버리지(강함/취약)를 한 줄로 표면화.
    // 위협 그리드 UI 가 프로덕션 SquadBuilder 엔 없으므로 신규 UXML 없이 기존 요약 라벨에 텍스트로 surface.
    private void RenderResponseSummary(
        GameSessionState session,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById)
    {
        var posture = LocalizePosture(session.SelectedTeamPosture);
        var coverageLine = BuildCoverageLine(anchorByHeroId, heroById);
        var disclaimer = "확정 전투 예측이 아니라 현재 편성 read model의 대응 힌트입니다.";
        _responseSummaryLabel.text = string.IsNullOrEmpty(coverageLine)
            ? $"{posture} 기준. {disclaimer}"
            : $"{posture} 기준 · {coverageLine}\n{disclaimer}";
    }

    // 배치 분대 → 아키타입 governance → 팀 카운터 커버리지. 전투/거버넌스와 동일 SoT(CounterCoverageAggregationService).
    private string BuildCoverageLine(
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById)
    {
        if (anchorByHeroId.Count == 0
            || !_root.CombatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return string.Empty;
        }

        var templates = new List<SM.Meta.Model.CombatArchetypeTemplate>(anchorByHeroId.Count);
        foreach (var heroId in anchorByHeroId.Keys)
        {
            if (heroById.TryGetValue(heroId, out var hero)
                && !string.IsNullOrWhiteSpace(hero.ArchetypeId)
                && snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var template))
            {
                templates.Add(template);
            }
        }

        if (templates.Count == 0)
        {
            return string.Empty;
        }

        var (strong, gaps) = SquadCounterCoveragePreview.Classify(SquadCounterCoveragePreview.Evaluate(templates));
        var parts = new List<string>();
        if (strong.Count > 0)
        {
            parts.Add($"대응 강함: {string.Join("·", strong.Select(LocalizeCounterTool))}");
        }
        if (gaps.Count > 0)
        {
            parts.Add($"취약: {string.Join("·", gaps.Select(LocalizeCounterTool))}");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : "대응 도구 없음 — 보강 필요";
    }

    private static string LocalizeCounterTool(string tool) => TacticsLexicon.CounterTool(tool);

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

    // P1 타겟 지시 행 — 값 자리에 cycle 버튼. UXML 무변경(동적 생성, roster row와 같은 패턴).
    private void AddTargetDirectiveRow(GameSessionState session, string heroId)
    {
        var row = new VisualElement { name = "SquadBuilderTargetDirectiveRow" };
        row.AddToClassList("sm-sqb-modal__operation-row");

        var keyLabel = new Label("지시");
        keyLabel.AddToClassList("sm-sqb-modal__operation-key");
        row.Add(keyLabel);

        var cycleButton = new Button { name = "SquadBuilderTargetDirectiveButton", text = LocalizeDirective(session.GetHeroTargetDirective(heroId)) };
        cycleButton.AddToClassList("sm-sqb-modal__operation-value");
        cycleButton.clicked += () =>
        {
            var next = _root.SessionState.CycleHeroTargetDirective(heroId);
            _statusText = $"타겟 지시 변경: {LocalizeDirective(next)}";
            Render();
        };
        row.Add(cycleButton);

        _operationRows.Add(row);
    }

    private static string LocalizeDirective(PlayerTargetDirective directive) => TacticsLexicon.Directive(directive);

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
            ClassKey: NormalizeClassKey(hero.ClassId),
            RarityLabel: hero.RecruitTier.ToString().ToLowerInvariant(),
            IsDeployed: anchorByHeroId.ContainsKey(hero.HeroId),
            CharacterId: string.IsNullOrWhiteSpace(hero.CharacterId) ? hero.ArchetypeId : hero.CharacterId);
    }

    // 흉상 resolve — 못 찾으면 null (caller가 글리프 fallback). 빈 슬롯(row==null)은 호출 안 함.
    private Texture2D? ResolvePortrait(SquadBuilderHeroRow row)
        => string.IsNullOrWhiteSpace(row.CharacterId) ? null : _portraitSprite?.Invoke(row.CharacterId);

    private static string NormalizeClassKey(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId)) return "unknown";
        var normalized = classId.Trim().ToLowerInvariant();
        return normalized is "vanguard" or "duelist" or "ranger" or "mystic"
            ? normalized
            : "unknown";
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

    // 전술 어휘 표시명은 TacticsLexicon 단일 소스 — 전술 공방(TacticalWorkshop)과 라벨 드리프트 방지.
    private static string LocalizeAnchor(DeploymentAnchorId anchor) => TacticsLexicon.Anchor(anchor);

    private static string LocalizePosture(TeamPostureType posture) => TacticsLexicon.Posture(posture);

    private static string LocalizeFormation(FormationLine? formation) => TacticsLexicon.Formation(formation);

    private static string LocalizeRange(RangeDiscipline? range) => TacticsLexicon.Range(range);

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
        string ClassKey,
        string RarityLabel,
        bool IsDeployed,
        string CharacterId);

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
