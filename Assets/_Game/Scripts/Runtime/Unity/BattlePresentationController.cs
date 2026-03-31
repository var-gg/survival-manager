using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public sealed class BattlePresentationController : MonoBehaviour
{
    [SerializeField] private Transform battleStageRoot = null!;
    [SerializeField] private RectTransform actorOverlayRoot = null!;

    private readonly Dictionary<string, BattleActorView> _actorViews = new();
    private Camera _camera = null!;
    private BattlePresentationOptions _options = BattlePresentationOptions.CreateDefault();

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

        foreach (var actor in initialStep.Units)
        {
            var actorGo = new GameObject(actor.Name);
            actorGo.transform.SetParent(battleStageRoot, false);
            var view = actorGo.AddComponent<BattleActorView>();
            view.Initialize(actor, actorOverlayRoot, _camera, this);
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
}
