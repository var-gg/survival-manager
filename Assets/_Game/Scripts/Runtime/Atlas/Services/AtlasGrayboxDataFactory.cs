using System.Collections.Generic;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasGrayboxDataFactory
{
    public static AtlasRegionDefinition CreateRegion()
    {
        var nodes = new List<AtlasRegionNode>
        {
            new("hex_m2_0", new AtlasHexCoordinate(-2, 0), AtlasNodeKind.Normal, "North Spur", "wolf scout trace", "fang", "peel_anti_dive"),
            new("hex_m2_1", new AtlasHexCoordinate(-2, 1), AtlasNodeKind.Skirmish, "Fang Skirmish", "beast skirmish / weakside dive", "beast trophy", "peel_anti_dive", 0),
            new("hex_m2_2", new AtlasHexCoordinate(-2, 2), AtlasNodeKind.Normal, "Moss Shelf", "low pressure path", "herb", "guard_anchor"),
            new("hex_m1_m1", new AtlasHexCoordinate(-1, -1), AtlasNodeKind.SigilAnchor, "West Sigil Anchor", "anchor slot", "sigil", "route_read"),
            new("hex_m1_0", new AtlasHexCoordinate(-1, 0), AtlasNodeKind.Normal, "Broken Trail", "beast patrol", "ore", "break_formation"),
            new("hex_m1_1", new AtlasHexCoordinate(-1, 1), AtlasNodeKind.Event, "Buried Oath", "event clue / oath memory", "bond token", "route_read"),
            new("hex_m1_2", new AtlasHexCoordinate(-1, 2), AtlasNodeKind.Skirmish, "Ridge Ambush", "beast skirmish / flank pressure", "fang", "guard_anchor", 1),
            new("hex_0_m2", new AtlasHexCoordinate(0, -2), AtlasNodeKind.Normal, "Dry Wash", "safe scout line", "standard cache", "route_read"),
            new("hex_0_m1", new AtlasHexCoordinate(0, -1), AtlasNodeKind.Reward, "Relic Cache", "reward cache / no combat", "relic metal", "route_read"),
            new("hex_0_0", new AtlasHexCoordinate(0, 0), AtlasNodeKind.Elite, "Glass Elk Elite", "elite beast / guard break", "elite relic", "break_formation", 2),
            new("hex_0_1", new AtlasHexCoordinate(0, 1), AtlasNodeKind.SigilAnchor, "Center Sigil Anchor", "anchor slot", "sigil", "route_read"),
            new("hex_0_2", new AtlasHexCoordinate(0, 2), AtlasNodeKind.Normal, "Fen Edge", "swarm signs", "herb", "cleanse_mark"),
            new("hex_1_m2", new AtlasHexCoordinate(1, -2), AtlasNodeKind.SigilAnchor, "East Sigil Anchor", "anchor slot", "sigil", "route_read"),
            new("hex_1_m1", new AtlasHexCoordinate(1, -1), AtlasNodeKind.Normal, "Cairn Step", "ruin ward", "relic", "break_formation"),
            new("hex_1_0", new AtlasHexCoordinate(1, 0), AtlasNodeKind.Boss, "Ashen Gate Boss", "boss / marked cleave pressure", "boss dossier", "guard_anchor", 3),
            new("hex_1_1", new AtlasHexCoordinate(1, 1), AtlasNodeKind.Normal, "Old Road", "standard patrol", "coin", "peel_anti_dive"),
            new("hex_2_m2", new AtlasHexCoordinate(2, -2), AtlasNodeKind.Normal, "Watch Stone", "distant warning", "scout report", "route_read"),
            new("hex_2_m1", new AtlasHexCoordinate(2, -1), AtlasNodeKind.Extract, "Dawn Extract", "extract after boss", "region pin", "route_read", 4),
            new("hex_2_0", new AtlasHexCoordinate(2, 0), AtlasNodeKind.Normal, "South Spur", "low pressure path", "standard cache", "cleanse_mark"),
        };

        var sigils = new[]
        {
            new AtlasSigilDefinition(
                "sigil_beast_spoils",
                "Beast Spoils",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Beast trophy reward", 34) }),
            new AtlasSigilDefinition(
                "sigil_flank_pressure",
                "Flank Pressure",
                2,
                "threat",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "Flank threat", 30) }),
            new AtlasSigilDefinition(
                "sigil_ruin_scholar",
                "Ruin Scholar",
                1,
                "affinity",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "Ruin affinity", 24) }),
            new AtlasSigilDefinition(
                "sigil_elite_relic",
                "Elite Relic",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Relic reward", 44) }),
            new AtlasSigilDefinition(
                "sigil_marked_boss",
                "Marked Boss",
                1,
                "threat",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "Marked pressure", 38) }),
            new AtlasSigilDefinition(
                "sigil_forest_guide",
                "Forest Guide",
                2,
                "affinity",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "Forest affinity", 20) }),
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

        var routes = new[]
        {
            new AtlasRouteCandidate("route_west_reward", "West Reward", new[] { "hex_m2_1", "hex_m1_1", "hex_0_0", "hex_1_0", "hex_2_m1" }),
            new AtlasRouteCandidate("route_center_greed", "Center Greed", new[] { "hex_m2_1", "hex_0_m1", "hex_0_0", "hex_1_0", "hex_2_m1" }),
            new AtlasRouteCandidate("route_east_safe", "East Safe", new[] { "hex_m1_2", "hex_1_m1", "hex_0_0", "hex_1_0", "hex_2_m1" }),
        };

        return new AtlasRegionDefinition(
            "region_wolfpine_graybox",
            "Wolfpine Sigil Graybox",
            nodes,
            new[] { new AtlasHexCoordinate(-1, -1), new AtlasHexCoordinate(0, 1), new AtlasHexCoordinate(1, -2) },
            sigils,
            roster,
            routes);
    }
}
