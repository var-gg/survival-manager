using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Unity;
using SM.Unity.Atlas;
using SM.Unity.UI;
using SM.Unity.UI.Atlas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor.Authoring.Atlas;

public static class AtlasGrayboxAuthoringAssetUtility
{
    private const string AtlasScenePath = "Assets/_Game/Scenes/Atlas.unity";
    private const string AtlasPrefabPath = "Assets/_Game/Prefabs/Atlas/AtlasRegionWolfpineTrail.prefab";
    private const string AtlasMaterialFolder = "Assets/_Game/Materials/Atlas";
    private const string AtlasMeshFolder = "Assets/_Game/Meshes/Atlas";
    private const string BattleForestMapPrefabPath = "Assets/_Game/Prefabs/Battle/Maps/BattleMap_Forest_Ruins_01.prefab";

    [MenuItem("SM/Internal/Recovery/Rebuild Atlas Graybox Scene")]
    public static void RebuildAtlasSceneMenu()
    {
        RebuildAtlasScene();
    }

    public static void EnsureAtlasScene()
    {
        if (!File.Exists(AtlasScenePath) || !File.Exists(AtlasPrefabPath))
        {
            RebuildAtlasScene();
        }
    }

    public static void RebuildAtlasScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureRootObject("SceneMarker_Atlas");
        var camera = EnsureCamera();
        EnsureGoldenHourLighting();
        var environment = InstantiateAtlasEnvironmentPrefab();
        UiEventSystemConfigurator.EnsureSceneEventSystem(scene);
        var screenController = EnsureRuntimeRoot();
        ConfigureSceneController(environment, camera, screenController);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, AtlasScenePath);
        PatchSerializedScriptReferences(
            AtlasScenePath,
            ("SM.Unity::SM.Unity.UI.Atlas.AtlasScreenController", "Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/AtlasScreenController.cs"),
            ("SM.Unity::SM.Unity.Atlas.Atlas3DSceneController", "Assets/_Game/Scripts/Runtime/Unity/Atlas/Atlas3DSceneController.cs"));
        PatchSerializedScriptReferences(
            AtlasPrefabPath,
            ("SM.Unity::SM.Unity.Atlas.AtlasCharacterStandeePresenter", "Assets/_Game/Scripts/Runtime/Unity/Atlas/AtlasCharacterStandeePresenter.cs"));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AtlasGraybox] Atlas 3D scene rebuilt.");
    }

    private static AtlasScreenController EnsureRuntimeRoot()
    {
        var root = EnsureRootObject("AtlasRuntimeRoot");
        var hostGo = CreateChild(root.transform, "AtlasRuntimePanelHost");
        var host = EnsureComponent<RuntimePanelHost>(hostGo);
        RuntimePanelAssetRegistry.ConfigureHost(host, SceneNames.Atlas);

        var controllerGo = CreateChild(root.transform, "AtlasScreenController");
        var controller = EnsureComponent<AtlasScreenController>(controllerGo);
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("panelHost").objectReferenceValue = host;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return controller;
    }

    private static Camera EnsureCamera()
    {
        var go = EnsureRootObject("Main Camera");
        var camera = EnsureComponent<Camera>(go);
        go.tag = "MainCamera";
        go.transform.position = new Vector3(-2.80f, 7.40f, -4.90f);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(2.80f, -7.00f, 4.90f).normalized, Vector3.up);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.026f, 0.032f, 0.040f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 4.85f;
        camera.fieldOfView = 35f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 160f;
        EnsureOptionalCinemachineRig(go);
        return camera;
    }

    private static GameObject InstantiateAtlasEnvironmentPrefab()
    {
        var prefab = RebuildAtlasEnvironmentPrefab();
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("Atlas environment prefab instance could not be created.");
        }

        instance.name = "AtlasRegionWolfpineTrail";
        return instance;
    }

    private static GameObject RebuildAtlasEnvironmentPrefab()
    {
        EnsureAssetFolder("Assets/_Game/Prefabs");
        EnsureAssetFolder("Assets/_Game/Prefabs/Atlas");
        EnsureAssetFolder("Assets/_Game/Materials");
        EnsureAssetFolder(AtlasMaterialFolder);
        EnsureAssetFolder("Assets/_Game/Meshes");
        EnsureAssetFolder(AtlasMeshFolder);

        var root = new GameObject("AtlasRegionWolfpineTrail");
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var hitDiscMesh = LoadOrCreateMesh("Mesh_Atlas_HexHitDisc", () => AtlasHexWorldMapper.CreateHexDiscMesh(AtlasHexWorldMapper.HexRadius * 0.98f));
        var auraRingMesh = LoadOrCreateMesh("Mesh_Atlas_AuraRing", () => AtlasHexWorldMapper.CreateHexRingMesh(AtlasHexWorldMapper.HexRadius * 1.06f, 0.026f));
        var leylineRingMesh = LoadOrCreateMesh("Mesh_Atlas_LeyLineRing", () => AtlasHexWorldMapper.CreateHexRingMesh(AtlasHexWorldMapper.HexRadius * 0.98f, 0.004f, 0.012f));
        CreateForestDiorama(root.transform);
        CreateHexHitboxes(root.transform, region, hitDiscMesh);

        var leyline = root.AddComponent<AtlasHexLeylineRenderer>();
        leyline.Rebuild(
            region,
            leylineRingMesh,
            kind => LoadOrCreateMaterial($"M_Atlas_LeyLine_{kind}", ResolveLeylineColor(kind), "SM/Atlas/HexLeyLine"));
        UnityEngine.Object.DestroyImmediate(leyline);

        var aura = root.AddComponent<AtlasSigilAuraVFXController>();
        aura.Render(
            new AtlasScreenPresenter(region).Build(),
            null,
            auraRingMesh,
            category => LoadOrCreateMaterial($"M_Atlas_Aura_{category}", AtlasSigilAuraVFXController.ResolveCategoryColor(category), "SM/Atlas/SigilAuraRing"),
            LoadOrCreateMaterial("M_Atlas_Aura_OverlapAmberCoral", AtlasSigilAuraVFXController.ResolveOverlapColor(), "SM/Atlas/SigilAuraRing", pulseOffset: 1.35f));
        UnityEngine.Object.DestroyImmediate(aura);

        var standees = root.AddComponent<AtlasCharacterStandeePresenter>();
        standees.Rebuild(
            region,
            entry => LoadOrCreateMaterial($"M_Atlas_Standee_{entry.CharacterId}", entry.AccentColor, "lilToon"),
            LoadOrCreateMaterial("M_Atlas_StandeeBase", new Color(0.08f, 0.07f, 0.06f, 0.72f)));

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, AtlasPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Failed to save {AtlasPrefabPath}.");
        }

        return prefab;
    }

    private static void CreateForestDiorama(Transform root)
    {
        var terrainRoot = CreateChild(root, "WolfpineTrailTerrain");
        var ground = CreatePrimitiveRenderer(
            terrainRoot.transform,
            PrimitiveType.Cube,
            "MossPlateau",
            new Vector3(0f, -0.12f, 0f),
            new Vector3(8.8f, 0.18f, 5.8f),
            LoadOrCreateMaterial("M_Atlas_Wolfpine_Moss", new Color(0.14f, 0.24f, 0.13f, 1f)));
        ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        CreatePrimitiveRenderer(
            terrainRoot.transform,
            PrimitiveType.Cube,
            "CliffDrop_FogPlane",
            new Vector3(1.1f, -0.55f, 2.80f),
            new Vector3(8.2f, 0.10f, 1.6f),
            LoadOrCreateMaterial("M_Atlas_FoggedCliff", new Color(0.10f, 0.15f, 0.15f, 0.48f)));
        CreatePrimitiveRenderer(
            terrainRoot.transform,
            PrimitiveType.Cube,
            "WarmStream",
            new Vector3(-2.15f, -0.035f, -0.42f),
            new Vector3(0.12f, 0.018f, 5.2f),
            LoadOrCreateMaterial("M_Atlas_Stream", new Color(0.07f, 0.24f, 0.30f, 0.82f)));
        CreateRoutePathAndLandmarks(terrainRoot.transform);

        var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleForestMapPrefabPath);
        if (mapPrefab != null)
        {
            var map = PrefabUtility.InstantiatePrefab(mapPrefab, terrainRoot.transform) as GameObject;
            if (map != null)
            {
                map.name = "BattleMapForestRuinsBackdrop";
                map.transform.localPosition = new Vector3(0f, -0.18f, 4.45f);
                map.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
                map.transform.localScale = Vector3.one * 0.38f;
            }
        }

        CreateTrees(terrainRoot.transform);
        CreateRocksAndRuins(terrainRoot.transform);
    }

    private static void CreateRoutePathAndLandmarks(Transform parent)
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var pathMaterial = LoadOrCreateMaterial("M_Atlas_RouteGlow", new Color(1.00f, 0.70f, 0.22f, 0.34f), "SM/Atlas/HexLeyLine", pulseOffset: 0.42f);
        var beaconMaterial = LoadOrCreateMaterial("M_Atlas_NodeBeacon", new Color(1.00f, 0.82f, 0.34f, 0.48f), "SM/Atlas/HexLeyLine", pulseOffset: 0.70f);
        var bossMaterial = LoadOrCreateMaterial("M_Atlas_BossBeacon", new Color(1.00f, 0.28f, 0.25f, 0.40f), "SM/Atlas/HexLeyLine", pulseOffset: 1.10f);
        var extractMaterial = LoadOrCreateMaterial("M_Atlas_ExtractBeacon", new Color(0.30f, 0.86f, 0.74f, 0.38f), "SM/Atlas/HexLeyLine", pulseOffset: 1.00f);
        var nodes = region.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var path = region.StageCandidates
            .Where(candidate => nodes.ContainsKey(candidate.HexId))
            .GroupBy(candidate => candidate.SiteStageIndex)
            .OrderBy(group => group.Key)
            .Select(group => group.OrderBy(candidate => candidate.CandidateBadge, StringComparer.Ordinal).First())
            .Select(candidate => (Candidate: candidate, Node: nodes[candidate.HexId], World: AtlasHexWorldMapper.ToWorld(nodes[candidate.HexId].Hex)))
            .ToArray();

        for (var i = 1; i < path.Length; i++)
        {
            CreateRouteSegment(parent, path[i - 1].World, path[i].World, pathMaterial, $"RouteRibbon_{i:00}");
        }

        foreach (var entry in path)
        {
            var material = entry.Node.Kind switch
            {
                AtlasNodeKind.Boss => bossMaterial,
                AtlasNodeKind.Extract => extractMaterial,
                _ => beaconMaterial,
            };
            CreatePrimitiveRenderer(
                parent,
                PrimitiveType.Cylinder,
                $"RouteBeacon_{entry.Candidate.CandidateBadge}",
                entry.World + Vector3.up * 0.092f,
                new Vector3(0.115f, 0.007f, 0.115f),
                material);
        }
    }

    private static void CreateRouteSegment(Transform parent, Vector3 from, Vector3 to, Material material, string name)
    {
        var delta = to - from;
        delta.y = 0f;
        var length = delta.magnitude;
        if (length <= 0.001f)
        {
            return;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = from + delta * 0.5f + Vector3.up * 0.045f;
        go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        go.transform.localScale = new Vector3(0.026f, 0.006f, length);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
    }

    private static void CreateHexHitboxes(Transform root, AtlasRegionDefinition region, Mesh hitDiscMesh)
    {
        var hitboxRoot = CreateChild(root, "AtlasHexRaycastSurfaces");
        foreach (var node in region.Nodes)
        {
            var tile = new GameObject($"HexHit_{node.NodeId}");
            tile.transform.SetParent(hitboxRoot.transform, false);
            tile.transform.position = AtlasHexWorldMapper.ToWorld(node.Hex) + Vector3.up * 0.11f;
            tile.transform.localRotation = Quaternion.identity;

            var filter = tile.AddComponent<MeshFilter>();
            filter.sharedMesh = hitDiscMesh;

            var collider = tile.AddComponent<MeshCollider>();
            collider.sharedMesh = hitDiscMesh;
        }
    }

    private static void CreateTrees(Transform parent)
    {
        var placements = new[]
        {
            new Vector3(-4.05f, 0f, -2.45f),
            new Vector3(-4.05f, 0f, 2.45f),
            new Vector3(4.05f, 0f, -2.45f),
            new Vector3(4.05f, 0f, 2.45f),
        };
        var trunk = LoadOrCreateMaterial("M_Atlas_TreeTrunk", new Color(0.20f, 0.12f, 0.07f, 1f));
        var needle = LoadOrCreateMaterial("M_Atlas_PineNeedle", new Color(0.06f, 0.20f, 0.13f, 1f));
        for (var i = 0; i < placements.Length; i++)
        {
            var root = CreateChild(parent, $"WolfpineTree_{i:00}");
            root.transform.localPosition = placements[i];
            CreatePrimitiveRenderer(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.22f, 0f), new Vector3(0.063f, 0.224f, 0.063f), trunk);
            CreatePrimitiveRenderer(root.transform, PrimitiveType.Capsule, "Crown", new Vector3(0f, 0.62f, 0f), new Vector3(0.294f, 0.434f, 0.294f), needle);
        }
    }

    private static void CreateRocksAndRuins(Transform parent)
    {
        var rock = LoadOrCreateMaterial("M_Atlas_WarmRock", new Color(0.32f, 0.28f, 0.22f, 1f));
        var gold = LoadOrCreateMaterial("M_Atlas_SigilStoneGlow", new Color(0.92f, 0.62f, 0.22f, 0.54f), "SM/Atlas/HexLeyLine");
        foreach (var position in new[] { new Vector3(-1.8f, 0f, -1.6f), new Vector3(0.9f, 0f, 1.3f), new Vector3(2.5f, 0f, -0.8f) })
        {
            CreatePrimitiveRenderer(parent, PrimitiveType.Cube, $"MossRock_{position.x:0.0}_{position.z:0.0}", position + Vector3.up * 0.10f, new Vector3(0.42f, 0.22f, 0.34f), rock);
        }

        var region = AtlasGrayboxDataFactory.CreateRegion();
        foreach (var anchor in region.SigilAnchors)
        {
            var world = AtlasHexWorldMapper.ToWorld(anchor);
            CreatePrimitiveRenderer(parent, PrimitiveType.Cylinder, $"SigilPin_{anchor.Q}_{anchor.R}", world + Vector3.up * 0.25f, new Vector3(0.055f, 0.24f, 0.055f), gold);
            CreatePrimitiveRenderer(parent, PrimitiveType.Sphere, $"SigilPinGlimmer_{anchor.Q}_{anchor.R}", world + Vector3.up * 0.52f, new Vector3(0.085f, 0.085f, 0.085f), gold);
        }
    }

    private static void EnsureGoldenHourLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.24f, 0.20f, 0.16f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.16f, 0.18f, 0.16f, 1f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.014f;

        var lightGo = EnsureRootObject("GoldenHour Directional Light");
        var light = EnsureComponent<Light>(lightGo);
        light.type = LightType.Directional;
        light.color = new Color(1.00f, 0.72f, 0.45f, 1f);
        light.intensity = 1.28f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(42f, -34f, 0f);

        var postProcessHint = EnsureRootObject("GoldenHourPostProcessBaseline");
        postProcessHint.transform.position = Vector3.zero;
    }

    private static void ConfigureSceneController(GameObject environment, Camera camera, AtlasScreenController screenController)
    {
        var controller = EnsureComponent<Atlas3DSceneController>(environment);
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("worldCamera").objectReferenceValue = camera;
        serialized.FindProperty("screenController").objectReferenceValue = screenController;
        serialized.FindProperty("auraController").objectReferenceValue = environment.GetComponent<AtlasSigilAuraVFXController>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureOptionalCinemachineRig(GameObject cameraGo)
    {
        var brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine")
                        ?? Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
        if (brainType != null && cameraGo.GetComponent(brainType) == null)
        {
            cameraGo.AddComponent(brainType);
        }

        var rig = GameObject.Find("CinemachineIsometricCamera");
        if (rig == null)
        {
            rig = new GameObject("CinemachineIsometricCamera");
        }

        rig.transform.position = cameraGo.transform.position;
        rig.transform.rotation = cameraGo.transform.rotation;
        var cameraType = Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine")
                         ?? Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");
        if (cameraType != null && rig.GetComponent(cameraType) == null)
        {
            rig.AddComponent(cameraType);
        }
    }

    private static Renderer CreatePrimitiveRenderer(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        return renderer;
    }

    private static Material LoadOrCreateMaterial(
        string assetName,
        Color color,
        string shaderName = "Universal Render Pipeline/Lit",
        float pulseOffset = 0f)
    {
        var path = $"{AtlasMaterialFolder}/{assetName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader)
            {
                name = assetName,
                color = color,
            };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            var emissionMultiplier = string.Equals(shaderName, "Universal Render Pipeline/Lit", StringComparison.Ordinal)
                ? 0.16f
                : 1.15f;
            material.SetColor("_EmissionColor", color * emissionMultiplier);
        }

        if (material.HasProperty("_Tint"))
        {
            material.SetColor("_Tint", color);
        }

        if (material.HasProperty("_RingColor"))
        {
            material.SetColor("_RingColor", color);
        }

        if (material.HasProperty("_PulseOffset"))
        {
            material.SetFloat("_PulseOffset", pulseOffset);
        }

        return material;
    }

    private static Color ResolveLeylineColor(AtlasNodeKind kind)
    {
        return kind switch
        {
            AtlasNodeKind.Boss => new Color(1.00f, 0.38f, 0.32f, 0.11f),
            AtlasNodeKind.Elite => new Color(0.94f, 0.58f, 0.38f, 0.09f),
            AtlasNodeKind.SigilAnchor => new Color(0.50f, 0.90f, 0.96f, 0.10f),
            AtlasNodeKind.Reward => new Color(1.00f, 0.76f, 0.28f, 0.09f),
            AtlasNodeKind.Event => new Color(0.56f, 0.70f, 1.00f, 0.08f),
            _ => new Color(1.00f, 0.88f, 0.54f, 0.045f),
        };
    }

    private static Mesh LoadOrCreateMesh(string assetName, Func<Mesh> factory)
    {
        var path = $"{AtlasMeshFolder}/{assetName}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null)
        {
            var refreshed = factory();
            mesh.Clear();
            mesh.vertices = refreshed.vertices;
            mesh.triangles = refreshed.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            UnityEngine.Object.DestroyImmediate(refreshed);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        mesh = factory();
        mesh.name = assetName;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static void PatchSerializedScriptReferences(string assetPath, params (string EditorClassIdentifier, string ScriptPath)[] fixups)
    {
        var lines = File.ReadAllLines(assetPath);
        var changed = false;
        foreach (var (editorClassIdentifier, scriptPath) in fixups)
        {
            var scriptGuid = AssetDatabase.AssetPathToGUID(scriptPath);
            if (string.IsNullOrWhiteSpace(scriptGuid))
            {
                throw new InvalidOperationException($"MonoScript asset at {scriptPath} was not found.");
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains($"m_EditorClassIdentifier: {editorClassIdentifier}", StringComparison.Ordinal))
                {
                    continue;
                }

                var searchStart = Math.Max(0, i - 8);
                for (var j = i - 1; j >= searchStart; j--)
                {
                    if (!lines[j].TrimStart().StartsWith("m_Script:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    lines[j] = $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}";
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            File.WriteAllLines(assetPath, lines);
        }
    }

    private static GameObject EnsureRootObject(string name)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return new GameObject(name);
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
}
