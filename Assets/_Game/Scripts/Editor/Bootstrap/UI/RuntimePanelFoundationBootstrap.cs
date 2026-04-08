using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using SM.Unity.UI;

namespace SM.Editor.Bootstrap.UI;

public static class RuntimePanelFoundationBootstrap
{
    private const string FoundationRoot = "Assets/_Game/UI/Foundation";
    private const string PanelSettingsRoot = "Assets/_Game/UI/Foundation/PanelSettings";

    [MenuItem("SM/Internal/Recovery/Ensure Runtime Panel Foundation")]
    public static void EnsureFoundationAssets()
    {
        EnsureFolder("Assets/_Game");
        EnsureFolder("Assets/_Game/UI");
        EnsureFolder(FoundationRoot);
        EnsureFolder(PanelSettingsRoot);
        EnsureSharedPanelSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureSharedPanelSettings()
    {
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimePanelAssetRegistry.SharedPanelSettingsPath);
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "FirstPlayableRuntimePanelSettings";
            AssetDatabase.CreateAsset(panelSettings, RuntimePanelAssetRegistry.SharedPanelSettingsPath);
        }

        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.clearColor = false;
        EditorUtility.SetDirty(panelSettings);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }
}
