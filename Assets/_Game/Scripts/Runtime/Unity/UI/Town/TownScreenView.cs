using System;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

/// <summary>
/// Town hub V1 view — 12 hero card grid (코드 직접 build) + header + bottom toolbar + floating Quick Battle.
/// audit §2.1 RosterGrid hub. HeroPortraitCard atom은 Resources 외부라 production runtime 의존성을 만들지 않으려고
/// 직접 VisualElement code로 카드를 build (atom 보존, dev preview에서만 atom 재사용).
/// </summary>
public sealed class TownScreenView
{
    private readonly Label _eyebrowLabel;
    private readonly Label _titleLabel;
    private readonly Label _heroCountLabel;
    private readonly Label _localeStatusLabel;
    private readonly Button _localeKoButton;
    private readonly Button _localeEnButton;
    private readonly Button _helpButton;
    private readonly VisualElement _helpStrip;
    private readonly Label _helpBodyLabel;
    private readonly Button _helpDismissButton;
    private readonly VisualElement _gridContainer;
    private readonly Button _squadBuilderButton;
    private readonly Button _expeditionButton;
    private readonly Button _saveButton;
    private readonly Button _loadButton;
    private readonly Button _returnToStartButton;
    private readonly Button _quickBattleButton;
    private readonly Label _statusLabel;

