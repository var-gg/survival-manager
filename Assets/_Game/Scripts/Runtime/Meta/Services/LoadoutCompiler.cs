using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed class LoadoutCompiler
{
    public const string CurrentCompileVersion = "build-compile-audit.v1";

    private sealed class CompiledArtifacts
    {
        public List<CombatModifierPackage> NumericPackages { get; } = new();
        public List<CombatRuleModifierPackage> RulePackages { get; } = new();
        public HashSet<string> CompileTags { get; } = new(StringComparer.Ordinal);
        public List<CompileProvenanceEntry> Provenance { get; } = new();
    }

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
        var compileProvenance = new List<CompileProvenanceEntry>();
        var teamTactic = ResolveTeamTactic(blueprint, content);

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

            var artifacts = new CompiledArtifacts();
            artifacts.CompileTags.Add($"race:{hero.RaceId}");
            artifacts.CompileTags.Add($"class:{hero.ClassId}");
            artifacts.CompileTags.Add(hero.RaceId);
            artifacts.CompileTags.Add(hero.ClassId);

            AddNumericPackage(content.TraitPackages, hero.PositiveTraitId, artifacts, hero.Id, "trait");
            AddNumericPackage(content.TraitPackages, hero.NegativeTraitId, artifacts, hero.Id, "trait");

            if (loadout != null)
            {
                foreach (var itemInstanceId in loadout.EquippedItemInstanceIds)
                {
                    if (!itemInstances.TryGetValue(itemInstanceId, out var itemInstance))
                    {
                        continue;
                    }

                    AddNumericPackage(content.ItemPackages, itemInstance.ItemBaseId, artifacts, hero.Id, "item");
                    artifacts.CompileTags.Add($"item:{itemInstance.ItemBaseId}");

                    foreach (var affixId in itemInstance.AffixIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                    {
                        AddNumericPackage(content.AffixPackages, affixId, artifacts, hero.Id, "affix");
                        artifacts.CompileTags.Add($"affix:{affixId}");
                    }
                }
            }

            foreach (var augmentId in overlay.TemporaryAugmentIds.Concat(permanentAugmentLoadout.EquippedAugmentIds).Distinct(StringComparer.Ordinal))
            {
                AddNumericPackage(content.AugmentPackages, augmentId, artifacts, hero.Id, "augment");
                if (!content.AugmentCatalog.TryGetValue(augmentId, out var augment))
                {
                    continue;
                }

                AddRulePackage(augment.RulePackage, artifacts, hero.Id, "augment_rule");
                foreach (var tag in augment.Tags)
                {
                    artifacts.CompileTags.Add(tag);
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

                    artifacts.NumericPackages.Add(node.Package);
                    artifacts.Provenance.Add(new CompileProvenanceEntry(hero.Id, node.Package.Source, node.Package.SourceId, "passive_numeric", node.CompileTags));
                    AddRulePackage(node.RulePackage, artifacts, hero.Id, "passive_rule");
                    foreach (var tag in node.CompileTags)
                    {
                        artifacts.CompileTags.Add(tag);
                    }
                }
            }

            if (progression != null)
            {
                foreach (var unlockedSkillId in progression.UnlockedSkillIds)
                {
                    artifacts.CompileTags.Add($"skill-unlock:{unlockedSkillId}");
                }
            }

            var skills = ResolveSkills(archetype, loadout, itemInstances, skillInstances, content);
            foreach (var skill in skills)
            {
                foreach (var tag in skill.CompileTags ?? Array.Empty<string>())
                {
                    artifacts.CompileTags.Add(tag);
                }

                if (skill.RuleModifierTags is { Count: > 0 })
                {
                    artifacts.RulePackages.Add(new CombatRuleModifierPackage(
                        $"skill:{skill.Id}",
                        ModifierSource.Skill,
                        skill.RuleModifierTags.Select(tag => new RuleModifier(RuleModifierKind.BehaviorTag, tag)).ToList()));
                    artifacts.Provenance.Add(new CompileProvenanceEntry(hero.Id, ModifierSource.Skill, skill.Id, "skill_rule", skill.RuleModifierTags.ToList()));
                }
            }

            var roleInstruction = ResolveRoleInstruction(assignment.Key, hero, blueprint, content);
            artifacts.CompileTags.Add(roleInstruction.RoleTag);

            compiled.Add(new BattleUnitLoadout(
                hero.Id,
                hero.Name,
                hero.RaceId,
                hero.ClassId,
                assignment.Key,
                new Dictionary<StatKey, float>(archetype.BaseStats),
                new[] { new UnitRuleChain($"rules:{hero.Id}", archetype.Tactics.ToList()) },
                skills,
                teamTactic,
                roleInstruction,
                "opening:standard",
                artifacts.NumericPackages.ToList(),
                null,
                artifacts.CompileTags.OrderBy(tag => tag, StringComparer.Ordinal).ToList(),
                roleInstruction.RoleTag,
                archetype.PreferredDistance,
                archetype.ProtectRadius,
                archetype.Mana,
                artifacts.RulePackages.ToList(),
                null));

            compileProvenance.AddRange(artifacts.Provenance);
        }

        var teamTags = compiled
            .SelectMany(unit => unit.CompileTags ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();
        var teamPackages = SynergyLoadoutService.BuildTeamPackages(compiled, content);
        foreach (var package in teamPackages)
        {
            compileProvenance.Add(new CompileProvenanceEntry("team", package.Source, package.SourceId, "team_numeric", Array.Empty<string>()));
        }

        var finalized = compiled
            .Select(unit => unit with
            {
                TeamPackages = teamPackages,
                TeamRulePackages = Array.Empty<CombatRuleModifierPackage>()
            })
            .ToList();
        var compileHash = ComputeCompileHash(finalized, teamPackages, blueprint, overlay);

        return new BattleLoadoutSnapshot(
            $"snapshot:{blueprint.BlueprintId}:{overlay.CurrentNodeIndex}",
            CurrentCompileVersion,
            compileHash,
            teamTactic,
            finalized,
            blueprint.DeploymentAssignments
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .ToList(),
            teamTags,
            compileProvenance);
    }

    private static void AddNumericPackage(
        IReadOnlyDictionary<string, CombatModifierPackage> source,
        string id,
        CompiledArtifacts artifacts,
        string subjectId,
        string artifactKind)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!source.TryGetValue(id, out var package))
        {
            return;
        }

        artifacts.NumericPackages.Add(package);
        artifacts.Provenance.Add(new CompileProvenanceEntry(subjectId, package.Source, package.SourceId, artifactKind, Array.Empty<string>()));
    }

    private static void AddRulePackage(
        CombatRuleModifierPackage? package,
        CompiledArtifacts artifacts,
        string subjectId,
        string artifactKind)
    {
        if (package == null)
        {
            return;
        }

        artifacts.RulePackages.Add(package);
        artifacts.Provenance.Add(new CompileProvenanceEntry(
            subjectId,
            package.Source,
            package.SourceId,
            artifactKind,
            package.Modifiers.Select(modifier => modifier.Value).ToList()));
    }

    private static IReadOnlyList<BattleSkillSpec> ResolveSkills(
        CombatArchetypeTemplate archetype,
        HeroLoadoutState? loadout,
        IReadOnlyDictionary<string, ItemInstanceState> itemInstances,
        IReadOnlyDictionary<string, SkillInstanceState> skillInstances,
        CombatContentSnapshot content)
    {
        var equipped = new List<BattleSkillSpec>();
        var hasEquippedLoadout = false;
        if (loadout != null)
        {
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

                equipped.Add(skill with
                {
                    SlotKind = CompiledSkillSlots.Normalize(instance.SlotKind, skill.SlotKind),
                    CompileTags = instance.CompileTags
                });
                hasEquippedLoadout = true;
            }

            foreach (var itemInstanceId in loadout.EquippedItemInstanceIds)
            {
                if (!itemInstances.TryGetValue(itemInstanceId, out var itemInstance))
                {
                    continue;
                }

                if (content.ItemGrantedSkills == null || !content.ItemGrantedSkills.TryGetValue(itemInstance.ItemBaseId, out var grantedSkills))
                {
                    continue;
                }

                equipped.AddRange(grantedSkills);
            }
        }

        if (!hasEquippedLoadout)
        {
            equipped.InsertRange(0, archetype.Skills);
        }

        return equipped
            .Select(skill => skill with { SlotKind = CompiledSkillSlots.Normalize(skill.SlotKind) })
            .GroupBy(skill => skill.SlotKind, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(GetCompiledSkillSlotOrder)
            .ToList();
    }

    private static int GetCompiledSkillSlotOrder(BattleSkillSpec skill)
    {
        for (var i = 0; i < CompiledSkillSlots.Ordered.Count; i++)
        {
            if (string.Equals(CompiledSkillSlots.Ordered[i], skill.SlotKind, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
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
            .Append(blueprint.TeamTacticId).Append('|')
            .Append(overlay.CurrentNodeIndex).Append('|')
            .Append(overlay.CompileVersion).Append('|');

        foreach (var unit in units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            sb.Append(unit.Id).Append(':')
                .Append(unit.PreferredAnchor).Append(':')
                .Append(unit.RoleTag).Append(':')
                .Append(unit.PreferredDistance.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.ProtectRadius.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveMana.Max.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveMana.GainOnAttack.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveMana.GainOnHit.ToString("0.###", CultureInfo.InvariantCulture)).Append('|');

            foreach (var stat in unit.BaseStats.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
            {
                sb.Append(stat.Key.Value).Append('=').Append(stat.Value.ToString("0.###", CultureInfo.InvariantCulture)).Append(';');
            }

            sb.Append('|').Append(string.Join(",", unit.CompileTags ?? Array.Empty<string>())).Append('|');

            foreach (var skill in unit.Skills.OrderBy(skill => skill.Id, StringComparer.Ordinal))
            {
                sb.Append(skill.Id).Append(':')
                    .Append(skill.SlotKind).Append(':')
                    .Append(skill.DamageType).Append(':')
                    .Append(skill.Delivery).Append(':')
                    .Append(skill.TargetRule).Append(':')
                    .Append(skill.Range.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.Power.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.PowerFlat.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.PhysCoeff.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.MagCoeff.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.HealCoeff.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.HealthCoeff.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.CanCrit ? "1" : "0").Append(':')
                    .Append(skill.ManaCost.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.BaseCooldownSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.CastWindupSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(string.Join(",", skill.CompileTags ?? Array.Empty<string>())).Append(':')
                    .Append(string.Join(",", skill.RuleModifierTags ?? Array.Empty<string>())).Append(':')
                    .Append(string.Join(",", skill.SupportAllowedTags ?? Array.Empty<string>())).Append(':')
                    .Append(string.Join(",", skill.RequiredWeaponTags ?? Array.Empty<string>())).Append(':')
                    .Append(string.Join(",", skill.RequiredClassTags ?? Array.Empty<string>()))
                    .Append('|');
            }

            foreach (var package in unit.NumericPackages.OrderBy(package => package.SourceId, StringComparer.Ordinal))
            {
                sb.Append("num:").Append(package.Source).Append(':').Append(package.SourceId).Append('|');
            }

            foreach (var package in (unit.RulePackages ?? Array.Empty<CombatRuleModifierPackage>()).OrderBy(package => package.SourceId, StringComparer.Ordinal))
            {
                sb.Append("rule:").Append(package.Source).Append(':').Append(package.SourceId).Append(':')
                    .Append(string.Join(",", package.Modifiers.Select(modifier => $"{modifier.Kind}:{modifier.Value}:{modifier.Magnitude.ToString("0.###", CultureInfo.InvariantCulture)}")))
                    .Append('|');
            }
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
