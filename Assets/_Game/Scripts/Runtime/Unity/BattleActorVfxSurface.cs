using SM.Combat.Model;
using SM.Combat.Services;
using UnityEngine;

namespace SM.Unity;

public sealed class BattleActorVfxSurface : MonoBehaviour
{
    // 원거리 commit(활/지팡이)의 투사체 비행 연출 상수. presentation 전용 — sim truth와 무관.
    private const float MinProjectileTravelSeconds = 0.12f;
    private const float MaxProjectileTravelSeconds = 0.5f;
    private const float ProjectileVisualSpeed = 22f;
    private const float ProjectileArrivalLingerSeconds = 0.04f;

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
            SpawnCatalogEntry(entry, wrapper, relatedWorld, cue);
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

    private void SpawnCatalogEntry(BattleVfxCatalogEntry entry, BattleActorWrapper wrapper, Vector3? relatedWorld, BattlePresentationCue cue)
    {
        var socket = wrapper.GetSocketTransform(entry.SocketId);
        var position = socket != null ? socket.TransformPoint(entry.LocalOffset) : LastSpawnPosition + entry.LocalOffset;
        var rotation = ResolveSpawnRotation(socket, position, relatedWorld);
        var instance = Instantiate(entry.Prefab, position, rotation * Quaternion.Euler(entry.LocalEulerAngles));
        instance.name = $"{entry.Prefab.name}_{entry.CueType}";
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, entry.LocalScale);

        // 활/지팡이 원거리 commit은 시전자 ProjectileOrigin에서 대상까지 실제로 가로질러 날아가는 투사체로 렌더한다.
        // (그 전엔 origin에 정적 burst만 떠서 화살/마법탄이 갭을 건너지 않아 "거리/모션 없이 피만 닳는" 것처럼 보였다.)
        // 근접 휘두름·임팩트·힐·가드 등 비투사체 cue는 기존대로 소켓에 정적 스폰한다.
        var isTravelingProjectile = relatedWorld.HasValue
            && entry.AnimationSemantic is BattleAnimationSemantic.BowShot or BattleAnimationSemantic.ProjectileCast;

        if (entry.ParentToSocket && socket != null && !isTravelingProjectile)
        {
            instance.transform.SetParent(socket, worldPositionStays: true);
        }

        LastSpawnedPrefabName = entry.Prefab.name;
        _activeSpawnedVfx.Add(instance);
        if (!Application.isPlaying)
        {
            return;
        }

        if (isTravelingProjectile)
        {
            var (releaseDelaySeconds, travelSeconds) = ResolveProjectileFlightWindow(cue, position, relatedWorld!.Value);
            StartCoroutine(TravelProjectileThenRelease(
                instance, position, relatedWorld.Value, releaseDelaySeconds, travelSeconds));
        }
        else
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

    // J12 인과 복원: cue는 WindupStartTick에 소비되지만 투사체는 release tick(제스처가 시위를 놓는
    // 순간, driver의 원거리 핀과 같은 산식)까지 숨겨 두었다가 출발시키고, 비행은 release→ContactTick
    // 구간만 차지해 정확히 데미지 틱에 도착한다. 기존엔 windup 시작에 출발해 '이펙트가 제스처를
    // 선행'하는 역전이 보였다. 스케줄이 없으면 거리/속도 추정 즉시 출발 폴백. presentation 전용.
    private static (float releaseDelaySeconds, float travelSeconds) ResolveProjectileFlightWindow(
        BattlePresentationCue cue, Vector3 from, Vector3 to)
    {
        if (cue.CommitSchedule is { } schedule)
        {
            var releaseTick = BattleContactPinScheduler.ResolvePresentationReleaseTick(
                schedule.WindupStartTick, schedule.ContactTick, BattleContactPinScheduler.DefaultProjectileTravelTicks);
            var delaySeconds = Mathf.Max(0, releaseTick - schedule.WindupStartTick) * BattleSimulator.DefaultFixedStepSeconds;
            var travelSeconds = Mathf.Max(0, schedule.ContactTick - releaseTick) * BattleSimulator.DefaultFixedStepSeconds;
            return (delaySeconds, Mathf.Clamp(travelSeconds, MinProjectileTravelSeconds, MaxProjectileTravelSeconds));
        }

        var distance = Vector3.Distance(from, to);
        return (0f, Mathf.Clamp(distance / ProjectileVisualSpeed, MinProjectileTravelSeconds, MaxProjectileTravelSeconds));
    }

    // 투사체를 origin → 대상으로 직선 보간 이동시킨 뒤 짧은 잔상만 남기고 정리한다.
    // 임팩트 폭발은 별도 ImpactDamage cue가 대상의 Hit 소켓에서 띄우므로 여기서는 비행만 담당한다.
    private System.Collections.IEnumerator TravelProjectileThenRelease(
        GameObject instance, Vector3 from, Vector3 to, float releaseDelaySeconds, float travelSeconds)
    {
        if (releaseDelaySeconds > 0f && instance != null)
        {
            // release까지 숨김 — 같은 구간에 화살 nock(시위의 화살)이 가시 상태라 바통터치가 맞는다.
            instance.SetActive(false);
            yield return new WaitForSeconds(releaseDelaySeconds);
            if (instance == null)
            {
                yield break;
            }

            instance.SetActive(true);
        }

        var duration = Mathf.Max(0.0001f, travelSeconds);
        var elapsed = 0f;
        while (elapsed < duration && instance != null)
        {
            elapsed += Time.deltaTime;
            instance.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (instance != null)
        {
            instance.transform.position = to;
        }

        yield return new WaitForSeconds(ProjectileArrivalLingerSeconds);
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
