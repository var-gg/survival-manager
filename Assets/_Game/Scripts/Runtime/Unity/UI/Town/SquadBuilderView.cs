using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

/// <summary>
/// 전술 설정 사용자 액션 계약 — View의 클릭/키 입력이 presenter로 흐르는 유일한 통로.
/// </summary>
public interface ISquadBuilderActions
{
    void OnAnchorClicked(DeploymentAnchorId anchor);
    void OnPostureClicked(TeamPostureType posture);
    void OnTargetDirectiveCycled(string heroId);
    void OnReset();
    void OnConfirm();
    void Close();
}

/// <summary>
/// SquadBuilder View 계약 — presenter가 의존하는 표면만. 콘크리트 SquadBuilderView는 VisualElement에
/// 묶이지만 presenter는 이 인터페이스만 알면 되어 헤드리스 테스트에서 fake view로 구동한다.
/// open/close 표시 상태는 ViewState.IsOpen으로 Render에 흐르고, FocusModal은 Open 시 1회성
/// BringToFront/Focus 명령만 담당한다.
/// </summary>
public interface ISquadBuilderView
{
    void Bind(ISquadBuilderActions actions);
    void Render(SquadBuilderViewState state);
    void FocusModal();
}

/// <summary>
/// 전술 설정(SquadBuilder) UITK View — TownSquadBuilder.uxml 요소 바인딩과 DOM 렌더만 소유.
/// 계산/세션 접근은 전부 SquadBuilderPresenter.BuildState()가 만든 SquadBuilderViewState로 받는다.
/// 포트레잇은 ViewState의 CharacterId를 키로 여기서 resolve (못 찾으면 이니셜 글리프 fallback).
/// </summary>
public sealed class SquadBuilderView : ISquadBuilderView
{
    private readonly VisualElement _panelRoot;
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

    private ISquadBuilderActions? _actions;
    private bool _isOpen;

    public SquadBuilderView(VisualElement panelRoot, Func<string, Texture2D?>? portraitSprite = null)
    {
        _panelRoot = panelRoot ?? throw new ArgumentNullException(nameof(panelRoot));
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

        // 클릭 배선은 ctor에서 1회 — Bind가 여러 번 불려도 핸들러가 중복되지 않게 _actions 간접 참조.
        _closeButton.clicked += () => _actions?.Close();
        foreach (var entry in _anchorButtons)
        {
            var anchor = entry.Anchor;
            entry.Button.clicked += () => _actions?.OnAnchorClicked(anchor);
        }
        foreach (var entry in _postureButtons)
        {
            var posture = entry.Posture;
            entry.Button.clicked += () => _actions?.OnPostureClicked(posture);
        }
        if (_resetButton != null) _resetButton.clicked += () => _actions?.OnReset();
        if (_confirmButton != null) _confirmButton.clicked += () => _actions?.OnConfirm();
    }

    public void Bind(ISquadBuilderActions actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public void FocusModal()
    {
        FindModalOverlay()?.BringToFront();
        _modalRoot.BringToFront();
        _modalRoot.Focus();
    }

    public void Render(SquadBuilderViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        _isOpen = state.IsOpen;
        _modalRoot.style.display = state.IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _modalRoot.EnableInClassList("sm-sqb-modal--open", state.IsOpen);
        _modalRoot.EnableInClassList("sm-sqb-modal--closed", !state.IsOpen);
        _modalRoot.EnableInClassList("sm-modal-anim--enter", !state.IsOpen);
        // hub의 town-hub__modal-overlay wrapper도 토글 — UXML inline display:none 강제 default closed 우회.
        var wrapper = FindModalOverlay();
        if (wrapper != null) wrapper.style.display = state.IsOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (!state.IsOpen) return;

        foreach (var entry in _anchorButtons)
        {
            var slot = state.AnchorSlots.FirstOrDefault(s => s.Anchor == entry.Anchor);
            if (slot == null) continue;
            RenderAnchorButton(entry.Button, slot);
            entry.Button.EnableInClassList("sm-sqb-modal__anchor-button--selected", slot.IsSelected);
        }

        foreach (var entry in _postureButtons)
        {
            var isSelected = state.Postures.Any(p => p.Posture == entry.Posture && p.IsSelected);
            entry.Button.EnableInClassList("sm-sqb-modal__posture-button--selected", isSelected);
        }

        RenderRoster(state);
        RenderSelectedDetail(state.SelectedDetail);
        RenderOperationRows(state);
        RenderSynergyChips(state);
        _responseSummaryLabel.text = state.ResponseSummary;
        _statusLabel.text = state.StatusText;

        // wave-58 mockup chip strip update — 배치 X/6, 태세, 위험 점수 (문구는 presenter가 계산).
        if (_deploymentChip != null) _deploymentChip.text = state.DeploymentChipLabel;
        if (_postureChip != null) _postureChip.text = state.PostureChipLabel;
        if (_riskChip != null) _riskChip.text = state.RiskChipLabel;
    }

    private void RenderRoster(SquadBuilderViewState state)
    {
        _rosterList.Clear();
        _rosterCountLabel.text = $"· {state.RosterCount}";

        foreach (var row in state.RosterRows)
        {
            var container = new VisualElement { name = $"SquadBuilderRosterRow_{SanitizeName(row.HeroId)}" };
            container.AddToClassList("sm-sqb-modal__roster-row");
            container.EnableInClassList("sm-sqb-modal__roster-row--deployed", row.IsDeployed);

            var rosterPortraitTex = ResolvePortrait(row);
            var icon = new Label(rosterPortraitTex != null ? string.Empty : row.Glyph);
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
                pip.EnableInClassList("sm-sqb-modal__roster-pip--on", i < row.PipCount);
                pips.Add(pip);
            }
            copy.Add(pips);
            container.Add(copy);

            _rosterList.Add(container);
        }
    }

