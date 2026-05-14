using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Permanent Augment V1 surface View — "장착한 영구 augment 1개 + 해금 후보 풀" 모델 (audit §4.1 다운스코프).
/// UXML container: AugmentGrid / DetailHero / DetailTitle / DetailSubtitle / DetailEffect / DetailFlavor /
/// StatCompare(meta rows) / EquipSlot / EquipCta. Render(ViewState) 시 각 container clear + 재구축.
/// posture 그리드 / progress / family 색축 폐기 — cell 상태축은 equipped / unlocked / locked 3종.
/// </summary>
public sealed class PermanentAugmentView
{
    private readonly VisualElement _augmentGrid;
    private readonly VisualElement _detailHero;
    private readonly Label _detailTitle;
    private readonly Label _detailSubtitle;
    private readonly Label _detailEffect;
    private readonly Label _detailFlavor;
    private readonly VisualElement _metaRows;
    private readonly VisualElement _equipSlot;
    private readonly Button? _equipCta;
    private readonly Label? _equipCtaLabel;

    // glow halo 위에 합성하는 augment icon — 최초 Render 시 1회 생성 후 재사용.
    private VisualElement? _detailHeroIcon;
    private PermanentAugmentDetailViewState? _detail;

    private IPermanentAugmentActions? _actions;

