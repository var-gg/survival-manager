using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity;

/// <summary>
/// 전투 씬용 시각 통합 authoring 컴포넌트.
/// Battle.unity 씬에 pre-place하면 Edit 모드에서도 슬라이더로 즉시 preview,
/// Play 모드에서도 동일 값 적용. RenderSettings / Sun / Fill / Volume / Camera HDR 한 곳에서.
///
/// 사용:
/// 1. Battle.unity 씬에 빈 GameObject "BattleRenderEnvironment" 생성
/// 2. 이 컴포넌트 attach
/// 3. Inspector 상단의 "기본 프리셋" 버튼으로 baseline 적용
/// 4. 각 섹션 슬라이더로 fine-tune (실시간 Scene view 프리뷰)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BattleRenderEnvironmentAuthoring : MonoBehaviour
{
    // 기본 자산 경로 — Inspector에서 비워두면 자동으로 이걸 로드.
    private const string DefaultSkyboxPath = "Assets/TriForge Assets/Fantasy Worlds - Forest/Materials/M_fwOF_SkyBox_01.mat";
    private const string DefaultVolumeProfilePath = "Assets/TriForge Assets/Fantasy Worlds - Forest/Scenes/PostProcessing/VP_FW01_Summer.asset";

    [Header("실시간 프리뷰")]
    [Tooltip("켜져 있으면 슬라이더를 돌릴 때마다 Scene view에 즉시 반영됩니다. 끄면 'Force Apply' 버튼을 눌러야 적용.")]
    [SerializeField] private bool autoApplyInEditMode = true;

    [Header("환경광 (Ambient — Trilight 모드)")]
    [Tooltip("위쪽(하늘) 방향에서 오는 환경광. 보통 차가운 색.")]
    [SerializeField] private Color ambientSky = new(0.36f, 0.44f, 0.52f, 1f);
    [Tooltip("측면(수평) 방향에서 오는 환경광. 중간 톤.")]
    [SerializeField] private Color ambientEquator = new(0.38f, 0.41f, 0.39f, 1f);
    [Tooltip("아래(지면 반사)에서 오는 환경광. 보통 따뜻하고 어두움.")]
    [SerializeField] private Color ambientGround = new(0.14f, 0.13f, 0.10f, 1f);
    [Tooltip("환경광 전반 세기. 1.0이 기본, 1.5+로 올리면 전체 화면이 밝아짐.")]
    [Range(0f, 2f), SerializeField] private float ambientIntensity = 0.75f;

    [Header("스카이박스")]
    [Tooltip("Battle 씬 스카이박스 머티리얼. 비워두면 기존 설정 유지.")]
    [SerializeField] private Material? skybox;
    [Tooltip("카메라 Clear Flags를 강제로 Skybox로. 비우면 검은 배경.")]
    [SerializeField] private bool forceCameraClearToSkybox = true;

    [Header("포그 (Fog)")]
    [Tooltip("⚠ Stylized cel 셰이더 호환성 주의 — Quibli/lilToon은 URP fog 처리가 불완전해서 " +
             "켜면 leaf/grass/character edge에 fog clear color가 파란 형광처럼 새어 나옴. " +
             "전투 미리보기에서는 항상 OFF 권장. 분위기 fog가 필요하면 URP Volume의 Bloom/Color로 대체.")]
    [SerializeField] private bool fogEnabled;
    [Tooltip("Linear: start~end 거리 사이 선형 / ExponentialSquared: 거리 제곱으로 짙어짐.")]
    [SerializeField] private FogMode fogMode = FogMode.Linear;
    [Tooltip("포그 색. 거리에 따라 화면이 이 색으로 페이드됨.")]
    [SerializeField] private Color fogColor = new(0.28f, 0.34f, 0.38f, 1f);
    [Tooltip("Linear 모드: 이 거리부터 포그 시작 (카메라 기준 m).")]
    [Range(0f, 200f), SerializeField] private float fogStart = 22f;
    [Tooltip("Linear 모드: 이 거리에서 포그 100%.")]
    [Range(1f, 400f), SerializeField] private float fogEnd = 90f;
    [Tooltip("Exponential 모드 전용 밀도. Linear에선 무시됨.")]
    [Range(0f, 0.05f), SerializeField] private float fogDensity = 0.01f;

    [Header("햇빛 (Sun Directional Key)")]
    [Tooltip("X=피치(위아래 각도, 0=수평, 90=수직), Y=요(좌우 각도). 그림자 방향을 결정.")]
    [SerializeField] private Vector3 sunRotationEuler = new(40f, -55f, 0f);
    [Tooltip("햇빛 색. 따뜻하게: (1, 0.86, 0.66) / 중성: 흰색 / 노을: (1, 0.62, 0.30)")]
    [ColorUsage(false, true), SerializeField] private Color sunColor = new(1f, 0.86f, 0.66f, 1f);
    [Tooltip("햇빛 세기. 2~3이 자연스러운 일중. 5+는 매우 강함.")]
    [Range(0f, 5f), SerializeField] private float sunIntensity = 2.4f;
    [Tooltip("그림자 종류. Soft=부드러운 그림자 / Hard=날카로운 / None=그림자 없음.")]
    [SerializeField] private LightShadows sunShadowType = LightShadows.Soft;
    [Tooltip("그림자 진하기. 1=꽉 찬 검정, 0.5=절반.")]
    [Range(0f, 1f), SerializeField] private float sunShadowStrength = 1.0f;
    [Tooltip("그림자 캐스터로부터 거리 보정. 너무 크면 그림자가 떠 보이고, 너무 작으면 자기 자신에 그림자 (acne).")]
    [Range(0f, 0.1f), SerializeField] private float sunShadowBias = 0.005f;
    [Tooltip("법선 방향 그림자 보정. 그림자 가장자리 깨끗하게.")]
    [Range(0f, 1f), SerializeField] private float sunShadowNormalBias = 0.03f;

    [Header("필 라이트 (Fill — 그림자 없는 보조광)")]
    [Tooltip("Sun과 반대 방향에서 오는 약한 보조광. 그림자 영역이 완전 검정 되지 않게.")]
    [SerializeField] private Vector3 fillRotationEuler = new(35f, 135f, 0f);
    [Tooltip("필 색. 보통 차가운 톤 (Sun이 따뜻한 경우 보색 대비).")]
    [ColorUsage(false, true), SerializeField] private Color fillColor = new(0.34f, 0.42f, 0.52f, 1f);
    [Tooltip("필 세기. 0.05~0.20이 적절. 너무 높으면 그림자가 사라지는 느낌.")]
    [Range(0f, 1f), SerializeField] private float fillIntensity = 0.08f;

    [Header("카메라 HDR · 후처리 (URP)")]
    [Tooltip("Camera에 UniversalAdditionalCameraData를 강제 attach. URP 후처리 작동에 필수.")]
    [SerializeField] private bool forceCameraUACD = true;
    [Tooltip("URP 후처리 패스 활성화. 끄면 Volume 효과가 안 보임.")]
    [SerializeField] private bool renderPostProcessing = true;
    [Tooltip("카메라 내부 HDR 렌더링. Bloom 같은 효과에 필요. 끄면 LDR 렌더링.")]
    [SerializeField] private bool allowHDR = true;
    [Tooltip("HDR 디스플레이로 출력. 일반 SDR 모니터에선 false 권장 (사용자 캡쳐와 톤 일치).")]
    [SerializeField] private bool allowHDROutput;

    [Header("Volume 후처리 — 원본 프로파일")]
    [Tooltip("기반 VolumeProfile asset (예: TriForge VP_FW01_Summer). 런타임에 instantiate 복사해서 사용하므로 원본은 안 건드림. " +
             "비우면 후처리 기능 비활성.")]
    [SerializeField] private VolumeProfile? sourceVolumeProfile;

    [Header("Volume 후처리 — Bloom (블룸)")]
    [Tooltip("밝은 픽셀의 발광 효과 세기. 0=없음, 0.05~0.15 자연, 0.3+ 강함. SDR 디스플레이에선 ~50% 채도 손실 보정 필요.")]
    [Range(0f, 3f), SerializeField] private float bloomIntensity = 0.08f;
    [Tooltip("이 밝기 이상의 픽셀만 bloom. 1.0이 SDR 최대값. 1.3이면 HDR 영역만 발광.")]
    [Range(0f, 3f), SerializeField] private float bloomThreshold = 1.30f;
    [Tooltip("Bloom 퍼짐 범위. 작을수록 점광, 클수록 큰 후광.")]
    [Range(0f, 1f), SerializeField] private float bloomScatter = 0.55f;
    [Tooltip("Bloom 색조. 흰색=중성, 따뜻=(1, 0.62, 0.24) sunset, 차가움=(0.5, 0.7, 1)")]
    [ColorUsage(false, true), SerializeField] private Color bloomTint = Color.white;

    [Header("Volume 후처리 — Color Adjustments (색 보정)")]
    [Tooltip("노출 보정. 양수=밝게, 음수=어둡게. ±0.3 정도가 자연.")]
    [Range(-3f, 3f), SerializeField] private float postExposure;
    [Tooltip("대비. 양수=어두운 곳 더 어둡고 밝은 곳 더 밝게. 0=원본.")]
    [Range(-100f, 100f), SerializeField] private float contrast = 12f;
    [Tooltip("채도. 양수=색이 진해짐, 음수=흑백 쪽. SDR 디스플레이에서 ~50% 손실되므로 약간 over-push 권장.")]
    [Range(-100f, 100f), SerializeField] private float saturation = 6f;
    [Tooltip("전체에 곱해지는 색 필터. 약간 따뜻하게 = (1, 0.96, 0.90) / 차갑게 = (0.95, 0.98, 1) / 흰색 = 변화 없음.")]
    [ColorUsage(false), SerializeField] private Color colorFilter = Color.white;

    [Header("Volume 후처리 — Tonemap · Vignette")]
    [Tooltip("Neutral=가벼운 압축, ACES=영화같은 부드러운 압축 (채도 더 손실), None=압축 없음 (clip 위험).")]
    [SerializeField] private TonemappingMode tonemapMode = TonemappingMode.Neutral;
    [Tooltip("화면 가장자리 어둡게. 0~0.2가 자연. 0.3+면 시네마틱.")]
    [Range(0f, 1f), SerializeField] private float vignetteIntensity = 0.12f;
    [Tooltip("비네팅 부드러움. 1에 가까울수록 페이드 영역 넓음.")]
    [Range(0.01f, 1f), SerializeField] private float vignetteSmoothness = 0.42f;

    [Header("런타임 적용 옵션")]
    [Tooltip("기존 BattleStageEnvironmentAdapter가 만들던 자동 sun/fill을 끄고 여기 값만 사용. " +
             "끄면 두 시스템이 충돌할 수 있으니 보통 켜둠.")]
    [SerializeField] private bool overrideRuntimeLightCreation = true;

    // ───── 내부 인스턴스 (Hierarchy에 자동 생성됨)
    private Transform? _envRoot;
    private Light? _sunLight;
    private Light? _fillLight;
    private Volume? _runtimeVolume;
    private VolumeProfile? _runtimeProfile;

    public bool OverrideRuntimeLightCreation => overrideRuntimeLightCreation;

    public void Apply()
    {
        EnsureDefaultAssetsLoaded();
        EnsureChildRoot();
        DestroyOrphanRuntimeVolumes();
        ApplyRenderSettings();
        ApplyLights();
        ApplyCamera();
        ApplyVolume();
        ForceShadowCastingOnSceneRenderers();
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// BattleStageEnvironmentAdapter가 만든 "__BattleVolume" 같은 중복 global Volume이
    /// 씬에 남아 있으면 authoring의 Volume과 priority 충돌해 톤이 망가짐. Apply마다 정리.
    /// </summary>
    private void DestroyOrphanRuntimeVolumes()
    {
        var allVolumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var v in allVolumes)
        {
            if (v == null) continue;
            if (!v.isGlobal) continue;
            var n = v.gameObject.name;
            // 우리 Authoring이 만든 BattleVolume은 보존, 그 외 runtime 충돌 volume은 제거.
            if (n == "BattleVolume") continue;
            if (n == "__BattleVolume")
            {
                if (Application.isPlaying) Destroy(v.gameObject);
                else DestroyImmediate(v.gameObject);
            }
        }
    }

    /// <summary>
    /// 씬 안의 모든 mesh renderer가 shadow를 cast/receive 하도록 강제.
    /// 트리/바위 prefab이 shadow caster 비활성으로 ship된 경우 대비.
    /// ShadowsOnly marker가 붙은 오브젝트와 BattleActorContactShadow는 건너뜀.
    /// </summary>
    private void ForceShadowCastingOnSceneRenderers()
    {
        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            // UI layer skip
            if (renderer.gameObject.layer == 5) continue;
            // ShadowsOnly marker가 있는 오브젝트는 건드리지 않음
            if (renderer.GetComponentInParent<BattleShadowOnlyMarker>() != null) continue;
            // Contact shadow blob 자신은 cast 안 함
            if (renderer.gameObject.name.Contains("ContactShadow")) continue;
            if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private void EnsureDefaultAssetsLoaded()
    {
#if UNITY_EDITOR
        if (skybox == null)
        {
            skybox = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultSkyboxPath);
        }
        if (sourceVolumeProfile == null)
        {
            sourceVolumeProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumeProfilePath);
        }
#endif
    }

    private void EnsureChildRoot()
    {
        if (_envRoot != null)
        {
            return;
        }

        var existing = transform.Find("__EnvironmentRig");
        if (existing != null)
        {
            _envRoot = existing;
            return;
        }

        var go = new GameObject("__EnvironmentRig")
        {
            hideFlags = HideFlags.DontSaveInBuild
        };
        go.transform.SetParent(transform, false);
        _envRoot = go.transform;
    }

    private void ApplyRenderSettings()
    {
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = ambientIntensity;

        RenderSettings.fog = fogEnabled;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        if (fogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
        else
        {
            RenderSettings.fogDensity = fogDensity;
        }
    }

    private void ApplyLights()
    {
        if (_envRoot == null)
        {
            return;
        }

        _sunLight ??= FindOrCreateLight("BattleSun", _envRoot);
        if (_sunLight != null)
        {
            _sunLight.type = LightType.Directional;
            _sunLight.transform.rotation = Quaternion.Euler(sunRotationEuler);
            _sunLight.color = sunColor;
            _sunLight.intensity = sunIntensity;
            _sunLight.shadows = sunShadowType;
            _sunLight.shadowStrength = sunShadowStrength;
            _sunLight.shadowBias = sunShadowBias;
            _sunLight.shadowNormalBias = sunShadowNormalBias;
            _sunLight.shadowNearPlane = 0.10f;
            _sunLight.shadowResolution = LightShadowResolution.VeryHigh;
            RenderSettings.sun = _sunLight;
        }

        _fillLight ??= FindOrCreateLight("BattleFill", _envRoot);
        if (_fillLight != null)
        {
            _fillLight.type = LightType.Directional;
            _fillLight.transform.rotation = Quaternion.Euler(fillRotationEuler);
            _fillLight.color = fillColor;
            _fillLight.intensity = fillIntensity;
            _fillLight.shadows = LightShadows.None;
        }
    }

    private static Light? FindOrCreateLight(string name, Transform parent)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            return existing.GetComponent<Light>() ?? existing.gameObject.AddComponent<Light>();
        }

        var go = new GameObject(name)
        {
            hideFlags = HideFlags.DontSaveInBuild
        };
        go.transform.SetParent(parent, false);
        return go.AddComponent<Light>();
    }

    private void ApplyCamera()
    {
        if (!forceCameraUACD)
        {
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (forceCameraClearToSkybox)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
        }
        cam.allowHDR = allowHDR;

        var urpData = cam.GetComponent<UniversalAdditionalCameraData>()
                      ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderPostProcessing = renderPostProcessing;
        urpData.allowHDROutput = allowHDROutput;
    }

    private void ApplyVolume()
    {
        if (sourceVolumeProfile == null || _envRoot == null)
        {
            return;
        }

        if (_runtimeVolume == null)
        {
            var existing = _envRoot.Find("BattleVolume");
            if (existing != null)
            {
                _runtimeVolume = existing.GetComponent<Volume>();
            }
            if (_runtimeVolume == null)
            {
                var go = new GameObject("BattleVolume")
                {
                    hideFlags = HideFlags.DontSaveInBuild
                };
                go.transform.SetParent(_envRoot, false);
                _runtimeVolume = go.AddComponent<Volume>();
                _runtimeVolume.isGlobal = true;
                _runtimeVolume.priority = 100;
                _runtimeVolume.weight = 1f;
            }
        }

        if (_runtimeProfile == null || _runtimeProfile.name != $"{sourceVolumeProfile.name}_Runtime")
        {
            _runtimeProfile = Instantiate(sourceVolumeProfile);
            _runtimeProfile.name = $"{sourceVolumeProfile.name}_Runtime";
        }

        _runtimeVolume.profile = _runtimeProfile;
        TuneVolumeProfile(_runtimeProfile);
    }

    private void TuneVolumeProfile(VolumeProfile profile)
    {
        if (profile.TryGet<Bloom>(out var bloom))
        {
            bloom.active = true;
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.tint.Override(bloomTint);
            bloom.scatter.Override(bloomScatter);
        }

        if (profile.TryGet<ColorAdjustments>(out var ca))
        {
            ca.active = true;
            ca.postExposure.Override(postExposure);
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);
            ca.colorFilter.Override(colorFilter);
        }

        if (profile.TryGet<Tonemapping>(out var tm))
        {
            tm.active = true;
            tm.mode.Override(tonemapMode);
        }

        if (profile.TryGet<Vignette>(out var vignette))
        {
            vignette.active = true;
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);
        }

        if (profile.TryGet<DepthOfField>(out var dof)) dof.active = false;
        if (profile.TryGet<SplitToning>(out var st)) st.active = false;
        if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh)) smh.active = false;
        if (profile.TryGet<WhiteBalance>(out var wb))
        {
            wb.active = true;
            wb.temperature.Override(0f);
            wb.tint.Override(0f);
        }
        if (profile.TryGet<ChannelMixer>(out var cm)) cm.active = false;
        if (profile.TryGet<LiftGammaGain>(out var lgg)) lgg.active = false;
    }

    // ───── Presets

    public void ApplyGameplayPreset()
    {
        ambientSky = new Color(0.36f, 0.44f, 0.52f, 1f);
        ambientEquator = new Color(0.38f, 0.41f, 0.39f, 1f);
        ambientGround = new Color(0.14f, 0.13f, 0.10f, 1f);
        ambientIntensity = 0.95f;

        fogEnabled = false;

        // 숲 stage 기준: 녹색 leaf 텍스처와 곱해졌을 때 yellow-green 폭주 안 하도록
        // 약하게-따뜻한 sun + 적당한 강도. cinematic은 별도 preset에서 강하게.
        sunRotationEuler = new Vector3(42f, -52f, 0f);
        sunColor = new Color(1f, 0.96f, 0.88f, 1f);
        sunIntensity = 1.55f;
        sunShadowType = LightShadows.Soft;
        sunShadowStrength = 1.0f;
        sunShadowBias = 0.005f;
        sunShadowNormalBias = 0.03f;

        fillRotationEuler = new Vector3(35f, 135f, 0f);
        fillColor = new Color(0.34f, 0.42f, 0.52f, 1f);
        fillIntensity = 0.10f;

        // Bloom은 LDR 디스플레이에서 노란 톤을 누적 증폭하므로 forest baseline은 매우 약하게.
        bloomIntensity = 0.04f;
        bloomThreshold = 1.40f;
        bloomScatter = 0.50f;
        bloomTint = Color.white;
        postExposure = 0f;
        contrast = 8f;
        saturation = 2f;
        colorFilter = Color.white;
        tonemapMode = TonemappingMode.Neutral;
        vignetteIntensity = 0.12f;
        vignetteSmoothness = 0.42f;

        Apply();
    }

    public void ApplyCinematicPreset()
    {
        ambientSky = new Color(0.42f, 0.55f, 0.70f, 1f);
        ambientEquator = new Color(0.50f, 0.56f, 0.58f, 1f);
        ambientGround = new Color(0.16f, 0.15f, 0.11f, 1f);
        ambientIntensity = 1.0f;

        // 포그는 stylized 셰이더(Quibli/lilToon)와 호환 안 됨 — 항상 OFF.
        // 분위기는 Bloom + Vignette + warm color filter로 대체.
        fogEnabled = false;
        fogMode = FogMode.Linear;
        fogColor = new Color(0.30f, 0.42f, 0.50f, 1f);
        fogStart = 12f;
        fogEnd = 55f;

        sunRotationEuler = new Vector3(34f, -52f, 0f);
        sunColor = new Color(1f, 0.82f, 0.54f, 1f);
        sunIntensity = 2.8f;
        sunShadowType = LightShadows.Soft;
        sunShadowStrength = 1.0f;

        fillRotationEuler = new Vector3(35f, 135f, 0f);
        fillColor = new Color(0.30f, 0.42f, 0.56f, 1f);
        fillIntensity = 0.14f;

        bloomIntensity = 0.30f;
        bloomThreshold = 1.10f;
        bloomScatter = 0.65f;
        bloomTint = new Color(1f, 0.78f, 0.48f, 1f);
        postExposure = 0.10f;
        contrast = 20f;
        saturation = 8f;
        colorFilter = Color.white;
        tonemapMode = TonemappingMode.ACES;
        vignetteIntensity = 0.22f;
        vignetteSmoothness = 0.38f;

        Apply();
    }

    public void ApplyDebugNeutralPreset()
    {
        ambientSky = new Color(0.30f, 0.36f, 0.42f, 1f);
        ambientEquator = new Color(0.34f, 0.36f, 0.34f, 1f);
        ambientGround = new Color(0.12f, 0.11f, 0.09f, 1f);
        ambientIntensity = 0.55f;

        fogEnabled = false;

        sunRotationEuler = new Vector3(50f, -45f, 0f);
        sunColor = Color.white;
        sunIntensity = 1.5f;
        sunShadowType = LightShadows.Soft;
        sunShadowStrength = 1.0f;

        fillIntensity = 0f;

        bloomIntensity = 0f;
        bloomThreshold = 1.5f;
        bloomTint = Color.white;
        postExposure = 0f;
        contrast = 0f;
        saturation = 0f;
        colorFilter = Color.white;
        tonemapMode = TonemappingMode.Neutral;
        vignetteIntensity = 0f;

        Apply();
    }

    // ───── Unity lifecycle

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        if (autoApplyInEditMode || Application.isPlaying)
        {
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Apply();
                }
            };
#else
            Apply();
#endif
        }
    }

    private void OnDrawGizmosSelected()
    {
        var sunDir = Quaternion.Euler(sunRotationEuler) * Vector3.forward;
        var origin = transform.position + Vector3.up * 5f;
        Gizmos.color = new Color(sunColor.r, sunColor.g, sunColor.b, 1f);
        Gizmos.DrawLine(origin, origin + sunDir * 4f);
        Gizmos.DrawWireSphere(origin + sunDir * 4f, 0.3f);

        if (fogEnabled && fogMode == FogMode.Linear)
        {
            Gizmos.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0.4f);
            var cam = Camera.main;
            var camPos = cam != null ? cam.transform.position : transform.position;
            Gizmos.DrawWireSphere(camPos, fogStart);
            Gizmos.DrawWireSphere(camPos, fogEnd);
        }
    }
}
