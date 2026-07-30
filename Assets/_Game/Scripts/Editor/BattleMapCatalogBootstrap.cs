using SM.Combat.Model;
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
        WarnIfPropsBlockContestedZone(root.transform);
    }

    /// <summary>
    /// 레인 밴드 안에 장식 프롭이 서 있으면 에러를 낸다.
    ///
    /// <b>이 가드가 있는 이유.</b> 어두운 잎사귀가 싸움에 인접하면 <b>주인공보다 대비가 세진다</b>.
    /// 2026-07-30 실측(전투 중반 실화면, 지면 휘도 168 기준)에서
    /// 덤불 |Δ지면| = 144.2 · 캐릭터 |Δ지면| = 69.1 이었다 — 장식이 피사체보다 2.1배 셌다.
    /// 어두운 잎사귀 자체는 죄가 아니다(LoL 수풀도 지면보다 어둡다). 죄는 그게 <b>싸움에 붙어 있는 것</b>이다.
    /// 좌표만 봐선 그 자리가 전장인지 사람이 알 수 없다. 그래서 기계가 본다.
    ///
    /// <b>왜 전체를 도는가.</b> 처음엔 그룹 이름을 <c>{ Understory, Landmarks, Treeline }</c> 로
    /// 박아 뒀는데, <see cref="EnsureWolfPineRoadEdgeBreakup"/> 가 만드는 <c>WolfPineRoadEdges</c> 를
    /// 놓쳐서 <b>가드가 통과하면서 3개를 그냥 지나쳤다.</b> 손으로 적은 그룹 목록은 반드시 낡는다.
    /// 그래서 루트 밑 모든 그룹의 모든 프롭을 돈다.
    ///
    /// <b>밴드는 왜 레인 폭인가.</b> 진형 확산 최대치(<c>ResolveFormationOffset</c> 의 widthFactor +
    /// flank bias + Diver 보정)를 다 더하면 z 는 3.0 까지 나간다. 그 수치를 밴드로 쓰면 도로 갓길
    /// 장식(<c>WolfPineRoadEdges</c>, |z| 2.7~2.95)까지 걸려서 가드가 <b>의도된 저작</b>을 때린다.
    /// 갓길 장식은 경기장 경계를 읽게 해 주는 기능이라 남겨야 한다. 그래서 밴드는 <b>앵커 레인 폭</b>이고,
    /// 돌진 유닛이 갓길을 스치는 건 이 가드가 잡을 대상이 아니다.
    /// 밴드를 <see cref="BattlefieldLayout.Default"/> 에서 유도하는 게 중요하다 —
    /// 하드코딩하면 레이아웃이 바뀐 뒤에도 통과하면서 거짓말을 한다.
    /// </summary>
    private static void WarnIfPropsBlockContestedZone(Transform root)
    {
        var layout = BattlefieldLayout.Default;

        // 스폰이 앵커보다 바깥이라 BackRowX 만으로는 실제 교전 폭이 안 나온다.
        var halfX = layout.BackRowX + layout.SpawnOffsetX;
        var halfZ = Mathf.Abs(layout.TopLaneY) + 0.3f;

        foreach (Transform group in root)
        {
            foreach (Transform prop in group)
            {
                var renderers = prop.GetComponentsInChildren<Renderer>(includeInactive: true);

                // 그리지 않는 것은 시선을 못 뺏으므로 이 가드의 대상이 아니다.
                if (renderers.Length == 0)
                {
                    continue;
                }

                // 지면·도로·스커트 같은 <b>표면</b>은 설계상 경기장을 통째로 덮으므로 원점에 있는 게 맞다.
                // 이름으로 걸러내면 또 낡는다(그래서 이 가드가 한 번 거짓말했다). 크기로 가른다 —
                // 경기장 폭만큼 넓은 것은 장식일 수 없다.
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                if (bounds.size.x >= halfX * 2f)
                {
                    continue;
                }

                // 그룹 트랜스폼이 항등이 아닐 수도 있으니 루트 기준으로 환산해서 본다.
                var p = root.InverseTransformPoint(prop.position);
                if (Mathf.Abs(p.x) >= halfX || Mathf.Abs(p.z) >= halfZ)
                {
                    continue;
                }

                Debug.LogError(
                    $"[BattleMapCatalogBootstrap] '{group.name}/{prop.name}' 이(가) 레인 밴드 안에 있다: "
                    + $"(x {p.x:0.##}, z {p.z:0.##}). 허용 밖: |x| >= {halfX:0.##} 또는 |z| >= {halfZ:0.##}. "
                    + "장식이 싸움에 붙으면 캐릭터보다 대비가 세져서 전투 가독성을 깎는다 — "
                    + "먼쪽 배경(+z)이나 좌우 바깥으로 옮겨라.");
            }
        }
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

        // 실측(2026-07-30): 이 언더스토리가 <b>전투 가독성의 2차 원인</b>이었다.
        //
        // 전투 중반 실화면에서 요소별 대비를 재면 이렇게 나온다(지면 휘도 168 기준).
        //   덤불/고사리  화면의 3.87%  휘도 23.8  |Δ지면| = 144.2   <- 제일 시끄러움
        //   캐릭터       화면의 7.32%  휘도 98.9  |Δ지면| =  69.1
        // 주인공이 <b>화면에서 한 번도 1등이 아니다.</b> 장식이 피사체보다 2.1배 세게 튄다.
        // 잎이 어두운 것 자체는 죄가 아니다(LoL 수풀도 지면보다 어둡다). 죄는 그 대비가
        // <b>싸움 한복판에 있다는 것</b>이다 — 대비는 인접할 때만 경쟁한다.
        //
        // 그래서 교전 구역을 비운다. BattlefieldLayout.Default 기준 실제 교전 footprint 는
        // 앵커 x [-4.9, 4.9] + 스폰 오프셋 1.25 -> x [-6.2, 6.2], 레인 z [-1.8, 1.8] + 진형
        // 확산 -> z [-2.5, 2.5] 다. <b>이 사각형 안에는 아무 프롭도 두지 않는다.</b>
        // 지형 엄폐 메커니즘은 없으므로(진형 몸빵은 측정 후 revert 됨) 순수 장식이라 이동이 안전하다.
        //
        // 배치 원칙: 카메라는 -z 에서 +z 를 33° 내려본다. 그래서 +z 는 싸움 <b>뒤</b>로 깔리는
        // 배경이고, -z 는 <b>앞</b>을 가리는 전경이다. 옮길 곳은 먼쪽 배경 아니면 좌우 바깥이다.
        var understory = EnsureChild(root, "WolfPineUnderstory");

        // 전경 큰 덤불 2개는 자리는 맞는데 배율이 과했다(2.0/2.1). 화면 아래를 검은 덩어리로 막는다.
        AddVendorPrefab(understory, "Grass_Left_Front", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(-6.9f, -1.07f, -5.3f), new Vector3(0f, 38f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
        AddVendorPrefab(understory, "Grass_Right_Front", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(6.8f, -1.07f, -5.0f), new Vector3(0f, -34f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
        AddVendorPrefab(understory, "Grass_Left_Back", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_2.prefab", new Vector3(-7.8f, -1.07f, 5.6f), new Vector3(0f, -20f, 0f), new Vector3(1.45f, 1.45f, 1.45f));
        AddVendorPrefab(understory, "Grass_Right_Back", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_3.prefab", new Vector3(7.7f, -1.07f, 5.8f), new Vector3(0f, 24f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        // 아래 6개는 전부 교전 사각형 안이나 그 테두리에 서 있었다 — 고사리 2개는 싸움 한복판,
        // 길가 풀 4개는 z ±3.0~3.4 로 전장을 빙 둘러쌌다. 바깥으로 내보낸다.
        // (이전 좌표: Fern -4.6,2.1 / 4.8,-2.0 · Path -3.0,-3.3 / -1.2,3.0 / 2.8,3.4 / 3.3,-3.4)
        AddVendorPrefab(understory, "Fern_Left_Road", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_01.prefab", new Vector3(-7.7f, -1.06f, 3.2f), new Vector3(0f, -20f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(understory, "Fern_Right_Road", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_02.prefab", new Vector3(8.0f, -1.06f, -2.6f), new Vector3(0f, 28f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
        AddVendorPrefab(understory, "Grass_Left_Path_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(-5.2f, -1.07f, -5.6f), new Vector3(0f, 66f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
        AddVendorPrefab(understory, "Grass_Left_Path_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(-2.2f, -1.07f, 5.2f), new Vector3(0f, -18f, 0f), new Vector3(1.55f, 1.55f, 1.55f));
        AddVendorPrefab(understory, "Grass_Right_Path_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(2.6f, -1.07f, 5.4f), new Vector3(0f, -42f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
        AddVendorPrefab(understory, "Grass_Right_Path_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(5.0f, -1.07f, -5.7f), new Vector3(0f, 18f, 0f), new Vector3(1.65f, 1.65f, 1.65f));
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

    /// <summary>
    /// 전장 지면 두 장의 파라미터를 <b>여기서</b> 소유한다.
    ///
    /// <b>이 값들은 커밋된 .mat 과 일부러 일치시켜 둔 것이다. 갈라지면 안 된다.</b>
    ///
    /// 2026-07-30 에 이게 갈라져 있었다. 과거 누군가 에디터에서 지면/도로를 손으로 튜닝했는데
    /// 이 함수는 갱신하지 않아서, 두 소스가 <b>전 파라미터에서</b> 어긋나 있었다.
    ///
    /// <code>
    ///            커밋된 .mat (튜닝됨)          이 코드에 있던 값
    ///   ground   0.42,0.55,0.18 · tex 0.62    0.25,0.40,0.11 · tex 0.96
    ///   road     0.55,0.42,0.28 · tex 0.60    0.76,0.43,0.14 · tex 0.98
    ///   (양쪽 lightContribution 0.58          vs 0.32/0.34)
    /// </code>
    ///
    /// 이 함수는 실행될 때마다 .mat 을 덮어쓰므로, <b>맵을 재생성하는 것만으로 튜닝이 조용히
    /// 되돌아갔다</b>. 실제로 되돌아간 화면을 재니 교전면 휘도 94.5·채도 0.40 인데 캐릭터가
    /// 94.5·0.44 였다 — <b>지면과 주인공을 가르는 두 채널이 통째로 무너졌다.</b> 튜닝된 쪽은
    /// 교전면 160·0.17 대 캐릭터 99·0.44 로 값 차 61 을 벌어 준다. 튜닝된 쪽이 맞다.
    ///
    /// <b>고칠 때 주의 1 — 색만 만지면 아무 일도 안 일어난다.</b> textureImpact 가 높으면
    /// 알베도가 거의 전부 텍스처다. 실제로 tex 0.98 상태에서 색을 크게 바꿔도 교전면은
    /// 94.5 -> 94.2 로 안 움직였다. 그때 화면(휘도 94.2·채도 0.36)은 도로 텍스처 파일
    /// 자체(85.8·0.40)와 거의 같았다 — <b>화면은 그냥 텍스처였다.</b> 값을 옮기려면
    /// textureImpact 를 먼저 내려야 한다(튜닝된 0.60~0.62 가 이미 그 자리다).
    ///
    /// <b>주의 2 — 지면 머티리얼은 교전면 전용이 아니다.</b> <c>PlayableFloor</c> 뿐 아니라
    /// 지형 조형 메시(<c>NearSkirt</c>, <c>FarRidge</c>)도 같은 머티리얼을 쓴다. 그래서 지면을
    /// 밝히면 전경 지형 덩어리가 같이 밝아져 화면 아래쪽을 통째로 먹는다. 실제로 그렇게 만들어
    /// 확인했다. 교전면만 따로 조율하려면 <b>머티리얼을 분리하는 게 선행</b>이다.
    /// </summary>
    private static void EnsureWolfPineMaterials()
    {
        EnsureWolfPineMaterial(
            GroundMaterialPath,
            "M_BattleMap_WolfPine_Ground",
            new Color(0.42f, 0.55f, 0.18f, 1f),
            new Color(0.055f, 0.12f, 0.04f, 1f),
            0.62f,
            0.58f,
            0.60f,
            GroundTexturePath,
            new Vector2(8f, 6f));

        EnsureWolfPineMaterial(
            RoadMaterialPath,
            "M_BattleMap_WolfPine_Road",
            new Color(0.55f, 0.42f, 0.28f, 1f),
            new Color(0.155f, 0.08f, 0.032f, 1f),
            0.60f,
            0.58f,
            0.60f,
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
