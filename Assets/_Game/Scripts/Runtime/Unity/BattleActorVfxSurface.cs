using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public sealed class BattleActorVfxSurface : MonoBehaviour
{
    [SerializeField] private BattleVfxCatalog catalog = null!;

    private BattleVfxCatalog? _resolvedCatalog;
    private readonly System.Collections.Generic.List<GameObject> _activeSpawnedVfx = new();

    public int TriggerCount { get; private set; }
    public BattlePresentationCueType LastCueType { get; private set; }
    public BattleActorSocketId LastSocketId { get; private set; }
    public Vector3 LastSpawnPosition { get; private set; }
    public string LastSpawnedPrefabName { get; private set; } = string.Empty;
    public int ActiveSpawnedVfxCount => _activeSpawnedVfx.Count;

    public void ConfigureCatalog(BattleVfxCatalog configuredCatalog)
    {
        catalog = configuredCatalog;
        _resolvedCatalog = null;
    }

    public void ConsumeCue(BattlePresentationCue cue, BattleActorWrapper wrapper, Vector3? relatedWorld = null)
    {
        if (cue.CueType == BattlePresentationCueType.WindupEnter
            && cue.ActionType != BattleActionType.ActiveSkill)
        {
            return;
        }

        var resolvedCatalog = ResolveCatalog();
        BattleVfxCatalogEntry? entry = null;
        var hasCatalogEntry = resolvedCatalog != null && resolvedCatalog.TryResolve(cue, out entry);
        var socketId = hasCatalogEntry ? entry!.SocketId : BattleActorSocketId.Center;
        if (!hasCatalogEntry && !TryResolveSocket(cue.CueType, out socketId))
        {
            return;
        }

        TriggerCount++;
        LastCueType = cue.CueType;
        LastSocketId = socketId;
        LastSpawnPosition = wrapper.GetSocketWorld(socketId);

        if (hasCatalogEntry && entry != null)
        {
            SpawnCatalogEntry(entry, wrapper, relatedWorld);
        }
    }

    public void ClearTransientState(BattlePresentationCueType reason)
    {
        for (var i = _activeSpawnedVfx.Count - 1; i >= 0; i--)
        {
            var instance = _activeSpawnedVfx[i];
            if (instance != null)
            {
                DestroyPresentationObject(instance);
            }
        }

        _activeSpawnedVfx.Clear();
    }

    private BattleVfxCatalog? ResolveCatalog()
    {
        _resolvedCatalog ??= BattleVfxCatalog.ResolveRuntimeCatalog(catalog);
        return _resolvedCatalog;
    }

    private void SpawnCatalogEntry(BattleVfxCatalogEntry entry, BattleActorWrapper wrapper, Vector3? relatedWorld)
    {
        var socket = wrapper.GetSocketTransform(entry.SocketId);
        var position = socket != null ? socket.TransformPoint(entry.LocalOffset) : LastSpawnPosition + entry.LocalOffset;
        var rotation = ResolveSpawnRotation(socket, position, relatedWorld);
        var instance = Instantiate(entry.Prefab, position, rotation * Quaternion.Euler(entry.LocalEulerAngles));
        instance.name = $"{entry.Prefab.name}_{entry.CueType}";
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, entry.LocalScale);
        if (entry.ParentToSocket && socket != null)
        {
            instance.transform.SetParent(socket, worldPositionStays: true);
        }

        LastSpawnedPrefabName = entry.Prefab.name;
        _activeSpawnedVfx.Add(instance);
        if (Application.isPlaying)
        {
            StartCoroutine(ReleaseSpawnedVfxAfterLifetime(instance, entry.LifetimeSeconds));
        }
    }

    private static Quaternion ResolveSpawnRotation(Transform? socket, Vector3 position, Vector3? relatedWorld)
    {
        if (relatedWorld.HasValue)
        {
            var direction = relatedWorld.Value - position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        return socket != null ? socket.rotation : Quaternion.identity;
    }

    private System.Collections.IEnumerator ReleaseSpawnedVfxAfterLifetime(GameObject instance, float lifetimeSeconds)
    {
        yield return new WaitForSeconds(lifetimeSeconds);
        _activeSpawnedVfx.Remove(instance);
        if (instance != null)
        {
            DestroyPresentationObject(instance);
        }
    }

    private static bool TryResolveSocket(BattlePresentationCueType cueType, out BattleActorSocketId socketId)
    {
        socketId = cueType switch
        {
            BattlePresentationCueType.WindupEnter => BattleActorSocketId.Telegraph,
            BattlePresentationCueType.ActionCommitBasic => BattleActorSocketId.ProjectileOrigin,
            BattlePresentationCueType.ActionCommitSkill => BattleActorSocketId.ProjectileOrigin,
            BattlePresentationCueType.ActionCommitHeal => BattleActorSocketId.Cast,
            BattlePresentationCueType.ImpactDamage => BattleActorSocketId.Hit,
            BattlePresentationCueType.ImpactHeal => BattleActorSocketId.Head,
            BattlePresentationCueType.GuardEnter => BattleActorSocketId.Telegraph,
            BattlePresentationCueType.GuardExit => BattleActorSocketId.Telegraph,
            BattlePresentationCueType.RepositionStart => BattleActorSocketId.FeetRing,
            BattlePresentationCueType.RepositionStop => BattleActorSocketId.FeetRing,
            BattlePresentationCueType.DeathStart => BattleActorSocketId.Center,
            _ => BattleActorSocketId.Center,
        };

        return cueType is BattlePresentationCueType.WindupEnter
            or BattlePresentationCueType.ActionCommitBasic
            or BattlePresentationCueType.ActionCommitSkill
            or BattlePresentationCueType.ActionCommitHeal
            or BattlePresentationCueType.ImpactDamage
            or BattlePresentationCueType.ImpactHeal
            or BattlePresentationCueType.GuardEnter
            or BattlePresentationCueType.GuardExit
            or BattlePresentationCueType.RepositionStart
            or BattlePresentationCueType.RepositionStop
            or BattlePresentationCueType.DeathStart;
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
}
