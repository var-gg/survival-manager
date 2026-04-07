using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity;

public enum BattleAnimationHookType
{
    None = 0,
    TelegraphOpen = 1,
    Commit = 2,
    ProjectileRelease = 3,
    Impact = 4,
    GuardEnter = 5,
    GuardExit = 6,
    Step = 7,
    DeathStart = 8,
}

[Serializable]
public sealed class BattleAnimationRawEventMapping
{
    public string RawEventName = string.Empty;
    public BattleAnimationHookType Hook = BattleAnimationHookType.None;
}

public sealed class BattleAnimationEventBridge : MonoBehaviour
{
    [SerializeField] private List<BattleAnimationRawEventMapping> rawEventMappings = new();

    private readonly Dictionary<string, BattleAnimationHookType> _resolvedMappings = new(StringComparer.Ordinal);
    private bool _cueWindowOpen;

    public int DispatchCount { get; private set; }
    public BattleAnimationHookType LastDispatchedHook { get; private set; }

    public void SetRawEventMapping(string rawEventName, BattleAnimationHookType hook)
    {
        rawEventMappings.RemoveAll(mapping => string.Equals(mapping.RawEventName, rawEventName, StringComparison.Ordinal));
        rawEventMappings.Add(new BattleAnimationRawEventMapping
        {
            RawEventName = rawEventName,
            Hook = hook,
        });
        _resolvedMappings.Clear();
    }

    public void OpenCueWindow(BattlePresentationCue cue)
    {
        _cueWindowOpen = cue.CueType is not BattlePresentationCueType.PlaybackReset
            and not BattlePresentationCueType.SeekSnapshotApplied;
    }

    public bool TryHandleRawEvent(string rawEventName)
    {
        if (!_cueWindowOpen)
        {
            return false;
        }

        if (!TryResolveHook(rawEventName, out var hook) || hook == BattleAnimationHookType.None)
        {
            return false;
        }

        DispatchCount++;
        LastDispatchedHook = hook;
        return true;
    }

    public bool TryHandleHook(BattleAnimationHookType hook)
    {
        if (!_cueWindowOpen || hook == BattleAnimationHookType.None)
        {
            return false;
        }

        DispatchCount++;
        LastDispatchedHook = hook;
        return true;
    }

    public void ClearTransientState(BattlePresentationCueType reason)
    {
        _cueWindowOpen = false;
        LastDispatchedHook = BattleAnimationHookType.None;
    }

    private bool TryResolveHook(string rawEventName, out BattleAnimationHookType hook)
    {
        if (_resolvedMappings.Count != rawEventMappings.Count)
        {
            _resolvedMappings.Clear();
            foreach (var mapping in rawEventMappings)
            {
                if (!string.IsNullOrWhiteSpace(mapping.RawEventName))
                {
                    _resolvedMappings[mapping.RawEventName] = mapping.Hook;
                }
            }
        }

        return _resolvedMappings.TryGetValue(rawEventName, out hook);
    }
}
