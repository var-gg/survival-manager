using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>봉인 observation 한 entry에서 복원한 player-visible id와 commit join table.</summary>
public sealed record SealedObservationJoinView(
    bool Available,
    string FailureReason,
    IReadOnlyList<string> VisibleIds,
    IReadOnlyList<SealedRewardOptionRow> RewardOptions,
    IReadOnlyList<SealedRecruitOfferRow> RecruitOffers,
    IReadOnlyList<SealedPassiveNodeRow> PassiveNodes,
    IReadOnlyList<SealedRefitItemRow> RefitItems);

public sealed record SealedRewardOptionRow(
    int Index,
    string Kind,
    string ItemId,
    string TemporaryAugmentId,
    bool ItemMechanicsPresent,
    bool TemporaryAugmentMechanicsPresent,
    IReadOnlyList<string> FamilyIds);

public sealed record SealedRecruitOfferRow(
    int OfferIndex,
    string ArchetypeId,
    string FlexActiveSkillId,
    string FlexPassiveSkillId,
    IReadOnlyList<string> FamilyIds);

public sealed record SealedPassiveNodeRow(
    string HeroId,
    string BoardId,
    string NodeId,
    string GrantedSkillId,
    IReadOnlyList<string> FamilyIds);

public sealed record SealedRefitSlotRow(int SlotIndex, string CurrentAffixId);

public sealed record SealedRefitItemRow(
    string ItemId,
    string ItemInstanceId,
    IReadOnlyList<string> FamilyIds,
    IReadOnlyList<SealedRefitSlotRow> Slots);

/// <summary>
/// Editor observation writer의 canonical frame을 runtime에서 fail-closed로 읽는 fallback reader.
/// 상위 policy type을 참조하지 않으며 schema marker와 numeric scalar는 id universe에서 제외한다.
/// </summary>
public static class SealedObservationJoinReader
{
    private const string PolicySchemaV1 = "SealedLlmPolicyObservationV1";
    private const string PolicySchemaV2 = "SealedLlmPolicyObservationV2";
    private const string RosterPolicySchemaV1 = "SealedLlmRosterPolicyObservationV1";
    private const string RosterPolicySchemaV2 = "SealedLlmRosterPolicyObservationV2";

    private const string DeploymentSeam = "deployment";
    private const string RewardSeam = "reward";
    private const string PrepSeam = "prep";
    private const string RecruitSeam = "recruit";
    private const string LevelNodeSeam = "level_node";
    private const string RefitSeam = "refit";

    public static SealedObservationJoinView Read(byte[] canonicalBytes, string seamType)
    {
        try
        {
            var collector = new Collector();
            var reader = new SealedCanonicalFrameReader(canonicalBytes, "observation");
            var schema = reader.ReadString("schema");
            switch (schema)
            {
                case PolicySchemaV1 when seamType is DeploymentSeam or RewardSeam:
                    ParsePolicy(reader, collector, hasPrepSurface: false);
                    break;
                case PolicySchemaV2 when seamType is DeploymentSeam or PrepSeam or RewardSeam:
                    ParsePolicy(reader, collector, hasPrepSurface: true);
                    break;
                case RosterPolicySchemaV1 when seamType is RecruitSeam or LevelNodeSeam or RefitSeam:
                    ParseRosterPolicy(reader, collector, hasSealSurface: false);
                    break;
                case RosterPolicySchemaV2 when seamType is RecruitSeam or LevelNodeSeam or RefitSeam:
                    ParseRosterPolicy(reader, collector, hasSealSurface: true);
                    break;
                case PolicySchemaV1:
                case PolicySchemaV2:
                case RosterPolicySchemaV1:
                case RosterPolicySchemaV2:
                    throw new FormatException(
                        $"Observation schema '{schema}' is incompatible with seam '{seamType}'.");
                default:
                    throw new FormatException($"Unknown observation schema '{schema}'.");
            }

            reader.RequireEnd();
            return collector.Build();
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or FormatException
                                          or InvalidOperationException
                                          or ArithmeticException)
        {
            return Unavailable(exception.Message);
        }
    }

    private static SealedObservationJoinView Unavailable(string reason)
        => new(
            false,
            reason ?? string.Empty,
            Array.Empty<string>(),
            Array.Empty<SealedRewardOptionRow>(),
            Array.Empty<SealedRecruitOfferRow>(),
            Array.Empty<SealedPassiveNodeRow>(),
            Array.Empty<SealedRefitItemRow>());

