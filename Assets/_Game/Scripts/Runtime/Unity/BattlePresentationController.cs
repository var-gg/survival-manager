using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Unity.UI.Battle;
using Unity.Profiling;
using UnityEngine;

namespace SM.Unity
{

public sealed class BattlePresentationController : MonoBehaviour
{
    private static readonly ProfilerMarker SetBlendMarker = new("SM.BattlePresentationController.SetBlend");
    private const float ReadabilityBoostDuration = 1.8f;
    private const float SelectionPickRadiusPixels = 72f;
    public const float StartupIdleSeconds = 0.28f;
    public const float StartupStanceSeconds = 0.34f;
    public const float StartupHoldSeconds = StartupIdleSeconds + StartupStanceSeconds;

    [SerializeField] private Transform battleStageRoot = null!;
    [SerializeField] private RectTransform actorOverlayRoot = null!;
    [SerializeField] private BattleActorPresentationCatalog presentationCatalog = null!;
    [SerializeField] private BattleMapCatalog battleMapCatalog = null!;

    private readonly Dictionary<string, BattleActorView> _actorViews = new();
    private readonly Dictionary<string, BattleUnitReadModel> _cachedFromUnitsById = new();
    private readonly Dictionary<string, BattleUnitReadModel> _cachedToUnitsById = new();
    private readonly Dictionary<string, Renderer> _anchorPlateRenderers = new();
    private readonly List<Renderer> _laneGuideRenderers = new();
    private readonly BattlePresentationCueBuilder _cueBuilder = new();

    private Camera _camera = null!;
    private BattleStageEnvironmentAdapter? _activeEnvironmentAdapter;
    private BattlePresentationOptions _options = BattlePresentationOptions.CreateDefault();
    private BattleUnitMetadataFormatter? _metadataFormatter;
    private BattleSimulationStep? _cachedFromStep;
    private BattleSimulationStep? _cachedToStep;
    private string _selectedAnchorKey = string.Empty;
    private float _readabilityBoostRemaining;
    private float _startupElapsedSeconds;

    public bool IsPaused { get; private set; }
    public int LastCueCount { get; private set; }
    public int TotalCueCount { get; private set; }
    public string ActiveMapId { get; private set; } = string.Empty;
    public string ActiveMapDisplayName { get; private set; } = string.Empty;

    public void ConfigurePresentationCatalog(BattleActorPresentationCatalog catalog)
    {
        presentationCatalog = catalog;
    }

    public void ConfigureBattleMapCatalog(BattleMapCatalog catalog)
    {
        battleMapCatalog = catalog;
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            _camera = Camera.main!;
        }

        foreach (var view in _actorViews.Values)
        {
            view.RefreshOverlayPosition();
        }
    }

    public void Initialize(BattleSimulationStep initialStep)
    {
        Initialize(initialStep, BattleMapSelectionContext.Empty);
    }

    public void Initialize(BattleSimulationStep initialStep, BattleMapSelectionContext mapContext)
    {
        ValidateReferences();
        _camera = Camera.main!;
        IsPaused = false;
        LastCueCount = 0;
        _startupElapsedSeconds = 0f;
        Clear();
        var tacticalOverlayMode = CreateSelectedMap(mapContext);
        CreateStageDecor(tacticalOverlayMode);
        CreateBattleLighting();
        _activeEnvironmentAdapter?.Apply();
        var runtimeCatalog = BattleActorPresentationCatalog.ResolveRuntimeCatalog(presentationCatalog);

        foreach (var actor in initialStep.Units)
        {
            var wrapperPrefab = runtimeCatalog.ResolveWrapperPrefab(actor);
            var wrapper = Instantiate(wrapperPrefab, transform);
            wrapper.name = $"BattleActor_{actor.Id}_Wrapper";
            wrapper.gameObject.SetActive(true);
            wrapper.Configure(actor);
            var view = wrapper.GetComponent<BattleActorView>();
            if (view == null)
            {
                view = wrapper.gameObject.AddComponent<BattleActorView>();
            }

            view.Initialize(actor, actorOverlayRoot, _camera, this, _metadataFormatter);
            view.ApplyOptions(_options);
            _actorViews[actor.Id] = view;
        }

        ClearTransients(BattlePresentationCueType.PlaybackReset);
        RenderSnapshot(initialStep);
        SetFocus(initialStep, string.Empty);
    }

