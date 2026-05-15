using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// RosterGrid V1 surface View — Town hub default panel.
/// UXML container: GridContainer / HeroCount / FilterStrip. HeroPortraitCard atom template은 ctor inject.
/// 본 View는 atom 자체를 건드리지 않고 RosterGrid 전용 overlay layer만 추가 (다른 consumer 보호).
/// </summary>
public sealed class RosterGridView
{
    private readonly VisualElement _gridContainer;
    private readonly Label? _heroCountLabel;
    private readonly VisualTreeAsset? _heroCardTemplate;
    private readonly VisualElement? _modalRoot;
    private readonly Button? _closeButton;

    private IRosterGridActions? _actions;

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

    public RosterGridView(VisualElement root, VisualTreeAsset? heroCardTemplate)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _heroCardTemplate = heroCardTemplate;   // production hub modal은 null 허용 (heroCardTemplate은 후속 task)
        _modalRoot = root.Q<VisualElement>("RosterGridPreviewRoot");
        _closeButton = root.Q<Button>(className: "rgp-header__close");
        _gridContainer = root.Q<VisualElement>("GridContainer")
            ?? throw new ArgumentException("GridContainer 못 찾음");
        _heroCountLabel = root.Q<Label>("HeroCount");
    }

    public void Bind(IRosterGridActions actions)
    {
        _actions = actions;
    }

    public void Render(RosterGridViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        _gridContainer.Clear();
        foreach (var hero in state.Heroes)
        {
            _gridContainer.Add(BuildHeroCard(hero));
        }

        if (_heroCountLabel != null)
        {
            _heroCountLabel.text = $"{state.Heroes.Count} / {state.RosterCap}";
        }
    }

    private VisualElement BuildHeroCard(RosterGridHeroCardViewState hero)
    {
        var instance = _heroCardTemplate.Instantiate();
        var card = instance.Q<VisualElement>("HeroPortraitCard");
        if (card != null)
        {
            ApplyHeroCardClasses(card, hero);
            ApplyHeroCardLabels(card, hero);
            InjectProfileOverlay(card, hero);
            card.RegisterCallback<ClickEvent>(_ => _actions?.OnHeroSelected(hero.HeroId));
        }
        return instance;
    }

    private static void ApplyHeroCardClasses(VisualElement card, RosterGridHeroCardViewState hero)
    {
        // state(KO/injured/selected)는 roster 영속 필드 아님 — default 고정. run/battle 컨텍스트에서만 의미.
        RemoveClasses(card, "sm-hpc--default", "sm-hpc--selected", "sm-hpc--ko", "sm-hpc--injured");
        card.AddToClassList("sm-hpc--default");

        RemoveClasses(card,
            "sm-hpc--fam-beastkin", "sm-hpc--fam-vanguard", "sm-hpc--fam-striker",
            "sm-hpc--fam-ranger", "sm-hpc--fam-mystic");
        card.AddToClassList($"sm-hpc--fam-{hero.FamilyKey}");

        RemoveClasses(card, "sm-hpc--rare-common", "sm-hpc--rare-rare", "sm-hpc--rare-epic");
        card.AddToClassList($"sm-hpc--rare-{hero.RarityKey}");
    }

    private static void ApplyHeroCardLabels(VisualElement card, RosterGridHeroCardViewState hero)
    {
        // HeroInstanceRecord.Name은 단일 필드 — atom의 name-ko에 표시, name-en은 비움.
        var nameKo = card.Q<Label>("name-ko");
        var nameEn = card.Q<Label>("name-en");
        var archetype = card.Q<Label>("archetype-text");
        if (nameKo != null) nameKo.text = hero.DisplayName;
        if (nameEn != null) nameEn.text = string.Empty;
        if (archetype != null) archetype.text = hero.ArchetypeLabel;
    }

    /// <summary>
    /// HeroPortraitCard atom 위에 RosterGrid 전용 profile column overlay.
    /// atom 자체는 다른 consumer (Battle, Recruit detail 등)에서도 쓰니까 건드리지 않음.
    /// </summary>
    private static void InjectProfileOverlay(VisualElement card, RosterGridHeroCardViewState hero)
    {
        // 중복 호출 방지
        var existing = card.Q<VisualElement>("RosterProfileOverlay");
        existing?.RemoveFromHierarchy();

        var overlay = new VisualElement { name = "RosterProfileOverlay" };
        overlay.AddToClassList("rgp-profile-overlay");
        overlay.pickingMode = PickingMode.Ignore;

        // equip slot dots — top-left
        var equipRow = new VisualElement();
        equipRow.AddToClassList("rgp-profile-overlay__equip-row");
        for (var i = 0; i < 3; i++)
        {
            var dot = new VisualElement();
            dot.AddToClassList("rgp-profile-overlay__equip-dot");
            if (i < hero.EquipSlots) dot.AddToClassList("rgp-profile-overlay__equip-dot--filled");
            equipRow.Add(dot);
        }
        overlay.Add(equipRow);

        // level + xp bar — bottom edge
        var levelRow = new VisualElement();
        levelRow.AddToClassList("rgp-profile-overlay__level-row");
        var lvlLabel = new Label($"Lv {hero.Level}");
        lvlLabel.AddToClassList("rgp-profile-overlay__level-label");
        levelRow.Add(lvlLabel);
        var xpBar = new VisualElement();
        xpBar.AddToClassList("rgp-profile-overlay__xp-bar");
        var xpFill = new VisualElement();
        xpFill.AddToClassList("rgp-profile-overlay__xp-fill");
        xpFill.style.width = new StyleLength(new Length(hero.XpPct, LengthUnit.Percent));
        xpBar.Add(xpFill);
        levelRow.Add(xpBar);
        overlay.Add(levelRow);

        card.Add(overlay);
    }

    private static void RemoveClasses(VisualElement card, params string[] classes)
    {
        foreach (var c in classes)
        {
            if (card.ClassListContains(c)) card.RemoveFromClassList(c);
        }
    }
}

public interface IRosterGridActions
{
    void OnHeroSelected(string heroId);
    void OnFilterSelected(string filterKey);
    void OnQuickBattleClicked();
}
