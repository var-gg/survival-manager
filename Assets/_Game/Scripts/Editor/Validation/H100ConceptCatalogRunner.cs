using System;
using System.IO;
using System.Linq;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>실콘텐츠를 pure census/graph 입력으로 낮춰 BT1 concept catalog를 생성한다.</summary>
public static class H100ConceptCatalogRunner
{
    private const string DefaultOutputDirectory = "Logs/h100-concept-catalog";

    public static void RunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100ConceptCatalogRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var requested = Environment.GetEnvironmentVariable("SM_H100_CONCEPT_CATALOG_OUTPUT")
                        ?? DefaultOutputDirectory;
        var outputDirectory = ResolveOutputDirectory(projectRoot, requested);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {error}");
        }

        var catalog = DeriveForSnapshot(snapshot);
        var path = ConceptCatalogArtifactWriter.Write(outputDirectory, catalog);
        var anchorSummary = string.Join(
            ", ",
            catalog.AnchorDerivations.Select(value =>
                $"{value.AnchorId}={(value.DerivationGap ? "gap" : value.LegalRecipeCount.ToString())}/{value.Variants.Count}"));
        Debug.Log(
            $"[H100ConceptCatalog] anchors={catalog.Summary.OwnerAnchorCount} "
            + $"resolved={catalog.Summary.OwnerAnchorWithRecipeCount} gaps={catalog.Summary.DerivationGapCount} "
            + $"ownerVariants={catalog.Summary.OwnerVariantCount} systemMedoids={catalog.Summary.SystemDerivedMedoidCount} "
            + $"rawStatExcluded={catalog.Summary.RawStatOnlyExcludedCount} output={path} anchors=[{anchorSummary}]");
    }

    public static ConceptCatalog DeriveForSnapshot(CombatContentSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var roster = H100BuildSpaceContentAdapter.BuildCanonicalRosterFromSnapshot(snapshot);
        var census = BuildSpaceEnumerator.Generate(roster);
        CanonicalBuildSpaceContract.RequireExpected(census);
        var truthGraph = H100BuildGrammarTruthProjector.Project(snapshot);
        return ConceptCatalogDeriver.Derive(
            OwnerConceptAnchorCatalog.CreateRatificationPendingDraft(),
            census,
            truthGraph,
            H100BuildGrammarVisibleSurfaceProjector.FeedbackWitnessVocabulary());
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 concept catalog output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
