using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Unity;
using SM.Unity.Sandbox;

namespace SM.Editor.Authoring.CombatSandbox;

public sealed record CombatSandboxLaunchTruthPreview(
    string BreakpointSummary,
    string DriftSummary,
    string MembershipWarning);

public static class CombatSandboxLaunchTruthDiffService
{
    public static CombatSandboxLaunchTruthPreview BuildPreview(
        CombatSandboxCompiledScenario scenario,
        ICombatContentLookup lookup)
    {
        var slice = lookup.GetFirstPlayableSlice();
        return new CombatSandboxLaunchTruthPreview(
            BuildBreakpointSummary(scenario, slice),
            BuildDriftSummary(scenario, lookup, slice),
            BuildMembershipWarning(scenario, slice));
    }

    private static string BuildBreakpointSummary(
        CombatSandboxCompiledScenario scenario,
        FirstPlayableSliceDefinition? slice)
    {
        var builder = new StringBuilder();
        AppendBreakpointSummary(builder, "left", scenario.LeftTeam, slice);
        builder.AppendLine();
        AppendBreakpointSummary(builder, "right", scenario.RightTeam, slice);
        return builder.ToString().TrimEnd();
    }

    private static void AppendBreakpointSummary(
        StringBuilder builder,
        string label,
        CombatSandboxCompiledTeam team,
        FirstPlayableSliceDefinition? slice)
    {
        builder.AppendLine($"{label} [{team.SourceMode}]");
        AppendFamilyCounts(builder, "race", team.Snapshot.Allies.Select(unit => unit.RaceId), false, slice);
        AppendFamilyCounts(builder, "class", team.Snapshot.Allies.Select(unit => unit.ClassId), true, slice);
    }

    private static void AppendFamilyCounts(
        StringBuilder builder,
        string prefix,
        IEnumerable<string> familyIds,
        bool isClassFamily,
        FirstPlayableSliceDefinition? slice)
    {
        var counts = familyIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
        if (counts.Count == 0)
        {
            builder.AppendLine($"- {prefix}: none");
            return;
        }

        foreach (var group in counts)
        {
            var synergyId = $"synergy_{group.Key}";
            var thresholds = ResolveThresholds(slice, synergyId, isClassFamily);
            var reached = group.Count() >= thresholds.Major
                ? "major"
                : group.Count() >= thresholds.Minor
                    ? "minor"
                    : "none";
            builder.AppendLine($"- {prefix}:{group.Key} count={group.Count()} thresholds={thresholds.Minor}/{thresholds.Major} reached={reached}");
        }
    }

    private static string BuildDriftSummary(
        CombatSandboxCompiledScenario scenario,
        ICombatContentLookup lookup,
        FirstPlayableSliceDefinition? slice)
    {
        var builder = new StringBuilder();
        AppendDriftSummary(builder, "left", scenario.LeftTeam, lookup, slice);
        builder.AppendLine();
        AppendDriftSummary(builder, "right", scenario.RightTeam, lookup, slice);
        return builder.ToString().TrimEnd();
    }

    private static void AppendDriftSummary(
        StringBuilder builder,
        string label,
        CombatSandboxCompiledTeam team,
        ICombatContentLookup lookup,
        FirstPlayableSliceDefinition? slice)
    {
        builder.AppendLine($"{label} [{team.SourceMode}]");
        foreach (var unit in team.Snapshot.Allies)
        {
            var categories = ResolveDriftCategories(team, unit, lookup, slice);
            builder.AppendLine(categories.Count == 0
                ? $"- {unit.Id} [{unit.ArchetypeId}] baseline"
                : $"- {unit.Id} [{unit.ArchetypeId}] {string.Join(", ", categories)}");
        }
    }

