using SM.Meta.Model;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

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

        SetButtonLabel(offlineLocalButton, "OfflineLocal");
        offlineLocalButton.interactable = !_root.HasBlockingError;

        titleText.text = Localize(
            GameLocalizationTables.UICommon,
            "ui.common.session_realm",
            "Session Realm");
        statusText.text = BuildStatusText();
        hintText.text = BuildHintText();
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

        if (_root.CurrentRealm is SessionRealm realm)
        {
            return Localize(
                GameLocalizationTables.UICommon,
                "ui.common.session_realm.current",
                "Current realm: {0}",
                realm);
        }

        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.session_realm.offline_only",
            "OfflineLocal 세션으로 Town 흐름을 시작하세요.");
    }

    private string BuildHintText()
    {
        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.session_realm.offline_hint",
            "현재 playable slice는 OfflineLocal만 지원합니다.\n런 중 세션 전환은 허용되지 않습니다.");
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