    public PermanentAugmentView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _augmentGrid = root.Q<VisualElement>("AugmentGrid")
            ?? throw new ArgumentException("AugmentGrid 못 찾음");
        _detailHero = root.Q<VisualElement>("DetailHero")
            ?? throw new ArgumentException("DetailHero 못 찾음");
        _detailTitle = root.Q<Label>("DetailTitle")
            ?? throw new ArgumentException("DetailTitle 못 찾음");
        _detailSubtitle = root.Q<Label>("DetailSubtitle")
            ?? throw new ArgumentException("DetailSubtitle 못 찾음");
        _detailEffect = root.Q<Label>("DetailEffect")
            ?? throw new ArgumentException("DetailEffect 못 찾음");
        _detailFlavor = root.Q<Label>("DetailFlavor")
            ?? throw new ArgumentException("DetailFlavor 못 찾음");
        _metaRows = root.Q<VisualElement>("StatCompare")
            ?? throw new ArgumentException("StatCompare 못 찾음");
        _equipSlot = root.Q<VisualElement>("EquipSlot")
            ?? throw new ArgumentException("EquipSlot 못 찾음");
        // equip CTA는 없어도 preview가 깨지지 않게 nullable.
        _equipCta = root.Q<Button>(className: "pap-equip-cta");
        _equipCtaLabel = root.Q<Label>("EquipCtaLabel");
    }

    public void Bind(IPermanentAugmentActions actions)
    {
        _actions = actions;
        if (_equipCta != null)
        {
            _equipCta.clicked -= HandleEquipClicked;
            _equipCta.clicked += HandleEquipClicked;
        }
    }

    public void Render(PermanentAugmentViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        RenderGrid(state.Cells);
        RenderDetail(state.Detail);
    }

    private void RenderGrid(IReadOnlyList<PermanentAugmentCellViewState> cells)
    {
        _augmentGrid.Clear();
        foreach (var spec in cells)
        {
            var cell = new VisualElement();
            cell.AddToClassList("pap-augment-cell");
            cell.AddToClassList("sm-hover-raise");   // 콘솔급 motion — hover raise
            if (spec.IsSelected) cell.AddToClassList("pap-augment-cell--selected");
            if (spec.IsEquipped) cell.AddToClassList("pap-augment-cell--equipped");
            if (!spec.Unlocked) cell.AddToClassList("pap-augment-cell--locked");

            // family bucket — 정보 표기용 neutral chip (색축 폐기 — AugmentDefinition.FamilyId)
            var familyChip = new Label(spec.FamilyBucket);
            familyChip.AddToClassList("pap-augment-cell__family");
            cell.Add(familyChip);

            var iconWrap = new VisualElement();
            iconWrap.AddToClassList("pap-augment-cell__icon");
            if (spec.IconSprite != null) iconWrap.style.backgroundImage = new StyleBackground(spec.IconSprite);
            cell.Add(iconWrap);

            var name = new Label(spec.KoLabel);
            name.AddToClassList("pap-augment-cell__name");
            cell.Add(name);

            // status footer — equipped / unlocked / locked 3종 (progress 폐기)
            var statusRow = new VisualElement();
            statusRow.AddToClassList("pap-augment-cell__status");
            var statusText = new Label(StatusLabel(spec));
            statusText.AddToClassList("pap-augment-cell__status-text");
            statusText.AddToClassList($"pap-augment-cell__status-text--{StatusSuffix(spec)}");
            statusRow.Add(statusText);
            cell.Add(statusRow);

            cell.tooltip = $"{spec.AugmentId}\nfamily: {spec.FamilyBucket}";
            cell.RegisterCallback<ClickEvent>(_ => _actions?.OnAugmentSelected(spec.AugmentId));
            _augmentGrid.Add(cell);
        }
    }

    private void RenderDetail(PermanentAugmentDetailViewState detail)
    {
        _detail = detail;

        // Detail hero — glow halo 뒤판 위에 selected augment icon 합성.
        if (_detailHeroIcon == null)
        {
            _detailHeroIcon = new VisualElement();
            _detailHeroIcon.AddToClassList("pap-detail-hero__icon");
            _detailHeroIcon.pickingMode = PickingMode.Ignore;
            _detailHero.Add(_detailHeroIcon);
        }
        if (detail.IconSprite != null)
            _detailHeroIcon.style.backgroundImage = new StyleBackground(detail.IconSprite);

        _detailTitle.text = detail.KoLabel;
        // subtitle — posture 매핑 폐기, 영문 별칭 + family bucket
        _detailSubtitle.text = $"{detail.EnLabel}  ·  {detail.FamilyBucket}";
        _detailEffect.text = detail.SignatureEffect;
        _detailFlavor.text = detail.FlavorText;

        if (detail.IconSprite != null)
            _equipSlot.style.backgroundImage = new StyleBackground(detail.IconSprite);

        _metaRows.Clear();
        foreach (var line in detail.MetaRows)
        {
            var row = new VisualElement();
            row.AddToClassList("pap-meta-row");

            var lbl = new Label(line.Label);
            lbl.AddToClassList("pap-meta-row__label");
            row.Add(lbl);

            var val = new Label(line.Value);
            val.AddToClassList("pap-meta-row__value");
            row.Add(val);

            _metaRows.Add(row);
        }

        // Equip CTA — 단일 슬롯 equip. equipped / unlocked / locked 상태 분기.
        if (_equipCtaLabel != null)
        {
            _equipCtaLabel.text = detail.IsEquipped
                ? "장착됨"
                : detail.IsUnlocked ? "장착" : "🔒 미해금";
        }
        _equipCta?.SetEnabled(detail.IsUnlocked && !detail.IsEquipped);
    }

    private void HandleEquipClicked()
    {
        if (_detail != null && _detail.IsUnlocked && !_detail.IsEquipped)
            _actions?.OnEquipAugment(_detail.SelectedAugmentId);
    }

    private static string StatusLabel(PermanentAugmentCellViewState spec)
    {
        if (spec.IsEquipped) return "✓ 장착 중";
        return spec.Unlocked ? "해금됨 — 장착 가능" : "🔒 미해금";
    }

    private static string StatusSuffix(PermanentAugmentCellViewState spec)
    {
        if (spec.IsEquipped) return "equipped";
        return spec.Unlocked ? "unlocked" : "locked";
    }
}

public interface IPermanentAugmentActions
{
    void OnAugmentSelected(string augmentId);
    void OnEquipAugment(string augmentId);
}