    private static IReadOnlyList<string> ResolveDriftCategories(
        CombatSandboxCompiledTeam team,
        BattleUnitLoadout unit,
        ICombatContentLookup lookup,
        FirstPlayableSliceDefinition? slice)
    {
        if (slice?.UnitBlueprintIds.Contains(unit.ArchetypeId, StringComparer.Ordinal) != true)
        {
            return new[] { "out_of_roster_scope" };
        }

        var categories = new List<string>();
        if (HasSlotDrift(unit))
        {
            categories.Add("slot");
        }

        if (GetProvenanceSourceIds(team.Snapshot, unit.Id, "item").Count > 0)
        {
            categories.Add("equipment");
        }

        if (GetProvenanceSourceIds(team.Snapshot, unit.Id, "passive_numeric").Count > 0)
        {
            categories.Add("passive-board");
        }

        if (GetProvenanceSourceIds(team.Snapshot, unit.Id, "augment_temporary", "augment_permanent").Count > 0)
        {
            categories.Add("augment");
        }

        if (HasPostureOrTacticDrift(team, lookup, slice))
        {
            categories.Add("posture/tactic");
        }

        return categories;
    }

    private static bool HasSlotDrift(BattleUnitLoadout unit)
    {
        if (!DefaultSignatureActiveIds.TryGetValue(unit.ArchetypeId, out var expectedSignatureActive)
            || !DefaultFlexActiveIds.TryGetValue(unit.ArchetypeId, out var expectedFlexActive)
            || !DefaultSignaturePassiveIds.TryGetValue(unit.ClassId, out var expectedSignaturePassive)
            || !DefaultFlexPassiveIds.TryGetValue(unit.ClassId, out var expectedFlexPassive))
        {
            return false;
        }

        return !string.Equals(unit.EffectiveSignatureActive?.Id ?? string.Empty, expectedSignatureActive, StringComparison.Ordinal)
               || !string.Equals(unit.EffectiveFlexActive?.Id ?? string.Empty, expectedFlexActive, StringComparison.Ordinal)
               || !string.Equals(unit.EffectiveSignaturePassive.Id, expectedSignaturePassive, StringComparison.Ordinal)
               || !string.Equals(unit.EffectiveFlexPassive.Id, expectedFlexPassive, StringComparison.Ordinal);
    }

    private static bool HasPostureOrTacticDrift(
        CombatSandboxCompiledTeam team,
        ICombatContentLookup lookup,
        FirstPlayableSliceDefinition? slice)
    {
        var recommendedPostures = team.Snapshot.Allies
            .Where(unit => slice?.UnitBlueprintIds.Contains(unit.ArchetypeId, StringComparer.Ordinal) == true)
            .Select(unit => lookup.TryGetArchetype(unit.ArchetypeId, out var archetype)
                ? ToRuntimePosture(archetype.PreferredTeamPosture)
                : team.Snapshot.TeamTactic.Posture)
            .Distinct()
            .ToList();
        if (recommendedPostures.Count != 1)
        {
            return false;
        }

        var expectedPosture = recommendedPostures[0];
        var expectedTacticId = ResolveTeamTacticId(expectedPosture);
        return team.Snapshot.TeamTactic.Posture != expectedPosture
               || !string.Equals(team.Snapshot.TeamTactic.Id, expectedTacticId, StringComparison.Ordinal);
    }

    private static string BuildMembershipWarning(
        CombatSandboxCompiledScenario scenario,
        FirstPlayableSliceDefinition? slice)
    {
        var builder = new StringBuilder();
        AppendMembershipWarning(builder, "left", scenario.LeftTeam, slice);
        builder.AppendLine();
        AppendMembershipWarning(builder, "right", scenario.RightTeam, slice);
        return builder.ToString().TrimEnd();
    }

    private static void AppendMembershipWarning(
        StringBuilder builder,
        string label,
        CombatSandboxCompiledTeam team,
        FirstPlayableSliceDefinition? slice)
    {
        var outOfScopeUnits = team.Snapshot.Allies
            .Where(unit => slice?.UnitBlueprintIds.Contains(unit.ArchetypeId, StringComparer.Ordinal) != true)
            .Select(unit => $"{unit.Id}[{unit.ArchetypeId}]")
            .ToList();
        builder.AppendLine(outOfScopeUnits.Count == 0
            ? $"{label}: all units inside first_playable_slice"
            : $"{label}: {string.Join(", ", outOfScopeUnits)}");
    }

