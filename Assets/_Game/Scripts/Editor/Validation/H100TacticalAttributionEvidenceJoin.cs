using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

/// <summary>Stage 4/E05/E06의 기존 관측을 channel intended-context별로 조인한다.</summary>
internal static class H100TacticalAttributionEvidenceJoin
{
    private static readonly IReadOnlyDictionary<string, string[]> IntendedProfiles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [FormationChannelIds.Flank] = new[] { "baited_gap", "forward_spear", "open_skirmish" },
            [FormationChannelIds.Rear] = new[] { "baited_gap", "forward_spear" },
            [FormationChannelIds.ScreenBlock] = new[] { "fortified_line", "screened_backline" },
            [FormationChannelIds.Save] = new[] { "fortified_line", "screened_backline" },
            [FormationChannelIds.BacklineDiveKill] = new[] { "baited_gap", "forward_spear" },
        };

    private static readonly IReadOnlyDictionary<string, string[]> PreviewRuleProfiles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["standard_role_rows"] = new[] { "open_skirmish" },
            ["stable_priority_screen"] = new[] { "fortified_line", "screened_backline" },
            ["protected_breaker_crossfire"] = new[] { "screened_backline" },
            ["center_break_crossfire"] = new[] { "forward_spear" },
            ["durable_multi_entry_rotation"] = new[] { "baited_gap" },
        };

    public static IReadOnlyList<FormationOptionAttributionEvidence> Build(
        string projectRoot,
        ConceptCatalog catalog,
        H100TacticalAttributionRunSettings settings)
    {
        var formation = Read<FormationEvaluationReport>(projectRoot, settings.FormationReportPath);
        var intent = Read<IntentTrackReport>(projectRoot, settings.IntentTrackReportPath);
        var preview = Read<H100PreviewPolicyAcceptanceReport>(projectRoot, settings.PreviewPolicyReportPath);
        var profilesByVariant = catalog.SystemDerivedMedoids
            .Concat(catalog.AnchorDerivations.SelectMany(value => value.Variants))
            .GroupBy(value => value.VariantId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Fingerprint.FormationProfile, StringComparer.Ordinal);
        var competent = formation.PolicySummaries.FirstOrDefault(value =>
            string.Equals(value.PolicyId, formation.CompetentPolicyId, StringComparison.Ordinal));

        return FormationChannelIds.All.OrderBy(value => value, StringComparer.Ordinal).Select(channelId =>
        {
            var profiles = IntendedProfiles[channelId];
            var variantIds = profilesByVariant
                .Where(value => profiles.Contains(value.Value, StringComparer.Ordinal))
                .Select(value => value.Key)
                .ToHashSet(StringComparer.Ordinal);
            var variantRows = intent.Runs.SelectMany(run => run.VariantResults)
                .Where(value => variantIds.Contains(value.VariantId))
                .ToArray();
            var relevantRuns = intent.Runs.Where(run =>
                    variantIds.Contains(run.RepresentativeVariantId)
                    || variantIds.Contains(run.SelectedTrackVariantId))
                .ToArray();
            var previewRows = preview.PairedCases.Where(value =>
            {
                if (!PreviewRuleProfiles.TryGetValue(value.FormationRule, out var ruleProfiles))
                {
                    return false;
                }

                return ruleProfiles.Any(profile => profiles.Contains(profile, StringComparer.Ordinal));
            }).ToArray();
            var stageFour = competent?.Channels.FirstOrDefault(value =>
                string.Equals(value.ChannelId, channelId, StringComparison.Ordinal));
            return new FormationOptionAttributionEvidence(
                channelId,
                profiles,
                stageFour?.EligibleCount ?? 0,
                stageFour?.FiredCount ?? 0,
                variantRows.Select(value => value.VariantId).Distinct(StringComparer.Ordinal).Count(),
                variantRows.Length,
                variantRows.Count(value => value.TrackAvailable),
                relevantRuns.Count(value => value.PolicyRealized
                                            && variantIds.Contains(value.SelectedTrackVariantId)),
                relevantRuns.Count(value => value.PayoffWitnessed),
                previewRows.Length,
                previewRows.Count(value => value.CounterEvidenceSupported));
        }).ToArray();
    }

    private static T Read<T>(string projectRoot, string requested)
    {
        var path = Resolve(projectRoot, requested);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"BT1-E09 evidence report is missing: {path}", path);
        }

        return HeadlessMetricJson.Deserialize<T>(File.ReadAllText(path));
    }

    private static string Resolve(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"BT1-E09 evidence path must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
