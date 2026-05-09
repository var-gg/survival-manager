using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity;

[CreateAssetMenu(menuName = "SM/Battle/Battle VFX Catalog", fileName = "BattleVfxCatalog")]
public sealed partial class BattleVfxCatalog : ScriptableObject
{
    public const string ResourcesPath = "_Game/Battle/BattleVfxCatalog";

    [SerializeField] private List<BattleVfxCatalogEntry> entries = new();

    public static BattleVfxCatalog? ResolveRuntimeCatalog(BattleVfxCatalog? configuredCatalog)
    {
        if (configuredCatalog != null)
        {
            return configuredCatalog;
        }

        var loaded = Resources.Load<BattleVfxCatalog>(ResourcesPath);
        if (loaded != null && loaded.HasAnyEntry())
        {
            return loaded;
        }

#if UNITY_EDITOR
        return TryCreateEditorEpicToonFxFallbackCatalog();
#else
        return null;
#endif
    }

    public bool TryResolve(BattlePresentationCueType cueType, out BattleVfxCatalogEntry entry)
    {
        foreach (var candidate in entries)
        {
            if (candidate != null
                && candidate.CueType == cueType
                && candidate.AnimationSemantic == BattleAnimationSemantic.None
                && candidate.Prefab != null)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    public bool TryResolve(BattlePresentationCue cue, out BattleVfxCatalogEntry entry)
    {
        if (cue.AnimationSemantic != BattleAnimationSemantic.None)
        {
            foreach (var candidate in entries)
            {
                if (candidate != null
                    && candidate.CueType == cue.CueType
                    && candidate.AnimationSemantic == cue.AnimationSemantic
                    && candidate.Prefab != null)
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        return TryResolve(cue.CueType, out entry);
    }

    public void SetEntry(
        BattlePresentationCueType cueType,
        GameObject prefab,
        BattleActorSocketId socketId,
        float lifetimeSeconds,
        Vector3 localOffset,
        Vector3 localEulerAngles,
        Vector3 localScale,
        bool parentToSocket,
        BattleAnimationSemantic animationSemantic = BattleAnimationSemantic.None)
    {
        var existing = entries.Find(entry => entry.CueType == cueType && entry.AnimationSemantic == animationSemantic);
        if (existing == null)
        {
            entries.Add(new BattleVfxCatalogEntry(
                cueType,
                prefab,
                socketId,
                lifetimeSeconds,
                localOffset,
                localEulerAngles,
                localScale,
                parentToSocket,
                animationSemantic));
            return;
        }

        existing.Configure(prefab, socketId, lifetimeSeconds, localOffset, localEulerAngles, localScale, parentToSocket, animationSemantic);
    }

    private bool HasAnyEntry()
    {
        foreach (var entry in entries)
        {
            if (entry != null && entry.Prefab != null)
            {
                return true;
            }
        }

        return false;
    }
}

[Serializable]
public sealed class BattleVfxCatalogEntry
{
    [SerializeField] private BattlePresentationCueType cueType;
    [SerializeField] private GameObject prefab = null!;
    [SerializeField] private BattleActorSocketId socketId = BattleActorSocketId.Center;
    [SerializeField, Min(0.05f)] private float lifetimeSeconds = 2f;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 localScale = Vector3.one;
    [SerializeField] private bool parentToSocket;
    [SerializeField] private BattleAnimationSemantic animationSemantic = BattleAnimationSemantic.None;

    public BattleVfxCatalogEntry(
        BattlePresentationCueType cueType,
        GameObject prefab,
        BattleActorSocketId socketId,
        float lifetimeSeconds,
        Vector3 localOffset,
        Vector3 localEulerAngles,
        Vector3 localScale,
        bool parentToSocket,
        BattleAnimationSemantic animationSemantic = BattleAnimationSemantic.None)
    {
        this.cueType = cueType;
        Configure(prefab, socketId, lifetimeSeconds, localOffset, localEulerAngles, localScale, parentToSocket, animationSemantic);
    }

    public BattlePresentationCueType CueType => cueType;
    public GameObject Prefab => prefab;
    public BattleActorSocketId SocketId => socketId;
    public float LifetimeSeconds => lifetimeSeconds;
    public Vector3 LocalOffset => localOffset;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 LocalScale => localScale;
    public bool ParentToSocket => parentToSocket;
    public BattleAnimationSemantic AnimationSemantic => animationSemantic;

    public void Configure(
        GameObject configuredPrefab,
        BattleActorSocketId configuredSocketId,
        float configuredLifetimeSeconds,
        Vector3 configuredLocalOffset,
        Vector3 configuredLocalEulerAngles,
        Vector3 configuredLocalScale,
        bool configuredParentToSocket,
        BattleAnimationSemantic configuredAnimationSemantic = BattleAnimationSemantic.None)
    {
        prefab = configuredPrefab;
        socketId = configuredSocketId;
        lifetimeSeconds = Mathf.Max(0.05f, configuredLifetimeSeconds);
        localOffset = configuredLocalOffset;
        localEulerAngles = configuredLocalEulerAngles;
        localScale = configuredLocalScale == Vector3.zero ? Vector3.one : configuredLocalScale;
        parentToSocket = configuredParentToSocket;
        animationSemantic = configuredAnimationSemantic;
    }
}
