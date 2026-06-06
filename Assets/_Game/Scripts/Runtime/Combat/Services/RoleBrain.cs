using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

/// <summary>
/// Phase 1 tactical brain. Chooses *what each unit is trying to do* (its <see cref="CombatIntent"/>) from its
/// role, posture, and battle context — a layer above the Phase 0 movement/attack executor. The executor stays
/// dumb and reliable; the intent biases which target and where the unit wants to be, so the fight reads as
/// role-driven drama (tank holds, duelist dives, ranger anchors, mystic saves) rather than a lockstep blob.
///
/// Deterministic: a pure function of battle truth (positions, roles, posture, target ids) — no RNG, no time —
/// so it reproduces exactly on re-sim from seed and needs no serialization. Anti-jitter commit windows and
/// hard interrupts (master design A) are layered on per behavior as those intents land; the baseline intents
/// here (Engage / AnchorFire) are stable by construction, so they need no commit gating.
/// </summary>
public static class RoleBrain
{
    public static void ResolveIntent(BattleState state, UnitSnapshot actor)
    {
        if (!actor.IsAlive)
        {
            return;
        }

        actor.SetCombatIntent(ResolveIntentFor(state, actor));
    }

    private static CombatIntent ResolveIntentFor(BattleState state, UnitSnapshot actor)
    {
        // Ranger / backline ranged → AnchorFire: hold the backline anchor and fire at whatever enters range.
        // It does not advance into melee to chase a distant enemy, and it does not kite (Phase 0). Deliberate
        // reposition-on-threat and scout/rift skirmish steps arrive as later role/archetype behavior.
        if (IsAnchoredRanged(actor))
        {
            var anchor = MovementResolver.ResolveHomePosition(state, actor);
            return new CombatIntent(CombatIntentType.AnchorFire, actor.CurrentTargetId, null, anchor, 0, 0);
        }

        // Mystic / backline support → SupportAnchor: hold the backline anchor and support/cast from there
        // instead of walking forward to poke an enemy. It still pursues allies normally to land heals/barriers
        // (the movement hold is gated to enemy targets only). Clutch-heal thresholds arrive as later behavior.
        if (IsBacklineSupport(actor))
        {
            var anchor = MovementResolver.ResolveHomePosition(state, actor);
            return new CombatIntent(CombatIntentType.SupportAnchor, actor.CurrentTargetId, null, anchor, 0, 0);
        }

        // Everyone else → Engage: pursue and fight with the clean Phase 0 pursuit. Per-role drama
        // (HoldLine/Peel/Dive) layers in over this baseline in subsequent phases.
        return CombatIntent.Engage(actor.CurrentTargetId);
    }

    private static bool IsAnchoredRanged(UnitSnapshot actor)
    {
        return actor.Behavior.FormationLine == FormationLine.Backline
               && actor.Definition.ClassId == "ranger";
    }

    private static bool IsBacklineSupport(UnitSnapshot actor)
    {
        return actor.Behavior.FormationLine == FormationLine.Backline
               && actor.Definition.ClassId == "mystic";
    }
}
