using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor;

public static partial class BattleMapCatalogBootstrap
{
    private const string CatalogPath = "Assets/Resources/_Game/Battle/BattleMapCatalog.asset";
    private const string MapPrefabPath = "Assets/_Game/Prefabs/Battle/Maps/BattleMap_Forest_Ruins_01.prefab";
    private const string MapMaterialFolderPath = "Assets/_Game/Materials/Battle/Maps";
    private const string GroundMaterialPath = "Assets/_Game/Materials/Battle/Maps/M_BattleMap_WolfPine_Ground.mat";
    private const string RoadMaterialPath = "Assets/_Game/Materials/Battle/Maps/M_BattleMap_WolfPine_Road.mat";
    private const string RoadMeshPath = "Assets/_Game/Prefabs/Battle/Maps/Mesh_WolfPineRoad.asset";
    private const string GroundTexturePath = "Assets/TriForge Assets/Fantasy Worlds - Forest/Textures/Terrain/T_fwOF_GrassTerrain_01_BC.png";
    private const string RoadTexturePath = "Assets/TriForge Assets/Fantasy Worlds - Forest/Textures/Terrain/T_fwOF_Soil_02_BC.png";
    private const string MapId = "map_001_forest_ruins";

    [MenuItem("SM/Internal/Content/Generate Battle Map Catalog")]
    public static void EnsureBattleMapCatalog()
    {
        EnsureFolder("Assets/_Game/Prefabs/Battle/Maps");
        EnsureFolder("Assets/Resources/_Game/Battle");
        EnsureFolder(MapMaterialFolderPath);

        var mapPrefab = EnsureForestRuinsMapPrefab();
        var catalog = AssetDatabase.LoadAssetAtPath<BattleMapCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BattleMapCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.SetDefaultMapId(MapId);
        catalog.SetMap(
            MapId,
            "늑대소나무길",
            mapPrefab,
            Vector3.zero,
            Vector3.zero,
            Vector3.one,
            BattleMapTacticalOverlayMode.None);
        catalog.SetChapterPool("chapter_ashen_gate", new[] { MapId });
        catalog.SetChapterPool("chapter_sunken_bastion", new[] { MapId });
        catalog.SetChapterPool("chapter_ruined_crypts", new[] { MapId });
        catalog.SetChapterPool("chapter_glass_forest", new[] { MapId });
        catalog.SetChapterPool("chapter_heartforge_descent", new[] { MapId });

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BattleMapCatalogBootstrap] Battle map catalog ready: {CatalogPath}");
    }

    private static GameObject EnsureForestRuinsMapPrefab()
    {
        EnsureWolfPineMaterials();

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
        if (existing != null)
        {
            EnsurePrefabRootAuthoring();
            return AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath)!;
        }

        var root = new GameObject("BattleMap_Forest_Ruins_01");
        try
        {
            EnsureMapRootAuthoring(root);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Failed to create battle map prefab: {MapPrefabPath}");
            }

            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void EnsurePrefabRootAuthoring()
    {
        var root = PrefabUtility.LoadPrefabContents(MapPrefabPath);
        try
        {
            EnsureMapRootAuthoring(root);
            PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureMapRootAuthoring(GameObject root)
    {
        RemoveMissingScripts(root.transform);
        ClearChildren(root.transform);
        EnsurePlayableFloor(root.transform);
        EnsureWolfPineRoad(root.transform);
        EnsureWolfPineDiorama(root.transform);
        EnsureWolfPineDressing(root.transform);
        EnsureWolfPineEdgeTreatment(root.transform);
    }

    private static void EnsurePlayableFloor(Transform root)
    {
        var floor = root.Find("PlayableFloor")?.gameObject;
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "PlayableFloor";
            floor.transform.SetParent(root, false);
        }

        floor.transform.localPosition = new Vector3(0f, -1.12f, 0f);
        floor.transform.localRotation = Quaternion.identity;
        floor.transform.localScale = new Vector3(30f, 1f, 20f);
        RemoveCollider(floor);

        var renderer = floor.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        }
    }

    private static void EnsureWolfPineRoad(Transform root)
    {
        var road = new GameObject("WolfPineRoad");
        road.name = "WolfPineRoad";
        road.transform.SetParent(root, false);
        road.transform.localPosition = new Vector3(0f, -1.105f, -0.12f);
        road.transform.localRotation = Quaternion.Euler(0f, -2.5f, 0f);
        road.transform.localScale = Vector3.one;

        var meshFilter = road.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = EnsureWolfPineRoadMesh();
        var renderer = road.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
    }

    private static void EnsureWolfPineDressing(Transform root)
    {
        var treeLine = EnsureChild(root, "WolfPineTreeline");
        AddVendorPrefab(treeLine, "Pine_Left_Back_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_1.prefab", new Vector3(-15.8f, -1.12f, 9.3f), new Vector3(0f, 30f, 0f), new Vector3(0.54f, 0.54f, 0.54f));
        AddVendorPrefab(treeLine, "Pine_Left_Back_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_2.prefab", new Vector3(-11.6f, -1.12f, 10.4f), new Vector3(0f, -12f, 0f), new Vector3(0.58f, 0.58f, 0.58f));
        AddVendorPrefab(treeLine, "Pine_Left_Mid_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_S_1.prefab", new Vector3(-16.2f, -1.12f, 4.5f), new Vector3(0f, 58f, 0f), new Vector3(0.56f, 0.56f, 0.56f));
        AddVendorPrefab(treeLine, "Pine_Left_Front_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_S_2.prefab", new Vector3(-19.6f, -1.12f, -11.2f), new Vector3(0f, 8f, 0f), new Vector3(0.34f, 0.34f, 0.34f));
        AddVendorPrefab(treeLine, "Pine_Right_Back_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_2.prefab", new Vector3(15.4f, -1.12f, 9.5f), new Vector3(0f, -22f, 0f), new Vector3(0.55f, 0.55f, 0.55f));
        AddVendorPrefab(treeLine, "Pine_Right_Back_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_1.prefab", new Vector3(11.4f, -1.12f, 10.7f), new Vector3(0f, 18f, 0f), new Vector3(0.56f, 0.56f, 0.56f));
        AddVendorPrefab(treeLine, "Pine_Right_Mid_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_S_1.prefab", new Vector3(16.0f, -1.12f, 4.4f), new Vector3(0f, -40f, 0f), new Vector3(0.56f, 0.56f, 0.56f));
        AddVendorPrefab(treeLine, "Pine_Right_Front_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_S_2.prefab", new Vector3(19.4f, -1.12f, -11.0f), new Vector3(0f, -8f, 0f), new Vector3(0.34f, 0.34f, 0.34f));

        var understory = EnsureChild(root, "WolfPineUnderstory");
        AddVendorPrefab(understory, "Grass_Left_Front", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(-6.9f, -1.07f, -5.3f), new Vector3(0f, 38f, 0f), new Vector3(2.0f, 2.0f, 2.0f));
        AddVendorPrefab(understory, "Grass_Right_Front", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(6.8f, -1.07f, -5.0f), new Vector3(0f, -34f, 0f), new Vector3(2.1f, 2.1f, 2.1f));
        AddVendorPrefab(understory, "Grass_Left_Back", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_2.prefab", new Vector3(-7.8f, -1.07f, 5.6f), new Vector3(0f, -20f, 0f), new Vector3(1.45f, 1.45f, 1.45f));
        AddVendorPrefab(understory, "Grass_Right_Back", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_3.prefab", new Vector3(7.7f, -1.07f, 5.8f), new Vector3(0f, 24f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(understory, "Fern_Left_Road", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_01.prefab", new Vector3(-4.6f, -1.06f, 2.1f), new Vector3(0f, -20f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(understory, "Fern_Right_Road", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_02.prefab", new Vector3(4.8f, -1.06f, -2.0f), new Vector3(0f, 28f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
        AddVendorPrefab(understory, "Grass_Left_Path_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(-3.0f, -1.07f, -3.3f), new Vector3(0f, 66f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
        AddVendorPrefab(understory, "Grass_Left_Path_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(-1.2f, -1.07f, 3.0f), new Vector3(0f, -18f, 0f), new Vector3(1.55f, 1.55f, 1.55f));
        AddVendorPrefab(understory, "Grass_Right_Path_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(2.8f, -1.07f, 3.4f), new Vector3(0f, -42f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(understory, "Grass_Right_Path_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(3.3f, -1.07f, -3.4f), new Vector3(0f, 18f, 0f), new Vector3(1.65f, 1.65f, 1.65f));
        AddVendorPrefab(understory, "Fern_Left_Front_Cluster", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_03.prefab", new Vector3(-7.9f, -1.06f, -3.9f), new Vector3(0f, 14f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
        AddVendorPrefab(understory, "Fern_Right_Back_Cluster", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_03.prefab", new Vector3(8.4f, -1.06f, 4.4f), new Vector3(0f, -34f, 0f), new Vector3(1.15f, 1.15f, 1.15f));
        AddVendorPrefab(understory, "Bush_Left_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Bush_01.prefab", new Vector3(-9.6f, -1.12f, 3.3f), new Vector3(0f, 22f, 0f), new Vector3(0.75f, 0.75f, 0.75f));
        AddVendorPrefab(understory, "Bush_Right_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Bush_02.prefab", new Vector3(9.5f, -1.12f, -3.1f), new Vector3(0f, -18f, 0f), new Vector3(0.78f, 0.78f, 0.78f));

        var landmarks = EnsureChild(root, "WolfPineLandmarks");
        AddVendorPrefab(landmarks, "MossyRock_Left", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_RockMossy_03.prefab", new Vector3(-8.2f, -1.06f, -2.8f), new Vector3(0f, 34f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
        AddVendorPrefab(landmarks, "MossyRock_Right", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_RockMossy_06.prefab", new Vector3(8.5f, -1.06f, 2.4f), new Vector3(0f, -28f, 0f), new Vector3(1.15f, 1.15f, 1.15f));
        AddVendorPrefab(landmarks, "FallenLog_Back", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Log_03.prefab", new Vector3(-4.8f, -1.04f, 6.4f), new Vector3(0f, -18f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
        AddVendorPrefab(landmarks, "FallenLog_Front", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Log_05.prefab", new Vector3(3.9f, -1.04f, -6.0f), new Vector3(0f, 24f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
        AddVendorPrefab(landmarks, "RoadSign_Left", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_RoadsignPost_03.prefab", new Vector3(-5.9f, -1.05f, 3.8f), new Vector3(0f, 82f, 0f), Vector3.one);
        AddVendorPrefab(landmarks, "Mushrooms_Left_Log", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_fwOF_Mushroom_A_Group_02.prefab", new Vector3(-5.7f, -1.04f, 5.0f), new Vector3(0f, 18f, 0f), new Vector3(1.0f, 1.0f, 1.0f));
        AddVendorPrefab(landmarks, "Mushrooms_Right_Path", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_fwOF_Mushroom_B_Group_01.prefab", new Vector3(6.4f, -1.04f, -4.2f), new Vector3(0f, -30f, 0f), new Vector3(0.95f, 0.95f, 0.95f));
        AddVendorPrefab(landmarks, "Branch_Left_Path", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Branch_03.prefab", new Vector3(-2.6f, -1.04f, -4.7f), new Vector3(0f, 64f, 0f), new Vector3(1.0f, 1.0f, 1.0f));
        AddVendorPrefab(landmarks, "SmallStones_Right", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Stone_Group_02.prefab", new Vector3(7.1f, -1.055f, 0.8f), new Vector3(0f, -16f, 0f), new Vector3(0.9f, 0.9f, 0.9f));
    }

    private static void EnsureWolfPineMaterials()
    {
        EnsureWolfPineMaterial(
            GroundMaterialPath,
            "M_BattleMap_WolfPine_Ground",
            new Color(0.25f, 0.40f, 0.11f, 1f),
            new Color(0.035f, 0.095f, 0.025f, 1f),
            0.96f,
            0.32f,
            0.62f,
            GroundTexturePath,
            new Vector2(8f, 6f));
        EnsureWolfPineMaterial(
            RoadMaterialPath,
            "M_BattleMap_WolfPine_Road",
            new Color(0.76f, 0.43f, 0.14f, 1f),
            new Color(0.12f, 0.055f, 0.018f, 1f),
            0.98f,
            0.34f,
            0.74f,
            RoadTexturePath,
            new Vector2(5.5f, 0.85f));
    }

    private static Material EnsureWolfPineMaterial(
        string path,
        string materialName,
        Color color,
        Color shadowColor,
        float textureImpact,
        float lightContribution,
        float unityShadowPower,
        string texturePath,
        Vector2 textureScale)
    {
        var shader = Shader.Find("Quibli/Stylized Lit")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            throw new System.InvalidOperationException("No shader could be resolved for forest ruins ground material.");
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
            material.name = materialName;
        }

        SetColor(material, "_BaseColor", color);
        SetColor(material, "_ColorGradient", Color.Lerp(shadowColor, color, 0.55f));
        SetColor(material, "_Color", color);
        var texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
        if (texture != null)
        {
            SetTexture(material, texture, "_BaseMap", "_MainTex");
            SetTextureScale(material, textureScale, "_BaseMap", "_MainTex");
        }
        else
        {
            ClearTexture(material, "_BaseMap");
            ClearTexture(material, "_MainTex");
        }

        ClearTexture(material, "_BumpMap");
        SetFloat(material, "_Metallic", 0f);
        SetFloat(material, "_Smoothness", 0.04f);
        SetFloat(material, "_TextureImpact", textureImpact);
        SetFloat(material, "_LightContribution", lightContribution);
        SetFloat(material, "_SelfShadingSize", 0.34f);
        SetFloat(material, "_ReceiveShadows", 1f);
        SetFloat(material, "_SpecularEnabled", 0f);
        SetFloat(material, "_RimEnabled", 0f);
        SetFloat(material, "_OverrideLightAttenuation", 1f);
        SetVector(material, "_LightAttenuation", new Vector4(0.30f, 0.78f, 0f, 0f));
        SetColor(material, "_ShadowColor", shadowColor);
        SetFloat(material, "_UnityShadowMode", 1f);
        SetFloat(material, "_UnityShadowOcclusion", 0f);
        SetFloat(material, "_UnityShadowPower", unityShadowPower);
        SetFloat(material, "_UnityShadowSharpness", 8f);
        SetFloat(material, "_ShadowEdgeSize", 0.62f);
        SetFloat(material, "_ShadowEdgeSizeExtra", 0.045f);
        material.EnableKeyword("DR_LIGHT_ATTENUATION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh EnsureWolfPineRoadMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RoadMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = "Mesh_WolfPineRoad"
            };
            AssetDatabase.CreateAsset(mesh, RoadMeshPath);
        }

        var lowerEdge = new[]
        {
            new Vector3(-70f, 0f, -2.95f),
            new Vector3(-56f, 0f, -2.25f),
            new Vector3(-42f, 0f, -3.45f),
            new Vector3(-30f, 0f, -2.55f),
            new Vector3(-22f, 0f, -3.35f),
            new Vector3(-16f, 0f, -2.70f),
            new Vector3(-11f, 0f, -3.65f),
            new Vector3(-7f, 0f, -2.55f),
            new Vector3(-3f, 0f, -3.30f),
            new Vector3(0f, 0f, -2.75f),
            new Vector3(4f, 0f, -3.85f),
            new Vector3(8f, 0f, -2.62f),
            new Vector3(13f, 0f, -3.42f),
            new Vector3(19f, 0f, -2.45f),
            new Vector3(28f, 0f, -3.55f),
            new Vector3(42f, 0f, -2.35f),
            new Vector3(56f, 0f, -3.20f),
            new Vector3(70f, 0f, -2.72f),
        };
        var upperEdge = new[]
        {
            new Vector3(-70f, 0f, 2.55f),
            new Vector3(-56f, 0f, 3.25f),
            new Vector3(-42f, 0f, 2.35f),
            new Vector3(-30f, 0f, 3.38f),
            new Vector3(-22f, 0f, 2.40f),
            new Vector3(-16f, 0f, 3.12f),
            new Vector3(-11f, 0f, 2.35f),
            new Vector3(-7f, 0f, 3.38f),
            new Vector3(-3f, 0f, 2.62f),
            new Vector3(0f, 0f, 3.55f),
            new Vector3(4f, 0f, 2.48f),
            new Vector3(8f, 0f, 3.24f),
            new Vector3(13f, 0f, 2.25f),
            new Vector3(19f, 0f, 3.12f),
            new Vector3(28f, 0f, 2.44f),
            new Vector3(42f, 0f, 3.28f),
            new Vector3(56f, 0f, 2.42f),
            new Vector3(70f, 0f, 3.02f),
        };
        var vertices = new Vector3[lowerEdge.Length * 2];
        var uvs = new Vector2[vertices.Length];
        for (var i = 0; i < lowerEdge.Length; i++)
        {
            var t = i / (float)(lowerEdge.Length - 1);
            vertices[i * 2] = lowerEdge[i];
            vertices[(i * 2) + 1] = upperEdge[i];
            uvs[i * 2] = new Vector2(t * 11f, 0f);
            uvs[(i * 2) + 1] = new Vector2(t * 11f, 1f);
        }

        var triangles = new int[(lowerEdge.Length - 1) * 6];
        var triangleIndex = 0;
        for (var i = 0; i < lowerEdge.Length - 1; i++)
        {
            var lower = i * 2;
            var upper = lower + 1;
            var nextLower = lower + 2;
            var nextUpper = lower + 3;
            triangles[triangleIndex++] = lower;
            triangles[triangleIndex++] = upper;
            triangles[triangleIndex++] = nextLower;
            triangles[triangleIndex++] = upper;
            triangles[triangleIndex++] = nextUpper;
            triangles[triangleIndex++] = nextLower;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        return CreateChild(parent, name);
    }

    private static void ClearChildren(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void EnsureVendorPrefab(Transform parent, string name, string path, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        if (parent.Find(name) != null)
        {
            return;
        }

        AddVendorPrefab(parent, name, path, localPosition, localEulerAngles, localScale);
    }

    private static void AddVendorPrefab(Transform parent, string name, string path, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[BattleMapCatalogBootstrap] Missing vendor prefab: {path}");
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            return;
        }

        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEulerAngles);
        instance.transform.localScale = localScale;
    }

    private static void CopyTexture(Material source, Material target, string sourcePropertyName, params string[] targetPropertyNames)
    {
        if (!source.HasProperty(sourcePropertyName))
        {
            return;
        }

        var texture = source.GetTexture(sourcePropertyName);
        if (texture == null)
        {
            return;
        }

        foreach (var targetPropertyName in targetPropertyNames)
        {
            if (target.HasProperty(targetPropertyName))
            {
                target.SetTexture(targetPropertyName, texture);
            }
        }
    }

    private static void ClearTexture(Material material, string propertyName)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, null);
        }
    }

    private static void SetTexture(Material material, Texture texture, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }
    }

    private static void SetTextureScale(Material material, Vector2 scale, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTextureScale(propertyName, scale);
            }
        }
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetColor(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetVector(Material material, string propertyName, Vector4 value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetVector(propertyName, value);
        }
    }

    private static void RemoveCollider(GameObject target)
    {
        var collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static void RemoveMissingScripts(Transform root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
        }
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
