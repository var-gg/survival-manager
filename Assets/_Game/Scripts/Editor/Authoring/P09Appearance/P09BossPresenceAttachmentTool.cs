#nullable enable
using System.Collections.Generic;
using System.IO;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.P09Appearance
{

/// <summary>
/// P09는 인간형 하나만 만들고, 모든 유닛이 같은 키로 정규화된다. 그래서 전투 화면에서
/// 보스가 일반 병사와 실루엣이 같다 — 8명이 붙어 싸우는 걸 <b>보는</b> 게임에서 이건 가독성 결함이다.
///
/// 이 도구는 그 결함을 <b>강체 부착물</b>로 해소한다. P09 리그는 Humanoid(본 77개)라
/// <c>Head</c>/<c>Chest</c>/<c>shoulder_L·R</c>가 이름으로 잡히고, P09 자신이 이미
/// Shield·Staff를 스킨드가 아닌 <b>정적 메시</b>로 소켓에 달고 있다. 새 메커니즘이 필요 없다.
///
/// 못 하는 것: 변형이 필요한 것(흐르는 망토, 허리에서 접히는 로브, 체격 자체).
/// 그건 스킨 웨이트가 필요하고, 강체 부착으로는 어느 도구를 써도 안 된다.
///
/// 이 도구는 <b>판단용 렌더</b>를 만든다. 부착 전/후를 같은 화각으로 나란히 찍어서
/// "보스로 읽히는가"를 눈으로 결정하기 위한 것이다.
///
/// <para><b>2026-07-29 — ProBuilder 프리미티브로 4회 시도 후 폐기.</b>
/// 본 부착·캐릭터 공간 저작·치수 실측까지 전부 맞춘 뒤에도, 상자·원기둥·원뿔은 조각되고
/// 텍스처 입혀진 툰 캐릭터 옆에서 <b>"붙여 놓은 비계"</b>로 읽혔다. 실패 원인이 배치가 아니라
/// 조형이라 더 깎을 값이 없었다. ProBuilder의 제자리는 맵 건축(관문·계단·재판장 단·우리)이고,
/// 거기서는 프리미티브 형태가 오히려 정답이며 원거리에서 보인다.
///
/// 현재 방식은 <b>P09 자신의 정적 메시를 복제해 재배치</b>하는 것이다. 같은 제작자의 같은
/// 텍스처·같은 폴리 밀도라 화풍이 저절로 맞고, 조형 비용도 구매 비용도 0이다.
/// 이걸로도 부족하면 그때가 에셋 구매 시점이며, <b>부착 메커니즘은 이 도구 그대로</b> 쓰면 된다.</para>
/// </summary>
public static class P09BossPresenceAttachmentTool
{
    private const string VisualPrefabPath =
        "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Demo_Prefab/P09_Human_Combat_Demo Variant.prefab";
    private const string OutputFolder = "Logs/p09-boss-presence";
    private const int RenderWidth = 700;
    private const int RenderHeight = 1200;

    private static readonly Color Background = new(0.36f, 0.38f, 0.44f, 1f);

    [MenuItem("SM/Internal/P09/Render Boss Presence Comparison")]
    public static void RenderComparisonMenu()
    {
        const string characterId = "extra_sunken_bastion_adjudicator";
        Directory.CreateDirectory(OutputFolder);

        RenderOne(characterId, withAttachments: false, $"{characterId}_before.png");
        RenderOne(characterId, withAttachments: true, $"{characterId}_after.png");

        AssetDatabase.Refresh();
        Debug.Log($"[P09BossPresence] before/after written to {OutputFolder}.");
    }

    /// <summary>
    /// <b>같은 캐릭터를 실제 렌더 파이프라인(URP + lilToon)으로 찍는다.</b>
    ///
    /// 위의 비교 렌더는 <see cref="PreviewRenderUtility"/> 안에서 돌고, 거기서는 lilToon 이
    /// 안 잡혀 마젠타가 되므로 프로젝트가 <c>Hidden/SM/P09PreviewTintedUnlit</c> 로 갈아끼운다.
    /// 그 셰이더는 <b>조명이 아예 없다</b> — 텍스처를 샘플해 색을 곱하고 텍스처 자체 휘도로
    /// 가짜 음영 밴드를 만드는 게 전부다. 그래서 프리뷰 렌더는 납작하고 탁하게 나온다.
    /// 저작 판단(계열·머리색 고르기)에는 충분하지만 <b>색감·분위기 판단의 근거로 쓰면 안 된다.</b>
    ///
    /// 이 메뉴는 임시 카메라를 실제 씬에 세워 게임과 같은 경로로 렌더한다.
    /// 프리뷰 렌더와 나란히 놓고 "탁한 색이 파이프라인 문제인지 에셋 문제인지"를 판정하기 위한 것이다.
    /// </summary>
    [MenuItem("SM/Internal/P09/Render Boss Presence Comparison (Game Shaders)")]
    public static void RenderGameShaderComparisonMenu()
    {
        const string characterId = "extra_sunken_bastion_adjudicator";
        Directory.CreateDirectory(OutputFolder);

        RenderThroughGamePipeline(characterId, withAttachments: false, $"{characterId}_game_before.png");
        RenderThroughGamePipeline(characterId, withAttachments: true, $"{characterId}_game_after.png");

        AssetDatabase.Refresh();
        Debug.Log($"[P09BossPresence] game-shader renders written to {OutputFolder}.");
    }

    private static void RenderThroughGamePipeline(string characterId, bool withAttachments, string fileName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        if (prefab == null)
        {
            throw new FileNotFoundException($"Missing P09 visual prefab: {VisualPrefabPath}");
        }

        var preset = FindPreset(characterId);
        var generated = new List<Material>();
        GameObject? instance = null;
        GameObject? rig = null;
        RenderTexture? target = null;
        Texture2D? readback = null;
        var previousActive = RenderTexture.active;
        System.Action? restoreAmbient = null;
        try
        {
            instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.hideFlags = HideFlags.DontSave;
            if (preset != null)
            {
                preset.ApplyTo(instance.transform, generated);
            }

            P09PreviewPoseUtility.TryApplyDefaultIdlePose(instance, preset != null ? preset.SexId : 1);

            // 여기서는 머티리얼을 <b>갈아끼우지 않는다.</b> 그게 이 렌더의 전부다.
            if (withAttachments)
            {
                BuildAdjudicatorPresence(instance);
            }

            rig = new GameObject("__SM_P09GameShaderRig") { hideFlags = HideFlags.DontSave };

            // 조명을 지어내지 않는다. 전투 씬이 실제로 쓰는 BattleRenderEnvironmentAuthoring 의
            // 기본값을 그대로 옮긴다 — 각도·색·환경광까지. 임의 조명으로 찍으면 그건 게임도
            // 프리뷰도 아닌 세 번째 그림이 되고, 색감 판단의 근거가 되지 못한다.
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousSky = RenderSettings.ambientSkyColor;
            var previousEquator = RenderSettings.ambientEquatorColor;
            var previousGround = RenderSettings.ambientGroundColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.125f, 0.155f, 0.190f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.105f, 0.112f, 0.100f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.040f, 0.045f, 0.036f, 1f);
            restoreAmbient = () =>
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientSkyColor = previousSky;
                RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround;
            };

            var keyGo = new GameObject("Key") { hideFlags = HideFlags.DontSave };
            keyGo.transform.SetParent(rig.transform, false);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.35f;
            key.color = new Color(1f, 0.97f, 0.91f);
            keyGo.transform.rotation = Quaternion.Euler(44f, -50f, 0f);

            var fillGo = new GameObject("Fill") { hideFlags = HideFlags.DontSave };
            fillGo.transform.SetParent(rig.transform, false);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.color = new Color(0.78f, 0.84f, 1f);
            fillGo.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

            var camGo = new GameObject("Cam") { hideFlags = HideFlags.DontSave };
            camGo.transform.SetParent(rig.transform, false);
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.allowMSAA = true;
            camera.cullingMask = ~0;
            camera.fieldOfView = 30f;
            FrameCamera(camera, instance.transform);

            target = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            readback = new Texture2D(RenderWidth, RenderHeight, TextureFormat.RGBA32, false, false);
            readback.ReadPixels(new Rect(0, 0, RenderWidth, RenderHeight), 0, 0);
            readback.Apply();
            File.WriteAllBytes(Path.Combine(OutputFolder, fileName).Replace('\\', '/'), readback.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;
            restoreAmbient?.Invoke();
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }

            if (readback != null)
            {
                Object.DestroyImmediate(readback);
            }

            if (rig != null)
            {
                Object.DestroyImmediate(rig);
            }

            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }

            foreach (var material in generated)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }
        }
    }

    private static void RenderOne(string characterId, bool withAttachments, string fileName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        if (prefab == null)
        {
            throw new FileNotFoundException($"Missing P09 visual prefab: {VisualPrefabPath}");
        }

        var preset = FindPreset(characterId);
        var generated = new List<Material>();
        PreviewRenderUtility? renderer = null;
        GameObject? instance = null;
        Texture2D? texture = null;
        try
        {
            instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            if (preset != null)
            {
                preset.ApplyTo(instance.transform, generated);
            }

            P09PreviewPoseUtility.TryApplyDefaultIdlePose(instance, preset != null ? preset.SexId : 1);

            // 부착물을 <b>프리뷰 머티리얼 변환보다 먼저</b> 만든다. 부착물이 P09 자신의 메시 복제라
            // 원본 머티리얼을 들고 오는데, 변환 뒤에 붙이면 그것만 변환을 못 받아 프리뷰에서 마젠타로 뜬다.
            if (withAttachments)
            {
                BuildAdjudicatorPresence(instance);
            }

            var profile = CharacterShowcasePreviewApplier.LoadDefault();
            if (CharacterShowcasePreviewApplier.ShouldUseReadableMaterials(profile))
            {
                P09DetailPreservingPaletteTool.ApplyPreviewReadableMaterials(instance.transform, generated);
            }

            renderer = new PreviewRenderUtility();
            if (profile != null)
            {
                CharacterShowcasePreviewApplier.ApplyTo(renderer, profile);
            }

            renderer.AddSingleGO(instance);
            var camera = renderer.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Background;
            camera.allowMSAA = true;
            FrameCamera(camera, instance.transform);

            renderer.BeginStaticPreview(new Rect(0, 0, RenderWidth, RenderHeight));
            renderer.Render();
            texture = renderer.EndStaticPreview();
            File.WriteAllBytes(Path.Combine(OutputFolder, fileName).Replace('\\', '/'), texture.EncodeToPNG());
        }
        finally
        {
            renderer?.Cleanup();
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }

            foreach (var material in generated)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// 침몰한 보루의 재결관 — 학파 재판 권위. 2챕터 첫 벽이라 플레이어가 "보스"를 처음 인식하는 자리다.
    ///
    /// ProBuilder 프리미티브로 네 번 시도해 실패한 뒤의 두 번째 접근이다. 새 형상을 만들지 않고
    /// <b>P09 자신의 정적 메시를 복제해 재배치</b>한다. 같은 제작자의 같은 텍스처·같은 폴리 밀도라
    /// 화풍이 저절로 맞는다 — 조형 비용도, 구매 비용도 0이다.
    ///
    /// 실측 재고: Staff_001~004 는 높이 <b>1.19~1.22</b> 로 이 캐릭터(1.661) 대비 아주 큰 수직 요소고,
    /// Shield_001~005 는 지름 약 0.42 의 원반이다. 전자는 등 뒤 의장기, 후자는 어깨 원반(론델)이 된다.
    /// </summary>
    private static void BuildAdjudicatorPresence(GameObject instance)
    {
        var animator = instance.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning("[P09BossPresence] Humanoid animator를 찾지 못해 부착물을 건너뛴다.");
            return;
        }

        var root = instance.transform;
        var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Chest);
        var shoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);

        // 1) 등 뒤 의장기 — 머리끝(약 1.55) 위로 나와야 군중 속에서 먼저 보인다.
        //    지팡이 자체가 1.22 라 살짝만 키우고 뒤로 기울인다.
        if (chest != null)
        {
            var standard = CloneP09Part(instance, "Staff_004");
            if (standard != null)
            {
                AttachCentered(standard, chest, root, new Vector3(-0.10f, 0.26f, -0.13f),
                    new Vector3(-13f, 0f, 8f), 0.88f);
            }
        }

        // 2) 어깨 원반 — 방패를 축소해 오른쪽 어깨에만. 좌우 대칭이 깨지면 실루엣이 즉시 갈린다.
        //    RightShoulder 본은 쇄골 뿌리(x=0.035)라 삼각근까지 바깥으로 밀어낸다.
        if (shoulder != null)
        {
            var rondel = CloneP09Part(instance, "Shield_003");
            if (rondel != null)
            {
                AttachCentered(rondel, shoulder, root, new Vector3(0.135f, 0.045f, 0f),
                    new Vector3(0f, 0f, -74f), 0.62f);
            }
        }
    }

    /// <summary>
    /// P09 프리팹 안의 정적 파츠를 이름으로 찾아 복제한다. 선택되지 않은 파츠는 비활성 상태로
    /// 들어 있으므로 반드시 켜 준다 — 안 켜면 조용히 아무것도 안 보인다.
    /// </summary>
    private static GameObject? CloneP09Part(GameObject instance, string meshObjectName)
    {
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(includeInactive: true))
        {
            if (!string.Equals(filter.gameObject.name, meshObjectName, System.StringComparison.Ordinal))
            {
                continue;
            }

            var clone = Object.Instantiate(filter.gameObject);
            clone.name = "BossPresence_" + meshObjectName;
            clone.hideFlags = HideFlags.DontSave;
            clone.SetActive(true);
            foreach (var child in clone.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                child.gameObject.SetActive(true);
            }

            return clone;
        }

        Debug.LogWarning($"[P09BossPresence] P09 파츠를 찾지 못했다: {meshObjectName}");
        return null;
    }

    /// <summary>
    /// 원본 파츠의 <b>피벗 위치를 모른다</b>. 손에 쥐게 만든 피벗이라 중심과 한참 떨어져 있을 수 있어서,
    /// 로컬 좌표로 놓으면 어디로 갈지 예측이 안 된다. 그래서 붙인 뒤 실제 렌더러 bounds 중심을 재고
    /// 원하는 지점으로 밀어 넣는다 — 저작값이 곧 "이 부품의 한가운데가 놓일 자리"가 된다.
    /// </summary>
    private static void AttachCentered(
        GameObject go,
        Transform bone,
        Transform root,
        Vector3 characterSpaceCenter,
        Vector3 characterSpaceEuler,
        float uniformScale)
    {
        go.transform.SetParent(bone, worldPositionStays: false);
        go.transform.rotation = root.rotation * Quaternion.Euler(characterSpaceEuler);

        var parentScale = bone.lossyScale;
        go.transform.localScale = new Vector3(
            uniformScale / Mathf.Max(Mathf.Abs(parentScale.x), 1e-5f),
            uniformScale / Mathf.Max(Mathf.Abs(parentScale.y), 1e-5f),
            uniformScale / Mathf.Max(Mathf.Abs(parentScale.z), 1e-5f));

        go.transform.position = Vector3.zero;
        if (!TryGetWorldBounds(go, out var bounds))
        {
            go.transform.position = bone.position + root.rotation * characterSpaceCenter;
            return;
        }

        var desired = bone.position + root.rotation * characterSpaceCenter;
        go.transform.position += desired - bounds.center;
    }

    private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        var found = false;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(includeInactive: false))
        {
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return found;
    }


    /// <summary>
    /// 이 캐릭터가 실제로 쓰고 있는 머티리얼을 빌려온다. 부착물이 같은 셰이더·같은 톤으로
    /// 그려져야 한 벌로 보인다 — 색만 맞춘 단색 머티리얼로는 안 된다.
    /// </summary>


    /// <summary>
    /// <b>본 로컬 공간에 직접 저작하지 않는다.</b> Humanoid 본은 축이 임의로 회전·스케일돼 있어서
    /// "뒤로 0.16" 같은 직관적 오프셋이 전혀 다른 방향으로 간다 — 첫 렌더에서 깃대가 옆으로 눕고
    /// 첨두가 허공에 떠올랐다. 그래서 부모는 본으로 두되(애니메이션은 그대로 타야 하니까),
    /// 위치·회전은 <b>캐릭터 공간</b>(x=오른쪽, y=위, z=정면)에서 잡고 스케일은 부모 스케일을 상쇄한다.
    /// 저작값이 곧 화면에서 보이는 값이 된다.
    /// </summary>
    private static void Attach(
        GameObject go,
        Transform bone,
        Transform root,
        Vector3 characterSpaceOffset,
        Vector3 characterSpaceEuler,
        Vector3 worldSize)
    {
        go.transform.SetParent(bone, worldPositionStays: false);
        go.transform.position = bone.position + root.rotation * characterSpaceOffset;
        go.transform.rotation = root.rotation * Quaternion.Euler(characterSpaceEuler);

        var parentScale = bone.lossyScale;
        go.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(Mathf.Abs(parentScale.x), 1e-5f),
            worldSize.y / Mathf.Max(Mathf.Abs(parentScale.y), 1e-5f),
            worldSize.z / Mathf.Max(Mathf.Abs(parentScale.z), 1e-5f));
    }


    private static BattleP09AppearancePreset? FindPreset(string characterId)
    {
        foreach (var preset in Resources.LoadAll<BattleP09AppearancePreset>(BattleP09AppearancePreset.ResourcesFolder))
        {
            if (preset != null && string.Equals(preset.CharacterId, characterId, System.StringComparison.Ordinal))
            {
                return preset;
            }
        }

        return null;
    }

    /// <summary>
    /// 부착 전/후를 <b>같은 화각</b>으로 찍는다. 부착물 때문에 bounds가 커져 카메라가 물러나면
    /// 커진 실루엣이 렌더에서 상쇄돼 비교가 무의미해진다 — 그래서 몸통 기준으로 고정한다.
    /// </summary>
    private static void FrameCamera(Camera camera, Transform root)
    {
        var target = new Vector3(0f, 1.16f, 0f);
        var distance = 4.85f;
        var direction = Quaternion.Euler(3f, -28f, 0f) * Vector3.forward;
        camera.transform.position = target + direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = distance * 4f;
    }
}

}
