using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Unity.UI;

namespace SM.Unity.UI.Battle;

public sealed class BattleScreenPresenter
{
    private const int MaxVisibleLogs = 5;
    private const int MaxVisibleDecisive = 3;

    private readonly GameLocalizationController _localization;
    private readonly GameSessionState _sessionState;
    private readonly BattlePresentationOptions _options;

    public BattleScreenPresenter(
        GameLocalizationController localization,
        GameSessionState sessionState,
        BattlePresentationOptions options)
    {
        _localization = localization;
        _sessionState = sessionState;
        _options = options;
    }

    public BattleShellViewState BuildLoadingState(bool showHelp = false)
    {
        return CreateState(
            allyHpText: string.Empty,
            enemyHpText: string.Empty,
            logText: Localize(GameLocalizationTables.UIBattle, "ui.battle.log.preparing", "Preparing battle log"),
            resultText: Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress"),
            playbackText: BuildPlaybackText(isPaused: false, playbackSpeed: 1f),
            statusText: Localize(GameLocalizationTables.UIBattle, "ui.battle.status.initializing", "Initializing live simulation"),
            progressNormalized: 0f,
            isPaused: false,
            isBattleFinished: false,
            showSettings: false,
            canReplay: false,
            canRebattle: false,
            canPause: false,
            canChangeSpeed: false,
            showHelp: showHelp,
            selectedUnit: BattleSelectedUnitViewState.Hidden);
    }

    public BattleShellViewState BuildErrorState(string message, bool showHelp = false)
    {
        return CreateState(
            allyHpText: string.Empty,
            enemyHpText: string.Empty,
            logText: message,
            resultText: message,
            playbackText: BuildPlaybackText(isPaused: false, playbackSpeed: 1f),
            statusText: message,
            progressNormalized: 0f,
            isPaused: false,
            isBattleFinished: false,
            showSettings: false,
            canReplay: false,
            canRebattle: false,
            canPause: false,
            canChangeSpeed: false,
            settingsStatusText: message,
            showHelp: showHelp,
            selectedUnit: BattleSelectedUnitViewState.Hidden);
    }

    public BattleShellViewState BuildState(
        BattleSimulationStep step,
        IReadOnlyList<BattleEvent> recentLogs,
        IReadOnlyList<string> decisiveTimeline,
        int totalEventCount,
        bool isPaused,
        float playbackSpeed,
        bool isBattleFinished,
        bool showSettings,
        float progressNormalized,
        string settingsStatusText,
        bool canReplay,
        bool canRebattle,
        bool canPause = false,
        bool canChangeSpeed = false,
        bool showHelp = false,
        BattleSelectedUnitViewState? selectedUnit = null)
    {
        return CreateState(
            BuildTeamSummary(
                Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.allies", "Allies"),
                step.Units.Where(actor => actor.Side == TeamSide.Ally)),
            BuildTeamSummary(
                Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.enemies", "Enemies"),
                step.Units.Where(actor => actor.Side == TeamSide.Enemy)),
            BuildLogText(step, recentLogs, decisiveTimeline),
            BuildResultText(step, totalEventCount),
            BuildPlaybackText(isPaused, playbackSpeed),
            BuildStatus(step, isPaused),
            progressNormalized,
            isPaused,
            isBattleFinished,
            showSettings,
            canReplay,
            canRebattle,
            canPause,
            canChangeSpeed,
            settingsStatusText,
            showHelp,
            selectedUnit ?? BattleSelectedUnitViewState.Hidden);
    }

