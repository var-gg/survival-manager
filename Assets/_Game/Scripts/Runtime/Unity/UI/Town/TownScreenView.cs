using System;
using System.Collections.Generic;
using SM.Unity.UI;
using SM.Unity.UI.Bark;
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

    private readonly Label _ledgerTitleLabel;
    private readonly Button _rosterButton;
    private readonly Label _rosterEntryLabel;
    private readonly Button _compendiumButton;
    private readonly Label _compendiumEntryLabel;
    private readonly Button _tacticalSetupButton;
    private readonly Label _tacticalSetupEntryLabel;
    private readonly Button _tacticalWorkshopButton;
    private readonly Label _tacticalWorkshopEntryLabel;
    private readonly Button _permanentAugmentButton;
    private readonly Label _permanentAugmentEntryLabel;
    private readonly Button _theaterButton;
    private readonly Label _theaterEntryLabel;
    private readonly Label _serviceSelectedHeroLabel;
    private readonly Label _serviceWalletLabel;
    private readonly Label _serviceInventoryLabel;
    private readonly Label _serviceRosterPressureLabel;
    private readonly Label _serviceAvailabilityLabel;
    private readonly Label _clusterEyebrowLabel;

    private readonly Label _statusLabel;
    private readonly Button _quickBattleButton;
    private readonly Button _expeditionButton;

    private Action<string>? _onNpcClick;
    private Action<string>? _onHeroClick;
    private string _welcomeHeroId = string.Empty;
    // NPC id(달목/쇠매/갈마/솔길) → 흉상 Texture. null이면 글리프 placeholder 유지.
    private readonly Func<string, UnityEngine.Texture2D?>? _portraitLoader;

    // wave-36-barkbus: bark popup attach 대상 face card lookup table.
    // 매 Render마다 재구축 — face card는 list mutation 시 재생성되므로 stale ref 방지.
    private readonly Dictionary<string, VisualElement> _faceCardsBySourceId = new(StringComparer.Ordinal);
    private IDisposable? _barkSubscription;

    public TownScreenView(VisualElement root, Func<string, UnityEngine.Texture2D?>? portraitLoader = null)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _portraitLoader = portraitLoader;

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

        _ledgerTitleLabel = Require<Label>(root, "LedgerTitleLabel");
        _rosterButton = Require<Button>(root, "RosterButton");
        _rosterEntryLabel = Require<Label>(root, "RosterEntryLabel");
        _compendiumButton = Require<Button>(root, "CompendiumButton");
        _compendiumEntryLabel = Require<Label>(root, "CompendiumEntryLabel");
        _tacticalSetupButton = Require<Button>(root, "TacticalSetupButton");
        _tacticalSetupEntryLabel = Require<Label>(root, "TacticalSetupEntryLabel");
        _tacticalWorkshopButton = Require<Button>(root, "TacticalWorkshopButton");
        _tacticalWorkshopEntryLabel = Require<Label>(root, "TacticalWorkshopEntryLabel");
        _permanentAugmentButton = Require<Button>(root, "PermanentAugmentButton");
        _permanentAugmentEntryLabel = Require<Label>(root, "PermanentAugmentEntryLabel");
        _theaterButton = Require<Button>(root, "TheaterButton");
        _theaterEntryLabel = Require<Label>(root, "TheaterEntryLabel");
        _serviceSelectedHeroLabel = Require<Label>(root, "ServiceSelectedHeroLabel");
        _serviceWalletLabel = Require<Label>(root, "ServiceWalletLabel");
        _serviceInventoryLabel = Require<Label>(root, "ServiceInventoryLabel");
        _serviceRosterPressureLabel = Require<Label>(root, "ServiceRosterPressureLabel");
        _serviceAvailabilityLabel = Require<Label>(root, "ServiceAvailabilityLabel");
        _clusterEyebrowLabel = Require<Label>(root, "ClusterEyebrowLabel");

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
    public void BindCompendiumOpen(Action open) => _compendiumButton.clicked += open;
    public void BindTacticalSetupOpen(Action open) => _tacticalSetupButton.clicked += open;
    public void BindTacticalWorkshopOpen(Action open) => _tacticalWorkshopButton.clicked += open;
    public void BindPermanentAugmentOpen(Action open) => _permanentAugmentButton.clicked += open;
    public void BindTheaterOpen(Action open) => _theaterButton.clicked += open;

    // wave-36-barkbus: bus 주입. Render 이후에도 face card lookup이 stale하지 않도록
    // popup attach는 lookup miss 시 silently 무시 (race 안전).
    public void BindBarkBus(BarkBus bus)
    {
        _barkSubscription?.Dispose();
        _barkSubscription = bus?.Subscribe(OnBarkReceived);
    }

    public void UnbindBarkBus()
    {
        _barkSubscription?.Dispose();
        _barkSubscription = null;
    }

    private void OnBarkReceived(BarkEvent evt)
    {
        if (evt == null) return;
        if (!_faceCardsBySourceId.TryGetValue(evt.SourceId, out var faceCard) || faceCard == null) return;
        BarkPopupView.Attach(faceCard, evt);
    }

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
        _settingsButton.SetEnabled(state.ShowSettings);
        _settingsButton.style.display = state.ShowSettings ? DisplayStyle.Flex : DisplayStyle.None;
        _returnToStartButton.text = state.ReturnToStartLabel;
        _returnToStartButton.tooltip = state.ReturnToStartTooltip;
        _returnToStartButton.SetEnabled(state.CanReturnToStart);

        _helpStrip.style.display = state.Help.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _helpBodyLabel.text = state.Help.Body;
        _helpDismissButton.text = state.Help.DismissLabel;

        _ledgerTitleLabel.text = state.LedgerTitle;
        _rosterEntryLabel.text = state.RosterEntryLabel;
        _compendiumEntryLabel.text = state.CompendiumEntryLabel;
        _tacticalSetupEntryLabel.text = state.TacticalSetupEntryLabel;
        _tacticalWorkshopEntryLabel.text = state.TacticalWorkshopEntryLabel;
        _permanentAugmentEntryLabel.text = state.PermanentAugmentEntryLabel;
        _theaterEntryLabel.text = state.TheaterEntryLabel;
        _theaterButton.SetEnabled(state.ShowTheater);
        _theaterButton.style.display = state.ShowTheater ? DisplayStyle.Flex : DisplayStyle.None;
        _serviceSelectedHeroLabel.text = state.ServiceDecision.SelectedHeroLabel;
        _serviceWalletLabel.text = state.ServiceDecision.WalletLabel;
        _serviceInventoryLabel.text = state.ServiceDecision.InventoryLabel;
        _serviceRosterPressureLabel.text = state.ServiceDecision.RosterPressureLabel;
        _serviceAvailabilityLabel.text = state.ServiceDecision.ModalAvailabilityLabel;
        _clusterEyebrowLabel.text = state.ClusterEyebrow;

        // wave-36-barkbus: 매 Render마다 face card lookup 재구축. 이전 ref는 mutation으로 무효.
        _faceCardsBySourceId.Clear();

        // NPC strip — 4 face card lg.
        _npcStrip.Clear();
        foreach (var npc in state.NpcEntries)
        {
            var npcPortrait = _portraitLoader?.Invoke(npc.NpcId);
            var card = BuildFaceCard(npc.NpcId, npc.DisplayName, npc.EmotionKey, npc.BadgeKey, "lg", "npc", npc.RoleLabel, npcPortrait);
            var npcId = npc.NpcId;
            card.clicked += () => _onNpcClick?.Invoke(npcId);
            _npcStrip.Add(card);
            _faceCardsBySourceId[npcId] = card;
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
                "hero-deploy",
                portraitTex: _portraitLoader?.Invoke(state.WelcomeCaptain.CharacterId));
            var heroId = state.WelcomeCaptain.HeroId;
            welcome.clicked += () => _onHeroClick?.Invoke(heroId);
            _welcomeMount.Add(welcome);
            _faceCardsBySourceId[heroId] = welcome;
        }
        _welcomeGreeting.text = state.WelcomeCaptain.Greeting;

        // Deploy row — 4 face card md.
        _deployRow.Clear();
        foreach (var h in state.DeployHeroes)
        {
            var card = BuildFaceCard(h.HeroId, h.DisplayName, h.EmotionKey, h.BadgeKey, "md", "hero-deploy",
                portraitTex: _portraitLoader?.Invoke(h.CharacterId));
            var heroId = h.HeroId;
            card.clicked += () => _onHeroClick?.Invoke(heroId);
            _deployRow.Add(card);
            // welcome captain은 deploy first hero라 dict 이미 있을 수 있음 — 덮어쓰기는 안전.
            _faceCardsBySourceId[heroId] = card;
        }

        // Roster row — face card sm (deploy 제외 8).
        _rosterRow.Clear();
        foreach (var h in state.RosterHeroes)
        {
            var card = BuildFaceCard(h.HeroId, h.DisplayName, h.EmotionKey, h.BadgeKey, "sm", "hero",
                portraitTex: _portraitLoader?.Invoke(h.CharacterId));
            var heroId = h.HeroId;
            card.clicked += () => _onHeroClick?.Invoke(heroId);
            _rosterRow.Add(card);
            _faceCardsBySourceId[heroId] = card;
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
    private static Button BuildFaceCard(string id, string displayName, string emotion, string badge, string size, string role, string caption = "", UnityEngine.Texture2D? portraitTex = null)
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
        // NPC role 4명별 portrait BG tint — 실제 얼굴이 없을 때 visually distinct하게(glyph fallback 배경).
        if (!string.IsNullOrEmpty(badge) && badge != "none")
        {
            portrait.AddToClassList($"sm-face-card__portrait--{badge}");
        }
        portrait.pickingMode = PickingMode.Ignore;
        if (portraitTex != null)
        {
            // 라이브러리에 실제 흉상이 있으면 얼굴을 채워 crop — glyph placeholder 대체.
            portrait.style.backgroundImage = new StyleBackground(portraitTex);
            portrait.style.unityBackgroundScaleMode = UnityEngine.ScaleMode.ScaleAndCrop;
        }
        else
        {
            var portraitGlyph = new Label(ResolvePortraitGlyph(badge, role, id));
            portraitGlyph.AddToClassList("sm-face-card__portrait-glyph");
            portraitGlyph.pickingMode = PickingMode.Ignore;
            portrait.Add(portraitGlyph);
        }
        card.Add(portrait);

        var nameLabel = new Label(displayName);
        nameLabel.AddToClassList("sm-face-card__name");
        nameLabel.pickingMode = PickingMode.Ignore;
        card.Add(nameLabel);

        // NPC 거점은 이름 아래 기능 caption(모집/장비/수련/창고)을 노출 — hero 카드는 caption 없이 통과.
        if (!string.IsNullOrEmpty(caption))
        {
            var captionLabel = new Label(caption);
            captionLabel.AddToClassList("sm-face-card__caption");
            captionLabel.pickingMode = PickingMode.Ignore;
            card.Add(captionLabel);
        }

        var pipRow = new VisualElement();
        pipRow.AddToClassList("sm-face-card__pip-row");
        for (var i = 0; i < 5; i++)
        {
            var pip = new VisualElement();
            pip.AddToClassList("sm-face-card__pip");
            pip.EnableInClassList("sm-face-card__pip--on", i < ResolvePipCount(badge, role, id));
            pipRow.Add(pip);
        }
        pipRow.pickingMode = PickingMode.Ignore;
        card.Add(pipRow);

        return card;
    }

    private static string ResolvePortraitGlyph(string badge, string role, string id)
    {
        return badge switch
        {
            "tavern" => "♟",
            "forge" => "⚒",
            "clinic" => "✚",
            "records" => "✧",
            "captain" => "✦",
            _ when role.Contains("hero", StringComparison.Ordinal) => "◆",
            _ when id.Contains("squad", StringComparison.OrdinalIgnoreCase) => "⚔",
            _ => "◇"
        };
    }

    private static int ResolvePipCount(string badge, string role, string id)
    {
        if (badge == "captain") return 5;
        if (role.Contains("hero-deploy", StringComparison.Ordinal)) return 4;
        if (role.Contains("hero", StringComparison.Ordinal)) return 3;
        return (id.GetHashCode() & int.MaxValue) % 3 + 2;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
