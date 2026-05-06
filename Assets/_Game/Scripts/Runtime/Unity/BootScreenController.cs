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

        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.status",
            "This build runs a local loop from Town through Expedition, Battle, and Reward.");
    }

    private string BuildHintText()
    {
        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.hint",
            string.Empty);
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
