using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Passive Board V1 surface View. UXML container: BoardHeader / BoardCanvas / DetailIcon / FooterBreakdown.
/// per-hero 컨텍스트 — 보드는 hero 클래스로 고정 (자유 탭 전환 폐기). Render 시 각 container clear + 재구축.
/// </summary>
public sealed class PassiveBoardView
{
    private readonly VisualElement _boardHeader;
    private readonly VisualElement _boardCanvas;
    private readonly VisualElement _detailIcon;
    private readonly Label? _tierLabel;
    private readonly Label? _detailTitle;
    private readonly Label? _detailDesc;
    private readonly Label? _detailTags;
    private readonly Label? _availableLabel;
    private readonly Button? _activateButton;
    private readonly Label _footerBreakdown;
    private readonly VisualElement? _modalRoot;
    private readonly Button? _closeButton;

    private IPassiveBoardActions? _actions;

    public void BindClose(Action close)
    {
        if (_closeButton == null || close == null) return;
        _closeButton.clicked += close;
    }

    public void Open()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.Flex;
        _modalRoot.RemoveFromClassList("sm-modal-anim--enter");
        var wrapper = _modalRoot.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        if (_modalRoot == null) return;
        _modalRoot.style.display = DisplayStyle.None;
        _modalRoot.AddToClassList("sm-modal-anim--enter");
        var wrapper = _modalRoot.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.None;
    }

    public PassiveBoardView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _modalRoot = root.Q<VisualElement>("PbpRoot");
        _closeButton = root.Q<Button>(className: "pbp-footer__close");   // UXML footer에 X 버튼 있음
        _boardHeader = root.Q<VisualElement>("BoardHeader")
            ?? throw new ArgumentException("BoardHeader 못 찾음");
        _boardCanvas = root.Q<VisualElement>("BoardCanvas")
            ?? throw new ArgumentException("BoardCanvas 못 찾음");
        _detailIcon = root.Q<VisualElement>("DetailIcon")
            ?? throw new ArgumentException("DetailIcon 못 찾음");
        _footerBreakdown = root.Q<Label>("FooterBreakdown")
            ?? throw new ArgumentException("FooterBreakdown 못 찾음");

        // optional detail panel labels (V1 UXML static element)
        _tierLabel = root.Q<Label>(className: "pbp-detail__tier-label");
        _detailTitle = root.Q<Label>(className: "pbp-detail__title");
        _detailDesc = root.Q<Label>("DetailDesc");
        _detailTags = root.Q<Label>("DetailTags");
        _availableLabel = root.Q<Label>(className: "pbp-detail__available");
        _activateButton = root.Q<Button>(className: "pbp-detail__activate");
    }

    public void Bind(IPassiveBoardActions actions)
    {
        _actions = actions;
        if (_activateButton != null)
        {
            _activateButton.clicked -= HandleActivateClicked;
            _activateButton.clicked += HandleActivateClicked;
        }
    }

    public void Render(PassiveBoardViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        RenderHeader(state.Header);
        RenderNodes(state.Nodes);
        RenderDetail(state.Detail);
        _footerBreakdown.text = state.Footer.BreakdownText;
    }

    // per-hero 헤더 — hero portrait + 이름 + 클래스 보드 인디케이터 (고정, 비대화).
    private void RenderHeader(PassiveBoardHeaderViewState header)
    {
        _boardHeader.Clear();

        var portrait = new VisualElement();
        portrait.AddToClassList("pbp-header__portrait");
        portrait.AddToClassList($"pbp-header__portrait--{header.ClassKey}");
        if (header.HeroPortrait != null)
            portrait.style.backgroundImage = new StyleBackground(header.HeroPortrait);
        _boardHeader.Add(portrait);

        var info = new VisualElement();
        info.AddToClassList("pbp-header__info");

        var heroName = new Label(header.HeroDisplayName);
        heroName.AddToClassList("pbp-header__hero-name");
        info.Add(heroName);

        var classRow = new VisualElement();
        classRow.AddToClassList("pbp-header__class-row");
        var classIcon = new VisualElement();
        classIcon.AddToClassList("pbp-header__class-icon");
        if (header.ClassIconSprite != null)
            classIcon.style.backgroundImage = new StyleBackground(header.ClassIconSprite);
        classRow.Add(classIcon);
        var classLabel = new Label(header.ClassLabel);
        classLabel.AddToClassList("pbp-header__class-label");
        classRow.Add(classLabel);
        info.Add(classRow);

        _boardHeader.Add(info);
    }

    private void RenderNodes(IReadOnlyList<PassiveBoardNodeViewState> nodes)
    {
        _boardCanvas.Clear();
        foreach (var n in nodes)
        {
            var node = new VisualElement();
            node.AddToClassList("pbp-board__node");
            node.AddToClassList("sm-select-snap");   // 콘솔급 motion — 클릭 snap
            node.AddToClassList($"pbp-board__node--{n.KindKey}");
            if (n.IsActive) node.AddToClassList("pbp-board__node--active");
            node.style.left = new StyleLength(new Length(n.Left * 100f, LengthUnit.Percent));
            node.style.top = new StyleLength(new Length(n.Top * 100f, LengthUnit.Percent));
            if (n.IconSprite != null) node.style.backgroundImage = new StyleBackground(n.IconSprite);

            node.tooltip = $"{n.NodeId}\n[{n.KindKey}] {n.RuleSummary}\ntags: {n.Tags}\n{(n.IsActive ? "ACTIVE" : "inactive")}";
            node.RegisterCallback<ClickEvent>(_ => _actions?.OnNodeSelected(n.NodeId));
            _boardCanvas.Add(node);
        }
    }

    private void RenderDetail(PassiveBoardDetailViewState detail)
    {
        if (detail.IconSprite != null) _detailIcon.style.backgroundImage = new StyleBackground(detail.IconSprite);
        if (_tierLabel != null) _tierLabel.text = detail.KindLabel;
        if (_detailTitle != null) _detailTitle.text = detail.TitleText;
        if (_detailDesc != null) _detailDesc.text = detail.RuleSummary;
        if (_detailTags != null)
        {
            _detailTags.text = detail.Tags;
            _detailTags.style.display = string.IsNullOrEmpty(detail.Tags)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
        if (_availableLabel != null) _availableLabel.text = detail.AvailableLabel;
        if (_activateButton != null) _activateButton.text = detail.ButtonLabel;
    }

    private void HandleActivateClicked()
    {
        // V1: activate/deactivate는 selected node 기준. presenter는 selected state 보유.
        _actions?.OnToggleActivateClicked();
    }
}

public interface IPassiveBoardActions
{
    void OnNodeSelected(string nodeId);
    void OnToggleActivateClicked();
}
