using System;
using System.Linq;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using SM.Unity.Sandbox;

namespace SM.Editor.Authoring.CombatSandbox;

public static class CombatSandboxExecutionService
{
    public static CombatSandboxRunRequest BuildRequest(CombatSandboxState state)
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());

        if (state.Config != null)
        {
            session.SetTeamPosture(state.Config.AllyPosture);
            session.SetTeamTactic(state.Config.TeamTacticId);
            if (state.Config.AllySlots.Count > 0)
            {
                foreach (var anchor in session.DeploymentAnchors)
                {
                    session.AssignHeroToAnchor(anchor, null);
                }

                foreach (var slot in state.Config.AllySlots.Where(slot => !string.IsNullOrWhiteSpace(slot.HeroId)))
                {
                    session.AssignHeroToAnchor(slot.Anchor, slot.HeroId);
                }
            }
        }

        var allySnapshot = session.BuildBattleLoadoutSnapshot();
        if (!lookup.TryGetCombatSnapshot(out var content, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var encounter = BuildEncounter(state.Config);
        var buildResult = BattleSetupBuilder.Build(Array.Empty<BattleParticipantSpec>(), encounter, content);
        if (!buildResult.IsSuccess)
        {
            throw new InvalidOperationException(buildResult.Error ?? "Sandbox encounter build failed.");
        }

        var seed = state.Config != null && state.Config.Seed != 0 ? state.Config.Seed : state.Seed;
        var batchCount = state.Config != null && state.Config.BatchCount > 0 ? state.Config.BatchCount : state.BatchCount;
        return new CombatSandboxRunRequest(
            allySnapshot,
            buildResult.Enemies,
            seed == 0 ? 17 : seed,
            Math.Max(1, batchCount),
            state.Config != null && !string.IsNullOrWhiteSpace(state.Config.Id) ? state.Config.Id : "sandbox.transient");
    }

    private static BattleEncounterPlan BuildEncounter(CombatSandboxConfig config)
    {
        if (config == null || config.EnemySlots.Count == 0)
        {
            return BattleEncounterPlans.CreateObserverSmokePlan();
        }

        return new BattleEncounterPlan(
            config.EnemySlots.Select(slot => new BattleParticipantSpec(
                string.IsNullOrWhiteSpace(slot.ParticipantId) ? $"enemy.{slot.ArchetypeId}.{slot.Anchor}" : slot.ParticipantId,
                string.IsNullOrWhiteSpace(slot.DisplayName) ? slot.ArchetypeId : slot.DisplayName,
                slot.ArchetypeId,
                slot.Anchor,
                slot.PositiveTraitId,
                slot.NegativeTraitId,
                Array.Empty<BattleEquippedItemSpec>(),
                slot.TemporaryAugmentIds,
                config.EnemyPosture,
                string.IsNullOrWhiteSpace(slot.RoleTag) ? "auto" : slot.RoleTag))
            .ToList(),
            config.EnemyPosture);
    }
}
