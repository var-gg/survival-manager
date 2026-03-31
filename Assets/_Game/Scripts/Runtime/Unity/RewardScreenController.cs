using UnityEngine;
using UnityEngine.UI;
using SM.Meta.Services;

namespace SM.Unity;

public sealed class RewardScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text summaryText = null!;
    [SerializeField] private Text choicesText = null!;
    [SerializeField] private RectTransform rewardCardsRoot = null!;
    [SerializeField] private Text statusText = null!;

    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;

    private void Start()
    {
        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return;
        }

        _localization = _root.Localization;
        _contentText = new ContentTextResolver(_localization, _root.CombatContentLookup);
        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Reward);
        Refresh();
    }

    private void OnDestroy()
    {
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
    }

    public void Choose0() => Choose(0);
    public void Choose1() => Choose(1);
    public void Choose2() => Choose(2);

    public void ReturnToTown()
    {
        if (!EnsureReady()) return;
        _root.SessionState.ReturnToTownAfterReward();
        _root.SaveProfile();
        _root.SceneFlow.ReturnToTown();
    }

    private void Choose(int index)
    {
        if (!EnsureReady()) return;

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

    private bool EnsureReady()
    {
        ValidateReferences();
        if (_root != null)
        {
            return true;
        }

        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return false;
        }

        return true;
    }

    private void ValidateReferences()
    {
        AssertText(titleText, nameof(titleText));
        AssertText(summaryText, nameof(summaryText));
        AssertText(choicesText, nameof(choicesText));
        AssertRect(rewardCardsRoot, nameof(rewardCardsRoot));
        AssertText(statusText, nameof(statusText));
    }

    private static void AssertText(Text text, string fieldName)
    {
        if (text == null)
        {
            Debug.LogError($"[RewardScreenController] Missing Text reference: {fieldName}");
        }
    }

    private static void AssertRect(RectTransform rectTransform, string fieldName)
    {
        if (rectTransform == null)
        {
            Debug.LogError($"[RewardScreenController] Missing RectTransform reference: {fieldName}");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.LogError($"[RewardScreenController] {message}");
    }

    private void Refresh(string message = "")
    {
        if (!EnsureReady()) return;

        var session = _root.SessionState;
        titleText.text = Localize(GameLocalizationTables.UIReward, "ui.reward.title", "Reward Operator UI");
        summaryText.text =
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.battle_result", "Battle Result: {0}", session.LastBattleVictory ? Localize(GameLocalizationTables.UIReward, "ui.reward.result.victory", "Victory") : Localize(GameLocalizationTables.UIReward, "ui.reward.result.defeat", "Defeat"))}\n" +
            $"{session.LastBattleSummary.Resolve(_localization, _contentText)}\n" +
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.auto_loot", "Auto Loot: {0}", session.LastAutomaticLootBundle == null ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None") : LootResolutionService.FormatSummary(session.LastAutomaticLootBundle))}\n" +
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.gold", "Gold: {0}", session.Profile.Currencies.Gold)}\n" +
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.reroll", "Trait Reroll: {0}", session.Profile.Currencies.TraitRerollCurrency)}\n" +
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.crafting", "Craft Mats: Ember {0} / Echo {1} / Sigil {2}", session.Profile.Currencies.EmberDust, session.Profile.Currencies.EchoCrystal, session.Profile.Currencies.BossSigil)}\n" +
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.slots", "Perm Slots: {0}", session.PermanentAugmentSlotCount)}\n" +
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.inventory", "Inventory: {0}", session.Profile.Inventory.Count)}\n" +
            $"{Localize(GameLocalizationTables.UIReward, "ui.reward.summary.temp_augments", "Temp Augments: {0}", session.Expedition.TemporaryAugmentIds.Count)}";
        choicesText.text = Localize(GameLocalizationTables.UIReward, "ui.reward.choices.header", "Choose one reward card");
        RefreshRewardCards(session);
        statusText.text = string.IsNullOrWhiteSpace(message)
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.status.default", "Pick one card and return to town.")
            : message;
    }

    private void RefreshRewardCards(GameSessionState session)
    {
        for (var i = 0; i < 3; i++)
        {
            var card = rewardCardsRoot.Find($"ChoiceCard{i + 1}");
            if (card == null)
            {
                continue;
            }

            var choice = i < session.PendingRewardChoices.Count ? session.PendingRewardChoices[i] : null;
            SetCardText(card, "TitleText", choice == null ? Localize(GameLocalizationTables.UIReward, "ui.reward.choice.empty", "Empty Card") : ResolveChoiceTitle(choice));
            SetCardText(card, "BodyText", choice == null ? Localize(GameLocalizationTables.UIReward, "ui.reward.choice.none", "No reward choice is available.") : ResolveChoiceDescription(choice));
            SetCardText(card, "KindText", choice == null ? "-" : BuildKindText(choice));
            SetCardText(card, "ChooseButton/Label", Localize(GameLocalizationTables.UIReward, "ui.reward.action.choose", "Choose"));

            var image = card.GetComponent<Image>();
            if (image != null)
            {
                image.color = ResolveCardColor(choice);
            }
        }
    }

    private static void SetCardText(Transform cardRoot, string childName, string value)
    {
        var child = cardRoot.Find(childName);
        if (child == null)
        {
            return;
        }

        var text = child.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private string BuildKindText(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.gold", "Gold +{0}", choice.GoldAmount),
            RewardChoiceKind.Item => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.item", "Item / {0}", _contentText.GetItemName(choice.PayloadId)),
            RewardChoiceKind.TemporaryAugment => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.temp_augment", "Temp / {0}", _contentText.GetAugmentName(choice.PayloadId)),
            RewardChoiceKind.TraitRerollCurrency => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.reroll", "Trait Reroll +{0}", choice.TraitRerollAmount),
            RewardChoiceKind.PermanentAugmentSlot => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.permanent_slot", "Permanent Slot +{0}", choice.PermanentSlotAmount),
            _ => choice.Kind.ToString()
        };
    }

    private static Color ResolveCardColor(RewardChoiceViewModel? choice)
    {
        if (choice == null)
        {
            return new Color(0.18f, 0.21f, 0.30f, 0.96f);
        }

        return choice.Kind switch
        {
            RewardChoiceKind.Gold => new Color(0.48f, 0.36f, 0.16f, 0.96f),
            RewardChoiceKind.Item => new Color(0.20f, 0.28f, 0.42f, 0.96f),
            RewardChoiceKind.TemporaryAugment => new Color(0.18f, 0.34f, 0.24f, 0.96f),
            RewardChoiceKind.TraitRerollCurrency => new Color(0.28f, 0.22f, 0.44f, 0.96f),
            RewardChoiceKind.PermanentAugmentSlot => new Color(0.40f, 0.24f, 0.14f, 0.96f),
            _ => new Color(0.18f, 0.21f, 0.30f, 0.96f)
        };
    }

    private string ResolveChoiceTitle(RewardChoiceViewModel choice)
    {
        return Localize(GameLocalizationTables.UIReward, choice.TitleKey, choice.PayloadId);
    }

    private string ResolveChoiceDescription(RewardChoiceViewModel choice)
    {
        return Localize(GameLocalizationTables.UIReward, choice.DescriptionKey, choice.PayloadId);
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
        Refresh();
    }
}
