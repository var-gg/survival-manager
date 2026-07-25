using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SM.HeadlessMetrics;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>
/// H100 decision 전후의 deterministic session truth를 ordinal, length-prefixed bytes로 봉인한다.
/// UI 표시 상태, object identity, timestamp는 포함하지 않는다.
/// </summary>
internal static class H100SessionStateCanonicalHash
{
    public const string SchemaVersion = "H100SessionStateCanonicalHashV2";

    public static string Compute(GameSessionState session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        var profile = session.Profile ?? throw new InvalidOperationException("H100 session has no bound profile.");
        var progress = profile.CampaignProgress ?? new CampaignProgressRecord();
        var currencies = profile.Currencies ?? new CurrencyRecord();
        using var payload = new MemoryStream();

        Append(payload, "schema", SchemaVersion);
        Append(payload, "campaign.selected_chapter_id", progress.SelectedChapterId);
        Append(payload, "campaign.selected_site_id", progress.SelectedSiteId);
        AppendOrdinalCollection(payload, "campaign.cleared_chapter_ids", progress.ClearedChapterIds);
        AppendOrdinalCollection(payload, "campaign.cleared_site_ids", progress.ClearedSiteIds);
        Append(payload, "campaign.story_cleared", progress.StoryCleared);
        Append(payload, "campaign.endless_unlocked", progress.EndlessUnlocked);

        Append(payload, "currencies.gold", currencies.Gold);
        Append(payload, "currencies.echo", currencies.Echo);

        AppendRoster(payload, profile);
        AppendInventory(payload, profile.Inventory);
        AppendOrdinalCollection(payload, "expedition.squad_hero_ids", session.ExpeditionSquadHeroIds);

        var assignments = (session.DeploymentAssignments ??
                           new Dictionary<SM.Combat.Model.DeploymentAnchorId, string?>())
            .Select(pair => $"{pair.Key}={pair.Value ?? string.Empty}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        AppendOrdinalCollection(payload, "deployment.assignments", assignments);
        Append(payload, "deployment.team_posture", session.SelectedTeamPosture.ToString());
        Append(payload, "deployment.team_tactic_id", session.SelectedTeamTacticId);

        Append(payload, "expedition.current_node_index", session.CurrentExpeditionNodeIndex);
        Append(payload, "expedition.selected_node_index", session.SelectedExpeditionNodeIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        AppendOrdinalCollection(payload, "expedition.temporary_augment_ids", session.Expedition?.TemporaryAugmentIds);
        Append(payload, "reward.pending_settlement", session.HasPendingRewardSettlement);
        Append(payload, "permanent_augment.slot_count", session.PermanentAugmentSlotCount);
        AppendOrdinalCollection(payload, "permanent_augment.unlocked_ids", profile.UnlockedPermanentAugmentIds);
        AppendPermanentAugmentLoadouts(payload, profile.PermanentAugmentLoadouts);

        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }

    private static void AppendRoster(Stream payload, SaveProfile profile)
    {
        var heroes = (profile.Heroes ?? new List<HeroInstanceRecord>())
            .Where(hero => hero != null)
            .OrderBy(hero => hero.HeroId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(hero => hero.ArchetypeId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(hero => hero.CharacterId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        Append(payload, "roster.count", heroes.Length);

        foreach (var hero in heroes)
        {
            Append(payload, "roster.hero.id", hero.HeroId);
            Append(payload, "roster.hero.character_id", hero.CharacterId);
            Append(payload, "roster.hero.archetype_id", hero.ArchetypeId);
            Append(payload, "roster.hero.race_id", hero.RaceId);
            Append(payload, "roster.hero.class_id", hero.ClassId);
            Append(payload, "roster.hero.positive_trait_id", hero.PositiveTraitId);
            Append(payload, "roster.hero.negative_trait_id", hero.NegativeTraitId);
            Append(payload, "roster.hero.flex_active_id", hero.FlexActiveId);
            Append(payload, "roster.hero.flex_passive_id", hero.FlexPassiveId);
            Append(payload, "roster.hero.recruit_tier", hero.RecruitTier.ToString());
            Append(payload, "roster.hero.recruit_source", hero.RecruitSource.ToString());
            Append(payload, "roster.hero.current_hp", hero.CurrentHp);
            Append(payload, "roster.hero.max_hp", hero.MaxHp);

            var progressions = (profile.HeroProgressions ?? new List<HeroProgressionRecord>())
                .Where(value => value != null && string.Equals(value.HeroId, hero.HeroId, StringComparison.Ordinal))
                .OrderBy(value => value.Level)
                .ThenBy(value => value.Experience)
                .ToArray();
            Append(payload, "roster.hero.progression_count", progressions.Length);
            Append(payload, "roster.hero.level", progressions.FirstOrDefault()?.Level ?? 1);

            var loadouts = (profile.HeroLoadouts ?? new List<HeroLoadoutRecord>())
                .Where(value => value != null && string.Equals(value.HeroId, hero.HeroId, StringComparison.Ordinal))
                .OrderBy(value => value.PassiveBoardId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => JoinOrdinal(value.SelectedPassiveNodeIds), StringComparer.Ordinal)
                .ToArray();
            AppendOrdinalCollection(
                payload,
                "roster.hero.passive_board_ids",
                loadouts.Select(value => value.PassiveBoardId));
            AppendOrdinalCollection(
                payload,
                "roster.hero.selected_passive_node_ids",
                loadouts.SelectMany(value => value.SelectedPassiveNodeIds ?? new List<string>())
                    .Concat((profile.PassiveSelections ?? new List<PassiveSelectionRecord>())
                        .Where(value => value != null && string.Equals(value.HeroId, hero.HeroId, StringComparison.Ordinal))
                        .SelectMany(value => value.SelectedNodeIds ?? new List<string>())));

            var equippedItemIds = (hero.EquippedItemIds ?? new List<string>())
                .Concat(loadouts.SelectMany(value => value.EquippedItemInstanceIds ?? new List<string>()))
                .Concat((profile.Inventory ?? new List<InventoryItemRecord>())
                    .Where(value => value != null && string.Equals(value.EquippedHeroId, hero.HeroId, StringComparison.Ordinal))
                    .Select(value => value.ItemInstanceId))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Append(payload, "roster.hero.equipped_item_count", equippedItemIds.Length);
            foreach (var itemInstanceId in equippedItemIds)
            {
                var item = (profile.Inventory ?? new List<InventoryItemRecord>())
                    .Where(value => value != null && string.Equals(value.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                    .OrderBy(value => value.ItemBaseId ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(value => value.EquippedHeroId ?? string.Empty, StringComparer.Ordinal)
                    .FirstOrDefault();
                Append(payload, "roster.hero.equipped_item.instance_id", itemInstanceId);
                Append(payload, "roster.hero.equipped_item.base_id", item?.ItemBaseId);
                AppendAffixes(payload, "roster.hero.equipped_item.affixes", item?.AffixIds);
                AppendAffixMagnitudes(payload, "roster.hero.equipped_item.affix_magnitudes", item?.AffixMagnitudeRolls);
            }
        }
    }

    private static void AppendInventory(Stream payload, IEnumerable<InventoryItemRecord>? inventory)
    {
        var items = (inventory ?? Array.Empty<InventoryItemRecord>())
            .Where(item => item != null)
            .OrderBy(item => item.ItemInstanceId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.ItemBaseId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.EquippedHeroId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        Append(payload, "inventory.count", items.Length);
        foreach (var item in items)
        {
            Append(payload, "inventory.item.instance_id", item.ItemInstanceId);
            Append(payload, "inventory.item.base_id", item.ItemBaseId);
            Append(payload, "inventory.item.equipped_hero_id", item.EquippedHeroId);
            AppendAffixes(payload, "inventory.item.affixes", item.AffixIds);
            AppendAffixMagnitudes(payload, "inventory.item.affix_magnitudes", item.AffixMagnitudeRolls);
        }
    }

    private static void AppendPermanentAugmentLoadouts(
        Stream payload,
        IEnumerable<PermanentAugmentLoadoutRecord>? loadouts)
    {
        var values = (loadouts ?? Array.Empty<PermanentAugmentLoadoutRecord>())
            .Where(value => value != null)
            .OrderBy(value => value.BlueprintId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(value => JoinOrdinal(value.EquippedAugmentIds), StringComparer.Ordinal)
            .ToArray();
        Append(payload, "permanent_augment.loadout_count", values.Length);
        foreach (var value in values)
        {
            Append(payload, "permanent_augment.loadout.blueprint_id", value.BlueprintId);
            AppendOrdinalCollection(payload, "permanent_augment.loadout.equipped_ids", value.EquippedAugmentIds);
        }
    }

    private static void AppendAffixes(Stream payload, string name, IReadOnlyList<string>? affixIds)
    {
        var indexed = (affixIds ?? Array.Empty<string>())
            .Select((affixId, index) => $"{index.ToString("D8", CultureInfo.InvariantCulture)}={affixId ?? string.Empty}");
        AppendOrdinalCollection(payload, name, indexed);
    }

    private static void AppendAffixMagnitudes(
        Stream payload,
        string name,
        IEnumerable<InventoryAffixMagnitudeRecord>? magnitudes)
    {
        var canonical = (magnitudes ?? Array.Empty<InventoryAffixMagnitudeRecord>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.AffixId))
            .OrderBy(value => value.AffixId, StringComparer.Ordinal)
            .Select(value =>
                $"{value.AffixId}={unchecked((uint)BitConverter.SingleToInt32Bits(value.Magnitude)).ToString("X8", CultureInfo.InvariantCulture)}");
        AppendOrdinalCollection(payload, name, canonical);
    }

    private static string JoinOrdinal(IEnumerable<string>? values)
        => string.Join("|", (values ?? Array.Empty<string>())
            .OrderBy(value => value ?? string.Empty, StringComparer.Ordinal));

    private static void AppendOrdinalCollection(
        Stream payload,
        string name,
        IEnumerable<string>? values)
    {
        var ordered = (values ?? Array.Empty<string>())
            .Select(value => value ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Append(payload, $"{name}.count", ordered.Length);
        foreach (var value in ordered)
        {
            Append(payload, $"{name}.item", value);
        }
    }

    private static void Append(Stream payload, string name, string? value)
    {
        LengthPrefixedStableHash.AppendPart(payload, name);
        LengthPrefixedStableHash.AppendPart(payload, value ?? string.Empty);
    }

    private static void Append(Stream payload, string name, int value)
        => Append(payload, name, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(Stream payload, string name, bool value)
        => Append(payload, name, value ? "true" : "false");
}
