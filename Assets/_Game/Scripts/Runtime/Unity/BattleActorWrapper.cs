using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public enum BattleActorSocketId
{
    Center = 0,
    Head = 1,
    Hud = 2,
    Hit = 3,
    FeetRing = 4,
    Telegraph = 5,
    Cast = 6,
    ProjectileOrigin = 7,
    CameraFocus = 8,
}

public readonly record struct BattleActorSocketStatus(
    BattleActorSocketId SocketId,
    Transform? AuthoredTransform,
    bool UsesFallback,
    Vector3 WorldPosition);

public sealed class BattleActorWrapper : MonoBehaviour
{
    private const float GroundPlaneY = -0.98f;

    [SerializeField] private Transform visualRoot = null!;
    [SerializeField] private Transform vendorVisualSlot = null!;
    [SerializeField] private Transform centerAnchor = null!;
    [SerializeField] private Transform headAnchor = null!;
    [SerializeField] private Transform hudAnchor = null!;
    [SerializeField] private Transform hitAnchor = null!;
    [SerializeField] private Transform feetRingAnchor = null!;
    [SerializeField] private Transform telegraphAnchor = null!;
    [SerializeField] private Transform castAnchor = null!;
    [SerializeField] private Transform projectileOrigin = null!;
    [SerializeField] private Transform cameraFocusTarget = null!;

    private float _resolvedHeadAnchorHeight = 2f;

    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public Transform VendorVisualSlot => vendorVisualSlot != null ? vendorVisualSlot : VisualRoot;

    public void ConfigureAuthoring(
        Transform visualRootTransform,
        Transform? vendorSlot,
        Transform? center,
        Transform? head,
        Transform? hud,
        Transform? hit,
        Transform? feetRing,
        Transform? telegraph,
        Transform? cast,
        Transform? projectile,
        Transform? cameraFocus)
    {
        visualRoot = visualRootTransform;
        vendorVisualSlot = vendorSlot!;
        centerAnchor = center!;
        headAnchor = head!;
        hudAnchor = hud!;
        hitAnchor = hit!;
        feetRingAnchor = feetRing!;
        telegraphAnchor = telegraph!;
        castAnchor = cast!;
        projectileOrigin = projectile!;
        cameraFocusTarget = cameraFocus!;
    }

    public void Configure(BattleUnitReadModel actor)
    {
        _resolvedHeadAnchorHeight = Mathf.Max(1.2f, actor.HeadAnchorHeight);
    }

    public Transform? GetSocketTransform(BattleActorSocketId socketId)
    {
        return socketId switch
        {
            BattleActorSocketId.Center => centerAnchor,
            BattleActorSocketId.Head => headAnchor,
            BattleActorSocketId.Hud => hudAnchor,
            BattleActorSocketId.Hit => hitAnchor,
            BattleActorSocketId.FeetRing => feetRingAnchor,
            BattleActorSocketId.Telegraph => telegraphAnchor,
            BattleActorSocketId.Cast => castAnchor,
            BattleActorSocketId.ProjectileOrigin => projectileOrigin,
            BattleActorSocketId.CameraFocus => cameraFocusTarget,
            _ => null,
        };
    }

    public bool UsesFallback(BattleActorSocketId socketId)
    {
        return GetSocketTransform(socketId) == null;
    }

    public Vector3 GetSocketWorld(BattleActorSocketId socketId)
    {
        var authored = GetSocketTransform(socketId);
        if (authored != null)
        {
            return authored.position;
        }

        return socketId switch
        {
            BattleActorSocketId.Center => transform.position + new Vector3(0f, 0.10f, 0f),
            BattleActorSocketId.Head => transform.position + new Vector3(0f, _resolvedHeadAnchorHeight, 0f),
            BattleActorSocketId.Hud => GetSocketWorld(BattleActorSocketId.Head),
            BattleActorSocketId.Hit => GetSocketWorld(BattleActorSocketId.Center),
            BattleActorSocketId.FeetRing => new Vector3(transform.position.x, GroundPlaneY, transform.position.z),
            BattleActorSocketId.Telegraph => GetSocketWorld(BattleActorSocketId.FeetRing),
            BattleActorSocketId.Cast => projectileOrigin != null
                ? projectileOrigin.position
                : GetSocketWorld(BattleActorSocketId.Center),
            BattleActorSocketId.ProjectileOrigin => castAnchor != null
                ? castAnchor.position
                : GetSocketWorld(BattleActorSocketId.Center),
            BattleActorSocketId.CameraFocus => GetSocketWorld(BattleActorSocketId.Center),
            _ => transform.position,
        };
    }

    public Vector3 GetAnchorWorld(BattlePresentationAnchorId anchorId)
    {
        return anchorId switch
        {
            BattlePresentationAnchorId.Root => transform.position,
            BattlePresentationAnchorId.Feet => GetSocketWorld(BattleActorSocketId.FeetRing),
            BattlePresentationAnchorId.Center => GetSocketWorld(BattleActorSocketId.Center),
            BattlePresentationAnchorId.Head => GetSocketWorld(BattleActorSocketId.Head),
            BattlePresentationAnchorId.Cast => GetSocketWorld(BattleActorSocketId.Cast),
            _ => transform.position,
        };
    }

    public IReadOnlyList<BattleActorSocketStatus> CaptureSocketStatus()
    {
        var results = new List<BattleActorSocketStatus>();
        foreach (BattleActorSocketId socketId in System.Enum.GetValues(typeof(BattleActorSocketId)))
        {
            var authored = GetSocketTransform(socketId);
            results.Add(new BattleActorSocketStatus(socketId, authored, authored == null, GetSocketWorld(socketId)));
        }

        return results;
    }
}
