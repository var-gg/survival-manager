using System;
using System.Linq;
using SM.Meta;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Town;

/// <summary>
/// 잿골 hub V2 presenter — pindoc://v1-scene-screen-routing-ashglen-hub-analysis 정합.
/// 명일방주 식 ambient hub: 4 NPC menu (좌) + Welcome hero center + utility (우) + Atlas CTA (하).
/// 옛 RosterGrid+toolbar 폐기. Recruit/Equipment/Passive/Inventory 진입은 NPC entry로 위임,
/// PermAugment/SquadBuilder/Roster는 utility entry, Quick Battle/Save/Load/Return은 top utility.
///
/// Welcome hero rotation: V1 단순 fallback — deploy squad 첫 번째 (또는 roster 첫 번째). 후속에
/// chapter 단계별 narrative cue 적용.
/// </summary>
public sealed class TownScreenPresenter
{
    private const string HelpPrefsKey = "SM.Help.Town";

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly TownScreenView _view;
    private readonly ScreenHelpState _helpState;
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

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

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
            Refresh(Localize(
                GameLocalizationTables.UITown,
                "ui.town.error.return_start_locked",
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
            Refresh(Localize(
                GameLocalizationTables.UITown,
                "ui.town.error.quick_battle_locked",
                "Quick Battle (Smoke) is unavailable while a reward settlement or expedition run is active."));
            return;
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!checkpoint.IsSuccessful) { Refresh(checkpoint.Message); return; }

        _root.BeginTransientTownSmoke();
        _root.SessionState.PrepareTownQuickBattleSmoke();
        _root.SceneFlow.GoToBattle();
    }

    /// <summary>Welcome hero standee click — Phase 3 CharacterSheet modal 후속. 일단 status placeholder.</summary>
    public void OpenWelcomeHeroSheet(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        Refresh($"동료 시트 — {heroId} (Phase 3 후속 wire)");
    }

    private TownScreenViewState BuildState(GameSessionState session)
    {
        var showDebugActions = Application.isEditor || Debug.isDebugBuild;

        // Welcome hero V1: deploy squad 첫 번째, fallback Profile.Heroes 첫 번째.
        var welcomeHeroId = session.ExpeditionSquadHeroIds.FirstOrDefault()
            ?? session.Profile.Heroes.FirstOrDefault()?.HeroId
            ?? string.Empty;
        var welcomeHero = session.Profile.Heroes
            .FirstOrDefault(h => string.Equals(h.HeroId, welcomeHeroId, StringComparison.Ordinal));
        var welcomeName = string.IsNullOrEmpty(welcomeHeroId)
            ? Localize(GameLocalizationTables.UITown, "ui.town.welcome.empty_name", "—")
            : (!string.IsNullOrEmpty(welcomeHero?.Name) ? welcomeHero!.Name : _contentText.GetCharacterName(welcomeHeroId, welcomeHeroId));

        var rosterCount = session.Profile.Heroes.Count;
        var rosterCap = MetaBalanceDefaults.TownRosterCap;
        var statusText = _pendingStatus;
        _pendingStatus = string.Empty;

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
            ReturnToStartLabel: Localize(GameLocalizationTables.UICommon, "ui.common.return_start", "Return to Start"),
            ReturnToStartTooltip: BuildReturnToStartTooltip(session),
            CanReturnToStart: !IsReturnToStartBlocked(session),
            DalmokEntry: new TownNpcEntryViewState(Localize(GameLocalizationTables.UITown, "ui.town.npc.dalmok.hint", "새 동료 명단을 정리해두었네.")),
            SoemaeEntry: new TownNpcEntryViewState(Localize(GameLocalizationTables.UITown, "ui.town.npc.soemae.hint", "망치 소리는 거짓말 안 해.")),
            GalmaEntry: new TownNpcEntryViewState(Localize(GameLocalizationTables.UITown, "ui.town.npc.galma.hint", "다친 곳은 다음에도 다친다.")),
            SolgilEntry: new TownNpcEntryViewState(Localize(GameLocalizationTables.UITown, "ui.town.npc.solgil.hint", "원정대가 못 본 것을 적어둔다.")),
            WelcomeHero: new TownWelcomeHeroViewState(
                HeroId: welcomeHeroId,
                EyebrowText: Localize(GameLocalizationTables.UITown, "ui.town.welcome.eyebrow", "오늘의 부관"),
                HeroName: welcomeName,
                Greeting: Localize(GameLocalizationTables.UITown, "ui.town.welcome.greeting_default", "잿골은 늘 그대로일세. 잠시 숨을 돌리시지요."),
                HintText: Localize(GameLocalizationTables.UITown, "ui.town.welcome.hint", "클릭해 동료 시트를 봅니다.")),
            RosterCountText: $"{rosterCount} / {rosterCap}",
            ExpeditionLabel: BuildExpeditionLabel(session),
            ExpeditionTooltip: BuildExpeditionTooltip(session),
            QuickBattleLabel: Localize(GameLocalizationTables.UITown, "ui.town.action.quick_battle_smoke", "Quick Battle (Smoke)"),
            QuickBattleTooltip: Localize(GameLocalizationTables.UITown, "ui.town.tooltip.quick_battle_smoke", "Open an integration smoke battle using the current Town build, then return through Reward or direct Town restore."),
            CanQuickBattle: showDebugActions && session.CanStartQuickBattleSmoke,
            ShowQuickBattle: showDebugActions,
            StatusText: statusText);
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
            Localize(
                GameLocalizationTables.UITown,
                "ui.town.help.body",
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
            return Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_reward", "Open Reward to settle the previous node before continuing.");
        }

        return session.CanResumeExpedition
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_resume", "Resume the authored expedition from the currently selected route.")
            : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_start", "Begin the authored expedition loop with the current preparation.");
    }

    private string BuildReturnToStartTooltip(GameSessionState session)
    {
        return IsReturnToStartBlocked(session)
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start_locked", "Blocked while an authored run or smoke battle is still active.")
            : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start", "Leave the local run shell and go back to Boot.");
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
