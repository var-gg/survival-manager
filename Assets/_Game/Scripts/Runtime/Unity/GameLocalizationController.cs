using System;
using System.Collections;
using System.Collections.Generic;
using SM.Content.Definitions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace SM.Unity;

public sealed class GameLocalizationController : MonoBehaviour
{
    public event Action<Locale>? LocaleChanged;

    public bool IsInitialized { get; private set; }

    public Locale? CurrentLocale => LocalizationSettings.SelectedLocale;

    public bool IsDevelopmentFallbackContext => Application.isEditor || Debug.isDebugBuild;

    public IReadOnlyList<Locale> AvailableLocales =>
        LocalizationSettings.AvailableLocales != null
            ? LocalizationSettings.AvailableLocales.Locales
            : Array.Empty<Locale>();

    public IEnumerator EnsureInitialized()
    {
        if (IsInitialized)
        {
            yield break;
        }

        yield return LocalizationSettings.InitializationOperation;
        if (IsInitialized)
        {
            yield break;
        }

        LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
        LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
        IsInitialized = true;

        if (LocalizationSettings.SelectedLocale != null)
        {
            LocaleChanged?.Invoke(LocalizationSettings.SelectedLocale);
        }
    }

    public string Localize(string tableCollection, string entryKey, params object[] arguments)
    {
        if (!IsInitialized || string.IsNullOrWhiteSpace(tableCollection) || string.IsNullOrWhiteSpace(entryKey))
        {
            return string.Empty;
        }

        return LocalizationSettings.StringDatabase.GetLocalizedString(
            tableCollection,
            entryKey,
            arguments);
    }

    public string LocalizeOrFallback(string tableCollection, string entryKey, string fallback, params object[] arguments)
    {
        return LocalizeOrFallback(tableCollection, entryKey, fallback, true, arguments);
    }

    public string LocalizeOrFallback(string tableCollection, string entryKey, string fallback, bool allowFallback, params object[] arguments)
    {
        var localized = Localize(tableCollection, entryKey, arguments);
        if (!string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        if (!allowFallback)
        {
            return BuildMissingPlayerFacingString(tableCollection, entryKey);
        }

        if (arguments.Length == 0)
        {
            return fallback;
        }

        try
        {
            return string.Format(fallback, arguments);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    public string LocalizePlayerFacingContent(string tableCollection, string entryKey, string fallback, params object[] arguments)
    {
        var allowFallback = ContentLocalizationPolicy.AllowsRuntimeFallback(IsDevelopmentFallbackContext);
        return LocalizeOrFallback(tableCollection, entryKey, fallback, allowFallback, arguments);
    }

    public string GetLocaleButtonLabel(Locale locale)
    {
        return locale.Identifier.Code switch
        {
            "ko" => LocalizeOrFallback(GameLocalizationTables.UICommon, "ui.common.locale.korean", "한국어"),
            "en" => LocalizeOrFallback(GameLocalizationTables.UICommon, "ui.common.locale.english", "English"),
            _ => locale.LocaleName
        };
    }

    public bool TrySetLocale(string localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode))
        {
            return false;
        }

        var locale = LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        if (locale == null)
        {
            return false;
        }

        LocalizationSettings.SelectedLocale = locale;
        return true;
    }

    public void RefreshActiveScene()
    {
        FirstPlayableRuntimeSceneBinder.RefreshLocalizedBindings(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
    }

    private void HandleSelectedLocaleChanged(Locale locale)
    {
        RefreshActiveScene();
        LocaleChanged?.Invoke(locale);
    }

    private static string BuildMissingPlayerFacingString(string tableCollection, string entryKey)
    {
        if (string.IsNullOrWhiteSpace(tableCollection) && string.IsNullOrWhiteSpace(entryKey))
        {
            return "[missing-localization]";
        }

        return $"[missing:{tableCollection}:{entryKey}]";
    }
}
