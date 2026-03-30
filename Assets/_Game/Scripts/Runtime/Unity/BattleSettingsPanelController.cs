using System;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleSettingsPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform panelRoot = null!;
    [SerializeField] private Text worldHpButtonLabel = null!;
    [SerializeField] private Text overlayHpButtonLabel = null!;
    [SerializeField] private Text teamSummaryButtonLabel = null!;
    [SerializeField] private Text statusText = null!;

    private BattlePresentationOptions _options = null!;
    private Action<BattlePresentationOptions>? _applyCallback;
    private GameLocalizationController? _localization;
    private bool _initialized;

    public void Initialize(BattlePresentationOptions options, Action<BattlePresentationOptions> applyCallback)
    {
        _options = options;
        _applyCallback = applyCallback;
        _localization ??= GameSessionRoot.Instance?.Localization;
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
            _localization.LocaleChanged += HandleLocaleChanged;
        }

        if (!_initialized && panelRoot != null)
        {
            panelRoot.gameObject.SetActive(false);
            _initialized = true;
        }

        RefreshLabels();
        _applyCallback?.Invoke(_options);
    }

    public void TogglePanel()
    {
        if (!EnsureReady()) return;
        panelRoot.gameObject.SetActive(!panelRoot.gameObject.activeSelf);
        statusText.text = panelRoot.gameObject.activeSelf
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.closed", "Settings panel closed");
        RefreshLabels();
    }

    public void ToggleWorldActorHp()
    {
        if (!EnsureReady()) return;
        _options.ToggleWorldActorHp();
        Apply("WorldActorHp", Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.world_hp_label", "Actor HP"));
    }

    public void ToggleOverlayActorHp()
    {
        if (!EnsureReady()) return;
        _options.ToggleOverlayActorHp();
        Apply("OverlayActorHp", Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.overlay_hp_label", "Overlay HP"));
    }

    public void ToggleTeamSummary()
    {
        if (!EnsureReady()) return;
        _options.ToggleTeamHpSummary();
        Apply("TeamSummary", Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.team_summary_label", "Team Summary"));
    }

    private void Apply(string settingId, string label)
    {
        _applyCallback?.Invoke(_options);
        statusText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.state_changed", "{0}: {1}", label, BuildStateLabel(settingId));
        RefreshLabels();
    }

    private string BuildStateLabel(string label)
    {
        var isOn = label switch
        {
            "WorldActorHp" => _options.ShowWorldActorHp,
            "OverlayActorHp" => _options.ShowOverlayActorHp,
            _ => _options.ShowTeamHpSummary
        };
        return isOn
            ? Localize(GameLocalizationTables.UICommon, "ui.common.on", "ON")
            : Localize(GameLocalizationTables.UICommon, "ui.common.off", "OFF");
    }

    private void RefreshLabels()
    {
        if (worldHpButtonLabel != null)
        {
            worldHpButtonLabel.text = Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.settings.world_hp",
                "Actor HP {0}",
                BuildStateLabel("WorldActorHp"));
        }

        if (overlayHpButtonLabel != null)
        {
            overlayHpButtonLabel.text = Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.settings.overlay_hp",
                "Overlay HP {0}",
                BuildStateLabel("OverlayActorHp"));
        }

        if (teamSummaryButtonLabel != null)
        {
            teamSummaryButtonLabel.text = Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.settings.team_summary",
                "Team Summary {0}",
                BuildStateLabel("TeamSummary"));
        }

        if (statusText != null && string.IsNullOrWhiteSpace(statusText.text))
        {
            statusText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings");
        }
    }

    private void OnDestroy()
    {
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
    }

    private bool EnsureReady()
    {
        if (panelRoot == null || worldHpButtonLabel == null || overlayHpButtonLabel == null || teamSummaryButtonLabel == null || statusText == null)
        {
            Debug.LogError("[BattleSettingsPanelController] Missing settings panel references.");
            return false;
        }

        if (_options == null)
        {
            Debug.LogError("[BattleSettingsPanelController] Presentation options are not initialized.");
            return false;
        }

        return true;
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization != null
            ? _localization.LocalizeOrFallback(table, key, fallback, args)
            : args.Length == 0
                ? fallback
                : string.Format(fallback, args);
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        RefreshLabels();
    }
}
