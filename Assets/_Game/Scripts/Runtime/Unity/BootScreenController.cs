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

        SetButtonLabel(offlineLocalButton, Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_local_run",
            "Start Local Run"));
        offlineLocalButton.interactable = !_root.HasBlockingError;

        titleText.text = Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.title",
            "Start");
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

        // 빌드 파이프라인 설명(영문)이 아니라 플레이어용 도입 1줄을 fallback으로 — Boot 온보딩 0줄 문제 해소.
        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.status",
            "잿골 마을로 이동 중 — 동료를 편성하고 첫 원정을 떠납니다.");
    }

    private string BuildHintText()
    {
        // 빈 문자열 대신 첫 행동을 안내 — 신규 플레이어가 시작 화면에서 막히지 않게.
        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.hint",
            "마을에서 동료를 모은 뒤 [원정으로]를 눌러 첫 전투를 시작하세요.");
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
