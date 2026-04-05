using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.UnityCliTools;

[UnityCliTool(
    Name = "observer_contract_report",
    Description = "Reads Town/Battle observer scene contracts from repo-tracked scene YAML and returns a compact JSON report.",
    Group = "report")]
public static class ObserverContractReport
{
    private const string TownScenePath = "Assets/_Game/Scenes/Town.unity";
    private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
    private const string TownScreenUxmlPath = "Assets/_Game/UI/Screens/Town/TownScreen.uxml";
    private const string BattleScreenUxmlPath = "Assets/_Game/UI/Screens/Battle/BattleScreen.uxml";

    public sealed class Parameters
    {
        [ToolParameter("Scene contract to report: town, battle, or all. Default: all.")]
        public string Scene { get; set; } = "all";
    }

    public static object HandleCommand(JObject parameters)
    {
        var toolParams = new ToolParams(parameters);
        var scene = (toolParams.Get("scene", "all") ?? "all").Trim().ToLowerInvariant();

        var data = scene switch
        {
            "town" => BuildTownReport(),
            "battle" => BuildBattleReport(),
            "all" => new
            {
                town = BuildTownReport(),
                battle = BuildBattleReport(),
                consoleErrors = GetConsoleSummary(),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, "scene must be one of: town, battle, all"),
        };

        return new SuccessResponse("Observer contract report generated.", data);
    }

    private static object BuildTownReport()
    {
        var sceneText = ReadSceneText(TownScenePath);
        var uxmlText = ReadSceneText(TownScreenUxmlPath);
        return new
        {
            scene = "Town",
            scenePath = TownScenePath,
            exists = File.Exists(GetAbsolutePath(TownScenePath)),
            controllers = new
            {
                TownScreenController = ContainsAny(sceneText, "SM.Unity.TownScreenController", "TownScreenController, SM.Unity"),
                RuntimePanelHost = ContainsAny(sceneText, "SM.Unity.UI.RuntimePanelHost", "RuntimePanelHost, SM.Unity"),
            },
            TownRuntimeRoot = ContainsName(sceneText, "TownRuntimeRoot"),
            TownRuntimePanelHost = ContainsName(sceneText, "TownRuntimePanelHost"),
            UxmlQuickBattleButton = uxmlText.Contains("QuickBattleButton", StringComparison.Ordinal),
            UxmlDeployButtonFrontTop = uxmlText.Contains("DeployButton_FrontTop", StringComparison.Ordinal),
            UxmlStatusLabel = uxmlText.Contains("StatusLabel", StringComparison.Ordinal),
            consoleErrors = GetConsoleSummary(),
        };
    }

    private static object BuildBattleReport()
    {
        var sceneText = ReadSceneText(BattleScenePath);
        var uxmlText = ReadSceneText(BattleScreenUxmlPath);
        return new
        {
            scene = "Battle",
            scenePath = BattleScenePath,
            exists = File.Exists(GetAbsolutePath(BattleScenePath)),
            controllers = new
            {
                BattleScreenController = ContainsAny(sceneText, "SM.Unity.BattleScreenController", "BattleScreenController, SM.Unity"),
                BattlePresentationController = ContainsAny(sceneText, "SM.Unity.BattlePresentationController", "BattlePresentationController, SM.Unity"),
                RuntimePanelHost = ContainsAny(sceneText, "SM.Unity.UI.RuntimePanelHost", "RuntimePanelHost, SM.Unity"),
            },
            BattleRuntimeRoot = ContainsName(sceneText, "BattleRuntimeRoot"),
            BattleRuntimePanelHost = ContainsName(sceneText, "BattleRuntimePanelHost"),
            BattlePresentationRoot = ContainsName(sceneText, "BattlePresentationRoot"),
            BattleStageRoot = ContainsName(sceneText, "BattleStageRoot"),
            ActorOverlayCanvas = ContainsName(sceneText, "ActorOverlayCanvas"),
            ActorOverlayRoot = ContainsName(sceneText, "ActorOverlayRoot"),
            UxmlPauseButton = uxmlText.Contains("PauseButton", StringComparison.Ordinal),
            UxmlSettingsButton = uxmlText.Contains("SettingsButton", StringComparison.Ordinal),
            UxmlSettingsPanel = uxmlText.Contains("SettingsPanel", StringComparison.Ordinal),
            UxmlProgressFill = uxmlText.Contains("ProgressFill", StringComparison.Ordinal),
            consoleErrors = GetConsoleSummary(),
        };
    }

    private static string ReadSceneText(string scenePath)
    {
        var absolutePath = GetAbsolutePath(scenePath);
        return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
    }

    private static string GetAbsolutePath(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve project root.");
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.IndexOf(needle, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsName(string text, string objectName)
    {
        return text.IndexOf($"m_Name: {objectName}", StringComparison.Ordinal) >= 0;
    }

    private static ConsoleSummary GetConsoleSummary()
    {
        try
        {
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            if (logEntriesType == null)
            {
                return ConsoleSummary.Unavailable("UnityEditor.LogEntries type not found.");
            }

            var getCountsByType = logEntriesType.GetMethod(
                "GetCountsByType",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (getCountsByType == null)
            {
                return ConsoleSummary.Unavailable("GetCountsByType API not found.");
            }

            var args = new object[] { 0, 0, 0 };
            getCountsByType.Invoke(null, args);

            return new ConsoleSummary
            {
                available = true,
                errors = ToInt(args[0]),
                warnings = ToInt(args[1]),
                logs = ToInt(args[2]),
                note = string.Empty,
            };
        }
        catch (Exception ex)
        {
            return ConsoleSummary.Unavailable(ex.Message);
        }
    }

    private static int ToInt(object? value)
    {
        return value switch
        {
            int intValue => intValue,
            _ => 0,
        };
    }

    private sealed class ConsoleSummary
    {
        public bool available { get; init; }

        public int errors { get; init; }

        public int warnings { get; init; }

        public int logs { get; init; }

        public string note { get; init; } = string.Empty;

        public static ConsoleSummary Unavailable(string note)
        {
            return new ConsoleSummary
            {
                available = false,
                errors = 0,
                warnings = 0,
                logs = 0,
                note = note,
            };
        }
    }
}
