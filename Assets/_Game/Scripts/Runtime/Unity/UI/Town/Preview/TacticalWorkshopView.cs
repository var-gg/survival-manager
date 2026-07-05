using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Tactical Workshop(전술 공방) View 계약 — Presenter가 이 인터페이스만 알고,
/// FastUnit은 RecordingTacticalWorkshopView로 scene 없이 계약 검증한다.
/// </summary>
public interface ITacticalWorkshopView
{
    void Bind(ITacticalWorkshopActions actions);
    void BindClose(Action close);
    void Open();
    void Close();
    void Render(TacticalWorkshopViewState state);
}

/// <summary>
/// Tactical Workshop UITK View — UXML root에서 container 참조 캡처, Render(ViewState) 시
/// dynamic content 재구축. presentation만 담당 — battle/save truth를 만들지 않고
/// Presenter가 노출한 액션 콜백만 호출한다.
///
/// sprite/texture 로드를 직접 안 함 — ViewState의 Texture2D(pre-resolve) 또는
/// USS art class(twp-*-card--art-{key})로 배경을 입힌다.
/// </summary>
public sealed class TacticalWorkshopView : ITacticalWorkshopView
{
    private readonly VisualElement _root;
    private readonly Button? _closeButton;
    private readonly Button? _resetButton;
    private readonly Label? _deployChip;
    private readonly Label? _postureChip;
    private readonly Label? _answerChip;
    private readonly VisualElement _anchorPad;
    private readonly VisualElement _postureRow;
    private readonly VisualElement _synergyRow;
    private readonly VisualElement _threatGrid;
    private readonly VisualElement _tacticPresetRows;

    private ITacticalWorkshopActions? _actions;
    private Action? _close;

    public TacticalWorkshopView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _root = root.Q<VisualElement>("TwpRoot") ?? root;
        // Town hub 전체 tree가 들어와도 panel subtree로 스코프 — 다른 panel과 name 충돌 방지.
        _closeButton = _root.Q<Button>("TwpCloseButton") ?? _root.Q<Button>(className: "twp-header__close");
        _resetButton = _root.Q<Button>("TwpResetButton");
        _deployChip = _root.Q<Label>("TwpDeployChip");
        _postureChip = _root.Q<Label>("TwpPostureChip");
        _answerChip = _root.Q<Label>("TwpAnswerChip");
        _anchorPad = _root.Q<VisualElement>(className: "twp-anchor-pad")
            ?? throw new ArgumentException("twp-anchor-pad 못 찾음");
        _postureRow = _root.Q<VisualElement>("PostureCardRow")
            ?? throw new ArgumentException("PostureCardRow 못 찾음");
        _synergyRow = _root.Q<VisualElement>(className: "twp-synergy-row")
            ?? throw new ArgumentException("twp-synergy-row 못 찾음");
        _threatGrid = _root.Q<VisualElement>("ThreatGrid")
            ?? throw new ArgumentException("ThreatGrid 못 찾음");
        _tacticPresetRows = _root.Q<VisualElement>("TacticPresetRows")
            ?? throw new ArgumentException("TacticPresetRows 못 찾음");

