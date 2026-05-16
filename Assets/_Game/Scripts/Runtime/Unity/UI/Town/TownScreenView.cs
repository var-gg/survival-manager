using System;
using System.Collections.Generic;
using SM.Unity.UI;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

/// <summary>
/// 잿골 hub V3 View — pindoc://decision-town-hub-v3-ashglen-face-cluster.
/// HeroFaceCard atom (sm-face-card) 코드 build — NPC strip + Welcome captain + deploy row + roster row.
/// 가변 hero list: BuildState에서 매 Render마다 cluster clear + 재구축. atom contract는
/// pindoc://analysis-hero-face-card-bark-emotion-system.
/// </summary>
public sealed class TownScreenView
{
    private readonly Label _titleEyebrowLabel;
    private readonly Label _titleLabel;
    private readonly Label _localeStatusLabel;
    private readonly Button _localeKoButton;
    private readonly Button _localeEnButton;
    private readonly Button _helpButton;
    private readonly VisualElement _helpStrip;
    private readonly Label _helpBodyLabel;
    private readonly Button _helpDismissButton;
    private readonly Button _saveButton;
    private readonly Button _loadButton;
    private readonly Button _settingsButton;
    private readonly Button _returnToStartButton;

    private readonly VisualElement _npcStrip;
    private readonly VisualElement _welcomeMount;
    private readonly Label _welcomeGreeting;
    private readonly VisualElement _deployRow;
    private readonly VisualElement _rosterRow;

    private readonly Button _rosterButton;
    private readonly Button _squadBuilderButton;
    private readonly Button _permanentAugmentButton;
    private readonly Button _theaterButton;

    private readonly Label _statusLabel;
    private readonly Button _quickBattleButton;
    private readonly Button _expeditionButton;

    private Action<string>? _onNpcClick;
    private Action<string>? _onHeroClick;
    private string _welcomeHeroId = string.Empty;

