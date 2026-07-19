using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>세션/씬과 .NET headless가 공유하는 battle-state 합성 경계.</summary>
public static class SessionBattleStateComposer
{
    public static bool TryCompose(
        ISessionContentLookup lookup,
        BattleLoadoutSnapshot allySnapshot,
        ResolvedEncounterContext encounter,
        out BattleState state,
        out string error)
    {
        state = null!;
        if (!lookup.TryGetCombatSnapshot(out var combatSnapshot, out error))
        {
            return false;
        }

        state = BattleFactory.Create(
            allySnapshot.Allies,
            encounter.Enemies,
            allySnapshot.TeamTactic.Posture,
            encounter.EnemyPosture,
            BattleSimulator.DefaultFixedStepSeconds,
            seed: encounter.Context.BattleSeed,
            statusRules: allySnapshot.StatusRules ?? CombatStatusRuleCompiler.Compile(combatSnapshot));
        new EncounterResolutionService(combatSnapshot).ApplyBattleBootstrap(state, encounter);
        return true;
    }
}
