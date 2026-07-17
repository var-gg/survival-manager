using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>Town recruit/passive/refit UI parity surface를 opt-in policy DTO로 투영한다.</summary>
internal static class H100RosterPolicyObservationBuilder
{
    public static HeadlessRosterPolicyObservation Build(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        int decisionSeed)
    {
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"Cannot build H100 roster policy observation: {error}");
        }

        var policyObservation = H100PolicyObservationBuilder.Build(
            session,
            lookup,
            decisionSeed,
            includeTownRoster: true);
        var rosterArchetypes = policyObservation.Roster.Select(value => value.ArchetypeId)
            .ToHashSet(StringComparer.Ordinal);
        var recruitOffers = session.RecruitOffers.Select((offer, index) =>
        {
            snapshot.Archetypes.TryGetValue(offer.UnitBlueprintId, out var archetype);
            return new HeadlessRecruitOfferObservation(
                index,
                offer.UnitBlueprintId,
                archetype?.RaceId ?? string.Empty,
                archetype?.ClassId ?? string.Empty,
                archetype?.RoleTag ?? string.Empty,
                offer.FlexActiveId,
                offer.FlexPassiveId,
                offer.Metadata?.GoldCost ?? 0,
                offer.Metadata?.Tier.ToString() ?? string.Empty,
                offer.Metadata?.PlanFit.ToString() ?? string.Empty,
                rosterArchetypes.Contains(offer.UnitBlueprintId));
        }).ToArray();

        var nodesByBoard = snapshot.PassiveNodes.Values
            .Where(node => node != null
                           && !string.IsNullOrWhiteSpace(node.Id)
                           && !string.IsNullOrWhiteSpace(node.BoardId))
            .GroupBy(node => node.BoardId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new HeadlessPassiveBoardObservation(
                group.Key,
                group.OrderBy(node => node.BoardDepth)
                    .ThenBy(node => node.Id, StringComparer.Ordinal)
                    .Select(BuildPassiveNode)
                    .ToArray()))
            .ToArray();
        var loadoutByHero = session.Profile.HeroLoadouts
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.HeroId))
            .GroupBy(value => value.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var passiveHeroes = policyObservation.Roster.Select(hero =>
        {
            loadoutByHero.TryGetValue(hero.HeroId, out var loadout);
            return new HeadlessPassiveHeroObservation(
                hero.HeroId,
                hero.Level,
                loadout?.PassiveBoardId ?? string.Empty,
                StableIds(loadout?.SelectedPassiveNodeIds),
                PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(hero.Level),
                PassiveBoardSelectionValidator.MaxKeystoneCount,
                nodesByBoard);
        }).ToArray();

        var itemBuilder = new SessionInventoryItemBuilder(lookup, GameSessionState.BuildStableSeed);
        var refitItems = (session.Profile.Inventory ?? new List<InventoryItemRecord>())
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemInstanceId))
            .OrderBy(item => item.ItemBaseId, StringComparer.Ordinal)
            .ThenBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .Select(item =>
            {
                var mechanics = H100PolicyObservationBuilder.BuildItemMechanics(
                    item.ItemBaseId,
                    item.ItemInstanceId,
                    item.AffixIds,
                    snapshot);
                var slots = mechanics.Affixes.Select((affix, slotIndex) => new HeadlessRefitSlotObservation(
                    slotIndex,
                    affix,
                    itemBuilder.BuildRefitCandidateAffixIds(item, slotIndex).Count > 0))
                    .ToArray();
                return new HeadlessRefitItemObservation(
                    mechanics.ItemId,
                    mechanics.ItemInstanceId,
                    item.EquippedHeroId,
                    mechanics.Tags,
                    mechanics.WeaponFamilyTag,
                    MetaBalanceDefaults.RefitEchoCost,
                    slots);
            })
            .ToArray();
        var observation = new HeadlessRosterPolicyObservation(
            decisionSeed,
            session.SelectedCampaignChapterId,
            session.SelectedCampaignSiteId,
            MetaBalanceDefaults.TownRosterCap,
            policyObservation.Roster,
            policyObservation.Wallet,
            recruitOffers,
            passiveHeroes,
            refitItems);
        observation = H100RosterPlayerVisibleFactProjector.AttachEvidenceIndex(observation);
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    private static HeadlessPassiveNodeObservation BuildPassiveNode(PassiveNodeTemplate node)
        => new(
            node.Id,
            node.BoardDepth,
            node.NodeKind.ToString(),
            StableIds(node.PrerequisiteNodeIds),
            StableIds(node.MutualExclusionTagIds),
            node.GrantedSkillId,
            StableIds(node.CompileTags),
            H100PolicyObservationBuilder.BuildStatModifiers(node.Package?.Modifiers),
            H100PolicyObservationBuilder.BuildRuleModifiers(node.RulePackage));

    private static string[] StableIds(IEnumerable<string>? ids)
        => (ids ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
}
