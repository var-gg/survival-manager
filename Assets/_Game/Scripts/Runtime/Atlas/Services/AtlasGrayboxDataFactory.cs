using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasGrayboxDataFactory
{
    public static AtlasRegionDefinition CreateRegion()
    {
        return CreateThirtySevenHexRegion();
    }

    public static AtlasRegionDefinition CreateThirtySevenHexRegion()
    {
        var nodes = new List<AtlasRegionNode>
        {
            Node("hex_0_m3", 0, -3, AtlasNodeKind.Skirmish, "North Watch Skirmish", "wolf scout trace", "fang", "peel_anti_dive", layer: AtlasRegionLayer.Outer),
            Node("hex_1_m3", 1, -3, AtlasNodeKind.SigilAnchor, "Northwest Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_2_m3", 2, -3, AtlasNodeKind.Skirmish, "Needle Pass", "beast skirmish / weakside dive", "beast trophy", "peel_anti_dive", 1, AtlasRegionLayer.Outer),
            Node("hex_3_m3", 3, -3, AtlasNodeKind.SigilAnchor, "North Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),

            Node("hex_m1_m2", -1, -2, AtlasNodeKind.Cache, "Frost Cache", "low pressure path", "standard cache", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_0_m2", 0, -2, AtlasNodeKind.SigilAnchor, "Middle Pressure Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Middle),
            Node("hex_1_m2", 1, -2, AtlasNodeKind.Echo, "Buried Oath", "event clue / oath memory", "bond token", "route_read", layer: AtlasRegionLayer.Middle),
            Node("hex_2_m2", 2, -2, AtlasNodeKind.SigilAnchor, "Relic Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Middle),
            Node("hex_3_m2", 3, -2, AtlasNodeKind.Elite, "Ridge Ambush", "beast skirmish / flank pressure", "fang", "guard_anchor", 1, AtlasRegionLayer.Outer),

            Node("hex_m2_m1", -2, -1, AtlasNodeKind.ScoutVantage, "High Scout Perch", "distant warning", "scout report", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_m1_m1", -1, -1, AtlasNodeKind.Echo, "Broken Trail", "beast patrol", "ore", "break_formation", layer: AtlasRegionLayer.Middle),
            Node("hex_0_m1", 0, -1, AtlasNodeKind.Cache, "Dry Wash", "safe scout line", "standard cache", "route_read", layer: AtlasRegionLayer.Inner),
            Node("hex_1_m1", 1, -1, AtlasNodeKind.SigilAnchor, "Inner Evidence Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Inner),
            Node("hex_2_m1", 2, -1, AtlasNodeKind.Cache, "Relic Cache", "reward cache / no combat", "relic metal", "guard_anchor", layer: AtlasRegionLayer.Middle),
            Node("hex_3_m1", 3, -1, AtlasNodeKind.ScoutVantage, "East Watch Stone", "distant warning", "scout report", "route_read", layer: AtlasRegionLayer.Outer),

            Node("hex_m3_0", -3, 0, AtlasNodeKind.SigilAnchor, "West Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_m2_0", -2, 0, AtlasNodeKind.SigilAnchor, "Oath Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Middle),
            Node("hex_m1_0", -1, 0, AtlasNodeKind.Elite, "Glass Elk Elite", "elite beast / guard break", "elite relic", "break_formation", 2, AtlasRegionLayer.Inner),
            Node("hex_0_0", 0, 0, AtlasNodeKind.Boss, "Ashen Gate Boss", "boss / marked cleave pressure", "boss dossier", "guard_anchor", 3, AtlasRegionLayer.Core),
            Node("hex_1_0", 1, 0, AtlasNodeKind.Extract, "Dawn Extract", "extract after boss", "region pin", "route_read", 4, AtlasRegionLayer.Inner),
            Node("hex_2_0", 2, 0, AtlasNodeKind.Echo, "Fen Edge", "swarm signs", "herb", "cleanse_mark", layer: AtlasRegionLayer.Middle),
            Node("hex_3_0", 3, 0, AtlasNodeKind.SigilAnchor, "East Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),

            Node("hex_m3_1", -3, 1, AtlasNodeKind.Skirmish, "Fang Skirmish", "beast skirmish / weakside dive", "beast trophy", "peel_anti_dive", 0, AtlasRegionLayer.Outer),
            Node("hex_m2_1", -2, 1, AtlasNodeKind.Echo, "Old Road", "standard patrol", "coin", "peel_anti_dive", layer: AtlasRegionLayer.Middle),
            Node("hex_m1_1", -1, 1, AtlasNodeKind.SigilAnchor, "Inner Oracle Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Inner),
            Node("hex_0_1", 0, 1, AtlasNodeKind.Elite, "Cairn Step", "ruin ward", "relic", "break_formation", 2, AtlasRegionLayer.Inner),
            Node("hex_1_1", 1, 1, AtlasNodeKind.Cache, "Moss Shelf", "low pressure path", "herb", "guard_anchor", layer: AtlasRegionLayer.Middle),
            Node("hex_2_1", 2, 1, AtlasNodeKind.ScoutVantage, "South Spur", "standard patrol", "coin", "cleanse_mark", layer: AtlasRegionLayer.Outer),

            Node("hex_m3_2", -3, 2, AtlasNodeKind.SigilAnchor, "Southwest Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_m2_2", -2, 2, AtlasNodeKind.Skirmish, "Moss Shelf Skirmish", "beast patrol", "herb", "guard_anchor", 1, AtlasRegionLayer.Middle),
            Node("hex_m1_2", -1, 2, AtlasNodeKind.Cache, "Fen Cache", "reward cache / no combat", "herb", "cleanse_mark", layer: AtlasRegionLayer.Middle),
            Node("hex_0_2", 0, 2, AtlasNodeKind.SigilAnchor, "Gate Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Middle),
            Node("hex_1_2", 1, 2, AtlasNodeKind.Echo, "Dawn Echo", "event clue / oath memory", "bond token", "route_read", layer: AtlasRegionLayer.Outer),

            Node("hex_m3_3", -3, 3, AtlasNodeKind.SigilAnchor, "South Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_m2_3", -2, 3, AtlasNodeKind.Cache, "South Cache", "low pressure path", "standard cache", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_m1_3", -1, 3, AtlasNodeKind.ScoutVantage, "Distant Watch Stone", "distant warning", "scout report", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_0_3", 0, 3, AtlasNodeKind.SigilAnchor, "Southeast Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),
        };

        var sigils = CreateSigils();
        var roster = CreateRoster();
        var stageCandidates = new[]
        {
            new AtlasStageCandidate(1, "1A", "hex_m3_1", Array.Empty<string>()),
            new AtlasStageCandidate(1, "1B", "hex_0_m3", Array.Empty<string>()),
            new AtlasStageCandidate(2, "2A", "hex_m2_2", new[] { "hex_m3_1", "hex_0_m3" }),
            new AtlasStageCandidate(2, "2B", "hex_3_m2", new[] { "hex_m3_1", "hex_0_m3" }),
            new AtlasStageCandidate(3, "3A", "hex_m1_0", new[] { "hex_m2_2", "hex_3_m2" }),
            new AtlasStageCandidate(3, "3B", "hex_0_1", new[] { "hex_m2_2", "hex_3_m2" }),
            new AtlasStageCandidate(4, "보스", "hex_0_0", new[] { "hex_m1_0", "hex_0_1" }),
            new AtlasStageCandidate(5, "추출", "hex_1_0", new[] { "hex_0_0" }),
        };

        var anchorSlots = new[]
        {
            Anchor("anchor_outer_northwest", "hex_1_m3", AtlasRegionLayer.Outer, "stage_1_2", "Approach", AtlasHexDirection.SouthEast, "hex_0_m3", "hex_m2_2"),
            Anchor("anchor_outer_north", "hex_3_m3", AtlasRegionLayer.Outer, "stage_1_2", "Approach", AtlasHexDirection.SouthWest, "hex_0_m3", "hex_3_m2"),
            Anchor("anchor_outer_east", "hex_3_0", AtlasRegionLayer.Outer, "stage_2_3", "Pressure", AtlasHexDirection.West, "hex_3_m2", "hex_0_1"),
            Anchor("anchor_outer_southeast", "hex_0_3", AtlasRegionLayer.Outer, "stage_2_3", "Pressure", AtlasHexDirection.NorthWest, "hex_m2_2", "hex_0_1"),
            Anchor("anchor_outer_south", "hex_m3_3", AtlasRegionLayer.Outer, "stage_1_2", "Approach", AtlasHexDirection.NorthEast, "hex_m3_1", "hex_m2_2"),
            Anchor("anchor_outer_west", "hex_m3_0", AtlasRegionLayer.Outer, "stage_1_2", "Approach", AtlasHexDirection.East, "hex_m3_1", "hex_m2_2"),
            Anchor("anchor_middle_pressure", "hex_0_m2", AtlasRegionLayer.Middle, "stage_2_3", "Pressure", AtlasHexDirection.SouthEast, "hex_m2_2", "hex_3_m2", "hex_m1_0"),
            Anchor("anchor_middle_relic", "hex_2_m2", AtlasRegionLayer.Middle, "stage_2_3", "Pressure", AtlasHexDirection.SouthWest, "hex_3_m2", "hex_0_1"),
            Anchor("anchor_middle_oath", "hex_m2_0", AtlasRegionLayer.Middle, "stage_2_3", "Evidence", AtlasHexDirection.East, "hex_m2_2", "hex_m1_0"),
            Anchor("anchor_middle_gate", "hex_0_2", AtlasRegionLayer.Middle, "stage_3_boss", "Evidence", AtlasHexDirection.NorthWest, "hex_0_1", "hex_0_0"),
            Anchor("anchor_inner_evidence", "hex_1_m1", AtlasRegionLayer.Inner, "stage_3_boss", "Evidence", AtlasHexDirection.West, "hex_m1_0", "hex_0_1", "hex_0_0"),
            Anchor("anchor_inner_oracle", "hex_m1_1", AtlasRegionLayer.Inner, "stage_3_boss", "Evidence", AtlasHexDirection.East, "hex_m1_0", "hex_0_1", "hex_0_0"),
        };

        var region = new AtlasRegionDefinition(
            "region_wolfpine_graybox",
            "Wolfpine Sigil Graybox",
            nodes,
            BuildSigilAnchorCoordinates(nodes, anchorSlots),
            sigils,
            roster,
            stageCandidates,
            anchorSlots,
            "anchor_slots_v3_37hex",
            "footprint_profiles_v2_shape_category");

        ValidateThirtySevenRegion(region);
        return region;
    }

    public static AtlasRegionDefinition CreateLegacyNineteenHexRegion()
    {
        var nodes = new List<AtlasRegionNode>
        {
            Node("hex_m2_0", -2, 0, AtlasNodeKind.Skirmish, "North Spur", "wolf scout trace", "fang", "peel_anti_dive", 0, AtlasRegionLayer.Outer),
            Node("hex_m2_1", -2, 1, AtlasNodeKind.Skirmish, "Fang Skirmish", "beast skirmish / weakside dive", "beast trophy", "peel_anti_dive", 0, AtlasRegionLayer.Outer),
            Node("hex_m2_2", -2, 2, AtlasNodeKind.Cache, "Moss Shelf", "low pressure path", "herb", "guard_anchor", layer: AtlasRegionLayer.Outer),
            Node("hex_m1_m1", -1, -1, AtlasNodeKind.SigilAnchor, "West Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Middle),
            Node("hex_m1_0", -1, 0, AtlasNodeKind.Echo, "Broken Trail", "beast patrol", "ore", "break_formation", layer: AtlasRegionLayer.Inner),
            Node("hex_m1_1", -1, 1, AtlasNodeKind.Echo, "Buried Oath", "event clue / oath memory", "bond token", "route_read", layer: AtlasRegionLayer.Inner),
            Node("hex_m1_2", -1, 2, AtlasNodeKind.Skirmish, "Ridge Ambush", "beast skirmish / flank pressure", "fang", "guard_anchor", 1, AtlasRegionLayer.Middle),
            Node("hex_0_m2", 0, -2, AtlasNodeKind.Cache, "Dry Wash", "safe scout line", "standard cache", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_0_m1", 0, -1, AtlasNodeKind.Skirmish, "Relic Cache", "reward cache / no combat", "relic metal", "guard_anchor", 1, AtlasRegionLayer.Inner),
            Node("hex_0_0", 0, 0, AtlasNodeKind.Elite, "Glass Elk Elite", "elite beast / guard break", "elite relic", "break_formation", 2, AtlasRegionLayer.Core),
            Node("hex_0_1", 0, 1, AtlasNodeKind.SigilAnchor, "Center Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Inner),
            Node("hex_0_2", 0, 2, AtlasNodeKind.Echo, "Fen Edge", "swarm signs", "herb", "cleanse_mark", layer: AtlasRegionLayer.Outer),
            Node("hex_1_m2", 1, -2, AtlasNodeKind.SigilAnchor, "East Sigil Anchor", "anchor slot", "sigil", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_1_m1", 1, -1, AtlasNodeKind.Elite, "Cairn Step", "ruin ward", "relic", "break_formation", 2, AtlasRegionLayer.Inner),
            Node("hex_1_0", 1, 0, AtlasNodeKind.Boss, "Ashen Gate Boss", "boss / marked cleave pressure", "boss dossier", "guard_anchor", 3, AtlasRegionLayer.Inner),
            Node("hex_1_1", 1, 1, AtlasNodeKind.ScoutVantage, "Old Road", "standard patrol", "coin", "peel_anti_dive", layer: AtlasRegionLayer.Middle),
            Node("hex_2_m2", 2, -2, AtlasNodeKind.ScoutVantage, "Watch Stone", "distant warning", "scout report", "route_read", layer: AtlasRegionLayer.Outer),
            Node("hex_2_m1", 2, -1, AtlasNodeKind.Extract, "Dawn Extract", "extract after boss", "region pin", "route_read", 4, AtlasRegionLayer.Outer),
            Node("hex_2_0", 2, 0, AtlasNodeKind.Cache, "South Spur", "low pressure path", "standard cache", "cleanse_mark", layer: AtlasRegionLayer.Outer),
        };

        var anchorSlots = new[]
        {
            Anchor("anchor_outer_approach", "hex_m1_m1", AtlasRegionLayer.Outer, "stage_1_2", "Approach", AtlasHexDirection.SouthEast, "hex_m2_1", "hex_m2_0", "hex_m1_2"),
            Anchor("anchor_middle_pressure", "hex_0_1", AtlasRegionLayer.Middle, "stage_2_3", "Pressure", AtlasHexDirection.NorthEast, "hex_m1_2", "hex_0_m1", "hex_0_0"),
            Anchor("anchor_inner_evidence", "hex_1_m2", AtlasRegionLayer.Inner, "stage_3_boss", "Evidence", AtlasHexDirection.SouthEast, "hex_0_0", "hex_1_m1", "hex_1_0"),
        };

        return new AtlasRegionDefinition(
            "region_wolfpine_legacy19_graybox",
            "Wolfpine Sigil Graybox",
            nodes,
            BuildSigilAnchorCoordinates(nodes, anchorSlots),
            CreateSigils(),
            CreateRoster(),
            new[]
            {
                new AtlasStageCandidate(1, "1A", "hex_m2_1", Array.Empty<string>()),
                new AtlasStageCandidate(1, "1B", "hex_m2_0", Array.Empty<string>()),
                new AtlasStageCandidate(2, "2A", "hex_m1_2", new[] { "hex_m2_1", "hex_m2_0" }),
                new AtlasStageCandidate(2, "2B", "hex_0_m1", new[] { "hex_m2_1", "hex_m2_0" }),
                new AtlasStageCandidate(3, "3A", "hex_0_0", new[] { "hex_m1_2", "hex_0_m1" }),
                new AtlasStageCandidate(3, "3B", "hex_1_m1", new[] { "hex_m1_2", "hex_0_m1" }),
                new AtlasStageCandidate(4, "보스", "hex_1_0", new[] { "hex_0_0", "hex_1_m1" }),
                new AtlasStageCandidate(5, "추출", "hex_2_m1", new[] { "hex_1_0" }),
            },
            anchorSlots,
            "anchor_slots_v2_19hex_legacy",
            "footprint_profiles_v2_shape_category");
    }

    private static AtlasRegionNode Node(
        string nodeId,
        int q,
        int r,
        AtlasNodeKind kind,
        string label,
        string enemyPreview,
        string rewardFamily,
        string answerLane,
        int siteNodeIndex = -1,
        AtlasRegionLayer layer = AtlasRegionLayer.Outer)
    {
        return new AtlasRegionNode(nodeId, new AtlasHexCoordinate(q, r), kind, label, enemyPreview, rewardFamily, answerLane, siteNodeIndex, layer);
    }

    private static SigilAnchorSlot Anchor(
        string anchorId,
        string hexId,
        AtlasRegionLayer layer,
        string stageBand,
        string anchorRole,
        AtlasHexDirection orientationToCore,
        params string[] coveragePreview)
    {
        return new SigilAnchorSlot(
            anchorId,
            hexId,
            layer.ToString(),
            stageBand,
            anchorRole,
            orientationToCore,
            coveragePreview,
            new[] { nameof(TraversalMode.CampaignFirstClear), nameof(TraversalMode.CampaignResume), nameof(TraversalMode.Revisit), nameof(TraversalMode.EndlessRegion) });
    }

    private static IReadOnlyList<AtlasHexCoordinate> BuildSigilAnchorCoordinates(
        IReadOnlyList<AtlasRegionNode> nodes,
        IReadOnlyList<SigilAnchorSlot> slots)
    {
        var byId = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        return slots.Select(slot => byId[slot.HexId].Hex).ToArray();
    }

    private static IReadOnlyList<AtlasSigilDefinition> CreateSigils()
    {
        return new[]
        {
            new AtlasSigilDefinition(
                "sigil_beast_spoils",
                "Beast Spoils",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Beast trophy reward", 34) },
                AtlasModifierCategory.RewardBias,
                "RewardBias.Cluster.Wide",
                1),
            new AtlasSigilDefinition(
                "sigil_flank_pressure",
                "Flank Pressure",
                2,
                "threat",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "Flank threat", 30) },
                AtlasModifierCategory.ThreatPressure,
                "ThreatPressure.Lane.Long",
                1),
            new AtlasSigilDefinition(
                "sigil_ruin_scholar",
                "Ruin Scholar",
                1,
                "affinity",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "Ruin affinity", 24) },
                AtlasModifierCategory.AffinityBoost,
                "AffinityBoost.ScoutArc.Deep",
                1),
            new AtlasSigilDefinition(
                "sigil_elite_relic",
                "Elite Relic",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Relic reward", 44) },
                AtlasModifierCategory.RewardBias,
                "RewardBias.Cluster.Dense",
                2),
            new AtlasSigilDefinition(
                "sigil_marked_boss",
                "Marked Boss",
                1,
                "threat",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "Marked pressure", 38) },
                AtlasModifierCategory.ThreatPressure,
                "ThreatPressure.Lane.Hard",
                2),
            new AtlasSigilDefinition(
                "sigil_forest_guide",
                "Forest Guide",
                2,
                "affinity",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "Forest affinity", 20) },
                AtlasModifierCategory.AffinityBoost,
                "AffinityBoost.ScoutArc.Wide",
                1),
        };
    }

    private static IReadOnlyList<AtlasCharacterPreview> CreateRoster()
    {
        return new[]
        {
            new AtlasCharacterPreview("hero_dawn_priest", "Danrin / Dawn Priest", "support cleanse", "cleanse_mark", new[] { "oath", "herb", "bond" }),
            new AtlasCharacterPreview("hero_pack_raider", "Ippalbaram / Pack Raider", "bruiser anti-dive", "peel_anti_dive", new[] { "beast", "fang", "trail" }),
            new AtlasCharacterPreview("hero_echo_savant", "Gonghan / Echo Savant", "carry route reader", "route_read", new[] { "relic", "scout", "glass" }),
            new AtlasCharacterPreview("hero_grave_hexer", "Mukhwang / Grave Hexer", "control breaker", "break_formation", new[] { "ruin", "oath", "relic" }),
            new AtlasCharacterPreview("hero_iron_warden", "Iron Warden", "guard anchor", "guard_anchor", new[] { "boss", "gate", "elite" }),
            new AtlasCharacterPreview("hero_ash_cartographer", "Ash Cartographer", "scout pathing", "route_read", new[] { "road", "scout", "region" }),
        };
    }

    private static void ValidateThirtySevenRegion(AtlasRegionDefinition region)
    {
        if (region.Nodes.Count != 37)
        {
            throw new InvalidOperationException($"Atlas 37hex region must contain 37 nodes, but found {region.Nodes.Count}.");
        }

        AssertLayerCount(region, AtlasRegionLayer.Outer, 18);
        AssertLayerCount(region, AtlasRegionLayer.Middle, 12);
        AssertLayerCount(region, AtlasRegionLayer.Inner, 6);
        AssertLayerCount(region, AtlasRegionLayer.Core, 1);
        AssertAnchorLayerCount(region, AtlasRegionLayer.Outer, 6);
        AssertAnchorLayerCount(region, AtlasRegionLayer.Middle, 4);
        AssertAnchorLayerCount(region, AtlasRegionLayer.Inner, 2);
        AssertAnchorLayerCount(region, AtlasRegionLayer.Core, 0);
    }

    private static void AssertLayerCount(AtlasRegionDefinition region, AtlasRegionLayer layer, int expected)
    {
        var actual = region.Nodes.Count(node => node.Layer == layer);
        if (actual != expected)
        {
            throw new InvalidOperationException($"Atlas layer {layer} must contain {expected} nodes, but found {actual}.");
        }
    }

    private static void AssertAnchorLayerCount(AtlasRegionDefinition region, AtlasRegionLayer layer, int expected)
    {
        var actual = region.SigilAnchorSlots.Count(slot => string.Equals(slot.LayerId, layer.ToString(), StringComparison.Ordinal));
        if (actual != expected)
        {
            throw new InvalidOperationException($"Atlas anchor layer {layer} must contain {expected} anchors, but found {actual}.");
        }
    }
}
