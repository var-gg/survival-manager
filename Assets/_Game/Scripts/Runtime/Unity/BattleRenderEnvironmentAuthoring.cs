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
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BattleRenderEnvironmentAuthoring : MonoBehaviour
{
    [Header("Live Preview")]
    [Tooltip("Edit 모드에서 슬라이더 변경 시 즉시 적용. Play 모드에서는 항상 적용됨.")]
    [SerializeField] private bool autoApplyInEditMode = true;

    [Header("Ambient Light (Trilight mode)")]
    [SerializeField] private Color ambientSky = new(0.36f, 0.44f, 0.52f, 1f);
    [SerializeField] private Color ambientEquator = new(0.38f, 0.41f, 0.39f, 1f);
    [SerializeField] private Color ambientGround = new(0.14f, 0.13f, 0.10f, 1f);
    [Range(0f, 2f), SerializeField] private float ambientIntensity = 0.75f;

    [Header("Skybox")]
    [Tooltip("Battle 씬용 스카이박스 머티리얼. 비워두면 기존 RenderSettings.skybox 유지.")]
    [SerializeField] private Material? skybox;
    [SerializeField] private bool forceCameraClearToSkybox = true;

    [Header("Fog")]
    [SerializeField] private bool fogEnabled;
    [SerializeField] private FogMode fogMode = FogMode.Linear;
    [SerializeField] private Color fogColor = new(0.28f, 0.34f, 0.38f, 1f);
    [Range(0f, 200f), SerializeField] private float fogStart = 22f;
    [Range(1f, 400f), SerializeField] private float fogEnd = 90f;
    [Range(0f, 0.05f), SerializeField] private float fogDensity = 0.01f;

    [Header("Sun (Directional Key Light)")]
    [Tooltip("Sun 방향 (pitch, yaw, roll). pitch가 높을수록 위에서, yaw로 좌우 방향.")]
    [SerializeField] private Vector3 sunRotationEuler = new(40f, -55f, 0f);
    [ColorUsage(false, true), SerializeField] private Color sunColor = new(1f, 0.86f, 0.66f, 1f);
    [Range(0f, 5f), SerializeField] private float sunIntensity = 2.4f;
    [SerializeField] private LightShadows sunShadowType = LightShadows.Soft;
    [Range(0f, 1f), SerializeField] private float sunShadowStrength = 1.0f;
    [Range(0f, 0.1f), SerializeField] private float sunShadowBias = 0.005f;
    [Range(0f, 1f), SerializeField] private float sunShadowNormalBias = 0.03f;

    [Header("Fill (Directional, no shadow)")]
    [SerializeField] private Vector3 fillRotationEuler = new(35f, 135f, 0f);
    [ColorUsage(false, true), SerializeField] private Color fillColor = new(0.34f, 0.42f, 0.52f, 1f);
    [Range(0f, 1f), SerializeField] private float fillIntensity = 0.08f;

    [Header("Camera HDR (URP)")]
    [Tooltip("Camera UACD를 강제 attach + post-process / HDR output 설정. Edit-vs-Play disparity 해결.")]
    [SerializeField] private bool forceCameraUACD = true;
    [SerializeField] private bool renderPostProcessing = true;
    [SerializeField] private bool allowHDR = true;
    [SerializeField] private bool allowHDROutput;

    [Header("Volume Post-Process — Source Profile")]
    [Tooltip("Vendor profile (TriForge VP_FW01_Summer 등) reference. 비워두면 자체 default 사용. " +
             "런타임에서 Instantiate copy해서 사용하므로 원본 asset은 mutate 안 됨.")]
    [SerializeField] private VolumeProfile? sourceVolumeProfile;

    [Header("Volume Post-Process — Overrides")]
    [Tooltip("Bloom: HDR pixel만 bloom (threshold > 1) 권장. intensity는 SDR 디스플레이에서 압축됨.")]
    [Range(0f, 3f), SerializeField] private float bloomIntensity = 0.08f;
    [Range(0f, 3f), SerializeField] private float bloomThreshold = 1.30f;
    [Range(0f, 1f), SerializeField] private float bloomScatter = 0.55f;
    [ColorUsage(false, true), SerializeField] private Color bloomTint = Color.white;

    [Range(-3f, 3f), SerializeField] private float postExposure;
    [Range(-100f, 100f), SerializeField] private float contrast = 12f;
    [Range(-100f, 100f), SerializeField] private float saturation = 6f;
    [ColorUsage(false), SerializeField] private Color colorFilter = Color.white;

    [SerializeField] private TonemappingMode tonemapMode = TonemappingMode.Neutral;
    [Range(0f, 1f), SerializeField] private float vignetteIntensity = 0.12f;
    [Range(0.01f, 1f), SerializeField] private float vignetteSmoothness = 0.42f;

    [Header("Authoring 적용 옵션")]
    [Tooltip("기존 BattleStageEnvironmentAdapter가 만들던 자동 sun/fill을 끄고 여기 값만 사용.")]
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
        EnsureChildRoot();
        ApplyRenderSettings();
        ApplyLights();
        ApplyCamera();
        ApplyVolume();
        DynamicGI.UpdateEnvironment();
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

        // 추가 components 비활성 (gameplay에서 conflict 회피)
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
        ambientIntensity = 0.75f;

        fogEnabled = false;

        sunRotationEuler = new Vector3(40f, -55f, 0f);
        sunColor = new Color(1f, 0.86f, 0.66f, 1f);
        sunIntensity = 2.4f;
        sunShadowType = LightShadows.Soft;
        sunShadowStrength = 1.0f;
        sunShadowBias = 0.005f;
        sunShadowNormalBias = 0.03f;

        fillRotationEuler = new Vector3(35f, 135f, 0f);
        fillColor = new Color(0.34f, 0.42f, 0.52f, 1f);
        fillIntensity = 0.08f;

        bloomIntensity = 0.08f;
        bloomThreshold = 1.30f;
        bloomScatter = 0.55f;
        bloomTint = Color.white;
        postExposure = 0f;
        contrast = 12f;
        saturation = 6f;
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

        fogEnabled = true;
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
            // Defer to avoid OnValidate restrictions on creating GameObjects.
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
        // Sun direction visualization
        var sunDir = Quaternion.Euler(sunRotationEuler) * Vector3.forward;
        var origin = transform.position + Vector3.up * 5f;
        Gizmos.color = new Color(sunColor.r, sunColor.g, sunColor.b, 1f);
        Gizmos.DrawLine(origin, origin + sunDir * 4f);
        Gizmos.DrawWireSphere(origin + sunDir * 4f, 0.3f);

        // Fog range visualization
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
