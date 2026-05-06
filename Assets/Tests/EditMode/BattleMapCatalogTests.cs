using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleMapCatalogTests
{
    [Test]
    public void SelectMap_UsesChapterPoolBeforeDefault()
    {
        var catalog = ScriptableObject.CreateInstance<BattleMapCatalog>();
        var defaultPrefab = new GameObject("DefaultMapPrefab");
        var chapterPrefab = new GameObject("ChapterMapPrefab");

        try
        {
            catalog.SetDefaultMapId("map_default");
            catalog.SetMap("map_default", "Default", defaultPrefab, Vector3.zero, Vector3.zero, Vector3.one, BattleMapTacticalOverlayMode.FullArena);
            catalog.SetMap("map_chapter", "Chapter", chapterPrefab, Vector3.zero, Vector3.zero, Vector3.one, BattleMapTacticalOverlayMode.ReadabilityOnly);
            catalog.SetChapterPool("chapter_test", new[] { "map_chapter" });

            Assert.That(catalog.TrySelectMap(new BattleMapSelectionContext("chapter_test", "site_a", "encounter_a", 7), out var selected), Is.True);
            Assert.That(selected.MapId, Is.EqualTo("map_chapter"));
            Assert.That(selected.TacticalOverlayMode, Is.EqualTo(BattleMapTacticalOverlayMode.ReadabilityOnly));

            Assert.That(catalog.TrySelectMap(new BattleMapSelectionContext("chapter_other", "site_b", "encounter_b", 7), out var fallback), Is.True);
            Assert.That(fallback.MapId, Is.EqualTo("map_default"));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(defaultPrefab);
            Object.DestroyImmediate(chapterPrefab);
        }
    }

    [Test]
    public void Presentation_InstantiatesSelectedMapUnderStageRoot()
    {
        var cameraGo = new GameObject("MainCamera");
        var stageGo = new GameObject("StageRoot");
        var overlayGo = new GameObject("OverlayRoot", typeof(RectTransform));
        var controllerGo = new GameObject("BattlePresentationController");
        var catalog = ScriptableObject.CreateInstance<BattleMapCatalog>();
        var prefab = new GameObject("MapPrefab");

        try
        {
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();

            catalog.SetDefaultMapId("map_test");
            catalog.SetMap("map_test", "Map Test", prefab, new Vector3(1f, 2f, 3f), new Vector3(0f, 45f, 0f), Vector3.one * 2f, BattleMapTacticalOverlayMode.ReadabilityOnly);
            catalog.SetChapterPool("chapter_test", new[] { "map_test" });

            var controller = controllerGo.AddComponent<BattlePresentationController>();
            SetField(controller, "battleStageRoot", stageGo.transform);
            SetField(controller, "actorOverlayRoot", overlayGo.GetComponent<RectTransform>());
            controller.ConfigureBattleMapCatalog(catalog);

            controller.Initialize(
                CreateEmptyStep(),
                new BattleMapSelectionContext("chapter_test", "site_test", "encounter_test", 11));

            var map = stageGo.transform.Find("BattleMap_map_test");

            Assert.That(controller.ActiveMapId, Is.EqualTo("map_test"));
            Assert.That(controller.ActiveMapDisplayName, Is.EqualTo("Map Test"));
            Assert.That(map, Is.Not.Null);
            Assert.That(map!.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(map.GetComponent<BattleMapMaterialAdapter>(), Is.Not.Null);
            Assert.That(stageGo.transform.Find("StageDecor"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(controllerGo);
            Object.DestroyImmediate(overlayGo);
            Object.DestroyImmediate(stageGo);
            Object.DestroyImmediate(cameraGo);
        }
    }

    [Test]
    public void MaterialAdapter_RemapsTriForgeMaterials()
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/TriForge Assets/Fantasy Worlds - Forest/Materials/Ruins/M_fwOF_Ruins_Arches_01.mat");
        Assume.That(source, Is.Not.Null, "Fantasy Worlds material is not imported in this checkout.");
        Assume.That(source!.shader.name, Does.StartWith("TriForge/"));

        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var targetRenderer = primitive.GetComponent<Renderer>();
            targetRenderer.sharedMaterial = source;

            var adapter = primitive.AddComponent<BattleMapMaterialAdapter>();
            adapter.Apply();

            Assert.That(targetRenderer.sharedMaterial, Is.Not.SameAs(source));
            Assert.That(targetRenderer.sharedMaterial.shader.name, Does.Not.StartWith("TriForge/"));
        }
        finally
        {
            Object.DestroyImmediate(primitive);
        }
    }

    [Test]
    public void ForestRuinsPrefab_HasProjectOwnedEnvironmentAndFloor()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/Battle/Maps/BattleMap_Forest_Ruins_01.prefab");
        Assume.That(prefab, Is.Not.Null, "Forest ruins battle map has not been generated in this checkout.");

        Assert.That(prefab!.transform.Find("PlayableFloor"), Is.Not.Null);
        Assert.That(prefab.transform.Find("WolfPineRoad"), Is.Not.Null);
        Assert.That(prefab.transform.Find("WolfPineTreeline/Pine_Left_Back_01"), Is.Not.Null);
    }

    private static BattleSimulationStep CreateEmptyStep()
    {
        return new BattleSimulationStep(
            0,
            0f,
            System.Array.Empty<BattleUnitReadModel>(),
            System.Array.Empty<BattleEvent>(),
            false,
            null);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field!.SetValue(target, value);
    }
}
