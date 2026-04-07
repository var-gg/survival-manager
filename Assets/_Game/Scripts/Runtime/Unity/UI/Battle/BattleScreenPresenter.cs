using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

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

    public BattleShellViewState BuildLoadingState()
    {
        return CreateState(
            allyHpText: string.Empty,
            enemyHpText: string.Empty,
            logText: Localize(GameLocalizationTables.UIBattle, "ui.battle.log.preparing", "Preparing battle log"),
            resultText: Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress"),
            speedText: Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.active", "Speed x{0:0}", 1f),
            statusText: Localize(GameLocalizationTables.UIBattle, "ui.battle.status.initializing", "Initializing live simulation"),
            progressNormalized: 0f,
            isPaused: false,
            isBattleFinished: false,
            showSettings: false,
            canReplay: false,
            canRebattle: false,
            selectedUnit: BattleSelectedUnitViewState.Hidden);
    }

    public BattleShellViewState BuildErrorState(string message)
    {
        return CreateState(
            allyHpText: string.Empty,
            enemyHpText: string.Empty,
            logText: message,
            resultText: message,
            speedText: Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.active", "Speed x{0:0}", 1f),
            statusText: message,
            progressNormalized: 0f,
            isPaused: false,
            isBattleFinished: false,
            showSettings: false,
            canReplay: false,
            canRebattle: false,
            settingsStatusText: message,
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
        BattleSelectedUnitViewState? selectedUnit = null)
    {
        return CreateState(
            BuildTeamSummary(Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.allies", "Allies"), step.Units.Where(actor => actor.Side == TeamSide.Ally)),
            BuildTeamSummary(Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.enemies", "Enemies"), step.Units.Where(actor => actor.Side == TeamSide.Enemy)),
            BuildLogText(step, recentLogs, decisiveTimeline),
            BuildResultText(step, totalEventCount),
            isPaused
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.paused", "Speed x{0:0} | Paused", playbackSpeed)
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.active", "Speed x{0:0}", playbackSpeed),
            BuildStatus(step, isPaused),
            progressNormalized,
            isPaused,
            isBattleFinished,
            showSettings,
            canReplay,
            canRebattle,
            settingsStatusText,
            selectedUnit ?? BattleSelectedUnitViewState.Hidden);
    }

    private BattleShellViewState CreateState(
        string allyHpText,
        string enemyHpText,
        string logText,
        string resultText,
        string speedText,
        string statusText,
        float progressNormalized,
        bool isPaused,
        bool isBattleFinished,
        bool showSettings,
        bool canReplay,
        bool canRebattle,
        string? settingsStatusText = null,
        BattleSelectedUnitViewState? selectedUnit = null)
    {
        return new BattleShellViewState(
            Localize(GameLocalizationTables.UIBattle, "ui.battle.title", "Battle Observer UI"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            allyHpText,
            enemyHpText,
            logText,
            resultText,
            speedText,
            statusText,
            isPaused
                ? Localize(GameLocalizationTables.UICommon, "ui.common.resume", "Resume")
                : Localize(GameLocalizationTables.UICommon, "ui.common.pause", "Pause"),
            Localize(GameLocalizationTables.UICommon, "ui.common.continue", "Continue"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.replay", "Replay"),
            canReplay,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.rebattle", "Rebattle"),
            canRebattle,
            Localize(GameLocalizationTables.UICommon, "ui.common.return_town", "Return Town"),
            _sessionState.IsQuickBattleSmokeActive && isBattleFinished,
            Localize(GameLocalizationTables.UICommon, "ui.common.settings", "Settings"),
            progressNormalized,
            _options.ShowTeamHpSummary,
            isBattleFinished,
            new BattleSettingsViewState(
                showSettings,
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.overhead_ui", "Overhead UI {0}", BuildStateLabel(_options.ShowOverheadUi)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.damage_text", "Damage Text {0}", BuildStateLabel(_options.ShowDamageText)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.team_summary", "Team Summary {0}", BuildStateLabel(_options.ShowTeamHpSummary)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.debug_overlay", "Debug Overlay {0}", BuildStateLabel(_options.ShowDebugOverlay)),
                string.IsNullOrWhiteSpace(settingsStatusText)
                    ? Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings")
                    : settingsStatusText),
            selectedUnit ?? BattleSelectedUnitViewState.Hidden);
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
            return $"Result | {result} | {pressure}{pausedSuffix}";
        }

        if (BattleReadabilityFormatter.TryResolveStepFocus(step, out var focus))
        {
            var verb = BattleReadabilityFormatter.BuildSemanticLabel(focus.Semantic);
            if (focus.IsWindup)
            {
                verb = $"{verb} {UnityEngine.Mathf.RoundToInt(focus.Progress * 100f)}%";
            }

            return $"Step {step.StepIndex:000} | {focus.ActorName} {verb} -> {focus.TargetName} | {pressure}{pausedSuffix}";
        }

        return $"Step {step.StepIndex:000} | {pressure}{pausedSuffix}";
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
        return $"{outcome} | {step.StepIndex:000} steps | {totalEventCount} events";
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
            BattleLogCode.BasicAttackDamage => $"[{eventData.StepIndex}] {source} hit {target} -{eventData.Value:0}",
            BattleLogCode.ActiveSkillHeal => $"[{eventData.StepIndex}] {source} heal {target} +{eventData.Value:0}",
            BattleLogCode.ActiveSkillDamage => $"[{eventData.StepIndex}] {source} skill {target} -{eventData.Value:0}",
            BattleLogCode.WaitDefend => $"[{eventData.StepIndex}] {source} guard",
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.Kill => $"[{eventData.StepIndex}] {target} down",
            _ => $"[{eventData.StepIndex}] {source} {BattleReadabilityFormatter.BuildShortEventVerb(eventData)}"
        };
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
