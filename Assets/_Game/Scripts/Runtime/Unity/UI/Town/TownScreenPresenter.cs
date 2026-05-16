using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta;
using SM.Meta.Model;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Town;

/// <summary>
/// 잿골 hub V3 presenter — pindoc://decision-town-hub-v3-ashglen-face-cluster.
/// 얼굴 중심 cluster + 가변 deploy + 4 NPC ambient + utility entries + Atlas CTA.
/// NPC click 매핑은 controller가 SetNpcOpener로 inject — presenter는 dict lookup만.
/// Welcome captain V1 rotation: deploy squad 첫 hero (narrative cue 부관). Phase 8에 EmotionState 통합.
/// </summary>
public sealed class TownScreenPresenter
{
    private const string HelpPrefsKey = "SM.Help.Town";

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly TownScreenView _view;
    private readonly ScreenHelpState _helpState;
    private readonly Dictionary<string, Action> _npcOpeners = new(StringComparer.Ordinal);
    private Action? _settingsOpener;
    private Action? _theaterOpener;
    private string _pendingStatus = string.Empty;

    public TownScreenPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        TownScreenView view)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _helpState = new ScreenHelpState(HelpPrefsKey);
    }

    public void Initialize() { _view.Bind(this); Refresh(); }

    public void Refresh(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) _pendingStatus = message;
        var session = _root.SessionState;
        session.EnsureBattleDeployReady();
        _view.Render(BuildState(session));
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void ToggleHelp() { _helpState.Toggle(); Refresh(); }
    public void DismissHelp() { _helpState.Dismiss(); Refresh(); }

    public void SaveProfile()
    {
        var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
        Refresh(checkpoint.Status == SessionCheckpointStatus.Success
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.profile_saved", "Profile saved.")
            : checkpoint.Message);
    }

    public void LoadProfile()
    {
        if (!_root.SessionState.CanManualProfileReload(out var reason))
        {
            _root.SessionState.RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateManualReloadBlocked(
                _root.ActiveProfileId, _root.ActiveProfileId, reason));
            Refresh(reason);
            return;
        }

        var checkpoint = _root.BindProfile(SessionCheckpointKind.ManualLoad);
        Refresh(checkpoint.Status == SessionCheckpointStatus.Success
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.profile_loaded", "Profile reloaded.")
            : checkpoint.Message);
    }

    public void ReturnToStart()
    {
        if (IsReturnToStartBlocked(_root.SessionState))
        {
            Refresh(Localize(GameLocalizationTables.UITown, "ui.town.error.return_start_locked",
                "진행 중인 런이 있어 지금은 시작 화면으로 돌아갈 수 없습니다."));
            return;
        }

        var checkpoint = _root.ReturnToSessionMenu();
        if (!checkpoint.IsSuccessful) Refresh(checkpoint.Message);
    }

    public void OpenExpedition()
    {
        if (_root.SessionState.HasPendingRewardSettlement)
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
            if (!checkpoint.IsSuccessful) { Refresh(checkpoint.Message); return; }
            _root.SceneFlow.GoToReward();
            return;
        }

        if (_root.SessionState.CanResumeExpedition)
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
            if (!checkpoint.IsSuccessful) { Refresh(checkpoint.Message); return; }
            _root.SceneFlow.GoToAtlas();
            return;
        }

        _root.SessionState.BeginNewExpedition();
        var newRunCheckpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!newRunCheckpoint.IsSuccessful) { Refresh(newRunCheckpoint.Message); return; }
        _root.SceneFlow.GoToAtlas();
    }

    public void QuickBattle()
    {
        if (!_root.SessionState.CanStartQuickBattleSmoke)
        {
            Refresh(Localize(GameLocalizationTables.UITown, "ui.town.error.quick_battle_locked",
                "Quick Battle (Smoke) is unavailable while a reward settlement or expedition run is active."));
            return;
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!checkpoint.IsSuccessful) { Refresh(checkpoint.Message); return; }

        _root.BeginTransientTownSmoke();
        _root.SessionState.PrepareTownQuickBattleSmoke();
        _root.SceneFlow.GoToBattle();
    }

    public void OpenSettings()
    {
        if (_settingsOpener != null) _settingsOpener.Invoke();
        else Refresh("Settings — V1 모델 미존재 (audit §4.1 P2, 후속 wire)");
    }

    public void OpenTheater()
    {
        if (_theaterOpener != null) _theaterOpener.Invoke();
        else Refresh("극장 (Theater) — story replay 후속 wire");
    }

    /// <summary>NPC 거점 click 라우팅 — controller가 SetNpcOpener로 inject한 modal opener invoke.</summary>
    public void OnNpcClick(string npcId)
    {
        if (_npcOpeners.TryGetValue(npcId, out var open)) open.Invoke();
        else Refresh($"NPC 거점 미연결: {npcId}");
    }

    /// <summary>Hero face card click — Phase 3 CharacterSheet modal 후속.</summary>
    public void OnHeroClick(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        Refresh($"동료 시트 — {heroId} (Phase 3 CharacterSheet 후속 wire)");
    }

    public void SetNpcOpener(string npcId, Action open) => _npcOpeners[npcId] = open;
    public void SetSettingsOpener(Action open) => _settingsOpener = open;
    public void SetTheaterOpener(Action open) => _theaterOpener = open;

    private TownScreenViewState BuildState(GameSessionState session)
    {
        var showDebugActions = Application.isEditor || Debug.isDebugBuild;
        var statusText = _pendingStatus;
        _pendingStatus = string.Empty;

        var deployHeroIds = session.ExpeditionSquadHeroIds.ToList();
        var welcomeHeroId = deployHeroIds.FirstOrDefault()
            ?? session.Profile.Heroes.FirstOrDefault()?.HeroId
            ?? string.Empty;
        var welcomeHero = session.Profile.Heroes
            .FirstOrDefault(h => string.Equals(h.HeroId, welcomeHeroId, StringComparison.Ordinal));
        var welcomeName = ResolveHeroDisplayName(welcomeHeroId, welcomeHero?.Name);
        var welcomeGreeting = string.IsNullOrEmpty(welcomeHeroId)
            ? Localize(GameLocalizationTables.UITown, "ui.town.welcome.empty", "잿골은 늘 그대로일세. 한 사람도 함께가 아닌가.")
            : Localize(GameLocalizationTables.UITown, "ui.town.welcome.greeting_default", "잿골은 늘 그대로일세. 잠시 숨을 돌리시지요.");

        // NPC 4 거점 — 정적 (Phase 8에 EmotionState/BarkBus 통합)
        var npcEntries = new List<TownNpcCardViewState>
        {
            new("dalmok",  "달목", "amused",    "tavern"),
            new("soemae",  "쇠매", "neutral",   "forge"),
            new("galma",   "갈마", "concerned", "clinic"),
            new("solgil",  "솔길", "neutral",   "records"),
        };

        // Deploy row — ExpeditionSquadHeroIds (deploy 4 cap)
        var deployCards = new List<TownHeroCardViewState>();
        foreach (var hid in deployHeroIds)
        {
            var hero = session.Profile.Heroes.FirstOrDefault(h => string.Equals(h.HeroId, hid, StringComparison.Ordinal));
            var displayName = ResolveHeroDisplayName(hid, hero?.Name);
            var badge = string.Equals(hid, welcomeHeroId, StringComparison.Ordinal) ? "captain" : "none";
            deployCards.Add(new TownHeroCardViewState(hid, displayName, "neutral", badge, IsDeploy: true));
        }

        // Roster row — Profile.Heroes 중 deploy 외
        var deployIdSet = new HashSet<string>(deployHeroIds, StringComparer.Ordinal);
        var rosterCards = session.Profile.Heroes
            .Where(h => !deployIdSet.Contains(h.HeroId))
            .Select(h => new TownHeroCardViewState(
                HeroId: h.HeroId,
                DisplayName: ResolveHeroDisplayName(h.HeroId, h.Name),
                EmotionKey: "neutral",
                BadgeKey: "none",
                IsDeploy: false))
            .ToList();

        return new TownScreenViewState(
            TitleEyebrow: Localize(GameLocalizationTables.UITown, "ui.town.eyebrow", "ASHGLEN — 잿골"),
            Title: Localize(GameLocalizationTables.UITown, "ui.town.title", "변방 잿골 마을"),
            LocaleStatus: BuildLocaleStatus(),
            LocaleKoLabel: GetLocaleButtonLabel("ko", "한국어"),
            LocaleEnLabel: GetLocaleButtonLabel("en", "English"),
            HelpButtonLabel: Localize(GameLocalizationTables.UICommon, "ui.common.help", "?"),
            Help: BuildHelpState(),
            SaveLabel: Localize(GameLocalizationTables.UICommon, "ui.common.save", "Save"),
            LoadLabel: Localize(GameLocalizationTables.UICommon, "ui.common.load", "Load"),
            SettingsLabel: Localize(GameLocalizationTables.UICommon, "ui.common.settings", "⚙"),
            ReturnToStartLabel: Localize(GameLocalizationTables.UICommon, "ui.common.return_start", "Return to Start"),
            ReturnToStartTooltip: BuildReturnToStartTooltip(session),
            CanReturnToStart: !IsReturnToStartBlocked(session),
            NpcEntries: npcEntries,
            WelcomeCaptain: new TownWelcomeViewState(
                HeroId: welcomeHeroId,
                DisplayName: welcomeName,
                EmotionKey: "confident",
                Greeting: welcomeGreeting),
            DeployHeroes: deployCards,
            RosterHeroes: rosterCards,
            ExpeditionLabel: BuildExpeditionLabel(session),
            ExpeditionTooltip: BuildExpeditionTooltip(session),
            QuickBattleLabel: Localize(GameLocalizationTables.UITown, "ui.town.action.quick_battle_smoke", "Quick Battle (Smoke)"),
            QuickBattleTooltip: Localize(GameLocalizationTables.UITown, "ui.town.tooltip.quick_battle_smoke",
                "Open an integration smoke battle using the current Town build, then return through Reward or direct Town restore."),
            CanQuickBattle: showDebugActions && session.CanStartQuickBattleSmoke,
            ShowQuickBattle: showDebugActions,
            StatusText: statusText);
    }

    private string ResolveHeroDisplayName(string heroId, string? heroName)
    {
        if (string.IsNullOrEmpty(heroId)) return "—";
        if (!string.IsNullOrEmpty(heroName)) return heroName!;
        return _contentText.GetCharacterName(heroId, heroId);
    }

    private string BuildLocaleStatus()
    {
        var locale = _localization.CurrentLocale;
        if (locale == null) return "-";
        return $"{Localize(GameLocalizationTables.UICommon, "ui.common.current_language", "Current")}: {_localization.GetLocaleButtonLabel(locale)}";
    }

    private string GetLocaleButtonLabel(string localeCode, string fallback)
    {
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        return locale != null ? _localization.GetLocaleButtonLabel(locale) : fallback;
    }

    private HelpStripViewState BuildHelpState()
    {
        return new HelpStripViewState(
            _helpState.IsVisible,
            Localize(GameLocalizationTables.UITown, "ui.town.help.body",
                "달목·쇠매·갈마·솔길 — 4 거점 NPC를 통해 모집·장비·수련·창고로. 우측 명부에서 동료를, 원정 버튼으로 출발."),
            Localize(GameLocalizationTables.UICommon, "ui.common.hide", "Hide"));
    }

    private string BuildExpeditionLabel(GameSessionState session)
    {
        if (session.HasPendingRewardSettlement)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.action.open", "Open Reward");
        }

        return session.CanResumeExpedition
            ? Localize(GameLocalizationTables.UITown, "ui.town.action.resume_expedition", "Resume Expedition")
            : Localize(GameLocalizationTables.UITown, "ui.town.action.start_expedition", "원정으로");
    }

    private string BuildExpeditionTooltip(GameSessionState session)
    {
        if (session.HasPendingRewardSettlement)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_reward",
                "Open Reward to settle the previous node before continuing.");
        }

        return session.CanResumeExpedition
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_resume",
                "Resume the authored expedition from the currently selected route.")
            : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_start",
                "Begin the authored expedition loop with the current preparation.");
    }

    private string BuildReturnToStartTooltip(GameSessionState session)
    {
        return IsReturnToStartBlocked(session)
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start_locked",
                "Blocked while an authored run or smoke battle is still active.")
            : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start",
                "Leave the local run shell and go back to Boot.");
    }

    private static bool IsReturnToStartBlocked(GameSessionState session)
    {
        return session.HasActiveExpeditionRun || session.IsQuickBattleSmokeActive;
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
