using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>동일 composition·site·seed에서 placement만 다른 BT1-E09 paired corpus를 만든다.</summary>
internal static class H100TacticalAttributionCaseFactory
{
    private static readonly string[] RequiredSiteIds =
    {
        "site_ashen_gate",
        "site_wolfpine_trail",
        "site_sunken_bastion",
    };

    private static readonly string[] ProfileOrder =
    {
        "fortified_line",
        "forward_spear",
        "baited_gap",
        "screened_backline",
        "open_skirmish",
    };

    public static IReadOnlyList<H100TacticalAttributionCase> Build(
        CombatContentSnapshot snapshot,
        BuildSpaceCensus census,
        ConceptCatalog catalog,
        H100TacticalAttributionRunSettings settings)
    {
        var sites = ResolveSites(snapshot);
        var compositions = SelectCompositions(census, catalog, settings.CompositionCount);
        var cases = new List<H100TacticalAttributionCase>(
            compositions.Count * sites.Count * settings.SeedCount * (2 + 2 + census.Medoids.Count));

        for (var compositionIndex = 0; compositionIndex < compositions.Count; compositionIndex++)
        {
            var selected = compositions[compositionIndex];
            foreach (var site in sites)
            {
                for (var seedIndex = 0; seedIndex < settings.SeedCount; seedIndex++)
                {
                    var decisionSeed = settings.SeedBase + seedIndex;
                    var battleSeed = H100SessionDriver.DeriveSeed(
                        $"h100-bt1-e09|{selected.Build.BuildId}|{site.SiteId}",
                        decisionSeed);
                    var stratum = $"c{compositionIndex:D2}|{site.SiteId}|s{seedIndex:D2}";

                    AddPair(
                        cases,
                        selected,
                        site,
                        decisionSeed,
                        battleSeed,
                        stratum,
                        PlacementAttributionComparisonKind.SemanticAdjacentSwap,
                        selected.SemanticBaseline,
                        selected.SemanticCandidate,
                        semanticExpected: true);
                    AddPair(
                        cases,
                        selected,
                        site,
                        decisionSeed,
                        battleSeed,
                        stratum,
                        PlacementAttributionComparisonKind.ProfileTransition,
                        selected.ProfileBaseline,
                        selected.ProfileCandidate,
                        semanticExpected: false);

                    var anchorPairingId = $"{stratum}|{PlacementAttributionComparisonKind.AnchorSweep}";
                    for (var medoidIndex = 0; medoidIndex < census.Medoids.Count; medoidIndex++)
                    {
                        AddCase(
                            cases,
                            selected,
                            site,
                            decisionSeed,
                            battleSeed,
                            anchorPairingId,
                            PlacementAttributionComparisonKind.AnchorSweep,
                            $"anchor-medoid-{medoidIndex:D2}",
                            census.Medoids[medoidIndex].Placement,
                            isBaseline: medoidIndex == 0,
                            semanticExpected: false);
                    }
                }
            }
        }

        return cases.OrderBy(value => value.CaseId, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<SelectedComposition> SelectCompositions(
        BuildSpaceCensus census,
        ConceptCatalog catalog,
        int count)
    {
        var builds = census.Combinations.ToDictionary(value => value.BuildId, StringComparer.Ordinal);
        var formations = census.Formations.ToDictionary(value => value.Signature, StringComparer.Ordinal);
        var variants = catalog.SystemDerivedMedoids
            .Concat(catalog.AnchorDerivations.SelectMany(value => value.Variants))
            .Where(value => builds.ContainsKey(value.MedoidRecipe.BuildId)
                            && formations.ContainsKey(value.MedoidRecipe.FormationSignature))
            .OrderBy(value => Array.IndexOf(ProfileOrder, value.Fingerprint.FormationProfile))
            .ThenBy(value => value.MedoidRecipe.BuildId, StringComparer.Ordinal)
            .ThenBy(value => value.VariantId, StringComparer.Ordinal)
            .ToArray();

        var candidates = variants
            .GroupBy(value => value.MedoidRecipe.BuildId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(variant => TryBuildSelection(
                builds[variant.MedoidRecipe.BuildId],
                formations[variant.MedoidRecipe.FormationSignature],
                variant,
                census.Formations))
            .Where(value => value != null)
            .Cast<SelectedComposition>()
            .OrderBy(value => Array.IndexOf(ProfileOrder, value.ProfileBaseline.Profile))
            .ThenBy(value => value.Build.BuildId, StringComparer.Ordinal)
            .ToArray();

        var selected = new List<SelectedComposition>(count);
        foreach (var profile in ProfileOrder)
        {
            if (selected.Count >= count)
            {
                break;
            }

            var candidate = candidates.FirstOrDefault(value =>
                string.Equals(value.ProfileBaseline.Profile, profile, StringComparison.Ordinal)
                && selected.All(existing => !string.Equals(existing.Build.BuildId, value.Build.BuildId, StringComparison.Ordinal)));
            if (candidate != null)
            {
                selected.Add(candidate);
            }
        }

        foreach (var candidate in candidates)
        {
            if (selected.Count >= count)
            {
                break;
            }

            if (selected.All(existing => !string.Equals(existing.Build.BuildId, candidate.Build.BuildId, StringComparison.Ordinal)))
            {
                selected.Add(candidate);
            }
        }

        if (selected.Count != count)
        {
            throw new InvalidOperationException(
                $"BT1-E09 needs {count} concept-medoid compositions with a feature-invariant same-role adjacent swap (actual={selected.Count}).");
        }

        return selected;
    }

    private static SelectedComposition? TryBuildSelection(
        BuildCombination build,
        FormationPlacement conceptPlacement,
        ConceptVariant variant,
        IReadOnlyList<FormationPlacement> formations)
    {
        var members = build.FormationMembers;
        var semantic = FindSemanticSwap(members, formations);
        if (semantic == null)
        {
            return null;
        }

        var baselineProfile = FormationFeatureClassifier.ClassifyProfile(conceptPlacement.Features);
        var candidateProfile = PreferredTransition(baselineProfile);
        var profileCandidate = formations
            .Where(value => string.Equals(
                FormationFeatureClassifier.ClassifyProfile(value.Features),
                candidateProfile,
                StringComparison.Ordinal))
            .OrderBy(value => value.Signature, StringComparer.Ordinal)
            .FirstOrDefault();
        if (profileCandidate == null)
        {
            return null;
        }

        return new SelectedComposition(
            build,
            variant.VariantId,
            semantic.Baseline,
            semantic.Candidate,
            new PlacementSelection(conceptPlacement, baselineProfile),
            new PlacementSelection(profileCandidate, candidateProfile));
    }

    private static SemanticSwap? FindSemanticSwap(
        IReadOnlyList<BuildArchetype> members,
        IReadOnlyList<FormationPlacement> formations)
    {
        foreach (var placement in formations.OrderBy(value => value.Signature, StringComparer.Ordinal))
        {
            for (var left = 0; left < members.Count; left++)
            {
                for (var right = left + 1; right < members.Count; right++)
                {
                    if (members[left].Role != members[right].Role
                        || !AreAdjacent(placement.AnchorsByMemberIndex[left], placement.AnchorsByMemberIndex[right]))
                    {
                        continue;
                    }

                    var swapped = placement.AnchorsByMemberIndex.ToArray();
                    (swapped[left], swapped[right]) = (swapped[right], swapped[left]);
                    var features = FormationFeatureClassifier.Classify(swapped);
                    if (!Equals(features, placement.Features))
                    {
                        continue;
                    }

                    var candidate = new FormationPlacement(
                        -1,
                        Signature(swapped),
                        swapped,
                        features);
                    return new SemanticSwap(
                        new PlacementSelection(
                            placement,
                            FormationFeatureClassifier.ClassifyProfile(placement.Features)),
                        new PlacementSelection(
                            candidate,
                            FormationFeatureClassifier.ClassifyProfile(features)));
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<SiteSelection> ResolveSites(CombatContentSnapshot snapshot)
    {
        var sites = snapshot.ExpeditionSites ?? throw new InvalidOperationException("Expedition sites are unavailable.");
        return RequiredSiteIds.Select(siteId =>
        {
            if (!sites.TryGetValue(siteId, out var site))
            {
                throw new InvalidOperationException($"BT1-E09 encounter family is unavailable: {siteId}");
            }

            return new SiteSelection(site.ChapterId, site.Id);
        }).ToArray();
    }

    private static void AddPair(
        ICollection<H100TacticalAttributionCase> cases,
        SelectedComposition selected,
        SiteSelection site,
        int decisionSeed,
        int battleSeed,
        string stratum,
        string comparisonKind,
        PlacementSelection baseline,
        PlacementSelection candidate,
        bool semanticExpected)
    {
        var pairingId = $"{stratum}|{comparisonKind}";
        AddCase(cases, selected, site, decisionSeed, battleSeed, pairingId, comparisonKind, "baseline", baseline.Placement, true, semanticExpected);
        AddCase(cases, selected, site, decisionSeed, battleSeed, pairingId, comparisonKind, "candidate", candidate.Placement, false, semanticExpected);
    }

    private static void AddCase(
        ICollection<H100TacticalAttributionCase> cases,
        SelectedComposition selected,
        SiteSelection site,
        int decisionSeed,
        int battleSeed,
        string pairingId,
        string comparisonKind,
        string variantId,
        FormationPlacement placement,
        bool isBaseline,
        bool semanticExpected)
    {
        var profile = FormationFeatureClassifier.ClassifyProfile(placement.Features);
        var members = selected.Build.FormationMembers
            .Select((member, index) => new H100BattleScreeningMember(
                member.ArchetypeId,
                placement.AnchorsByMemberIndex[index]))
            .ToArray();
        var caseId = $"{pairingId}|{variantId}";
        cases.Add(new H100TacticalAttributionCase(
            caseId,
            pairingId,
            comparisonKind,
            selected.Build.BuildId,
            selected.ConceptVariantId,
            site.ChapterId,
            site.SiteId,
            decisionSeed,
            battleSeed,
            variantId,
            isBaseline,
            semanticExpected,
            profile,
            Snapshot(placement.Features),
            placement.AnchorsByMemberIndex.Select(value => (int)value).ToArray(),
            members));
    }

    private static bool AreAdjacent(DeploymentAnchorId left, DeploymentAnchorId right)
    {
        var leftRow = (int)left / 3;
        var leftColumn = (int)left % 3;
        var rightRow = (int)right / 3;
        var rightColumn = (int)right % 3;
        return Math.Abs(leftRow - rightRow) + Math.Abs(leftColumn - rightColumn) == 1;
    }

    private static string Signature(IReadOnlyList<DeploymentAnchorId> anchors)
        => string.Join("|", anchors.Select((anchor, index) =>
            $"{((BuildRole)index).ToString().ToLowerInvariant()}:{(int)anchor}"));

    private static string PreferredTransition(string profile)
        => profile switch
        {
            "fortified_line" => "forward_spear",
            "forward_spear" => "fortified_line",
            "screened_backline" => "forward_spear",
            "baited_gap" => "fortified_line",
            _ => "screened_backline",
        };

    private static PlacementAttributionBattleRecord.FormationFeatureSnapshot Snapshot(FormationFeatures value)
        => new(
            value.FrontlineCount,
            value.ProtectedSlotCount,
            value.SideExposureCount,
            value.RearExposureCount,
            value.FlankRearExposureScore,
            value.SupportDistance,
            value.BacklineAccessibility);

    private sealed record SiteSelection(string ChapterId, string SiteId);
    private sealed record PlacementSelection(FormationPlacement Placement, string Profile);
    private sealed record SemanticSwap(PlacementSelection Baseline, PlacementSelection Candidate);
    private sealed record SelectedComposition(
        BuildCombination Build,
        string ConceptVariantId,
        PlacementSelection SemanticBaseline,
        PlacementSelection SemanticCandidate,
        PlacementSelection ProfileBaseline,
        PlacementSelection ProfileCandidate);
}
