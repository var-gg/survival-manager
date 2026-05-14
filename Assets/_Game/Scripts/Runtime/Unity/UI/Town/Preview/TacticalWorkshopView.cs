using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Tactical Workshop V1 surface View — UXML clone된 root에서 container 참조 캡처,
/// Render(ViewState) 시 dynamic content 재구축. Presenter 패턴 (TownScreenView 참고).
///
/// 본 View는 sprite/texture 로드를 안 함 — caller (Editor Bootstrap / Runtime Presenter)가
/// ViewState 빌드 시 Texture2D를 pre-resolve해서 넘김.
///
/// presenter wire: Bind(actions) 호출 후 posture 카드 클릭 → actions.OnPostureSelected(postureId).
/// </summary>
public sealed class TacticalWorkshopView
{
    private readonly VisualElement _root;
    private readonly VisualElement _anchorPad;
    private readonly VisualElement _postureRow;
    private readonly VisualElement _synergyRow;
    private readonly VisualElement _threatGrid;
    private readonly VisualElement _tacticPresetRows;

    private ITacticalWorkshopActions? _actions;

    public TacticalWorkshopView(VisualElement root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _anchorPad = root.Q<VisualElement>(className: "twp-anchor-pad")
            ?? throw new ArgumentException("twp-anchor-pad 못 찾음");
        _postureRow = root.Q<VisualElement>("PostureCardRow")
            ?? throw new ArgumentException("PostureCardRow 못 찾음");
        _synergyRow = root.Q<VisualElement>(className: "twp-synergy-row")
            ?? throw new ArgumentException("twp-synergy-row 못 찾음");
        _threatGrid = root.Q<VisualElement>("ThreatGrid")
            ?? throw new ArgumentException("ThreatGrid 못 찾음");
        _tacticPresetRows = root.Q<VisualElement>("TacticPresetRows")
            ?? throw new ArgumentException("TacticPresetRows 못 찾음");
    }

    public void Bind(ITacticalWorkshopActions actions)
    {
        _actions = actions;
    }

    public void Render(TacticalWorkshopViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        RenderAnchors(state.Anchors);
        RenderPostures(state.Postures);
        RenderSynergyChips(state.SynergyChips);
        RenderThreats(state.Threats);
        RenderTactics(state.Tactics);
    }

