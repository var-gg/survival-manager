using SM.Core.Ids;

namespace SM.Combat.Model;

/// <summary>
/// Combat-truth classification of a single in-tick position transition. These are facts the sim
/// already computes (mobility destination, knockback vector, action-state-tagged locomotion) and
/// previously discarded right after <c>SetPosition</c>. Emitting them lets presentation animate
/// without reverse-engineering semantics from position deltas. Kinds are Combat facts only — never
/// presentation clip names — so the SM.Combat -> SM.Unity seam stays one-directional.
/// </summary>
public enum BattleMotionKind
{
    Approach = 0,
    Disengage = 1,
    Reposition = 2,
    MobilityDash = 3,
    Knockback = 4,
    ForcedDisplacement = 5,
}

/// <summary>
/// One position transition that occurred within <see cref="StepIndex"/>. <see cref="From"/> and
/// <see cref="To"/> are the truth positions immediately before and after the move.
/// <see cref="SequenceInStep"/> gives a deterministic order for multiple motions affecting the same
/// step (e.g. locomotion then separation, or attack then knockback). Identity uses the tick index
/// rather than seconds because 0.1f is not exact in binary float.
/// </summary>
public sealed record BattleMotionIntent(
    int StepIndex,
    int SequenceInStep,
    EntityId ActorId,
    BattleMotionKind Kind,
    CombatVector2 From,
    CombatVector2 To,
    EntityId? SourceActorId = null,
    bool IsDiscrete = false);