    private BattleShellViewState CreateState(
        string allyHpText,
        string enemyHpText,
        string logText,
        string resultText,
        string playbackText,
        string statusText,
        float progressNormalized,
        bool isPaused,
        bool isBattleFinished,
        bool showSettings,
        bool canReplay,
        bool canRebattle,
        bool canPause,
        bool canChangeSpeed,
        string? settingsStatusText = null,
        bool showHelp = false,
        BattleSelectedUnitViewState? selectedUnit = null)
    {
        var isSmoke = _sessionState.IsQuickBattleSmokeActive;
        var isDirect = _sessionState.IsDirectCombatSandboxLane;
        return new BattleShellViewState(
            Localize(GameLocalizationTables.UIBattle, "ui.battle.title", "Battle"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            Localize(GameLocalizationTables.UICommon, "ui.common.help", "Help"),
            CreateHelpState(showHelp),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.summary", "Summary"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.allies", "Allies"),
            allyHpText,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.enemies", "Enemies"),
            enemyHpText,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.log", "Log"),
            logText,
            resultText,
            playbackText,
            statusText,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.group.playback", "Playback"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.speed_1", "x1"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.speed_2", "x2"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.speed_4", "x4"),
            isPaused
                ? Localize(GameLocalizationTables.UICommon, "ui.common.resume", "Resume")
                : Localize(GameLocalizationTables.UICommon, "ui.common.pause", "Pause"),
            Localize(
                GameLocalizationTables.UIBattle,
                isDirect ? "ui.battle.tooltip.playback_direct" : "ui.battle.tooltip.playback",
                isDirect
                    ? "Combat Sandbox lets you pause, replay the same seed, and roll a new seed."
                    : "Quick Battle (Smoke) lets you pause, replay, and change playback speed."),
            isSmoke,
            canChangeSpeed,
            canPause,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.group.primary", isDirect ? "Sandbox Result" : "Primary Action"),
            Localize(GameLocalizationTables.UICommon, "ui.common.continue", "Continue"),
            !isDirect,
            isBattleFinished
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.continue_ready", "Proceed to Reward with the resolved battle result.")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.continue_locked", "Continue activates after the battle is fully resolved."),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.action.replay_same_seed" : "ui.battle.action.replay", isDirect ? "Replay Same Seed" : "Replay"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.replay_same_seed" : "ui.battle.tooltip.replay", isDirect ? "Replay the active Combat Sandbox battle with the same deterministic seed." : "Replay the current Quick Battle (Smoke) timeline from the start."),
            canReplay,
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.action.new_seed" : "ui.battle.action.rebattle", isDirect ? "New Seed" : "Rebattle (Debug)"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.new_seed" : "ui.battle.tooltip.rebattle", isDirect ? "Restart Combat Sandbox with the next seed while keeping the active preset." : "Restart Quick Battle (Smoke) with a fresh seed."),
            canRebattle,
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.action.exit_sandbox" : "ui.battle.action.return_town_debug", isDirect ? "Exit Sandbox" : "Return to Town (Debug)"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.exit_sandbox" : "ui.battle.tooltip.return_town_direct", isDirect ? "Leave Combat Sandbox after the battle is resolved." : "Leave Quick Battle (Smoke) after the battle is resolved and go directly back to Town."),
            isSmoke && isBattleFinished,
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.group.sandbox" : "ui.battle.group.smoke", isDirect ? "Combat Sandbox" : "Quick Battle (Smoke)"),
            isSmoke,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.group.utility", "Utility"),
            Localize(GameLocalizationTables.UICommon, "ui.common.settings", "Settings"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.settings_sandbox" : "ui.battle.tooltip.settings", isDirect ? "Open display settings. Sandbox diagnostics appear only in Combat Sandbox." : "Open display settings. Debug settings appear only in Quick Battle (Smoke)."),
            progressNormalized,
            true,
            _options.ShowTeamHpSummary,
            !isDirect && isBattleFinished,
            new BattleSettingsViewState(
                showSettings,
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings"),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.display", "Display"),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.overhead_ui", "Overhead UI {0}", BuildStateLabel(_options.ShowOverheadUi)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.overhead_ui", "Show HP and state over units in the battle field."),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.damage_text", "Damage Text {0}", BuildStateLabel(_options.ShowDamageText)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.damage_text", "Show floating damage and heal numbers during battle."),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.team_summary", "Team Summary {0}", BuildStateLabel(_options.ShowTeamHpSummary)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.team_summary", "Show ally and enemy team summaries in the side panels."),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.debug", "Debug"),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.debug_overlay", "Debug Overlay {0}", BuildStateLabel(_options.ShowDebugOverlay)),
                Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.debug_overlay_sandbox" : "ui.battle.tooltip.debug_overlay", isDirect ? "Show targeting lines and sandbox diagnostics for Combat Sandbox." : "Show targeting lines and battle diagnostics for Quick Battle (Smoke)."),
                isSmoke,
                string.IsNullOrWhiteSpace(settingsStatusText)
                    ? Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings")
                    : settingsStatusText),
            selectedUnit ?? BattleSelectedUnitViewState.Hidden);
    }

    private HelpStripViewState CreateHelpState(bool showHelp)
    {
        var helpBody = _sessionState.IsDirectCombatSandboxLane
            ? "Read the battle through the summary, recent log, and selected unit panel. Combat Sandbox stays inside battle: replay the same seed, roll a new seed, or exit the sandbox."
            : "Read the battle through the summary, recent log, and selected unit panel. Continue unlocks after the battle resolves.";
        return new HelpStripViewState(
            showHelp,
            Localize(
                GameLocalizationTables.UIBattle,
                _sessionState.IsDirectCombatSandboxLane ? "ui.battle.help.body_sandbox" : "ui.battle.help.body",
                helpBody),
            Localize(GameLocalizationTables.UICommon, "ui.common.hide", "Hide"));
    }

    private string BuildLocaleStatus()
    {
        var locale = _localization.CurrentLocale;
        if (locale == null)
        {
            return "-";
        }

        return $"{Localize(GameLocalizationTables.UICommon, "ui.common.current_language", "Current")}: {_localization.GetLocaleButtonLabel(locale)}";
    }

    private string BuildStateLabel(bool isOn)
    {
        return isOn
            ? Localize(GameLocalizationTables.UICommon, "ui.common.on", "ON")
            : Localize(GameLocalizationTables.UICommon, "ui.common.off", "OFF");
    }

    private string GetLocaleButtonLabel(string localeCode, string fallback)
    {
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        return locale != null ? _localization.GetLocaleButtonLabel(locale) : fallback;
    }

    private string BuildPlaybackText(bool isPaused, float playbackSpeed)
    {
        if (_sessionState.IsDirectCombatSandboxLane)
        {
            return isPaused
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.direct_paused", "Combat Sandbox | Speed x{0:0} | Paused", playbackSpeed)
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.direct", "Combat Sandbox | Speed x{0:0}", playbackSpeed);
        }

        if (_sessionState.IsQuickBattleSmokeActive)
        {
            return isPaused
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.quick_paused", "Quick Battle (Smoke) | Speed x{0:0} | Paused", playbackSpeed)
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.quick", "Quick Battle (Smoke) | Speed x{0:0}", playbackSpeed);
        }

        return Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.ingame", "Authored Expedition Battle");
    }

    private string BuildTeamSummary(string label, IEnumerable<BattleUnitReadModel> units)
    {
        var snapshot = units.ToList();
        if (snapshot.Count == 0)
        {
            return $"{label} 0/0 | 0 / 0 HP";
        }

        var alive = snapshot.Count(unit => unit.IsAlive);
        var total = snapshot.Count;
        var currentHp = snapshot.Sum(unit => UnityEngine.Mathf.Max(0f, unit.CurrentHealth));
        var maxHp = snapshot.Sum(unit => UnityEngine.Mathf.Max(1f, unit.MaxHealth));
        return $"{label} {alive}/{total} | {currentHp:0} / {maxHp:0} HP";
    }

    private string BuildStatus(BattleSimulationStep step, bool isPaused)
    {
        var pausedSuffix = isPaused
            ? $" | {Localize(GameLocalizationTables.UIBattle, "ui.battle.status.pause_suffix", "Paused")}"
            : string.Empty;
        var pressure = BuildPressureLabel(step);

        if (step.IsFinished)
        {
            var result = step.Winner == TeamSide.Ally
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.status.finished_summary", "Result | {0} | {1}{2}", result, pressure, pausedSuffix);
        }

        if (BattleReadabilityFormatter.TryResolveStepFocus(step, out var focus))
        {
            var verb = BattleReadabilityFormatter.BuildSemanticLabel(focus.Semantic);
            if (focus.IsWindup)
            {
                verb = $"{verb} {UnityEngine.Mathf.RoundToInt(focus.Progress * 100f)}%";
            }

            return Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.step_focus",
                "Step {0:000} | {1} {2} -> {3} | {4}{5}",
                step.StepIndex,
                focus.ActorName,
                verb,
                focus.TargetName,
                pressure,
                pausedSuffix);
        }

        return Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.status.step_only",
            "Step {0:000} | {1}{2}",
            step.StepIndex,
            pressure,
            pausedSuffix);
    }

    private string BuildPressureLabel(BattleSimulationStep step)
    {
        var diff = BattleReadabilityFormatter.ComputePressureScore(step, TeamSide.Ally);
        if (diff > 0.08f)
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.pressure.allies", "Allies pressing");
        }

        if (diff < -0.08f)
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.pressure.enemies", "Enemies pressing");
        }

        return Localize(GameLocalizationTables.UIBattle, "ui.battle.pressure.even", "Pressure even");
    }

    private string BuildResultText(BattleSimulationStep step, int totalEventCount)
    {
        if (!step.IsFinished)
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress");
        }

        var outcome = step.Winner == TeamSide.Ally
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
        return Localize(GameLocalizationTables.UIBattle, "ui.battle.result.summary", "{0} | {1:000} steps | {2} events", outcome, step.StepIndex, totalEventCount);
    }

    private string BuildLogText(
        BattleSimulationStep step,
        IReadOnlyList<BattleEvent> recentLogs,
        IReadOnlyList<string> decisiveTimeline)
    {
        if (step.IsFinished && decisiveTimeline.Count > 0)
        {
            var decisive = decisiveTimeline.TakeLast(MaxVisibleDecisive);
            var recent = recentLogs.TakeLast(2).Select(BuildLogLine);
            return string.Join("\n", decisive.Concat(recent));
        }

        return string.Join("\n", recentLogs.TakeLast(MaxVisibleLogs).Select(BuildLogLine));
    }

    private string BuildLogLine(BattleEvent eventData)
    {
        var source = string.IsNullOrWhiteSpace(eventData.ActorName) ? "?" : eventData.ActorName;
        var target = string.IsNullOrWhiteSpace(eventData.TargetName) ? "?" : eventData.TargetName;
        return eventData.LogCode switch
        {
            BattleLogCode.BasicAttackDamage => Localize(GameLocalizationTables.UIBattle, "ui.battle.log.basic_attack", "[{0}] {1} hit {2} -{3:0}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.ActiveSkillHeal => Localize(GameLocalizationTables.UIBattle, "ui.battle.log.heal", "[{0}] {1} heal {2} +{3:0}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.ActiveSkillDamage => Localize(GameLocalizationTables.UIBattle, "ui.battle.log.skill", "[{0}] {1} skill {2} -{3:0}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.WaitDefend => Localize(GameLocalizationTables.UIBattle, "ui.battle.log.guard", "[{0}] {1} guard", eventData.StepIndex, source),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.Kill => Localize(GameLocalizationTables.UIBattle, "ui.battle.log.down", "[{0}] {1} down", eventData.StepIndex, target),
            _ => Localize(GameLocalizationTables.UIBattle, "ui.battle.log.generic", "[{0}] {1} {2}", eventData.StepIndex, source, BattleReadabilityFormatter.BuildShortEventVerb(eventData))
        };
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