        _root.focusable = true;
        _root.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
        if (_resetButton != null)
        {
            _resetButton.clicked += () => _actions?.OnTacticsReset();
        }
    }

    public void Bind(ITacticalWorkshopActions actions)
    {
        _actions = actions;
    }

    public void BindClose(Action close)
    {
        if (close == null) return;
        _close = close;
        if (_closeButton != null)
        {
            _closeButton.clicked += close;
        }
    }

    public void Open()
    {
        _root.style.display = DisplayStyle.Flex;
        _root.RemoveFromClassList("sm-modal-anim--enter");
        var overlay = FindModalOverlay();
        if (overlay != null)
        {
            overlay.style.display = DisplayStyle.Flex;
            overlay.BringToFront();
        }
        _root.BringToFront();
        _root.Focus();
    }

    public void Close()
    {
        _root.style.display = DisplayStyle.None;
        _root.AddToClassList("sm-modal-anim--enter");
        var overlay = FindModalOverlay();
        if (overlay != null) overlay.style.display = DisplayStyle.None;
    }

    public void Render(TacticalWorkshopViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (_deployChip != null) _deployChip.text = state.DeployChipLabel;
        if (_postureChip != null) _postureChip.text = state.PostureChipLabel;
        if (_answerChip != null)
        {
            _answerChip.text = state.AnswerChipLabel;
            _answerChip.EnableInClassList("twp-command-chip--warn", state.AnswerChipWarn);
        }
        RenderAnchors(state.Anchors);
        RenderPostures(state.Postures);
        RenderSynergyChips(state.SynergyChips, state.SynergyEmptyText);
        RenderThreats(state.Threats);
        RenderTactics(state.Tactics);
    }

    private void RenderAnchors(IReadOnlyList<TacticalWorkshopAnchorViewState> anchors)
    {
        // 기존 standee 제거 (anchor pad에 다른 자식도 있음 — twp-standee class만 제거)
        var existing = _anchorPad.Query<VisualElement>(className: "twp-standee").ToList();
        foreach (var s in existing) s.RemoveFromHierarchy();

        foreach (var a in anchors)
        {
            var standee = new VisualElement();
            standee.AddToClassList("twp-standee");
            standee.AddToClassList($"twp-standee--{AnchorPositionClass(a.AnchorId)}");
            var isEmpty = string.IsNullOrEmpty(a.AssignedHeroId);
            if (isEmpty) standee.AddToClassList("twp-standee--empty");

            var pillar = new VisualElement();
            pillar.AddToClassList("twp-standee__pillar");
            if (isEmpty) pillar.AddToClassList("twp-standee__pillar--empty");
            standee.Add(pillar);

            if (!isEmpty)
            {
                var figure = new VisualElement();
                figure.AddToClassList("twp-standee__figure");
                if (a.AssignedFigure != null) figure.style.backgroundImage = new StyleBackground(a.AssignedFigure);
                standee.Add(figure);
            }

            standee.tooltip = isEmpty ? $"{a.AnchorId} · empty" : $"{a.AnchorId} · {a.AssignedHeroId}";
            // anchor pad는 read-only reference — anchor 편집은 SquadBuilder 책임 (audit §2.2)
            _anchorPad.Add(standee);
        }
    }

    private void RenderPostures(IReadOnlyList<TacticalWorkshopPostureViewState> postures)
    {
        _postureRow.Clear();
        foreach (var p in postures)
        {
            // 인터랙티브 요소는 Button — SquadBuilder posture 버튼과 같은 grammar.
            // (VisualElement + ClickEvent 콜백은 witness의 합성 클릭 디스패치가 닿지 않는다.)
            var card = new Button { name = $"TwpPosture_{p.PostureId}", text = string.Empty };
            card.AddToClassList("twp-posture-card");
            card.AddToClassList($"twp-posture-card--art-{p.SpriteKey}");
            card.AddToClassList("sm-hover-raise");   // 콘솔급 motion — hover 시 raise
            if (p.IsSelected) card.AddToClassList("twp-posture-card--selected");
            if (p.Sprite != null) card.style.backgroundImage = new StyleBackground(p.Sprite);

            var label = new Label(p.KoLabel);
            label.AddToClassList("twp-posture-card__label");
            label.pickingMode = PickingMode.Ignore;
            card.Add(label);

            card.tooltip = $"{p.PostureId} — {p.KoLabel}";
            var postureId = p.PostureId;
            card.clicked += () => _actions?.OnPostureSelected(postureId);
            _postureRow.Add(card);
        }
    }

    private void RenderSynergyChips(IReadOnlyList<TacticalWorkshopSynergyChipViewState> chips, string emptyText)
    {
        _synergyRow.Clear();

        if (chips.Count == 0)
        {
            var empty = new Label(emptyText);
            empty.AddToClassList("twp-synergy-empty");
            _synergyRow.Add(empty);
            return;
        }

        foreach (var c in chips)
        {
            var chip = new Label($"{c.KoLabel} {c.CountLabel}");
            chip.AddToClassList("twp-synergy-chip");
            chip.EnableInClassList("twp-synergy-chip--active", c.IsActive);
            chip.EnableInClassList("twp-synergy-chip--muted", !c.IsActive);
            chip.tooltip = c.SynergyId;
            _synergyRow.Add(chip);
        }
    }

    private void RenderThreats(IReadOnlyList<TacticalWorkshopThreatViewState> threats)
    {
        _threatGrid.Clear();
        foreach (var t in threats)
        {
            var chip = new VisualElement();
            chip.AddToClassList("twp-threat-chip");
            chip.AddToClassList($"twp-threat-chip--art-{t.SpriteKey}");
            if (!string.IsNullOrEmpty(t.AnswerState))
                chip.AddToClassList($"twp-threat-chip--{t.AnswerState}");
            if (t.Sprite != null) chip.style.backgroundImage = new StyleBackground(t.Sprite);

            var lbl = new Label(t.KoLabel);
            lbl.AddToClassList("twp-threat-chip__label");
            chip.Add(lbl);
            chip.tooltip = $"{t.LaneId} — {t.KoLabel}";
            _threatGrid.Add(chip);
        }
    }

    private void RenderTactics(IReadOnlyList<TacticalWorkshopHeroTacticViewState> tactics)
    {
        _tacticPresetRows.Clear();
        foreach (var hero in tactics)
        {
            _tacticPresetRows.Add(BuildHeroTacticBlock(hero));
        }
    }

    /// <summary>
    /// per-unit tactic 1줄 행 — [이름] [anchor·role] [formation·range] [지시 cycle 버튼].
    /// RoleInstruction/BehaviorProfile은 read-only, P1 타겟 지시만 클릭 cycle 편집.
    /// bias 3종은 행 tooltip으로 노출(상세는 전술 설정 선택 디테일 소유).
    /// 가짜 condition→action→target rule chain 폐기 (audit §4.1 P1-1).
    /// </summary>
    private VisualElement BuildHeroTacticBlock(TacticalWorkshopHeroTacticViewState hero)
    {
        var block = new VisualElement();
        block.AddToClassList("twp-tactic-hero");
        block.tooltip = BuildBiasTooltip(hero.Biases);

        var name = new Label(hero.DisplayName);
        name.AddToClassList("twp-tactic-hero__name");
        block.Add(name);

        var role = new Label($"{hero.AnchorLabel} · {hero.RoleLabel}");
        role.AddToClassList("twp-tactic-hero__role");
        block.Add(role);

        var behavior = new Label($"{hero.FormationLabel} · {hero.RangeLabel}");
        behavior.AddToClassList("twp-tactic-hero__behavior");
        block.Add(behavior);

        var directive = new Button { name = $"TwpDirective_{hero.HeroId}", text = $"지시 · {hero.DirectiveLabel}" };
        directive.AddToClassList("twp-tactic-hero__directive");
        directive.tooltip = "클릭하면 타겟 지시가 순환됩니다";
        var heroId = hero.HeroId;
        directive.clicked += () => _actions?.OnTacticDirectiveCycled(heroId);
        block.Add(directive);

        return block;
    }

    private static string BuildBiasTooltip(IReadOnlyList<TacticalWorkshopBiasViewState> biases)
    {
        var parts = new List<string>(biases.Count);
        foreach (var bias in biases)
        {
            parts.Add($"{bias.Label} {Mathf.RoundToInt(Mathf.Clamp01(bias.Value) * 100f)}");
        }

        return string.Join(" · ", parts);
    }

    private VisualElement? FindModalOverlay()
    {
        for (var current = _root.parent; current != null; current = current.parent)
        {
            if (current.ClassListContains("town-hub__modal-overlay"))
            {
                return current;
            }
        }

        return null;
    }

    private void HandleKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Escape) return;
        if (_root.resolvedStyle.display == DisplayStyle.None) return;
        _close?.Invoke();
        evt.StopPropagation();
    }

    private static string AnchorPositionClass(string anchorId) => anchorId switch
    {
        "FrontTop"    => "front-top",
        "FrontCenter" => "front-center",
        "FrontBottom" => "front-bottom",
        "BackTop"     => "back-top",
        "BackCenter"  => "back-center",
        "BackBottom"  => "back-bottom",
        _ => anchorId.ToLowerInvariant(),
    };
}

/// <summary>
/// View → Presenter event interface. View가 Presenter 구현 직접 참조 안 하도록 분리.
/// anchor pad는 read-only reference라 anchor 액션 없음 — posture / 타겟 지시 / 초기화만 편집 (audit §2.2).
/// </summary>
public interface ITacticalWorkshopActions
{
    void OnPostureSelected(string postureId);
    void OnTacticDirectiveCycled(string heroId);
    void OnTacticsReset();
}
