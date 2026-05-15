using System;
using System.Collections.Generic;
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
    private readonly VisualElement? _modalRoot;
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

    /// <summary>hub modal opener가 호출 — modal 표시 + sm-modal-anim 진입 transition.</summary>
    public void Open()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.Flex;
        _modalRoot.RemoveFromClassList("sm-modal-anim--enter");
    }

    /// <summary>hub modal opener가 호출 — modal 숨김.</summary>
    public void Close()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.None;
        _modalRoot.AddToClassList("sm-modal-anim--enter");
    }

    public void Render(RecruitViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        RenderCards(state.Candidates);
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
        if (c.SlotType == RecruitSlotType.Protected) card.AddToClassList("rcp-card--protected");
        if (c.SlotType == RecruitSlotType.OnPlan) card.AddToClassList("rcp-card--on-plan");

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
        if (c.PortraitSprite != null) portrait.style.backgroundImage = new StyleBackground(c.PortraitSprite);
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
        if (c.ClassSprite != null) classGlyph.style.backgroundImage = new StyleBackground(c.ClassSprite);
        metaRow.Add(classGlyph);
        var metaText = new Label(c.ClassKey);
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

    private void RenderActionBar(RecruitActionBarViewState bar)
    {
        if (_scoutLabel != null)
        {
            // scout는 6-kind directive (Frontline/Backline/Physical/Magical/Support/SynergyTag) — directive 표시명 노출
            _scoutLabel.text = bar.CanUseScout
                ? $"SCOUT · {bar.ScoutDirectiveLabel} (-{bar.ScoutEchoCost} Echo)"
                : "SCOUT — USED";
        }
        if (_refreshLabel != null)
        {
            _refreshLabel.text = bar.FreeRefreshesRemaining > 0
                ? "REFRESH — FREE"
                : $"REFRESH (-{bar.CurrentPaidRefreshCost} Gold)";
        }
    }

    private void HandleScoutClicked() => _actions?.OnScoutClicked();
    private void HandleRefreshClicked() => _actions?.OnRefreshClicked();

    private static string SlotLabel(RecruitSlotType slot, bool scoutBias) => slot switch
    {
        RecruitSlotType.OnPlan    => scoutBias ? "ON PLAN · SCOUT" : "ON PLAN",
        RecruitSlotType.Protected => "PROTECTED",
        RecruitSlotType.StandardA => "STANDARD A",
        RecruitSlotType.StandardB => "STANDARD B",
        _ => slot.ToString(),
    };

    private static string SlotClassSuffix(RecruitSlotType slot) => slot switch
    {
        RecruitSlotType.OnPlan    => "on-plan",
        RecruitSlotType.Protected => "protected",
        _                          => "standard",
    };

    private static string PlanLabel(RecruitPlanFit plan) => plan switch
    {
        RecruitPlanFit.OnPlan  => "ON PLAN",
        RecruitPlanFit.Bridge  => "BRIDGE",
        RecruitPlanFit.OffPlan => "OFF PLAN",
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
    void OnRecruitConfirmed(int slotIndex);
    void OnScoutClicked();
    void OnRefreshClicked();
}
