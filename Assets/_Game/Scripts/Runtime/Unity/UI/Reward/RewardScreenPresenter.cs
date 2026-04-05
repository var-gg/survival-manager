using System.Collections.Generic;
using System.Text;
using SM.Meta.Services;

namespace SM.Unity.UI.Reward;

public sealed class RewardScreenPresenter
{
    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly RewardScreenView _view;

    public RewardScreenPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        RewardScreenView view)
    {
        _root = root;
        _localization = localization;
        _contentText = contentText;
        _view = view;
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void Choose0() => Choose(0);
    public void Choose1() => Choose(1);
    public void Choose2() => Choose(2);

    public void ReturnToTown()
    {
        _root.SessionState.ReturnToTownAfterReward();
        _root.SaveProfile();
        _root.SceneFlow.ReturnToTown();
    }

    public void Refresh(string message = "")
    {
        _view.Render(BuildState(_root.SessionState, message));
    }

    private void Choose(int index)
    {
        var choice = index >= 0 && index < _root.SessionState.PendingRewardChoices.Count
            ? _root.SessionState.PendingRewardChoices[index]
            : null;
        if (_root.SessionState.ApplyRewardChoice(index))
        {
            _root.SaveProfile();
            Refresh(choice == null
                ? Localize(GameLocalizationTables.UIReward, "ui.reward.status.choice_applied", "Reward applied.")
                : Localize(GameLocalizationTables.UIReward, "ui.reward.status.choice_applied_named", "{0} applied.", ResolveChoiceTitle(choice)));
            return;
        }

        Refresh(Localize(GameLocalizationTables.UIReward, "ui.reward.error.choice_failed", "Failed to apply the selected reward."));
    }

    private RewardScreenViewState BuildState(GameSessionState session, string message)
    {
        return new RewardScreenViewState(
            Localize(GameLocalizationTables.UIReward, "ui.reward.title", "Reward Operator UI"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            BuildSummaryText(session),
            Localize(GameLocalizationTables.UIReward, "ui.reward.choices.header", "Choose one reward card"),
            BuildChoiceCards(session),
            string.IsNullOrWhiteSpace(message)
                ? Localize(GameLocalizationTables.UIReward, "ui.reward.status.default", "Pick one card and return to town.")
                : message,
            Localize(GameLocalizationTables.UICommon, "ui.common.return_town", "Return Town"));
    }

    private IReadOnlyList<RewardChoiceCardViewState> BuildChoiceCards(GameSessionState session)
    {
        var cards = new List<RewardChoiceCardViewState>(3);
        for (var i = 0; i < 3; i++)
        {
            var choice = i < session.PendingRewardChoices.Count ? session.PendingRewardChoices[i] : null;
            cards.Add(new RewardChoiceCardViewState(
                choice == null ? Localize(GameLocalizationTables.UIReward, "ui.reward.choice.empty", "Empty Card") : ResolveChoiceTitle(choice),
                choice == null ? Localize(GameLocalizationTables.UIReward, "ui.reward.choice.none", "No reward choice is available.") : ResolveChoiceDescription(choice),
                choice == null ? "-" : BuildKindText(choice),
                Localize(GameLocalizationTables.UIReward, "ui.reward.action.choose", "Choose"),
                choice != null));
        }

        return cards;
    }

    private string BuildSummaryText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.battle_result",
            "Battle Result: {0}",
            session.LastBattleVictory
                ? Localize(GameLocalizationTables.UIReward, "ui.reward.result.victory", "Victory")
                : Localize(GameLocalizationTables.UIReward, "ui.reward.result.defeat", "Defeat")));
        sb.AppendLine(session.LastBattleSummary.Resolve(_localization, _contentText));
        sb.AppendLine(Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.auto_loot",
            "Auto Loot: {0}",
            session.LastAutomaticLootBundle == null ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None") : LootResolutionService.FormatSummary(session.LastAutomaticLootBundle)));
        sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.gold", "Gold: {0}", session.Profile.Currencies.Gold));
        sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.echo", "Echo: {0}", session.Profile.Currencies.Echo));
        sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.slots", "Perm Slots: {0}", session.PermanentAugmentSlotCount));
        sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.inventory", "Inventory: {0}", session.Profile.Inventory.Count));
        sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.temp_augments", "Temp Augments: {0}", session.Expedition.TemporaryAugmentIds.Count));
        return sb.ToString();
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

    private string GetLocaleButtonLabel(string localeCode, string fallback)
    {
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        if (locale != null)
        {
            return _localization.GetLocaleButtonLabel(locale);
        }

        return fallback;
    }

    private string BuildKindText(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.gold", "Gold +{0}", choice.GoldAmount),
            RewardChoiceKind.Item => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.item", "Item / {0}", _contentText.GetItemName(choice.PayloadId)),
            RewardChoiceKind.TemporaryAugment => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.temp_augment", "Temp / {0}", _contentText.GetAugmentName(choice.PayloadId)),
            RewardChoiceKind.Echo => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.echo", "Echo +{0}", choice.EchoAmount),
            RewardChoiceKind.PermanentAugmentSlot => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.permanent_slot", "Permanent Slot +{0}", choice.PermanentSlotAmount),
            _ => choice.Kind.ToString()
        };
    }

    private string ResolveChoiceTitle(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.TitleKey, choice.PayloadId);
    private string ResolveChoiceDescription(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.DescriptionKey, choice.PayloadId);
    private string Localize(string table, string key, string fallback, params object[] args) => _localization.LocalizeOrFallback(table, key, fallback, args);
}
