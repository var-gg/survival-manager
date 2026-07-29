using System.IO;
using SM.Editor.Bootstrap;
using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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

    [MenuItem("SM/Internal/Capture/Battle Live (Game View)")]
    public static void CaptureBattleLive()
    {
        // Synchronous Camera.Render → HDR RT → PNG.
        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("[BattleSceneCaptureTool] CaptureLive: no Main Camera found.");
            return;
        }

        // Diagnostic: dump runtime render state at capture time.
        var hdr = camera.allowHDR;
        var clearFlags = camera.clearFlags;
        var skybox = RenderSettings.skybox != null ? RenderSettings.skybox.name : "<null>";
        var fog = RenderSettings.fog;
        var fogMode = RenderSettings.fogMode;
        var ambientMode = RenderSettings.ambientMode;
        var ambientSky = RenderSettings.ambientSkyColor;
        var ambientIntensity = RenderSettings.ambientIntensity;
        var sun = RenderSettings.sun != null ? RenderSettings.sun.name : "<null>";
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        var globalVolumes = string.Join(", ", System.Array.ConvertAll(volumes, v =>
            $"{v.name}(p={v.priority},g={v.isGlobal},w={v.weight},prof={(v.profile != null ? v.profile.name : "<null>")})"));
        Debug.Log($"[CaptureLive.Diag] playing={EditorApplication.isPlaying}, hdr={hdr}, clearFlags={clearFlags}, skybox={skybox}, fog={fog}/{fogMode}, ambientMode={ambientMode}, ambientSky={ambientSky}, ambientIntensity={ambientIntensity}, sun={sun}, volumes=[{globalVolumes}]");

        var urpData = camera.gameObject.GetComponent<UniversalAdditionalCameraData>()
                      ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var previousRenderPostProcessing = urpData.renderPostProcessing;
        urpData.renderPostProcessing = true;

        Directory.CreateDirectory(CaptureDirectory);
        var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.DefaultHDR)
        {
            antiAliasing = 4
        };
        var previousActive = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        Texture2D tex = null;

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
            var stampedPath = Path.Combine(CaptureDirectory, $"battle_live_{stamp}.png");
            var latestPath = Path.Combine(CaptureDirectory, LatestFileName);
            File.WriteAllBytes(stampedPath, bytes);
            File.WriteAllBytes(latestPath, bytes);
            File.WriteAllText(Path.Combine(CaptureDirectory, MarkerFileName), stamp);

            LastCaptureLuminance = MeanLuminance(tex);
            Debug.Log($"[BattleSceneCaptureTool] LIVE captured {CaptureWidth}x{CaptureHeight} " +
                      $"(mean luminance {LastCaptureLuminance:0.000}) → {latestPath}");
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

    [MenuItem("SM/Internal/Capture/Battle Play Auto")]
    public static void StartPlayAutoCapture()
    {
        StartPlayAutoCapture(characterLightingAb: false);
    }

    /// <summary>
    /// 캐릭터 전용 조명 A/B 를 <b>한 플레이 세션 안에서</b> 두 장 찍는다.
    /// 시뮬 상태·카메라·후처리가 전부 동일한 상태에서 조명 하나만 움직이므로
    /// 두 장의 차이가 곧 조명의 기여분이고, <b>바뀐 픽셀이 곧 캐릭터 영역</b>이다
    /// (조명이 액터 렌더링 레이어에만 닿기 때문). 캐릭터가 화면의 4% 뿐이라
    /// 전체 평균 휘도로는 안 보이던 값 위계를 이 차분으로 직접 잴 수 있다.
    /// </summary>
    [MenuItem("SM/Internal/Capture/Battle Play Auto A-B (Character Lighting)")]
    public static void StartPlayAutoCaptureCharacterLightingAb()
    {
        StartPlayAutoCapture(characterLightingAb: true);
    }

    private static void StartPlayAutoCapture(bool characterLightingAb)
    {
        // 1. 전투 세션이 실제로 서는 경로로 Play 진입
        // 2. 런타임 게이트 통과 후 백버퍼 캡쳐
        // 3. Exit Play
        // State is persisted via SessionState across domain reload.
        if (!EnsureBattleSceneOpen())
        {
            Debug.LogError("[BattleSceneCaptureTool] PlayAuto: failed to open Battle scene.");
            return;
        }

        SessionState.SetBool(PlayAutoPendingKey, true);
        SessionState.SetInt(PlayAutoFrameKey, 0);
        SessionState.EraseBool(ScreenshotRequestedKey);
        SessionState.SetBool(AbModeKey, characterLightingAb);
        SessionState.SetInt(AbStepKey, 0);
        SessionState.SetInt(AbStepFrameKey, 0);

        try
        {
            DeleteIfExists(PlayModeScreenshotPath);
            if (characterLightingAb)
            {
                DeleteIfExists(AbOffScreenshotPath);
                DeleteIfExists(AbOnScreenshotPath);
            }
        }
        catch (System.Exception ex)
        {
            ClearPlayAutoState();
            Debug.LogError(
                $"[BattleSceneCaptureTool] PlayAuto: could not reset capture outputs before capture. {ex.Message}");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            // 이미 세션이 도는 중이면 그 세션에 얹는다.
            return;
        }

        // Battle 씬만 열고 Play 를 눌러 봐야 세션이 없어 시뮬이 안 돈다 — 실제로 stepIndex=-1,
        // activeWrappers=0 로 게이트가 영원히 안 열린다. 전투를 실제로 세우는 경로로 들어간다.
        FirstPlayableBootstrap.PlayCombatSandbox();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private const string PlayAutoPendingKey = "SM.BattleCapture.PlayAutoPending";
    private const string PlayAutoFrameKey = "SM.BattleCapture.PlayAutoFrame";
    /// <summary>
    /// 전투가 실제로 시작될 때까지 기다릴 상한. 360(약 6초)이었는데 <b>너무 짧아서</b>
    /// 세션이 정상적으로 서는 중에 타임아웃이 났고, 타임아웃이 <see cref="EditorApplication.ExitPlaymode"/>를
    /// 부르면서 전투 샌드박스 EditorPref 까지 지워 <b>다음 시도까지 오염시켰다</b>
    /// (그 다음 Play 는 샌드박스 레인이 아닌 direct-scene fallback 으로 들어가 인카운터 해석에 실패했다).
    /// 게이트가 상태 기반이라 상한을 넉넉히 잡아도 비용이 없다 — 조건이 서면 즉시 찍는다.
    /// </summary>
    private const int PlayAutoReadyTimeoutFrames = 3600;

    /// <summary>내용이 안 보이면 이 프레임 수만큼 더 기다리며 다시 찍는다.</summary>
    private const int PlayAutoMaxExtraFrames = 1800;

    /// <summary>PlayMode 백버퍼 캡쳐 산출물. 화면에 나간 프레임 그대로이므로 이쪽이 정본이다.</summary>
    private const string PlayModeScreenshotPath = "Captures/battle_playmode.png";

    private const string ScreenshotRequestedKey = "SM.BattleCapture.ScreenshotRequested";

    private const string AbModeKey = "SM.BattleCapture.AbCharacterLighting";
    private const string AbStepKey = "SM.BattleCapture.AbStep";
    private const string AbStepFrameKey = "SM.BattleCapture.AbStepFrame";
    private const string AbOffScreenshotPath = "Captures/battle_ab_charlight_off.png";
    private const string AbOnScreenshotPath = "Captures/battle_ab_charlight_on.png";

    /// <summary>조명 토글은 다음 렌더부터 반영된다. 셔터를 그만큼 늦춰야 이전 프레임을 찍지 않는다.</summary>
    private const int AbSettleFrames = 6;

    private const int AbFileWaitFrames = 900;

    /// <summary>이 값 아래면 사실상 검은 화면이다. 순검정 PNG 를 "성공"으로 보고하지 않기 위한 바닥.</summary>
    private const float MinUsefulCaptureLuminance = 0.012f;

    /// <summary>마지막 캡쳐의 평균 휘도. 캡쳐가 내용을 담았는지 판정하는 유일한 근거다.</summary>
    public static float LastCaptureLuminance { get; private set; }

    private static float MeanLuminance(Texture2D texture)
    {
        var pixels = texture.GetPixels32();
        if (pixels.Length == 0)
        {
            return 0f;
        }

        double total = 0d;
        const int stride = 97; // 소수 간격으로 성글게 — 2560x1080 전수는 필요 없다.
        var samples = 0;
        for (var i = 0; i < pixels.Length; i += stride)
        {
            var p = pixels[i];
            total += (0.299d * p.r + 0.587d * p.g + 0.114d * p.b) / 255d;
            samples++;
        }

        return samples == 0 ? 0f : (float)(total / samples);
    }

    [InitializeOnLoadMethod]
    private static void RegisterPlayAutoHooks()
    {
        EditorApplication.update -= OnPlayAutoUpdate;
        EditorApplication.update += OnPlayAutoUpdate;
    }

    private static void OnPlayAutoUpdate()
    {
        if (!SessionState.GetBool(PlayAutoPendingKey, false))
        {
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            return;
        }

        var frame = SessionState.GetInt(PlayAutoFrameKey, 0) + 1;
        SessionState.SetInt(PlayAutoFrameKey, frame);

        if (!TryGetBattleCaptureReadiness(out var readiness))
        {
            if (frame < PlayAutoReadyTimeoutFrames)
            {
                return;
            }

            FailPlayAutoCapture(
                $"timed out after {frame} frames waiting for the simulation to advance and all spawned actors " +
                $"to expose an active character renderer. Current state: {readiness}");
            return;
        }

        if (SessionState.GetBool(AbModeKey, false))
        {
            AdvanceCharacterLightingAb(frame, readiness);
            return;
        }

        // 프레임 수만 세고 찍으면 인트로 페이드가 남아 있을 때 순검정 PNG 가 저장된다 —
        // 툴은 "captured" 라고 보고하므로 조용한 실패다. 실제로 그렇게 한 장 나왔다.
        // 내용이 보일 때까지 다시 찍고, 끝내 안 보이면 실패로 명확히 남긴다.
        // 화면에 실제로 나간 프레임을 그대로 가져온다. camera.Render() 로 오프스크린 RT 에 그리는 건
        // <b>플레이어가 보는 프레임과 같은 경로가 아니다</b> — 최종 blit 후처리 적용 여부가 달라질 수 있고,
        // 이 프로젝트는 Linear 컬러 스페이스라 HDR RT 를 RGB24 로 ReadPixels 하면 sRGB 변환이 빠져
        // 저장본이 계통적으로 어두워진다. 그래서 PlayMode 에서는 백버퍼 캡쳐를 정본으로 삼는다.
        // (같은 기법을 TownPreviewCaptureUtility 가 이미 쓰고 있다.)
        if (!SessionState.GetBool(ScreenshotRequestedKey, false))
        {
            ScreenCapture.CaptureScreenshot(PlayModeScreenshotPath);
            SessionState.SetBool(ScreenshotRequestedKey, true);
            Debug.Log($"[BattleSceneCaptureTool] PlayAuto runtime gate passed at frame {frame}. {readiness}");
            return;
        }

        if (!File.Exists(PlayModeScreenshotPath))
        {
            if (frame < PlayAutoReadyTimeoutFrames + PlayAutoMaxExtraFrames)
            {
                return;
            }

            FailPlayAutoCapture(
                $"timed out waiting for backbuffer screenshot '{PlayModeScreenshotPath}' after the runtime gate passed. " +
                $"Current state: {readiness}");
            return;
        }

        // 비교용으로 기존 오프스크린 경로도 한 장 남긴다. 두 장이 다르면 그 차이가 곧 진단이다.
        CaptureBattleLive();
        if (LastCaptureLuminance < MinUsefulCaptureLuminance
            && frame < PlayAutoReadyTimeoutFrames + PlayAutoMaxExtraFrames)
        {
            return;
        }

        if (LastCaptureLuminance < MinUsefulCaptureLuminance)
        {
            Debug.LogError(
                $"[BattleSceneCaptureTool] 캡쳐가 거의 검다(평균 휘도 {LastCaptureLuminance:0.000}). " +
                $"런타임 게이트는 통과했지만 렌더 결과가 유효하지 않다. Current state: {readiness}");
        }

        ClearPlayAutoState();
        EditorApplication.ExitPlaymode();
    }

    /// <summary>
    /// 캐릭터 조명 OFF → ON 두 장을 같은 세션에서 순서대로 찍는다.
    /// 한 장씩 따로 돌리면 시뮬이 다른 순간에 있고 유닛 위치도 달라져 차분이 조명 기여분이 아니게 된다.
    /// </summary>
    private static void AdvanceCharacterLightingAb(int frame, string readiness)
    {
        var step = SessionState.GetInt(AbStepKey, 0);
        var stepFrame = SessionState.GetInt(AbStepFrameKey, 0);

        switch (step)
        {
            case 0:
                if (!TrySetCharacterLighting(false))
                {
                    FailPlayAutoCapture(
                        "A/B: no BattleRenderEnvironmentAuthoring in the running scene, so character lighting " +
                        $"could not be toggled. Current state: {readiness}");
                    return;
                }

                AdvanceAbStep(1, frame);
                return;

            case 1:
                if (frame - stepFrame < AbSettleFrames)
                {
                    return;
                }

                ScreenCapture.CaptureScreenshot(AbOffScreenshotPath);
                AdvanceAbStep(2, frame);
                return;

            case 2:
                if (!File.Exists(AbOffScreenshotPath))
                {
                    if (frame - stepFrame < AbFileWaitFrames)
                    {
                        return;
                    }

                    FailPlayAutoCapture(
                        $"A/B: lights-off frame '{AbOffScreenshotPath}' never landed. Current state: {readiness}");
                    return;
                }

                TrySetCharacterLighting(true);
                AdvanceAbStep(3, frame);
                return;

            case 3:
                if (frame - stepFrame < AbSettleFrames)
                {
                    return;
                }

                ScreenCapture.CaptureScreenshot(AbOnScreenshotPath);
                AdvanceAbStep(4, frame);
                return;

            default:
                if (!File.Exists(AbOnScreenshotPath))
                {
                    if (frame - stepFrame < AbFileWaitFrames)
                    {
                        return;
                    }

                    FailPlayAutoCapture(
                        $"A/B: lights-on frame '{AbOnScreenshotPath}' never landed. Current state: {readiness}");
                    return;
                }

                Debug.Log(
                    $"[BattleSceneCaptureTool] A/B captured at frame {frame}: " +
                    $"'{AbOffScreenshotPath}' (character lighting off) and '{AbOnScreenshotPath}' (on). {readiness}");
                ClearPlayAutoState();
                EditorApplication.ExitPlaymode();
                return;
        }
    }

    private static void AdvanceAbStep(int step, int frame)
    {
        SessionState.SetInt(AbStepKey, step);
        SessionState.SetInt(AbStepFrameKey, frame);
    }

    private static bool TrySetCharacterLighting(bool enabled)
    {
        var environment = Object.FindFirstObjectByType<BattleRenderEnvironmentAuthoring>();
        if (environment == null)
        {
            return false;
        }

        environment.SetCharacterLightingEnabled(enabled);
        return true;
    }

    private static bool TryGetBattleCaptureReadiness(out string state)
    {
        var screen = Object.FindFirstObjectByType<BattleScreenController>();
        var step = screen != null ? screen.LatestStep : null;
        var stepIndex = step?.StepIndex ?? -1;
        var expectedActorCount = step?.Units.Count ?? 0;
        var wrappers = Object.FindObjectsByType<BattleActorWrapper>(FindObjectsSortMode.None);
        var activeWrapperCount = 0;
        var wrappersWithCharacterRenderer = 0;
        var firstMissingCharacterRenderer = "<none>";

        foreach (var wrapper in wrappers)
        {
            if (!wrapper.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeWrapperCount++;
            var hasCharacterRenderer = false;
            foreach (var renderer in wrapper.GetComponentsInChildren<Renderer>(true))
            {
                if (IsCaptureReadyCharacterRenderer(renderer))
                {
                    hasCharacterRenderer = true;
                    break;
                }
            }

            if (hasCharacterRenderer)
            {
                wrappersWithCharacterRenderer++;
            }
            else if (firstMissingCharacterRenderer == "<none>")
            {
                firstMissingCharacterRenderer = wrapper.name;
            }
        }

        state =
            $"controller={(screen != null ? "present" : "missing")}, stepIndex={stepIndex}, " +
            $"expectedActors={expectedActorCount}, activeWrappers={activeWrapperCount}, " +
            $"wrappersWithCharacterRenderer={wrappersWithCharacterRenderer}, " +
            $"firstMissingCharacterRenderer={firstMissingCharacterRenderer}";

        return stepIndex > 0
               && expectedActorCount > 0
               && activeWrapperCount >= expectedActorCount
               && wrappersWithCharacterRenderer >= expectedActorCount;
    }

    private static bool IsCaptureReadyCharacterRenderer(Renderer renderer)
    {
        if (!renderer.enabled
            || renderer.forceRenderingOff
            || !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        return renderer.name != "ContactShadow"
               && renderer.name != "GroundShadow"
               && renderer.name != "PulseProxy";
    }

    private static void FailPlayAutoCapture(string reason)
    {
        Debug.LogError($"[BattleSceneCaptureTool] PlayAuto failed: {reason}");
        ClearPlayAutoState();
        EditorApplication.ExitPlaymode();
    }

    private static void ClearPlayAutoState()
    {
        SessionState.EraseBool(PlayAutoPendingKey);
        SessionState.EraseInt(PlayAutoFrameKey);
        SessionState.EraseBool(ScreenshotRequestedKey);
        SessionState.EraseBool(AbModeKey);
        SessionState.EraseInt(AbStepKey);
        SessionState.EraseInt(AbStepFrameKey);
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
        keyGo.transform.rotation = Quaternion.Euler(40f, -55f, 0f);
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1.00f, 0.86f, 0.66f, 1f);
        key.intensity = 2.40f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 1.0f;
        key.shadowBias = 0.005f;
        key.shadowNormalBias = 0.03f;
        key.shadowBias = 0.02f;
        key.shadowNormalBias = 0.10f;
        key.shadowNearPlane = 0.10f;
        key.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
        RenderSettings.sun = key;

        var fillGo = new GameObject("PreviewFill");
        fillGo.transform.SetParent(lightingRoot.transform, false);
        fillGo.transform.rotation = Quaternion.Euler(35f, 135f, 0f);
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.34f, 0.42f, 0.52f, 1f);
        fill.intensity = 0.08f;
        fill.shadows = LightShadows.None;

        AddPointAccent(lightingRoot.transform, "WarmAccent", new Vector3(-5.5f, 2.2f, 2.8f), new Color(1f, 0.52f, 0.20f, 1f), 1.5f, 7f);

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
