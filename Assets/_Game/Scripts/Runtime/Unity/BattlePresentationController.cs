using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Unity.UI.Battle;
using UnityEngine;

namespace SM.Unity;

public sealed class BattlePresentationController : MonoBehaviour
{
    [SerializeField] private Transform battleStageRoot = null!;
    [SerializeField] private RectTransform actorOverlayRoot = null!;

    private readonly Dictionary<string, BattleActorView> _actorViews = new();
    private Camera _camera = null!;
    private BattlePresentationOptions _options = BattlePresentationOptions.CreateDefault();
    private BattleUnitMetadataFormatter? _metadataFormatter;

    public bool IsPaused { get; private set; }

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
        ValidateReferences();
        _camera = Camera.main!;
        IsPaused = false;
        Clear();
        CreateStageDecor();

        foreach (var actor in initialStep.Units)
        {
            var actorGo = new GameObject(actor.Name);
            actorGo.transform.SetParent(battleStageRoot, false);
            var view = actorGo.AddComponent<BattleActorView>();
            view.Initialize(actor, actorOverlayRoot, _camera, this, _metadataFormatter);
            view.ApplyOptions(_options);
            _actorViews[actor.Id] = view;
        }

        SetBlend(initialStep, initialStep, 1f);
    }

    public void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;
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

    public void PushStep(BattleSimulationStep previousStep, BattleSimulationStep currentStep)
    {
        TriggerEvents(currentStep.Events);
        SetBlend(previousStep, currentStep, 0f);
    }

    public void SetBlend(BattleSimulationStep fromStep, BattleSimulationStep toStep, float alpha)
    {
        if (_actorViews.Count == 0)
        {
            return;
        }

        var fromById = fromStep.Units.ToDictionary(unit => unit.Id);
        var toById = toStep.Units.ToDictionary(unit => unit.Id);
        foreach (var (id, view) in _actorViews)
        {
            if (!toById.TryGetValue(id, out var toState))
            {
                continue;
            }

            var fromState = fromById.TryGetValue(id, out var resolvedFrom)
                ? resolvedFrom
                : toState;
            view.ApplyBlend(fromState, toState, alpha);
        }
    }

    private void TriggerEvents(IReadOnlyList<BattleEvent> events)
    {
        foreach (var eventData in events)
        {
            if (_actorViews.TryGetValue(eventData.ActorId.Value, out var sourceView))
            {
                BattleActorView? targetView = null;
                if (eventData.TargetId is { } targetId && _actorViews.TryGetValue(targetId.Value, out var resolvedTarget))
                {
                    targetView = resolvedTarget;
                }

                sourceView.PlayAsSource(eventData, targetView);
                targetView?.PlayAsTarget(eventData);
            }
        }
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

        if (battleStageRoot != null)
        {
            for (var i = battleStageRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(battleStageRoot.GetChild(i).gameObject);
            }
        }

        if (actorOverlayRoot != null)
        {
            for (var i = actorOverlayRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(actorOverlayRoot.GetChild(i).gameObject);
            }
        }
    }

    private void CreateStageDecor()
    {
        if (battleStageRoot == null)
        {
            return;
        }

        var decorRoot = new GameObject("StageDecor");
        decorRoot.transform.SetParent(battleStageRoot, false);

        CreateStageBlock(
            decorRoot.transform,
            "ArenaFloor",
            new Vector3(0f, -1.12f, 0f),
            new Vector3(18f, 0.16f, 9.2f),
            new Color(0.18f, 0.14f, 0.11f, 1f));
        CreateStageBlock(
            decorRoot.transform,
            "ArenaInnerFloor",
            new Vector3(0f, -1.04f, 0f),
            new Vector3(14.2f, 0.04f, 6.8f),
            new Color(0.25f, 0.21f, 0.17f, 1f));
        CreateStageBlock(
            decorRoot.transform,
            "CenterLine",
            new Vector3(0f, -0.99f, 0f),
            new Vector3(0.12f, 0.01f, 6.2f),
            new Color(0.85f, 0.68f, 0.34f, 1f));
        CreateStageBlock(
            decorRoot.transform,
            "AllyZone",
            new Vector3(-3.35f, -0.985f, 0f),
            new Vector3(4.1f, 0.01f, 5.8f),
            new Color(0.15f, 0.30f, 0.47f, 1f));
        CreateStageBlock(
            decorRoot.transform,
            "EnemyZone",
            new Vector3(3.35f, -0.985f, 0f),
            new Vector3(4.1f, 0.01f, 5.8f),
            new Color(0.42f, 0.17f, 0.15f, 1f));
    }

    private static void CreateStageBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;

        var collider = block.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        var renderer = block.GetComponent<Renderer>();
        renderer.sharedMaterial = BattlePresentationMaterialFactory.Create(color);
    }
}
