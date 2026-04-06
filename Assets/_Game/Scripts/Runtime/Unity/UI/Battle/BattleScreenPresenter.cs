using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;

namespace SM.Unity.UI.Battle;

public sealed class BattleScreenPresenter
{
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
            settingsStatusText: message,
            selectedUnit: BattleSelectedUnitViewState.Hidden);
    }

    public BattleShellViewState BuildState(
        BattleSimulationStep step,
        IReadOnlyList<BattleEvent> recentLogs,
        int totalEventCount,
        bool isPaused,
        float playbackSpeed,
        bool isBattleFinished,
        bool showSettings,
        float progressNormalized,
        string settingsStatusText,
        BattleSelectedUnitViewState? selectedUnit = null)
    {
        return CreateState(
            BuildHpText(Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.allies", "Allied HP"), step.Units.Where(actor => actor.Side == TeamSide.Ally)),
            BuildHpText(Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.enemies", "Enemy HP"), step.Units.Where(actor => actor.Side == TeamSide.Enemy)),
            string.Join("\n", recentLogs.Select(BuildLogLine)),
            isBattleFinished
                ? step.Winner == TeamSide.Ally
                    ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
                    : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress"),
            isPaused
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.paused", "Speed x{0:0} | Paused", playbackSpeed)
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.active", "Speed x{0:0}", playbackSpeed),
            BuildStatus(step, totalEventCount, isPaused),
            progressNormalized,
            isPaused,
            isBattleFinished,
            showSettings,
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
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.rebattle", "Re-battle"),
            Localize(GameLocalizationTables.UICommon, "ui.common.return_town", "Return Town"),
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
        if (locale != null)
        {
            return _localization.GetLocaleButtonLabel(locale);
        }

        return fallback;
    }

    private string BuildHpText(string title, IEnumerable<BattleUnitReadModel> units)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        foreach (var unit in units)
        {
            var marker = unit.IsAlive ? "-" : "x";
            sb.AppendLine($"{marker} {ResolveUnitDisplayName(unit.Name)}: {unit.CurrentHealth:0}/{unit.MaxHealth:0}");
        }

        return sb.ToString();
    }

    private string BuildStatus(BattleSimulationStep step, int totalEventCount, bool isPaused)
    {
        var pauseLabel = isPaused
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.status.pause_suffix", " | Paused")
            : string.Empty;
        if (step.IsFinished)
        {
            return Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.finished",
                "Battle finished | {0} steps | {1} events",
                step.StepIndex,
                totalEventCount);
        }

        var lastEvent = step.Events.LastOrDefault();
        if (lastEvent != null)
        {
            return Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.last_event",
                "Step {0} | {1} -> {2} | {3} {4:0}{5}",
                step.StepIndex,
                lastEvent.ActorName,
                lastEvent.TargetName ?? "-",
                lastEvent.ActionType,
                lastEvent.Value,
                pauseLabel);
        }

        var windingUp = step.Units.FirstOrDefault(unit => unit.ActionState == CombatActionState.Windup);
        if (windingUp != null)
        {
            return Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.windup",
                "Step {0} | {1} windup {2}% -> {3}{4}",
                step.StepIndex,
                windingUp.Name,
                UnityEngine.Mathf.RoundToInt(windingUp.WindupProgress * 100f),
                windingUp.TargetName ?? "-",
                pauseLabel);
        }

        return Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.status.posture",
            "Step {0} | posture {1}{2}",
            step.StepIndex,
            _sessionState.SelectedTeamPosture,
            pauseLabel);
    }

    private string BuildLogLine(BattleEvent eventData)
    {
        var source = string.IsNullOrWhiteSpace(eventData.ActorName) ? "?" : eventData.ActorName;
        var target = string.IsNullOrWhiteSpace(eventData.TargetName) ? "?" : eventData.TargetName;
        return eventData.LogCode switch
        {
            BattleLogCode.BasicAttackDamage => Localize(GameLocalizationTables.CombatLog, "combat.log.damage", "S{0} {1} dealt {3:0} damage to {2}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.ActiveSkillHeal => Localize(GameLocalizationTables.CombatLog, "combat.log.heal", "S{0} {1} healed {2} for {3:0}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.ActiveSkillDamage => Localize(GameLocalizationTables.CombatLog, "combat.log.skill", "S{0} {1} used a skill on {2} for {3:0}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.WaitDefend => Localize(GameLocalizationTables.CombatLog, "combat.log.guard", "S{0} {1} took a guard stance", eventData.StepIndex, source),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.StatusApplied => Localize(GameLocalizationTables.CombatLog, "combat.log.status_applied", "S{0} {1} applied {2} to {3}", eventData.StepIndex, source, eventData.PayloadId, target),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.StatusRemoved => Localize(GameLocalizationTables.CombatLog, "combat.log.status_removed", "S{0} {1} removed {2}", eventData.StepIndex, target, eventData.PayloadId),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.CleanseTriggered => Localize(GameLocalizationTables.CombatLog, "combat.log.cleanse", "S{0} {1} cleansed {2} on {3}", eventData.StepIndex, source, eventData.PayloadId, target),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.ControlResistApplied => Localize(GameLocalizationTables.CombatLog, "combat.log.control_resist", "S{0} {1} gained control resist", eventData.StepIndex, target),
            _ => Localize(GameLocalizationTables.CombatLog, "combat.log.generic", "S{0} {1} {2}", eventData.StepIndex, source, eventData.ActionType)
        };
    }

    private string ResolveUnitDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith("content."))
        {
            return name;
        }

        var resolved = Localize(GameLocalizationTables.ContentArchetype, name, name);
        return resolved != name ? resolved : name.Replace("content.archetype.", "").Replace(".name", "");
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
