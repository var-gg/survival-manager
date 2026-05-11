using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity;

[DisallowMultipleComponent]
public sealed class BattleStageEnvironmentAdapter : MonoBehaviour
{
    private const string DefaultForestRuinsSkyboxPath = "Assets/TriForge Assets/Fantasy Worlds - Forest/Materials/M_fwOF_SkyBox_01.mat";
    private const string DefaultVolumeProfilePath = "Assets/TriForge Assets/Fantasy Worlds - Forest/Scenes/PostProcessing/VP_FW01_Summer.asset";

    [SerializeField] private Material skybox = null!;
    [SerializeField] private VolumeProfile volumeProfile = null!;
    [SerializeField] private Color ambientSky = new(0.40f, 0.50f, 0.62f, 1f);
    [SerializeField] private Color ambientEquator = new(0.46f, 0.50f, 0.50f, 1f);
    [SerializeField] private Color ambientGround = new(0.18f, 0.17f, 0.12f, 1f);
    [SerializeField, Range(0f, 3f)] private float ambientIntensity = 0.95f;
    [SerializeField] private bool applyFog = true;
    [SerializeField] private Color fogColor = new(0.66f, 0.74f, 0.82f, 1f);
    [SerializeField, Range(0f, 200f)] private float fogStart = 45f;
    [SerializeField, Range(1f, 400f)] private float fogEnd = 230f;
    [SerializeField] private bool applyCameraSkybox = true;

    private Volume? _runtimeVolume;

    private Material? _previousSkybox;
    private AmbientMode _previousAmbientMode;
    private Color _previousAmbientLight;
    private Color _previousAmbientSky;
    private Color _previousAmbientEquator;
    private Color _previousAmbientGround;
    private bool _previousFog;
    private FogMode _previousFogMode;
    private Color _previousFogColor;
    private float _previousFogDensity;
    private CameraClearFlags? _previousCameraClearFlags;
    private Camera? _camera;
    private bool _captured;

    private void Awake()
    {
        Apply();
    }

    public void ConfigureAuthoring(
        Material? skyboxMaterial,
        Color configuredAmbientSky,
        Color configuredAmbientEquator,
        Color configuredAmbientGround,
        Color configuredFogColor,
        float configuredFogStart,
        float configuredFogEnd)
    {
        skybox = skyboxMaterial!;
        ambientSky = configuredAmbientSky;
        ambientEquator = configuredAmbientEquator;
        ambientGround = configuredAmbientGround;
        fogColor = configuredFogColor;
        fogStart = configuredFogStart;
        fogEnd = configuredFogEnd;
    }

