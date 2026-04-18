using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.Sandbox;

public sealed class CombatSandboxScenarioCompiler
{
    private readonly ICombatContentLookup _lookup;
    private readonly LoadoutCompiler _loadoutCompiler = new();

    public CombatSandboxScenarioCompiler(ICombatContentLookup lookup)
    {
        _lookup = lookup;
    }

    public bool TryCompileScenario(
        CombatSandboxCompilationContext context,
        CombatSandboxConfig? config,
        CombatSandboxLaneKind laneKind,
        int? seedOverride,
        out CombatSandboxCompiledScenario scenario,
        out string error)
    {
        scenario = null!;
        error = string.Empty;

        if (config == null)
        {
            error = "Combat Sandbox active config is missing.";
            return false;
        }

        if (!_lookup.TryGetCombatSnapshot(out var content, out error))
        {
            return false;
        }

        var execution = ResolveExecution(config, seedOverride);
        var leftDefinition = ResolveLeftTeam(context, config, laneKind);
        var rightDefinition = ResolveRightTeam(config);

        if (!TryCompileTeam(context, leftDefinition, "left", content, out var leftTeam, out error))
        {
            return false;
        }

        if (!TryCompileTeam(context, rightDefinition, "right", content, out var rightTeam, out error))
        {
            return false;
        }

        var warnings = leftTeam.Warnings.Concat(rightTeam.Warnings).ToList();
        var scenarioId = !string.IsNullOrWhiteSpace(config.Scenario.ScenarioId)
            ? config.Scenario.ScenarioId
            : !string.IsNullOrWhiteSpace(config.Id)
                ? config.Id
                : "combat_sandbox.active";
        var displayName = !string.IsNullOrWhiteSpace(config.Scenario.DisplayName)
            ? config.Scenario.DisplayName
            : !string.IsNullOrWhiteSpace(config.DisplayName)
                ? config.DisplayName
                : "Combat Sandbox";

        scenario = new CombatSandboxCompiledScenario(
            scenarioId,
            displayName,
            laneKind,
            execution.Seed,
            execution,
            leftTeam,
            rightTeam,
            warnings);
        return true;
    }

    private CombatSandboxExecutionSettings ResolveExecution(CombatSandboxConfig config, int? seedOverride)
    {
        var execution = CloneExecution(config.Execution);
        var useLegacyFallback = !config.UseScenarioAuthoring || string.IsNullOrWhiteSpace(execution.PresetId);
        if (string.IsNullOrWhiteSpace(execution.DisplayName))
        {
            execution.DisplayName = "Active Runtime";
        }

        if (string.IsNullOrWhiteSpace(execution.PresetId))
        {
            execution.PresetId = "runtime.active";
        }

        if (useLegacyFallback)
        {
            if (config.Seed != 0)
            {
                execution.Seed = config.Seed;
            }

            if (config.BatchCount > 0)
            {
                execution.BatchCount = config.BatchCount;
            }
        }

        if (execution.BatchCount <= 0)
        {
            execution.BatchCount = Math.Max(1, config.BatchCount);
        }

        execution.Seed = seedOverride ?? ResolveSeed(config, execution);
        return execution;
    }

    private static int ResolveSeed(CombatSandboxConfig config, CombatSandboxExecutionSettings execution)
    {
        if (execution.Seed != 0)
        {
            return execution.Seed;
        }

        return config.Seed != 0 ? config.Seed : 17;
    }

    private CombatSandboxTeamDefinition ResolveLeftTeam(
        CombatSandboxCompilationContext context,
        CombatSandboxConfig config,
        CombatSandboxLaneKind laneKind)
    {
        if (config.UseScenarioAuthoring)
        {
            if (laneKind == CombatSandboxLaneKind.TownIntegrationSmoke)
            {
                return BuildTownSmokeLeftTeam(context, config.LeftTeam);
            }

            if (HasRuntimeTeamDefinition(config.LeftTeam))
            {
                return CloneTeam(config.LeftTeam);
            }
        }

        return BuildLegacyLeftTeam(config, context);
    }

    private CombatSandboxTeamDefinition ResolveRightTeam(CombatSandboxConfig config)
    {
        if (config.UseScenarioAuthoring && HasRuntimeTeamDefinition(config.RightTeam))
        {
            return CloneTeam(config.RightTeam);
        }

        return BuildLegacyRightTeam(config);
    }

    private bool TryCompileTeam(
        CombatSandboxCompilationContext context,
        CombatSandboxTeamDefinition team,
        string teamKey,
        CombatContentSnapshot content,
        out CombatSandboxCompiledTeam compiled,
        out string error)
    {
        return team.SourceMode == SandboxLoadoutSourceKind.CurrentLocalProfile
            ? TryCompileCurrentProfileTeam(context, team, teamKey, content, out compiled, out error)
            : TryCompileSyntheticTeam(context, team, teamKey, content, out compiled, out error);
    }

