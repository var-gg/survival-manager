using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.HeadlessCensus;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>실 session의 현재 roster와 실제 제시 offer를 evaluator-only pure DTO로 낮춘다.</summary>
internal static class H100IntentTrackInputProjector
{
    public static IntentTrackState ProjectInitialState(
        HeadlessPolicyObservation observation,
        SaveProfile profile,
        CombatContentSnapshot snapshot)
    {
        var roster = observation.Roster.OrderBy(value => value.HeroId, StringComparer.Ordinal)
            .Select(ProjectMember)
            .ToArray();
        var inventory = new List<string>();
        foreach (var item in (profile.Inventory ?? new List<InventoryItemRecord>())
                     .OrderBy(value => value.ItemBaseId, StringComparer.Ordinal)
                     .ThenBy(value => value.ItemInstanceId, StringComparer.Ordinal))
        {
            inventory.Add($"item:{item.ItemBaseId}");
            inventory.AddRange((item.AffixIds ?? new List<string>()).Select(value => $"affix:{value}"));
        }

        var temporaryAugmentComponents = observation.TemporaryAugments
            .Select(value => $"augment:{value.AugmentId}")
            .ToArray();
        var temporaryAugmentEffects = observation.TemporaryAugments.SelectMany(AugmentEffects).ToArray();
        var selected = observation.Roster.Where(value => value.IsDeployed).ToArray();
        var activeSynergies = ActiveSynergies(selected, observation.SynergyCatalog);
        var owned = roster.SelectMany(value => value.ComponentIds)
            .Concat(inventory)
            .Concat(temporaryAugmentComponents)
            .Concat(activeSynergies)
            .ToArray();
        var activeMembers = selected.Select(ProjectMember).ToArray();
        return new IntentTrackState(
            roster,
            MetaBalanceDefaults.TownRosterCap,
            inventory,
            roster.SelectMany(value => value.ComponentIds).Where(value => value.StartsWith("skill:", StringComparison.Ordinal)).ToArray(),
            roster.SelectMany(value => value.ComponentIds).Where(value => value.StartsWith("passive:", StringComparison.Ordinal)).ToArray(),
            owned,
            observation.Wallet.Gold,
            observation.Roster.Sum(value => Math.Max(
                0,
                PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(value.Level) - value.SelectedPassiveNodeIds.Count)),
            observation.Wallet.Echo,
            selected.Select(value => value.HeroId).ToArray(),
            TagCounts(activeMembers),
            activeMembers.SelectMany(value => value.ComponentIds)
                .Concat(temporaryAugmentComponents)
                .Concat(activeSynergies)
                .ToArray(),
            activeMembers.SelectMany(value => value.EffectIds).Concat(temporaryAugmentEffects).ToArray(),
            TeamRules(selected, snapshot),
            null,
            Array.Empty<string>());
    }

