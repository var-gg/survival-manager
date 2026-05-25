using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Recruit V1 surface View. UXML container: CardRow / ScoutLabel / RefreshLabel.
/// per-card Recruit button은 동적 생성. Render(ViewState) 시 CardRow clear + 재구축.
/// 카드 컬럼은 runtime offer 모델 정합 (audit §4.1) — signature는 archetype 고정(gold),
/// flex는 offer rolled(blue)로 시각 분리. PlanScore / ProtectedByPity column 노출.
/// </summary>
public sealed class RecruitView
{
    private readonly VisualElement _cardRow;
    private readonly VisualElement? _detailStateChips;
    private readonly VisualElement? _detailTags;
    private readonly VisualElement? _detailPortrait;
    private readonly VisualElement? _detailClassGlyph;
    private readonly VisualElement? _detailMetricBars;
    private readonly VisualElement? _detailSkillPips;
    private readonly VisualElement? _pressureNeedChips;
    private readonly VisualElement? _modalRoot;
    private readonly Label? _detailNameLabel;
    private readonly Label? _detailMetaLabel;
    private readonly Label? _detailPlanLabel;
    private readonly Label? _detailCostLabel;
    private readonly Label? _detailSkillLabel;
    private readonly Label? _pressureRosterLabel;
    private readonly Label? _pressureNeedLabel;
    private readonly Label? _pressurePlanLabel;
    private readonly Label? _scoutLabel;
    private readonly Label? _refreshLabel;
    private readonly Button? _scoutButton;
    private readonly Button? _refreshButton;
    private readonly Button? _closeButton;

    private IRecruitActions? _actions;

