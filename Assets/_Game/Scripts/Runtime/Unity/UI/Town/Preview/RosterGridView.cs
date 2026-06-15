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
public sealed class RosterGridView : IRosterGridView
{
    private readonly VisualElement _gridContainer;
    private readonly Label? _heroCountLabel;
    private readonly VisualTreeAsset? _heroCardTemplate;
    private readonly VisualElement? _modalRoot;
    private readonly Button? _closeButton;
    private readonly VisualElement? _filterStrip;
    private readonly Button? _quickBattleButton;
    private readonly Func<string, Texture2D?>? _portraitLoader;

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

    public RosterGridView(VisualElement root, VisualTreeAsset? heroCardTemplate, Func<string, Texture2D?>? portraitLoader = null)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _heroCardTemplate = heroCardTemplate;   // production hub modal은 null 허용 (heroCardTemplate은 후속 task)
        _portraitLoader = portraitLoader;       // CharacterId → 초상화 Texture. null이면 이니셜 placeholder 유지.
        _modalRoot = root.Q<VisualElement>("RosterGridPreviewRoot");
        _closeButton = root.Q<Button>(className: "rgp-header__close");
        _gridContainer = root.Q<VisualElement>("GridContainer")
            ?? throw new ArgumentException("GridContainer 못 찾음");
        _heroCountLabel = root.Q<Label>("HeroCount");
        _filterStrip = root.Q<VisualElement>("FilterStrip");
        _quickBattleButton = root.Q<Button>("QuickBattleButton");
    }

    public void Bind(IRosterGridActions actions)
    {
        _actions = actions;
        // 빠른 전투 CTA — Presenter.OnQuickBattleClicked는 이미 구현돼 있음(스모크 진입). 버튼만 연결.
        if (_quickBattleButton != null)
        {
            _quickBattleButton.clicked += () => _actions?.OnQuickBattleClicked();
        }
    }

    public void Render(RosterGridViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        RenderFilters(state.Filters);

        _gridContainer.Clear();
        if (state.Heroes.Count == 0)
        {
            // 빈 명부를 텅 빈 그리드로 두지 않고 다음 행동을 안내(신규 프로필/전원 이탈 시).
            var empty = new Label("아직 합류한 동료가 없습니다. 모집소(달목)에서 동료를 영입하세요.");
            empty.AddToClassList("rgp-empty-state");
            _gridContainer.Add(empty);
        }
        foreach (var hero in state.Heroes)
        {
            _gridContainer.Add(BuildHeroCard(hero));
        }

        if (_heroCountLabel != null)
        {
            _heroCountLabel.text = $"{state.Heroes.Count} / {state.RosterCap}";
        }
    }

    // 필터 칩을 data-driven으로 재구축 — UXML 하드코딩 칩(세력명, 핸들러 0)을 제거하고
    // Presenter가 로스터 실재 race/class로 만든 칩만 렌더 + 클릭 wire. group 전환 시 divider 삽입.
    private void RenderFilters(IReadOnlyList<RosterGridFilterChipViewState>? filters)
    {
        if (_filterStrip == null)
        {
            return;
        }

        _filterStrip.Clear();
        if (filters == null || filters.Count == 0)
        {
            return;
        }

        string? previousGroup = null;
        foreach (var chip in filters)
        {
            if (previousGroup != null && chip.GroupKey != previousGroup)
            {
                var divider = new VisualElement();
                divider.AddToClassList("rgp-filter-divider");
                divider.pickingMode = PickingMode.Ignore;
                _filterStrip.Add(divider);
            }
            previousGroup = chip.GroupKey;

            // 칩 라벨에 인원수 부기 — "전위 3"처럼 탐색을 돕는다(0이면 수치 생략).
            var button = new Button { text = chip.Count > 0 ? $"{chip.Label} {chip.Count}" : chip.Label };
            button.AddToClassList("rgp-filter-chip");
            button.AddToClassList("sm-cta");
            button.AddToClassList("sm-cta--inline");
            button.AddToClassList("sm-cta--secondary");
            if (chip.IsSelected)
            {
                button.AddToClassList("rgp-filter-chip--selected");
            }

            var key = chip.Key;
            button.clicked += () => _actions?.OnFilterSelected(key);
            _filterStrip.Add(button);
        }
    }

    private VisualElement BuildHeroCard(RosterGridHeroCardViewState hero)
    {
        if (_heroCardTemplate == null)
        {
            return BuildFallbackHeroCard(hero);
        }

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

    private VisualElement BuildFallbackHeroCard(RosterGridHeroCardViewState hero)
    {
        var card = new VisualElement { name = "HeroPortraitCard" };
        card.AddToClassList("sm-hpc");
        Add(card, "outline", "sm-hpc__outline");
        Add(card, "inset", "sm-hpc__inset");
        Add(card, "rib", "sm-hpc__rib");
        Add(card, "gem", "sm-hpc__gem");
        Add(card, "cornament-tl", "sm-hpc__cornament", "sm-hpc__cornament--tl");
        Add(card, "cornament-tr", "sm-hpc__cornament", "sm-hpc__cornament--tr");
        Add(card, "cornament-bl", "sm-hpc__cornament", "sm-hpc__cornament--bl");
        Add(card, "cornament-br", "sm-hpc__cornament", "sm-hpc__cornament--br");

        var portrait = Add(card, "portrait", "sm-hpc__portrait");
        Add(portrait, "portrait-inset", "sm-hpc__portrait-inset");
        var silhouette = Add(portrait, "silhouette", "sm-hpc__silhouette");
        // 초상화 라이브러리(Resources/_Game/Art/Characters)에서 CharacterId로 bust를 resolve.
        // 찾으면 실제 얼굴을 silhouette window에 그리고, 못 찾을 때만 이니셜 placeholder로 fallback.
        var portraitTex = string.IsNullOrWhiteSpace(hero.CharacterId)
            ? null
            : _portraitLoader?.Invoke(hero.CharacterId);
        if (portraitTex != null)
        {
            silhouette.style.backgroundImage = new StyleBackground(portraitTex);
        }
        else
        {
            Add(silhouette, string.Empty, "sm-hpc__p09-placeholder-bust");
            Add(silhouette, string.Empty, "sm-hpc__p09-placeholder-head");
            var initials = new Label(BuildFallbackInitials(hero.DisplayName)) { name = "portrait-initials" };
            initials.AddToClassList("sm-hpc__portrait-initials");
            initials.pickingMode = PickingMode.Ignore;
            silhouette.Add(initials);
        }
        Add(portrait, "portrait-glow", "sm-hpc__portrait-glow");

        var tier = Add(portrait, "tier", "sm-hpc__tier");
        Add(tier, string.Empty, "sm-hpc__tier-jewel", "sm-hpc__tier-jewel--1");
        Add(tier, string.Empty, "sm-hpc__tier-jewel", "sm-hpc__tier-jewel--2");
        Add(tier, string.Empty, "sm-hpc__tier-jewel", "sm-hpc__tier-jewel--3");

        var nameKo = new Label { name = "name-ko" };
        nameKo.AddToClassList("sm-hpc__name-ko");
        card.Add(nameKo);
        var nameEn = new Label { name = "name-en" };
        nameEn.AddToClassList("sm-hpc__name-en");
        card.Add(nameEn);

        var archetypeChip = Add(card, "archetype-chip", "sm-hpc__archetype-chip");
        Add(archetypeChip, string.Empty, "sm-hpc__archetype-chip-cap", "sm-hpc__archetype-chip-cap--tl");
        Add(archetypeChip, string.Empty, "sm-hpc__archetype-chip-cap", "sm-hpc__archetype-chip-cap--br");
        Add(archetypeChip, string.Empty, "sm-hpc__archetype-jewel");
        var archetype = new Label { name = "archetype-text" };
        archetype.AddToClassList("sm-hpc__archetype-text");
        archetypeChip.Add(archetype);

        ApplyHeroCardClasses(card, hero);
        ApplyHeroCardLabels(card, hero);
        InjectProfileOverlay(card, hero);
        card.RegisterCallback<ClickEvent>(_ => _actions?.OnHeroSelected(hero.HeroId));
        return card;
    }

    private static VisualElement Add(VisualElement parent, string name, params string[] classes)
    {
        var element = new VisualElement();
        if (!string.IsNullOrEmpty(name))
        {
            element.name = name;
        }

        foreach (var className in classes)
        {
            element.AddToClassList(className);
        }

        element.pickingMode = PickingMode.Ignore;
        parent.Add(element);
        return element;
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

    // 초상화 미연결 placeholder용 이니셜 — 이름을 공백/-/_로 나눠 앞 1~2 토큰의 첫 글자.
    // (BattleScreenView.BuildInitial / RecruitView.BuildFallbackInitials와 동일 규칙, Linq 미사용 사본)
    private static string BuildFallbackInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "?";
        }

        var parts = value.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return value.Substring(0, 1).ToUpperInvariant();
        }

        var initials = char.ToUpperInvariant(parts[0][0]).ToString();
        if (parts.Length > 1)
        {
            initials += char.ToUpperInvariant(parts[1][0]);
        }
        return initials;
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

/// <summary>
/// RosterGrid View 계약 — presenter가 의존하는 표면(bind/render)만. 콘크리트 RosterGridView는 VisualElement에
/// 묶이지만 presenter는 이 인터페이스만 알면 되어 headless 테스트에서 fake view로 BuildState/command를 구동한다.
/// (Open/Close/BindClose는 modal opener controller가 콘크리트 view에 직접 호출 — presenter 표면 아님.)
/// </summary>
public interface IRosterGridView
{
    void Bind(IRosterGridActions actions);
    void Render(RosterGridViewState state);
}
