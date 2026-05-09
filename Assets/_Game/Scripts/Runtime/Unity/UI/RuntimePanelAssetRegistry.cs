using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity.UI;

public readonly struct RuntimePanelScreenDescriptor
{
    public RuntimePanelScreenDescriptor(
        string hostObjectName,
        string visualTreePath,
        string styleSheetPath,
        int sortingOrder)
    {
        HostObjectName = hostObjectName;
        VisualTreePath = visualTreePath;
        StyleSheetPath = styleSheetPath;
        SortingOrder = sortingOrder;
    }

    public string HostObjectName { get; }
    public string VisualTreePath { get; }
    public string StyleSheetPath { get; }
    public int SortingOrder { get; }
}

public static class RuntimePanelAssetRegistry
{
    public const string SharedPanelSettingsPath = "Assets/_Game/UI/Foundation/PanelSettings/FirstPlayableRuntimePanelSettings.asset";
    public const string ThemeTokensStylePath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    public const string RuntimeThemeStylePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    public static bool TryGetScreenDescriptor(string sceneName, out RuntimePanelScreenDescriptor descriptor)
    {
        switch (sceneName)
        {
            case SceneNames.Town:
                descriptor = new RuntimePanelScreenDescriptor(
                    "TownRuntimePanelHost",
                    "Assets/_Game/UI/Screens/Town/TownScreen.uxml",
                    "Assets/_Game/UI/Screens/Town/TownScreen.uss",
                    0);
                return true;

            case SceneNames.Expedition:
                descriptor = new RuntimePanelScreenDescriptor(
                    "ExpeditionRuntimePanelHost",
                    "Assets/_Game/UI/Screens/Expedition/ExpeditionScreen.uxml",
                    "Assets/_Game/UI/Screens/Expedition/ExpeditionScreen.uss",
                    0);
                return true;

            case SceneNames.Atlas:
                descriptor = new RuntimePanelScreenDescriptor(
                    "AtlasRuntimePanelHost",
                    "Assets/_Game/UI/Screens/Atlas/AtlasScreen.uxml",
                    "Assets/_Game/UI/Screens/Atlas/AtlasScreen.uss",
                    0);
                return true;

            case SceneNames.Battle:
                descriptor = new RuntimePanelScreenDescriptor(
                    "BattleRuntimePanelHost",
                    "Assets/_Game/UI/Screens/Battle/BattleScreen.uxml",
                    "Assets/_Game/UI/Screens/Battle/BattleScreen.uss",
                    12);
                return true;

            case SceneNames.Reward:
                descriptor = new RuntimePanelScreenDescriptor(
                    "RewardRuntimePanelHost",
                    "Assets/_Game/UI/Screens/Reward/RewardScreen.uxml",
                    "Assets/_Game/UI/Screens/Reward/RewardScreen.uss",
                    0);
                return true;

            default:
                descriptor = default;
                return false;
        }
    }

#if UNITY_EDITOR
    public static void ConfigureHost(RuntimePanelHost host, string sceneName)
    {
        if (host == null || !TryGetScreenDescriptor(sceneName, out var descriptor))
        {
            return;
        }

        host.Configure(
            LoadSharedPanelSettings(),
            LoadVisualTree(descriptor.VisualTreePath),
            new[]
            {
                LoadStyleSheet(ThemeTokensStylePath),
                LoadStyleSheet(RuntimeThemeStylePath),
                LoadStyleSheet(descriptor.StyleSheetPath)
            },
            descriptor.SortingOrder,
            descriptor.HostObjectName);
    }

    public static PanelSettings? LoadSharedPanelSettings()
    {
        return AssetDatabase.LoadAssetAtPath<PanelSettings>(SharedPanelSettingsPath);
    }

    public static VisualTreeAsset? LoadVisualTree(string path)
    {
        return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
    }

    public static StyleSheet? LoadStyleSheet(string path)
    {
        return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
    }
#endif
}