    public void ConfigureForestRuinsDefaults()
    {
#if UNITY_EDITOR
        if (skybox == null)
        {
            skybox = AssetDatabase.LoadAssetAtPath<Material>(DefaultForestRuinsSkyboxPath);
        }
        if (volumeProfile == null)
        {
            volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumeProfilePath);
        }
#endif
        ambientSky = new Color(0.36f, 0.44f, 0.52f, 1f);
        ambientEquator = new Color(0.38f, 0.41f, 0.39f, 1f);
        ambientGround = new Color(0.14f, 0.13f, 0.10f, 1f);
        ambientIntensity = 1.05f;
        applyFog = false;
        fogColor = new Color(0.28f, 0.34f, 0.38f, 1f);
        fogStart = 22f;
        fogEnd = 70f;
    }

    public void Apply()
    {
        CapturePreviousState();

        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = ambientIntensity;

        RenderSettings.fog = applyFog;
        if (applyFog)
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        if (applyCameraSkybox && Camera.main != null)
        {
            _camera = Camera.main;
            _previousCameraClearFlags ??= _camera.clearFlags;
            _camera.clearFlags = CameraClearFlags.Skybox;

            // GPT-Pro 진단 검증으로 발견: Battle.unity Camera에 UACD가 없어서
            // URP가 default UACD를 attach하는데 renderPostProcessing=false라
            // Volume override가 Play 모드에서 적용 안 됨. 강제로 켠다.
            var urpData = _camera.GetComponent<UniversalAdditionalCameraData>()
                          ?? _camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = true;
            urpData.allowHDROutput = false;
            // HDR rendering ON for proper Volume Bloom/ColorAdjustments computation;
            // allowHDROutput OFF so backbuffer is SDR (matches typical user display).
            _camera.allowHDR = true;
        }

        EnsureGlobalVolume();

        DynamicGI.UpdateEnvironment();
    }

    private void EnsureGlobalVolume()
    {
        if (volumeProfile == null)
        {
            return;
        }

        if (_runtimeVolume == null)
        {
            var volumeGo = new GameObject("__BattleVolume");
            volumeGo.transform.SetParent(transform, false);
            _runtimeVolume = volumeGo.AddComponent<Volume>();
            _runtimeVolume.isGlobal = true;
            _runtimeVolume.priority = 100;
            _runtimeVolume.weight = 1f;
        }

        // Use an instance copy so our runtime tuning never mutates the vendor asset on disk.
        var runtimeProfile = Instantiate(volumeProfile);
        runtimeProfile.name = $"{volumeProfile.name}_Runtime";
        _runtimeVolume.profile = runtimeProfile;
        TuneRuntimeProfile(runtimeProfile);
    }

    private static void TuneRuntimeProfile(VolumeProfile profile)
    {
        // The asset's raw Bloom 1.65 + ColorAdjustments contrast 35 over-expose without the demo scene's
        // full point-light rig. Pull those down to values that read on our 4-light setup.
        // GPT-Pro recommended gameplay profile: subtle post-process, structure-first.
        // SDR-display calibrated: GPT-Pro values were tuned for linear/HDR view but our final blit is SDR.
        // Push saturation/contrast/exposure noticeably to compensate for SDR clamp losing chroma.
        if (profile.TryGet<Bloom>(out var bloom))
        {
            bloom.active = true;
            bloom.intensity.Override(0.12f);
            bloom.threshold.Override(1.30f);
            bloom.tint.Override(Color.white);
            bloom.scatter.Override(0.55f);
            bloom.clamp.Override(2.0f);
        }

        if (profile.TryGet<ColorAdjustments>(out var ca))
        {
            // SDR-display compensation: HDR space saturation gets compressed ~50% by Neutral tonemap.
            // Aggressive push to land vivid on user's monitor.
            ca.postExposure.Override(0.30f);
            ca.contrast.Override(28f);
            ca.saturation.Override(60f);
            ca.colorFilter.Override(Color.white);
        }

        if (profile.TryGet<Tonemapping>(out var tm))
        {
            tm.active = true;
            tm.mode.Override(TonemappingMode.Neutral);
        }

        if (profile.TryGet<DepthOfField>(out var dof))
        {
            dof.active = false;
        }

        if (profile.TryGet<Vignette>(out var vignette))
        {
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.42f);
        }

        if (profile.TryGet<SplitToning>(out var split))
        {
            split.active = false;
        }

        if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh))
        {
            smh.active = false;
        }

        if (profile.TryGet<WhiteBalance>(out var wb))
        {
            wb.temperature.Override(4f);
            wb.tint.Override(2f);
        }

        if (profile.TryGet<ChannelMixer>(out var cm))
        {
            cm.active = false;
        }

        if (profile.TryGet<LiftGammaGain>(out var lgg))
        {
            lgg.active = false;
        }
    }

    private void CapturePreviousState()
    {
        if (_captured)
        {
            return;
        }

        _previousSkybox = RenderSettings.skybox;
        _previousAmbientMode = RenderSettings.ambientMode;
        _previousAmbientLight = RenderSettings.ambientLight;
        _previousAmbientSky = RenderSettings.ambientSkyColor;
        _previousAmbientEquator = RenderSettings.ambientEquatorColor;
        _previousAmbientGround = RenderSettings.ambientGroundColor;
        _previousFog = RenderSettings.fog;
        _previousFogMode = RenderSettings.fogMode;
        _previousFogColor = RenderSettings.fogColor;
        _previousFogDensity = RenderSettings.fogDensity;
        _captured = true;
    }

    private void OnDestroy()
    {
        if (!_captured)
        {
            return;
        }

        RenderSettings.skybox = _previousSkybox;
        RenderSettings.ambientMode = _previousAmbientMode;
        RenderSettings.ambientLight = _previousAmbientLight;
        RenderSettings.ambientSkyColor = _previousAmbientSky;
        RenderSettings.ambientEquatorColor = _previousAmbientEquator;
        RenderSettings.ambientGroundColor = _previousAmbientGround;
        RenderSettings.fog = _previousFog;
        RenderSettings.fogMode = _previousFogMode;
        RenderSettings.fogColor = _previousFogColor;
        RenderSettings.fogDensity = _previousFogDensity;

        if (_camera != null && _previousCameraClearFlags.HasValue)
        {
            _camera.clearFlags = _previousCameraClearFlags.Value;
        }

        DynamicGI.UpdateEnvironment();
    }
}
