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
    [SerializeField] private Button onlineAuthoritativeButton = null!;

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
        SetButtonLabel(onlineAuthoritativeButton, "OnlineAuthoritative");
        offlineLocalButton.interactable = !_root.HasBlockingError;
        onlineAuthoritativeButton.interactable = false;

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
            "ui.common.session_realm.select",
            "OfflineLocal 또는 OnlineAuthoritative 세션 영역을 선택하세요.");
    }

    private string BuildHintText()
    {
        _root.CanStartRealm(SessionRealm.OnlineAuthoritative, out var onlineReason);
        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.session_realm.hint",
            "{0}\n런 중 authority 전환은 허용되지 않습니다.",
            onlineReason);
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
