using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class RewardScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text summaryText = null!;
    [SerializeField] private Text choicesText = null!;
    [SerializeField] private RectTransform rewardCardsRoot = null!;
    [SerializeField] private Text statusText = null!;

    private GameSessionRoot _root = null!;

    private void Start()
    {
        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return;
        }

        _root.SessionState.SetCurrentScene(SceneNames.Reward);
        Refresh();
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

        if (_root.SessionState.ApplyRewardChoice(index))
        {
            _root.SaveProfile();
            Refresh(_root.SessionState.LastRewardApplicationSummary);
            return;
        }

        Refresh("보상 선택에 실패했습니다.");
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
        titleText.text = "Reward Operator UI";
        summaryText.text =
            $"전투 결과: {(session.LastBattleVictory ? "승리" : "패배")}\n" +
            $"{session.LastBattleSummary}\n" +
            $"Gold: {session.Profile.Currencies.Gold}\n" +
            $"Trait Reroll: {session.Profile.Currencies.TraitRerollCurrency}\n" +
            $"Perm Slots: {session.PermanentAugmentSlotCount}\n" +
            $"Inventory: {session.Profile.Inventory.Count}\n" +
            $"Temp Augments: {session.Expedition.TemporaryAugmentIds.Count}";
        choicesText.text = "3지선다 보상 카드 / meta progression";
        RefreshRewardCards(session);
        statusText.text = string.IsNullOrWhiteSpace(message)
            ? "카드를 하나 고르고 Town으로 돌아가세요."
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
            SetCardText(card, "TitleText", choice?.Title ?? "빈 카드");
            SetCardText(card, "BodyText", choice == null ? "선택지가 없습니다." : choice.Description);
            SetCardText(card, "KindText", choice == null ? "-" : BuildKindText(choice));

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

    private static string BuildKindText(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => $"Gold +{choice.GoldAmount}",
            RewardChoiceKind.Item => $"Item / {choice.PayloadId}",
            RewardChoiceKind.TemporaryAugment => $"Temp / {choice.PayloadId}",
            RewardChoiceKind.TraitRerollCurrency => $"Trait Reroll +{choice.TraitRerollAmount}",
            RewardChoiceKind.PermanentAugmentSlot => $"Permanent Slot +{choice.PermanentSlotAmount}",
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
}