    public static IReadOnlyList<IntentTrackChoice> ProjectDeploymentChoices(
        HeadlessPolicyObservation observation,
        IReadOnlyList<ConceptContract> contracts,
        IReadOnlyList<FormationPlacement> formations,
        CombatContentSnapshot snapshot)
    {
        if (contracts == null || contracts.Count == 0)
        {
            throw new ArgumentException("Intent-track deployment projection requires at least one contract.", nameof(contracts));
        }

        var roster = observation.Roster.OrderBy(value => value.HeroId, StringComparer.Ordinal).ToArray();
        var capacity = Math.Min(observation.DeployCapacity, roster.Length);
        var formationPredicates = contracts.SelectMany(contract => contract.IdentityPredicates.Concat(contract.ProgressMilestones))
            .Where(value => value.StartsWith("formation.", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var representatives = formations
            .GroupBy(value => string.Join(",", formationPredicates.Select(predicate =>
                IntentTrackPredicateEvaluator.SatisfiesFormationPredicate(predicate, value.Features) ? "1" : "0")), StringComparer.Ordinal)
            .Select(group => group.OrderBy(value => value.Signature, StringComparer.Ordinal).First())
            .OrderBy(value => value.Signature, StringComparer.Ordinal)
            .ToArray();
        if (representatives.Length == 0)
        {
            throw new InvalidOperationException("Intent-track deployment projection requires at least one legal formation.");
        }

        var augmentComponents = observation.TemporaryAugments.Select(value => $"augment:{value.AugmentId}").ToArray();
        var augmentEffects = observation.TemporaryAugments.SelectMany(AugmentEffects).ToArray();
        var choices = new List<IntentTrackChoice>();
        var rosterRepresentatives = EnumerateCombinations(roster, capacity)
            .GroupBy(selected => RosterSemanticSignature(selected, contracts), StringComparer.Ordinal)
            .Select(group => group.OrderBy(
                    selected => string.Join("|", selected.Select(value => value.ArchetypeId).OrderBy(value => value, StringComparer.Ordinal)),
                    StringComparer.Ordinal)
                .First())
            .OrderBy(selected => string.Join("|", selected.Select(value => value.ArchetypeId).OrderBy(value => value, StringComparer.Ordinal)), StringComparer.Ordinal)
            .ToArray();
        foreach (var selected in rosterRepresentatives)
        {
            var selectedMembers = selected.Select(ProjectMember).ToArray();
            var memberIds = selectedMembers.Select(value => value.MemberId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            foreach (var formation in representatives)
            {
                var activeSynergies = ActiveSynergies(selected, observation.SynergyCatalog);
                var activeComponents = selectedMembers.SelectMany(value => value.ComponentIds)
                    .Concat(augmentComponents)
                    .Concat(activeSynergies)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var activeEffects = selectedMembers.SelectMany(value => value.EffectIds)
                    .Concat(augmentEffects)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var teamRules = TeamRules(selected, snapshot);
                choices.Add(new IntentTrackChoice(
                    $"deploy:{string.Join("+", memberIds)}:{formation.Signature}",
                    memberIds,
                    Array.Empty<string>(),
                    Array.Empty<IntentTrackRosterMember>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    memberIds,
                    TagCounts(selectedMembers),
                    activeComponents,
                    activeEffects,
                    teamRules,
                    formation.Features,
                    activeComponents.Concat(activeEffects).Concat(teamRules).ToArray(),
                    false));
            }
        }

        return choices.OrderBy(value => value.ChoiceId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<IntentTrackChoice> ProjectRewardChoices(HeadlessPolicyObservation observation)
    {
        return observation.RewardOptions.OrderBy(value => value.Index).Select(option =>
        {
            var owned = new List<string>();
            var inventory = new List<string>();
            var effects = new List<string>();
            var offered = new List<string> { option.PayloadId, option.Kind.ToString() };
            if (option.Mechanics.Item != null)
            {
                var item = option.Mechanics.Item;
                owned.Add($"item:{item.ItemId}");
                inventory.Add($"item:{item.ItemId}");
                owned.AddRange(item.Affixes.Select(value => $"affix:{value.AffixId}"));
                owned.AddRange(item.GrantedSkills.Select(value => $"skill:{value.SkillId}"));
                offered.AddRange(item.Tags);
                offered.AddRange(item.StatModifiers.Select(value => value.StatId));
                offered.AddRange(item.GrantedSkills.Select(value => $"skill:{value.SkillId}"));
                effects.AddRange(SkillEffects(item.GrantedSkills));
            }

            if (option.Mechanics.TemporaryAugment != null)
            {
                var augment = option.Mechanics.TemporaryAugment;
                owned.Add($"augment:{augment.AugmentId}");
                effects.AddRange(AugmentEffects(augment));
                offered.AddRange(augment.Tags);
                offered.AddRange(augment.BuildBiasTags);
                offered.AddRange(augment.StatModifiers.Select(value => value.StatId));
            }

            offered.AddRange(owned);
            offered.AddRange(effects);
            return new IntentTrackChoice(
                $"reward:{option.Index:D2}:{option.Kind}:{option.PayloadId}",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<IntentTrackRosterMember>(),
                inventory,
                option.Mechanics.Item?.GrantedSkills.Select(value => $"skill:{value.SkillId}").ToArray()
                ?? Array.Empty<string>(),
                Array.Empty<string>(),
                owned,
                option.GoldAmount,
                0,
                0,
                0,
                option.EchoAmount,
                0,
                Array.Empty<string>(),
                Array.Empty<IntentTrackTagCount>(),
                Array.Empty<string>(),
                effects,
                Array.Empty<string>(),
                null,
                offered,
                true);
        }).ToArray();
    }

    private static IntentTrackRosterMember ProjectMember(HeadlessHeroObservation hero)
    {
        var components = new List<string>
        {
            $"archetype:{hero.ArchetypeId}",
        };
        components.AddRange(hero.SkillCards.Select(value => $"skill:{value.SkillId}"));
        if (!string.IsNullOrWhiteSpace(hero.FlexActiveSkillId)) components.Add($"skill:{hero.FlexActiveSkillId}");
        if (!string.IsNullOrWhiteSpace(hero.FlexPassiveSkillId)) components.Add($"passive:{hero.FlexPassiveSkillId}");
        components.AddRange(hero.SelectedPassiveNodeIds.Select(value => $"passive:{value}"));
        components.AddRange(hero.EquippedItems.Select(value => $"item:{value.ItemId}"));
        components.AddRange(hero.EquippedItems.SelectMany(value => value.Affixes).Select(value => $"affix:{value.AffixId}"));
        components.AddRange(hero.EquippedItems.SelectMany(value => value.GrantedSkills).Select(value => $"skill:{value.SkillId}"));
        var skills = hero.SkillCards.Concat(hero.EquippedItems.SelectMany(value => value.GrantedSkills)).ToArray();
        return new IntentTrackRosterMember(
            hero.ArchetypeId,
            new[] { hero.ArchetypeId, hero.RaceId, hero.ClassId, hero.RoleTag }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            components.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            SkillEffects(skills));
    }

    internal static IReadOnlyList<string> SkillEffects(IEnumerable<HeadlessSkillObservation> skills)
    {
        var effects = new List<string>();
        foreach (var skill in skills)
        {
            if (skill.HealingCoefficient > 0f)
            {
                effects.Add("combat_effect:healing");
                effects.Add("combat_effect:Heal");
            }

            if (skill.Power > 0f
                || skill.PowerFlat > 0f
                || skill.PhysicalCoefficient > 0f
                || skill.MagicalCoefficient > 0f
                || skill.HealthCoefficient > 0f)
            {
                effects.Add("combat_effect:damage");
            }

            foreach (var status in skill.AppliedStatuses)
            {
                effects.Add($"status:{status.StatusId}");
                if (ContainsAny(status.StatusId, "barrier", "shield")) effects.Add("combat_effect:barrier");
                if (ContainsAny(status.StatusId, "barrier", "shield")) effects.Add("combat_effect:Barrier");
                if (ContainsAny(status.StatusId, "cleanse")) effects.Add("combat_effect:status_removed");
            }
        }

        return effects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> AugmentEffects(HeadlessAugmentMechanicsObservation augment)
    {
        var effects = new List<string>();
        foreach (var effect in augment.TriggeredEffects)
        {
            if (!string.IsNullOrWhiteSpace(effect.StatusId)) effects.Add($"status:{effect.StatusId}");
            if (ContainsAny(effect.Operation, "heal")) effects.Add("combat_effect:healing");
            if (ContainsAny(effect.Operation, "heal")) effects.Add("combat_effect:Heal");
            if (ContainsAny(effect.Operation, "damage")) effects.Add("combat_effect:damage");
            if (ContainsAny(effect.Operation, "barrier", "shield")) effects.Add("combat_effect:barrier");
            if (ContainsAny(effect.Operation, "barrier", "shield")) effects.Add("combat_effect:Barrier");
            if (ContainsAny(effect.Operation, "energy")) effects.Add("combat_effect:GainEnergy");
        }

        return effects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<IntentTrackTagCount> TagCounts(IEnumerable<IntentTrackRosterMember> members)
        => members.SelectMany(value => value.Tags)
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new IntentTrackTagCount(group.Key, group.Count()))
            .ToArray();

    private static IReadOnlyList<string> TeamRules(
        IReadOnlyList<HeadlessHeroObservation> selected,
        CombatContentSnapshot snapshot)
    {
        var units = selected.OrderBy(hero => hero.HeroId, StringComparer.Ordinal)
            .Select(hero => new BattleUnitLoadout(
                hero.HeroId,
                hero.HeroId,
                hero.RaceId,
                hero.ClassId,
                hero.PreferredAnchor,
                new Dictionary<StatKey, float>(),
                Array.Empty<UnitRuleChain>(),
                Array.Empty<BattleSkillSpec>(),
                CompileTags: new[] { hero.RaceId, hero.ClassId, hero.RoleTag }))
            .ToArray();
        var rules = snapshot.SynergyCatalog.Values
            .Where(value => value?.Rule != null)
            .Select(value => value.Rule)
            .OrderBy(value => value.SynergyId, StringComparer.Ordinal)
            .ThenBy(value => value.Threshold)
            .ToArray();
        return SynergyService.BuildForTeam(units, rules)
            .Select(package => package.GrantedTeamRuleId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ActiveSynergies(
        IReadOnlyList<HeadlessHeroObservation> selected,
        IReadOnlyList<HeadlessSynergyObservation> catalog)
    {
        return catalog.SelectMany(synergy => synergy.Tiers
                .Where(tier => selected.Count(hero => HeroHasTag(hero, synergy.CountedTagId)) >= tier.Threshold)
                .Select(tier => $"synergy:{synergy.SynergyId}@{tier.Threshold}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HeroHasTag(HeadlessHeroObservation hero, string tag)
        => string.Equals(hero.ArchetypeId, tag, StringComparison.Ordinal)
           || string.Equals(hero.RaceId, tag, StringComparison.Ordinal)
           || string.Equals(hero.ClassId, tag, StringComparison.Ordinal)
           || string.Equals(hero.RoleTag, tag, StringComparison.Ordinal);

    private static string RosterSemanticSignature(
        IReadOnlyList<HeadlessHeroObservation> selected,
        IReadOnlyList<ConceptContract> contracts)
    {
        var claims = contracts.SelectMany(contract => contract.IdentityPredicates
                .Concat(contract.ProgressMilestones)
                .Concat(contract.AllowedSubstitutions)
                .Concat(contract.CounterAffordances))
            .ToArray();
        var members = selected.Select(ProjectMember).ToArray();
        var tagCounts = members.SelectMany(value => value.Tags)
            .Where(tag => claims.Any(claim => claim.Contains(tag, StringComparison.Ordinal)))
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}:{group.Count()}");
        var semantic = members.SelectMany(value => value.ComponentIds.Concat(value.EffectIds))
            .Where(value => claims.Any(claim => claim.Contains(value, StringComparison.Ordinal)
                                                || claim.Contains(SemanticTail(value), StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);
        return $"tags={string.Join(",", tagCounts)}|semantic={string.Join(",", semantic)}";
    }

    private static string SemanticTail(string value)
    {
        var separator = value.IndexOf(':');
        return separator >= 0 && separator + 1 < value.Length ? value.Substring(separator + 1) : value;
    }

    private static IReadOnlyList<HeadlessHeroObservation[]> EnumerateCombinations(
        IReadOnlyList<HeadlessHeroObservation> values,
        int choose)
    {
        var results = new List<HeadlessHeroObservation[]>();
        var buffer = new HeadlessHeroObservation[choose];
        void Visit(int sourceIndex, int targetIndex)
        {
            if (targetIndex == choose)
            {
                results.Add(buffer.ToArray());
                return;
            }

            for (var index = sourceIndex; index <= values.Count - (choose - targetIndex); index++)
            {
                buffer[targetIndex] = values[index];
                Visit(index + 1, targetIndex + 1);
            }
        }

        if (choose > 0) Visit(0, 0);
        return results;
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => !string.IsNullOrWhiteSpace(value)
           && tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
