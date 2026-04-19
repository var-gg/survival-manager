using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class CombatLocalizationContractBatchOnlyTests
{
    [Test]
    public void LocalizationController_Uses_Fallback_Instead_Of_Raw_Missing_Key()
    {
        var go = new GameObject("LocalizationControllerTest");
        try
        {
            var controller = go.AddComponent<GameLocalizationController>();
            var localized = controller.LocalizeOrFallback(GameLocalizationTables.UIBattle, "ui.battle.missing_test", "Fallback Label");

            Assert.That(localized, Is.EqualTo("Fallback Label"));
            Assert.That(localized.Contains("ui.battle.missing_test"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void UiBattle_StringTable_Covers_Runtime_Keys()
    {
        var scriptRoot = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity");
        var sharedDataPath = Path.Combine("Assets", "Localization", "StringTables", "UI_Battle Shared Data.asset");
        var sharedData = File.ReadAllText(sharedDataPath);
        var keys = Directory.EnumerateFiles(scriptRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "ui\\.battle\\.[a-z0-9._]+").Cast<Match>())
            .Select(match => match.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        Assert.That(keys, Is.Not.Empty);

        var missing = keys
            .Where(key => !sharedData.Contains($"Key: {key}"))
            .ToList();

        Assert.That(missing, Is.Empty, $"Missing UI_Battle localization keys: {string.Join(", ", missing)}");
    }
}
