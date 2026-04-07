using UnityEngine;

namespace SM.Unity;

public sealed class BattleActorVfxSurface : MonoBehaviour
{
    public int TriggerCount { get; private set; }
    public BattlePresentationCueType LastCueType { get; private set; }
    public BattleActorSocketId LastSocketId { get; private set; }
    public Vector3 LastSpawnPosition { get; private set; }

    public void ConsumeCue(BattlePresentationCue cue, BattleActorWrapper wrapper)
    {
        if (!TryResolveSocket(cue.CueType, out var socketId))
        {
            return;
        }

        TriggerCount++;
        LastCueType = cue.CueType;
        LastSocketId = socketId;
        LastSpawnPosition = wrapper.GetSocketWorld(socketId);
    }

    public void ClearTransientState(BattlePresentationCueType reason)
    {
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
}
