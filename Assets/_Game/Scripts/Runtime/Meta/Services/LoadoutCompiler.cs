using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed class LoadoutCompiler
{
    // p3-skill-displacement.v1: 스킬 hash 직렬화에 DisplacementKind/Distance 추가(전 스킬 라인 변경).
    // affix-template.v1: affix의 CompileTags/RuleModifierTags 유닛 전파 + RequiredTags/ExcludedTags
    //   조건 게이트 신설(과거엔 조건 무시로 수치 무조건 적용) — affix 장착 라인의 hash 변경.
    // skill-triggered-effects.v1: 스킬 TriggeredEffects가 유닛 트리거 채널로 합류(패시브/서포트
    //   슬롯의 실전투 통로) — 해당 스킬 장착 유닛의 trig 라인 hash 변경.
    // support-modifier.v1: 서포트 젬 페어-변조(SupportModifier) 신설 — 젬 장착 유닛의
    //   매칭 액티브 수치/상태/cleanse와 owner 스탯이 컴파일 타임에 변환된다.
    // item-template.v1: 아이템 CompileTags/무기 family 태그 유닛 전파 + 젬 무기 게이트 활성화
    //   (RequiredWeaponTags 저작이 이제 실판정) — 아이템 장착 라인의 태그/hash 변경.
    // passive-granted-skill.v1: 패시브 노드 도달 보상 스킬(PoE식 notable) 신설 — 선택 노드의
    //   GrantedSkillId가 가리키는 스킬의 TriggeredEffects/CompileTags가 유닛에 합류하고,
    //   SupportModifier 보유 스킬은 서포트 젬 목록에 합류(4슬롯 계약 무접촉).
    public const string CurrentCompileVersion = "passive-granted-skill.v1";

    private sealed class CompiledArtifacts
    {
        public List<CombatModifierPackage> NumericPackages { get; } = new();
        public List<CombatRuleModifierPackage> RulePackages { get; } = new();
        public List<CombatTriggeredEffect> TriggeredEffects { get; } = new();
        public HashSet<string> CompileTags { get; } = new(StringComparer.Ordinal);
        public List<CompileProvenanceEntry> Provenance { get; } = new();
    }

    private sealed record ResolvedSkillSelection(
        BattleSkillSpec Skill,
        string SourceKind,
        string SourceId,
        string RawSlotKind);

    private sealed record ResolvedRoleInstructionSelection(
        string Id,
        SlotRoleInstruction Instruction);

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
        CombatContentSnapshot content,
        IReadOnlyList<CombatModifierPackage>? squadSupportPackages = null,
        WarWoundSpec? warWoundSpec = null,
        IReadOnlyCollection<string>? activeWoundHeroIds = null)
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
            if (archetype.ClassStatPackage != null)
            {
                artifacts.NumericPackages.Add(archetype.ClassStatPackage);
                artifacts.Provenance.Add(new CompileProvenanceEntry(
                    hero.Id,
                    archetype.ClassStatPackage.Source,
                    archetype.ClassStatPackage.SourceId,
                    "class_stat_cap",
                    BuildModifierDetails(archetype.ClassStatPackage.Modifiers)));
            }

            AddNumericPackage(content.TraitPackages, hero.PositiveTraitId, artifacts, hero.Id, "trait");
            AddNumericPackage(content.TraitPackages, hero.NegativeTraitId, artifacts, hero.Id, "trait");

            var deferredConditionalAffixes = new List<(AffixTemplate Template, float? Magnitude)>();
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
                    // 아이템 태그 전파 — affix 조건 게이트·서포트 젬 무기 게이트의 판정 재료.
                    if (content.ItemCatalog != null && content.ItemCatalog.TryGetValue(itemInstance.ItemBaseId, out var itemTemplate))
                    {
                        foreach (var tag in itemTemplate.CompileTags)
                        {
                            artifacts.CompileTags.Add(tag);
                        }

                        if (!string.IsNullOrWhiteSpace(itemTemplate.WeaponFamilyTag))
                        {
                            artifacts.CompileTags.Add(itemTemplate.WeaponFamilyTag);
                        }
                    }

                    foreach (var affixId in itemInstance.AffixIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                    {
                        artifacts.CompileTags.Add($"affix:{affixId}");
                        var affixTemplate = ResolveAffixTemplate(content, affixId);
                        var rolledMagnitude = AffixMagnitudePackageResolver.Find(
                            itemInstance.AffixMagnitudes,
                            affixId);
                        if (affixTemplate is { IsConditional: true })
                        {
                            // 조건부 affix는 전체 태그 조립이 끝난 뒤 일괄 평가한다(아래).
                            // 자기 CompileTags로 자기 조건을 충족시키는 순환을 여기서 차단한다.
                            deferredConditionalAffixes.Add((affixTemplate, rolledMagnitude));
                            continue;
                        }

                        AddAffixNumericPackage(
                            content.AffixPackages,
                            affixId,
                            rolledMagnitude,
                            artifacts,
                            hero.Id,
                            "affix");
                        ApplyAffixTemplate(affixTemplate, rolledMagnitude, artifacts, hero.Id);
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
                if (augment.TriggeredEffects != null)
                {
                    artifacts.TriggeredEffects.AddRange(augment.TriggeredEffects);
                }

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
                if (augment.TriggeredEffects != null)
                {
                    artifacts.TriggeredEffects.AddRange(augment.TriggeredEffects);
                }

                foreach (var tag in augment.Tags)
                {
                    artifacts.CompileTags.Add(tag);
                }
            }

            var grantedSupportGems = new List<BattleSkillSpec>();
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

                    ApplyPassiveGrantedSkill(node, artifacts, hero.Id, content, grantedSupportGems);
                }
            }

            if (progression != null)
            {
                foreach (var unlockedSkillId in progression.UnlockedSkillIds)
                {
                    artifacts.CompileTags.Add($"skill-unlock:{unlockedSkillId}");
                }
            }

            var resolvedSkills = ApplySupportModifiers(
                ResolveSkills(hero, archetype, loadout, itemInstances, skillInstances, content),
                artifacts,
                hero.Id,
                grantedSupportGems);
            if (warWoundSpec != null
                && activeWoundHeroIds != null
                && activeWoundHeroIds.Contains(hero.Id, StringComparer.Ordinal))
            {
                resolvedSkills = ApplyWarWoundModifier(
                    resolvedSkills,
                    artifacts,
                    hero.Id,
                    warWoundSpec,
                    content);
            }
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

                // 스킬 발동형 효과 → 유닛 TriggeredEffects 합류(증강과 동일 채널, CombatTriggerEngine 소비).
                // 패시브/서포트 슬롯 스킬이 실행 루프 밖에서도 실전투 효과를 내는 정식 통로.
                if (skill.TriggeredEffects is { Count: > 0 })
                {
                    artifacts.TriggeredEffects.AddRange(skill.TriggeredEffects);
                    artifacts.Provenance.Add(new CompileProvenanceEntry(
                        hero.Id,
                        ModifierSource.Skill,
                        skill.Id,
                        "skill_triggered_effect",
                        skill.TriggeredEffects.Select(effect => $"{effect.Trigger}:{effect.Op}:{effect.StatusId}").ToList()));
                }
            }

            var roleSelection = ResolveRoleInstruction(assignment.Key, hero, blueprint, content);
            var roleInstruction = roleSelection.Instruction;
            artifacts.CompileTags.Add(roleInstruction.RoleTag);
            artifacts.Provenance.Add(new CompileProvenanceEntry(
                hero.Id,
                ModifierSource.Other,
                roleSelection.Id,
                "role_instruction",
                BuildRoleInstructionDetails(roleInstruction)));

            var roleVariant = ResolveRoleVariant(archetype, roleInstruction, artifacts.RulePackages);
            artifacts.CompileTags.Add($"role_variant:{roleVariant}");
            var dominantHand = ResolveDominantHand(hero, archetype, content);
            artifacts.CompileTags.Add($"dominant_hand:{dominantHand}");
            // P1 유닛별 타겟 지시(세션 사용자 입력) — compile tag + 로드아웃 + hash(아래)로 흘러
            // replay/audit 무결성에 포함된다.
            var targetDirective = ResolveTargetDirective(hero, blueprint);
            if (targetDirective != PlayerTargetDirective.Default)
            {
                artifacts.CompileTags.Add($"target_directive:{PlayerTargetDirectiveRules.ToStableId(targetDirective)}");
            }

            // 조건부 affix 게이트: 비조건 소스 전체가 조립을 마친 태그 집합의 스냅샷 기준으로
            // 일괄 평가한다. 조건부끼리의 연쇄 발동은 의도적으로 없다(평가 순서 무관 → 결정성).
            if (deferredConditionalAffixes.Count > 0)
            {
                var conditionContext = new HashSet<string>(artifacts.CompileTags, StringComparer.Ordinal);
                foreach (var deferredAffix in deferredConditionalAffixes)
                {
                    var affixTemplate = deferredAffix.Template;
                    if (!IsConditionalAffixSatisfied(affixTemplate, conditionContext))
                    {
                        artifacts.Provenance.Add(new CompileProvenanceEntry(
                            hero.Id,
                            ModifierSource.Item,
                            affixTemplate.Id,
                            "affix_conditional_inactive",
                            BuildConditionalAffixDetails(affixTemplate)));
                        continue;
                    }

                    AddAffixNumericPackage(
                        content.AffixPackages,
                        affixTemplate.Id,
                        deferredAffix.Magnitude,
                        artifacts,
                        hero.Id,
                        "affix_conditional");
                    ApplyAffixTemplate(affixTemplate, deferredAffix.Magnitude, artifacts, hero.Id);
                }
            }

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
                archetype.Governance,
                archetype.Id,
                hero.CharacterId,
                roleSelection.Id,
                dominantHand,
                artifacts.TriggeredEffects.ToList(),
                targetDirective));

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
                // 정치 지원(ADR-0028 slice 2)은 외부에서 도출된 일반 numeric package다. compile 안에서
                // 접어 넣어야 CompileHash(아래 finalized 기준)가 이를 포함 → replay/audit 무결성 유지.
                // 없으면(평시 경로) Packages 무변 → 기존 hash/테스트 불변.
                Packages = squadSupportPackages is { Count: > 0 }
                    ? unit.NumericPackages.Concat(squadSupportPackages).ToList()
                    : unit.Packages,
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
            content.FirstPlayableSlice?.UnitBlueprintIds,
            CombatStatusRuleCompiler.Compile(content));
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

    private static void AddAffixNumericPackage(
        IReadOnlyDictionary<string, CombatModifierPackage> source,
        string id,
        float? rolledMagnitude,
        CompiledArtifacts artifacts,
        string subjectId,
        string artifactKind)
    {
        if (string.IsNullOrWhiteSpace(id) || !source.TryGetValue(id, out var sharedPackage))
        {
            return;
        }

        var package = AffixMagnitudePackageResolver.Resolve(sharedPackage, rolledMagnitude);
        artifacts.NumericPackages.Add(package);
        artifacts.Provenance.Add(new CompileProvenanceEntry(
            subjectId,
            package.Source,
            package.SourceId,
            artifactKind,
            BuildModifierDetails(package.Modifiers)));
    }

    /// <summary>
    /// 서포트 젬 페어-변조 — SupportModifier를 가진 스킬(젬)이 같은 유닛의 액티브(코어/유틸리티) 중
    /// SupportAllowedTags/BlockedTags 매칭을 통과한 스킬을 컴파일 타임에 변환한다.
    /// 젬 id 오름차순으로 순차 적용(결정성), 젬이 젬을 변조하지 않는다.
    /// 클래스/무기 게이트: RequiredClassTags·RequiredWeaponTags가 유닛 태그(클래스 raw 태그,
    /// 아이템 weapon family 태그)와 매칭돼야 발동한다.
    /// </summary>
    private static IReadOnlyList<ResolvedSkillSelection> ApplySupportModifiers(
        IReadOnlyList<ResolvedSkillSelection> resolved,
        CompiledArtifacts artifacts,
        string heroId,
        IReadOnlyList<BattleSkillSpec>? grantedGems = null)
    {
        // 젬 풀 = 장착 슬롯 젬 + 패시브 노드 부여 젬(슬롯 밖). 같은 스킬 중복은 1회만(슬롯 dedup과 동일 의미론),
        // id 오름차순 순차 적용으로 결정성 유지.
        var gems = resolved
            .Where(selection => selection.Skill.SupportModifier != null)
            .Select(selection => selection.Skill)
            .Concat(grantedGems ?? Array.Empty<BattleSkillSpec>())
            .GroupBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(skill => skill.Id, StringComparer.Ordinal)
            .ToList();
        if (gems.Count == 0)
        {
            return resolved;
        }

        var transformed = resolved.ToList();
        foreach (var gem in gems)
        {
            var modifier = gem.SupportModifier!;
            if (gem.RequiredClassTags is { Count: > 0 }
                && !gem.RequiredClassTags.Any(artifacts.CompileTags.Contains))
            {
                artifacts.Provenance.Add(new CompileProvenanceEntry(
                    heroId, ModifierSource.Skill, gem.Id, "support_modifier_class_gate", gem.RequiredClassTags.ToList()));
                continue;
            }

            if (gem.RequiredWeaponTags is { Count: > 0 }
                && !gem.RequiredWeaponTags.Any(artifacts.CompileTags.Contains))
            {
                artifacts.Provenance.Add(new CompileProvenanceEntry(
                    heroId, ModifierSource.Skill, gem.Id, "support_modifier_weapon_gate", gem.RequiredWeaponTags.ToList()));
                continue;
            }

            if (modifier.OwnerModifiers is { Count: > 0 })
            {
                var package = new CombatModifierPackage($"support:{gem.Id}", ModifierSource.Skill, modifier.OwnerModifiers.ToList());
                artifacts.NumericPackages.Add(package);
                artifacts.Provenance.Add(new CompileProvenanceEntry(
                    heroId, ModifierSource.Skill, gem.Id, "support_owner_numeric", BuildModifierDetails(package.Modifiers)));
            }

            for (var i = 0; i < transformed.Count; i++)
            {
                var selection = transformed[i];
                if (selection.Skill.SupportModifier != null || !IsSupportTarget(gem, selection.Skill))
                {
                    continue;
                }

                transformed[i] = selection with { Skill = BattleSkillEffectModifier.Apply(selection.Skill, modifier) };
                artifacts.Provenance.Add(new CompileProvenanceEntry(
                    heroId, ModifierSource.Skill, gem.Id, "support_modifier", new[] { $"target:{selection.Skill.Id}" }));
            }
        }

        return transformed;
    }

    /// <summary>
    /// PoE식 패시브 노드 도달 보상 — 노드의 GrantedSkillId가 가리키는 스킬을 슬롯 계약 밖
    /// 효과 캐리어로 합류시킨다. TriggeredEffects/CompileTags는 증강과 동일 채널,
    /// SupportModifier 보유 스킬은 서포트 젬 풀 합류(ApplySupportModifiers에서 게이트/변조 판정).
    /// 미존재 스킬 id는 조용히 건너뛴다(저작 오류는 catalog validator가 잡는다).
    /// </summary>
    private static void ApplyPassiveGrantedSkill(
        PassiveNodeTemplate node,
        CompiledArtifacts artifacts,
        string heroId,
        CombatContentSnapshot content,
        List<BattleSkillSpec> grantedSupportGems)
    {
        if (string.IsNullOrWhiteSpace(node.GrantedSkillId)
            || !content.SkillCatalog.TryGetValue(node.GrantedSkillId, out var skill))
        {
            return;
        }

        foreach (var tag in skill.CompileTags ?? Array.Empty<string>())
        {
            artifacts.CompileTags.Add(tag);
        }

        if (skill.TriggeredEffects is { Count: > 0 })
        {
            artifacts.TriggeredEffects.AddRange(skill.TriggeredEffects);
            artifacts.Provenance.Add(new CompileProvenanceEntry(
                heroId,
                ModifierSource.Skill,
                skill.Id,
                "passive_granted_skill",
                skill.TriggeredEffects.Select(effect => $"{effect.Trigger}:{effect.Op}:{effect.StatusId}").ToList()));
        }

        if (skill.SupportModifier != null)
        {
            grantedSupportGems.Add(skill);
            artifacts.Provenance.Add(new CompileProvenanceEntry(
                heroId, ModifierSource.Skill, skill.Id, "passive_granted_support", new[] { $"node:{node.Id}" }));
        }
    }

    private static bool IsSupportTarget(BattleSkillSpec gem, BattleSkillSpec candidate)
    {
        // 변조 대상은 액티브 슬롯(코어/유틸리티)만 — 패시브·서포트·젬은 제외.
        if (!string.Equals(candidate.SlotKind, CompiledSkillSlots.CoreActive, StringComparison.Ordinal)
            && !string.Equals(candidate.SlotKind, CompiledSkillSlots.UtilityActive, StringComparison.Ordinal))
        {
            return false;
        }

        var candidateTags = candidate.CompileTags ?? Array.Empty<string>();
        var allowed = gem.SupportAllowedTags ?? Array.Empty<string>();
        if (allowed.Count == 0 || !allowed.Any(tag => candidateTags.Contains(tag, StringComparer.Ordinal)))
        {
            return false;
        }

        var blocked = gem.SupportBlockedTags ?? Array.Empty<string>();
        return !blocked.Any(tag => candidateTags.Contains(tag, StringComparer.Ordinal));
    }

    private static IReadOnlyList<ResolvedSkillSelection> ApplyWarWoundModifier(
        IReadOnlyList<ResolvedSkillSelection> resolved,
        CompiledArtifacts artifacts,
        string heroId,
        WarWoundSpec spec,
        CombatContentSnapshot content)
    {
        var modifier = new BattleSupportModifierSpec(
            PowerMultiplier: spec.WoundAbilityScalar,
            StatusDurationMultiplier: spec.WoundAbilityScalar);
        var transformed = resolved.ToList();
        for (var index = 0; index < transformed.Count; index++)
        {
            var selection = transformed[index];
            if (!string.Equals(selection.Skill.SlotKind, CompiledSkillSlots.CoreActive, StringComparison.Ordinal)
                && !string.Equals(selection.Skill.SlotKind, CompiledSkillSlots.UtilityActive, StringComparison.Ordinal))
            {
                continue;
            }

            transformed[index] = selection with
            {
                Skill = BattleSkillEffectModifier.Apply(
                    selection.Skill,
                    modifier,
                    status => content.StatusFamilies != null
                              && content.StatusFamilies.TryGetValue(status.StatusId, out var family)
                              && family.Group == StatusGroupValue.Control,
                    coefficientMultiplier: spec.WoundAbilityScalar),
            };
            artifacts.Provenance.Add(new CompileProvenanceEntry(
                heroId,
                ModifierSource.Other,
                "war_wound",
                "run_wound_skill_effect",
                new[]
                {
                    $"target:{selection.Skill.Id}",
                    $"scalar:{spec.WoundAbilityScalar.ToString("R", CultureInfo.InvariantCulture)}",
                }));
        }

        return transformed;
    }

    private static AffixTemplate? ResolveAffixTemplate(CombatContentSnapshot content, string affixId)
    {
        return content.AffixCatalog != null && content.AffixCatalog.TryGetValue(affixId, out var template)
            ? template
            : null;
    }

    private static void ApplyAffixTemplate(
        AffixTemplate? template,
        float? rolledMagnitude,
        CompiledArtifacts artifacts,
        string subjectId)
    {
        if (template == null)
        {
            return;
        }

        foreach (var tag in template.CompileTags)
        {
            artifacts.CompileTags.Add(tag);
        }

        AddRulePackage(template.RulePackage, artifacts, subjectId, "affix_rule");

        var triggeredEffects = AffixMagnitudePackageResolver.ResolveTriggeredEffects(
            template.TriggeredEffects,
            rolledMagnitude);
        if (triggeredEffects.Count == 0)
        {
            return;
        }

        artifacts.TriggeredEffects.AddRange(triggeredEffects);
        artifacts.Provenance.Add(new CompileProvenanceEntry(
            subjectId,
            ModifierSource.Item,
            template.Id,
            "affix_triggered_effect",
            triggeredEffects
                .Select(effect => $"{effect.Trigger}:{effect.Op}:{effect.Magnitude.ToString("R", CultureInfo.InvariantCulture)}")
                .ToList()));
    }

    private static bool IsConditionalAffixSatisfied(AffixTemplate template, HashSet<string> compileTags)
    {
        return template.RequiredTags.All(compileTags.Contains)
            && !template.ExcludedTags.Any(compileTags.Contains);
    }

    private static IReadOnlyList<string> BuildConditionalAffixDetails(AffixTemplate template)
    {
        return template.RequiredTags.Select(tag => $"requires:{tag}")
            .Concat(template.ExcludedTags.Select(tag => $"excludes:{tag}"))
            .ToList();
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

    private static ResolvedRoleInstructionSelection ResolveRoleInstruction(
        DeploymentAnchorId anchor,
        HeroRecord hero,
        SquadBlueprintState blueprint,
        CombatContentSnapshot content)
    {
        if (blueprint.HeroRoleIds.TryGetValue(hero.Id, out var roleId)
            && content.RoleInstructions.TryGetValue(roleId, out var role))
        {
            return new ResolvedRoleInstructionSelection(roleId, role.Instruction with { Anchor = anchor });
        }

        var fallbackRoleTag = hero.ClassId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
        return new ResolvedRoleInstructionSelection(fallbackRoleTag, new SlotRoleInstruction(anchor, fallbackRoleTag));
    }

    private static PlayerTargetDirective ResolveTargetDirective(HeroRecord hero, SquadBlueprintState blueprint)
    {
        return blueprint.HeroTargetDirectives != null
               && blueprint.HeroTargetDirectives.TryGetValue(hero.Id, out var directiveId)
            ? PlayerTargetDirectiveRules.ParseStableId(directiveId)
            : PlayerTargetDirective.Default;
    }

    private static RoleVariantTag ResolveRoleVariant(
        CombatArchetypeTemplate archetype,
        SlotRoleInstruction roleInstruction,
        IReadOnlyList<CombatRuleModifierPackage> rulePackages)
    {
        var isFrontRow = roleInstruction.Anchor.IsFrontRow();
        var hasHeal = archetype.Skills.Any(skill => skill.HealCoeff > 0f);
        var hasControl = archetype.Skills.Any(skill =>
            skill.Kind is SkillKind.Debuff or SkillKind.Utility);
        var hasSummon = archetype.Skills.Any(skill => skill.SummonProfile != null);
        var rangeDiscipline = archetype.Behavior?.RangeDiscipline ?? RangeDiscipline.HoldBand;

        if (archetype.ClassId == "duelist")
        {
            // Build identity is a pure function of the assembled tag set. Precedence is explicit so passive
            // selection order cannot change the derived role variant.
            if (CombatBehaviorTags.Contains(rulePackages, CombatBehaviorTags.DuelistHoldBruiser))
            {
                return RoleVariantTag.Peeler;
            }

            if (CombatBehaviorTags.Contains(rulePackages, CombatBehaviorTags.ExecuteLowHp))
            {
                return RoleVariantTag.Executioner;
            }

            if (CombatBehaviorTags.Contains(rulePackages, CombatBehaviorTags.DuelistDiveCommit))
            {
                return RoleVariantTag.Diver;
            }
        }

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

    private static DominantHand ResolveDominantHand(
        HeroRecord hero,
        CombatArchetypeTemplate archetype,
        CombatContentSnapshot content)
    {
        if (!string.IsNullOrWhiteSpace(hero.CharacterId)
            && content.Characters != null
            && content.Characters.TryGetValue(hero.CharacterId, out var character))
        {
            return character.DominantHand;
        }

        return hero.DominantHand != DominantHand.Right
            ? hero.DominantHand
            : archetype.DefaultDominantHand;
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
                .Append(resolvedTeamTactic.TargetSwitchPenalty.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.Compactness.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.Width.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.Depth.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.LineSpacing.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                .Append(resolvedTeamTactic.FlankBias.ToString("0.###", CultureInfo.InvariantCulture)).Append('|');
        }

        foreach (var unit in units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            sb.Append(unit.Id).Append(':')
                .Append(unit.ArchetypeId).Append(':')
                .Append(unit.CharacterId).Append(':')
                .Append(unit.DominantHand).Append(':')
                .Append(unit.PreferredAnchor).Append(':')
                .Append(unit.RoleInstructionId).Append(':')
                .Append(unit.RoleTag).Append(':')
                .Append(unit.RoleInstruction?.ProtectCarryBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.RoleInstruction?.BacklinePressureBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(unit.RoleInstruction?.RetreatBias.ToString("0.###", CultureInfo.InvariantCulture) ?? "0").Append(':')
                .Append(PlayerTargetDirectiveRules.ToStableId(unit.TargetDirective)).Append(':')
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
                        .Select(status => $"{status.StatusId}:{status.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)}:{status.Magnitude.ToString("0.###", CultureInfo.InvariantCulture)}:{status.MaxStacks}:{status.RefreshDurationOnReapply}{(status.Scope == EffectScope.CurrentTarget ? string.Empty : $":scope={status.Scope}")}"))).Append(':')
                    .Append(skill.CleanseProfileId ?? string.Empty).Append(':')
                    .Append(skill.VfxHookId ?? string.Empty).Append(':')
                    // P3 강제이동은 전투 결과를 바꾸는 저작 입력 — hash 포함(replay/audit).
                    .Append(skill.DisplacementKind).Append(':')
                    .Append(skill.DisplacementDistance.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    // AoE 저작과 포커스 캡도 전투 결과를 바꾸는 컴파일 입력 — hash 포함(replay/audit).
                    .Append(skill.AreaEffectFamily).Append(':')
                    .Append(skill.AreaRadius.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(skill.PunishCluster ? "1" : "0").Append(':')
                    .Append(skill.AllowsEliteFocusCap ? "1" : "0");
                if (skill.StartsOnCooldown)
                {
                    sb.Append(":opening-lock=")
                        .Append(skill.OpeningLockSeconds.ToString("0.###", CultureInfo.InvariantCulture));
                }

                sb.Append('|');
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

            foreach (var trigger in unit.EffectiveTriggeredEffects
                         .OrderBy(trigger => trigger.SourceId, StringComparer.Ordinal)
                         .ThenBy(trigger => trigger.Trigger)
                         .ThenBy(trigger => trigger.Op))
            {
                sb.Append("trig:").Append(trigger.SourceId).Append(':')
                    .Append(trigger.Trigger).Append(':')
                    .Append(trigger.Op).Append(':')
                    .Append(trigger.Scope).Append(':')
                    .Append(trigger.Magnitude.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(trigger.ThresholdRatio.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(trigger.StatusId).Append(':')
                    .Append(trigger.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(trigger.MaxStacks)
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
            $"compactness:{profile.Compactness.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"width:{profile.Width.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"depth:{profile.Depth.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"lineSpacing:{profile.LineSpacing.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"flankBias:{profile.FlankBias.ToString("0.###", CultureInfo.InvariantCulture)}",
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
