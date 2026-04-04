using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class GlobalLocalizationOverlayView : MonoBehaviour
{
    private const string OverlayName = "GlobalLocalizationOverlay";

    [SerializeField] private RectTransform rootRect = null!;
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text currentLocaleText = null!;
    [SerializeField] private Button koreanButton = null!;
    [SerializeField] private Button englishButton = null!;
    [SerializeField] private Text koreanButtonLabel = null!;
    [SerializeField] private Text englishButtonLabel = null!;

    private GameLocalizationController _controller = null!;

    public static GlobalLocalizationOverlayView EnsureAttached(RectTransform parent, GameLocalizationController controller)
    {
        var existing = parent.Find(OverlayName)?.GetComponent<GlobalLocalizationOverlayView>();
        if (existing != null)
        {
            existing.Bind(controller);
            return existing;
        }

        var overlayGo = new GameObject(
            OverlayName,
            typeof(RectTransform),
            typeof(Image),
            typeof(GlobalLocalizationOverlayView));
        overlayGo.transform.SetParent(parent, false);

        var overlay = overlayGo.GetComponent<GlobalLocalizationOverlayView>();
        overlay.Build();
        overlay.Bind(controller);
        return overlay;
    }

    public void ToggleVisibility()
    {
        rootRect.gameObject.SetActive(!rootRect.gameObject.activeSelf);
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.LocaleChanged -= HandleLocaleChanged;
        }
    }

    private void Build()
    {
        rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(20f, -20f);
        rootRect.sizeDelta = new Vector2(168f, 106f);

        var background = GetComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.12f, 0.86f);

        titleText = CreateText(rootRect, "TitleText", new Vector2(0f, -8f), new Vector2(152f, 18f), 13, TextAnchor.MiddleCenter);
        currentLocaleText = CreateText(rootRect, "CurrentLocaleText", new Vector2(0f, -28f), new Vector2(152f, 18f), 12, TextAnchor.MiddleCenter);
        koreanButton = CreateButton(rootRect, "KoreanButton", new Vector2(0f, -54f), out koreanButtonLabel);
        englishButton = CreateButton(rootRect, "EnglishButton", new Vector2(0f, -82f), out englishButtonLabel);

        koreanButton.onClick.AddListener(() => _controller?.TrySetLocale("ko"));
        englishButton.onClick.AddListener(() => _controller?.TrySetLocale("en"));
    }

    private void Bind(GameLocalizationController controller)
    {
        if (_controller != null)
        {
            _controller.LocaleChanged -= HandleLocaleChanged;
        }

        _controller = controller;
        _controller.LocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        Refresh();
    }

    private void Refresh()
    {
        GameFontCatalog.ApplyToHierarchy(rootRect);

        if (_controller == null)
        {
            return;
        }

        titleText.text = _controller.LocalizeOrFallback(GameLocalizationTables.UICommon, "ui.common.language", "Language");
        currentLocaleText.text = _controller.CurrentLocale == null
            ? "-"
            : $"{_controller.LocalizeOrFallback(GameLocalizationTables.UICommon, "ui.common.current_language", "Current")}: {_controller.GetLocaleButtonLabel(_controller.CurrentLocale)}";

        koreanButtonLabel.text = _controller.GetLocaleButtonLabel(
            UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale("ko") ?? _controller.CurrentLocale!);
        englishButtonLabel.text = _controller.GetLocaleButtonLabel(
            UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale("en") ?? _controller.CurrentLocale!);
    }

    private static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        GameFontCatalog.ApplyFont(text);
        return text;
    }

    private static Button CreateButton(Transform parent, string name, Vector2 anchoredPosition, out Text label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(132f, 22f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.20f, 0.28f, 0.40f, 0.98f);

        var button = go.GetComponent<Button>();
        label = CreateText(go.transform, "Label", Vector2.zero, new Vector2(126f, 18f), 12, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
}
