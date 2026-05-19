using System;
using System.IO;
using NUnit.Framework;
using SM.Unity.UI.Panels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class Phase2PanelShowcaseTests
{
    private const string ScenePath = "Assets/_Game/UI/Panels/Scenes/Phase2PanelShowcase.unity";

    private static readonly PanelSpec[] Panels =
    {
        new("skill_compendium_skills", "Assets/_Game/UI/Panels/SkillCompendium/SkillCompendium.uxml"),
        new("tactical_workshop", "Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uxml"),
        new("recruit_pack", "Assets/_Game/UI/Panels/RecruitPack/RecruitPack.uxml"),
        new("equipment_refit", "Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uxml"),
        new("inventory_tab", "Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uxml"),
        new("town_roster_grid", "Assets/_Game/UI/Panels/TownRosterGrid/TownRosterGrid.uxml"),
        new("town_squad_builder", "Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uxml"),
        new("permanent_augment", "Assets/_Game/UI/Panels/PermanentAugment/PermanentAugment.uxml"),
        new("passive_board", "Assets/_Game/UI/Panels/PassiveBoard/PassiveBoard.uxml"),
        new("settings_global", "Assets/_Game/UI/Panels/SettingsGlobal/SettingsGlobal.uxml"),
    };

    [Test]
    public void Phase2PanelShowcaseSceneContainsSwitchablePanelHost()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);

        var scene = EditorSceneManager.OpenScene(ScenePath);
        Assert.That(scene.IsValid(), Is.True);

        var hostGo = FindSceneObject(scene, "Phase2PanelShowcase");
        Assert.That(hostGo, Is.Not.Null);
        Assert.That(hostGo!.activeSelf, Is.True);

        var document = hostGo.GetComponent<UIDocument>();
        Assert.That(document, Is.Not.Null);
        Assert.That(document.panelSettings, Is.Not.Null);
        Assert.That(document.visualTreeAsset, Is.EqualTo(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Panels[0].UxmlPath)));

        var switcher = hostGo.GetComponent<Phase2PanelRuntimeShowcaseController>();
        if (switcher == null)
        {
            AssertSerializedShowcaseFallback();
            return;
        }

        var serializedSwitcher = new SerializedObject(switcher);
        var panels = serializedSwitcher.FindProperty("panels");
        Assert.That(panels.arraySize, Is.EqualTo(Panels.Length));
        for (var i = 0; i < Panels.Length; i++)
        {
            var panel = panels.GetArrayElementAtIndex(i);
            Assert.That(panel.FindPropertyRelative("Id").stringValue, Is.EqualTo(Panels[i].Id));
            Assert.That(
                panel.FindPropertyRelative("VisualTreeAsset").objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Panels[i].UxmlPath)));
        }
    }

    private static void AssertSerializedShowcaseFallback()
    {
        var text = File.ReadAllText(ScenePath);
        var switcherGuid = AssetDatabase.AssetPathToGUID("Assets/_Game/Scripts/Runtime/Unity/UI/Panels/Phase2PanelRuntimeShowcaseController.cs");
        Assert.That(text, Does.Contain($"m_Script: {{fileID: 11500000, guid: {switcherGuid}, type: 3}}"));
        Assert.That(text.Split("- Id: ").Length - 1, Is.EqualTo(Panels.Length));

        foreach (var panel in Panels)
        {
            Assert.That(text, Does.Contain($"- Id: {panel.Id}"));
            var uxmlGuid = AssetDatabase.AssetPathToGUID(panel.UxmlPath);
            Assert.That(text, Does.Contain($"guid: {uxmlGuid}"));
        }
    }

    private readonly struct PanelSpec
    {
        public PanelSpec(string id, string uxmlPath)
        {
            Id = id;
            UxmlPath = uxmlPath;
        }

        public string Id { get; }
        public string UxmlPath { get; }
    }

    private static GameObject? FindSceneObject(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene == scene && string.Equals(go.name, objectName, StringComparison.Ordinal))
            {
                return go;
            }
        }

        return null;
    }
}
