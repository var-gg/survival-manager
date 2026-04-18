using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;

namespace SM.Editor.Validation;

internal sealed record ArchetypeBuildLaneExpectation(
    string ArchetypeId,
    string BaselineLaneId,
    string BaselineSupportId,
    string AltLaneId,
    string AltSupportId);

internal static class FirstPlayableAuthoringContract
{
    internal const int LiveSignaturePassiveCap = 8;

    internal static readonly IReadOnlyList<string> RequiredPassiveBoardIds = new[]
    {
        "board_vanguard",
        "board_duelist",
        "board_ranger",
        "board_mystic",
    };

    internal static readonly HashSet<string> AllowedEncounterFamilyIds = new(StringComparer.Ordinal)
    {
        "encounter_family_bastion_front",
        "encounter_family_protect_carry",
        "encounter_family_weakside_dive",
        "encounter_family_tempo_swarm",
        "encounter_family_sustain_grind",
        "encounter_family_mark_execute",
        "encounter_family_control_cleanse",
        "encounter_family_summon_pressure",
    };

    internal static readonly HashSet<string> AllowedAnswerLaneIds = new(StringComparer.Ordinal)
    {
        "answer_lane_guard_anchor",
        "answer_lane_reach_anti_carry",
        "answer_lane_anti_swarm_persistence",
        "answer_lane_peel_cleanse",
        "answer_lane_anti_sustain_finish",
        "answer_lane_hybrid_boss_prep",
    };

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ClassLaneSupportIds =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["vanguard"] = new[] { "support_guarded", "support_purifying", "support_anchored" },
            ["duelist"] = new[] { "support_executioner", "support_swift", "support_brutal" },
            ["ranger"] = new[] { "support_longshot", "support_hunter_mark", "support_piercing" },
            ["mystic"] = new[] { "support_purifying", "support_echo", "support_lingering", "support_siphon" },
        };

    internal static readonly IReadOnlyDictionary<string, ArchetypeBuildLaneExpectation> ArchetypeBuildLaneExpectations =
        new Dictionary<string, ArchetypeBuildLaneExpectation>(StringComparer.Ordinal)
        {
            ["warden"] = new("warden", "hold_guard", "support_guarded", "attrition_hold", "support_anchored"),
            ["guardian"] = new("guardian", "attrition_hold", "support_anchored", "hold_guard", "support_guarded"),
            ["bulwark"] = new("bulwark", "anti_dive_peel", "support_purifying", "attrition_hold", "support_anchored"),
            ["slayer"] = new("slayer", "burst_execute", "support_executioner", "sustain_break", "support_brutal"),
            ["raider"] = new("raider", "sticky_flank", "support_brutal", "burst_execute", "support_executioner"),
            ["reaver"] = new("reaver", "sticky_flank", "support_swift", "sustain_break", "support_brutal"),
            ["hunter"] = new("hunter", "longshot_focus", "support_longshot", "armor_break_focus", "support_piercing"),
            ["scout"] = new("scout", "mobile_kite", "support_swift", "longshot_focus", "support_longshot"),
            ["marksman"] = new("marksman", "longshot_focus", "support_longshot", "armor_break_focus", "support_piercing"),
            ["priest"] = new("priest", "sustain_cleanse", "support_purifying", "control_attrition", "support_echo"),
            ["hexer"] = new("hexer", "control_attrition", "support_siphon", "persistent_pressure", "support_lingering"),
            ["shaman"] = new("shaman", "persistent_pressure", "support_lingering", "control_attrition", "support_echo"),
        };

    internal static IReadOnlyList<int> GetExpectedSynergyThresholds(SynergyDefinition synergy)
    {
        return IsClassSynergy(synergy) ? new[] { 2, 3 } : new[] { 2, 4 };
    }

    internal static bool IsClassSynergy(SynergyDefinition synergy)
    {
        return synergy != null
               && !string.IsNullOrWhiteSpace(synergy.CountedTagId)
               && ContentValidationPolicyCatalog.CanonicalClassIds.Contains(synergy.CountedTagId);
    }

    internal static string? ExtractEncounterFamily(IEnumerable<string> tags)
    {
        return tags?
            .Where(tag => AllowedEncounterFamilyIds.Contains(tag))
            .Distinct(StringComparer.Ordinal)
            .SingleOrDefault();
    }

    internal static IReadOnlyList<string> ExtractAnswerLanes(IEnumerable<string> tags)
    {
        return tags == null
            ? Array.Empty<string>()
            : tags
                .Where(tag => AllowedAnswerLaneIds.Contains(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList();
    }

    internal static IReadOnlyList<string> ExtractOverlayAskTags(IEnumerable<string> tags)
    {
        return tags == null
            ? Array.Empty<string>()
            : tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag) && tag.StartsWith("overlay_ask_", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList();
    }

    internal static IReadOnlyList<string> ExtractContextTags(LootBundleEntryDefinition entry)
    {
        var explicitTags = entry.RequiredContextTags == null
            ? new List<string>()
            : entry.RequiredContextTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList();
        return explicitTags.Count > 0
            ? explicitTags
            : InferContextTagsFromRouteEntryId(entry.Id);
    }

    private static IReadOnlyList<string> InferContextTagsFromRouteEntryId(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return Array.Empty<string>();
        }

        var answerLane = AllowedAnswerLaneIds
            .FirstOrDefault(lane => entryId.EndsWith("_" + lane, StringComparison.Ordinal));
        if (answerLane == null)
        {
            return Array.Empty<string>();
        }

        var routePrefix = entryId[..^(answerLane.Length + 1)];
        var siteId = new[] { "skirmish_", "elite_", "boss_" }
            .Where(prefix => routePrefix.StartsWith(prefix + "site_", StringComparison.Ordinal))
            .Select(prefix => routePrefix[prefix.Length..])
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(siteId)
            ? Array.Empty<string>()
            : new[] { siteId, answerLane };
    }

    internal static IReadOnlyList<string> CollectSupportSkillIds(UnitArchetypeDefinition archetype)
    {
        return archetype.FlexSupportSkillPool
            .Concat(archetype.RecruitFlexPassivePool)
            .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
            .Select(skill => skill.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }
}
