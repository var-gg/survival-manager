using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;

namespace SM.Unity.Sandbox;

public static class CombatSandboxPreviewFormatter
{
    public static string BuildScenarioSummary(CombatSandboxCompiledScenario scenario)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"lane={scenario.LaneKind} seed={scenario.Seed} batch={Math.Max(1, scenario.Execution.BatchCount)}");
        builder.AppendLine($"left={scenario.LeftTeam.Snapshot.CompileHash}");
        builder.AppendLine($"right={scenario.RightTeam.Snapshot.CompileHash}");
        if (scenario.Warnings.Count > 0)
        {
            builder.AppendLine($"warnings={scenario.Warnings.Count}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildTeamPreview(CombatSandboxCompiledTeam team)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{team.DisplayName} [{team.SourceMode}]");
        builder.AppendLine($"posture={team.Snapshot.TeamTactic.Posture} compile={team.Snapshot.CompileHash}");
        builder.AppendLine($"tags={FormatList(team.Snapshot.TeamTags, 10)}");
        builder.AppendLine($"counter={FormatCounterCoverage(team.Snapshot.TeamCounterCoverage)}");
        var weaknesses = DescribeWeaknesses(team.Snapshot.TeamCounterCoverage);
        if (!string.IsNullOrWhiteSpace(weaknesses))
        {
            builder.AppendLine($"weak={weaknesses}");
        }

        foreach (var unit in team.Snapshot.Allies.Take(6))
        {
            builder.AppendLine($"- {unit.Name} / {unit.RaceId} / {unit.ClassId} / {unit.RoleTag} / {unit.PreferredAnchor}");
            builder.AppendLine($"  items={FormatArtifacts(team.Snapshot.Provenance, unit.Id, "item")}");
            builder.AppendLine($"  affixes={FormatArtifacts(team.Snapshot.Provenance, unit.Id, "affix")}");
            builder.AppendLine($"  passives={FormatArtifacts(team.Snapshot.Provenance, unit.Id, "passive_numeric")}");
            builder.AppendLine($"  augments={FormatArtifacts(team.Snapshot.Provenance, unit.Id, "augment_temporary", "augment_permanent")}");
        }

        builder.AppendLine($"provenance={FormatProvenance(team.Snapshot.Provenance)}");
        return builder.ToString().TrimEnd();
    }

    private static string FormatArtifacts(
        IReadOnlyList<CompileProvenanceEntry>? provenance,
        string subjectId,
        params string[] artifactKinds)
    {
        if (provenance == null || provenance.Count == 0)
        {
            return "none";
        }

        var artifacts = provenance
            .Where(entry => string.Equals(entry.SubjectId, subjectId, StringComparison.Ordinal)
                            && artifactKinds.Any(kind => string.Equals(kind, entry.ArtifactKind, StringComparison.Ordinal)))
            .Select(entry => entry.SourceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return artifacts.Count == 0 ? "none" : string.Join(", ", artifacts);
    }

    private static string FormatCounterCoverage(TeamCounterCoverageReport? report)
    {
        if (report == null)
        {
            return "none";
        }

        return string.Join(
            ", ",
            new[]
            {
                $"armor:{report.ArmorShred}",
                $"expose:{report.Exposure}",
                $"guard:{report.GuardBreakMultiHit}",
                $"track:{report.TrackingArea}",
                $"tenacity:{report.TenacityStability}",
                $"antiheal:{report.AntiHealShatter}",
                $"peel:{report.InterceptPeel}",
                $"cleave:{report.CleaveWaveclear}",
            });
    }

    private static string DescribeWeaknesses(TeamCounterCoverageReport? report)
    {
        if (report == null)
        {
            return string.Empty;
        }

        var weaknesses = new List<string>();
        AddWeakness(weaknesses, "armor", report.ArmorShred);
        AddWeakness(weaknesses, "expose", report.Exposure);
        AddWeakness(weaknesses, "guard", report.GuardBreakMultiHit);
        AddWeakness(weaknesses, "track", report.TrackingArea);
        AddWeakness(weaknesses, "tenacity", report.TenacityStability);
        AddWeakness(weaknesses, "antiheal", report.AntiHealShatter);
        AddWeakness(weaknesses, "peel", report.InterceptPeel);
        AddWeakness(weaknesses, "cleave", report.CleaveWaveclear);
        return weaknesses.Count == 0 ? string.Empty : string.Join(", ", weaknesses);
    }

    private static void AddWeakness(ICollection<string> weaknesses, string label, CounterCoverageLevelValue level)
    {
        if (level is CounterCoverageLevelValue.None or CounterCoverageLevelValue.Light)
        {
            weaknesses.Add(label);
        }
    }

    private static string FormatProvenance(IReadOnlyList<CompileProvenanceEntry>? provenance)
    {
        if (provenance == null || provenance.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ", ",
            provenance
                .GroupBy(entry => entry.ArtifactKind)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatList(IEnumerable<string> values, int maxCount)
    {
        var materialized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(maxCount)
            .ToList();
        return materialized.Count == 0 ? "none" : string.Join(", ", materialized);
    }
}