    public void ApplyOptions(BattlePresentationOptions options)
    {
        _options = options;

        foreach (var view in _actorViews.Values)
        {
            view.ApplyOptions(_options);
        }
    }

    public void ConfigureMetadataFormatter(BattleUnitMetadataFormatter formatter)
    {
        _metadataFormatter = formatter;

        foreach (var view in _actorViews.Values)
        {
            view.SetMetadataFormatter(formatter);
        }
    }

    public void RenderSnapshot(BattleSimulationStep step)
    {
        LastCueCount = 0;
        SetBlend(step, step, 1f);
    }

    public void AdvanceStep(BattleSimulationStep previousStep, BattleSimulationStep currentStep)
    {
        var cues = _cueBuilder.Build(previousStep, currentStep);
        LastCueCount = cues.Count;
        TotalCueCount += LastCueCount;

        foreach (var cue in cues)
        {
            if (_actorViews.TryGetValue(cue.SubjectActorId, out var view))
            {
                view.ConsumeCue(cue, ResolveAnchorWorld(cue.RelatedActorId, cue.RelatedAnchor));
            }
        }

        if (currentStep.StepIndex > 0 || currentStep.IsFinished)
        {
            _startupElapsedSeconds = StartupHoldSeconds;
        }

        SetBlend(previousStep, currentStep, 0f);
    }

    public void SetBlend(BattleSimulationStep fromStep, BattleSimulationStep toStep, float alpha)
    {
        using var _ = SetBlendMarker.Auto();
        if (_actorViews.Count == 0)
        {
            return;
        }

        var fromById = ResolveUnitIndex(fromStep, ref _cachedFromStep, _cachedFromUnitsById);
        var toById = ResolveUnitIndex(toStep, ref _cachedToStep, _cachedToUnitsById);
        foreach (var (id, view) in _actorViews)
        {
            if (!toById.TryGetValue(id, out var toState))
            {
                continue;
            }

            var fromState = fromById.TryGetValue(id, out var resolvedFrom)
                ? resolvedFrom
                : toState;
            view.ApplyPresentationPhase(ResolvePresentationPhase(toStep));
            view.ApplyBlend(fromState, toState, alpha);
        }
    }

    public void SetFocus(BattleSimulationStep step, string selectedUnitId)
    {
        var focusActorId = string.Empty;
        var focusTargetId = string.Empty;
        if (BattleReadabilityFormatter.TryResolveStepFocus(step, out var focus))
        {
            focusActorId = focus.ActorId;
            focusTargetId = focus.TargetId ?? string.Empty;
        }

        var selectedUnit = step.Units.FirstOrDefault(unit => unit.Id == selectedUnitId);
        _selectedAnchorKey = selectedUnit == null
            ? string.Empty
            : BuildAnchorKey(selectedUnit.Side, selectedUnit.Anchor);

        foreach (var (id, view) in _actorViews)
        {
            if (!_cachedToUnitsById.TryGetValue(id, out var unit))
            {
                continue;
            }

            var targetId = ResolveFacingTargetId(unit, id, selectedUnitId, focusActorId, focusTargetId);

            view.ApplyContext(
                isSelected: id == selectedUnitId,
                isCurrentActor: id == focusActorId,
                isCurrentTarget: id == focusTargetId,
                focusTargetWorld: ResolveAnchorWorld(targetId, BattlePresentationAnchorId.Center),
                readabilityBoost: Mathf.Clamp01(_readabilityBoostRemaining / ReadabilityBoostDuration));
        }

        UpdateStageReadability();
    }