    public TownScreenView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));

        _titleEyebrowLabel = Require<Label>(root, "TitleEyebrowLabel");
        _titleLabel = Require<Label>(root, "TitleLabel");
        _localeStatusLabel = Require<Label>(root, "LocaleStatusLabel");
        _localeKoButton = Require<Button>(root, "LocaleKoButton");
        _localeEnButton = Require<Button>(root, "LocaleEnButton");
        _helpButton = Require<Button>(root, "HelpButton");
        _helpStrip = Require<VisualElement>(root, "HelpStrip");
        _helpBodyLabel = Require<Label>(root, "HelpBodyLabel");
        _helpDismissButton = Require<Button>(root, "HelpDismissButton");
        _saveButton = Require<Button>(root, "SaveButton");
        _loadButton = Require<Button>(root, "LoadButton");
        _settingsButton = Require<Button>(root, "SettingsButton");
        _returnToStartButton = Require<Button>(root, "ReturnToStartButton");

        _npcStrip = Require<VisualElement>(root, "NpcStrip");
        _welcomeMount = Require<VisualElement>(root, "WelcomeCaptainMount");
        _welcomeGreeting = Require<Label>(root, "WelcomeCaptainGreeting");
        _deployRow = Require<VisualElement>(root, "DeployRow");
        _rosterRow = Require<VisualElement>(root, "RosterRow");

        _rosterButton = Require<Button>(root, "RosterButton");
        _squadBuilderButton = Require<Button>(root, "SquadBuilderButton");
        _permanentAugmentButton = Require<Button>(root, "PermanentAugmentButton");
        _theaterButton = Require<Button>(root, "TheaterButton");

        _statusLabel = Require<Label>(root, "StatusLabel");
        _quickBattleButton = Require<Button>(root, "QuickBattleButton");
        _expeditionButton = Require<Button>(root, "ExpeditionButton");
    }

    public void Bind(TownScreenPresenter presenter)
    {
        if (presenter == null) throw new ArgumentNullException(nameof(presenter));
        _localeKoButton.clicked += presenter.SelectKorean;
        _localeEnButton.clicked += presenter.SelectEnglish;
        _helpButton.clicked += presenter.ToggleHelp;
        _helpDismissButton.clicked += presenter.DismissHelp;
        _saveButton.clicked += presenter.SaveProfile;
        _loadButton.clicked += presenter.LoadProfile;
        _returnToStartButton.clicked += presenter.ReturnToStart;
        _quickBattleButton.clicked += presenter.QuickBattle;
        _expeditionButton.clicked += presenter.OpenExpedition;
        _settingsButton.clicked += presenter.OpenSettings;
        _onNpcClick = presenter.OnNpcClick;
        _onHeroClick = presenter.OnHeroClick;
    }

    public void BindRosterOpen(Action open) => _rosterButton.clicked += open;
    public void BindSquadBuilderOpen(Action open) => _squadBuilderButton.clicked += open;
    public void BindPermanentAugmentOpen(Action open) => _permanentAugmentButton.clicked += open;
    public void BindTheaterOpen(Action open) => _theaterButton.clicked += open;

    public void Render(TownScreenViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        _titleEyebrowLabel.text = state.TitleEyebrow;
        _titleLabel.text = state.Title;
        _localeStatusLabel.text = state.LocaleStatus;
        _localeKoButton.text = state.LocaleKoLabel;
        _localeEnButton.text = state.LocaleEnLabel;
        _helpButton.text = state.HelpButtonLabel;
        _saveButton.text = state.SaveLabel;
        _loadButton.text = state.LoadLabel;
        _settingsButton.text = state.SettingsLabel;
        _returnToStartButton.text = state.ReturnToStartLabel;
        _returnToStartButton.tooltip = state.ReturnToStartTooltip;
        _returnToStartButton.SetEnabled(state.CanReturnToStart);

        _helpStrip.style.display = state.Help.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _helpBodyLabel.text = state.Help.Body;
        _helpDismissButton.text = state.Help.DismissLabel;

        // NPC strip — 4 face card lg.
        _npcStrip.Clear();
        foreach (var npc in state.NpcEntries)
        {
            var card = BuildFaceCard(npc.NpcId, npc.DisplayName, npc.EmotionKey, npc.BadgeKey, "lg", "npc");
            var npcId = npc.NpcId;
            card.clicked += () => _onNpcClick?.Invoke(npcId);
            _npcStrip.Add(card);
        }

        // Welcome captain — 큰 face card + greeting.
        _welcomeMount.Clear();
        _welcomeHeroId = state.WelcomeCaptain.HeroId;
        if (!string.IsNullOrEmpty(state.WelcomeCaptain.HeroId))
        {
            var welcome = BuildFaceCard(
                state.WelcomeCaptain.HeroId,
                state.WelcomeCaptain.DisplayName,
                state.WelcomeCaptain.EmotionKey,
                "captain",
                "lg",
                "hero-deploy");
            var heroId = state.WelcomeCaptain.HeroId;
            welcome.clicked += () => _onHeroClick?.Invoke(heroId);
            _welcomeMount.Add(welcome);
        }
        _welcomeGreeting.text = state.WelcomeCaptain.Greeting;

        // Deploy row — 4 face card md.
        _deployRow.Clear();
        foreach (var h in state.DeployHeroes)
        {
            var card = BuildFaceCard(h.HeroId, h.DisplayName, h.EmotionKey, h.BadgeKey, "md", "hero-deploy");
            var heroId = h.HeroId;
            card.clicked += () => _onHeroClick?.Invoke(heroId);
            _deployRow.Add(card);
        }

        // Roster row — face card sm (deploy 제외 8).
        _rosterRow.Clear();
        foreach (var h in state.RosterHeroes)
        {
            var card = BuildFaceCard(h.HeroId, h.DisplayName, h.EmotionKey, h.BadgeKey, "sm", "hero");
            var heroId = h.HeroId;
            card.clicked += () => _onHeroClick?.Invoke(heroId);
            _rosterRow.Add(card);
        }

        _expeditionButton.text = state.ExpeditionLabel;
        _expeditionButton.tooltip = state.ExpeditionTooltip;
        _quickBattleButton.text = state.QuickBattleLabel;
        _quickBattleButton.tooltip = state.QuickBattleTooltip;
        _quickBattleButton.SetEnabled(state.CanQuickBattle);
        _quickBattleButton.style.display = state.ShowQuickBattle ? DisplayStyle.Flex : DisplayStyle.None;

        _statusLabel.text = state.StatusText;
    }

    /// <summary>HeroFaceCard atom build — NPC + hero 공용. badge로 위계 분리, size로 변형.</summary>
    private static Button BuildFaceCard(string id, string displayName, string emotion, string badge, string size, string role)
    {
        var card = new Button { name = $"FaceCard_{id}", text = string.Empty };
        card.AddToClassList("sm-face-card");
        card.AddToClassList($"sm-face-card--{role}");
        card.AddToClassList($"sm-face-card--{size}");

        if (!string.IsNullOrEmpty(badge) && badge != "none")
        {
            var b = new Label(string.Empty);
            b.AddToClassList("sm-face-card__badge");
            b.AddToClassList($"sm-face-card__badge--{badge}");
            b.pickingMode = PickingMode.Ignore;
            card.Add(b);
        }

        var portrait = new VisualElement { name = "Portrait" };
        portrait.AddToClassList("sm-face-card__portrait");
        portrait.AddToClassList($"sm-face-card__emotion--{emotion}");
        portrait.pickingMode = PickingMode.Ignore;
        card.Add(portrait);

        var nameLabel = new Label(displayName);
        nameLabel.AddToClassList("sm-face-card__name");
        nameLabel.pickingMode = PickingMode.Ignore;
        card.Add(nameLabel);

        return card;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
