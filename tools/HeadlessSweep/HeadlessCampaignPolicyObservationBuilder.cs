using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;

internal static class HeadlessCampaignPolicyObservationBuilder
{
    internal static HeadlessPolicyObservation Build(
        HeadlessCampaignState state,
        int decisionSeed,
        bool includeTownRoster,
        ResolvedEncounterContext? measuredEncounter = null)
    {
        var deployed = state.Assignments.Values.ToHashSet(StringComparer.Ordinal);
        var visibleHeroIds = includeTownRoster
            ? state.Heroes.Select(hero => hero.Id)
            : state.ExpeditionSquadHeroIds;
        var heroesById = state.Heroes.ToDictionary(hero => hero.Id, StringComparer.Ordinal);
        var roster = visibleHeroIds
            .Where(heroesById.ContainsKey)
            .Select(heroId =>
            {
                var hero = heroesById[heroId];
                state.Snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype);
                var equippedItems = BuildEquippedItems(hero, state.Inventory, state.Snapshot);
                return new HeadlessHeroObservation(
                    hero.Id,
                    hero.ArchetypeId,
                    hero.RaceId,
                    hero.ClassId,
                    archetype?.RoleTag ?? string.Empty,
                    hero.Level,
                    hero.CurrentHp,
                    hero.MaxHp,
                    equippedItems.Count,
                    deployed.Contains(hero.Id),
                    archetype?.DefaultAnchor ?? ResolveClassAnchor(hero.ClassId),
                    BuildSkillCards(archetype?.Skills),
                    hero.FlexActiveId,
                    hero.FlexPassiveId,
                    equippedItems,
                    StableIds(hero.SelectedPassiveNodeIds));
            })
            .ToArray();

        var enemyPreview = BuildCurrentEnemyPreview(state.SelectedNode, state.Snapshot);
        if (measuredEncounter != null)
        {
            enemyPreview = ProjectMeasuredPreview(enemyPreview, measuredEncounter.Enemies);
        }