    public TownScreenView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _eyebrowLabel = Require<Label>(root, "TitleEyebrowLabel");
        _titleLabel = Require<Label>(root, "TitleLabel");
        _heroCountLabel = Require<Label>(root, "HeroCount");
        _localeStatusLabel = Require<Label>(root, "LocaleStatusLabel");
        _localeKoButton = Require<Button>(root, "LocaleKoButton");
        _localeEnButton = Require<Button>(root, "LocaleEnButton");
        _helpButton = Require<Button>(root, "HelpButton");
        _helpStrip = Require<VisualElement>(root, "HelpStrip");
        _helpBodyLabel = Require<Label>(root, "HelpBodyLabel");
        _helpDismissButton = Require<Button>(root, "HelpDismissButton");
        _gridContainer = Require<VisualElement>(root, "GridContainer");
        _squadBuilderButton = Require<Button>(root, "SquadBuilderButton");
        _expeditionButton = Require<Button>(root, "ExpeditionButton");
        _saveButton = Require<Button>(root, "SaveButton");
        _loadButton = Require<Button>(root, "LoadButton");
        _returnToStartButton = Require<Button>(root, "ReturnToStartButton");
        _quickBattleButton = Require<Button>(root, "QuickBattleButton");
        _statusLabel = Require<Label>(root, "StatusLabel");
    }

    public void Bind(TownScreenPresenter presenter)
    {
        _localeKoButton.clicked += presenter.SelectKorean;
        _localeEnButton.clicked += presenter.SelectEnglish;
        _helpButton.clicked += presenter.ToggleHelp;
        _helpDismissButton.clicked += presenter.DismissHelp;
        _saveButton.clicked += presenter.SaveProfile;
        _loadButton.clicked += presenter.LoadProfile;
        _returnToStartButton.clicked += presenter.ReturnToStart;
        _expeditionButton.clicked += presenter.OpenExpedition;
        _quickBattleButton.clicked += presenter.QuickBattle;
    }

    public void BindSquadBuilderOpen(Action open)
    {
        _squadBuilderButton.clicked += open;
    }

    public void Render(TownScreenViewState state)
    {
        _eyebrowLabel.text = state.TitleEyebrow;
        _titleLabel.text = state.Title;
        _heroCountLabel.text = $"{state.Heroes.Count} / {state.RosterCap}";
        _localeStatusLabel.text = state.LocaleStatus;
        _localeKoButton.text = state.LocaleKoLabel;
        _localeEnButton.text = state.LocaleEnLabel;
        _helpButton.text = state.HelpButtonLabel;

        _helpStrip.style.display = state.Help.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _helpBodyLabel.text = state.Help.Body;
        _helpDismissButton.text = state.Help.DismissLabel;

        _expeditionButton.text = state.ExpeditionLabel;
        _expeditionButton.tooltip = state.ExpeditionTooltip;
        _saveButton.text = state.SaveLabel;
        _loadButton.text = state.LoadLabel;
        _returnToStartButton.text = state.ReturnToStartLabel;
        _returnToStartButton.tooltip = state.ReturnToStartTooltip;
        _returnToStartButton.SetEnabled(state.CanReturnToStart);

        _quickBattleButton.style.display = state.ShowQuickBattle ? DisplayStyle.Flex : DisplayStyle.None;
        _quickBattleButton.text = state.QuickBattleLabel;
        _quickBattleButton.tooltip = state.QuickBattleTooltip;
        _quickBattleButton.SetEnabled(state.CanQuickBattle);

        _statusLabel.text = state.StatusText;

        _gridContainer.Clear();
        foreach (var hero in state.Heroes)
        {
            _gridContainer.Add(BuildHeroCard(hero));
        }
    }

    private static VisualElement BuildHeroCard(TownHeroCardViewState hero)
    {
        var card = new VisualElement { name = $"HeroCard_{hero.HeroId}" };
        card.AddToClassList("town-hub-card");
        // inline backup — USS class가 stylesheet 미로딩 환경에서 짤리는 것 방지.
        card.style.width = 138;
        card.style.height = 184;
        card.style.flexShrink = 0;
        // Foundation atom: rarity jewel border (common/rare/epic) + family tint rib.
        card.AddToClassList("sm-jewel-duotone");
        if (!string.IsNullOrEmpty(hero.RarityKey))
        {
            card.AddToClassList($"sm-jewel-duotone--{hero.RarityKey}");
        }

        // family rib — 카드 상단 4px 띠.
        var rib = new VisualElement { name = "FamilyRib" };
        rib.AddToClassList("town-hub-card__rib");
        rib.AddToClassList("sm-family-tint-rib");
        if (!string.IsNullOrEmpty(hero.FamilyKey))
        {
            rib.AddToClassList($"sm-family-tint-rib--{hero.FamilyKey}");
        }
        rib.pickingMode = PickingMode.Ignore;
        card.Add(rib);

        var equipRow = new VisualElement();
        equipRow.AddToClassList("town-hub-card__equip-row");
        for (var i = 0; i < 3; i++)
        {
            var dot = new VisualElement();
            dot.AddToClassList("town-hub-card__equip-dot");
            if (i < hero.EquipSlots) dot.AddToClassList("town-hub-card__equip-dot--filled");
            equipRow.Add(dot);
        }
        card.Add(equipRow);

        var portrait = new VisualElement();
        portrait.AddToClassList("town-hub-card__portrait");
        card.Add(portrait);

        var nameLabel = new Label(hero.DisplayName);
        nameLabel.AddToClassList("town-hub-card__name");
        card.Add(nameLabel);

        var archetype = new Label(hero.ArchetypeLabel);
        archetype.AddToClassList("town-hub-card__archetype");
        card.Add(archetype);

        var levelRow = new VisualElement();
        levelRow.AddToClassList("town-hub-card__level-row");
        var levelLabel = new Label($"Lv {hero.Level}");
        levelLabel.AddToClassList("town-hub-card__level-label");
        levelRow.Add(levelLabel);
        var xpBar = new VisualElement();
        xpBar.AddToClassList("town-hub-card__xp-bar");
        var xpFill = new VisualElement();
        xpFill.AddToClassList("town-hub-card__xp-fill");
        xpFill.style.width = new StyleLength(new Length(hero.XpPct, LengthUnit.Percent));
        xpBar.Add(xpFill);
        levelRow.Add(xpBar);
        card.Add(levelRow);

        return card;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