    private static string ResolveFacingTargetId(
        BattleUnitReadModel unit,
        string actorId,
        string selectedUnitId,
        string focusActorId,
        string focusTargetId)
    {
        if (actorId == focusActorId && !string.IsNullOrWhiteSpace(focusTargetId))
        {
            return focusTargetId;
        }

        if (actorId == selectedUnitId && !string.IsNullOrWhiteSpace(unit.TargetId))
        {
            return unit.TargetId;
        }

        return unit.TargetId ?? string.Empty;
    }

    public void ClearTransients(BattlePresentationCueType reason)
    {
        LastCueCount = 0;
        if (reason == BattlePresentationCueType.PlaybackReset)
        {
            _readabilityBoostRemaining = ReadabilityBoostDuration;
        }

        foreach (var view in _actorViews.Values)
        {
            view.ClearTransients(reason);
        }

        UpdateStageReadability();
    }

    public void TickTransients(float deltaTime, float playbackSpeed, bool paused)
    {
        IsPaused = paused;
        if (!paused)
        {
            _readabilityBoostRemaining = Mathf.Max(0f, _readabilityBoostRemaining - (deltaTime * playbackSpeed));
            _startupElapsedSeconds = Mathf.Min(
                StartupHoldSeconds,
                _startupElapsedSeconds + (Mathf.Max(0f, deltaTime) * Mathf.Max(0.05f, playbackSpeed)));
        }

        foreach (var view in _actorViews.Values)
        {
            view.TickTransients(deltaTime, playbackSpeed, paused);
        }

        UpdateStageReadability();
    }

    private BattleActorPresentationPhase ResolvePresentationPhase(BattleSimulationStep step)
    {
        if (step.IsFinished)
        {
            return BattleActorPresentationPhase.ResolvedIdle;
        }

        if (step.StepIndex == 0 && _startupElapsedSeconds < StartupIdleSeconds)
        {
            return BattleActorPresentationPhase.RelaxedIdle;
        }

        return BattleActorPresentationPhase.CombatReady;
    }

    public bool TryPickActor(Vector2 screenPosition, out string actorId)
    {
        actorId = string.Empty;
        var bestDistance = SelectionPickRadiusPixels * SelectionPickRadiusPixels;
        foreach (var (id, view) in _actorViews)
        {
            var distance = view.GetScreenDistanceSquared(screenPosition);
            if (distance >= 0f && distance <= bestDistance)
            {
                bestDistance = distance;
                actorId = id;
            }
        }

        return actorId.Length > 0;
    }

    private Vector3? ResolveAnchorWorld(string? actorId, BattlePresentationAnchorId anchorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        return _actorViews.TryGetValue(actorId, out var view)
            ? view.GetAnchorWorld(anchorId)
            : null;
    }

    private void ValidateReferences()
    {
        if (battleStageRoot == null)
        {
            Debug.LogError("[BattlePresentationController] Missing Transform reference: battleStageRoot");
        }

        if (actorOverlayRoot == null)
        {
            Debug.LogError("[BattlePresentationController] Missing RectTransform reference: actorOverlayRoot");
        }
    }

    private void Clear()
    {
        _actorViews.Clear();
        _cachedFromUnitsById.Clear();
        _cachedToUnitsById.Clear();
        _anchorPlateRenderers.Clear();
        _laneGuideRenderers.Clear();
        _cachedFromStep = null;
        _cachedToStep = null;
        _selectedAnchorKey = string.Empty;
        _readabilityBoostRemaining = 0f;
        _activeEnvironmentAdapter = null;

        if (battleStageRoot != null)
        {
            for (var i = battleStageRoot.childCount - 1; i >= 0; i--)
            {
                DestroyPresentationObject(battleStageRoot.GetChild(i).gameObject);
            }
        }

        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyPresentationObject(transform.GetChild(i).gameObject);
        }

