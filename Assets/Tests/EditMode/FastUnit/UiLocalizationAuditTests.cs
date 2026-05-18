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
        // UXML element에 name= attribute가 있으면 코드 (View.cs)가 setText로 덮어쓰는 "placeholder" 로 인정.
        // Editor scene view / UI Builder UX를 위해 한국어 fallback literal을 두는 게 자연스러움 —
        // Presenter는 runtime에 Localize key + fallback default로 setText 호출, UXML literal은 무시됨.
        // 추적 대상: nameless static literal — runtime에 갈아끼울 hook 없이 박힌 dead text.
        var failures = new List<string>();
        var elementPattern = new Regex(@"<ui:[A-Za-z]+\b[^/>]*/>", RegexOptions.Singleline);
        var textPattern = new Regex(@"\btext=""[^""]+""");
        var namePattern = new Regex(@"\bname=""[^""]+""");
        foreach (var path in ScreenUxmlPaths)
        {
            var uxml = File.ReadAllText(path);
            var nakedLiterals = new List<string>();
            foreach (Match element in elementPattern.Matches(uxml))
            {
                if (!textPattern.IsMatch(element.Value)) continue;
                if (namePattern.IsMatch(element.Value)) continue;   // placeholder: code setText로 덮어씀
                nakedLiterals.Add(textPattern.Match(element.Value).Value);
            }
            if (nakedLiterals.Count > 0)
            {
                failures.Add($"{path}: {string.Join(", ", nakedLiterals)}");
            }
        }

        Assert.That(failures, Is.Empty, $"Visible static UXML literals remain (nameless, no setText hook): {string.Join(" | ", failures)}");
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
