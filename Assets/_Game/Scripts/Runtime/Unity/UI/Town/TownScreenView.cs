using System;
using SM.Unity.UI;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

/// <summary>
/// 잿골 hub V2 View — pindoc://v1-scene-screen-routing-ashglen-hub-analysis 정합.
/// 4 NPC entry (좌) + Welcome hero center + utility entries (우) + utility top bar + Atlas CTA bar.
/// 옛 RosterGrid+toolbar layout 폐기.
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
    private readonly Button _returnToStartButton;

    private readonly Button _npcEntryDalmok;
    private readonly Label _npcHintDalmok;
    private readonly Button _npcEntrySoemae;
    private readonly Label _npcHintSoemae;
    private readonly Button _npcEntryGalma;
    private readonly Label _npcHintGalma;
    private readonly Button _npcEntrySolgil;
    private readonly Label _npcHintSolgil;

    private readonly Button _welcomeHeroEntry;
    private readonly Label _welcomeHeroEyebrow;
    private readonly Label _welcomeHeroName;
    private readonly Label _welcomeHeroGreeting;
    private readonly Label _welcomeHeroHint;

    private readonly Button _rosterButton;
    private readonly Label _rosterCountLabel;
    private readonly Button _permanentAugmentButton;
    private readonly Button _squadBuilderButton;

    private readonly Label _statusLabel;
    private readonly Button _quickBattleButton;
    private readonly Button _expeditionButton;

    private string _currentWelcomeHeroId = string.Empty;

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
        _returnToStartButton = Require<Button>(root, "ReturnToStartButton");

        _npcEntryDalmok = Require<Button>(root, "NpcEntry_Dalmok");
        _npcHintDalmok = Require<Label>(root, "NpcHint_Dalmok");
        _npcEntrySoemae = Require<Button>(root, "NpcEntry_Soemae");
        _npcHintSoemae = Require<Label>(root, "NpcHint_Soemae");
        _npcEntryGalma = Require<Button>(root, "NpcEntry_Galma");
        _npcHintGalma = Require<Label>(root, "NpcHint_Galma");
        _npcEntrySolgil = Require<Button>(root, "NpcEntry_Solgil");
        _npcHintSolgil = Require<Label>(root, "NpcHint_Solgil");

        _welcomeHeroEntry = Require<Button>(root, "WelcomeHeroEntry");
        _welcomeHeroEyebrow = Require<Label>(root, "WelcomeHeroEyebrow");
        _welcomeHeroName = Require<Label>(root, "WelcomeHeroName");
        _welcomeHeroGreeting = Require<Label>(root, "WelcomeHeroGreeting");
        _welcomeHeroHint = Require<Label>(root, "WelcomeHeroHint");

        _rosterButton = Require<Button>(root, "RosterButton");
        _rosterCountLabel = Require<Label>(root, "RosterCountLabel");
        _permanentAugmentButton = Require<Button>(root, "PermanentAugmentButton");
        _squadBuilderButton = Require<Button>(root, "SquadBuilderButton");

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
        _welcomeHeroEntry.clicked += () => presenter.OpenWelcomeHeroSheet(_currentWelcomeHeroId);
    }

    public void BindNpcDalmokOpen(Action open) => _npcEntryDalmok.clicked += open;
    public void BindNpcSoemaeOpen(Action open) => _npcEntrySoemae.clicked += open;
    public void BindNpcGalmaOpen(Action open) => _npcEntryGalma.clicked += open;
    public void BindNpcSolgilOpen(Action open) => _npcEntrySolgil.clicked += open;
    public void BindRosterOpen(Action open) => _rosterButton.clicked += open;
    public void BindPermanentAugmentOpen(Action open) => _permanentAugmentButton.clicked += open;
    public void BindSquadBuilderOpen(Action open) => _squadBuilderButton.clicked += open;

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
        _returnToStartButton.text = state.ReturnToStartLabel;
        _returnToStartButton.tooltip = state.ReturnToStartTooltip;
        _returnToStartButton.SetEnabled(state.CanReturnToStart);

        _helpStrip.style.display = state.Help.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _helpBodyLabel.text = state.Help.Body;
        _helpDismissButton.text = state.Help.DismissLabel;

        _npcHintDalmok.text = state.DalmokEntry.HintText;
        _npcHintSoemae.text = state.SoemaeEntry.HintText;
        _npcHintGalma.text = state.GalmaEntry.HintText;
        _npcHintSolgil.text = state.SolgilEntry.HintText;

        _currentWelcomeHeroId = state.WelcomeHero.HeroId;
        _welcomeHeroEyebrow.text = state.WelcomeHero.EyebrowText;
        _welcomeHeroName.text = state.WelcomeHero.HeroName;
        _welcomeHeroGreeting.text = state.WelcomeHero.Greeting;
        _welcomeHeroHint.text = state.WelcomeHero.HintText;
        _welcomeHeroEntry.SetEnabled(!string.IsNullOrEmpty(state.WelcomeHero.HeroId));

        _rosterCountLabel.text = state.RosterCountText;

        _expeditionButton.text = state.ExpeditionLabel;
        _expeditionButton.tooltip = state.ExpeditionTooltip;
        _quickBattleButton.text = state.QuickBattleLabel;
        _quickBattleButton.tooltip = state.QuickBattleTooltip;
        _quickBattleButton.SetEnabled(state.CanQuickBattle);
        _quickBattleButton.style.display = state.ShowQuickBattle ? DisplayStyle.Flex : DisplayStyle.None;

        _statusLabel.text = state.StatusText;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
