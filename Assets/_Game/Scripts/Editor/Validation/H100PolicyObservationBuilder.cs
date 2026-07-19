using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>
/// Unity/session boundary를 player-visible policy DTO로 투영하는 유일한 adapter.
/// 현재 node preview만 읽고 future node traversal이나 resolved encounter stat을 수행하지 않는다.
/// </summary>
internal static class H100PolicyObservationBuilder
{
    public static HeadlessPolicyObservation Build(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        int decisionSeed,
        bool includeTownRoster = false)
    {
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"Cannot build H100 policy observation: {error}");
        }

        var deployed = session.BattleDeployHeroIds.ToHashSet(StringComparer.Ordinal);
        var progressionByHero = session.Profile.HeroProgressions
            .Where(record => !string.IsNullOrWhiteSpace(record.HeroId))
            .GroupBy(record => record.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var loadoutByHero = session.Profile.HeroLoadouts
            .Where(record => !string.IsNullOrWhiteSpace(record.HeroId))
            .GroupBy(record => record.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rosterById = session.Profile.Heroes
            .Where(hero => !string.IsNullOrWhiteSpace(hero.HeroId))
            .GroupBy(hero => hero.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var visibleHeroIds = includeTownRoster
            ? session.Profile.Heroes
                .Where(hero => hero != null && !string.IsNullOrWhiteSpace(hero.HeroId))
                .Select(hero => hero.HeroId)
            : session.ExpeditionSquadHeroIds;
        var roster = visibleHeroIds
            .Where(rosterById.ContainsKey)
            .Select(heroId =>
            {
                var hero = rosterById[heroId];
                snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype);
                var equippedItems = BuildEquippedItems(hero, session.Profile.Inventory, snapshot);
                var selectedPassiveNodeIds = loadoutByHero.TryGetValue(hero.HeroId, out var loadout)
                    ? StableIds(loadout.SelectedPassiveNodeIds)
                    : Array.Empty<string>();
                return new HeadlessHeroObservation(
                    hero.HeroId,
                    hero.ArchetypeId,
                    hero.RaceId,
                    hero.ClassId,
                    archetype?.RoleTag ?? string.Empty,
                    progressionByHero.TryGetValue(hero.HeroId, out var progression) ? progression.Level : 1,
                    hero.CurrentHp,
                    hero.MaxHp,
                    equippedItems.Count,
                    deployed.Contains(hero.HeroId),
                    archetype?.DefaultAnchor ?? ResolveClassAnchor(hero.ClassId),
                    BuildSkillCards(archetype?.Skills),
                    hero.FlexActiveId,
                    hero.FlexPassiveId,
                    equippedItems,
                    selectedPassiveNodeIds);
            })
            .ToArray();

        var temporaryAugments = StableIds(session.Expedition.TemporaryAugmentIds)
            .Select(id => BuildAugmentMechanics(id, snapshot))
            .ToArray();

        var observation = new HeadlessPolicyObservation(
            decisionSeed,
            MetaBalanceDefaults.BattleDeployCap,
            session.SelectedCampaignChapterId,
            session.SelectedCampaignSiteId,
            roster,
            session.DeploymentAnchors.ToArray(),
            BuildCurrentEnemyPreview(session.GetSelectedExpeditionNode(), snapshot),
            session.PendingRewardChoices
                .Select((option, index) => new HeadlessRewardOption(
                    index,
                    MapRewardKind(option.Kind),
                    option.PayloadId,
                    option.GoldAmount,
                    option.EchoAmount,
                    option.PermanentSlotAmount,
                    BuildRewardMechanics(option, snapshot)))
                .ToArray(),
            new HeadlessWalletObservation(
                session.Profile.Currencies.Gold,
                session.Profile.Currencies.Echo),
            temporaryAugments,
            BuildSynergyCounts(roster, snapshot),
            BuildSynergyCatalog(snapshot),
            currentPlacements: session.EnumerateDeploymentAssignments()
                .Where(value => !string.IsNullOrWhiteSpace(value.HeroId))
                .OrderBy(value => value.Anchor)
                .Select(value => new HeadlessPlacement(value.Anchor, value.HeroId!))
                .ToArray(),
            ownedItems: includeTownRoster
                ? session.Profile.Inventory
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemInstanceId))
                    .OrderBy(item => item.ItemInstanceId, StringComparer.Ordinal)
                    .Select(item => new HeadlessOwnedItemObservation(
                        BuildItemMechanics(item.ItemBaseId, item.ItemInstanceId, item.AffixIds, snapshot),
                        item.EquippedHeroId))
                    .ToArray()
                : Array.Empty<HeadlessOwnedItemObservation>());
        observation = H100PlayerVisibleFactProjector.AttachEvidenceIndex(observation);
        HeadlessPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    internal static IReadOnlyList<HeadlessSkillObservation> BuildSkillCards(
        IReadOnlyList<BattleSkillSpec>? skills)
    {
        return (skills ?? Array.Empty<BattleSkillSpec>())
            .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
            .GroupBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(BuildSkillCard)
            .ToArray();
    }

    private static HeadlessSkillObservation BuildSkillCard(BattleSkillSpec skill)
    {
        var statuses = (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
            .Where(status => status != null && !string.IsNullOrWhiteSpace(status.StatusId))
            .OrderBy(status => status.StatusId, StringComparer.Ordinal)
            .ThenBy(status => status.Id, StringComparer.Ordinal)
            .Select(status => new HeadlessStatusApplicationObservation(
                status.Id,
                status.StatusId,
                status.DurationSeconds,
                status.Magnitude,
                status.MaxStacks))
            .ToArray();
        return new HeadlessSkillObservation(
            skill.Id,
            skill.Kind,
            CompiledSkillSlots.Normalize(skill.SlotKind),
            skill.Power,
            skill.Range,
            skill.DamageType,
            skill.PowerFlat,
            skill.PhysCoeff,
            skill.MagCoeff,
            skill.HealCoeff,
            skill.HealthCoeff,
            skill.ManaCost,
            skill.BaseCooldownSeconds,
            skill.CastWindupSeconds,
            skill.CanCrit,
            skill.Delivery,
            skill.TargetRule,
            statuses);
    }

    private static IReadOnlyList<HeadlessItemMechanicsObservation> BuildEquippedItems(
        HeroInstanceRecord hero,
        IEnumerable<InventoryItemRecord> inventory,
        CombatContentSnapshot snapshot)
    {
        var equippedIds = (hero.EquippedItemIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        return (inventory ?? Array.Empty<InventoryItemRecord>())
            .Where(item => item != null
                           && !string.IsNullOrWhiteSpace(item.ItemInstanceId)
                           && (equippedIds.Contains(item.ItemInstanceId)
                               || string.Equals(item.EquippedHeroId, hero.HeroId, StringComparison.Ordinal)))
            .GroupBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.ItemBaseId, StringComparer.Ordinal)
            .ThenBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .Select(item => BuildItemMechanics(
                item.ItemBaseId,
                item.ItemInstanceId,
                item.AffixIds,
                snapshot))
            .ToArray();
    }

    internal static HeadlessItemMechanicsObservation BuildItemMechanics(
        string itemId,
        string itemInstanceId,
        IEnumerable<string>? affixIds,
        CombatContentSnapshot snapshot)
    {
        ItemTemplate? item = null;
        if (snapshot.ItemCatalog != null)
        {
            snapshot.ItemCatalog.TryGetValue(itemId, out item);
        }

        snapshot.ItemPackages.TryGetValue(itemId, out var itemPackage);
        IReadOnlyList<BattleSkillSpec>? grantedSkills = null;
        if (snapshot.ItemGrantedSkills != null)
        {
            snapshot.ItemGrantedSkills.TryGetValue(itemId, out grantedSkills);
        }

        return new HeadlessItemMechanicsObservation(
            itemId,
            itemInstanceId,
            StableIds(item?.CompileTags),
            item?.WeaponFamilyTag ?? string.Empty,
            BuildStatModifiers(itemPackage?.Modifiers),
            StableIds(affixIds)
                .Select(id => BuildAffixMechanics(id, snapshot))
                .ToArray(),
            BuildSkillCards(grantedSkills));
    }

    internal static HeadlessAffixMechanicsObservation BuildAffixMechanics(
        string affixId,
        CombatContentSnapshot snapshot)
    {
        AffixTemplate? affix = null;
        if (snapshot.AffixCatalog != null)
        {
            snapshot.AffixCatalog.TryGetValue(affixId, out affix);
        }

        snapshot.AffixPackages.TryGetValue(affixId, out var package);
        return new HeadlessAffixMechanicsObservation(
            affixId,
            StableIds(affix?.CompileTags),
            StableIds(affix?.RequiredTags),
            StableIds(affix?.ExcludedTags),
            BuildStatModifiers(package?.Modifiers),
            BuildRuleModifiers(affix?.RulePackage));
    }

    internal static HeadlessAugmentMechanicsObservation BuildAugmentMechanics(
        string augmentId,
        CombatContentSnapshot snapshot)
    {
        snapshot.AugmentCatalog.TryGetValue(augmentId, out var augment);
        snapshot.AugmentPackages.TryGetValue(augmentId, out var package);
        return new HeadlessAugmentMechanicsObservation(
            augmentId,
            augment?.Category ?? string.Empty,
            augment?.FamilyId ?? string.Empty,
            augment?.Tier ?? 0,
            StableIds(augment?.Tags),
            StableIds(augment?.BuildBiasTags),
            BuildStatModifiers(package?.Modifiers),
            BuildRuleModifiers(augment?.RulePackage),
            BuildTriggeredEffects(augment?.TriggeredEffects));
    }

    private static HeadlessRewardMechanicsObservation BuildRewardMechanics(
        RewardChoiceViewModel option,
        CombatContentSnapshot snapshot)
    {
        return option.Kind switch
        {
            RewardChoiceKind.Item => new HeadlessRewardMechanicsObservation(
                BuildItemMechanics(option.PayloadId, string.Empty, Array.Empty<string>(), snapshot),
                null),
            RewardChoiceKind.TemporaryAugment => new HeadlessRewardMechanicsObservation(
                null,
                BuildAugmentMechanics(option.PayloadId, snapshot)),
            _ => HeadlessRewardMechanicsObservation.Empty,
        };
    }

    private static IReadOnlyList<HeadlessSynergyCountObservation> BuildSynergyCounts(
        IReadOnlyList<HeadlessHeroObservation> roster,
        CombatContentSnapshot snapshot)
    {
        var deployedTags = roster
            .Where(hero => hero.IsDeployed)
            .OrderBy(hero => hero.HeroId, StringComparer.Ordinal)
            .Select(hero =>
            {
                var tags = new List<string> { hero.RaceId, hero.ClassId };
                if (snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype))
                {
                    tags.AddRange(archetype.RecruitPlanTags ?? Array.Empty<string>());
                }

                return (IReadOnlyList<string>)StableIds(tags);
            })
            .ToArray();
        return SquadSynergyPreview.Evaluate(deployedTags, snapshot.SynergyCatalog)
            .GroupBy(surface => surface.CountedTagId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new HeadlessSynergyCountObservation(
                group.Key,
                group.Max(surface => surface.CurrentCount)))
            .ToArray();
    }

    internal static IReadOnlyList<HeadlessSynergyObservation> BuildSynergyCatalog(
        CombatContentSnapshot snapshot)
    {
        return snapshot.SynergyCatalog.Values
            .Where(template => template?.Rule != null
                               && !string.IsNullOrWhiteSpace(template.Rule.SynergyId)
                               && template.Rule.Threshold > 0)
            .Select(template => template.Rule)
            .GroupBy(rule => rule.SynergyId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new HeadlessSynergyObservation(
                group.Key,
                group.Select(rule => rule.CountedTagId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .FirstOrDefault() ?? string.Empty,
                group.OrderBy(rule => rule.Threshold)
                    .ThenBy(rule => rule.GrantedTeamRuleId, StringComparer.Ordinal)
                    .Select(rule => new HeadlessSynergyTierObservation(
                        rule.Threshold,
                        BuildStatModifiers(rule.Modifiers),
                        SM.Combat.Services.SynergyService.ResolveGrantedTeamRuleId(
                            rule.CountedTagId, rule.Threshold, rule.GrantedTeamRuleId)))
                    .ToArray()))
            .ToArray();
    }

    internal static IReadOnlyList<HeadlessStatModifierObservation> BuildStatModifiers(
        IEnumerable<StatModifier>? modifiers)
    {
        return (modifiers ?? Array.Empty<StatModifier>())
            .Where(modifier => modifier != null)
            .Select(modifier => new HeadlessStatModifierObservation(
                modifier.Stat.ToString(),
                modifier.Op.ToString(),
                modifier.Value,
                modifier.Tag?.Value ?? string.Empty))
            .OrderBy(modifier => modifier.StatId, StringComparer.Ordinal)
            .ThenBy(modifier => modifier.Operation, StringComparer.Ordinal)
            .ThenBy(modifier => modifier.Value)
            .ThenBy(modifier => modifier.TagId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<HeadlessRuleModifierObservation> BuildRuleModifiers(
        CombatRuleModifierPackage? package)
    {
        return (package?.Modifiers ?? Array.Empty<RuleModifier>())
            .Where(modifier => modifier != null)
            .Select(modifier => new HeadlessRuleModifierObservation(
                modifier.Kind.ToString(),
                modifier.Value,
                modifier.Magnitude))
            .OrderBy(modifier => modifier.Kind, StringComparer.Ordinal)
            .ThenBy(modifier => modifier.Value, StringComparer.Ordinal)
            .ThenBy(modifier => modifier.Magnitude)
            .ToArray();
    }

    private static IReadOnlyList<HeadlessTriggeredEffectObservation> BuildTriggeredEffects(
        IEnumerable<CombatTriggeredEffect>? effects)
    {
        return (effects ?? Array.Empty<CombatTriggeredEffect>())
            .Where(effect => effect != null)
            .Select(effect => new HeadlessTriggeredEffectObservation(
                effect.Trigger.ToString(),
                effect.Op.ToString(),
                effect.Scope.ToString(),
                effect.Magnitude,
                effect.ThresholdRatio,
                effect.StatusId,
                effect.DurationSeconds,
                effect.MaxStacks))
            .OrderBy(effect => effect.Trigger, StringComparer.Ordinal)
            .ThenBy(effect => effect.Operation, StringComparer.Ordinal)
            .ThenBy(effect => effect.Scope, StringComparer.Ordinal)
            .ThenBy(effect => effect.StatusId, StringComparer.Ordinal)
            .ThenBy(effect => effect.Magnitude)
            .ToArray();
    }

    private static string[] StableIds(IEnumerable<string>? ids)
    {
        return (ids ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static HeadlessEnemyPreview BuildCurrentEnemyPreview(
        ExpeditionNodeViewModel? currentNode,
        CombatContentSnapshot snapshot)
    {
        if (currentNode is not { RequiresBattle: true }
            || snapshot.Encounters is not { } encounters
            || !encounters.TryGetValue(currentNode.Id, out var encounter))
        {
            return HeadlessEnemyPreview.Unavailable;
        }

        var members = Array.Empty<EnemySquadMemberTemplate>();
        IReadOnlyList<string> squadRewardTags = Array.Empty<string>();
        if (snapshot.EnemySquads is { } squads
            && squads.TryGetValue(encounter.EnemySquadTemplateId, out var squad))
        {
            members = (squad.Members ?? Array.Empty<EnemySquadMemberTemplate>()).ToArray();
            squadRewardTags = squad.RewardDropTags ?? Array.Empty<string>();
        }

        var units = members.Select(member =>
        {
            snapshot.Archetypes.TryGetValue(member.ArchetypeId, out var archetype);
            return new HeadlessEnemyUnitPreview(
                member.ArchetypeId,
                archetype?.RaceId ?? string.Empty,
                archetype?.ClassId ?? string.Empty,
                archetype?.RoleTag ?? string.Empty,
                archetype?.DefaultAnchor ?? ResolveClassAnchor(archetype?.ClassId ?? string.Empty));
        }).ToArray();

        var bossAura = string.Empty;
        var bossUtility = string.Empty;
        IReadOnlyList<string> bossRewardTags = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(encounter.BossOverlayId)
            && snapshot.BossOverlays is { } overlays
            && overlays.TryGetValue(encounter.BossOverlayId, out var overlay))
        {
            bossAura = overlay.SignatureAuraTag;
            bossUtility = overlay.SignatureUtilityTag;
            bossRewardTags = overlay.RewardDropTags ?? Array.Empty<string>();
        }

        var rewardTags = (encounter.RewardDropTags ?? Array.Empty<string>())
            .Concat(squadRewardTags)
            .Concat(bossRewardTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .Take(8)
            .ToArray();
        return new HeadlessEnemyPreview(
            true,
            encounter.Id,
            encounter.FactionId,
            encounter.DifficultyBand,
            Math.Max(1, encounter.ThreatSkulls),
            units,
            bossAura,
            bossUtility,
            rewardTags);
    }

    private static SM.Combat.Model.DeploymentAnchorId ResolveClassAnchor(string classId)
        => classId is "vanguard" or "duelist"
            ? SM.Combat.Model.DeploymentAnchorId.FrontCenter
            : SM.Combat.Model.DeploymentAnchorId.BackCenter;

    private static HeadlessRewardKind MapRewardKind(RewardChoiceKind kind)
        => kind switch
        {
            RewardChoiceKind.Gold => HeadlessRewardKind.Gold,
            RewardChoiceKind.Item => HeadlessRewardKind.Item,
            RewardChoiceKind.TemporaryAugment => HeadlessRewardKind.TemporaryAugment,
            RewardChoiceKind.Echo => HeadlessRewardKind.Echo,
            RewardChoiceKind.PermanentAugmentSlot => HeadlessRewardKind.PermanentAugmentSlot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown visible reward kind."),
        };
}
