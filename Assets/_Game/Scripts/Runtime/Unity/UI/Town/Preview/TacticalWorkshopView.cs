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
            // anchor pad는 read-only reference — anchor 편집은 SquadBuilder 책임 (audit §2.2)
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

    /// <summary>
    /// per-unit tactic block — runtime 실재 요약 (read-only). RoleInstruction(anchor·role·bias) +
    /// BehaviorProfile(formation·range). 가짜 condition→action→target rule chain 폐기 (audit §4.1 P1-1).
    /// </summary>
    private static VisualElement BuildHeroTacticBlock(TacticalWorkshopHeroTacticViewState hero)
    {
        var block = new VisualElement();
        block.AddToClassList("twp-tactic-hero");
        block.AddToClassList("twp-tactic-hero--readonly");

        // header — portrait + name + anchor·role (RoleInstruction)
        var header = new VisualElement();
        header.AddToClassList("twp-tactic-hero__header");

        var portrait = new VisualElement();
        portrait.AddToClassList("twp-tactic-hero__portrait");
        header.Add(portrait);

        var name = new Label(hero.DisplayName);
        name.AddToClassList("twp-tactic-hero__name");
        header.Add(name);

        var role = new Label($"{hero.AnchorLabel} · {hero.RoleLabel}");
        role.AddToClassList("twp-tactic-hero__role");
        header.Add(role);

        block.Add(header);

        // behavior — FormationLine + RangeDiscipline (BehaviorProfile 요약)
        var behavior = new Label($"{hero.FormationLabel} · {hero.RangeLabel}");
        behavior.AddToClassList("twp-tactic-hero__behavior");
        block.Add(behavior);

        // bias bars — RoleInstruction bias 3 float (0..1)
        var biasContainer = new VisualElement();
        biasContainer.AddToClassList("twp-tactic-hero__biases");
        foreach (var bias in hero.Biases)
        {
            biasContainer.Add(BuildBiasRow(bias));
        }
        block.Add(biasContainer);

        return block;
    }

    private static VisualElement BuildBiasRow(TacticalWorkshopBiasViewState bias)
    {
        var row = new VisualElement();
        row.AddToClassList("twp-tactic-bias");

        var label = new Label(bias.Label);
        label.AddToClassList("twp-tactic-bias__label");
        row.Add(label);

        var bar = new VisualElement();
        bar.AddToClassList("twp-tactic-bias__bar");
        var fill = new VisualElement();
        fill.AddToClassList("twp-tactic-bias__fill");
        fill.style.width = new StyleLength(new Length(Mathf.Clamp01(bias.Value) * 100f, LengthUnit.Percent));
        bar.Add(fill);
        row.Add(bar);

        var value = new Label(Mathf.RoundToInt(Mathf.Clamp01(bias.Value) * 100f).ToString());
        value.AddToClassList("twp-tactic-bias__value");
        row.Add(value);

        return row;
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
/// anchor pad는 read-only reference라 anchor 액션 없음 — posture만 편집 가능 (audit §2.2).
/// </summary>
public interface ITacticalWorkshopActions
{
    void OnPostureSelected(string postureId);
}
