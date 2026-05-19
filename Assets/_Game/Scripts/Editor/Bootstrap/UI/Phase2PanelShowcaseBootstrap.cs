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
        new("town_roster_grid", "Assets/_Game/UI/Panels/TownRosterGrid/TownRosterGrid.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.TownRosterGrid),
        new("town_squad_builder", "Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.TownSquadBuilder),
        new("permanent_augment", "Assets/_Game/UI/Panels/PermanentAugment/PermanentAugment.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.PermanentAugment),
        new("passive_board", "Assets/_Game/UI/Panels/PassiveBoard/PassiveBoard.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.PassiveBoard),
        new("settings_global", "Assets/_Game/UI/Panels/SettingsGlobal/SettingsGlobal.uxml", Phase2PanelRuntimeShowcaseController.PanelKind.SettingsGlobal),
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
        RepairShowcaseControllerScriptReference();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Phase2PanelShowcase] Scene rebuilt with {Panels.Length} panel hosts. Use 1-9, 0 or arrow keys in Play Mode.");
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

    private static void RepairShowcaseControllerScriptReference()
    {
        if (!File.Exists(ScenePath))
        {
            return;
        }

        var scriptGuid = AssetDatabase.AssetPathToGUID("Assets/_Game/Scripts/Runtime/Unity/UI/Panels/Phase2PanelRuntimeShowcaseController.cs");
        if (string.IsNullOrWhiteSpace(scriptGuid))
        {
            throw new InvalidOperationException("Phase2PanelRuntimeShowcaseController script guid not found.");
        }

        var text = File.ReadAllText(ScenePath);
        var original = text;
        const string identifier = "SM.Unity::SM.Unity.UI.Panels.Phase2PanelRuntimeShowcaseController";
        const string pattern = @"(?m)m_Script:\s*\{fileID:\s*\d+\}(?<tail>\r?\n\s*m_Name:\s*\r?\n\s*m_EditorClassIdentifier:\s*SM\.Unity::SM\.Unity\.UI\.Panels\.Phase2PanelRuntimeShowcaseController)";
        var scriptReference = $"m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}";
        text = System.Text.RegularExpressions.Regex.Replace(text, pattern, $"{scriptReference}${{tail}}");
        if (text == original)
        {
            Debug.LogWarning($"[Phase2PanelShowcase] Script reference repair made no changes for {identifier}.");
            return;
        }

        File.WriteAllText(ScenePath, text, new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
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
