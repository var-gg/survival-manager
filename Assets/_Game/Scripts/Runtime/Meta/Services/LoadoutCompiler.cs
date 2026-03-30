using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed class LoadoutCompiler
{
    public const string CurrentCompileVersion = "build-compile-audit.v1";

    public BattleLoadoutSnapshot Compile(
        IReadOnlyList<HeroRecord> heroes,
        IReadOnlyDictionary<string, HeroLoadoutState> heroLoadouts,
        IReadOnlyDictionary<string, HeroProgressionState> heroProgressions,
        IReadOnlyDictionary<string, ItemInstanceState> itemInstances,
        IReadOnlyDictionary<string, SkillInstanceState> skillInstances,
        IReadOnlyDictionary<string, PassiveBoardSelectionState> passiveSelections,
        PermanentAugmentLoadoutState permanentAugmentLoadout,
        SquadBlueprintState blueprint,
        RunOverlayState overlay,
        CombatContentSnapshot content)
    {
        var heroesById = heroes.ToDictionary(hero => hero.Id, StringComparer.Ordinal);
        var compiled = new List<BattleUnitLoadout>();
        foreach (var assignment in blueprint.DeploymentAssignments.OrderBy(pair => pair.Key))
        {
            if (!heroesById.TryGetValue(assignment.Value, out var hero))
            {
                continue;
            }

            if (!content.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype))
            {
                continue;
            }

            heroLoadouts.TryGetValue(hero.Id, out var loadout);
            heroProgressions.TryGetValue(hero.Id, out var progression);
            passiveSelections.TryGetValue(hero.Id, out var passiveSelection);

            var packages = new List<CombatModifierPackage>();
            var compileTags = new HashSet<string>(StringComparer.Ordinal)
            {
                $"race:{hero.RaceId}",
                $"class:{hero.ClassId}",
                hero.RaceId,
                hero.ClassId,
            };

            AddPackage(content.TraitPackages, hero.PositiveTraitId, packages);
            AddPackage(content.TraitPackages, hero.NegativeTraitId, packages);

            if (loadout != null)
            {
                foreach (var itemInstanceId in loadout.EquippedItemInstanceIds)
                {
                    if (!itemInstances.TryGetValue(itemInstanceId, out var itemInstance))
                    {
                        continue;
                    }

                    AddPackage(content.ItemPackages, itemInstance.ItemBaseId, packages);
                    compileTags.Add($"item:{itemInstance.ItemBaseId}");
                    foreach (var affixId in itemInstance.AffixIds)
                    {
                        AddPackage(content.AffixPackages, affixId, packages);
                        compileTags.Add($"affix:{affixId}");
                    }
                }
            }

            foreach (var augmentId in overlay.TemporaryAugmentIds.Concat(permanentAugmentLoadout.EquippedAugmentIds).Distinct(StringComparer.Ordinal))
            {
                AddPackage(content.AugmentPackages, augmentId, packages);
                if (content.AugmentCatalog.TryGetValue(augmentId, out var augment))
                {
                    foreach (var tag in augment.Tags)
                    {
                        compileTags.Add(tag);
                    }
                }
            }

            if (passiveSelection != null)
            {
                foreach (var nodeId in passiveSelection.SelectedNodeIds)
                {
                    if (!content.PassiveNodes.TryGetValue(nodeId, out var node))
                    {
                        continue;
                    }

                    packages.Add(node.Package);
                    foreach (var tag in node.CompileTags)
                    {
                        compileTags.Add(tag);
                    }
                }
            }

            if (progression != null)
            {
                foreach (var unlockedSkillId in progression.UnlockedSkillIds)
                {
                    compileTags.Add($"skill-unlock:{unlockedSkillId}");
                }
            }

            var skills = ResolveSkills(archetype, loadout, skillInstances, content);
            foreach (var skill in skills)
            {
                foreach (var tag in skill.CompileTags ?? Array.Empty<string>())
                {
                    compileTags.Add(tag);
                }
            }

            var teamTactic = ResolveTeamTactic(blueprint, content);
            var roleInstruction = ResolveRoleInstruction(assignment.Key, hero, blueprint, content);
            compileTags.Add(roleInstruction.RoleTag);

            compiled.Add(new BattleUnitLoadout(
                hero.Id,
                hero.Name,
                hero.RaceId,
                hero.ClassId,
                assignment.Key,
                new Dictionary<SM.Core.Stats.StatKey, float>(archetype.BaseStats),
                new[] { new UnitRuleChain($"rules:{hero.Id}", archetype.Tactics.ToList()) },
                skills,
                teamTactic,
                roleInstruction,
                "opening:standard",
                packages,
                null,
                compileTags.ToList()));
        }

        var teamTags = compiled
            .SelectMany(unit => unit.CompileTags ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();
        var teamPackages = SynergyLoadoutService.BuildTeamPackages(compiled, content);
        var finalized = compiled
            .Select(unit => new BattleUnitLoadout(
                unit.Id,
                unit.Name,
                unit.RaceId,
                unit.ClassId,
                unit.PreferredAnchor,
                unit.BaseStats,
                unit.RuleChains,
                unit.Skills,
                unit.TeamTactic,
                unit.RoleInstruction,
                unit.OpeningIntent,
                unit.Packages,
                teamPackages,
                unit.CompileTags))
            .ToList();
        var compileHash = ComputeCompileHash(finalized, teamPackages, blueprint, overlay);

        return new BattleLoadoutSnapshot(
            $"snapshot:{blueprint.BlueprintId}:{overlay.CurrentNodeIndex}",
            CurrentCompileVersion,
            compileHash,
            ResolveTeamTactic(blueprint, content),
            finalized,
            blueprint.DeploymentAssignments
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .ToList(),
            teamTags);
    }

    private static void AddPackage(
        IReadOnlyDictionary<string, CombatModifierPackage> source,
        string id,
        ICollection<CombatModifierPackage> destination)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (source.TryGetValue(id, out var package))
        {
            destination.Add(package);
        }
    }

    private static IReadOnlyList<BattleSkillSpec> ResolveSkills(
        CombatArchetypeTemplate archetype,
        HeroLoadoutState? loadout,
        IReadOnlyDictionary<string, SkillInstanceState> skillInstances,
        CombatContentSnapshot content)
    {
        if (loadout == null || loadout.EquippedSkillInstanceIds.Count == 0)
        {
            return archetype.Skills.ToList();
        }

        var equipped = new List<BattleSkillSpec>();
        foreach (var instanceId in loadout.EquippedSkillInstanceIds)
        {
            if (!skillInstances.TryGetValue(instanceId, out var instance))
            {
                continue;
            }

            if (!content.SkillCatalog.TryGetValue(instance.SkillId, out var skill))
            {
                continue;
            }

            equipped.Add(skill with { SlotKind = instance.SlotKind, CompileTags = instance.CompileTags });
        }

        return equipped.Count > 0 ? equipped : archetype.Skills.ToList();
    }

    private static TeamTacticProfile ResolveTeamTactic(SquadBlueprintState blueprint, CombatContentSnapshot content)
    {
        if (!string.IsNullOrWhiteSpace(blueprint.TeamTacticId)
            && content.TeamTactics.TryGetValue(blueprint.TeamTacticId, out var template))
        {
            return template.Profile;
        }

        return new TeamTacticProfile(
            $"posture:{blueprint.TeamPosture}",
            blueprint.TeamPosture.ToString(),
            blueprint.TeamPosture);
    }

    private static SlotRoleInstruction ResolveRoleInstruction(
        DeploymentAnchorId anchor,
        HeroRecord hero,
        SquadBlueprintState blueprint,
        CombatContentSnapshot content)
    {
        if (blueprint.HeroRoleIds.TryGetValue(hero.Id, out var roleId)
            && content.RoleInstructions.TryGetValue(roleId, out var role))
        {
            return role.Instruction;
        }

        var fallbackRoleTag = hero.ClassId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
        return new SlotRoleInstruction(anchor, fallbackRoleTag);
    }

    private static string ComputeCompileHash(
        IReadOnlyList<BattleUnitLoadout> units,
        IReadOnlyList<CombatModifierPackage> teamPackages,
        SquadBlueprintState blueprint,
        RunOverlayState overlay)
    {
        var sb = new StringBuilder();
        sb.Append(blueprint.BlueprintId).Append('|')
            .Append(blueprint.TeamPosture).Append('|')
            .Append(overlay.CurrentNodeIndex).Append('|');

        foreach (var unit in units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            sb.Append(unit.Id).Append(':')
                .Append(unit.PreferredAnchor).Append(':')
                .Append(string.Join(",", unit.CompileTags ?? Array.Empty<string>())).Append('|');
        }

        foreach (var package in teamPackages.OrderBy(package => package.SourceId, StringComparer.Ordinal))
        {
            sb.Append(package.SourceId).Append(':').Append(package.Source).Append('|');
        }

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        var hash = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            hash.Append(b.ToString("x2"));
        }

        return hash.ToString();
    }
}
