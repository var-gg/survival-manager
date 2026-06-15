using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core;
using SM.Meta;
using SM.Unity;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.Narrative;

[DisallowMultipleComponent]
public sealed class StorySceneFlowBridge : MonoBehaviour
{
    [SerializeField] private StoryPresentationRunner _runner = null!;

    private readonly Queue<PendingAdvanceBatch> _pendingAdvanceBatches = new();
    private GameSessionRoot _root = null!;
    private bool _isDispatchingRequest;

    // headless conformance(narrative seam): null이면 기존 StoryPresentationRunner 경로(프로덕션 불변).
    // 캡쳐/headless 드라이버가 OverridePresentationSink로 즉시-ack sink를 꽂아 컷씬 무한대기를 우회한다.
    private INarrativePresentationSink? _presentationSink;

    public bool IsBusy => _isDispatchingRequest
                          || _pendingAdvanceBatches.Count > 0
                          || (_runner != null && _runner.IsBusy);

    public event Action<NarrativeMoment>? AdvanceStarted;
    public event Action<NarrativeMoment>? AdvanceCompleted;

    public void Advance(NarrativeMoment moment, StoryMomentContext context, Action? onCompleted = null)
    {
        if (!EnsureReady())
        {
            onCompleted?.Invoke();
            return;
        }

        EnsureBacklogBatchTracked();

        var session = _root.SessionState;
        var pendingCountBefore = session.NarrativeProgress.PendingPresentations.Length;
        AdvanceStarted?.Invoke(moment);
        session.AdvanceNarrative(moment, context ?? StoryMomentContext.Empty);
        var pendingCountAfter = session.NarrativeProgress.PendingPresentations.Length;
        var addedCount = Math.Max(0, pendingCountAfter - pendingCountBefore);
        if (addedCount == 0)
        {
            AdvanceCompleted?.Invoke(moment);
            onCompleted?.Invoke();
            TryPumpQueue();
            return;
        }

        _pendingAdvanceBatches.Enqueue(PendingAdvanceBatch.ForMoment(moment, addedCount, onCompleted));
        TryPumpQueue();
    }

    /// <summary>
    /// 세션이 이미 발화·큐잉한 연출을 화면에 present한다(추가 moment 발화 없음). "발화는 세션, 표시는 씬" —
    /// controller가 moment를 직접 Advance하는 대신 이 메서드로 큐를 흘린다(헤드리스는 세션 발화로 직접 drain).
    /// </summary>
    public void PresentPending()
    {
        if (!EnsureReady())
        {
            return;
        }

        EnsureBacklogBatchTracked();
        TryPumpQueue();
    }

    public void ClearPending()
    {
        _pendingAdvanceBatches.Clear();
        _isDispatchingRequest = false;
        if (_runner == null)
        {
            return;
        }

        _runner.StopAndClear();
    }

    private bool EnsureReady()
    {
        _root ??= GameSessionRoot.EnsureInstance();
        if (_root == null)
        {
            Debug.LogError("[StorySceneFlowBridge] GameSessionRoot could not be resolved.");
            return false;
        }

        if (_runner != null)
        {
            return true;
        }

        _runner = GetComponentInChildren<StoryPresentationRunner>(true);
        if (_runner != null)
        {
            return true;
        }

        var panelHost = ResolvePanelHost();
        if (panelHost == null)
        {
            Debug.LogError("[StorySceneFlowBridge] RuntimePanelHost could not be resolved.");
            return false;
        }

        var runnerObject = new GameObject("StoryPresentationRunner");
        runnerObject.transform.SetParent(panelHost.transform.parent != null ? panelHost.transform.parent : transform, false);
        _runner = runnerObject.AddComponent<StoryPresentationRunner>();
        _runner.SetPanelHost(panelHost);
        return true;
    }

    private void TryPumpQueue()
    {
        if (!EnsureReady() || _isDispatchingRequest || _runner.IsBusy)
        {
            return;
        }

        EnsureBacklogBatchTracked();
        if (_pendingAdvanceBatches.Count == 0)
        {
            return;
        }

        if (!_root.SessionState.TryDequeueNarrativePresentation(out var request) || request == null)
        {
            return;
        }

        _isDispatchingRequest = true;
        if (_presentationSink != null)
        {
            _presentationSink.Present(request, HandleDispatchedRequestCompleted);
        }
        else
        {
            _runner.Enqueue(new[] { request }, HandleDispatchedRequestCompleted);
        }
    }

    /// <summary>headless/캡쳐 드라이버가 연출 dispatch를 가로채도록 sink를 주입(미호출 시 프로덕션 runner 경로 유지).</summary>
    internal void OverridePresentationSink(INarrativePresentationSink sink) => _presentationSink = sink;

    private void HandleDispatchedRequestCompleted()
    {
        _isDispatchingRequest = false;
        if (_pendingAdvanceBatches.Count > 0)
        {
            _pendingAdvanceBatches.Peek().RemainingCount--;
        }

        while (_pendingAdvanceBatches.Count > 0 && _pendingAdvanceBatches.Peek().RemainingCount <= 0)
        {
            var completedBatch = _pendingAdvanceBatches.Dequeue();
            if (!completedBatch.HasMoment)
            {
                continue;
            }

            AdvanceCompleted?.Invoke(completedBatch.Moment);
            completedBatch.OnCompleted?.Invoke();
        }

        EnsureBacklogBatchTracked();
        TryPumpQueue();
    }

    private void EnsureBacklogBatchTracked()
    {
        if (_pendingAdvanceBatches.Count > 0 || _isDispatchingRequest || _root == null)
        {
            return;
        }

        var pendingCount = _root.SessionState.NarrativeProgress.PendingPresentations.Length;
        if (pendingCount > 0)
        {
            _pendingAdvanceBatches.Enqueue(PendingAdvanceBatch.ForBacklog(pendingCount));
        }
    }

    private RuntimePanelHost? ResolvePanelHost()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var panelHost = root.GetComponentsInChildren<RuntimePanelHost>(true).FirstOrDefault();
            if (panelHost != null)
            {
                return panelHost;
            }
        }

        return null;
    }

    private sealed class PendingAdvanceBatch
    {
        private PendingAdvanceBatch(NarrativeMoment moment, int remainingCount, Action? onCompleted, bool hasMoment)
        {
            Moment = moment;
            RemainingCount = remainingCount;
            OnCompleted = onCompleted;
            HasMoment = hasMoment;
        }

        public NarrativeMoment Moment { get; }
        public int RemainingCount { get; set; }
        public Action? OnCompleted { get; }
        public bool HasMoment { get; }

        public static PendingAdvanceBatch ForMoment(NarrativeMoment moment, int remainingCount, Action? onCompleted)
            => new(moment, remainingCount, onCompleted, hasMoment: true);

        public static PendingAdvanceBatch ForBacklog(int remainingCount)
            => new(default, remainingCount, null, hasMoment: false);
    }
}