    private static void ParsePolicy(
        SealedCanonicalFrameReader reader,
        Collector collector,
        bool hasPrepSurface)
    {
        reader.ReadInteger("decision_seed");
        reader.ReadInteger("deploy_capacity");
        collector.Visible(reader.ReadString("chapter_id"));
        collector.Visible(reader.ReadString("site_id"));
        ParseObjectList(reader, "roster", bytes => ParseHero(bytes, collector));
        ParseVisibleStrings(reader, "anchors", collector);
        ParseEnemyPreview(reader.ReadBytes("enemy_preview"), collector);
        ParseObjectList(reader, "reward_options", bytes =>
            collector.RewardOptions.Add(ParseRewardOption(bytes, collector)));
        ParseWallet(reader.ReadBytes("wallet"));
        ParseObjectList(reader, "temporary_augments", bytes => ParseAugment(bytes, collector));
        ParseObjectList(reader, "synergy_counts", bytes => ParseSynergyCount(bytes, collector));
        ParseObjectList(reader, "synergy_catalog", bytes => ParseSynergy(bytes, collector));
        if (hasPrepSurface)
        {
            ParseObjectList(reader, "current_placements", bytes => ParsePlacement(bytes, collector));
            ParseObjectList(reader, "owned_items", bytes => ParseOwnedItem(bytes, collector));
        }
        ParseEvidenceMap(reader, collector);
    }

