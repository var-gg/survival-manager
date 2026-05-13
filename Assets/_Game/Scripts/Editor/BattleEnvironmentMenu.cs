using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor;

/// <summary>
/// SM/Battle 메뉴 — 전투 씬 비주얼 튜닝 워크스페이스를 한 번에 셋업.
///
/// 사용 흐름:
///   1. SM/Battle/맵 + 캐릭터 미리보기 셋업  →  씬 열기 + 환경 + map prefab + 더미 캐릭터까지 한 방에.
///   2. BattleRenderEnvironment Inspector에서 슬라이더로 라이팅/포스트 튠.
///   3. 끝나면 SM/Battle/미리보기 정리.
/// </summary>
public static class BattleEnvironmentMenu
{
    private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
    private const string EnvironmentGameObjectName = "BattleRenderEnvironment";
    private const string PreviewWorkspaceName = "__BattlePreviewWorkspace";
    private const string DefaultMapPrefabPath = "Assets/_Game/Prefabs/Battle/Maps/BattleMap_Forest_Ruins_01.prefab";

    [MenuItem("SM/Battle/맵 + 캐릭터 미리보기 셋업", priority = 0)]
    public static void SetupBattlePreview()
    {
        EnsureBattleSceneOpen();
        var env = EnsureBattleRenderEnvironment();
        var workspace = EnsurePreviewWorkspace();
        EnsureMapInstance(workspace.transform);
        EnsureDummyActors(workspace.transform);

        env.Apply();

        Selection.activeGameObject = env.gameObject;
        EditorGUIUtility.PingObject(env.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[SM/Battle] 미리보기 셋업 완료 — Inspector에서 슬라이더로 튠하세요. 끝나면 'SM/Battle/미리보기 정리'.");
    }

    [MenuItem("SM/Battle/미리보기 정리", priority = 1)]
    public static void TeardownBattlePreview()
    {
        var ws = GameObject.Find(PreviewWorkspaceName);
        if (ws == null)
        {
            Debug.Log("[SM/Battle] 정리할 미리보기 워크스페이스가 없습니다.");
            return;
        }
        Undo.DestroyObjectImmediate(ws);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[SM/Battle] 미리보기 워크스페이스 제거 완료.");
    }

    // ─────────────────────────────────────────────────────────────────────

    private static BattleRenderEnvironmentAuthoring EnsureBattleRenderEnvironment()
    {
        var go = GameObject.Find(EnvironmentGameObjectName);
        var created = false;
        if (go == null)
        {
            go = new GameObject(EnvironmentGameObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create BattleRenderEnvironment");
            created = true;
        }

        var auth = go.GetComponent<BattleRenderEnvironmentAuthoring>();
        if (auth == null)
        {
            auth = Undo.AddComponent<BattleRenderEnvironmentAuthoring>(go);
            auth.ApplyGameplayPreset();
        }
        if (created)
        {
            Debug.Log($"[SM/Battle] '{EnvironmentGameObjectName}' 새로 생성됨.");
        }
        return auth;
    }

    private static GameObject EnsurePreviewWorkspace()
    {
        var existing = GameObject.Find(PreviewWorkspaceName);
        if (existing != null) return existing;

        var ws = new GameObject(PreviewWorkspaceName);
        // EditorOnly 태그가 있으면 빌드에서 자동 strip됨 (없으면 무시).
        try { ws.tag = "EditorOnly"; } catch { /* tag가 없을 수 있음 — 무시 */ }
        Undo.RegisterCreatedObjectUndo(ws, "Create Battle Preview Workspace");
        return ws;
    }

    private static void EnsureMapInstance(Transform workspace)
    {
        var existing = workspace.Find("BattleMap_Preview");
        if (existing != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMapPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[SM/Battle] Map prefab을 찾지 못했습니다: {DefaultMapPrefabPath}");
            return;
        }
        var instance = PrefabUtility.InstantiatePrefab(prefab, workspace) as GameObject;
        if (instance == null) return;
        instance.name = "BattleMap_Preview";
        Undo.RegisterCreatedObjectUndo(instance, "Instantiate Battle Map Preview");

        // Runtime DisableObtrusiveBackdrops와 동일하게 Edit 모드에서도 거대 backdrop mountain은 끔.
        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            var n = t.name;
            if (n == "WolfPineBackdrop"
                || n.StartsWith("BackdropMountain_")
                || n.StartsWith("DistantHill_"))
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    private static void EnsureDummyActors(Transform workspace)
    {
        var actorsRoot = workspace.Find("DummyActors");
        if (actorsRoot != null) return;

        var rootGo = new GameObject("DummyActors");
        rootGo.transform.SetParent(workspace, false);
        Undo.RegisterCreatedObjectUndo(rootGo, "Create Dummy Actor Root");

        var skinMaterial = CreateOrLoadDummyMaterial("DummyActorSkin", new Color(0.78f, 0.65f, 0.52f));

        // 2 ally (좌측 음수 x) + 2 enemy (우측 양수 x). PlayableFloor가 y≈-1.07 위에 있음.
        DummyActor(rootGo.transform, "Dummy_Ally_01", new Vector3(-1.6f, 0f, -0.6f), skinMaterial);
        DummyActor(rootGo.transform, "Dummy_Ally_02", new Vector3(-0.8f, 0f, 0.4f), skinMaterial);
        DummyActor(rootGo.transform, "Dummy_Enemy_01", new Vector3(0.9f, 0f, -0.3f), skinMaterial);
        DummyActor(rootGo.transform, "Dummy_Enemy_02", new Vector3(1.7f, 0f, 0.5f), skinMaterial);
    }

    private static void DummyActor(Transform parent, string name, Vector3 position, Material skin)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        // PrimitiveType.Capsule 기본 크기 = 높이 2 → 사람 비율로 살짝 작게.
        go.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
        // 콜라이더 제거 — 라이팅 프리뷰엔 불필요.
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var rend = go.GetComponent<Renderer>();
        if (rend != null && skin != null) rend.sharedMaterial = skin;

        Undo.RegisterCreatedObjectUndo(go, "Create Dummy Actor");
    }

    private static Material CreateOrLoadDummyMaterial(string name, Color color)
    {
        // URP Lit 머티리얼을 하나 만들어 재사용. AssetDatabase에 저장하지 않고 in-memory만.
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null!;
        var mat = new Material(shader) { name = name };
        mat.color = color;
        return mat;
    }

    private static void EnsureBattleSceneOpen()
    {
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.path == BattleScenePath) return;
        EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
    }
}
