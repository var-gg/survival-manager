using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Core.Stats;
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
        var roster = policyObservation.Roster
            .Select(hero => ProjectRosterHealth(session, hero))
            .ToArray();
        var rosterArchetypes = roster.Select(value => value.ArchetypeId)
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
        var passiveHeroes = roster.Select(hero =>
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
                var quote = session.GetRefitQuote(item.ItemInstanceId);
                var currentAffixIds = (item.AffixIds ?? new List<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
                var currentAffixIdSet = currentAffixIds.ToHashSet(StringComparer.Ordinal);
                var magnitudes = (item.AffixMagnitudeRolls ?? new List<InventoryAffixMagnitudeRecord>())
                    .Where(value => value != null && currentAffixIdSet.Contains(value.AffixId))
                    .ToDictionary(value => value.AffixId, value => value.Magnitude, StringComparer.Ordinal);
                var slots = mechanics.Affixes.Select((affix, slotIndex) => new HeadlessRefitSlotObservation(
                    slotIndex,
                    affix,
                    quote.CanPurchase && slotIndex == 0,
                    RefitRollQuality.Measure(
                        snapshot,
                        new[] { affix.AffixId },
                        magnitudes)))
                    .ToArray();
                var allowsSeal = snapshot.ItemCatalog != null
                                 && snapshot.ItemCatalog.TryGetValue(item.ItemBaseId, out var itemTemplate)
                                 && itemTemplate.AllowedCraftOperations is { Count: > 0 }
                                 && itemTemplate.AllowedCraftOperations.Contains(CraftOperationKindValue.Seal);
                var sealCosts = allowsSeal && currentAffixIds.Length > 1
                    ? Enumerable.Range(1, currentAffixIds.Length - 1)
                        .Select(lockedAffixCount =>
                        {
                            var sealQuote = session.GetSealQuote(
                                item.ItemInstanceId,
                                currentAffixIds.Take(lockedAffixCount).ToArray());
                            return sealQuote.CanPurchase
                                ? new HeadlessSealCostObservation(lockedAffixCount, sealQuote.EchoCost)
                                : null;
                        })
                        .Where(value => value != null)
                        .ToArray()
                    : Array.Empty<HeadlessSealCostObservation>();
                return new HeadlessRefitItemObservation(
                    mechanics.ItemId,
                    mechanics.ItemInstanceId,
                    item.EquippedHeroId,
                    mechanics.Tags,
                    mechanics.WeaponFamilyTag,
                    quote.EchoCost,
                    slots,
                    allowsSeal,
                    sealCosts);
            })
            .ToArray();
        var observation = new HeadlessRosterPolicyObservation(
            decisionSeed,
            session.SelectedCampaignChapterId,
            session.SelectedCampaignSiteId,
            MetaBalanceDefaults.TownRosterCap,
            roster,
            policyObservation.Wallet,
            recruitOffers,
            passiveHeroes,
            refitItems);
        observation = H100RosterPlayerVisibleFactProjector.AttachEvidenceIndex(observation);
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    private static HeadlessHeroObservation ProjectRosterHealth(
        GameSessionState session,
        HeadlessHeroObservation hero)
    {
        // HeroInstanceRecord의 0/0은 fresh hero의 "전투 데이터 없음" sentinel이다. 배치 영웅과
        // 실제 battle aftermath가 있는 reserve는 그대로 두고, 아직 싸우지 않은 reserve만 전투와
        // 동일한 개인 loadout stat 경로로 표시용 HP를 보충한다.
        if (hero.IsDeployed || hero.MaxHp > 0)
        {
            return hero;
        }

        var preview = session.TryBuildHeroStatPreview(hero.HeroId);
        var maxHealth = HeroEffectiveStatPreview.Resolve(preview, new[] { StatKey.MaxHealth })
            .FirstOrDefault()?.EffectiveValue ?? 0f;
        if (maxHealth <= 0f || float.IsNaN(maxHealth) || float.IsInfinity(maxHealth))
        {
            return hero;
        }

        var effectiveMaxHp = (int)Math.Max(1, Math.Round(maxHealth));
        return new HeadlessHeroObservation(
            hero.HeroId,
            hero.ArchetypeId,
            hero.RaceId,
            hero.ClassId,
            hero.RoleTag,
            hero.Level,
            effectiveMaxHp,
            effectiveMaxHp,
            hero.EquippedItemCount,
            hero.IsDeployed,
            hero.PreferredAnchor,
            hero.SkillCards,
            hero.FlexActiveSkillId,
            hero.FlexPassiveSkillId,
            hero.EquippedItems,
            hero.SelectedPassiveNodeIds);
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
