using System.IO;
using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SM.Editor.Tools;

/// <summary>
/// Edit-mode 전용 캡쳐 도구. Battle 씬을 열고 wolfpine 맵 프리팹을 hidden preview root에
/// 임시 instantiate한 뒤 BattleMapMaterialAdapter / BattleStageEnvironmentAdapter를
/// 그대로 호출해 런타임 룩을 재현하고 Main Camera를 PNG로 저장한다.
/// Play mode 진입 없이 동기 호출만으로 끝나서 unity-cli menu 디스패치에 잘 맞는다.
/// </summary>
public static class BattleSceneCaptureTool
{
    private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
    private const string MapPrefabPath = "Assets/_Game/Prefabs/Battle/Maps/BattleMap_Forest_Ruins_01.prefab";
    private const string GroundMaterialPath = "Assets/_Game/Materials/Battle/Maps/M_BattleMap_WolfPine_Ground.mat";
    private const string RoadMaterialPath = "Assets/_Game/Materials/Battle/Maps/M_BattleMap_WolfPine_Road.mat";
    private const string CaptureDirectory = "Captures";
    private const string LatestFileName = "battle_latest.png";
    private const string MarkerFileName = ".last_capture";
    private const int CaptureWidth = 2560;
    private const int CaptureHeight = 1080;

    [MenuItem("SM/Internal/Capture/Battle Scene")]
    public static void CaptureBattleSceneFromMenu()
    {
        Capture(addPreviewSunIfMissing: true);
    }

    public static string Capture(bool addPreviewSunIfMissing)
    {
        if (!EnsureBattleSceneOpen())
        {
            Debug.LogError($"[BattleSceneCaptureTool] Failed to open {BattleScenePath}");
            return null;
        }

        AssetDatabase.ImportAsset(GroundMaterialPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(RoadMaterialPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        GameObject previewRoot = null;
        GameObject tempLight = null;

        try
        {
            previewRoot = new GameObject("__BattlePreviewRoot")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
            if (mapPrefab == null)
            {
                Debug.LogError($"[BattleSceneCaptureTool] Map prefab not found at {MapPrefabPath}");
                return null;
            }

            var mapInstance = Object.Instantiate(mapPrefab, previewRoot.transform);
            mapInstance.name = "PreviewWolfPineMap";
            SetHideFlagsRecursively(mapInstance, HideFlags.HideAndDontSave);

            var preExistingMat = mapInstance.GetComponent<BattleMapMaterialAdapter>();
            var preExistingEnv = mapInstance.GetComponent<BattleStageEnvironmentAdapter>();
            Debug.Log(
                $"[BattleCapture.Diag] prefab={mapPrefab.name} path={MapPrefabPath} " +
                $"preExistingMat={preExistingMat != null} preExistingEnv={preExistingEnv != null}");

            var materialAdapter = preExistingMat ?? mapInstance.AddComponent<BattleMapMaterialAdapter>();
            materialAdapter.Apply();

            var envAdapter = preExistingEnv ?? mapInstance.AddComponent<BattleStageEnvironmentAdapter>();
            envAdapter.ConfigureForestRuinsDefaults();
            envAdapter.Apply();

            if (addPreviewSunIfMissing)
            {
                tempLight = EnsurePreviewSun(previewRoot.transform);
            }

            return DoCapture();
        }
        finally
        {
            if (tempLight != null)
            {
                Object.DestroyImmediate(tempLight);
            }

            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot);
            }
        }
    }

    private static bool EnsureBattleSceneOpen()
    {
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.path == BattleScenePath)
        {
            return true;
        }

        var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        return scene.IsValid();
    }

    private static void SetHideFlagsRecursively(GameObject root, HideFlags flags)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.hideFlags = flags;
        }
    }

    private static GameObject EnsurePreviewSun(Transform parent)
    {
        var existingLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var existing in existingLights)
        {
            if (existing.type == LightType.Directional && existing.enabled && existing.gameObject.activeInHierarchy)
            {
                return null;
            }
        }

        var lightingRoot = new GameObject("__BattlePreviewLighting")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        lightingRoot.transform.SetParent(parent, false);

        var keyGo = new GameObject("PreviewKey");
        keyGo.transform.SetParent(lightingRoot.transform, false);
        keyGo.transform.rotation = Quaternion.Euler(38f, -48f, 0f);
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1.00f, 0.92f, 0.78f, 1f);
        key.intensity = 2.80f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.97f;
        key.shadowBias = 0.01f;
        key.shadowNormalBias = 0.05f;
        key.shadowBias = 0.02f;
        key.shadowNormalBias = 0.10f;
        key.shadowNearPlane = 0.10f;
        key.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
        RenderSettings.sun = key;

        var fillGo = new GameObject("PreviewFill");
        fillGo.transform.SetParent(lightingRoot.transform, false);
        fillGo.transform.rotation = Quaternion.Euler(35f, 130f, 0f);
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.50f, 0.55f, 0.62f, 1f);
        fill.intensity = 0.12f;
        fill.shadows = LightShadows.None;

        AddPointAccent(lightingRoot.transform, "WarmAccent", new Vector3(-5.8f, 2.4f, 4.6f), new Color(1f, 0.62f, 0.24f, 1f), 2.2f, 9f);

        // ShadowsOnly trees — mesh invisible, shadows fall onto play area
        AddForegroundTree(parent, "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_03.prefab",
            new Vector3(5.8f, 0f, 5.5f), 1.70f, 22f);
        AddForegroundTree(parent, "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_07.prefab",
            new Vector3(-5.2f, 0f, 4.0f), 1.65f, -42f);
        AddForegroundTree(parent, "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_09.prefab",
            new Vector3(0.5f, 0f, 7.2f), 1.75f, 108f);

        return lightingRoot;
    }

    private static void AddForegroundTree(Transform parent, string prefabPath, Vector3 localPosition, float scale, float yawDegrees)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        var instance = Object.Instantiate(prefab, parent);
        instance.hideFlags = HideFlags.HideAndDontSave;
        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        instance.transform.localScale = Vector3.one * scale;
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            r.receiveShadows = false;
        }
    }

    private static void AddPointAccent(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private static string DoCapture()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("[BattleSceneCaptureTool] No Main Camera in Battle scene.");
            return null;
        }

        Directory.CreateDirectory(CaptureDirectory);

        var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.DefaultHDR)
        {
            antiAliasing = 4
        };

        var previousActive = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        Texture2D tex = null;

        var urpData = camera.gameObject.GetComponent<UniversalAdditionalCameraData>();
        if (urpData == null)
        {
            urpData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }
        var previousRenderPostProcessing = urpData.renderPostProcessing;
        urpData.renderPostProcessing = true;

        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;

            tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
            tex.Apply();

            var bytes = tex.EncodeToPNG();
            var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var stampedPath = Path.Combine(CaptureDirectory, $"battle_{stamp}.png");
            var latestPath = Path.Combine(CaptureDirectory, LatestFileName);
            File.WriteAllBytes(stampedPath, bytes);
            File.WriteAllBytes(latestPath, bytes);
            File.WriteAllText(Path.Combine(CaptureDirectory, MarkerFileName), stamp);

            Debug.Log($"[BattleSceneCaptureTool] Captured {CaptureWidth}x{CaptureHeight} → {latestPath}");
            return latestPath;
        }
        finally
        {
            urpData.renderPostProcessing = previousRenderPostProcessing;
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            rt.Release();
            Object.DestroyImmediate(rt);

            if (tex != null)
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
