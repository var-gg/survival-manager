using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SM.Unity;

public sealed class BattlePresentationController : MonoBehaviour
{
    [SerializeField] private Transform battleStageRoot = null!;
    [SerializeField] private RectTransform actorOverlayRoot = null!;

    private readonly Dictionary<string, BattleActorView> _actorViews = new();
    private Camera _camera = null!;

    private void Update()
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

    public void Initialize(BattleReplayTrack track)
    {
        ValidateReferences();
        _camera = Camera.main!;
        Clear();

        foreach (var actor in track.InitialRoster)
        {
            var actorGo = new GameObject(actor.Name);
            actorGo.transform.SetParent(battleStageRoot, false);
            var view = actorGo.AddComponent<BattleActorView>();
            view.Initialize(actor, actorOverlayRoot, _camera, ResolveSlotPosition(track.InitialRoster, actor));
            _actorViews[actor.Id] = view;
        }
    }

    public void PresentFrame(BattleReplayFrame frame)
    {
        if (_actorViews.Count == 0)
        {
            return;
        }

        var stateById = frame.ActorStates.ToDictionary(actor => actor.Id);
        foreach (var kv in _actorViews)
        {
            if (stateById.TryGetValue(kv.Key, out var snapshot))
            {
                kv.Value.ApplyState(snapshot);
            }
        }

        if (frame.FrameKind != BattleReplayFrameKind.Event || frame.ActionType == null)
        {
            return;
        }

        if (frame.SourceId != null && _actorViews.TryGetValue(frame.SourceId, out var sourceView))
        {
            BattleActorView? targetView = null;
            if (frame.TargetId != null && _actorViews.TryGetValue(frame.TargetId, out var resolvedTarget))
            {
                targetView = resolvedTarget;
            }

            sourceView.PlayAsSource(frame, targetView);

            if (targetView != null)
            {
                targetView.PlayAsTarget(frame);
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

    private static Vector3 ResolveSlotPosition(IReadOnlyList<BattleReplayActorSnapshot> roster, BattleReplayActorSnapshot actor)
    {
        var sameSide = roster.Where(entry => entry.Side == actor.Side).ToList();
        var rowIndex = sameSide.Where(entry => entry.Row == actor.Row).TakeWhile(entry => entry.Id != actor.Id).Count();
        var z = rowIndex == 0 ? -1.3f : 1.3f;
        if (actor.Side == SM.Combat.Model.TeamSide.Enemy)
        {
            z *= -1f;
        }

        var x = actor.Side == SM.Combat.Model.TeamSide.Ally
            ? actor.Row == SM.Combat.Model.RowPosition.Front ? -1.85f : -3.85f
            : actor.Row == SM.Combat.Model.RowPosition.Front ? 1.85f : 3.85f;

        return new Vector3(x, 0f, z);
    }
}
