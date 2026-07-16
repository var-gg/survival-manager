using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SM.HeadlessCensus;

/// <summary>495 build, 360 formation, 8 medoid와 구조 report를 결정적 파일로 쓴다.</summary>
public static class BuildSpaceArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public sealed record ArtifactSet(
        string BuildSpaceCsvPath,
        string FormationSpaceCsvPath,
        string FormationMedoidsCsvPath,
        string CensusReportPath);

    public static ArtifactSet Write(string outputDirectory, BuildSpaceCensus census)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("output directory is empty.", nameof(outputDirectory));
        }

        if (census == null)
        {
            throw new ArgumentNullException(nameof(census));
        }

        Directory.CreateDirectory(outputDirectory);
        var buildSpacePath = Path.Combine(outputDirectory, "build-space.csv");
        var formationSpacePath = Path.Combine(outputDirectory, "formation-space.csv");
        var medoidPath = Path.Combine(outputDirectory, "formation-medoids.csv");
        var reportPath = Path.Combine(outputDirectory, "census-report.json");
        File.WriteAllText(buildSpacePath, BuildCombinationCsv(census), Utf8WithoutBom);
        File.WriteAllText(formationSpacePath, BuildFormationCsv(census.Formations), Utf8WithoutBom);
        File.WriteAllText(medoidPath, BuildMedoidCsv(census.Medoids), Utf8WithoutBom);
        var report = new ReportDocument(
            "h100-build-space-census-v1",
            census.Summary,
            census.Medoids.OrderBy(medoid => medoid.Placement.Signature, StringComparer.Ordinal)
                .Select((medoid, index) => new MedoidDocument(
                    index + 1,
                    medoid.Placement.Signature,
                    medoid.ClusterSize,
                    medoid.TotalDistance,
                    medoid.Placement.Features))
                .ToArray());
        File.WriteAllText(reportPath, BuildSpaceJson.Serialize(report) + "\n", Utf8WithoutBom);
        return new ArtifactSet(buildSpacePath, formationSpacePath, medoidPath, reportPath);
    }

    private static string BuildCombinationCsv(BuildSpaceCensus census)
    {
        var rows = new List<string>
        {
            "build_index,build_id,archetype_ids,race_signature,class_signature,synergy_signature,doctrine_rule_ids,tank_count,damage_count,ranged_count,healer_count,distinct_race_count,distinct_class_count,race_tier2_count,class_tier2_count,class_tier3_count,race_tier4_count,exactly_three_race,race_two_plus_two,class_two_plus_two,role_complete,placement_count"
        };
        rows.AddRange(census.Combinations.OrderBy(build => build.BuildIndex).Select(build => string.Join(",", new[]
        {
            Number(build.BuildIndex), Csv(build.BuildId), Csv(build.ArchetypeSignature), Csv(build.RaceSignature),
            Csv(build.ClassSignature), Csv(build.Synergy.Signature), Csv(string.Join(";", build.Synergy.DoctrineRuleIds)),
            Number(build.Roles.TankCount), Number(build.Roles.DamageCount), Number(build.Roles.RangedCount),
            Number(build.Roles.HealerCount), Number(build.DistinctRaceCount), Number(build.DistinctClassCount),
            Number(build.Synergy.RaceTier2Count), Number(build.Synergy.ClassTier2Count),
            Number(build.Synergy.ClassTier3Count), Number(build.Synergy.RaceTier4Count), Bool(build.HasExactRaceThree),
            Bool(build.IsRaceTwoPlusTwo), Bool(build.IsClassTwoPlusTwo), Bool(build.Roles.IsRoleComplete),
            Number(census.Formations.Count),
        })));
        return string.Join("\n", rows) + "\n";
    }

    private static string BuildFormationCsv(IEnumerable<FormationPlacement> placements)
    {
        var rows = new List<string>
        {
            "placement_index,signature,role_anchor_ids,frontline_count,protected_slot_count,side_exposure_count,rear_exposure_count,flank_rear_exposure_score,support_distance,backline_accessibility"
        };
        rows.AddRange(placements.OrderBy(placement => placement.PlacementIndex)
            .Select(placement => FormationRow(placement.PlacementIndex, placement, null, null)));
        return string.Join("\n", rows) + "\n";
    }

    private static string BuildMedoidCsv(IEnumerable<FormationMedoid> medoids)
    {
        var rows = new List<string>
        {
            "medoid_index,placement_index,signature,role_anchor_ids,cluster_size,total_distance,frontline_count,protected_slot_count,side_exposure_count,rear_exposure_count,flank_rear_exposure_score,support_distance,backline_accessibility"
        };
        rows.AddRange(medoids.OrderBy(medoid => medoid.Placement.Signature, StringComparer.Ordinal)
            .Select((medoid, index) => FormationRow(index + 1, medoid.Placement, medoid.ClusterSize, medoid.TotalDistance)));
        return string.Join("\n", rows) + "\n";
    }

    private static string FormationRow(int index, FormationPlacement placement, int? clusterSize, double? totalDistance)
    {
        var features = placement.Features;
        var prefix = clusterSize.HasValue
            ? new[]
            {
                Number(index), Number(placement.PlacementIndex), Csv(placement.Signature),
                Csv(string.Join(";", placement.AnchorsByMemberIndex.Select(anchor => ((int)anchor).ToString(CultureInfo.InvariantCulture)))),
                Number(clusterSize.Value), Number(totalDistance ?? 0d),
            }
            : new[]
            {
                Number(index), Csv(placement.Signature),
                Csv(string.Join(";", placement.AnchorsByMemberIndex.Select(anchor => ((int)anchor).ToString(CultureInfo.InvariantCulture)))),
            };
        return string.Join(",", prefix.Concat(new[]
        {
            Number(features.FrontlineCount), Number(features.ProtectedSlotCount), Number(features.SideExposureCount),
            Number(features.RearExposureCount), Number(features.FlankRearExposureScore), Number(features.SupportDistance),
            Number(features.BacklineAccessibility),
        }));
    }

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";

    private sealed record ReportDocument(
        string SchemaVersion,
        BuildSpaceSummary Summary,
        IReadOnlyList<MedoidDocument> Medoids);

    private sealed record MedoidDocument(
        int MedoidIndex,
        string Signature,
        int ClusterSize,
        double TotalDistance,
        FormationFeatures Features);
}
