using System.Collections.Generic;
using SM.Editor.Bootstrap;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Unity;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Authoring.Inspectors;

internal static class EditorLocalizedTextResolver
{
    private static bool _editorLocalizationInitialized;

    public static string CurrentLocaleCode =>
        ResolveCurrentLocaleCode();

    public static string Label(string koFallback, string enFallback)
    {
        return IsKoreanLocale() ? koFallback : enFallback;
    }

    public static string LocalizeUi(string key, string koFallback, string enFallback)
    {
        return Localize(GameLocalizationTables.UIBattle, key, Label(koFallback, enFallback));
    }

    public static string Localize(string tableName, string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null)
        {
            return fallback;
        }

        foreach (var localeCode in EnumerateLocaleFallbacks())
        {
            if (collection.GetTable(new LocaleIdentifier(localeCode)) is not StringTable table)
            {
                continue;
            }

            if (table.GetEntry(key) is not { } entry || string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            return entry.Value;
        }

        return fallback;
    }

    public static string GetCharacterName(CharacterDefinition? character, string fallbackId = "")
    {
        if (character == null)
        {
            return fallbackId;
        }

        var fallback = !string.IsNullOrWhiteSpace(character.LegacyDisplayName)
            ? character.LegacyDisplayName
            : fallbackId;
        return Localize(ContentLocalizationTables.Characters, character.NameKey, fallback);
    }

    public static string GetArchetypeName(UnitArchetypeDefinition? archetype, string fallbackId = "")
    {
        if (archetype == null)
        {
            return fallbackId;
        }

        var fallback = !string.IsNullOrWhiteSpace(archetype.LegacyDisplayName)
            ? archetype.LegacyDisplayName
            : fallbackId;
        return Localize(ContentLocalizationTables.Archetypes, archetype.NameKey, fallback);
    }

    public static string GetRaceName(RaceDefinition? race, string fallbackId = "")
    {
        if (race == null)
        {
            return fallbackId;
        }

        var fallback = !string.IsNullOrWhiteSpace(race.LegacyDisplayName)
            ? race.LegacyDisplayName
            : fallbackId;
        return Localize(ContentLocalizationTables.Races, race.NameKey, fallback);
    }

    public static string GetClassName(ClassDefinition? @class, string fallbackId = "")
    {
        if (@class == null)
        {
            return fallbackId;
        }

        var fallback = !string.IsNullOrWhiteSpace(@class.LegacyDisplayName)
            ? @class.LegacyDisplayName
            : fallbackId;
        return Localize(ContentLocalizationTables.Classes, @class.NameKey, fallback);
    }

    public static string GetRoleName(RoleInstructionDefinition? roleInstruction, string fallbackRoleTag = "", string fallbackId = "")
    {
        if (roleInstruction != null)
        {
            var fallback = !string.IsNullOrWhiteSpace(roleInstruction.LegacyDisplayName)
                ? roleInstruction.LegacyDisplayName
                : RoleGlossary.GetLocalizedRoleTagFallback(roleInstruction.RoleTag, CurrentLocaleCode);
            return Localize(ContentLocalizationTables.Roles, roleInstruction.NameKey, fallback);
        }

        var roleTag = string.IsNullOrWhiteSpace(fallbackRoleTag) ? fallbackId : fallbackRoleTag;
        return RoleGlossary.GetLocalizedRoleTagFallback(roleTag, CurrentLocaleCode);
    }

    public static string GetRoleFamilyName(ClassDefinition? @class)
    {
        if (@class == null)
        {
            return string.Empty;
        }

        var roleFamilyTag = RoleGlossary.GetRoleFamilyTagOrDefault(@class.Id);
        if (string.Equals(roleFamilyTag, @class.Id, System.StringComparison.Ordinal))
        {
            return GetClassName(@class, @class.Id);
        }

        var fallback = RoleGlossary.GetLocalizedRoleFamilyFallback(roleFamilyTag, CurrentLocaleCode);
        return Localize(ContentLocalizationTables.Roles, ContentLocalizationTables.BuildRoleNameKey(roleFamilyTag), fallback);
    }

    public static string GetAnchorName(DeploymentAnchorId anchor)
    {
        return Localize(GameLocalizationTables.UICommon, anchor.ToLocalizationKey(), anchor.ToDisplayName());
    }

    private static IEnumerable<string> EnumerateLocaleFallbacks()
    {
        var current = CurrentLocaleCode;
        if (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
        }

        if (!string.Equals(current, "ko", System.StringComparison.OrdinalIgnoreCase))
        {
            yield return "ko";
        }

        if (!string.Equals(current, "en", System.StringComparison.OrdinalIgnoreCase))
        {
            yield return "en";
        }
    }

    private static bool IsKoreanLocale()
    {
        return string.Equals(CurrentLocaleCode, "ko", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCurrentLocaleCode()
    {
        EnsureEditorLocalizationInitialized();
        return LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
    }

    private static void EnsureEditorLocalizationInitialized()
    {
        if (_editorLocalizationInitialized)
        {
            return;
        }

        try
        {
            LocalizationFoundationBootstrap.EnsureFoundationAssets();

            var initialization = LocalizationSettings.InitializationOperation;
            if (initialization.IsValid() && !initialization.IsDone)
            {
                initialization.WaitForCompletion();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[EditorLocalization] Failed to initialize editor localization. Falling back to English. {ex.Message}");
        }
        finally
        {
            _editorLocalizationInitialized = true;
        }
    }
}
