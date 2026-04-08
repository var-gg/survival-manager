using System;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using SM.Unity.Sandbox;

namespace SM.Editor.Authoring.CombatSandbox;

public static class CombatSandboxExecutionService
{
    public static CombatSandboxCompiledScenario BuildCompiledScenario(CombatSandboxConfig config, int seedOverride = 0)
    {
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        var session = new GameSessionState(lookup);
        session.BindProfile(LoadLocalProfile());
        session.EnsureBattleDeployReady();

        var compiler = new CombatSandboxScenarioCompiler(lookup);
        if (!compiler.TryCompileScenario(
                new CombatSandboxCompilationContext(
                    session.Profile,
                    session.DeploymentAssignments,
                    session.ExpeditionSquadHeroIds,
                    session.SelectedTeamPosture,
                    session.SelectedTeamTacticId,
                    session.Expedition.TemporaryAugmentIds,
                    session.CurrentExpeditionNodeIndex),
                config,
                config.DefaultLaneKind == CombatSandboxLaneKind.None ? CombatSandboxLaneKind.DirectCombatSandbox : config.DefaultLaneKind,
                seedOverride == 0 ? null : seedOverride,
                out var compiled,
                out var error))
        {
            throw new InvalidOperationException(error);
        }

        return compiled;
    }

    private static SaveProfile LoadLocalProfile()
    {
        var persistence = new PersistenceEntryPoint();
        var profileId = string.IsNullOrWhiteSpace(persistence.Config.ProfileId)
            ? "default"
            : persistence.Config.ProfileId;

        try
        {
            if (persistence.Repository is ISaveRepositoryDiagnostics diagnostics)
            {
                var result = diagnostics.LoadOrCreateDetailed(
                    profileId,
                    new SaveRepositoryRequest
                    {
                        CheckpointKind = "CombatSandboxPreview",
                    });
                if (result.Profile != null)
                {
                    return result.Profile;
                }
            }

            return persistence.Repository.LoadOrCreate(profileId);
        }
        catch
        {
            return new SaveProfile
            {
                ProfileId = profileId,
            };
        }
    }

    public static CombatSandboxRunRequest BuildRequest(CombatSandboxState state, BattlefieldLayout? sceneLayout = null)
    {
        if (state.Config == null)
        {
            throw new InvalidOperationException("Combat Sandbox active config is missing.");
        }

        var compiled = BuildCompiledScenario(state.Config, state.Seed);
        var batchCount = state.BatchCount > 0 ? state.BatchCount : Math.Max(1, compiled.Execution.BatchCount);
        return new CombatSandboxRunRequest(
            compiled.LeftTeam.Snapshot,
            compiled.RightTeam.Snapshot.Allies,
            compiled.Seed,
            batchCount,
            compiled.ScenarioId,
            sceneLayout);
    }

    public static string BuildCounterCoverageSummary(TeamCounterCoverageReport? ally, TeamCounterCoverageReport? enemy)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ally coverage");
        AppendCoverage(builder, ally);
        builder.AppendLine();
        builder.AppendLine("Enemy coverage");
        AppendCoverage(builder, enemy);
        return builder.ToString().TrimEnd();
    }

    public static string BuildGovernanceSummary(BattleLoadoutSnapshot snapshot, string inspectUnitId)
    {
        var unit = !string.IsNullOrWhiteSpace(inspectUnitId)
            ? snapshot.Allies.FirstOrDefault(candidate => string.Equals(candidate.Id, inspectUnitId, StringComparison.Ordinal))
            : snapshot.Allies.FirstOrDefault();
        if (unit?.Governance == null)
        {
            return "Selected unit governance unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"unit={unit.Id}");
        builder.AppendLine($"rarity={unit.Governance.Rarity} role={unit.Governance.RoleProfile} budget={unit.Governance.BudgetFinalScore}");
        builder.AppendLine($"threats=[{string.Join(", ", unit.Governance.DeclaredThreatPatterns)}]");
        builder.AppendLine($"counters=[{string.Join(", ", unit.Governance.DeclaredCounterTools.Select(tool => $"{tool.Tool}:{tool.Strength}"))}]");
        builder.AppendLine($"flags={unit.Governance.DeclaredFeatureFlags}");
        return builder.ToString().TrimEnd();
    }

    public static string BuildReadabilitySummary(ReadabilityReport? report)
    {
        if (report == null)
        {
            return "Readability report unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"salience_p95={report.SalienceWeightPer1sP95:0.###}");
        builder.AppendLine($"unexplained_damage={report.UnexplainedDamageRatio:0.###}");
        builder.AppendLine($"target_switch_p95={report.TargetSwitchesPer10sP95:0.###}");
        builder.AppendLine($"violations=[{string.Join(", ", report.Violations ?? Array.Empty<ReadabilityViolationKind>())}]");
        return builder.ToString().TrimEnd();
    }

    public static string BuildExplanationSummary(BattleSummaryReport? report)
    {
        if (report == null)
        {
            return "Battle summary unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"top_damage=[{string.Join(", ", report.TopDamageSources ?? Array.Empty<string>())}]");
        builder.AppendLine($"top_reasons=[{string.Join(", ", report.TopDecisionReasons ?? Array.Empty<string>())}]");
        builder.AppendLine($"decisive=[{string.Join(", ", report.DecisiveMoments ?? Array.Empty<string>())}]");
        return builder.ToString().TrimEnd();
    }

    public static string BuildProvenanceSummary(System.Collections.Generic.IReadOnlyList<CompileProvenanceEntry> provenance)
    {
        if (provenance == null || provenance.Count == 0)
        {
            return "Provenance unavailable.";
        }

        var builder = new StringBuilder();
        var grouped = provenance
            .GroupBy(entry => entry.SubjectId)
            .OrderBy(group => group.Key)
            .ToList();

        builder.AppendLine($"--- {grouped.Count} subjects, {provenance.Count} entries ---");

        foreach (var group in grouped)
        {
            var artifacts = group.GroupBy(e => e.ArtifactKind).Select(g => $"{g.Key}({g.Count()})");
            builder.AppendLine($"[{group.Key}] {string.Join(" ", artifacts)}");
            foreach (var entry in group)
            {
                builder.AppendLine($"  {entry.ArtifactKind} source={entry.SourceId}");
                if (entry.Details != null && entry.Details.Count > 0)
                {
                    foreach (var detail in entry.Details)
                    {
                        builder.AppendLine($"    {detail}");
                    }
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendCoverage(StringBuilder builder, TeamCounterCoverageReport? report)
    {
        if (report == null)
        {
            builder.AppendLine("- missing");
            return;
        }

        foreach (var lane in new[]
                 {
                     ("ArmorShred", report.ArmorShred),
                     ("Exposure", report.Exposure),
                     ("GuardBreakMultiHit", report.GuardBreakMultiHit),
                     ("TrackingArea", report.TrackingArea),
                     ("TenacityStability", report.TenacityStability),
                     ("AntiHealShatter", report.AntiHealShatter),
                     ("InterceptPeel", report.InterceptPeel),
                     ("CleaveWaveclear", report.CleaveWaveclear),
                 })
        {
            var warning = lane.Item2 is CounterCoverageLevelValue.None or CounterCoverageLevelValue.Light ? " !weak" : string.Empty;
            builder.AppendLine($"- {lane.Item1}: {lane.Item2}{warning}");
        }
    }
}
