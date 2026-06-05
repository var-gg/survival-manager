using System.Collections.Generic;
using SM.Core.Ids;

namespace SM.Combat.Model;

/// <summary>
/// Stable identity for one action instance — a single accepted windup -&gt; contact -&gt; recovery
/// lifecycle. Allocated monotonically by <see cref="BattleState"/> at the accepted BeginWindup
/// transition and never reused, so multi-hit / AOE / projectile / cancel / death events can be paired
/// deterministically across replay and save-load (GPT Pro J9/J23). Wraps a <c>long</c> rather than
/// <see cref="EntityId"/> because it identifies an <em>action</em>, not an entity.
/// </summary>
public readonly record struct ActionInstanceId(long Value)
{
    public static readonly ActionInstanceId None = new(0);

    public bool IsValid => Value > 0;
}

/// <summary>Combat-truth kind of the action that produced a choreography intent.</summary>
public enum CombatEventKind
{
    BasicAttack = 0,
    Skill = 1,
}

/// <summary>
/// Lifecycle phase of a <see cref="BattleCombatEventIntent"/>. <see cref="Started"/> is emitted at the
/// windup-begin tick (carrying the predicted <see cref="BattleCombatEventIntent.ContactTick"/> so
/// presentation can schedule anticipation ahead of contact); <see cref="Contacted"/> at the
/// authoritative damage-resolve tick; <see cref="Canceled"/> when an in-windup action is interrupted
/// before contact (stun / target-loss / range-loss). <see cref="Completed"/> is reserved for a later
/// recovery-end marker.
/// </summary>
public enum CombatEventIntentStatus
{
    Started = 0,
    Contacted = 1,
    Canceled = 2,
    Completed = 3,
}

/// <summary>
/// Combat-truth outcome of a single resolved contact. Labeled by the sim from its own resolution
/// result (never inferred by presentation). NOTE (Stage 1): currently derived from the resolved
/// <see cref="BattleEvent"/>; Stage 2 replaces this with a typed outcome returned directly by
/// CombatActionResolver so no string is read.
/// </summary>
public enum CombatOutcome
{
    Hit = 0,
    Crit = 1,
    Block = 2,
    Miss = 3,
    Dodge = 4,
    Knockdown = 5,
    Kill = 6,
}

/// <summary>
/// One resolved contact within an action instance. <see cref="ContactIndex"/> is unique per resolved
/// contact (1:1 with the <see cref="BattleEvent"/> it labels); <see cref="ContactGroupIndex"/> is the
/// actor-commit dedupe key shared by all contacts of one hit frame, so an AOE swing emits a single
/// actor commit cue but N target reactions (GPT Pro J22). Combat facts only — never presentation
/// clip names.
/// </summary>
public sealed record BattleContactIntent(
    int ContactIndex,
    int ContactGroupIndex,
    int ContactTick,
    EntityId? TargetId,
    CombatOutcome Outcome,
    float Value = 0f,
    bool IsHeal = false);

/// <summary>
/// Phase/contact contract for one action instance, emitted by the deterministic sim as a sibling of
/// <see cref="BattleMotionIntent"/> (action choreography seam, C1). Carries only sim-owned facts:
/// identity (<see cref="ActionInstanceId"/>), the canonical integer <see cref="ContactTick"/> fixed at
/// BeginWindup (so presentation can pin the visual contact frame to the exact damage tick — GPT Pro
/// J20), lifecycle <see cref="Status"/>, and the resolved <see cref="Contacts"/>. Presentation-derived
/// ticks (release / recovery-end) deliberately live on the Unity-side BattleChoreographySchedule,
/// never here, keeping the SM.Combat -&gt; SM.Unity seam one-directional (GPT Pro Closure 1 / J21).
/// <see cref="SkillId"/> is an opaque authored key only — SM.Combat never reads presentation metadata
/// from it.
/// </summary>
public sealed record BattleCombatEventIntent(
    int StepIndex,
    ActionInstanceId ActionInstanceId,
    EntityId ActorId,
    CombatEventKind Kind,
    SkillDelivery DeliveryKind,
    int WindupStartTick,
    int ContactTick,
    CombatEventIntentStatus Status,
    EntityId? InitialTargetId = null,
    int? CancelTick = null,
    string? SkillId = null,
    IReadOnlyList<BattleContactIntent>? Contacts = null);