    private void RenderAnchors(IReadOnlyList<TacticalWorkshopAnchorViewState> anchors)
    {
        // 기존 standee 제거 (anchor pad에 hex/arc 등 다른 자식도 있음 — twp-standee class만 제거)
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
            standee.RegisterCallback<ClickEvent>(_ => _actions?.OnAnchorClicked(a.AnchorId));
            _anchorPad.Add(standee);
        }
    }

    private void RenderPostures(IReadOnlyList<TacticalWorkshopPostureViewState> postures)
    {
        _postureRow.Clear();
        foreach (var p in postures)
        {
            var card = new VisualElement();
            card.AddToClassList("twp-posture-card");
            card.AddToClassList("sm-hover-raise");   // 콘솔급 motion — hover 시 raise
            if (p.IsSelected) card.AddToClassList("twp-posture-card--selected");
            if (p.Sprite != null) card.style.backgroundImage = new StyleBackground(p.Sprite);

            var label = new Label(p.KoLabel);
            label.AddToClassList("twp-posture-card__label");
            card.Add(label);

            card.tooltip = $"{p.PostureId} — {p.KoLabel}";
            card.RegisterCallback<ClickEvent>(_ => _actions?.OnPostureSelected(p.PostureId));
            _postureRow.Add(card);
        }
    }

    private void RenderSynergyChips(IReadOnlyList<TacticalWorkshopSynergyChipViewState> chips)
    {
        _synergyRow.Clear();

        string? previousGroup = null;
        foreach (var c in chips)
        {
            if (previousGroup != null && c.Group != previousGroup)
            {
                var divider = new VisualElement();
                divider.AddToClassList("twp-synergy-divider");
                _synergyRow.Add(divider);
            }
            previousGroup = c.Group;

            var chip = new VisualElement();
            chip.AddToClassList("twp-synergy-chip");
            chip.AddToClassList($"twp-synergy-chip--{ChipKeyFromSynergyId(c.SynergyId)}");
            if (c.Sprite != null) chip.style.backgroundImage = new StyleBackground(c.Sprite);
            chip.tooltip = $"{c.SynergyId} — {c.KoLabel}";
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
            chip.AddToClassList("sm-select-snap");   // 콘솔급 motion — 클릭 시 snap
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

    private static VisualElement BuildHeroTacticBlock(TacticalWorkshopHeroTacticViewState hero)
    {
        var block = new VisualElement();
        block.AddToClassList("twp-tactic-hero");
        block.AddToClassList("twp-tactic-hero--readonly");

        var header = new VisualElement();
        header.AddToClassList("twp-tactic-hero__header");

        var portrait = new VisualElement();
        portrait.AddToClassList("twp-tactic-hero__portrait");
        header.Add(portrait);

        var name = new Label(hero.DisplayName);
        name.AddToClassList("twp-tactic-hero__name");
        header.Add(name);

        var posture = new Label($"자세: {hero.PostureLabel}");
        posture.AddToClassList("twp-tactic-hero__posture");
        header.Add(posture);

        var count = new Label($"{hero.Rules.Count} rules");
        count.AddToClassList("twp-tactic-hero__count");
        header.Add(count);

        block.Add(header);

        var rulesContainer = new VisualElement();
        rulesContainer.AddToClassList("twp-tactic-hero__rules");
        foreach (var rule in hero.Rules)
        {
            rulesContainer.Add(BuildRuleRow(rule));
        }
        block.Add(rulesContainer);

        return block;
    }

    private static VisualElement BuildRuleRow(TacticalWorkshopRuleViewState rule)
    {
        var row = new VisualElement();
        row.AddToClassList("twp-tactic-rule");

        var prio = new Label($"P{rule.Priority}");
        prio.AddToClassList("twp-tactic-rule__priority");
        row.Add(prio);

        row.Add(BuildChainChip(rule.ConditionId, "cond"));
        row.Add(BuildArrow());
        row.Add(BuildChainChip(rule.ActionId, "act"));
        row.Add(BuildArrow());
        row.Add(BuildChainChip(rule.TargetId, "tgt"));

        return row;
    }

    private static VisualElement BuildChainChip(string label, string kind)
    {
        var chip = new VisualElement();
        chip.AddToClassList("twp-tactic-rule__chip");
        chip.AddToClassList($"twp-tactic-rule__chip--{kind}");
        var lbl = new Label(label);
        lbl.AddToClassList("twp-tactic-rule__chip-label");
        chip.Add(lbl);
        return chip;
    }

    private static VisualElement BuildArrow()
    {
        var arrow = new VisualElement();
        arrow.AddToClassList("twp-tactic-rule__arrow");
        var lbl = new Label("→");
        lbl.AddToClassList("twp-tactic-rule__arrow-label");
        arrow.Add(lbl);
        return arrow;
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

    private static string ChipKeyFromSynergyId(string synergyId)
    {
        const string prefix = "synergy_";
        return synergyId.StartsWith(prefix, StringComparison.Ordinal)
            ? synergyId.Substring(prefix.Length)
            : synergyId;
    }
}

/// <summary>
/// View → Presenter event interface. View가 Presenter 구현 직접 참조 안 하도록 분리.
/// Editor Bootstrap이 dev tool로 사용 시 null 또는 stub로 inject 가능.
/// </summary>
public interface ITacticalWorkshopActions
{
    void OnPostureSelected(string postureId);
    void OnAnchorClicked(string anchorId);
}
