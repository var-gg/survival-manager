using System;
using System.Collections;
using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace SM.Unity;

public sealed class GameLocalizationController : MonoBehaviour
{
    private readonly HashSet<string> _reportedMissingKeys = new(StringComparer.Ordinal);

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

        // V1 narrative_korean_first 정책: PlayerPref/SystemLocale chain이 en으로
        // 떨어져도 ko로 강제 보정. 사용자가 명시적 locale switcher로 en 선택
        // 시점에만 다른 locale 허용 (그건 SetLocale 호출로 표현).
        //
        // PlayerPrefs "selected-locale"이 en으로 caching되어있으면 그게 우선이라
        // 매 init마다 ko로 reset. 사용자 명시 변경 전까지 ko fallback 유지.
        var currentCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
        if (!string.Equals(currentCode, "ko", StringComparison.OrdinalIgnoreCase))
        {
            var koLocale = LocalizationSettings.AvailableLocales?.GetLocale("ko");
            if (koLocale != null)
            {
                PlayerPrefs.DeleteKey("selected-locale");
                LocalizationSettings.SelectedLocale = koLocale;
            }
        }

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
        if (!LooksLikeMissingLocalizedString(localized, entryKey))
        {
            return FormatLocalizedStringIfNeeded(localized, arguments);
        }

        if (IsInitialized)
        {
            ReportMissingKey(tableCollection, entryKey);
        }

        if (!allowFallback && string.IsNullOrWhiteSpace(fallback))
        {
            return BuildMissingPlayerFacingString();
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

    public void ReportMissingKey(string tableCollection, string entryKey)
    {
        if (!IsDevelopmentFallbackContext)
        {
            return;
        }

        var dedupeKey = $"{tableCollection}:{entryKey}";
        if (!_reportedMissingKeys.Add(dedupeKey))
        {
            return;
        }

        Debug.LogWarning($"[Localization] Missing localized entry '{dedupeKey}'. Player-facing UI is using fallback text.");
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

    private static string BuildMissingPlayerFacingString()
    {
        return "[missing-localization]";
    }

    private static string FormatLocalizedStringIfNeeded(string localized, object[] arguments)
    {
        if (arguments.Length == 0 || localized.IndexOf('{', StringComparison.Ordinal) < 0)
        {
            return localized;
        }

        try
        {
            return string.Format(localized, arguments);
        }
        catch (FormatException)
        {
            return localized;
        }
    }

    private static bool LooksLikeMissingLocalizedString(string value, string entryKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return string.Equals(trimmed, entryKey, StringComparison.Ordinal)
               || trimmed.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("content.", StringComparison.Ordinal)
               || trimmed.StartsWith("ui.", StringComparison.Ordinal);
    }
}
