using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class UiLocalizationAuditTests
{
    private static readonly string[] ScreenUxmlPaths =
    {
        Path.Combine("Assets", "_Game", "UI", "Screens", "Town", "TownScreen.uxml"),
        Path.Combine("Assets", "_Game", "UI", "Screens", "Expedition", "ExpeditionScreen.uxml"),
        Path.Combine("Assets", "_Game", "UI", "Screens", "Battle", "BattleScreen.uxml"),
        Path.Combine("Assets", "_Game", "UI", "Screens", "Reward", "RewardScreen.uxml"),
    };

    private static readonly string[] RuntimeUiFiles =
    {
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "BootScreenController.cs"),
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "BattleScreenController.cs"),
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "UI", "Town", "TownScreenPresenter.cs"),
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "UI", "Town", "TownCharacterSheetFormatter.cs"),
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "UI", "Expedition", "ExpeditionScreenPresenter.cs"),
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "UI", "Battle", "BattleScreenPresenter.cs"),
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "UI", "Battle", "BattleUnitMetadataFormatter.cs"),
        Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "UI", "Reward", "RewardScreenPresenter.cs"),
    };

    [Test]
    public void ScreenUxml_DoesNotContainVisibleStaticTextLiterals()
    {
        var failures = new List<string>();
        foreach (var path in ScreenUxmlPaths)
        {
            var matches = Regex.Matches(File.ReadAllText(path), "text=\"[^\"]+\"");
            if (matches.Count > 0)
            {
                failures.Add($"{path}: {string.Join(", ", matches.Select(match => match.Value))}");
            }
        }

        Assert.That(failures, Is.Empty, $"Visible static UXML literals remain: {string.Join(" | ", failures)}");
    }

    [Test]
    public void SharedStringTables_Cover_RuntimeUiKeys()
    {
        var keyPattern = new Regex("ui\\.(common|town|expedition|battle|reward)\\.[a-z0-9._]+");
        var keys = RuntimeUiFiles
            .SelectMany(path => keyPattern.Matches(File.ReadAllText(path)).Cast<Match>())
            .Select(match => match.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        Assert.That(keys, Is.Not.Empty);

        var tableText = new Dictionary<string, string>
        {
            ["ui.common."] = File.ReadAllText(Path.Combine("Assets", "Localization", "StringTables", "UI_Common Shared Data.asset")),
            ["ui.town."] = File.ReadAllText(Path.Combine("Assets", "Localization", "StringTables", "UI_Town Shared Data.asset")),
            ["ui.expedition."] = File.ReadAllText(Path.Combine("Assets", "Localization", "StringTables", "UI_Expedition Shared Data.asset")),
            ["ui.battle."] = File.ReadAllText(Path.Combine("Assets", "Localization", "StringTables", "UI_Battle Shared Data.asset")),
            ["ui.reward."] = File.ReadAllText(Path.Combine("Assets", "Localization", "StringTables", "UI_Reward Shared Data.asset")),
        };

        var missing = keys
            .Where(key => tableText.First(entry => key.StartsWith(entry.Key)).Value.Contains($"m_Key: {key}") == false)
            .ToList();

        Assert.That(missing, Is.Empty, $"Missing shared UI localization keys: {string.Join(", ", missing)}");
    }
}
