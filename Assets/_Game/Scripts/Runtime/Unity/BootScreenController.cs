using SM.Meta.Model;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity
{

public sealed class BootScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text statusText = null!;
    [SerializeField] private Text hintText = null!;
    [SerializeField] private Button offlineLocalButton = null!;

    private GameSessionRoot _root = null!;

    private void Start()
    {
        _root = GameSessionRoot.EnsureInstance();

        if (offlineLocalButton != null)
        {
            offlineLocalButton.onClick.RemoveListener(HandleOfflineSelected);
            offlineLocalButton.onClick.AddListener(HandleOfflineSelected);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_root == null)
        {
            return;
        }

        // 폴백이 곧 이 화면의 문구다.
        //
        // 시작 화면은 로컬라이제이션이 초기화되기 전에 그려진다 — 실측하면 테이블 값이 아니라
        // <b>여기 적힌 폴백이 그대로 화면에 나온다</b>(시드를 한국어로 바꿔도 "Start" 가 떴다).
        // 그래서 이 넷은 "번역 실패 시 임시 문자열"이 아니라 <b>제품 문구</b>로 취급한다.
        // 시드(ui.common.start_screen.*)와 문구를 일치시켜 두 경로가 갈라지지 않게 한다.
        SetButtonLabel(offlineLocalButton, Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_local_run",
            "이어서 시작"));
        offlineLocalButton.interactable = !_root.HasBlockingError;

        titleText.text = Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.title",
            "잿골 연대기");
        statusText.text = BuildStatusText();
        var hint = BuildHintText();
        hintText.text = hint;
        hintText.gameObject.SetActive(!string.IsNullOrWhiteSpace(hint));
    }

    private void HandleOfflineSelected()
    {
        if (!_root.StartRealm(SessionRealm.OfflineLocal, out var error))
        {
            statusText.text = error;
            return;
        }

        _root.ClearBlockingError();
        _root.SceneFlow.GoToTown();
    }

    private string BuildStatusText()
    {
        if (_root.HasBlockingError)
        {
            return _root.LastBlockingError ?? string.Empty;
        }

        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.status",
            "잿문이 닫힌 뒤로, 변방에 남은 것은 잿골 하나뿐이다.");
    }

    private string BuildHintText()
    {
        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.hint",
            "마을을 꾸리고, 분대를 짜고, 원정에 나선다.");
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _root.Localization.LocalizeOrFallback(table, key, fallback, args);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        var text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = label;
        }
    }
}
}
