using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
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
        var baselineCatalog = new LaunchCoreRosterBaselineCatalog(lookup);
        return new CombatSandboxLaunchTruthPreview(
            BuildBreakpointSummary(scenario, baselineCatalog),
            BuildDriftSummary(scenario, baselineCatalog),
            BuildMembershipWarning(scenario, baselineCatalog));
    }

    private static string BuildBreakpointSummary(
        CombatSandboxCompiledScenario scenario,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        var builder = new StringBuilder();
        AppendBreakpointSummary(builder, "left", scenario.LeftTeam, baselineCatalog);
        builder.AppendLine();
        AppendBreakpointSummary(builder, "right", scenario.RightTeam, baselineCatalog);
        return builder.ToString().TrimEnd();
    }

    private static void AppendBreakpointSummary(
        StringBuilder builder,
        string label,
        CombatSandboxCompiledTeam team,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        builder.AppendLine($"{label} [{team.SourceMode}]");
        AppendFamilyCounts(builder, "race", team.Snapshot.Allies.Select(unit => unit.RaceId), isClassFamily: false, baselineCatalog);
        AppendFamilyCounts(builder, "class", team.Snapshot.Allies.Select(unit => unit.ClassId), isClassFamily: true, baselineCatalog);
    }

    private static void AppendFamilyCounts(
        StringBuilder builder,
        string prefix,
        IEnumerable<string> familyIds,
        bool isClassFamily,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
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
            var thresholds = baselineCatalog.ResolveSynergyThresholds(synergyId, isClassFamily);
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
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        var builder = new StringBuilder();
        AppendDriftSummary(builder, "left", scenario.LeftTeam, baselineCatalog);
        builder.AppendLine();
        AppendDriftSummary(builder, "right", scenario.RightTeam, baselineCatalog);
        return builder.ToString().TrimEnd();
    }

    private static void AppendDriftSummary(
        StringBuilder builder,
        string label,
        CombatSandboxCompiledTeam team,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        builder.AppendLine($"{label} [{team.SourceMode}]");
        foreach (var unit in team.Snapshot.Allies)
        {
            builder.AppendLine(FormatUnitDrift(team, unit, baselineCatalog));
        }
    }

    private static string FormatUnitDrift(
        CombatSandboxCompiledTeam team,
        BattleUnitLoadout unit,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        if (!baselineCatalog.IsInFirstPlayableSlice(unit.ArchetypeId))
        {
            return $"- {unit.Id} [{unit.ArchetypeId}] out_of_roster_scope";
        }

        var deltas = new List<string>();
        if (baselineCatalog.TryGetUnitBaseline(unit.ArchetypeId, out var baseline))
        {
            AppendIfPresent(deltas, BuildSlotDelta(unit, baseline));
            AppendIfPresent(deltas, BuildPassiveDelta(team.Snapshot, unit.Id, baseline));
        }

        AppendIfPresent(deltas, BuildEquipmentDelta(team.Snapshot, unit.Id));
        AppendIfPresent(deltas, BuildAugmentDelta(team.Snapshot, unit.Id));
        AppendIfPresent(deltas, BuildPostureTacticDelta(team, baselineCatalog));

        return deltas.Count == 0
            ? $"- {unit.Id} [{unit.ArchetypeId}] baseline"
            : $"- {unit.Id} [{unit.ArchetypeId}] {string.Join("; ", deltas)}";
    }

    private static string? BuildSlotDelta(BattleUnitLoadout unit, LaunchCoreUnitBaseline baseline)
    {
        var deltas = new List<string>();
        AppendSlotDelta(deltas, "signature_active", baseline.SignatureActiveId, unit.EffectiveSignatureActive?.Id ?? string.Empty);
        AppendSlotDelta(deltas, "flex_active", baseline.FlexActiveId, unit.EffectiveFlexActive?.Id ?? string.Empty);
        AppendSlotDelta(deltas, "signature_passive", baseline.SignaturePassiveId, unit.EffectiveSignaturePassive.Id);
        AppendSlotDelta(deltas, "flex_passive", baseline.FlexPassiveId, unit.EffectiveFlexPassive.Id);
        return deltas.Count == 0 ? null : $"slot {string.Join(", ", deltas)}";
    }

    private static void AppendSlotDelta(
        ICollection<string> deltas,
        string slotName,
        string expectedId,
        string actualId)
    {
        if (string.Equals(expectedId, actualId, StringComparison.Ordinal))
        {
            return;
        }

        deltas.Add($"{slotName}:{FormatId(expectedId)}->{FormatId(actualId)}");
    }

    private static string? BuildEquipmentDelta(BattleLoadoutSnapshot snapshot, string unitId)
    {
        var itemIds = GetProvenanceSourceIds(snapshot, unitId, "item");
        return itemIds.Count == 0
            ? null
            : $"equipment +{string.Join(", +", itemIds)}";
    }

    private static string? BuildPassiveDelta(
        BattleLoadoutSnapshot snapshot,
        string unitId,
        LaunchCoreUnitBaseline baseline)
    {
        var nodeIds = GetProvenanceSourceIds(snapshot, unitId, "passive_numeric");
        if (nodeIds.Count == 0)
        {
            return null;
        }

        var baselineBoard = string.IsNullOrWhiteSpace(baseline.DefaultPassiveBoardId)
            ? string.Empty
            : $" baseline={baseline.DefaultPassiveBoardId}";
        return $"passive-board{baselineBoard} +{string.Join(", +", nodeIds)}";
    }

    private static string? BuildAugmentDelta(BattleLoadoutSnapshot snapshot, string unitId)
    {
        var temporaryIds = GetProvenanceSourceIds(snapshot, unitId, "augment_temporary");
        var permanentIds = GetProvenanceSourceIds(snapshot, unitId, "augment_permanent");
        if (temporaryIds.Count == 0 && permanentIds.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();
        if (temporaryIds.Count > 0)
        {
            parts.Add($"+temp[{string.Join(", ", temporaryIds)}]");
        }

        if (permanentIds.Count > 0)
        {
            parts.Add($"+perm[{string.Join(", ", permanentIds)}]");
        }

        return $"augment {string.Join(" ", parts)}";
    }

    private static string? BuildPostureTacticDelta(
        CombatSandboxCompiledTeam team,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        var baselines = team.Snapshot.Allies
            .Where(unit => baselineCatalog.IsInFirstPlayableSlice(unit.ArchetypeId))
            .Select(unit => baselineCatalog.TryGetUnitBaseline(unit.ArchetypeId, out var baseline) ? baseline : null)
            .Where(baseline => baseline != null)
            .Cast<LaunchCoreUnitBaseline>()
            .ToList();
        if (baselines.Count == 0)
        {
            return null;
        }

        var expectedPostures = baselines
            .Select(baseline => baseline.PreferredPosture)
            .Distinct()
            .OrderBy(value => value.ToString(), StringComparer.Ordinal)
            .ToList();
        var expectedTactics = baselines
            .Select(baseline => baseline.RecommendedTeamTacticId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        if (expectedPostures.Count == 1 && expectedTactics.Count == 1)
        {
            var expectedPosture = expectedPostures[0];
            var expectedTactic = expectedTactics[0];
            if (team.Snapshot.TeamTactic.Posture == expectedPosture
                && string.Equals(team.Snapshot.TeamTactic.Id, expectedTactic, StringComparison.Ordinal))
            {
                return null;
            }

            return $"posture/tactic posture:{expectedPosture}->{team.Snapshot.TeamTactic.Posture}, tactic:{expectedTactic}->{team.Snapshot.TeamTactic.Id}";
        }

        return $"posture/tactic mixed-baseline posture=[{string.Join(", ", expectedPostures)}] tactic=[{string.Join(", ", expectedTactics)}] current={team.Snapshot.TeamTactic.Posture}/{team.Snapshot.TeamTactic.Id}";
    }

    private static string BuildMembershipWarning(
        CombatSandboxCompiledScenario scenario,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        var builder = new StringBuilder();
        AppendMembershipWarning(builder, "left", scenario.LeftTeam, baselineCatalog);
        builder.AppendLine();
        AppendMembershipWarning(builder, "right", scenario.RightTeam, baselineCatalog);
        return builder.ToString().TrimEnd();
    }

    private static void AppendMembershipWarning(
        StringBuilder builder,
        string label,
        CombatSandboxCompiledTeam team,
        LaunchCoreRosterBaselineCatalog baselineCatalog)
    {
        var outOfScopeUnits = team.Snapshot.Allies
            .Where(unit => !baselineCatalog.IsInFirstPlayableSlice(unit.ArchetypeId))
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
        return snapshot.Provenance == null
            ? Array.Empty<string>()
            : snapshot.Provenance
                .Where(entry => string.Equals(entry.SubjectId, unitId, StringComparison.Ordinal)
                                && artifactKinds.Any(kind => string.Equals(kind, entry.ArtifactKind, StringComparison.Ordinal)))
                .Select(entry => entry.SourceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
    }

    private static void AppendIfPresent(ICollection<string> deltas, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            deltas.Add(value);
        }
    }

    private static string FormatId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? "none" : id;
    }
}
