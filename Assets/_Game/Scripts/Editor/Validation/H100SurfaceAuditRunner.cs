using System;
using System.IO;
using SM.Editor.SeedData;
using SM.HeadlessMetrics;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>실콘텐츠 build grammar와 E01 player-visible surface를 대조하는 E02 단일 진입점.</summary>
public static class H100SurfaceAuditRunner
{
    private const string DefaultOutputDirectory = "Logs/h100-surface-audit";

    public static void RunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100SurfaceAuditRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(
            projectRoot,
            Environment.GetEnvironmentVariable("SM_H100_SURFACE_OUTPUT") ?? DefaultOutputDirectory);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {error}");
        }

        RunForSnapshot(snapshot, outputDirectory);
    }

    internal static InformationSurfaceAuditResult RunForSnapshot(
        CombatContentSnapshot snapshot,
        string outputDirectory)
    {
        var truthGraph = H100BuildGrammarTruthProjector.Project(snapshot);
        var input = H100BuildGrammarVisibleSurfaceProjector.Project(snapshot, truthGraph);
        var result = InformationSurfaceAuditor.Audit(input);
        var path = InformationSurfaceAuditArtifactWriter.Write(outputDirectory, result);
        Debug.Log(
            $"[H100SurfaceAudit] subjects={result.ActionableSubjectCount} edges={result.ActionableEdgeCount} "
            + $"missing={result.ActionableOfferMissingSemantics} undefined={result.UndefinedVisibleToken} "
            + $"hidden={result.HiddenPrerequisite} mismatch={result.DescriptionBehaviorMismatchCount} "
            + $"feedback={result.InteractionFeedbackCoverage:F6} gaps={result.Gaps.Count} output={path}");
        return result;
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 surface audit output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
