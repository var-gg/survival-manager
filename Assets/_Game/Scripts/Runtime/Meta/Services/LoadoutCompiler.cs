using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed class LoadoutCompiler
{
    public const string CurrentCompileVersion = "loop-d-telemetry-pruning-readability-balance.v1";

    private sealed class CompiledArtifacts
    {
        public List<CombatModifierPackage> NumericPackages { get; } = new();
        public List<CombatRuleModifierPackage> RulePackages { get; } = new();
        public HashSet<string> CompileTags { get; } = new(StringComparer.Ordinal);
        public List<CompileProvenanceEntry> Provenance { get; } = new();
    }

    private sealed record ResolvedSkillSelection(
        BattleSkillSpec Skill,
        string SourceKind,
        string SourceId,
        string RawSlotKind);

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
        compileProvenance.Add(new CompileProvenanceEntry(
            "team",
            ModifierSource.Other,
            teamTactic.Id,
            "team_tactic",
            BuildTeamTacticDetails(teamTactic)));

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
            artifacts.Provenance.Add(new CompileProvenanceEntry(
                hero.Id,
                ModifierSource.Other,
                archetype.Id,
                "archetype_base",
                BuildBaseStatDetails(archetype.BaseStats)));

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

            foreach (var augmentId in overlay.TemporaryAugmentIds.Distinct(StringComparer.Ordinal))
            {
                AddNumericPackage(content.AugmentPackages, augmentId, artifacts, hero.Id, "augment_temporary");
                if (!content.AugmentCatalog.TryGetValue(augmentId, out var augment))
                {
                    continue;
                }

                AddRulePackage(augment.RulePackage, artifacts, hero.Id, "augment_temporary_rule");
                foreach (var tag in augment.Tags)
                {
                    artifacts.CompileTags.Add(tag);
                }
            }

            foreach (var augmentId in permanentAugmentLoadout.EquippedAugmentIds.Distinct(StringComparer.Ordinal))
            {
                AddNumericPackage(content.AugmentPackages, augmentId, artifacts, hero.Id, "augment_permanent");
                if (!content.AugmentCatalog.TryGetValue(augmentId, out var augment))
                {
                    continue;
                }

                AddRulePackage(augment.RulePackage, artifacts, hero.Id, "augment_permanent_rule");
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

            var resolvedSkills = ResolveSkills(hero, archetype, loadout, itemInstances, skillInstances, content);
            foreach (var selection in resolvedSkills)
            {
                var skill = selection.Skill;
                foreach (var tag in skill.CompileTags ?? Array.Empty<string>())
                {
                    artifacts.CompileTags.Add(tag);
                }

                artifacts.Provenance.Add(new CompileProvenanceEntry(
                    hero.Id,
                    ModifierSource.Skill,
                    skill.Id,
                    "skill_slot",
                    new[]
                    {
                        $"slot:{skill.SlotKind}",
                        $"source:{selection.SourceKind}",
                        $"sourceId:{selection.SourceId}",
                        $"rawSlot:{selection.RawSlotKind}",
                    }));

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
            artifacts.Provenance.Add(new CompileProvenanceEntry(
                hero.Id,
                ModifierSource.Other,
                roleInstruction.RoleTag,
                "role_instruction",
                BuildRoleInstructionDetails(roleInstruction)));

            var roleVariant = ResolveRoleVariant(archetype, roleInstruction);
            artifacts.CompileTags.Add($"role_variant:{roleVariant}");

            compiled.Add(new BattleUnitLoadout(
                hero.Id,
                hero.Name,
                hero.RaceId,
                hero.ClassId,
                assignment.Key,
                new Dictionary<StatKey, float>(archetype.BaseStats),
                new[] { new UnitRuleChain($"rules:{hero.Id}", archetype.Tactics.ToList()) },
                resolvedSkills.Select(selection => selection.Skill).ToList(),
                teamTactic,
                roleInstruction,
                "opening:standard",
                artifacts.NumericPackages.ToList(),
                null,
                artifacts.CompileTags.OrderBy(tag => tag, StringComparer.Ordinal).ToList(),
                roleInstruction.RoleTag,
                roleVariant,
                archetype.Footprint,
                archetype.Behavior,
                archetype.Mobility,
                archetype.PreferredDistance,
                archetype.ProtectRadius,
                archetype.Mana,
                artifacts.RulePackages.ToList(),
                null,
                archetype.BasicAttack,
                ResolveLoopASkill(resolvedSkills, ActionSlotKind.SignatureActive, archetype.SignatureActive),
                ResolveLoopASkill(resolvedSkills, ActionSlotKind.FlexActive, archetype.FlexActive),
                ResolveLoopAPassive(resolvedSkills, ActionSlotKind.SignaturePassive, archetype.SignaturePassive),
                ResolveLoopAPassive(resolvedSkills, ActionSlotKind.FlexPassive, archetype.FlexPassive),
                archetype.MobilityReaction,
                archetype.Energy,
                archetype.EntityKind,
                archetype.Ownership,
                archetype.SummonProfile,
                archetype.Governance));

            compileProvenance.AddRange(artifacts.Provenance);
        }

        var teamTags = compiled
            .SelectMany(unit => unit.CompileTags ?? Array.Empty<string>())
            .Append($"team_posture:{teamTactic.Posture}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();
        var teamPackages = SynergyLoadoutService.BuildTeamPackages(compiled, content);
        foreach (var package in teamPackages)
        {
            compileProvenance.Add(new CompileProvenanceEntry(
                "team",
                package.Source,
                package.SourceId,
                "team_numeric",
                BuildModifierDetails(package.Modifiers)));
        }

        var finalized = compiled
            .Select(unit => unit with
            {
                TeamPackages = teamPackages,
                TeamRulePackages = Array.Empty<CombatRuleModifierPackage>()
            })
            .ToList();
        var counterCoverage = CounterCoverageAggregationService.AggregateFromLoadouts(finalized);
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
            compileProvenance,
            counterCoverage,
            content.FirstPlayableSlice?.UnitBlueprintIds);
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
        artifacts.Provenance.Add(new CompileProvenanceEntry(subjectId, package.Source, package.SourceId, artifactKind, BuildModifierDetails(package.Modifiers)));
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

    private static IReadOnlyList<ResolvedSkillSelection> ResolveSkills(
        HeroRecord hero,
        CombatArchetypeTemplate archetype,
        HeroLoadoutState? loadout,
        IReadOnlyDictionary<string, ItemInstanceState> itemInstances,
        IReadOnlyDictionary<string, SkillInstanceState> skillInstances,
        CombatContentSnapshot content)
    {
        var equipped = new List<ResolvedSkillSelection>();
        AddHeroFlexSelection(equipped, hero.FlexActiveId, ActionSlotKind.FlexActive, "hero_flex_active", content);
        AddHeroFlexSelection(equipped, hero.FlexPassiveId, ActionSlotKind.FlexPassive, "hero_flex_passive", content);
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

                equipped.Add(new ResolvedSkillSelection(
                    skill with
                    {
                        SlotKind = instance.ResolvedSlotKind is { } resolvedSlotKind
                            ? CompiledSkillSlots.FromActionSlotKind(resolvedSlotKind)
                            : CompiledSkillSlots.Normalize(instance.SlotKind, skill.SlotKind),
                        CompileTags = instance.CompileTags
                    },
                    "loadout_skill",
                    instance.SkillInstanceId,
                    instance.SlotKind));
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

                equipped.AddRange(grantedSkills.Select(skill => new ResolvedSkillSelection(
                    skill with { SlotKind = CompiledSkillSlots.Normalize(skill.SlotKind) },
                    "item_granted_skill",
                    itemInstance.ItemBaseId,
                    skill.SlotKind)));
            }
        }

        if (!hasEquippedLoadout)
        {
            equipped.InsertRange(0, archetype.Skills.Select(skill => new ResolvedSkillSelection(
                skill with { SlotKind = CompiledSkillSlots.Normalize(skill.SlotKind) },
                "archetype_skill",
                archetype.Id,
                skill.SlotKind)));
        }

        var resolved = equipped
            .Select(selection => selection with { Skill = selection.Skill with { SlotKind = CompiledSkillSlots.Normalize(selection.Skill.SlotKind) } })
            .GroupBy(selection => selection.Skill.SlotKind, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(GetSkillSourcePriority)
                .ThenBy(selection => selection.Skill.Id, StringComparer.Ordinal)
                .ThenBy(selection => selection.SourceId, StringComparer.Ordinal)
                .First())
            .OrderBy(selection => GetCompiledSkillSlotOrder(selection.Skill))
            .ToList();

        resolved = EnsureCanonicalSkillContract(resolved, archetype, content);

        var missingSlots = CompiledSkillSlots.Ordered
            .Where(requiredSlot => resolved.All(selection => !string.Equals(selection.Skill.SlotKind, requiredSlot, StringComparison.Ordinal)))
            .ToList();
        if (missingSlots.Count > 0)
        {
            var availableSlots = string.Join(", ", archetype.Skills.Select(skill => $"{skill.Id}:{skill.SlotKind}"));
            throw new InvalidOperationException($"Compiled skill contract requires all four canonical slots. Missing: {string.Join(", ", missingSlots)} for archetype '{archetype.Id}'. Available: [{availableSlots}]");
        }

        return resolved;
    }

    private static BattleSkillSpec? ResolveLoopASkill(
        IReadOnlyList<ResolvedSkillSelection> resolved,
        ActionSlotKind slotKind,
        BattleSkillSpec? fallback)
    {
        return resolved
            .Select(selection => selection.Skill)
            .FirstOrDefault(skill => skill.EffectiveSlotKind == slotKind)
            ?? fallback;
    }

    private static BattlePassiveSpec? ResolveLoopAPassive(
        IReadOnlyList<ResolvedSkillSelection> resolved,
        ActionSlotKind slotKind,
        BattlePassiveSpec? fallback)
    {
        var selected = resolved
            .Select(selection => selection.Skill)
            .FirstOrDefault(skill => skill.EffectiveSlotKind == slotKind);
        if (selected == null)
        {
            return fallback;
        }

        return new BattlePassiveSpec(
            selected.Id,
            selected.Name,
            slotKind,
            ActivationModel.Passive,
            selected.EffectDescriptors,
            false,
            selected.EffectFamilyId);
    }

    private static List<ResolvedSkillSelection> EnsureCanonicalSkillContract(
        IReadOnlyList<ResolvedSkillSelection> current,
        CombatArchetypeTemplate archetype,
        CombatContentSnapshot content)
    {
        var occupiedSlots = new HashSet<string>(
            current.Select(selection => selection.Skill.SlotKind),
            StringComparer.Ordinal);
        var supplemented = current.ToList();

        foreach (var skillId in GetFallbackSkillIds(archetype))
        {
            if (!content.SkillCatalog.TryGetValue(skillId, out var skill))
            {
                continue;
            }

            var normalizedSlot = CompiledSkillSlots.Normalize(skill.SlotKind);
            if (!occupiedSlots.Add(normalizedSlot))
            {
                continue;
            }

            supplemented.Add(new ResolvedSkillSelection(
                skill with { SlotKind = normalizedSlot },
                "archetype_fallback_skill",
                archetype.Id,
                skill.SlotKind));
        }

        return supplemented
            .GroupBy(selection => selection.Skill.SlotKind, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(GetSkillSourcePriority)
                .ThenBy(selection => selection.Skill.Id, StringComparer.Ordinal)
                .ThenBy(selection => selection.SourceId, StringComparer.Ordinal)
                .First())
            .OrderBy(selection => GetCompiledSkillSlotOrder(selection.Skill))
            .ToList();
    }

    private static IEnumerable<string> GetFallbackSkillIds(CombatArchetypeTemplate archetype)
    {
        foreach (var skillId in archetype.Id switch
                 {
                     "warden" => new[] { "skill_power_strike", "skill_warden_utility" },
                     "guardian" => new[] { "skill_guardian_core", "skill_guardian_utility" },
                     "bulwark" => new[] { "skill_bulwark_core", "skill_bulwark_utility" },
                     "slayer" => new[] { "skill_slayer_core", "skill_slayer_utility" },
                     "raider" => new[] { "skill_raider_core", "skill_raider_utility" },
                     "reaver" => new[] { "skill_reaver_core", "skill_reaver_utility" },
                     "hunter" => new[] { "skill_precision_shot", "skill_hunter_utility" },
                     "scout" => new[] { "skill_scout_core", "skill_scout_utility" },
                     "marksman" => new[] { "skill_marksman_core", "skill_marksman_utility" },
                     "priest" => new[] { "skill_priest_core", "skill_minor_heal" },
                     "hexer" => new[] { "skill_hexer_core", "skill_hexer_utility" },
                     "shaman" => new[] { "skill_shaman_core", "skill_shaman_utility" },
                     _ => Array.Empty<string>(),
                 })
        {
            yield return skillId;
        }

        foreach (var skillId in archetype.ClassId switch
                 {
                     "vanguard" => new[] { "skill_vanguard_passive_1", "skill_vanguard_support_1" },
                     "duelist" => new[] { "skill_duelist_passive_1", "skill_duelist_support_1" },
                     "ranger" => new[] { "skill_ranger_passive_1", "skill_ranger_support_1" },
                     "mystic" => new[] { "skill_mystic_passive_1", "skill_mystic_support_1" },
                     _ => Array.Empty<string>(),
                 })
        {
            yield return skillId;
        }
    }

    private static int GetSkillSourcePriority(ResolvedSkillSelection selection)
    {
        return selection.SourceKind switch
        {
            "hero_flex_active" => 0,
            "hero_flex_passive" => 0,
            "loadout_skill" => 1,
            "item_granted_skill" => 2,
            _ => 3,
        };
    }

    private static void AddHeroFlexSelection(
        ICollection<ResolvedSkillSelection> selections,
        string skillId,
        ActionSlotKind slotKind,
        string sourceKind,
        CombatContentSnapshot content)
    {
        if (string.IsNullOrWhiteSpace(skillId) || !content.SkillCatalog.TryGetValue(skillId, out var skill))
        {
            return;
        }

        var normalizedSlot = CompiledSkillSlots.FromActionSlotKind(slotKind);
        selections.Add(new ResolvedSkillSelection(
            skill with
            {
                SlotKind = normalizedSlot,
                ResolvedSlotKind = slotKind,
            },
            sourceKind,
            skillId,
            normalizedSlot));
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

        var fallbackId = blueprint.TeamPosture switch
        {
            TeamPostureType.HoldLine => "team_tactic_hold_line",
            TeamPostureType.ProtectCarry => "team_tactic_protect_carry",
            TeamPostureType.CollapseWeakSide => "team_tactic_collapse_weak_side",
            TeamPostureType.AllInBackline => "team_tactic_all_in_backline",
            _ => "team_tactic_standard_advance",
        };
        if (content.TeamTactics.TryGetValue(fallbackId, out var fallbackTemplate))
        {
            return fallbackTemplate.Profile;
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
            return role.Instruction with { Anchor = anchor };
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

    private static RoleVariantTag ResolveRoleVariant(
        CombatArchetypeTemplate archetype,
        SlotRoleInstruction roleInstruction)
    {
        var isFrontRow = roleInstruction.Anchor.IsFrontRow();
        var hasHeal = archetype.Skills.Any(skill => skill.HealCoeff > 0f);
        var hasControl = archetype.Skills.Any(skill =>
            skill.Kind is SkillKind.Debuff or SkillKind.Utility);
        var hasSummon = archetype.Skills.Any(skill => skill.SummonProfile != null);
        var rangeDiscipline = archetype.Behavior?.RangeDiscipline ?? RangeDiscipline.HoldBand;

        return archetype.ClassId switch
        {
            "vanguard" when roleInstruction.ProtectCarryBias > 0.3f => RoleVariantTag.Peeler,
            "vanguard" => RoleVariantTag.Anchor,
            "duelist" when rangeDiscipline == RangeDiscipline.Collapse && isFrontRow => RoleVariantTag.Executioner,
            "duelist" => RoleVariantTag.Diver,
            "ranger" when rangeDiscipline is RangeDiscipline.KiteBackward or RangeDiscipline.SideStepHold => RoleVariantTag.Sniper,
            "ranger" => RoleVariantTag.Harrier,
            "mystic" when hasHeal => RoleVariantTag.Controller,
            "mystic" when hasControl || hasSummon => RoleVariantTag.Controller,
            "mystic" => RoleVariantTag.Battery,
            _ when isFrontRow => RoleVariantTag.Anchor,
            _ => RoleVariantTag.Sniper,
        };
    }

    private static string ComputeCompileHash(
        IReadOnlyList<BattleUnitLoadout> units,
        IReadOnlyList<CombatModifierPackage> teamPackages,
        SquadBlueprintState blueprint,
        RunOverlayState overlay)
    {
        var sb = new StringBuilder();
        var resolvedTeamTactic = units.FirstOrDefault()?.TeamTactic;
        sb.Append(blueprint.BlueprintId).Append('|')
            .Append(blueprint.TeamPosture).Append('|')
            .Append(blueprint.TeamTacticId).Append('|')
            .Append(overlay.CurrentNodeIndex).Append('|')
            .Append(overlay.CompileVersion).Append('|');

        if (resolvedTeamTactic != null)
        {
            sb.Append("teamTactic:")
                .Append(resolvedTeamTactic.Id).Append(':')
                .Append(resolvedTeamTactic.Posture).Append(':')
                .Append(resolvedTeamTactic.CombatPace.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.FocusModeBias.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.FrontSpacingBias.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.BackSpacingBias.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.ProtectCarryBias.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.TargetSwitchPenalty.ToString("0.###", CultureInfo.InvariantCulture)).Append('|');
        }

        foreach (var unit in units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            sb.Append(unit.Id).Append(':')
                .Append(unit.PreferredAnchor).Append(':')
                .Append(unit.RoleTag).Append(':')
                .Append(unit.RoleInstruction?.ProtectCarryBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.RoleInstruction?.BacklinePressureBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.RoleInstruction?.RetreatBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.NavigationRadius.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.SeparationRadius.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.CombatReach.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.PreferredRangeBand.ClampedMin.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.PreferredRangeBand.ClampedMax.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.EngagementSlotCount.ToString(CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.EngagementSlotRadius.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Footprint?.BodySizeCategory.ToString() ?? "none").Append(':')
                .Append(unit.Behavior?.ReevaluationInterval.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Behavior?.RangeHysteresis.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Behavior?.RetreatBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Behavior?.MaintainRangeBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Behavior?.DodgeChance.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Behavior?.BlockChance.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Behavior?.BlockMitigation.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Mobility?.Style.ToString() ?? "none").Append(':')
                .Append(unit.Mobility?.Purpose.ToString() ?? "none").Append(':')
                .Append(unit.Mobility?.Distance.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.Mobility?.Cooldown.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.PreferredDistance.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.ProtectRadius.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveMana.Max.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveMana.GainOnAttack.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveMana.GainOnHit.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveEnergy.Max.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EffectiveEnergy.Starting.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(unit.EntityKind).Append(':')
                .Append(unit.EffectiveBasicAttack.Id).Append(':')
                .Append(unit.EffectiveSignatureActive?.Id ?? "none").Append(':')
                .Append(unit.EffectiveFlexActive?.Id ?? "none").Append(':')
                .Append(unit.EffectiveSignaturePassive.Id).Append(':')
                .Append(unit.EffectiveFlexPassive.Id).Append(':')
                .Append(unit.EffectiveMobilityReaction?.Id ?? "none").Append('|');

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
                    .Append(string.Join(",", skill.SupportBlockedTags ?? Array.Empty<string>())).Append(':')
                    .Append(string.Join(",", skill.RequiredWeaponTags ?? Array.Empty<string>())).Append(':')
                    .Append(string.Join(",", skill.RequiredClassTags ?? Array.Empty<string>())).Append(':')
                    .Append(string.Join(",", (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
                        .Select(status => $"{status.StatusId}:{status.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)}:{status.Magnitude.ToString("0.###", CultureInfo.InvariantCulture)}:{status.MaxStacks}:{status.RefreshDurationOnReapply}"))).Append(':')
                    .Append(skill.CleanseProfileId ?? string.Empty)
                    .Append('|');
            }

            foreach (var package in unit.NumericPackages.OrderBy(package => package.SourceId, StringComparer.Ordinal))
            {
                AppendModifierPackage(sb, "num", package.Source, package.SourceId, package.Modifiers);
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
            AppendModifierPackage(sb, "team", package.Source, package.SourceId, package.Modifiers);
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

    private static IReadOnlyList<string> BuildBaseStatDetails(IReadOnlyDictionary<StatKey, float> baseStats)
    {
        return baseStats
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key.Value}:{pair.Value.ToString("0.###", CultureInfo.InvariantCulture)}")
            .ToList();
    }

    private static IReadOnlyList<string> BuildTeamTacticDetails(TeamTacticProfile profile)
    {
        return new[]
        {
            $"posture:{profile.Posture}",
            $"combatPace:{profile.CombatPace.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"focusModeBias:{profile.FocusModeBias.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"frontSpacingBias:{profile.FrontSpacingBias.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"backSpacingBias:{profile.BackSpacingBias.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"protectCarryBias:{profile.ProtectCarryBias.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"targetSwitchPenalty:{profile.TargetSwitchPenalty.ToString("0.###", CultureInfo.InvariantCulture)}",
        };
    }

    private static IReadOnlyList<string> BuildRoleInstructionDetails(SlotRoleInstruction instruction)
    {
        return new[]
        {
            $"anchor:{instruction.Anchor}",
            $"roleTag:{instruction.RoleTag}",
            $"protectCarryBias:{instruction.ProtectCarryBias.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"backlinePressureBias:{instruction.BacklinePressureBias.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"retreatBias:{instruction.RetreatBias.ToString("0.###", CultureInfo.InvariantCulture)}",
        };
    }

    private static IReadOnlyList<string> BuildModifierDetails(IReadOnlyList<StatModifier> modifiers)
    {
        return modifiers
            .OrderBy(modifier => modifier.Stat.Value, StringComparer.Ordinal)
            .ThenBy(modifier => modifier.Op)
            .ThenBy(modifier => modifier.Source)
            .ThenBy(modifier => modifier.SourceId, StringComparer.Ordinal)
            .Select(modifier => $"{modifier.Stat.Value}:{modifier.Op}:{modifier.Value.ToString("0.###", CultureInfo.InvariantCulture)}:{modifier.Source}:{modifier.SourceId}")
            .ToList();
    }

    private static void AppendModifierPackage(
        StringBuilder builder,
        string prefix,
        ModifierSource source,
        string sourceId,
        IReadOnlyList<StatModifier> modifiers)
    {
        builder.Append(prefix).Append(':').Append(source).Append(':').Append(sourceId).Append(':');
        foreach (var detail in BuildModifierDetails(modifiers))
        {
            builder.Append(detail).Append(',');
        }

        builder.Append('|');
    }
}