    private static IReadOnlyList<string> GetProvenanceSourceIds(
        BattleLoadoutSnapshot snapshot,
        string unitId,
        params string[] artifactKinds)
    {
        IReadOnlyList<string> sourceIds = snapshot.Provenance == null
            ? Array.Empty<string>()
            : snapshot.Provenance
                .Where(entry => string.Equals(entry.SubjectId, unitId, StringComparison.Ordinal)
                                && artifactKinds.Any(kind => string.Equals(kind, entry.ArtifactKind, StringComparison.Ordinal)))
                .Select(entry => entry.SourceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        return sourceIds;
    }

    private static (int Minor, int Major) ResolveThresholds(
        FirstPlayableSliceDefinition? slice,
        string synergyId,
        bool isClassFamily)
    {
        var entry = slice?.SynergyGrammar?.FirstOrDefault(candidate => string.Equals(candidate.FamilyId, synergyId, StringComparison.Ordinal));
        if (entry != null)
        {
            return (Math.Max(1, entry.MinorThreshold), Math.Max(entry.MinorThreshold, entry.MajorThreshold));
        }

        return isClassFamily ? (2, 3) : (2, 4);
    }

    private static string ResolveTeamTacticId(TeamPostureType posture)
    {
        return posture switch
        {
            TeamPostureType.HoldLine => "team_tactic_hold_line",
            TeamPostureType.ProtectCarry => "team_tactic_protect_carry",
            TeamPostureType.CollapseWeakSide => "team_tactic_collapse_weak_side",
            TeamPostureType.AllInBackline => "team_tactic_all_in_backline",
            _ => "team_tactic_standard_advance",
        };
    }

    private static TeamPostureType ToRuntimePosture(TeamPostureTypeValue posture)
    {
        return (TeamPostureType)(int)posture;
    }

    private static readonly IReadOnlyDictionary<string, string> DefaultSignatureActiveIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = "skill_power_strike",
            ["guardian"] = "skill_guardian_core",
            ["bulwark"] = "skill_bulwark_core",
            ["slayer"] = "skill_slayer_core",
            ["raider"] = "skill_raider_core",
            ["reaver"] = "skill_reaver_core",
            ["hunter"] = "skill_precision_shot",
            ["scout"] = "skill_scout_core",
            ["marksman"] = "skill_marksman_core",
            ["priest"] = "skill_priest_core",
            ["hexer"] = "skill_hexer_core",
            ["shaman"] = "skill_shaman_core",
        };

    private static readonly IReadOnlyDictionary<string, string> DefaultFlexActiveIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = "skill_warden_utility",
            ["guardian"] = "skill_guardian_utility",
            ["bulwark"] = "skill_bulwark_utility",
            ["slayer"] = "skill_slayer_utility",
            ["raider"] = "skill_raider_utility",
            ["reaver"] = "skill_reaver_utility",
            ["hunter"] = "skill_hunter_utility",
            ["scout"] = "skill_scout_utility",
            ["marksman"] = "skill_marksman_utility",
            ["priest"] = "skill_minor_heal",
            ["hexer"] = "skill_hexer_utility",
            ["shaman"] = "skill_shaman_utility",
        };

    private static readonly IReadOnlyDictionary<string, string> DefaultSignaturePassiveIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vanguard"] = "skill_vanguard_passive_1",
            ["duelist"] = "skill_duelist_passive_1",
            ["ranger"] = "skill_ranger_passive_1",
            ["mystic"] = "skill_mystic_passive_1",
        };

    private static readonly IReadOnlyDictionary<string, string> DefaultFlexPassiveIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vanguard"] = "skill_vanguard_support_1",
            ["duelist"] = "skill_duelist_support_1",
            ["ranger"] = "skill_ranger_support_1",
            ["mystic"] = "skill_mystic_support_1",
        };
}
