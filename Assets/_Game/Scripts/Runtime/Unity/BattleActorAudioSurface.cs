using UnityEngine;

namespace SM.Unity;

public sealed class BattleActorAudioSurface : MonoBehaviour
{
    public int TriggerCount { get; private set; }
    public BattlePresentationCueType LastCueType { get; private set; }
    public BattleActorSocketId LastSocketId { get; private set; }
    public string LastHookId { get; private set; } = string.Empty;

    public void ConsumeCue(BattlePresentationCue cue, BattleActorWrapper wrapper)
    {
        if (!TryResolveSocket(cue.CueType, out var socketId)
            || !TryResolveHookId(cue.CueType, out var hookId))
        {
            return;
        }

        TriggerCount++;
        LastCueType = cue.CueType;
        LastSocketId = socketId;
        LastHookId = hookId;
    }

    public void ClearTransientState(BattlePresentationCueType reason)
    {
    }

    private static bool TryResolveSocket(BattlePresentationCueType cueType, out BattleActorSocketId socketId)
    {
        socketId = cueType switch
        {
            BattlePresentationCueType.ActionCommitBasic => BattleActorSocketId.ProjectileOrigin,
            BattlePresentationCueType.ActionCommitSkill => BattleActorSocketId.ProjectileOrigin,
            BattlePresentationCueType.ActionCommitHeal => BattleActorSocketId.Cast,
            BattlePresentationCueType.ImpactDamage => BattleActorSocketId.Hit,
            BattlePresentationCueType.ImpactHeal => BattleActorSocketId.Hit,
            BattlePresentationCueType.GuardEnter => BattleActorSocketId.Center,
            BattlePresentationCueType.GuardExit => BattleActorSocketId.Center,
            BattlePresentationCueType.RepositionStart => BattleActorSocketId.FeetRing,
            BattlePresentationCueType.RepositionStop => BattleActorSocketId.FeetRing,
            BattlePresentationCueType.DeathStart => BattleActorSocketId.Center,
            _ => BattleActorSocketId.Center,
        };

        return cueType is BattlePresentationCueType.ActionCommitBasic
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

    public static bool TryResolveHookId(BattlePresentationCueType cueType, out string hookId)
    {
        hookId = cueType switch
        {
            BattlePresentationCueType.ActionCommitBasic => "sfx.combat.action_commit_basic",
            BattlePresentationCueType.ActionCommitSkill => "sfx.combat.action_commit_skill",
            BattlePresentationCueType.ActionCommitHeal => "sfx.combat.action_commit_heal",
            BattlePresentationCueType.ImpactDamage => "sfx.combat.impact_damage",
            BattlePresentationCueType.ImpactHeal => "sfx.combat.impact_heal",
            BattlePresentationCueType.GuardEnter => "sfx.combat.guard_enter",
            BattlePresentationCueType.GuardExit => "sfx.combat.guard_exit",
            BattlePresentationCueType.RepositionStart => "sfx.combat.reposition_start",
            BattlePresentationCueType.RepositionStop => "sfx.combat.reposition_stop",
            BattlePresentationCueType.DeathStart => "sfx.combat.death_start",
            _ => string.Empty,
        };

        return !string.IsNullOrWhiteSpace(hookId);
    }
}