    private bool TryCompileCurrentProfileTeam(
        CombatSandboxCompilationContext context,
        CombatSandboxTeamDefinition team,
        string teamKey,
        CombatContentSnapshot content,
        out CombatSandboxCompiledTeam compiled,
        out string error)
    {
        compiled = null!;
        error = string.Empty;

        var warnings = new List<string>();
        var members = ResolveProfileMembers(context, team, warnings);
        if (members.Count == 0)
        {
            error = $"Combat Sandbox {teamKey} team does not resolve to any local-profile hero.";
            return false;
        }

        var profileHeroes = context.Profile.Heroes
            .Where(hero => !string.IsNullOrWhiteSpace(hero.HeroId))
            .ToDictionary(hero => hero.HeroId, hero => hero, StringComparer.Ordinal);
        var profileLoadouts = context.Profile.HeroLoadouts
            .Where(loadout => !string.IsNullOrWhiteSpace(loadout.HeroId))
            .ToDictionary(loadout => loadout.HeroId, loadout => loadout, StringComparer.Ordinal);
        var profileProgressions = context.Profile.HeroProgressions
            .Where(record => !string.IsNullOrWhiteSpace(record.HeroId))
            .ToDictionary(record => record.HeroId, record => record, StringComparer.Ordinal);
        var profileSelections = context.Profile.PassiveSelections
            .Where(record => !string.IsNullOrWhiteSpace(record.HeroId))
            .ToDictionary(record => record.HeroId, record => record, StringComparer.Ordinal);
        var profileItems = context.Profile.Inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemInstanceId))
            .ToDictionary(item => item.ItemInstanceId, item => item, StringComparer.Ordinal);
        var skillInstances = context.Profile.SkillInstances
            .Where(skill => !string.IsNullOrWhiteSpace(skill.SkillInstanceId))
            .ToDictionary(
                skill => skill.SkillInstanceId,
                skill => new SkillInstanceState(
                    skill.SkillInstanceId,
                    skill.SkillId,
                    skill.SlotKind,
                    skill.CompileTags ?? new List<string>(),
                    skill.ResolvedSlotKind),
                StringComparer.Ordinal);

        var heroes = new List<HeroRecord>();
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var passiveSelections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var deploymentAssignments = new Dictionary<DeploymentAnchorId, string>();
        var heroRoleIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var squadHeroIds = new List<string>();
        var temporaryAugmentIds = context.CurrentTemporaryAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var permanentAugmentIds = GetCurrentPermanentAugmentIds(context.Profile).ToList();

        foreach (var member in members)
        {
            if (!profileHeroes.TryGetValue(member.HeroId, out var hero))
            {
                warnings.Add($"Missing local hero '{member.HeroId}' for team '{team.DisplayName}'.");
                continue;
            }

            var heroRecord = CreateHeroRecord(hero);
            var loadoutState = profileLoadouts.TryGetValue(member.HeroId, out var loadout)
                ? CreateHeroLoadoutState(loadout)
                : CreateHeroLoadoutState(hero);
            var progressionState = profileProgressions.TryGetValue(member.HeroId, out var progression)
                ? CreateHeroProgressionState(progression)
                : CreateHeroProgressionState(member.HeroId);
            var passiveSelection = profileSelections.TryGetValue(member.HeroId, out var selection)
                ? CreatePassiveSelectionState(selection)
                : CreatePassiveSelectionState(member.HeroId, loadoutState.PassiveBoardId, loadoutState.SelectedPassiveNodeIds);

            foreach (var itemId in loadoutState.EquippedItemInstanceIds)
            {
                if (!itemInstances.ContainsKey(itemId) && profileItems.TryGetValue(itemId, out var item))
                {
                    itemInstances[itemId] = CreateItemInstanceState(item);
                }
            }

            ApplyBuildOverride(
                ref heroRecord,
                ref loadoutState,
                ref progressionState,
                ref passiveSelection,
                itemInstances,
                temporaryAugmentIds,
                permanentAugmentIds,
                member);

            if (!TryRegisterMember(member, heroRecord.Id, member.Anchor, warnings, deploymentAssignments, squadHeroIds, heroRoleIds))
            {
                continue;
            }

            heroes.Add(heroRecord);
            heroLoadouts[heroRecord.Id] = loadoutState;
            heroProgressions[heroRecord.Id] = progressionState;
            passiveSelections[heroRecord.Id] = passiveSelection;
        }

        if (heroes.Count == 0)
        {
            warnings.Add($"Combat Sandbox {teamKey} team could not compile any local-profile hero. Falling back to starter synthetic roster.");
            return TryCompileSyntheticTeam(
                context,
                BuildCurrentProfileFallbackTeam(team),
                teamKey,
                content,
                out compiled,
                out error);
        }

        MergeAugments(team, temporaryAugmentIds, permanentAugmentIds);

        var blueprint = BuildBlueprint(team, teamKey, deploymentAssignments, squadHeroIds, heroRoleIds, context.CurrentTeamPosture, context.CurrentTeamTacticId);
        var overlay = BuildOverlay(team, context.CurrentNodeIndex, temporaryAugmentIds);
        var snapshot = FinalizeSnapshot(
            _loadoutCompiler.Compile(
                heroes,
                heroLoadouts,
                heroProgressions,
                itemInstances,
                skillInstances,
                passiveSelections,
                new PermanentAugmentLoadoutState(blueprint.BlueprintId, permanentAugmentIds),
                blueprint,
                overlay,
                content),
            team);

        compiled = new CombatSandboxCompiledTeam(
            ResolveTeamId(team, teamKey),
            ResolveTeamDisplayName(team, teamKey),
            team.SourceMode,
            ResolveProvenanceLabel(team),
            blueprint,
            overlay,
            snapshot,
            team.Tags,
            warnings);
        return true;
    }

    private bool TryCompileSyntheticTeam(
        CombatSandboxCompilationContext context,
        CombatSandboxTeamDefinition team,
        string teamKey,
        CombatContentSnapshot content,
        out CombatSandboxCompiledTeam compiled,
        out string error)
    {
        compiled = null!;
        error = string.Empty;

        if (team.SourceMode == SandboxLoadoutSourceKind.RemoteDeckRef && team.Members.Count == 0)
        {
            error = $"Combat Sandbox {teamKey} remote deck source is not connected yet and does not have a cached snapshot.";
            return false;
        }

        var warnings = new List<string>();
        var heroes = new List<HeroRecord>();
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var passiveSelections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var deploymentAssignments = new Dictionary<DeploymentAnchorId, string>();
        var heroRoleIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var squadHeroIds = new List<string>();
        var temporaryAugmentIds = new List<string>();
        var permanentAugmentIds = new List<string>();

        foreach (var member in team.Members.Where(member => member != null))
        {
            if (!TryBuildSyntheticMember(
                    context,
                    member,
                    heroes.Count,
                    content,
                    itemInstances,
                    temporaryAugmentIds,
                    permanentAugmentIds,
                    out var heroRecord,
                    out var loadoutState,
                    out var progressionState,
                    out var passiveSelection,
                    out var roleInstructionId,
                    out var memberError))
            {
                if (!string.IsNullOrWhiteSpace(memberError))
                {
                    warnings.Add(memberError);
                }

                continue;
            }

            if (!TryRegisterMember(member, heroRecord.Id, member.Anchor, warnings, deploymentAssignments, squadHeroIds, heroRoleIds, roleInstructionId))
            {
                continue;
            }

            heroes.Add(heroRecord);
            heroLoadouts[heroRecord.Id] = loadoutState;
            heroProgressions[heroRecord.Id] = progressionState;
            passiveSelections[heroRecord.Id] = passiveSelection;
        }

        if (heroes.Count == 0)
        {
            var authoredMembers = string.Join(
                " | ",
                team.Members
                    .Where(member => member != null)
                    .Select(member => $"{member.SourceKind}:{member.CharacterId}/{member.ArchetypeId}/{member.HeroId}@{member.Anchor}"));
            error = warnings.Count == 0
                ? $"Combat Sandbox {teamKey} team does not contain any valid authored member. Members={authoredMembers}"
                : $"Combat Sandbox {teamKey} team does not contain any valid authored member. Members={authoredMembers} | {string.Join(" | ", warnings.Distinct(StringComparer.Ordinal))}";
            return false;
        }

        if (team.SourceMode == SandboxLoadoutSourceKind.RemoteDeckRef)
        {
            warnings.Add($"Remote deck '{team.RemoteDeck.UserId}/{team.RemoteDeck.DeckId}/{team.RemoteDeck.Revision}' is stubbed. Using the cached authored snapshot baked into the active config.");
        }

        MergeAugments(team, temporaryAugmentIds, permanentAugmentIds);

        var blueprint = BuildBlueprint(team, teamKey, deploymentAssignments, squadHeroIds, heroRoleIds, TeamPostureType.StandardAdvance, string.Empty);
        var overlay = BuildOverlay(team, context.CurrentNodeIndex, temporaryAugmentIds);
        var snapshot = FinalizeSnapshot(
            _loadoutCompiler.Compile(
                heroes,
                heroLoadouts,
                heroProgressions,
                itemInstances,
                new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
                passiveSelections,
                new PermanentAugmentLoadoutState(blueprint.BlueprintId, permanentAugmentIds),
                blueprint,
                overlay,
                content),
            team);

        compiled = new CombatSandboxCompiledTeam(
            ResolveTeamId(team, teamKey),
            ResolveTeamDisplayName(team, teamKey),
            team.SourceMode,
            ResolveProvenanceLabel(team),
            blueprint,
            overlay,
            snapshot,
            team.Tags,
            warnings);
        return true;
    }

    private bool TryBuildSyntheticMember(
        CombatSandboxCompilationContext context,
        CombatSandboxTeamMemberDefinition member,
        int memberIndex,
        CombatContentSnapshot content,
        IDictionary<string, ItemInstanceState> itemInstances,
        ICollection<string> temporaryAugmentIds,
        ICollection<string> permanentAugmentIds,
        out HeroRecord heroRecord,
        out HeroLoadoutState loadoutState,
        out HeroProgressionState progressionState,
        out PassiveBoardSelectionState passiveSelection,
        out string roleInstructionId,
        out string error)
    {
        error = string.Empty;
        roleInstructionId = ResolveRoleInstructionId(member);

        if (member.SourceKind == SandboxUnitSourceKind.LocalProfileHero)
        {
            var profileHero = context.Profile.Heroes.FirstOrDefault(hero =>
                string.Equals(hero.HeroId, member.HeroId, StringComparison.Ordinal));
            if (profileHero == null)
            {
                error = $"Missing local hero '{member.HeroId}' for authored synthetic team.";
                heroRecord = null!;
                loadoutState = null!;
                progressionState = null!;
                passiveSelection = null!;
                return false;
            }

            heroRecord = CreateHeroRecord(profileHero);
            var loadoutRecord = context.Profile.HeroLoadouts.FirstOrDefault(record =>
                string.Equals(record.HeroId, profileHero.HeroId, StringComparison.Ordinal));
            loadoutState = loadoutRecord != null
                ? CreateHeroLoadoutState(loadoutRecord)
                : CreateHeroLoadoutState(profileHero);
            var progressionRecord = context.Profile.HeroProgressions.FirstOrDefault(record =>
                string.Equals(record.HeroId, profileHero.HeroId, StringComparison.Ordinal));
            progressionState = progressionRecord != null
                ? CreateHeroProgressionState(progressionRecord)
                : CreateHeroProgressionState(profileHero.HeroId);
            var passiveSelectionRecord = context.Profile.PassiveSelections.FirstOrDefault(record =>
                string.Equals(record.HeroId, profileHero.HeroId, StringComparison.Ordinal));
            passiveSelection = passiveSelectionRecord != null
                ? CreatePassiveSelectionState(passiveSelectionRecord)
                : CreatePassiveSelectionState(profileHero.HeroId, loadoutState.PassiveBoardId, loadoutState.SelectedPassiveNodeIds);

            foreach (var itemId in loadoutState.EquippedItemInstanceIds)
            {
                if (itemInstances.ContainsKey(itemId))
                {
                    continue;
                }

                var itemRecord = context.Profile.Inventory.FirstOrDefault(item =>
                    string.Equals(item.ItemInstanceId, itemId, StringComparison.Ordinal));
                if (itemRecord != null)
                {
                    itemInstances[itemId] = CreateItemInstanceState(itemRecord);
                }
            }
        }
        else
        {
            if (!TryResolveArchetype(member, content, out var archetype, out var characterId, out error))
            {
                heroRecord = null!;
                loadoutState = null!;
                progressionState = null!;
                passiveSelection = null!;
                return false;
            }

            var heroId = string.IsNullOrWhiteSpace(member.MemberId)
                ? $"sandbox.{ResolveTeamMemberToken(member)}.{memberIndex + 1}"
                : member.MemberId;
            var displayName = !string.IsNullOrWhiteSpace(member.DisplayName)
                ? member.DisplayName
                : string.IsNullOrWhiteSpace(characterId)
                    ? archetype.Id
                    : characterId;
            heroRecord = new HeroRecord(
                heroId,
                displayName,
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                RecruitTier.Common,
                RecruitOfferSource.RecruitPhase,
                new UnitRetrainState(),
                new UnitEconomyFootprint(),
                characterId);
            loadoutState = new HeroLoadoutState(heroId, Array.Empty<string>(), Array.Empty<string>(), string.Empty, Array.Empty<string>(), Array.Empty<string>());
            progressionState = new HeroProgressionState(heroId, 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).ToList());
            passiveSelection = new PassiveBoardSelectionState(heroId, string.Empty, Array.Empty<string>());
        }

        ApplyBuildOverride(
            ref heroRecord,
            ref loadoutState,
            ref progressionState,
            ref passiveSelection,
            itemInstances,
            temporaryAugmentIds,
            permanentAugmentIds,
            member);
        return true;
    }

    private static bool TryResolveArchetype(
        CombatSandboxTeamMemberDefinition member,
        CombatContentSnapshot content,
        out CombatArchetypeTemplate archetype,
        out string characterId,
        out string error)
    {
        archetype = null!;
        characterId = string.Empty;
        error = string.Empty;

        if (member.SourceKind == SandboxUnitSourceKind.Character)
        {
            characterId = member.CharacterId;
            if (string.IsNullOrWhiteSpace(characterId))
            {
                error = "Character source requires CharacterId.";
                return false;
            }

            if (content.Characters == null || !content.Characters.TryGetValue(characterId, out var character))
            {
                error = $"Character '{characterId}' is missing from combat content.";
                return false;
            }

            var archetypeId = !string.IsNullOrWhiteSpace(member.ArchetypeId)
                ? member.ArchetypeId
                : character.DefaultArchetypeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(archetypeId) || !content.Archetypes.TryGetValue(archetypeId, out archetype))
            {
                error = $"Character '{characterId}' does not resolve to a valid archetype.";
                return false;
            }

            return true;
        }

        var requestedArchetypeId = !string.IsNullOrWhiteSpace(member.ArchetypeId)
            ? member.ArchetypeId
            : member.CharacterId;
        if (string.IsNullOrWhiteSpace(requestedArchetypeId) || !content.Archetypes.TryGetValue(requestedArchetypeId, out archetype))
        {
            error = $"Archetype '{requestedArchetypeId}' is missing from combat content.";
            return false;
        }

        characterId = member.CharacterId;
        return true;
    }

    private static List<CombatSandboxTeamMemberDefinition> ResolveProfileMembers(
        CombatSandboxCompilationContext context,
        CombatSandboxTeamDefinition team,
        ICollection<string> warnings)
    {
        var configuredMembers = team.Members
            .Where(member => member != null && !string.IsNullOrWhiteSpace(member.HeroId))
            .ToList();
        if (configuredMembers.Count > 0)
        {
            return configuredMembers;
        }

        var result = new List<CombatSandboxTeamMemberDefinition>();
        foreach (var pair in context.CurrentDeploymentAssignments
                     .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                     .OrderBy(pair => pair.Key))
        {
            result.Add(new CombatSandboxTeamMemberDefinition
            {
                MemberId = pair.Value!,
                HeroId = pair.Value!,
                SourceKind = SandboxUnitSourceKind.LocalProfileHero,
                Anchor = pair.Key,
            });
        }

        if (result.Count == 0)
        {
            warnings.Add("Current local profile does not have a ready deployment. Falling back to squad order.");
            foreach (var heroId in context.CurrentSquadHeroIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
            {
                result.Add(new CombatSandboxTeamMemberDefinition
                {
                    MemberId = heroId,
                    HeroId = heroId,
                    SourceKind = SandboxUnitSourceKind.LocalProfileHero,
                    Anchor = result.Count < 2
                        ? DeploymentAnchorId.FrontCenter
                        : result.Count == 2
                            ? DeploymentAnchorId.BackTop
                            : DeploymentAnchorId.BackBottom,
                });
            }
        }

        return result;
    }

    private static bool TryRegisterMember(
        CombatSandboxTeamMemberDefinition member,
        string heroId,
        DeploymentAnchorId anchor,
        ICollection<string> warnings,
        IDictionary<DeploymentAnchorId, string> deploymentAssignments,
        ICollection<string> squadHeroIds,
        IDictionary<string, string> heroRoleIds,
        string explicitRoleInstructionId = "")
    {
        if (deploymentAssignments.ContainsKey(anchor))
        {
            warnings.Add($"Duplicate anchor '{anchor}' in sandbox team. Keeping the first member.");
            return false;
        }

        if (squadHeroIds.Any(id => string.Equals(id, heroId, StringComparison.Ordinal)))
        {
            warnings.Add($"Duplicate member id '{heroId}' in sandbox team. Keeping the first member.");
            return false;
        }

        deploymentAssignments[anchor] = heroId;
        squadHeroIds.Add(heroId);

        var roleInstructionId = !string.IsNullOrWhiteSpace(explicitRoleInstructionId)
            ? explicitRoleInstructionId
            : ResolveRoleInstructionId(member);
        if (!string.IsNullOrWhiteSpace(roleInstructionId))
        {
            heroRoleIds[heroId] = roleInstructionId;
        }

        return true;
    }

    private static void ApplyBuildOverride(
        ref HeroRecord heroRecord,
        ref HeroLoadoutState loadoutState,
        ref HeroProgressionState progressionState,
        ref PassiveBoardSelectionState passiveSelection,
        IDictionary<string, ItemInstanceState> itemInstances,
        ICollection<string> temporaryAugmentIds,
        ICollection<string> permanentAugmentIds,
        CombatSandboxTeamMemberDefinition member)
    {
        var buildOverride = member.BuildOverride;
        if (buildOverride == null)
        {
            return;
        }

        var retrainState = heroRecord.RetrainState?.Clone() ?? new UnitRetrainState();
        if (buildOverride.RetrainCount > 0)
        {
            retrainState.RetrainCount = buildOverride.RetrainCount;
        }

        heroRecord = heroRecord with
        {
            FlexActiveId = !string.IsNullOrWhiteSpace(buildOverride.FlexActiveSkillId) ? buildOverride.FlexActiveSkillId : heroRecord.FlexActiveId,
            FlexPassiveId = !string.IsNullOrWhiteSpace(buildOverride.FlexPassiveSkillId) ? buildOverride.FlexPassiveSkillId : heroRecord.FlexPassiveId,
            PositiveTraitId = !string.IsNullOrWhiteSpace(buildOverride.PositiveTraitId) ? buildOverride.PositiveTraitId : heroRecord.PositiveTraitId,
            NegativeTraitId = !string.IsNullOrWhiteSpace(buildOverride.NegativeTraitId) ? buildOverride.NegativeTraitId : heroRecord.NegativeTraitId,
            RetrainState = retrainState,
        };

        var equippedItemIds = loadoutState.EquippedItemInstanceIds.ToList();
        if (buildOverride.EquippedItems.Count > 0)
        {
            equippedItemIds = new List<string>(buildOverride.EquippedItems.Count);
            for (var index = 0; index < buildOverride.EquippedItems.Count; index++)
            {
                var itemOverride = buildOverride.EquippedItems[index];
                if (string.IsNullOrWhiteSpace(itemOverride.ItemId))
                {
                    continue;
                }

                var itemInstanceId = $"{heroRecord.Id}.sandbox.item.{index}";
                itemInstances[itemInstanceId] = new ItemInstanceState(
                    itemInstanceId,
                    itemOverride.ItemId,
                    itemOverride.AffixIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
                    heroRecord.Id);
                equippedItemIds.Add(itemInstanceId);
            }
        }

        var passiveBoardId = !string.IsNullOrWhiteSpace(buildOverride.PassiveBoardId)
            ? buildOverride.PassiveBoardId
            : loadoutState.PassiveBoardId;
        var passiveNodeIds = buildOverride.PassiveNodeIds.Count > 0
            ? buildOverride.PassiveNodeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
            : loadoutState.SelectedPassiveNodeIds.ToList();
        var equippedPermanentAugmentIds = buildOverride.PermanentAugmentIds.Count > 0
            ? buildOverride.PermanentAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
            : loadoutState.EquippedPermanentAugmentIds.ToList();

        loadoutState = new HeroLoadoutState(
            heroRecord.Id,
            equippedItemIds,
            loadoutState.EquippedSkillInstanceIds,
            passiveBoardId,
            passiveNodeIds,
            equippedPermanentAugmentIds);

        progressionState = progressionState with
        {
            UnlockedPassiveNodeIds = progressionState.UnlockedPassiveNodeIds
                .Concat(passiveNodeIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
        };

        passiveSelection = new PassiveBoardSelectionState(
            heroRecord.Id,
            passiveBoardId,
            passiveNodeIds);

        foreach (var augmentId in buildOverride.TemporaryAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            temporaryAugmentIds.Add(augmentId);
        }

        foreach (var augmentId in buildOverride.PermanentAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            permanentAugmentIds.Add(augmentId);
        }
    }

    private static SquadBlueprintState BuildBlueprint(
        CombatSandboxTeamDefinition team,
        string teamKey,
        IReadOnlyDictionary<DeploymentAnchorId, string> deploymentAssignments,
        IReadOnlyList<string> squadHeroIds,
        IReadOnlyDictionary<string, string> heroRoleIds,
        TeamPostureType fallbackPosture,
        string fallbackTacticId)
    {
        var posture = team.TeamPosture;
        var tacticId = team.TeamTacticId;
        if (team.SourceMode == SandboxLoadoutSourceKind.CurrentLocalProfile)
        {
            if (string.IsNullOrWhiteSpace(tacticId))
            {
                tacticId = fallbackTacticId;
            }

            if (string.IsNullOrWhiteSpace(team.TeamTacticId) && team.TeamPosture == TeamPostureType.StandardAdvance)
            {
                posture = fallbackPosture;
            }
        }

        return new SquadBlueprintState(
            ResolveTeamId(team, teamKey),
            ResolveTeamDisplayName(team, teamKey),
            posture,
            tacticId,
            deploymentAssignments,
            squadHeroIds,
            heroRoleIds);
    }

    private static RunOverlayState BuildOverlay(
        CombatSandboxTeamDefinition team,
        int currentNodeIndex,
        IEnumerable<string> temporaryAugmentIds)
    {
        return new RunOverlayState(
            currentNodeIndex,
            temporaryAugmentIds
                .Concat(team.TeamTemporaryAugmentIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Array.Empty<string>(),
            LoadoutCompiler.CurrentCompileVersion,
            string.Empty);
    }

    private static BattleLoadoutSnapshot FinalizeSnapshot(BattleLoadoutSnapshot snapshot, CombatSandboxTeamDefinition team)
    {
        var teamTags = snapshot.TeamTags
            .Concat(team.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
            .Concat(new[]
            {
                $"sandbox_source:{team.SourceMode}",
                !string.IsNullOrWhiteSpace(team.TeamId) ? $"sandbox_team:{team.TeamId}" : string.Empty,
            })
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();
        var provenance = snapshot.Provenance?.ToList() ?? new List<CompileProvenanceEntry>();
        provenance.Add(new CompileProvenanceEntry(
            "team",
            ModifierSource.Other,
            ResolveProvenanceLabel(team),
            "sandbox_provenance",
            new[]
            {
                $"sourceMode:{team.SourceMode}",
                $"teamId:{ResolveTeamId(team, "team")}",
            }));

        return snapshot with
        {
            TeamTags = teamTags,
            Provenance = provenance,
        };
    }

    private static void MergeAugments(
        CombatSandboxTeamDefinition team,
        ICollection<string> temporaryAugmentIds,
        ICollection<string> permanentAugmentIds)
    {
        foreach (var augmentId in team.TeamTemporaryAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            temporaryAugmentIds.Add(augmentId);
        }

        foreach (var augmentId in team.TeamPermanentAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            permanentAugmentIds.Add(augmentId);
        }
    }

    private static IEnumerable<string> GetCurrentPermanentAugmentIds(SaveProfile profile)
    {
        return profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds
            ?.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            ?? Array.Empty<string>();
    }

    private static HeroRecord CreateHeroRecord(HeroInstanceRecord hero)
    {
        return new HeroRecord(
            hero.HeroId,
            hero.Name,
            hero.ArchetypeId,
            hero.RaceId,
            hero.ClassId,
            hero.PositiveTraitId,
            hero.NegativeTraitId,
            hero.FlexActiveId,
            hero.FlexPassiveId,
            hero.RecruitTier,
            hero.RecruitSource,
            hero.RetrainState?.Clone() ?? new UnitRetrainState(),
            hero.EconomyFootprint?.Clone() ?? new UnitEconomyFootprint(),
            hero.CharacterId);
    }

    private static HeroLoadoutState CreateHeroLoadoutState(HeroLoadoutRecord loadout)
    {
        return new HeroLoadoutState(
            loadout.HeroId,
            NormalizeIds(loadout.EquippedItemInstanceIds),
            NormalizeIds(loadout.EquippedSkillInstanceIds),
            loadout.PassiveBoardId ?? string.Empty,
            NormalizeIds(loadout.SelectedPassiveNodeIds),
            NormalizeIds(loadout.EquippedPermanentAugmentIds));
    }

    private static HeroLoadoutState CreateHeroLoadoutState(HeroInstanceRecord hero)
    {
        return new HeroLoadoutState(
            hero.HeroId,
            NormalizeIds(hero.EquippedItemIds),
            Array.Empty<string>(),
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static HeroProgressionState CreateHeroProgressionState(HeroProgressionRecord progression)
    {
        return new HeroProgressionState(
            progression.HeroId,
            progression.Level,
            progression.Experience,
            NormalizeIds(progression.UnlockedPassiveNodeIds),
            NormalizeIds(progression.UnlockedSkillIds));
    }

    private static HeroProgressionState CreateHeroProgressionState(string heroId)
    {
        return new HeroProgressionState(heroId, 1, 0, Array.Empty<string>(), Array.Empty<string>());
    }

    private static PassiveBoardSelectionState CreatePassiveSelectionState(PassiveSelectionRecord selection)
    {
        return new PassiveBoardSelectionState(
            selection.HeroId,
            selection.BoardId ?? string.Empty,
            NormalizeIds(selection.SelectedNodeIds));
    }

    private static PassiveBoardSelectionState CreatePassiveSelectionState(string heroId, string boardId, IReadOnlyList<string> selectedNodeIds)
    {
        return new PassiveBoardSelectionState(
            heroId,
            boardId ?? string.Empty,
            NormalizeIds(selectedNodeIds));
    }

    private static ItemInstanceState CreateItemInstanceState(InventoryItemRecord item)
    {
        return new ItemInstanceState(
            item.ItemInstanceId,
            item.ItemBaseId,
            NormalizeIds(item.AffixIds),
            item.EquippedHeroId ?? string.Empty);
    }

    private static IReadOnlyList<string> NormalizeIds(IEnumerable<string>? ids)
    {
        if (ids == null)
        {
            return Array.Empty<string>();
        }

        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string ResolveRoleInstructionId(CombatSandboxTeamMemberDefinition member)
    {
        if (!string.IsNullOrWhiteSpace(member.RoleInstructionId))
        {
            return member.RoleInstructionId;
        }

        return member.BuildOverride != null && !string.IsNullOrWhiteSpace(member.BuildOverride.RoleInstructionIdOverride)
            ? member.BuildOverride.RoleInstructionIdOverride
            : string.Empty;
    }

    private static string ResolveTeamMemberToken(CombatSandboxTeamMemberDefinition member)
    {
        if (!string.IsNullOrWhiteSpace(member.ArchetypeId))
        {
            return member.ArchetypeId;
        }

        if (!string.IsNullOrWhiteSpace(member.CharacterId))
        {
            return member.CharacterId;
        }

        if (!string.IsNullOrWhiteSpace(member.HeroId))
        {
            return member.HeroId;
        }

        return "member";
    }

    private static bool HasRuntimeTeamDefinition(CombatSandboxTeamDefinition team)
    {
        if (team == null)
        {
            return false;
        }

        if (team.SourceMode == SandboxLoadoutSourceKind.CurrentLocalProfile)
        {
            return true;
        }

        return team.Members.Any(member => member != null
                                          && (!string.IsNullOrWhiteSpace(member.ArchetypeId)
                                              || !string.IsNullOrWhiteSpace(member.CharacterId)
                                              || !string.IsNullOrWhiteSpace(member.HeroId)))
               || !string.IsNullOrWhiteSpace(team.ProvenanceLabel)
               || team.Tags.Count > 0;
    }

    private static CombatSandboxTeamDefinition BuildTownSmokeLeftTeam(
        CombatSandboxCompilationContext context,
        CombatSandboxTeamDefinition existing)
    {
        var team = CloneTeam(existing);
        team.SourceMode = SandboxLoadoutSourceKind.CurrentLocalProfile;
        team.TeamPosture = context.CurrentTeamPosture;
        team.TeamTacticId = context.CurrentTeamTacticId;
        team.DisplayName = string.IsNullOrWhiteSpace(team.DisplayName) ? "Town Current Build" : team.DisplayName;
        team.Members = context.CurrentDeploymentAssignments
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key)
            .Select(pair => new CombatSandboxTeamMemberDefinition
            {
                MemberId = pair.Value!,
                HeroId = pair.Value!,
                SourceKind = SandboxUnitSourceKind.LocalProfileHero,
                Anchor = pair.Key,
            })
            .ToList();

        team.Tags = team.Tags
            .Append("town_smoke")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        team.ProvenanceLabel = "town.current_profile";
        return team;
    }

    private static CombatSandboxTeamDefinition BuildLegacyLeftTeam(CombatSandboxConfig config, CombatSandboxCompilationContext context)
    {
        var team = new CombatSandboxTeamDefinition
        {
            TeamId = "legacy.left",
            DisplayName = "Current Local Profile",
            SourceMode = SandboxLoadoutSourceKind.CurrentLocalProfile,
            TeamPosture = config.AllyPosture,
            TeamTacticId = string.IsNullOrWhiteSpace(config.TeamTacticId) ? context.CurrentTeamTacticId : config.TeamTacticId,
            ProvenanceLabel = "legacy.ally_slots",
            Notes = "Flattened from legacy quick battle ally slots.",
            Tags = new List<string> { "legacy", "current_profile" },
            Members = config.AllySlots
                .Where(slot => slot != null && !string.IsNullOrWhiteSpace(slot.HeroId))
                .Select(slot => new CombatSandboxTeamMemberDefinition
                {
                    MemberId = slot.HeroId,
                    HeroId = slot.HeroId,
                    SourceKind = SandboxUnitSourceKind.LocalProfileHero,
                    Anchor = slot.Anchor,
                    RoleInstructionId = slot.RoleInstructionIdOverride,
                    BuildOverride = new CombatSandboxBuildOverrideData
                    {
                        RoleInstructionIdOverride = slot.RoleInstructionIdOverride,
                    }
                })
                .ToList(),
        };

        if (team.Members.Count == 0)
        {
            foreach (var pair in context.CurrentDeploymentAssignments
                         .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                         .OrderBy(pair => pair.Key))
            {
                team.Members.Add(new CombatSandboxTeamMemberDefinition
                {
                    MemberId = pair.Value!,
                    HeroId = pair.Value!,
                    SourceKind = SandboxUnitSourceKind.LocalProfileHero,
                    Anchor = pair.Key,
                });
            }
        }

        return team;
    }

    private static CombatSandboxTeamDefinition BuildLegacyRightTeam(CombatSandboxConfig config)
    {
        var team = new CombatSandboxTeamDefinition
        {
            TeamId = "legacy.right",
            DisplayName = "Legacy Observer Smoke",
            SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam,
            TeamPosture = config.EnemyPosture,
            TeamTacticId = config.EnemyTeamTacticId,
            ProvenanceLabel = "legacy.enemy_slots",
            Notes = "Flattened from legacy quick battle enemy slots.",
            Tags = new List<string> { "legacy", "observer_smoke" },
            Members = config.EnemySlots
                .Where(slot => slot != null)
                .Select(slot => new CombatSandboxTeamMemberDefinition
                {
                    MemberId = !string.IsNullOrWhiteSpace(slot.ParticipantId)
                        ? slot.ParticipantId
                        : $"enemy.{(!string.IsNullOrWhiteSpace(slot.CharacterId) ? slot.CharacterId : slot.ArchetypeIdOverride)}.{slot.Anchor}",
                    DisplayName = slot.DisplayName,
                    SourceKind = !string.IsNullOrWhiteSpace(slot.CharacterId)
                        ? SandboxUnitSourceKind.Character
                        : SandboxUnitSourceKind.Archetype,
                    CharacterId = slot.CharacterId,
                    ArchetypeId = slot.ArchetypeIdOverride,
                    Anchor = slot.Anchor,
                    RoleInstructionId = slot.RoleInstructionId,
                    BuildOverride = new CombatSandboxBuildOverrideData
                    {
                        PositiveTraitId = slot.PositiveTraitId,
                        NegativeTraitId = slot.NegativeTraitId,
                        TemporaryAugmentIds = slot.TemporaryAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                        RoleInstructionIdOverride = slot.RoleInstructionId,
                    }
                })
                .Where(member => !string.IsNullOrWhiteSpace(member.ArchetypeId) || !string.IsNullOrWhiteSpace(member.CharacterId))
                .ToList(),
        };

        if (team.Members.Count == 0)
        {
            team.Members = new List<CombatSandboxTeamMemberDefinition>
            {
                new() { MemberId = "enemy_guardian", DisplayName = "Enemy Guardian", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "guardian", Anchor = DeploymentAnchorId.FrontTop },
                new() { MemberId = "enemy_raider", DisplayName = "Enemy Raider", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "raider", Anchor = DeploymentAnchorId.FrontBottom },
                new() { MemberId = "enemy_hunter", DisplayName = "Enemy Hunter", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "hunter", Anchor = DeploymentAnchorId.BackTop },
                new() { MemberId = "enemy_hexer", DisplayName = "Enemy Hexer", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "hexer", Anchor = DeploymentAnchorId.BackBottom },
            };
        }

        return team;
    }

    private static CombatSandboxTeamDefinition BuildCurrentProfileFallbackTeam(CombatSandboxTeamDefinition source)
    {
        return new CombatSandboxTeamDefinition
        {
            TeamId = string.IsNullOrWhiteSpace(source.TeamId) ? "starter.synthetic_left" : source.TeamId,
            DisplayName = string.IsNullOrWhiteSpace(source.DisplayName) ? "Starter Synthetic" : source.DisplayName,
            SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam,
            TeamPosture = source.TeamPosture,
            TeamTacticId = source.TeamTacticId,
            ProvenanceLabel = string.IsNullOrWhiteSpace(source.ProvenanceLabel) ? "starter.synthetic_left" : source.ProvenanceLabel,
            Notes = string.IsNullOrWhiteSpace(source.Notes)
                ? "Fallback synthetic roster used when the local profile has no compile-ready hero."
                : source.Notes,
            Tags = source.Tags
                .Append("profile_fallback")
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Members = new List<CombatSandboxTeamMemberDefinition>
            {
                new() { MemberId = "ally_warden", DisplayName = "Warden", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "warden", Anchor = DeploymentAnchorId.FrontCenter, RoleInstructionId = "anchor" },
                new() { MemberId = "ally_slayer", DisplayName = "Slayer", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "slayer", Anchor = DeploymentAnchorId.FrontBottom, RoleInstructionId = "bruiser" },
                new() { MemberId = "ally_hunter", DisplayName = "Hunter", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "hunter", Anchor = DeploymentAnchorId.BackTop, RoleInstructionId = "carry" },
                new() { MemberId = "ally_priest", DisplayName = "Priest", SourceKind = SandboxUnitSourceKind.Character, CharacterId = "priest", Anchor = DeploymentAnchorId.BackBottom, RoleInstructionId = "support" },
            },
        };
    }

    private static CombatSandboxExecutionSettings CloneExecution(CombatSandboxExecutionSettings source)
    {
        return new CombatSandboxExecutionSettings
        {
            PresetId = source?.PresetId ?? string.Empty,
            DisplayName = source?.DisplayName ?? string.Empty,
            SeedMode = source?.SeedMode ?? SandboxSeedMode.Fixed,
            Seed = source?.Seed ?? 17,
            BatchCount = source?.BatchCount ?? 1,
            RunSideSwap = source?.RunSideSwap ?? false,
            RecordReplay = source?.RecordReplay ?? true,
            StopOnMismatch = source?.StopOnMismatch ?? false,
            StopOnReadabilityViolation = source?.StopOnReadabilityViolation ?? false,
            Notes = source?.Notes ?? string.Empty,
        };
    }

    private static CombatSandboxTeamDefinition CloneTeam(CombatSandboxTeamDefinition source)
    {
        return new CombatSandboxTeamDefinition
        {
            TeamId = source?.TeamId ?? string.Empty,
            DisplayName = source?.DisplayName ?? string.Empty,
            SourceMode = source?.SourceMode ?? SandboxLoadoutSourceKind.AuthoredSyntheticTeam,
            TeamPosture = source?.TeamPosture ?? TeamPostureType.StandardAdvance,
            TeamTacticId = source?.TeamTacticId ?? string.Empty,
            Tags = source?.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            Members = source?.Members?.Select(CloneMember).ToList() ?? new List<CombatSandboxTeamMemberDefinition>(),
            TeamTemporaryAugmentIds = source?.TeamTemporaryAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            TeamPermanentAugmentIds = source?.TeamPermanentAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            RemoteDeck = source?.RemoteDeck == null
                ? new CombatSandboxRemoteDeckReference()
                : new CombatSandboxRemoteDeckReference
                {
                    UserId = source.RemoteDeck.UserId,
                    DeckId = source.RemoteDeck.DeckId,
                    Revision = source.RemoteDeck.Revision,
                },
            ProvenanceLabel = source?.ProvenanceLabel ?? string.Empty,
            Notes = source?.Notes ?? string.Empty,
        };
    }

    private static CombatSandboxTeamMemberDefinition CloneMember(CombatSandboxTeamMemberDefinition source)
    {
        return new CombatSandboxTeamMemberDefinition
        {
            MemberId = source.MemberId,
            SourceKind = source.SourceKind,
            HeroId = source.HeroId,
            DisplayName = source.DisplayName,
            ArchetypeId = source.ArchetypeId,
            CharacterId = source.CharacterId,
            Anchor = source.Anchor,
            RoleInstructionId = source.RoleInstructionId,
            BuildOverride = CloneBuildOverride(source.BuildOverride),
            Notes = source.Notes,
        };
    }

    private static CombatSandboxBuildOverrideData CloneBuildOverride(CombatSandboxBuildOverrideData source)
    {
        return source == null
            ? new CombatSandboxBuildOverrideData()
            : new CombatSandboxBuildOverrideData
            {
                OverrideId = source.OverrideId,
                DisplayName = source.DisplayName,
                Tags = source.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                EquippedItems = source.EquippedItems?.Select(item => new CombatSandboxItemOverrideData
                {
                    ItemId = item.ItemId,
                    AffixIds = item.AffixIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                }).ToList() ?? new List<CombatSandboxItemOverrideData>(),
                PassiveBoardId = source.PassiveBoardId,
                PassiveNodeIds = source.PassiveNodeIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                TemporaryAugmentIds = source.TemporaryAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                PermanentAugmentIds = source.PermanentAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                FlexActiveSkillId = source.FlexActiveSkillId,
                FlexPassiveSkillId = source.FlexPassiveSkillId,
                PositiveTraitId = source.PositiveTraitId,
                NegativeTraitId = source.NegativeTraitId,
                RoleInstructionIdOverride = source.RoleInstructionIdOverride,
                RetrainCount = source.RetrainCount,
                Notes = source.Notes,
            };
    }

    private static string ResolveTeamId(CombatSandboxTeamDefinition team, string fallback)
    {
        return !string.IsNullOrWhiteSpace(team.TeamId) ? team.TeamId : $"sandbox.{fallback}";
    }

    private static string ResolveTeamDisplayName(CombatSandboxTeamDefinition team, string fallback)
    {
        return !string.IsNullOrWhiteSpace(team.DisplayName) ? team.DisplayName : $"Sandbox {fallback}";
    }

    private static string ResolveProvenanceLabel(CombatSandboxTeamDefinition team)
    {
        return !string.IsNullOrWhiteSpace(team.ProvenanceLabel)
            ? team.ProvenanceLabel
            : ResolveTeamId(team, "team");
    }
}
