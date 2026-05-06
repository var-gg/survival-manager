using UnityEditor;
using UnityEngine;

namespace SM.Editor;

public static partial class BattleMapCatalogBootstrap
{
    private const string FarRidgeMeshPath = "Assets/_Game/Prefabs/Battle/Maps/Mesh_WolfPineFarRidge.asset";
    private const string NearSkirtMeshPath = "Assets/_Game/Prefabs/Battle/Maps/Mesh_WolfPineNearSkirt.asset";

    private static void EnsureWolfPineDiorama(Transform root)
    {
        EnsureWolfPineTerrainLayers(root);
        EnsureWolfPineRoadEdgeBreakup(root);
        EnsureWolfPineHorizon(root);
        EnsureWolfPineAtmosphere(root);
        EnsureWolfPineForegroundFrame(root);
    }

    private static void EnsureWolfPineTerrainLayers(Transform root)
    {
        var terrainRoot = EnsureChild(root, "WolfPineTerrainLayers");
        var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);

        AddGroundStripMesh(
            terrainRoot,
            "FarRidge",
            FarRidgeMeshPath,
            groundMaterial,
            new[]
            {
                new Vector3(-56f, 0.00f, 8.8f),
                new Vector3(-42f, 0.00f, 9.4f),
                new Vector3(-28f, 0.00f, 8.7f),
                new Vector3(-14f, 0.00f, 9.5f),
                new Vector3(0f, 0.00f, 8.9f),
                new Vector3(14f, 0.00f, 9.6f),
                new Vector3(28f, 0.00f, 8.8f),
                new Vector3(42f, 0.00f, 9.3f),
                new Vector3(56f, 0.00f, 8.6f),
            },
            new[]
            {
                new Vector3(-63f, 0.42f, 25.8f),
                new Vector3(-47f, 0.73f, 28.3f),
                new Vector3(-31f, 0.56f, 26.5f),
                new Vector3(-15f, 0.84f, 29.2f),
                new Vector3(0f, 0.64f, 27.4f),
                new Vector3(15f, 0.88f, 29.7f),
                new Vector3(31f, 0.58f, 26.8f),
                new Vector3(47f, 0.76f, 28.6f),
                new Vector3(63f, 0.46f, 26.0f),
            });

