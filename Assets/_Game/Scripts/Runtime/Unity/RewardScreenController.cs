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
        _root.SessionState.EndOperatorRunToTown();
        _root.SaveProfile();
        _root.SceneFlow.ReturnToTown();
    }

    private void Choose(int index)
    {
        if (!EnsureReady()) return;

        if (_root.SessionState.ApplyRewardChoice(index))
        {
            _root.SaveProfile();
            Refresh($"보상 {index + 1}을 선택했고 저장까지 반영했습니다.");
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
            $"Inventory: {session.Profile.Inventory.Count}\n" +
            $"Temp Augments: {session.Expedition.TemporaryAugmentIds.Count}";
        choicesText.text = "3지선다 보상 카드";
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
            SetCardText(card, "KindText", choice == null ? "-" : choice.Kind.ToString());
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
}
