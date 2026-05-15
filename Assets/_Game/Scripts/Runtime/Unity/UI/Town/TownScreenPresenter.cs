using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Town;

/// <summary>
/// Town hub V1 presenter — RosterGrid 12 hero card + 핵심 hub 액션 (audit §2.1).
///
/// 옛 1253L dashboard presenter (recruit/deploy/character sheet/retrain/dismiss/board/augment 통합)는 폐기.
/// 보존: Save/Load/ReturnToStart/OpenExpedition/QuickBattle/Locale/Help. 후속 phase에서
/// Recruit/EquipmentRefit/PassiveBoard/PermanentAugment/CharacterSheet는 modal로 분리.
///
/// Localization keys는 옛 UiLocalizationAuditTests가 audit하는 ui.town.* / ui.common.* set 보존.
/// </summary>
public sealed class TownScreenPresenter
{
    private const string HelpPrefsKey = "SM.Help.Town";

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly TownScreenView _view;
    private readonly ScreenHelpState _helpState;

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
        var session = _root.SessionState;
        session.EnsureBattleDeployReady();
        _view.Render(BuildState(session, message));
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");

    public void ToggleHelp()
    {
        _helpState.Toggle();
        Refresh();
    }

    public void DismissHelp()
    {
        _helpState.Dismiss();
        Refresh();
    }

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
                _root.ActiveProfileId,
                _root.ActiveProfileId,
                reason));
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
        if (!checkpoint.IsSuccessful)
        {
            Refresh(checkpoint.Message);
        }
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

    private TownScreenViewState BuildState(GameSessionState session, string message)
    {
        var heroes = session.Profile.Heroes
            .Select(BuildHeroCard)
            .ToList();
        var statusText = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        var showDebugActions = Application.isEditor || Debug.isDebugBuild;

        return new TownScreenViewState(
            TitleEyebrow: Localize(GameLocalizationTables.UITown, "ui.town.eyebrow", "ASHGLEN — ROSTER"),
            Title: Localize(GameLocalizationTables.UITown, "ui.town.title", "Town Operator UI"),
            LocaleStatus: BuildLocaleStatus(),
            LocaleKoLabel: GetLocaleButtonLabel("ko", "한국어"),
            LocaleEnLabel: GetLocaleButtonLabel("en", "English"),
            HelpButtonLabel: Localize(GameLocalizationTables.UICommon, "ui.common.help", "Help"),
            Help: BuildHelpState(),
            RosterCap: MetaBalanceDefaults.TownRosterCap,
            Heroes: heroes,
            ExpeditionLabel: BuildExpeditionLabel(session),
            ExpeditionTooltip: BuildExpeditionTooltip(session),
            SaveLabel: Localize(GameLocalizationTables.UICommon, "ui.common.save", "Save"),
            LoadLabel: Localize(GameLocalizationTables.UICommon, "ui.common.load", "Load"),
            ReturnToStartLabel: Localize(GameLocalizationTables.UICommon, "ui.common.return_start", "Return to Start"),
            ReturnToStartTooltip: BuildReturnToStartTooltip(session),
            CanReturnToStart: !IsReturnToStartBlocked(session),
            QuickBattleLabel: Localize(GameLocalizationTables.UITown, "ui.town.action.quick_battle_smoke", "Quick Battle (Smoke)"),
            QuickBattleTooltip: Localize(GameLocalizationTables.UITown, "ui.town.tooltip.quick_battle_smoke", "Open an integration smoke battle using the current Town build, then return through Reward or direct Town restore."),
            CanQuickBattle: showDebugActions && session.CanStartQuickBattleSmoke,
            ShowQuickBattle: showDebugActions,
            StatusText: statusText);
    }

    private TownHeroCardViewState BuildHeroCard(HeroInstanceRecord hero)
    {
        _root.CombatContentLookup.TryGetArchetype(hero.HeroId, out var archetype);
        var classId = archetype?.Class?.Id ?? hero.ClassId ?? "vanguard";
        var raceId = archetype?.Race?.Id ?? hero.RaceId ?? "human";
        var className = _contentText.GetClassName(classId);
        var raceName = _contentText.GetRaceName(raceId);
        var archetypeName = _contentText.GetArchetypeName(hero.HeroId);

        var progression = _root.SessionState.Profile.HeroProgressions
            .FirstOrDefault(p => string.Equals(p.HeroId, hero.HeroId, StringComparison.Ordinal));
        var level = progression?.Level ?? 1;
        var xpPct = progression != null ? Mathf.Clamp(progression.Experience % 100, 0, 100) : 0;

        var displayName = !string.IsNullOrEmpty(hero.Name) ? hero.Name : archetypeName;
        var equipSlots = hero.EquippedItemIds?.Count(id => !string.IsNullOrEmpty(id)) ?? 0;

        return new TownHeroCardViewState(
            HeroId: hero.HeroId,
            DisplayName: displayName,
            ArchetypeLabel: $"{className} / {raceName}",
            FamilyKey: classId,
            RarityKey: hero.RecruitTier.ToString().ToLowerInvariant(),
            EquipSlots: equipSlots,
            Level: level,
            XpPct: xpPct);
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
                "1) Check chapter/site 2) confirm recruit and deploy 3) set posture 4) start the expedition."),
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
            : Localize(GameLocalizationTables.UITown, "ui.town.action.start_expedition", "Start Expedition");
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