        if (actorOverlayRoot != null)
        {
            for (var i = actorOverlayRoot.childCount - 1; i >= 0; i--)
            {
                DestroyPresentationObject(actorOverlayRoot.GetChild(i).gameObject);
            }
        }
    }

    private BattleMapTacticalOverlayMode CreateSelectedMap(BattleMapSelectionContext context)
    {
        ActiveMapId = string.Empty;
        ActiveMapDisplayName = string.Empty;

        if (battleStageRoot == null)
        {
            return BattleMapTacticalOverlayMode.FullArena;
        }

        var runtimeCatalog = BattleMapCatalog.ResolveRuntimeCatalog(battleMapCatalog);
        if (runtimeCatalog == null || !runtimeCatalog.TrySelectMap(context, out var map))
        {
            return BattleMapTacticalOverlayMode.FullArena;
        }

        var instance = Instantiate(map.Prefab, battleStageRoot);
        instance.name = $"BattleMap_{map.MapId}";
        instance.transform.localPosition = map.LocalPosition;
        instance.transform.localRotation = Quaternion.Euler(map.LocalEulerAngles);
        instance.transform.localScale = map.LocalScale;

        var preExistingMat = instance.GetComponent<BattleMapMaterialAdapter>();
        var preExistingEnv = instance.GetComponent<BattleStageEnvironmentAdapter>();
#if UNITY_EDITOR
        var prefabPath = UnityEditor.AssetDatabase.GetAssetPath(map.Prefab);
#else
        var prefabPath = "<build>";
#endif
        Debug.Log(
            $"[BattleMap.Diag] mapId={map.MapId} prefab={map.Prefab.name} path={prefabPath} " +
            $"instance={instance.name} preExistingMat={preExistingMat != null} preExistingEnv={preExistingEnv != null}");

        var materialAdapter = preExistingMat ?? instance.AddComponent<BattleMapMaterialAdapter>();
        materialAdapter.Apply();

        var environmentAdapter = preExistingEnv ?? instance.AddComponent<BattleStageEnvironmentAdapter>();
        // GPT-Pro 진단: pre-attached 여부와 무관하게 항상 runtime preset을 강제한다.
        // 이전 코드는 newly-created 시에만 ConfigureForestRuinsDefaults를 호출해 Edit/Play disparity 유발.
        environmentAdapter.ConfigureForestRuinsDefaults();

        _activeEnvironmentAdapter = environmentAdapter;
        _activeEnvironmentAdapter?.Apply();

        ActiveMapId = map.MapId;
        ActiveMapDisplayName = map.DisplayName;
        return map.TacticalOverlayMode;
    }

    private void CreateStageDecor(BattleMapTacticalOverlayMode tacticalOverlayMode)
    {
        if (battleStageRoot == null || tacticalOverlayMode == BattleMapTacticalOverlayMode.None)
        {
            return;
        }

        var decorRoot = new GameObject("StageDecor");
        decorRoot.transform.SetParent(battleStageRoot, false);

        if (tacticalOverlayMode == BattleMapTacticalOverlayMode.FullArena)
        {
            var backgroundRoot = new GameObject("Background");
            backgroundRoot.transform.SetParent(decorRoot.transform, false);
            CreateStageBlock(
                backgroundRoot.transform,
                "ArenaFloor",
                new Vector3(0f, -1.12f, 0f),
                new Vector3(18f, 0.16f, 9.2f),
                new Color(0.18f, 0.14f, 0.11f, 1f));
            CreateStageBlock(
                backgroundRoot.transform,
                "ArenaInnerFloor",
                new Vector3(0f, -1.04f, 0f),
                new Vector3(14.2f, 0.04f, 6.8f),
                new Color(0.25f, 0.21f, 0.17f, 1f));
            CreateStageBlock(
                backgroundRoot.transform,
                "CenterLine",
                new Vector3(0f, -0.99f, 0f),
                new Vector3(0.12f, 0.01f, 6.2f),
                new Color(0.85f, 0.68f, 0.34f, 1f));
            CreateStageBlock(
                backgroundRoot.transform,
                "AllyZone",
                new Vector3(-3.35f, -0.985f, 0f),
                new Vector3(4.1f, 0.01f, 5.8f),
                new Color(0.15f, 0.30f, 0.47f, 1f));
            CreateStageBlock(
                backgroundRoot.transform,
                "EnemyZone",
                new Vector3(3.35f, -0.985f, 0f),
                new Vector3(4.1f, 0.01f, 5.8f),
                new Color(0.42f, 0.17f, 0.15f, 1f));
        }

        var readabilityRoot = new GameObject("ReadabilitySurface");
        readabilityRoot.transform.SetParent(decorRoot.transform, false);

        _laneGuideRenderers.Add(CreateStageBlock(
            readabilityRoot.transform,
            "LaneGuideTop",
            new Vector3(0f, -0.978f, 1.8f),
            new Vector3(11.2f, 0.005f, 0.08f),
            new Color(0.39f, 0.40f, 0.43f, 1f)));
        _laneGuideRenderers.Add(CreateStageBlock(
            readabilityRoot.transform,
            "LaneGuideCenter",
            new Vector3(0f, -0.978f, 0f),
            new Vector3(11.2f, 0.005f, 0.08f),
            new Color(0.39f, 0.40f, 0.43f, 1f)));
        _laneGuideRenderers.Add(CreateStageBlock(
            readabilityRoot.transform,
            "LaneGuideBottom",
            new Vector3(0f, -0.978f, -1.8f),
            new Vector3(11.2f, 0.005f, 0.08f),
            new Color(0.39f, 0.40f, 0.43f, 1f)));

        foreach (TeamSide side in System.Enum.GetValues(typeof(TeamSide)))
        {
            foreach (DeploymentAnchorId anchor in System.Enum.GetValues(typeof(DeploymentAnchorId)))
            {
                var anchorPosition = BattleFactory.ResolveAnchorPosition(side, anchor);
                var anchorKey = BuildAnchorKey(side, anchor);
                _anchorPlateRenderers[anchorKey] = CreateStageBlock(
                    readabilityRoot.transform,
                    $"{anchorKey}_Plate",
                    new Vector3(anchorPosition.X, -0.974f, anchorPosition.Y),
                    new Vector3(0.68f, 0.008f, 0.92f),
                    side == TeamSide.Ally
                        ? new Color(0.22f, 0.45f, 0.62f, 1f)
                        : new Color(0.58f, 0.24f, 0.21f, 1f));
            }
        }

        UpdateStageReadability();
    }

    private void CreateBattleLighting()
    {
        if (battleStageRoot == null)
        {
            return;
        }

        var lightingRoot = new GameObject("BattleLighting");
        lightingRoot.transform.SetParent(battleStageRoot, false);

        var key = CreateBattleLight(
            lightingRoot.transform,
            "BattleKeyLight",
            LightType.Directional,
            Quaternion.Euler(38f, -48f, 0f),
            new Color(1f, 0.92f, 0.78f, 1f),
            2.80f);
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.97f;
        key.shadowBias = 0.01f;
        key.shadowNormalBias = 0.05f;
        key.shadowNearPlane = 0.10f;
        key.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
        RenderSettings.sun = key;

        // Warm narrative accent — placed far from character spawn area to avoid silhouette wash.
        var warmAccent = CreateBattleLight(
            lightingRoot.transform,
            "BattleWarmAccent",
            LightType.Point,
            Quaternion.identity,
            new Color(1f, 0.62f, 0.24f, 1f),
            2.2f);
        warmAccent.transform.localPosition = new Vector3(-5.8f, 2.4f, 4.6f);
        warmAccent.range = 9f;
        warmAccent.shadows = LightShadows.None;

        var fill = CreateBattleLight(
            lightingRoot.transform,
            "BattleFillLight",
            LightType.Directional,
            Quaternion.Euler(35f, 130f, 0f),
            new Color(0.50f, 0.55f, 0.62f, 1f),
            0.12f);
        fill.shadows = LightShadows.None;

        CreateForegroundDressing(lightingRoot.transform);
    }

    private void CreateForegroundDressing(Transform parent)
    {
#if UNITY_EDITOR
        // Foreground tree dressing — places 3 tall trees at side/front to cast dramatic crisp shadows across the play area.
        var dressingRoot = new GameObject("BattleForegroundDressing");
        dressingRoot.transform.SetParent(parent, false);

        // ShadowsOnly trees placed AROUND the play area edges so their shadows fall inward but their meshes are invisible.
        TryInstantiateForegroundTree(dressingRoot.transform, "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_03.prefab",
            position: new Vector3(5.8f, 0f, 5.5f), scale: 1.70f, yawDegrees: 22f);
        TryInstantiateForegroundTree(dressingRoot.transform, "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_07.prefab",
            position: new Vector3(-5.2f, 0f, 4.0f), scale: 1.65f, yawDegrees: -42f);
        TryInstantiateForegroundTree(dressingRoot.transform, "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Tree_B_09.prefab",
            position: new Vector3(0.5f, 0f, 7.2f), scale: 1.75f, yawDegrees: 108f);
#endif
    }