    private static void ParsePlacement(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "placement", "HeadlessPlacementV1");
        collector.Visible(reader.ReadString("anchor"));
        collector.Visible(reader.ReadString("hero_id"));
        reader.RequireEnd();
    }

    private static void ParseOwnedItem(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "owned_item", "HeadlessOwnedItemObservationV1");
        ParseItem(reader.ReadBytes("mechanics"), collector);
        collector.Visible(reader.ReadString("equipped_hero_id"));
        reader.RequireEnd();
    }

    private static void ParseRosterPolicy(
        SealedCanonicalFrameReader reader,
        Collector collector,
        bool hasSealSurface)
    {
        reader.ReadInteger("decision_seed");
        collector.Visible(reader.ReadString("chapter_id"));
        collector.Visible(reader.ReadString("site_id"));
        reader.ReadInteger("roster_capacity");
        ParseObjectList(reader, "roster", bytes => ParseHero(bytes, collector));
        ParseWallet(reader.ReadBytes("wallet"));
        ParseObjectList(reader, "recruit_offers", bytes =>
            collector.RecruitOffers.Add(ParseRecruitOffer(bytes, collector)));
        ParseObjectList(reader, "passive_heroes", bytes => ParsePassiveHero(bytes, collector));
        ParseObjectList(reader, "refit_items", bytes =>
            collector.RefitItems.Add(ParseRefitItem(bytes, collector, hasSealSurface)));
        ParseEvidenceMap(reader, collector);
    }

    private static void ParseHero(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "hero", "HeadlessHeroObservationV1");
        collector.Visible(reader.ReadString("hero_id"));
        collector.Visible(reader.ReadString("archetype_id"));
        collector.Visible(reader.ReadString("race_id"));
        collector.Visible(reader.ReadString("class_id"));
        collector.Visible(reader.ReadString("role_tag"));
        reader.ReadInteger("level");
        reader.ReadInteger("current_hp");
        reader.ReadInteger("max_hp");
        reader.ReadInteger("equipped_item_count");
        reader.ReadBoolean("is_deployed");
        collector.Visible(reader.ReadString("preferred_anchor"));
        ParseObjectList(reader, "skill_cards", item => ParseSkill(item, collector));
        collector.Visible(reader.ReadString("flex_active_skill_id"));
        collector.Visible(reader.ReadString("flex_passive_skill_id"));
        ParseObjectList(reader, "equipped_items", item => ParseItem(item, collector));
        ParseVisibleStrings(reader, "selected_passive_node_ids", collector);
        reader.RequireEnd();
    }

    private static void ParseSkill(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "skill", "HeadlessSkillObservationV1");
        collector.Visible(reader.ReadString("skill_id"));
        collector.Visible(reader.ReadString("kind"));
        collector.Visible(reader.ReadString("slot_kind"));
        reader.ReadSingle("power");
        reader.ReadSingle("range");
        collector.Visible(reader.ReadString("damage_type"));
        reader.ReadSingle("power_flat");
        reader.ReadSingle("physical_coefficient");
        reader.ReadSingle("magical_coefficient");
        reader.ReadSingle("healing_coefficient");
        reader.ReadSingle("health_coefficient");
        reader.ReadSingle("mana_cost");
        reader.ReadSingle("cooldown_seconds");
        reader.ReadSingle("windup_seconds");
        reader.ReadBoolean("can_crit");
        collector.Visible(reader.ReadString("delivery"));
        collector.Visible(reader.ReadString("target_rule"));
        ParseObjectList(reader, "applied_statuses", item => ParseStatusApplication(item, collector));
        reader.RequireEnd();
    }

    private static void ParseStatusApplication(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(
            bytes,
            "status_application",
            "HeadlessStatusApplicationObservationV1");
        collector.Visible(reader.ReadString("application_id"));
        collector.Visible(reader.ReadString("status_id"));
        reader.ReadSingle("duration_seconds");
        reader.ReadSingle("magnitude");
        reader.ReadInteger("max_stacks");
        reader.RequireEnd();
    }

    private static void ParseStatModifier(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "stat_modifier", "HeadlessStatModifierObservationV1");
        collector.Visible(reader.ReadString("stat_id"));
        collector.Visible(reader.ReadString("operation"));
        reader.ReadSingle("value");
        collector.Visible(reader.ReadString("tag_id"));
        reader.RequireEnd();
    }

    private static void ParseRuleModifier(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "rule_modifier", "HeadlessRuleModifierObservationV1");
        collector.Visible(reader.ReadString("kind"));
        collector.Visible(reader.ReadString("value"));
        reader.ReadSingle("magnitude");
        reader.RequireEnd();
    }

    private static void ParseTriggeredEffect(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(
            bytes,
            "triggered_effect",
            "HeadlessTriggeredEffectObservationV1");
        collector.Visible(reader.ReadString("trigger"));
        collector.Visible(reader.ReadString("operation"));
        collector.Visible(reader.ReadString("scope"));
        reader.ReadSingle("magnitude");
        reader.ReadSingle("threshold_ratio");
        collector.Visible(reader.ReadString("status_id"));
        reader.ReadSingle("duration_seconds");
        reader.ReadInteger("max_stacks");
        reader.RequireEnd();
    }

    private static AffixData ParseAffix(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "affix", "HeadlessAffixMechanicsObservationV1");
        var affixId = collector.Visible(reader.ReadString("affix_id"));
        var compileTags = ParseVisibleStrings(reader, "compile_tags", collector);
        ParseVisibleStrings(reader, "required_tags", collector);
        ParseVisibleStrings(reader, "excluded_tags", collector);
        ParseObjectList(reader, "stat_modifiers", item => ParseStatModifier(item, collector));
        ParseObjectList(reader, "rule_modifiers", item => ParseRuleModifier(item, collector));
        reader.RequireEnd();
        return new AffixData(affixId, compileTags);
    }

    private static ItemData ParseItem(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "item", "HeadlessItemMechanicsObservationV1");
        var itemId = collector.Visible(reader.ReadString("item_id"));
        var itemInstanceId = collector.Visible(reader.ReadString("item_instance_id"));
        var tags = ParseVisibleStrings(reader, "tags", collector);
        var weaponFamilyTag = collector.Visible(reader.ReadString("weapon_family_tag"));
        ParseObjectList(reader, "stat_modifiers", item => ParseStatModifier(item, collector));
        var affixes = new List<AffixData>();
        ParseObjectList(reader, "affixes", item => affixes.Add(ParseAffix(item, collector)));
        ParseObjectList(reader, "granted_skills", item => ParseSkill(item, collector));
        reader.RequireEnd();
        return new ItemData(itemId, itemInstanceId, tags, weaponFamilyTag, affixes);
    }

    private static AugmentData ParseAugment(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "augment", "HeadlessAugmentMechanicsObservationV1");
        var augmentId = collector.Visible(reader.ReadString("augment_id"));
        var category = collector.Visible(reader.ReadString("category"));
        var familyId = collector.Visible(reader.ReadString("family_id"));
        reader.ReadInteger("tier");
        var tags = ParseVisibleStrings(reader, "tags", collector);
        var buildBiasTags = ParseVisibleStrings(reader, "build_bias_tags", collector);
        ParseObjectList(reader, "stat_modifiers", item => ParseStatModifier(item, collector));
        ParseObjectList(reader, "rule_modifiers", item => ParseRuleModifier(item, collector));
        ParseObjectList(reader, "triggered_effects", item => ParseTriggeredEffect(item, collector));
        reader.RequireEnd();
        return new AugmentData(augmentId, category, familyId, tags, buildBiasTags);
    }

    private static void ParseWallet(byte[] bytes)
    {
        var reader = ObjectReader(bytes, "wallet", "HeadlessWalletObservationV1");
        reader.ReadInteger("gold");
        reader.ReadInteger("echo");
        reader.RequireEnd();
    }

    private static void ParseSynergyCount(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "synergy_count", "HeadlessSynergyCountObservationV1");
        collector.Visible(reader.ReadString("counted_tag_id"));
        reader.ReadInteger("current_count");
        reader.RequireEnd();
    }

    private static void ParseSynergy(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "synergy", "HeadlessSynergyObservationV1");
        collector.Visible(reader.ReadString("synergy_id"));
        collector.Visible(reader.ReadString("counted_tag_id"));
        ParseObjectList(reader, "tiers", item => ParseSynergyTier(item, collector));
        reader.RequireEnd();
    }

    private static void ParseSynergyTier(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "synergy_tier", "HeadlessSynergyTierObservationV1");
        reader.ReadInteger("threshold");
        ParseObjectList(reader, "stat_modifiers", item => ParseStatModifier(item, collector));
        collector.Visible(reader.ReadString("granted_team_rule_id"));
        reader.RequireEnd();
    }

    private static void ParseEnemyPreview(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "enemy_preview", "HeadlessEnemyPreviewV1");
        reader.ReadBoolean("is_available");
        collector.Visible(reader.ReadString("encounter_id"));
        collector.Visible(reader.ReadString("faction_id"));
        collector.Visible(reader.ReadString("difficulty_band"));
        reader.ReadInteger("threat_skulls");
        ParseObjectList(reader, "units", item => ParseEnemyUnit(item, collector));
        collector.Visible(reader.ReadString("boss_aura_tag"));
        collector.Visible(reader.ReadString("boss_utility_tag"));
        ParseVisibleStrings(reader, "reward_drop_tags", collector);
        reader.RequireEnd();
    }

    private static void ParseEnemyUnit(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "enemy_unit", "HeadlessEnemyUnitPreviewV1");
        collector.Visible(reader.ReadString("archetype_id"));
        collector.Visible(reader.ReadString("race_id"));
        collector.Visible(reader.ReadString("class_id"));
        collector.Visible(reader.ReadString("role_tag"));
        collector.Visible(reader.ReadString("preferred_anchor"));
        reader.RequireEnd();
    }

    private static SealedRewardOptionRow ParseRewardOption(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "reward_option", "HeadlessRewardOptionV1");
        var index = reader.ReadInteger("index");
        var kind = collector.Visible(reader.ReadString("kind"));
        collector.Visible(reader.ReadString("payload_id"));
        reader.ReadInteger("gold_amount");
        reader.ReadInteger("echo_amount");
        reader.ReadInteger("permanent_slot_amount");
        var mechanics = ParseRewardMechanics(reader.ReadBytes("mechanics"), collector);
        reader.RequireEnd();

        var itemFamily = mechanics.Item == null
            ? Array.Empty<string>()
            : mechanics.Item.Tags
                .Concat(new[] { mechanics.Item.WeaponFamilyTag })
                .Concat(mechanics.Item.Affixes.SelectMany(value => value.CompileTags));
        var augmentFamily = mechanics.Augment == null
            ? Array.Empty<string>()
            : mechanics.Augment.Tags
                .Concat(mechanics.Augment.BuildBiasTags)
                .Concat(new[] { mechanics.Augment.FamilyId, mechanics.Augment.Category });
        var family = string.Equals(kind, "Item", StringComparison.Ordinal)
            ? FamilyIds(itemFamily)
            : string.Equals(kind, "TemporaryAugment", StringComparison.Ordinal)
                ? FamilyIds(augmentFamily)
                : Array.Empty<string>();

        return new SealedRewardOptionRow(
            index,
            kind,
            mechanics.Item?.ItemId ?? string.Empty,
            mechanics.Augment?.AugmentId ?? string.Empty,
            mechanics.Item != null,
            mechanics.Augment != null,
            family);
    }

    private static RewardMechanicsData ParseRewardMechanics(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(
            bytes,
            "reward_mechanics",
            "HeadlessRewardMechanicsObservationV1");
        ItemData item = null;
        if (reader.ReadBoolean("item.present"))
        {
            item = ParseItem(reader.ReadBytes("item"), collector);
        }

        AugmentData augment = null;
        if (reader.ReadBoolean("temporary_augment.present"))
        {
            augment = ParseAugment(reader.ReadBytes("temporary_augment"), collector);
        }

        reader.RequireEnd();
        return new RewardMechanicsData(item, augment);
    }

    private static SealedRecruitOfferRow ParseRecruitOffer(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "recruit_offer", "HeadlessRecruitOfferObservationV1");
        var offerIndex = reader.ReadInteger("offer_index");
        var archetypeId = collector.Visible(reader.ReadString("archetype_id"));
        var raceId = collector.Visible(reader.ReadString("race_id"));
        var classId = collector.Visible(reader.ReadString("class_id"));
        var roleTag = collector.Visible(reader.ReadString("role_tag"));
        var flexActive = collector.Visible(reader.ReadString("flex_active_skill_id"));
        var flexPassive = collector.Visible(reader.ReadString("flex_passive_skill_id"));
        reader.ReadInteger("gold_cost");
        var tier = collector.Visible(reader.ReadString("tier"));
        collector.Visible(reader.ReadString("plan_fit"));
        reader.ReadBoolean("is_duplicate");
        reader.RequireEnd();
        return new SealedRecruitOfferRow(
            offerIndex,
            archetypeId,
            flexActive,
            flexPassive,
            FamilyIds(new[] { raceId, classId, roleTag, tier }));
    }

    private static void ParsePassiveHero(byte[] bytes, Collector collector)
    {
        var reader = ObjectReader(bytes, "passive_hero", "HeadlessPassiveHeroObservationV1");
        var heroId = collector.Visible(reader.ReadString("hero_id"));
        reader.ReadInteger("level");
        collector.Visible(reader.ReadString("selected_board_id"));
        ParseVisibleStrings(reader, "selected_node_ids", collector);
        reader.ReadInteger("max_active_node_count");
        reader.ReadInteger("max_keystone_count");
        ParseObjectList(reader, "boards", item => ParsePassiveBoard(item, heroId, collector));
        reader.RequireEnd();
    }

    private static void ParsePassiveBoard(byte[] bytes, string heroId, Collector collector)
    {
        var reader = ObjectReader(bytes, "passive_board", "HeadlessPassiveBoardObservationV1");
        var boardId = collector.Visible(reader.ReadString("board_id"));
        ParseObjectList(reader, "nodes", item =>
            collector.PassiveNodes.Add(ParsePassiveNode(item, heroId, boardId, collector)));
        reader.RequireEnd();
    }

    private static SealedPassiveNodeRow ParsePassiveNode(
        byte[] bytes,
        string heroId,
        string boardId,
        Collector collector)
    {
        var reader = ObjectReader(bytes, "passive_node", "HeadlessPassiveNodeObservationV1");
        var nodeId = collector.Visible(reader.ReadString("node_id"));
        reader.ReadInteger("board_depth");
        var nodeKind = collector.Visible(reader.ReadString("node_kind"));
        ParseVisibleStrings(reader, "prerequisite_node_ids", collector);
        ParseVisibleStrings(reader, "mutual_exclusion_tag_ids", collector);
        var grantedSkillId = collector.Visible(reader.ReadString("granted_skill_id"));
        var compileTags = ParseVisibleStrings(reader, "compile_tags", collector);
        ParseObjectList(reader, "stat_modifiers", item => ParseStatModifier(item, collector));
        ParseObjectList(reader, "rule_modifiers", item => ParseRuleModifier(item, collector));
        reader.RequireEnd();
        return new SealedPassiveNodeRow(
            heroId,
            boardId,
            nodeId,
            grantedSkillId,
            FamilyIds(compileTags.Concat(new[] { nodeKind })));
    }

    private static SealedRefitItemRow ParseRefitItem(
        byte[] bytes,
        Collector collector,
        bool hasSealSurface)
    {
        var reader = ObjectReader(
            bytes,
            "refit_item",
            hasSealSurface
                ? "HeadlessRefitItemObservationV2"
                : "HeadlessRefitItemObservationV1");
        var itemId = collector.Visible(reader.ReadString("item_id"));
        var itemInstanceId = collector.Visible(reader.ReadString("item_instance_id"));
        collector.Visible(reader.ReadString("equipped_hero_id"));
        var tags = ParseVisibleStrings(reader, "tags", collector);
        var weaponFamilyTag = collector.Visible(reader.ReadString("weapon_family_tag"));
        reader.ReadInteger("echo_cost");
        if (hasSealSurface)
        {
            reader.ReadBoolean("allows_seal");
            ParseObjectList(reader, "seal_costs", ParseSealCost);
        }

        var slots = new List<SealedRefitSlotRow>();
        ParseObjectList(reader, "affix_slots", item =>
            slots.Add(ParseRefitSlot(item, collector, hasSealSurface)));
        reader.RequireEnd();
        return new SealedRefitItemRow(
            itemId,
            itemInstanceId,
            FamilyIds(tags.Concat(new[] { weaponFamilyTag })),
            slots);
    }

    private static void ParseSealCost(byte[] bytes)
    {
        var reader = ObjectReader(
            bytes,
            "seal_cost",
            "HeadlessSealCostObservationV1");
        reader.ReadInteger("locked_affix_count");
        reader.ReadInteger("echo_cost");
        reader.RequireEnd();
    }

    private static SealedRefitSlotRow ParseRefitSlot(
        byte[] bytes,
        Collector collector,
        bool hasSealSurface)
    {
        var reader = ObjectReader(
            bytes,
            "refit_slot",
            hasSealSurface
                ? "HeadlessRefitSlotObservationV2"
                : "HeadlessRefitSlotObservationV1");
        var slotIndex = reader.ReadInteger("slot_index");
        var currentAffixId = string.Empty;
        if (reader.ReadBoolean("current_affix.present"))
        {
            currentAffixId = ParseAffix(reader.ReadBytes("current_affix"), collector).AffixId;
        }

        reader.ReadBoolean("can_refit");
        if (hasSealSurface)
        {
            reader.ReadDouble("roll_quality");
        }

        reader.RequireEnd();
        return new SealedRefitSlotRow(slotIndex, currentAffixId);
    }

    private static SealedCanonicalFrameReader ObjectReader(
        byte[] bytes,
        string scope,
        string schema)
    {
        var reader = new SealedCanonicalFrameReader(bytes, scope);
        reader.RequireSchema(schema);
        return reader;
    }

    private static void ParseObjectList(
        SealedCanonicalFrameReader reader,
        string field,
        Action<byte[]> parse)
    {
        var count = reader.ReadCount($"{field}.count");
        for (var index = 0; index < count; index++)
        {
            parse(reader.ReadBytes($"{field}[{index}]"));
        }
    }

    private static IReadOnlyList<string> ParseVisibleStrings(
        SealedCanonicalFrameReader reader,
        string field,
        Collector collector)
    {
        var count = reader.ReadCount($"{field}.count");
        var result = new string[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = collector.Visible(reader.ReadString($"{field}[{index}]"));
        }

        return result;
    }

    private static void ParseEvidenceMap(SealedCanonicalFrameReader reader, Collector collector)
    {
        var count = reader.ReadCount("evidence.count");
        for (var index = 0; index < count; index++)
        {
            collector.Visible(reader.ReadString($"evidence[{index}].key"));
            collector.Visible(reader.ReadString($"evidence[{index}].value"));
        }
    }

    private static IReadOnlyList<string> FamilyIds(IEnumerable<string> values)
        => (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private sealed class Collector
    {
        private readonly HashSet<string> _visibleIds = new(StringComparer.Ordinal);

        public List<SealedRewardOptionRow> RewardOptions { get; } = new();
        public List<SealedRecruitOfferRow> RecruitOffers { get; } = new();
        public List<SealedPassiveNodeRow> PassiveNodes { get; } = new();
        public List<SealedRefitItemRow> RefitItems { get; } = new();

        public string Visible(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _visibleIds.Add(value);
            }

            return value;
        }

        public SealedObservationJoinView Build()
            => new(
                true,
                string.Empty,
                _visibleIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                RewardOptions.ToArray(),
                RecruitOffers.ToArray(),
                PassiveNodes.ToArray(),
                RefitItems.ToArray());
    }

    private sealed record AffixData(string AffixId, IReadOnlyList<string> CompileTags);

    private sealed record ItemData(
        string ItemId,
        string ItemInstanceId,
        IReadOnlyList<string> Tags,
        string WeaponFamilyTag,
        IReadOnlyList<AffixData> Affixes);

    private sealed record AugmentData(
        string AugmentId,
        string Category,
        string FamilyId,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> BuildBiasTags);

    private sealed record RewardMechanicsData(ItemData Item, AugmentData Augment);
}