        var temporaryAugments = StableIds(state.TemporaryAugmentIds)
            .Select(id => BuildAugmentMechanics(id, state.Snapshot))
            .ToArray();
        var observation = new HeadlessPolicyObservation(
            decisionSeed,
            MetaBalanceDefaults.BattleDeployCap,
            state.SelectedChapterId,
            state.SelectedSiteId,
            roster,
            HeadlessCampaignState.DeploymentAnchors,
            enemyPreview,
            Array.Empty<HeadlessRewardOption>(),
            new HeadlessWalletObservation(state.Gold, state.Echo),
            temporaryAugments,
            BuildSynergyCounts(roster, state.Snapshot),
            BuildSynergyCatalog(state.Snapshot),
            currentPlacements: state.Assignments
                .OrderBy(pair => pair.Key)
                .Select(pair => new HeadlessPlacement(pair.Key, pair.Value))
                .ToArray(),
            ownedItems: includeTownRoster
                ? state.Inventory
                    .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                    .Select(item => new HeadlessOwnedItemObservation(
                        BuildItemMechanics(item.ItemBaseId, item.InstanceId, item.AffixIds, state.Snapshot),
                        item.EquippedHeroId))
                    .ToArray()
                : Array.Empty<HeadlessOwnedItemObservation>());
        observation = observation.WithEvidenceFactIds(BuildEvidenceIndex(observation));
        HeadlessPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    private static IReadOnlyDictionary<string, string> BuildEvidenceIndex(HeadlessPolicyObservation observation)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            HeadlessPolicyEvidence.DecisionSeedSignal,
            HeadlessPolicyEvidence.CampaignContextSignal,
            HeadlessPolicyEvidence.DeploymentSurfaceSignal,
            HeadlessPolicyEvidence.RosterSurfaceSignal,
            HeadlessPolicyEvidence.EnemyPreviewSignal,
            HeadlessPolicyEvidence.RewardSurfaceSignal,
        };
        foreach (var hero in observation.Roster)
        {
            keys.Add(HeadlessPolicyEvidence.HeroSignal(hero.HeroId));
            foreach (var skill in hero.SkillCards)
            {
                keys.Add(HeadlessPolicyEvidence.HeroSkillSignal(hero.HeroId, skill.SkillId));
            }
        }

        for (var index = 0; index < observation.EnemyPreview.Units.Count; index++)
        {
            keys.Add(HeadlessPolicyEvidence.EnemyUnitSignal(index));
        }

        return keys.OrderBy(key => key, StringComparer.Ordinal)
            .ToDictionary(key => key, key => $"headless-fact:{key}", StringComparer.Ordinal);
    }

    private static IReadOnlyList<HeadlessSkillObservation> BuildSkillCards(
        IReadOnlyList<BattleSkillSpec>? skills)
        => (skills ?? Array.Empty<BattleSkillSpec>())
            .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
            .GroupBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(BuildSkillCard)
            .ToArray();

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
        HeadlessCampaignHero hero,
        IEnumerable<HeadlessCampaignItem> inventory,
        CombatContentSnapshot snapshot)
    {
        var equippedIds = hero.EquippedItemIds.ToHashSet(StringComparer.Ordinal);
        return inventory
            .Where(item => equippedIds.Contains(item.InstanceId)
                           || string.Equals(item.EquippedHeroId, hero.Id, StringComparison.Ordinal))
            .GroupBy(item => item.InstanceId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.ItemBaseId, StringComparer.Ordinal)
            .ThenBy(item => item.InstanceId, StringComparer.Ordinal)
            .Select(item => BuildItemMechanics(item.ItemBaseId, item.InstanceId, item.AffixIds, snapshot))
            .ToArray();
    }

    private static HeadlessItemMechanicsObservation BuildItemMechanics(
        string itemId,
        string itemInstanceId,
        IEnumerable<string>? affixIds,
        CombatContentSnapshot snapshot)
    {
        ItemTemplate? item = null;
        snapshot.ItemCatalog?.TryGetValue(itemId, out item);
        snapshot.ItemPackages.TryGetValue(itemId, out var itemPackage);
        IReadOnlyList<BattleSkillSpec>? grantedSkills = null;
        snapshot.ItemGrantedSkills?.TryGetValue(itemId, out grantedSkills);
        return new HeadlessItemMechanicsObservation(
            itemId,
            itemInstanceId,
            StableIds(item?.CompileTags),
            item?.WeaponFamilyTag ?? string.Empty,
            BuildStatModifiers(itemPackage?.Modifiers),
            StableIds(affixIds).Select(id => BuildAffixMechanics(id, snapshot)).ToArray(),
            BuildSkillCards(grantedSkills));
    }

    private static HeadlessAffixMechanicsObservation BuildAffixMechanics(
        string affixId,
        CombatContentSnapshot snapshot)
    {
        AffixTemplate? affix = null;
        snapshot.AffixCatalog?.TryGetValue(affixId, out affix);
        snapshot.AffixPackages.TryGetValue(affixId, out var package);
        return new HeadlessAffixMechanicsObservation(
            affixId,
            StableIds(affix?.CompileTags),
            StableIds(affix?.RequiredTags),
            StableIds(affix?.ExcludedTags),
            BuildStatModifiers(package?.Modifiers),
            BuildRuleModifiers(affix?.RulePackage));
    }

    private static HeadlessAugmentMechanicsObservation BuildAugmentMechanics(
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

    private static IReadOnlyList<HeadlessSynergyObservation> BuildSynergyCatalog(
        CombatContentSnapshot snapshot)
        => snapshot.SynergyCatalog.Values
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
                        SynergyService.ResolveGrantedTeamRuleId(
                            rule.CountedTagId,
                            rule.Threshold,
                            rule.GrantedTeamRuleId)))
                    .ToArray()))
            .ToArray();

    private static IReadOnlyList<HeadlessStatModifierObservation> BuildStatModifiers(
        IEnumerable<StatModifier>? modifiers)
        => (modifiers ?? Array.Empty<StatModifier>())
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

    private static IReadOnlyList<HeadlessRuleModifierObservation> BuildRuleModifiers(
        CombatRuleModifierPackage? package)
        => (package?.Modifiers ?? Array.Empty<RuleModifier>())
            .Where(modifier => modifier != null)
            .Select(modifier => new HeadlessRuleModifierObservation(
                modifier.Kind.ToString(),
                modifier.Value,
                modifier.Magnitude))
            .OrderBy(modifier => modifier.Kind, StringComparer.Ordinal)
            .ThenBy(modifier => modifier.Value, StringComparer.Ordinal)
            .ThenBy(modifier => modifier.Magnitude)
            .ToArray();

    private static IReadOnlyList<HeadlessTriggeredEffectObservation> BuildTriggeredEffects(
        IEnumerable<CombatTriggeredEffect>? effects)
        => (effects ?? Array.Empty<CombatTriggeredEffect>())
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

    private static string[] StableIds(IEnumerable<string>? ids)
        => (ids ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static HeadlessEnemyPreview BuildCurrentEnemyPreview(
        SiteTrackNodeState? currentNode,
        CombatContentSnapshot snapshot)
    {
        if (currentNode is not { RequiresBattle: true }
            || snapshot.Encounters is not { } encounters
            || !encounters.TryGetValue(currentNode.EncounterId, out var encounter))
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
                archetype?.DefaultAnchor ?? ResolveClassAnchor(archetype?.ClassId ?? string.Empty),
                member.EquipmentBudget > 0
                    ? new[]
                    {
                        BuildItemMechanics(
                            member.EquipmentItemBaseId,
                            string.Empty,
                            member.EquipmentAffixIds,
                            snapshot),
                    }
                    : Array.Empty<HeadlessItemMechanicsObservation>());
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

    private static HeadlessEnemyPreview ProjectMeasuredPreview(
        HeadlessEnemyPreview authored,
        IReadOnlyList<BattleUnitLoadout> enemies)
        => new(
            authored.IsAvailable,
            authored.EncounterId,
            authored.FactionId,
            authored.DifficultyBand,
            authored.ThreatSkulls,
            (enemies ?? Array.Empty<BattleUnitLoadout>())
                .Select(unit => new HeadlessEnemyUnitPreview(
                    unit.ArchetypeId,
                    unit.RaceId,
                    unit.ClassId,
                    unit.RoleTag,
                    unit.PreferredAnchor,
                    authored.Units.FirstOrDefault(value => string.Equals(
                        value.ArchetypeId,
                        unit.ArchetypeId,
                        StringComparison.Ordinal))?.EquippedItems
                    ?? Array.Empty<HeadlessItemMechanicsObservation>()))
                .ToArray(),
            authored.BossAuraTag,
            authored.BossUtilityTag,
            authored.RewardDropTags);

    private static DeploymentAnchorId ResolveClassAnchor(string classId)
        => classId is "vanguard" or "duelist"
            ? DeploymentAnchorId.FrontCenter
            : DeploymentAnchorId.BackCenter;
}