        AddGroundMesh(
            terrainRoot,
            "NearSkirt",
            NearSkirtMeshPath,
            groundMaterial,
            new[]
            {
                new Vector3(-50f, 0.00f, -7.7f),
                new Vector3(50f, 0.00f, -8.4f),
                new Vector3(-58f, 0.34f, -27.0f),
                new Vector3(58f, 0.28f, -26.5f),
            });

    }

    private static void EnsureWolfPineHorizon(Transform root)
    {
        var horizonRoot = EnsureChild(root, "WolfPineHorizon");

        AddVendorPrefab(horizonRoot, "DistantHill_Left_Backdrop", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Mountains/P_fwOF_BackgroundMountain_01.prefab", new Vector3(-34f, -1.55f, 30.0f), new Vector3(0f, 166f, 0f), new Vector3(2.1f, 1.25f, 1.45f));
        AddVendorPrefab(horizonRoot, "DistantHill_Right_Backdrop", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Mountains/P_fwOF_BackgroundMountain_01.prefab", new Vector3(34f, -1.58f, 30.5f), new Vector3(0f, 193f, 0f), new Vector3(2.0f, 1.20f, 1.40f));

        AddVendorPrefab(horizonRoot, "HorizonTree_Left_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(-26f, -0.98f, 17.5f), new Vector3(0f, 32f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Left_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_09.prefab", new Vector3(-20.0f, -1.02f, 15.8f), new Vector3(0f, -12f, 0f), new Vector3(1.18f, 1.18f, 1.18f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Left_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_2.prefab", new Vector3(-14.5f, -1.02f, 16.9f), new Vector3(0f, 68f, 0f), new Vector3(1.05f, 1.05f, 1.05f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Center_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_01.prefab", new Vector3(-5.2f, -1.02f, 18.1f), new Vector3(0f, -28f, 0f), new Vector3(1.12f, 1.12f, 1.12f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Center_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_1.prefab", new Vector3(4.0f, -1.02f, 17.2f), new Vector3(0f, 24f, 0f), new Vector3(1.10f, 1.10f, 1.10f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Center_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(-13.5f, -1.00f, 24.5f), new Vector3(0f, 44f, 0f), new Vector3(1.18f, 1.18f, 1.18f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Center_04", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_10.prefab", new Vector3(0.8f, -1.02f, 25.2f), new Vector3(0f, -16f, 0f), new Vector3(1.04f, 1.04f, 1.04f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Center_05", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(14.4f, -1.00f, 24.0f), new Vector3(0f, -38f, 0f), new Vector3(1.16f, 1.16f, 1.16f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Right_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_02.prefab", new Vector3(13.7f, -1.02f, 16.2f), new Vector3(0f, -52f, 0f), new Vector3(1.16f, 1.16f, 1.16f));
        AddVendorPrefab(horizonRoot, "HorizonTree_Right_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(22.2f, -0.98f, 17.8f), new Vector3(0f, 8f, 0f), new Vector3(1.32f, 1.32f, 1.32f));

        AddVendorPrefab(horizonRoot, "RuinedArch_Left_Back", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Ruins/Elements/P_FW01_Ruins_Wall_Low_Broken_01.prefab", new Vector3(-18.5f, -1.00f, 11.7f), new Vector3(0f, 18f, 0f), new Vector3(1.35f, 1.15f, 1.35f));
        AddVendorPrefab(horizonRoot, "RuinedWall_Right_Back", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Ruins/Elements/P_FW01_Ruins_Wall_Low_Broken_02.prefab", new Vector3(18.2f, -1.00f, 11.2f), new Vector3(0f, -20f, 0f), new Vector3(1.2f, 1.05f, 1.2f));
    }

    private static void EnsureWolfPineAtmosphere(Transform root)
    {
        var atmosphereRoot = EnsureChild(root, "WolfPineAtmosphere");

        AddVendorPrefab(atmosphereRoot, "BackMist_LowRoad", "Assets/TriForge Assets/Fantasy Worlds - Forest/Particles/VFX_FW01_Mist_01.prefab", new Vector3(0.5f, -0.55f, 11.8f), new Vector3(0f, -8f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(atmosphereRoot, "BackLightRays_Left", "Assets/TriForge Assets/Fantasy Worlds - Forest/Particles/VFX_FW01_LightRays_01.prefab", new Vector3(-12.5f, 1.8f, 14.8f), new Vector3(0f, 30f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
        AddVendorPrefab(atmosphereRoot, "BackLeaves_Drift", "Assets/TriForge Assets/Fantasy Worlds - Forest/Particles/VFX_FW01_BlowingLeaves_01.prefab", new Vector3(3.0f, 0.6f, 8.5f), new Vector3(0f, -18f, 0f), new Vector3(0.85f, 0.85f, 0.85f));
    }

    private static void EnsureWolfPineRoadEdgeBreakup(Transform root)
    {
        var edgeRoot = EnsureChild(root, "WolfPineRoadEdges");

        AddVendorPrefab(edgeRoot, "EdgeGrass_Left_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(-29.0f, -1.06f, 3.3f), new Vector3(0f, 28f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
        AddVendorPrefab(edgeRoot, "EdgeGrass_Left_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(-20.5f, -1.06f, -3.1f), new Vector3(0f, -36f, 0f), new Vector3(1.45f, 1.45f, 1.45f));
        AddVendorPrefab(edgeRoot, "EdgeFern_Left_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_01.prefab", new Vector3(-12.8f, -1.06f, 3.7f), new Vector3(0f, 18f, 0f), new Vector3(1.15f, 1.15f, 1.15f));
        AddVendorPrefab(edgeRoot, "EdgeStone_Left_04", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Stone_Group_01.prefab", new Vector3(-5.2f, -1.05f, -3.3f), new Vector3(0f, -18f, 0f), new Vector3(0.72f, 0.72f, 0.72f));
        AddVendorPrefab(edgeRoot, "EdgeGrass_Center_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(2.6f, -1.06f, 3.2f), new Vector3(0f, -24f, 0f), new Vector3(1.20f, 1.20f, 1.20f));
        AddVendorPrefab(edgeRoot, "EdgeFern_Center_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_02.prefab", new Vector3(10.6f, -1.06f, -3.0f), new Vector3(0f, 34f, 0f), new Vector3(1.12f, 1.12f, 1.12f));
        AddVendorPrefab(edgeRoot, "EdgeGrass_Right_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(20.4f, -1.06f, 3.6f), new Vector3(0f, 12f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(edgeRoot, "EdgeStone_Right_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Stone_Group_03.prefab", new Vector3(29.0f, -1.05f, -3.3f), new Vector3(0f, -30f, 0f), new Vector3(0.70f, 0.70f, 0.70f));
    }

    private static void EnsureWolfPineForegroundFrame(Transform root)
    {
        var frameRoot = EnsureChild(root, "WolfPineForegroundFrame");

        AddVendorPrefab(frameRoot, "ForegroundLog_Left", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Log_01.prefab", new Vector3(-17.0f, -1.02f, -10.3f), new Vector3(0f, 28f, 0f), new Vector3(1.65f, 1.65f, 1.65f));
        AddVendorPrefab(frameRoot, "ForegroundLog_Right", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Log_04.prefab", new Vector3(16.8f, -1.02f, -10.0f), new Vector3(0f, -24f, 0f), new Vector3(1.55f, 1.55f, 1.55f));
        AddVendorPrefab(frameRoot, "ForegroundRock_Left", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_RockMossy_05.prefab", new Vector3(-22.0f, -1.04f, -6.7f), new Vector3(0f, 42f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(frameRoot, "ForegroundRock_Right", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_RockMossy_02.prefab", new Vector3(22.3f, -1.04f, -6.4f), new Vector3(0f, -34f, 0f), new Vector3(1.28f, 1.28f, 1.28f));
        AddVendorPrefab(frameRoot, "ForegroundFern_Left", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_03.prefab", new Vector3(-13.8f, -1.05f, -7.7f), new Vector3(0f, -18f, 0f), new Vector3(1.5f, 1.5f, 1.5f));
        AddVendorPrefab(frameRoot, "ForegroundFern_Right", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_02.prefab", new Vector3(13.4f, -1.05f, -7.5f), new Vector3(0f, 20f, 0f), new Vector3(1.45f, 1.45f, 1.45f));
        AddVendorPrefab(frameRoot, "ForegroundSapling_Left", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_TreeSapling_04.prefab", new Vector3(-34.0f, -1.02f, -2.0f), new Vector3(0f, 12f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(frameRoot, "ForegroundSapling_Right", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_TreeSapling_02.prefab", new Vector3(34.0f, -1.02f, -1.3f), new Vector3(0f, -14f, 0f), new Vector3(1.30f, 1.30f, 1.30f));
    }

    private static void AddGroundMesh(
        Transform parent,
        string name,
        string meshPath,
        Material material,
        Vector3[] vertices)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, -1.125f, 0f);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = EnsureGroundPatchMesh(meshPath, vertices);

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    private static void AddGroundStripMesh(
        Transform parent,
        string name,
        string meshPath,
        Material material,
        Vector3[] lowerEdge,
        Vector3[] upperEdge)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, -1.125f, 0f);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = EnsureGroundStripMesh(meshPath, lowerEdge, upperEdge);

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    private static Mesh EnsureGroundPatchMesh(string path, Vector3[] vertices)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(mesh, path);
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(8f, 0f),
            new Vector2(0f, 4f),
            new Vector2(8f, 4f),
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Mesh EnsureGroundStripMesh(string path, Vector3[] lowerEdge, Vector3[] upperEdge)
    {
        if (lowerEdge.Length != upperEdge.Length || lowerEdge.Length < 2)
        {
            throw new System.ArgumentException("Ground strip mesh requires matching lower/upper edge arrays with at least two points.");
        }

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(mesh, path);
        }

        var vertices = new Vector3[lowerEdge.Length * 2];
        var uvs = new Vector2[vertices.Length];
        for (var i = 0; i < lowerEdge.Length; i++)
        {
            var t = i / (float)(lowerEdge.Length - 1);
            vertices[i * 2] = lowerEdge[i];
            vertices[(i * 2) + 1] = upperEdge[i];
            uvs[i * 2] = new Vector2(t * 8f, 0f);
            uvs[(i * 2) + 1] = new Vector2(t * 8f, 4f);
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
}
