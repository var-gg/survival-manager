using SM.Core.Ids;

namespace SM.Combat.Model;

/// <summary>
/// Phase 1 tactical-brain intent. What a unit is *trying to do* this stretch of the fight, chosen at a low
/// cadence by <c>RoleBrain</c> rather than re-decided every 0.1s tick. The foundation movement/attack executor
/// (Phase 0) stays dumb and reliable; the intent biases *which* target and *where* the unit wants to be, so the
/// fight reads as role-driven drama (tank holds, duelist dives, ranger anchors, mystic saves) instead of a
/// lockstep blob — without per-tick jitter. Deterministic, transient (re-derived on replay from seed), so it
/// needs no serialization.
/// </summary>
public enum CombatIntentType
{
    None = 0,

    /// <summary>Pursue and fight the chosen target with the clean Phase 0 pursuit (default for most roles).</summary>
    Engage = 1,

    /// <summary>Tank: hold the frontline anchor band; engage what comes, do not over-chase past the line.</summary>
    HoldLine = 2,

    /// <summary>Melee DPS: committed dive onto an exposed backline target (ranger/mystic/low-HP).</summary>
    Dive = 3,

    /// <summary>Intercept a diver between it and the protected ally, rather than running to the ally directly.</summary>
    Peel = 4,

    /// <summary>Ranged: stand at the backline anchor and fire at whatever enters range — no advance, no kite.</summary>
    AnchorFire = 5,

    /// <summary>Support: hold backline near the carry and watch heal/barrier/peel thresholds.</summary>
    SupportAnchor = 6,

    /// <summary>Return to a safe line anchor after a dive/overextension.</summary>
    RejoinLine = 7,
}

/// <summary>
/// A unit's committed tactical intent. <see cref="AnchorPoint"/> is the formation / tactical destination the
/// unit wants to occupy; <see cref="TargetId"/> is the intent's chosen target (may differ from the raw nearest
/// enemy — e.g. a Peel targets the diver). <see cref="CommitUntilStep"/> guards against per-tick churn: the
/// brain does not reconsider before this step unless a hard interrupt fires. Pure value type — deterministic and
/// reconstructed on re-sim, never serialized. Dive entry metadata survives the one-tick transition back to a
/// baseline intent so the widened reachability allowance cannot be re-armed without measurable path progress.
/// </summary>
public readonly record struct CombatIntent(
    CombatIntentType Type,
    EntityId? TargetId,
    EntityId? ProtectAllyId,
    CombatVector2 AnchorPoint,
    int CommitUntilStep,
    int Priority,
    int DiveEntryStep = -1,
    EntityId? DiveEntryTargetId = null,
    float DiveEntryPathDistance = 0f,
    CombatVector2 DiveEntryActorPosition = default)
{
    public static readonly CombatIntent None = new(CombatIntentType.None, null, null, default, 0, 0);

    public static CombatIntent Engage(EntityId? targetId) =>
        new(CombatIntentType.Engage, targetId, null, default, 0, 0);
}
