using System;
using System.IO;
using SM.Unity.UI;
using SM.Unity.UI.Panels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

public static class Phase2PanelShowcaseBootstrap
{
    private const string ScenePath = "Assets/_Game/UI/Panels/Scenes/Phase2PanelShowcase.unity";
    private const string MenuPath = "SM/Internal/UI/Rebuild Phase 2 Panel Showcase";

    private static readonly PanelSpec[] Panels =
    {
        new("skill_compendium_skills", "Assets/_Game/UI/Panels/SkillCompendium/SkillCompendium.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.SkillCompendium),
        new("tactical_workshop", "Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.TacticalWorkshop),
        new("recruit_pack", "Assets/_Game/UI/Panels/RecruitPack/RecruitPack.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.RecruitPack),
        new("equipment_refit", "Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.EquipmentRefit),
        new("inventory_tab", "Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.InventoryTab),
    };

    [MenuItem(MenuPath)]
    public static void RebuildShowcaseSceneMenu()
    {
        RebuildShowcaseScene();
    }

    public static void RebuildShowcaseScene()
    {
        EnsureFolderPath(Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureCamera();

        var panelSettings = RuntimePanelAssetRegistry.LoadSharedPanelSettings();
        if (panelSettings == null)
        {
            throw new InvalidOperationException($"PanelSettings not found: {RuntimePanelAssetRegistry.SharedPanelSettingsPath}");
        }

        var hostGo = new GameObject("Phase2PanelShowcase");
        var document = hostGo.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;

        var entries = new Phase2PanelRuntimeShowcaseController.PanelEntry[Panels.Length];
        for (var i = 0; i < Panels.Length; i++)
        {
            entries[i] = CreatePanelEntry(Panels[i]);
        }

        if (entries.Length > 0)
        {
            document.visualTreeAsset = entries[0].VisualTreeAsset;
        }

        var switcher = hostGo.AddComponent<Phase2PanelRuntimeShowcaseController>();
        switcher.Configure(document, panelSettings, entries);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Phase2PanelShowcase] Scene rebuilt with {Panels.Length} panel hosts. Use 1-5 or arrow keys in Play Mode.");
    }

    private static Phase2PanelRuntimeShowcaseController.PanelEntry CreatePanelEntry(PanelSpec panel)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(panel.UxmlPath);
        if (visualTree == null)
        {
            throw new FileNotFoundException($"UXML not found or not imported: {panel.UxmlPath}");
        }

        return new Phase2PanelRuntimeShowcaseController.PanelEntry
        {
            Id = panel.Id,
            Kind = panel.Kind,
            VisualTreeAsset = visualTree,
        };
    }

    private static void EnsureCamera()
    {
        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.047f, 0.066f, 0.149f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 50f;
    }

    private static void EnsureFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolderPath(parent);
        }

        var folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(folderName) && !AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private readonly struct PanelSpec
    {
        public PanelSpec(string id, string uxmlPath, Phase2PanelRuntimeShowcaseController.PanelKind kind)
        {
            Id = id;
            UxmlPath = uxmlPath;
            Kind = kind;
        }

        public string Id { get; }
        public string UxmlPath { get; }
        public Phase2PanelRuntimeShowcaseController.PanelKind Kind { get; }
    }
}
