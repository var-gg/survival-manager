using System;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using SM.Unity.Sandbox;

namespace SM.Editor.Authoring.CombatSandbox;

public static class CombatSandboxExecutionService
{
    public static CombatSandboxRunRequest BuildRequest(CombatSandboxState state, BattlefieldLayout? sceneLayout = null)
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

        var encounter = BuildEncounter(lookup, state.Config);
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
            state.Config != null && !string.IsNullOrWhiteSpace(state.Config.Id) ? state.Config.Id : "sandbox.transient",
            sceneLayout);
    }

    private static BattleEncounterPlan BuildEncounter(ICombatContentLookup lookup, CombatSandboxConfig? config)
    {
        if (config == null || config.EnemySlots.Count == 0)
        {
            return BattleEncounterPlans.CreateObserverSmokePlan();
        }

        return new BattleEncounterPlan(
            config.EnemySlots.Select(slot => new BattleParticipantSpec(
                string.IsNullOrWhiteSpace(slot.ParticipantId) ? $"enemy.{ResolveSandboxArchetypeId(lookup, slot)}.{slot.Anchor}" : slot.ParticipantId,
                string.IsNullOrWhiteSpace(slot.DisplayName) ? ResolveSandboxCharacterId(slot) : slot.DisplayName,
                ResolveSandboxArchetypeId(lookup, slot),
                slot.Anchor,
                slot.PositiveTraitId,
                slot.NegativeTraitId,
                Array.Empty<BattleEquippedItemSpec>(),
                slot.TemporaryAugmentIds,
                config.EnemyPosture,
                ResolveSandboxRoleTag(lookup, slot),
                "opening:standard",
                ResolveSandboxCharacterId(slot),
                ResolveSandboxRoleInstructionId(lookup, slot)))
            .ToList(),
            config.EnemyPosture);
    }

    private static string ResolveSandboxCharacterId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.CharacterId))
        {
            return slot.CharacterId;
        }

        return slot.ArchetypeIdOverride ?? string.Empty;
    }

    private static string ResolveSandboxArchetypeId(ICombatContentLookup lookup, CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride))
        {
            return slot.ArchetypeIdOverride;
        }

        if (lookup.TryGetCharacterDefinition(ResolveSandboxCharacterId(slot), out var character)
            && character.DefaultArchetype != null)
        {
            return character.DefaultArchetype.Id;
        }

        return ResolveSandboxCharacterId(slot);
    }

    private static string ResolveSandboxRoleInstructionId(ICombatContentLookup lookup, CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleInstructionId))
        {
            return slot.RoleInstructionId;
        }

        if (lookup.TryGetCharacterDefinition(ResolveSandboxCharacterId(slot), out var character)
            && character.DefaultRoleInstruction != null)
        {
            return character.DefaultRoleInstruction.Id;
        }

        if (lookup.TryGetArchetype(ResolveSandboxArchetypeId(lookup, slot), out var archetype))
        {
            return archetype.Class.Id switch
            {
                "vanguard" => "anchor",
                "duelist" => "bruiser",
                "ranger" => "carry",
                "mystic" => "support",
                _ => slot.Anchor.IsFrontRow() ? "frontline" : "backline",
            };
        }

        return string.Empty;
    }

    private static string ResolveSandboxRoleTag(ICombatContentLookup lookup, CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleTag) && !string.Equals(slot.RoleTag, "auto", StringComparison.Ordinal))
        {
            return slot.RoleTag;
        }

        var roleInstructionId = ResolveSandboxRoleInstructionId(lookup, slot);
        if (lookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction))
        {
            return roleInstruction.RoleTag;
        }

        return slot.Anchor.IsFrontRow() ? "frontline" : "backline";
    }
}
