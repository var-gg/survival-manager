using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity;

[DisallowMultipleComponent]
public sealed class BattleStageEnvironmentAdapter : MonoBehaviour
{
    private const string DefaultForestRuinsSkyboxPath = "Assets/Allsky/Day Sun Mid/Day Sun Mid SummerSky 3/Day Sun Mid SummerSky 3.mat";

    [SerializeField] private Material skybox = null!;
    [SerializeField] private Color ambientSky = new(0.52f, 0.58f, 0.56f, 1f);
    [SerializeField] private Color ambientEquator = new(0.34f, 0.33f, 0.29f, 1f);
    [SerializeField] private Color ambientGround = new(0.16f, 0.14f, 0.12f, 1f);
    [SerializeField] private bool applyFog = true;
    [SerializeField] private Color fogColor = new(0.42f, 0.45f, 0.40f, 1f);
    [SerializeField, Range(0.001f, 0.08f)] private float fogDensity = 0.018f;
    [SerializeField] private bool applyCameraSkybox = true;

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
        float configuredFogDensity)
    {
        skybox = skyboxMaterial!;
        ambientSky = configuredAmbientSky;
        ambientEquator = configuredAmbientEquator;
        ambientGround = configuredAmbientGround;
        fogColor = configuredFogColor;
        fogDensity = configuredFogDensity;
    }

    public void ConfigureForestRuinsDefaults()
    {
#if UNITY_EDITOR
        if (skybox == null)
        {
            skybox = AssetDatabase.LoadAssetAtPath<Material>(DefaultForestRuinsSkyboxPath);
        }
#endif
        ambientSky = new Color(0.92f, 0.96f, 0.96f, 1f);
        ambientEquator = new Color(0.66f, 0.70f, 0.54f, 1f);
        ambientGround = new Color(0.38f, 0.32f, 0.22f, 1f);
        fogColor = new Color(0.60f, 0.72f, 0.82f, 1f);
        fogDensity = 0.0032f;
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

        RenderSettings.fog = applyFog;
        if (applyFog)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        if (applyCameraSkybox && Camera.main != null)
        {
            _camera = Camera.main;
            _previousCameraClearFlags ??= _camera.clearFlags;
            _camera.clearFlags = CameraClearFlags.Skybox;
        }

        DynamicGI.UpdateEnvironment();
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
