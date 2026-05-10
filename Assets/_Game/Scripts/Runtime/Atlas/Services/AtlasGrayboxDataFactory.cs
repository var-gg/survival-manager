using System;
using System.Collections.Generic;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasGrayboxDataFactory
{
    public static AtlasRegionDefinition CreateRegion()
    {
        var nodes = new List<AtlasRegionNode>
        {
            new("hex_m2_0", new AtlasHexCoordinate(-2, 0), AtlasNodeKind.Skirmish, "North Spur", "wolf scout trace", "fang", "peel_anti_dive", 0),
            new("hex_m2_1", new AtlasHexCoordinate(-2, 1), AtlasNodeKind.Skirmish, "Fang Skirmish", "beast skirmish / weakside dive", "beast trophy", "peel_anti_dive", 0),
            new("hex_m2_2", new AtlasHexCoordinate(-2, 2), AtlasNodeKind.Cache, "Moss Shelf", "low pressure path", "herb", "guard_anchor"),
            new("hex_m1_m1", new AtlasHexCoordinate(-1, -1), AtlasNodeKind.SigilAnchor, "West Sigil Anchor", "anchor slot", "sigil", "route_read"),
            new("hex_m1_0", new AtlasHexCoordinate(-1, 0), AtlasNodeKind.Echo, "Broken Trail", "beast patrol", "ore", "break_formation"),
            new("hex_m1_1", new AtlasHexCoordinate(-1, 1), AtlasNodeKind.Echo, "Buried Oath", "event clue / oath memory", "bond token", "route_read"),
            new("hex_m1_2", new AtlasHexCoordinate(-1, 2), AtlasNodeKind.Skirmish, "Ridge Ambush", "beast skirmish / flank pressure", "fang", "guard_anchor", 1),
            new("hex_0_m2", new AtlasHexCoordinate(0, -2), AtlasNodeKind.Cache, "Dry Wash", "safe scout line", "standard cache", "route_read"),
            new("hex_0_m1", new AtlasHexCoordinate(0, -1), AtlasNodeKind.Skirmish, "Relic Cache", "reward cache / no combat", "relic metal", "guard_anchor", 1),
            new("hex_0_0", new AtlasHexCoordinate(0, 0), AtlasNodeKind.Elite, "Glass Elk Elite", "elite beast / guard break", "elite relic", "break_formation", 2),
            new("hex_0_1", new AtlasHexCoordinate(0, 1), AtlasNodeKind.SigilAnchor, "Center Sigil Anchor", "anchor slot", "sigil", "route_read"),
            new("hex_0_2", new AtlasHexCoordinate(0, 2), AtlasNodeKind.Echo, "Fen Edge", "swarm signs", "herb", "cleanse_mark"),
            new("hex_1_m2", new AtlasHexCoordinate(1, -2), AtlasNodeKind.SigilAnchor, "East Sigil Anchor", "anchor slot", "sigil", "route_read"),
            new("hex_1_m1", new AtlasHexCoordinate(1, -1), AtlasNodeKind.Elite, "Cairn Step", "ruin ward", "relic", "break_formation", 2),
            new("hex_1_0", new AtlasHexCoordinate(1, 0), AtlasNodeKind.Boss, "Ashen Gate Boss", "boss / marked cleave pressure", "boss dossier", "guard_anchor", 3),
            new("hex_1_1", new AtlasHexCoordinate(1, 1), AtlasNodeKind.ScoutVantage, "Old Road", "standard patrol", "coin", "peel_anti_dive"),
            new("hex_2_m2", new AtlasHexCoordinate(2, -2), AtlasNodeKind.ScoutVantage, "Watch Stone", "distant warning", "scout report", "route_read"),
            new("hex_2_m1", new AtlasHexCoordinate(2, -1), AtlasNodeKind.Extract, "Dawn Extract", "extract after boss", "region pin", "route_read", 4),
            new("hex_2_0", new AtlasHexCoordinate(2, 0), AtlasNodeKind.Cache, "South Spur", "low pressure path", "standard cache", "cleanse_mark"),
        };

        var sigils = new[]
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

        var roster = new[]
        {
            new AtlasCharacterPreview("hero_dawn_priest", "Danrin / Dawn Priest", "support cleanse", "cleanse_mark", new[] { "oath", "herb", "bond" }),
            new AtlasCharacterPreview("hero_pack_raider", "Ippalbaram / Pack Raider", "bruiser anti-dive", "peel_anti_dive", new[] { "beast", "fang", "trail" }),
            new AtlasCharacterPreview("hero_echo_savant", "Gonghan / Echo Savant", "carry route reader", "route_read", new[] { "relic", "scout", "glass" }),
            new AtlasCharacterPreview("hero_grave_hexer", "Mukhwang / Grave Hexer", "control breaker", "break_formation", new[] { "ruin", "oath", "relic" }),
            new AtlasCharacterPreview("hero_iron_warden", "Iron Warden", "guard anchor", "guard_anchor", new[] { "boss", "gate", "elite" }),
            new AtlasCharacterPreview("hero_ash_cartographer", "Ash Cartographer", "scout pathing", "route_read", new[] { "road", "scout", "region" }),
        };

        var stageCandidates = new[]
        {
            new AtlasStageCandidate(1, "1A", "hex_m2_1", Array.Empty<string>()),
            new AtlasStageCandidate(1, "1B", "hex_m2_0", Array.Empty<string>()),
            new AtlasStageCandidate(2, "2A", "hex_m1_2", new[] { "hex_m2_1", "hex_m2_0" }),
            new AtlasStageCandidate(2, "2B", "hex_0_m1", new[] { "hex_m2_1", "hex_m2_0" }),
            new AtlasStageCandidate(3, "3A", "hex_0_0", new[] { "hex_m1_2", "hex_0_m1" }),
            new AtlasStageCandidate(3, "3B", "hex_1_m1", new[] { "hex_m1_2", "hex_0_m1" }),
            new AtlasStageCandidate(4, "보스", "hex_1_0", new[] { "hex_0_0", "hex_1_m1" }),
            new AtlasStageCandidate(5, "추출", "hex_2_m1", new[] { "hex_1_0" }),
        };

        var anchorSlots = new[]
        {
            new SigilAnchorSlot(
                "anchor_outer_approach",
                "hex_m1_m1",
                "Outer",
                "stage_1_2",
                "Approach",
                AtlasHexDirection.SouthEast,
                new[] { "hex_m2_1", "hex_m2_0", "hex_m1_2" },
                new[] { "CampaignFirstClear", "Revisit" }),
            new SigilAnchorSlot(
                "anchor_middle_pressure",
                "hex_0_1",
                "Middle",
                "stage_2_3",
                "Pressure",
                AtlasHexDirection.NorthEast,
                new[] { "hex_m1_2", "hex_0_m1", "hex_0_0" },
                new[] { "CampaignFirstClear", "Revisit" }),
            new SigilAnchorSlot(
                "anchor_inner_evidence",
                "hex_1_m2",
                "Inner",
                "stage_3_boss",
                "Evidence",
                AtlasHexDirection.SouthEast,
                new[] { "hex_0_0", "hex_1_m1", "hex_1_0" },
                new[] { "CampaignFirstClear", "Revisit" }),
        };

        return new AtlasRegionDefinition(
            "region_wolfpine_graybox",
            "Wolfpine Sigil Graybox",
            nodes,
            new[] { new AtlasHexCoordinate(-1, -1), new AtlasHexCoordinate(0, 1), new AtlasHexCoordinate(1, -2) },
            sigils,
            roster,
            stageCandidates,
            anchorSlots);
    }
}