#if UNITY_EDITOR
    private static void TryInstantiateForegroundTree(Transform parent, string prefabPath, Vector3 position, float scale, float yawDegrees)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        var instance = Instantiate(prefab, parent);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        instance.transform.localScale = Vector3.one * scale;

        // ShadowsOnly — 메쉬는 안 보이고 그림자만 play area에 떨어뜨림.
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            renderer.receiveShadows = false;
        }
    }
#endif

    private static Light CreateBattleLight(
        Transform parent,
        string name,
        LightType type,
        Quaternion rotation,
        Color color,
        float intensity)
    {
        var lightGo = new GameObject(name);
        lightGo.transform.SetParent(parent, false);
        lightGo.transform.rotation = rotation;
        var light = lightGo.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        return light;
    }

    private void UpdateStageReadability()
    {
        var boost = Mathf.Clamp01(_readabilityBoostRemaining / ReadabilityBoostDuration);
        foreach (var renderer in _laneGuideRenderers)
        {
            var baseColor = new Color(0.20f, 0.22f, 0.25f, 1f);
            var boosted = new Color(0.48f, 0.52f, 0.58f, 1f);
            BattlePresentationMaterialFactory.ApplyColor(renderer.sharedMaterial, Color.Lerp(baseColor, boosted, boost));
        }

        foreach (var (key, renderer) in _anchorPlateRenderers)
        {
            var isAlly = key.StartsWith("Ally", System.StringComparison.Ordinal);
            var baseColor = isAlly
                ? new Color(0.16f, 0.26f, 0.33f, 1f)
                : new Color(0.35f, 0.18f, 0.16f, 1f);
            var boosted = isAlly
                ? new Color(0.30f, 0.50f, 0.68f, 1f)
                : new Color(0.62f, 0.32f, 0.26f, 1f);
            var selected = isAlly
                ? new Color(0.70f, 0.92f, 1f, 1f)
                : new Color(1f, 0.78f, 0.60f, 1f);
            var color = key == _selectedAnchorKey
                ? selected
                : Color.Lerp(baseColor, boosted, boost);
            BattlePresentationMaterialFactory.ApplyColor(renderer.sharedMaterial, color);
        }
    }

    private static Renderer CreateStageBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;

        var collider = block.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyPresentationObject(collider);
        }

        var renderer = block.GetComponent<Renderer>();
        renderer.sharedMaterial = BattlePresentationMaterialFactory.Create(color);
        return renderer;
    }

    private static void DestroyPresentationObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static string BuildAnchorKey(TeamSide side, DeploymentAnchorId anchor)
    {
        return $"{side}:{anchor}";
    }

    private static IReadOnlyDictionary<string, BattleUnitReadModel> ResolveUnitIndex(
        BattleSimulationStep step,
        ref BattleSimulationStep? cachedStep,
        Dictionary<string, BattleUnitReadModel> cache)
    {
        if (ReferenceEquals(cachedStep, step))
        {
            return cache;
        }

        cache.Clear();
        foreach (var unit in step.Units)
        {
            cache[unit.Id] = unit;
        }

        cachedStep = step;
        return cache;
    }
}
}