    public RecruitView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _cardRow = root.Q<VisualElement>("CardRow")
            ?? throw new ArgumentException("CardRow 못 찾음");
        _modalRoot = root.Q<VisualElement>("RcpRoot");   // hub modal toggle용 (preview에선 null OK)
        _detailStateChips = root.Q<VisualElement>("SelectedCandidateStateChips");
        _detailTags = root.Q<VisualElement>("SelectedCandidateTags");
        _detailPortrait = root.Q<VisualElement>("SelectedCandidatePortrait");
        _detailClassGlyph = root.Q<VisualElement>("SelectedCandidateClassGlyph");
        _detailMetricBars = root.Q<VisualElement>("SelectedCandidateMetricBars");
        _detailSkillPips = root.Q<VisualElement>("SelectedCandidateSkillPips");
        _pressureNeedChips = root.Q<VisualElement>("RosterNeedChips");
        _detailNameLabel = root.Q<Label>("SelectedCandidateNameLabel");
        _detailMetaLabel = root.Q<Label>("SelectedCandidateMetaLabel");
        _detailPlanLabel = root.Q<Label>("SelectedCandidatePlanLabel");
        _detailCostLabel = root.Q<Label>("SelectedCandidateCostLabel");
        _detailSkillLabel = root.Q<Label>("SelectedCandidateSkillLabel");
        _pressureRosterLabel = root.Q<Label>("RosterPressureCountLabel");
        _pressureNeedLabel = root.Q<Label>("RosterPressureNeedLabel");
        _pressurePlanLabel = root.Q<Label>("RosterPressurePlanLabel");
        _scoutLabel = root.Q<Label>("ScoutLabel");
        _refreshLabel = root.Q<Label>("RefreshLabel");
        _scoutButton = root.Q<Button>(className: "rcp-actions__scout");
        _refreshButton = root.Q<Button>(className: "rcp-actions__refresh");
        _closeButton = root.Q<Button>(className: "rcp-header__close");
    }

    public void Bind(IRecruitActions actions)
    {
        _actions = actions;
        if (_scoutButton != null)
        {
            _scoutButton.clicked -= HandleScoutClicked;
            _scoutButton.clicked += HandleScoutClicked;
        }
        if (_refreshButton != null)
        {
            _refreshButton.clicked -= HandleRefreshClicked;
            _refreshButton.clicked += HandleRefreshClicked;
        }
    }

    /// <summary>hub modal opener가 close action 등록 (preview Bootstrap은 호출 안 함).</summary>
    public void BindClose(Action close)
    {
        if (_closeButton == null || close == null) return;
        _closeButton.clicked += close;
    }

    /// <summary>hub modal opener가 호출 — modal 표시 + sm-modal-anim 진입 transition. wrapper(town-hub__modal-overlay)도 같이 토글.</summary>
    public void Open()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.Flex;
        _modalRoot.RemoveFromClassList("sm-modal-anim--enter");
        var wrapper = _modalRoot.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.Flex;
    }

    /// <summary>hub modal opener가 호출 — modal 숨김. wrapper도 같이 닫음.</summary>
    public void Close()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.None;
        _modalRoot.AddToClassList("sm-modal-anim--enter");
        var wrapper = _modalRoot.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.None;
    }

    public void Render(RecruitViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        RenderCards(state.Candidates);
        RenderSelectedDetail(state.SelectedCandidateDetail);
        RenderRosterPressure(state.RosterPressure);
        RenderActionBar(state.ActionBar);
    }

    private void RenderCards(IReadOnlyList<RecruitCandidateViewState> candidates)
    {
        _cardRow.Clear();
        foreach (var c in candidates)
        {
            _cardRow.Add(BuildCard(c));
        }
    }

    private VisualElement BuildCard(RecruitCandidateViewState c)
    {
        var card = new VisualElement();
        card.AddToClassList("rcp-card");
        card.AddToClassList("sm-hover-raise");   // 콘솔급 motion — hover raise
        AddClassVariant(card, "rcp-card--class", c.ClassKey);
        if (c.IsSelected) card.AddToClassList("rcp-card--selected");
        if (c.SlotType == RecruitSlotType.Protected) card.AddToClassList("rcp-card--protected");
        if (c.SlotType == RecruitSlotType.OnPlan) card.AddToClassList("rcp-card--on-plan");
        card.RegisterCallback<ClickEvent>(_ => _actions?.OnCandidateSelected(c.SlotIndex));

        // pity badge — corner marker (slot type Protected와 별개: pity 보정으로 tier 보장된 offer)
        if (c.ProtectedByPity)
        {
            var pity = new Label("PITY");
            pity.AddToClassList("rcp-card__pity-badge");
            pity.pickingMode = PickingMode.Ignore;
            card.Add(pity);
        }

        // 1) slot type badge
        var slotBadge = new Label(SlotLabel(c.SlotType, c.ScoutBias));
        slotBadge.AddToClassList("rcp-card__slot-badge");
        slotBadge.AddToClassList($"rcp-card__slot-badge--{SlotClassSuffix(c.SlotType)}");
        if (c.ScoutBias) slotBadge.AddToClassList("rcp-card__slot-badge--scout");
        card.Add(slotBadge);

        // 2) portrait
        var portrait = new VisualElement();
        portrait.AddToClassList("rcp-card__portrait");
        AddClassVariant(portrait, "rcp-class", c.ClassKey);
        if (c.PortraitSprite != null)
        {
            portrait.style.backgroundImage = new StyleBackground(c.PortraitSprite);
        }
        else
        {
            var fallback = new Label(BuildFallbackInitials(c.DisplayName));
            fallback.AddToClassList("rcp-card__portrait-fallback");
            portrait.Add(fallback);
        }

        var portraitShade = new VisualElement();
        portraitShade.AddToClassList("rcp-card__portrait-shade");
        portrait.Add(portraitShade);

        var rankPips = new VisualElement();
        rankPips.AddToClassList("rcp-card__rank-pips");
        for (var i = 0; i < 3; i++)
        {
            var pip = new VisualElement();
            pip.AddToClassList("rcp-card__rank-pip");
            if (i < (int)c.Tier) pip.AddToClassList("rcp-card__rank-pip--filled");
            rankPips.Add(pip);
        }
        portrait.Add(rankPips);
        card.Add(portrait);

        // 3) display name (◐ archetype NameKey resolved)
        var nameLabel = new Label(c.DisplayName);
        nameLabel.AddToClassList("rcp-card__name");
        card.Add(nameLabel);

        // 4) class row (formation 제거 — offer엔 anchor/formation field 없음. class만 archetype 파생)
        var metaRow = new VisualElement();
        metaRow.AddToClassList("rcp-card__meta-row");
        var classGlyph = new VisualElement();
        classGlyph.AddToClassList("rcp-card__class-glyph");
        AddClassVariant(classGlyph, "rcp-class", c.ClassKey);
        if (c.ClassSprite != null) classGlyph.style.backgroundImage = new StyleBackground(c.ClassSprite);
        metaRow.Add(classGlyph);
        var metaText = new Label(c.ClassLabel);
        metaText.AddToClassList("rcp-card__meta-text");
        metaRow.Add(metaText);
        card.Add(metaRow);

        // 5) tier dot row + plan chip + plan score
        var tierPlan = new VisualElement();
        tierPlan.AddToClassList("rcp-card__tier-plan-row");
        var tierRow = new VisualElement();
        tierRow.AddToClassList("rcp-card__tier-row");
        for (var i = 0; i < 3; i++)
        {
            var dot = new VisualElement();
            dot.AddToClassList("rcp-card__tier-dot");
            if (i < (int)c.Tier) dot.AddToClassList("rcp-card__tier-dot--filled");
            tierRow.Add(dot);
        }
        tierPlan.Add(tierRow);

        // plan fit chip + plan score (Metadata.PlanScore.Total — 6-component breakdown 합)
        var planGroup = new VisualElement();
        planGroup.AddToClassList("rcp-card__plan-group");
        var planChip = new Label(PlanLabel(c.PlanFit));
        planChip.AddToClassList("rcp-card__plan-chip");
        planChip.AddToClassList($"rcp-card__plan-chip--{PlanClassSuffix(c.PlanFit)}");
        planGroup.Add(planChip);
        var planScore = new Label($"+{c.PlanScore}");
        planScore.AddToClassList("rcp-card__plan-score");
        planScore.tooltip = "Plan score — breakpoint / native tag / role need / augment hook / scout / oversaturation 합산";
        planGroup.Add(planScore);
        tierPlan.Add(planGroup);
        card.Add(tierPlan);

        var stateRow = new VisualElement();
        stateRow.AddToClassList("rcp-card__state-row");
        foreach (var chipText in c.StateChips)
        {
            stateRow.Add(BuildChip(chipText, "rcp-card__state-chip"));
        }
        card.Add(stateRow);

        // 6) tag chip row (◐ archetype RecruitPlanTags ≤3 — offer field 아님)
        var tagRow = new VisualElement();
        tagRow.AddToClassList("rcp-card__tag-row");
        foreach (var t in c.Tags)
        {
            var tag = new Label(t);
            tag.AddToClassList("rcp-card__tag");
            tagRow.Add(tag);
        }
        card.Add(tagRow);

        var skillIcons = new VisualElement();
        skillIcons.AddToClassList("rcp-card__skill-icons");
        skillIcons.Add(BuildSkillPip("SA", c.SigActive, "sig"));
        skillIcons.Add(BuildSkillPip("SP", c.SigPassive, "sig"));
        skillIcons.Add(BuildSkillPip("FA", c.FlexActive, "flex"));
        skillIcons.Add(BuildSkillPip("FP", c.FlexPassive, "flex"));
        card.Add(skillIcons);

        // 7) skill summary block — SIG(archetype 고정) gold / FLX(offer rolled) blue
        var skills = new VisualElement();
        skills.AddToClassList("rcp-card__skills");
        skills.Add(BuildSkillLine("SIG A", c.SigActive, "sig"));
        skills.Add(BuildSkillLine("SIG P", c.SigPassive, "sig"));
        skills.Add(BuildSkillLine("FLX A", c.FlexActive, "flex"));
        skills.Add(BuildSkillLine("FLX P", c.FlexPassive, "flex"));
        card.Add(skills);

        // 8) gold cost
        var costRow = new VisualElement();
        costRow.AddToClassList("rcp-card__cost-row");
        var costLabel = new Label($"{c.GoldCost} Gold");
        costLabel.AddToClassList("rcp-card__cost-label");
        costRow.Add(costLabel);
        card.Add(costRow);

        // 9) per-card Recruit button
        var recruitBtn = new Button { text = "RECRUIT" };
        recruitBtn.AddToClassList("rcp-card__recruit");
        recruitBtn.AddToClassList("sm-cta");
        recruitBtn.AddToClassList("sm-cta--md");
        recruitBtn.AddToClassList("sm-cta--primary");
        var slotIndex = c.SlotIndex;
        recruitBtn.clicked += () => _actions?.OnRecruitConfirmed(slotIndex);
        card.Add(recruitBtn);

        return card;
    }

    private void RenderSelectedDetail(RecruitSelectedCandidateDetailViewState? detail)
    {
        if (detail == null)
        {
            if (_detailNameLabel != null) _detailNameLabel.text = string.Empty;
            if (_detailMetaLabel != null) _detailMetaLabel.text = string.Empty;
            if (_detailPlanLabel != null) _detailPlanLabel.text = string.Empty;
            if (_detailCostLabel != null) _detailCostLabel.text = string.Empty;
            if (_detailSkillLabel != null) _detailSkillLabel.text = string.Empty;
            _detailStateChips?.Clear();
            _detailTags?.Clear();
            _detailPortrait?.Clear();
            _detailClassGlyph?.Clear();
            _detailMetricBars?.Clear();
            _detailSkillPips?.Clear();
            return;
        }

        if (_detailNameLabel != null) _detailNameLabel.text = detail.DisplayName;
        if (_detailMetaLabel != null) _detailMetaLabel.text = $"{detail.ClassLabel} / {detail.TierLabel}";
        if (_detailPlanLabel != null) _detailPlanLabel.text = $"{detail.PlanFitLabel} {detail.PlanScoreLabel}";
        if (_detailCostLabel != null) _detailCostLabel.text = detail.CostLabel;
        if (_detailSkillLabel != null)
        {
            _detailSkillLabel.text = detail.SkillSummary;
            _detailSkillLabel.tooltip = detail.SkillSummary;
        }

        RenderChipRow(_detailStateChips, detail.StateChips, "rcp-detail__state-chip");
        RenderChipRow(_detailTags, detail.Tags, "rcp-detail__tag");
        RenderPortrait(_detailPortrait, detail.PortraitSprite, detail.DisplayName, detail.ClassKey);
        RenderClassGlyph(_detailClassGlyph, detail.ClassSprite, detail.ClassKey);
        RenderMetrics(_detailMetricBars, detail.Metrics);
        RenderSkillPips(_detailSkillPips, detail.SkillPips);
    }

    private void RenderRosterPressure(RecruitRosterPressureViewState pressure)
    {
        if (_pressureRosterLabel != null) _pressureRosterLabel.text = pressure.RosterCountLabel;
        if (_pressureNeedLabel != null) _pressureNeedLabel.text = pressure.NeedLabel;
        if (_pressurePlanLabel != null) _pressurePlanLabel.text = pressure.PlanSummaryLabel;
        RenderChipRow(_pressureNeedChips, pressure.NeedChips, "rcp-pressure__need-chip");
    }

    private static void RenderChipRow(VisualElement? row, IReadOnlyList<string> chips, string className)
    {
        if (row == null)
        {
            return;
        }

        row.Clear();
        foreach (var chip in chips)
        {
            row.Add(BuildChip(chip, className));
        }
    }

    private static Label BuildChip(string text, string className)
    {
        var chip = new Label(text);
        chip.AddToClassList(className);
        return chip;
    }

    private static VisualElement BuildSkillLine(string slotKey, string skillText, string variant)
    {
        var line = new VisualElement();
        line.AddToClassList("rcp-card__skill-line");
        line.AddToClassList($"rcp-card__skill-line--{variant}");

        var slotLabel = new Label(slotKey);
        slotLabel.AddToClassList("rcp-card__skill-slot");
        line.Add(slotLabel);

        var nameLabel = new Label(skillText);
        nameLabel.AddToClassList("rcp-card__skill-name");
        line.Add(nameLabel);

        return line;
    }

    private static VisualElement BuildSkillPip(string slotKey, string skillText, string variant)
    {
        var pip = new VisualElement();
        pip.AddToClassList("rcp-skill-pip");
        pip.AddToClassList($"rcp-skill-pip--{variant}");
        pip.tooltip = $"{slotKey} · {skillText}";

        var slot = new Label(slotKey);
        slot.AddToClassList("rcp-skill-pip__slot");
        pip.Add(slot);

        return pip;
    }

    private static void RenderPortrait(VisualElement? portrait, Texture2D? sprite, string displayName, string classKey)
    {
        if (portrait == null)
        {
            return;
        }

        portrait.Clear();
        portrait.style.backgroundImage = StyleKeyword.Null;
        AddClassVariant(portrait, "rcp-class", classKey);
        if (sprite != null)
        {
            portrait.style.backgroundImage = new StyleBackground(sprite);
        }
        else
        {
            var fallback = new Label(BuildFallbackInitials(displayName));
            fallback.AddToClassList("rcp-detail__portrait-fallback");
            portrait.Add(fallback);
        }

        var shade = new VisualElement();
        shade.AddToClassList("rcp-detail__portrait-shade");
        portrait.Add(shade);
    }

    private static void RenderClassGlyph(VisualElement? glyph, Texture2D? sprite, string classKey)
    {
        if (glyph == null)
        {
            return;
        }

        glyph.Clear();
        glyph.style.backgroundImage = StyleKeyword.Null;
        AddClassVariant(glyph, "rcp-class", classKey);
        if (sprite != null)
        {
            glyph.style.backgroundImage = new StyleBackground(sprite);
        }
    }

    private static void RenderMetrics(VisualElement? row, IReadOnlyList<RecruitDecisionMetricViewState> metrics)
    {
        if (row == null)
        {
            return;
        }

        row.Clear();
        foreach (var metric in metrics)
        {
            var item = new VisualElement();
            item.AddToClassList("rcp-detail__metric");
            item.AddToClassList($"rcp-detail__metric--{metric.Tone}");

            var head = new VisualElement();
            head.AddToClassList("rcp-detail__metric-head");
            var label = new Label(metric.Label);
            label.AddToClassList("rcp-detail__metric-label");
            head.Add(label);
            var value = new Label($"{metric.Value}%");
            value.AddToClassList("rcp-detail__metric-value");
            head.Add(value);
            item.Add(head);

            var track = new VisualElement();
            track.AddToClassList("rcp-detail__metric-track");
            var fill = new VisualElement();
            fill.AddToClassList("rcp-detail__metric-fill");
            fill.style.width = Length.Percent(Mathf.Clamp(metric.Value, 0, 100));
            track.Add(fill);
            item.Add(track);

            row.Add(item);
        }
    }

    private static void RenderSkillPips(VisualElement? row, IReadOnlyList<RecruitSkillPipViewState> pips)
    {
        if (row == null)
        {
            return;
        }

        row.Clear();
        foreach (var pip in pips)
        {
            row.Add(BuildSkillPip(pip.SlotLabel, pip.SkillLabel, pip.Variant));
        }
    }

    private void RenderActionBar(RecruitActionBarViewState bar)
    {
        if (_scoutLabel != null)
        {
            // wave-31 GPT Pro patch: English enum → 한국어 localized (production hygiene).
            _scoutLabel.text = bar.CanUseScout
                ? $"정찰 · {bar.ScoutDirectiveLabel} (-{bar.ScoutEchoCost} 잔향)"
                : "정찰 — 사용됨";
        }
        if (_refreshLabel != null)
        {
            _refreshLabel.text = bar.FreeRefreshesRemaining > 0
                ? "무료 갱신"
                : $"갱신 (-{bar.CurrentPaidRefreshCost} G)";
        }
    }

    private void HandleScoutClicked() => _actions?.OnScoutClicked();
    private void HandleRefreshClicked() => _actions?.OnRefreshClicked();

    private static void AddClassVariant(VisualElement element, string prefix, string classKey)
    {
        if (element == null || string.IsNullOrWhiteSpace(classKey))
        {
            return;
        }

        element.AddToClassList($"{prefix}-{classKey.Trim().ToLowerInvariant()}");
    }

    private static string BuildFallbackInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "?";
        }

        var parts = value.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return value[..1].ToUpperInvariant();
        }

        return string.Concat(parts.Take(2).Select(part => part[..1])).ToUpperInvariant();
    }

    // wave-31 GPT Pro patch: English enum → 한국어 localized (production hygiene).
    private static string SlotLabel(RecruitSlotType slot, bool scoutBias) => slot switch
    {
        RecruitSlotType.OnPlan    => scoutBias ? "계획 적합 · 정찰" : "계획 적합",
        RecruitSlotType.Protected => "보호됨",
        RecruitSlotType.StandardA => "표준 A",
        RecruitSlotType.StandardB => "표준 B",
        _ => slot.ToString(),
    };

    private static string SlotClassSuffix(RecruitSlotType slot) => slot switch
    {
        RecruitSlotType.OnPlan    => "on-plan",
        RecruitSlotType.Protected => "protected",
        _                          => "standard",
    };

    // wave-31 GPT Pro patch: English enum → 한국어 localized (production hygiene).
    private static string PlanLabel(RecruitPlanFit plan) => plan switch
    {
        RecruitPlanFit.OnPlan  => "계획 적합",
        RecruitPlanFit.Bridge  => "연결",
        RecruitPlanFit.OffPlan => "계획 외",
        _ => plan.ToString(),
    };

    private static string PlanClassSuffix(RecruitPlanFit plan) => plan switch
    {
        RecruitPlanFit.OnPlan  => "on-plan",
        RecruitPlanFit.Bridge  => "bridge",
        RecruitPlanFit.OffPlan => "off-plan",
        _ => "off-plan",
    };
}

public interface IRecruitActions
{
    void OnCandidateSelected(int slotIndex);
    void OnRecruitConfirmed(int slotIndex);
    void OnScoutClicked();
    void OnRefreshClicked();
}