    private void RenderAnchorButton(Button button, SquadBuilderAnchorSlotViewState slot)
    {
        var row = slot.HeroRow;
        button.Clear();
        button.text = row?.DisplayName ?? "비어있음";
        // 채워진 슬롯도 클릭 = 다음 동료로 순환이라는 멘탈 모델을 항상 노출(빈 슬롯에서만 안내되던 문제 보완).
        button.tooltip = row != null
            ? $"{row.DisplayName} · 클릭하면 다음 동료로 순환"
            : $"{TacticsLexicon.Anchor(slot.Anchor)} · 클릭하면 동료 배치";

        var card = new VisualElement();
        card.AddToClassList("sm-sqb-modal__anchor-card");
        card.EnableInClassList("sm-sqb-modal__anchor-card--front", slot.Anchor.IsFrontRow());
        card.EnableInClassList("sm-sqb-modal__anchor-card--back", !slot.Anchor.IsFrontRow());
        card.EnableInClassList("sm-sqb-modal__anchor-card--empty", row == null);
        // wave-29 GPT Pro patch: --occupied modifier — formation board의 "deployed 4 + empty 2" 위계 명시화.
        card.EnableInClassList("sm-sqb-modal__anchor-card--occupied", row != null);

        // wave-29: anchor button 자체에도 --empty 토글 (selected와 별개 → USS variant 가능).
        button.EnableInClassList("sm-sqb-modal__anchor-button--empty", row == null);
        button.EnableInClassList("sm-sqb-modal__anchor-button--occupied", row != null);

        var anchorBadge = new Label(slot.ShortLabel);
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

            var glyph = new Label(row?.Glyph ?? "+");
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
            pip.EnableInClassList("sm-sqb-modal__anchor-pip--on", row != null && i < row.PipCount);
            pipRow.Add(pip);
        }
        card.Add(pipRow);

        button.Add(card);
    }

    private void RenderSelectedDetail(SquadBuilderSelectedDetailViewState detail)
    {
        _selectedAnchorLabel.text = detail.SelectedAnchorLabel;
        _selectedHeroName.text = detail.Name;
        _selectedHeroMeta.text = detail.Meta;
        _selectedHeroLoadout.text = detail.Loadout;
        _selectedHeroTags.Clear();
        foreach (var tag in detail.Tags)
        {
            AddDetailChip(_selectedHeroTags, tag);
        }
    }

    private void RenderOperationRows(SquadBuilderViewState state)
    {
        _operationRows.Clear();
        foreach (var row in state.OperationRows)
        {
            AddOperationRow(row.Key, row.Value);

            // P1 타겟 지시 행 — "역할" 행 바로 아래 cycle 버튼 (기존 렌더 순서 보존, UXML 무변경 동적 생성).
            if (state.TargetDirective != null && string.Equals(row.Key, "역할", StringComparison.Ordinal))
            {
                AddTargetDirectiveRow(state.TargetDirective);
            }
        }
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

    private void AddTargetDirectiveRow(SquadBuilderTargetDirectiveViewState directive)
    {
        var row = new VisualElement { name = "SquadBuilderTargetDirectiveRow" };
        row.AddToClassList("sm-sqb-modal__operation-row");

        var keyLabel = new Label("지시");
        keyLabel.AddToClassList("sm-sqb-modal__operation-key");
        row.Add(keyLabel);

        var cycleButton = new Button { name = "SquadBuilderTargetDirectiveButton", text = directive.DirectiveLabel };
        cycleButton.AddToClassList("sm-sqb-modal__operation-value");
        var heroId = directive.HeroId;
        cycleButton.clicked += () => _actions?.OnTargetDirectiveCycled(heroId);
        row.Add(cycleButton);

        _operationRows.Add(row);
    }

    private void RenderSynergyChips(SquadBuilderViewState state)
    {
        _synergyChips.Clear();

        if (state.SynergyChips.Count == 0)
        {
            AddSynergyChip(state.SynergyEmptyText, active: false, muted: true);
            return;
        }

        foreach (var chip in state.SynergyChips)
        {
            AddSynergyChip(chip.Text, active: chip.IsActive, muted: !chip.IsActive);
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

    private static void AddDetailChip(VisualElement parent, string text)
    {
        var tag = new Label(text);
        tag.AddToClassList("sm-sqb-modal__tag");
        parent.Add(tag);
    }

    private static void AddClassIconClass(VisualElement element, string classKey)
    {
        element.AddToClassList("sm-sqb-modal__class-icon");
        element.AddToClassList($"sm-sqb-modal__class-icon--{classKey}");
    }

    // 흉상 resolve — 못 찾으면 null (caller가 글리프 fallback). 빈 슬롯은 호출 안 함.
    private Texture2D? ResolvePortrait(SquadBuilderHeroRowViewState row)
        => string.IsNullOrWhiteSpace(row.CharacterId) ? null : _portraitSprite?.Invoke(row.CharacterId);

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

    private void HandleKeyDown(KeyDownEvent evt)
    {
        if (!_isOpen || evt.keyCode != KeyCode.Escape) return;
        _actions?.Close();
        evt.StopPropagation();
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
