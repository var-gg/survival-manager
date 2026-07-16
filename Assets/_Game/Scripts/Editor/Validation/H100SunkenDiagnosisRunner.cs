using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>Stage 5 Q1: arrival capture → same-state oracle → one-site lookback → Pro 판정표.</summary>
public static class H100SunkenDiagnosisRunner
{
    private const float TargetBattleSeconds = 35f;

    public static void RunFromCli()
    {
        var settings = H100SunkenDiagnosisSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100SunkenDiagnosisRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var combatSnapshot, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var census = BuildSpaceEnumerator.Generate(H100BuildSpaceContentAdapter.BuildCanonicalRoster(lookup));
        CanonicalBuildSpaceContract.RequireExpected(census);
        var arrivals = CaptureArrivals(lookup, settings);
        var snapshots = arrivals.Select(value => value.Snapshot).ToArray();
        var candidates = new List<SunkenOracleCandidateRecord>();
        var processed = 0;
        foreach (var arrival in arrivals.OrderBy(value => value.Snapshot.SampleId, StringComparer.Ordinal))
        {
            var profile = H100ProfileSnapshotCodec.Restore(arrival.ProfileSnapshot);
            var sameStateCases = H100SunkenOracleCaseFactory.Build(
                census,
                combatSnapshot,
                profile,
                settings.MedoidCount,
                settings.OwnedBuildLimit,
                SunkenOracleCandidateRecord.SameStateScope,
                "arrival",
                policyChoice: arrival.ChosenDecision);
            RequireCases(arrival.Snapshot.SampleId, SunkenOracleCandidateRecord.SameStateScope, sameStateCases);
            foreach (var oracleCase in sameStateCases)
            {
                var record = H100SunkenSiteRunner.Run(
                    lookup,
                    settings.RunId,
                    arrival,
                    arrival.ProfileSnapshot,
                    oracleCase,
                    settings.MaxBattleSteps);
                candidates.Add(record);
                processed++;
                LogProgress(processed, arrival.Snapshot.SampleId, record.Scope);
            }

            VerifyPolicyChoiceDeterminism(
                lookup,
                settings,
                arrival,
                sameStateCases.First(value => value.IsPolicyChoice),
                candidates.Last(value => value.SampleId == arrival.Snapshot.SampleId && value.IsPolicyChoice));

            foreach (var variant in H100SunkenLookbackFactory.Build(lookup, arrival))
            {
                var variantProfile = H100ProfileSnapshotCodec.Restore(variant.ProfileSnapshot);
                var lookbackCases = H100SunkenOracleCaseFactory.Build(
                    census,
                    combatSnapshot,
                    variantProfile,
                    settings.MedoidCount,
                    settings.LookbackBuildLimit,
                    SunkenOracleCandidateRecord.LookbackScope,
                    variant.VariantId,
                    variant.AddedRosterArchetypeId,
                    variant.RewardOptionIndex,
                    variant.RewardPayloadId);
                foreach (var oracleCase in lookbackCases)
                {
                    var record = H100SunkenSiteRunner.Run(
                        lookup,
                        settings.RunId,
                        arrival,
                        variant.ProfileSnapshot,
                        oracleCase,
                        settings.MaxBattleSteps,
                        variant);
                    candidates.Add(record);
                    processed++;
                    LogProgress(processed, arrival.Snapshot.SampleId, record.Scope);
                }
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("Sunken diagnosis emitted no oracle candidates.");
        }

        var searchMode = $"small-n={snapshots.Length}; owned-builds="
                         + (settings.OwnedBuildLimit <= 0 ? "all" : settings.OwnedBuildLimit.ToString())
                         + $"; placements=stage3-medoids-{settings.MedoidCount}; "
                         + $"lookback-top-k={settings.LookbackBuildLimit}; paired-site-seeds=true";
        var report = SunkenSolvabilityEvaluator.Evaluate(
            settings.RunId,
            H100SunkenDiagnosisSettings.TargetSiteId,
            snapshots,
            candidates,
            searchMode);
        var artifacts = SunkenSolvabilityArtifactWriter.Write(outputDirectory, snapshots, candidates, report);
        if (report.FailedCandidateCount > 0)
        {
            throw new InvalidOperationException(
                $"Sunken diagnosis has technical candidate failures: {report.FailedCandidateCount}. Report={artifacts.DiagnosisReportPath}");
        }

        Debug.Log(
            $"[H100SunkenDiagnosis] complete snapshots={report.SnapshotCount} candidates={candidates.Count} "
            + $"same_state_oracle={report.SameStateOracleWinRate:F4} regret={report.SelectionRegret:F4} "
            + $"availability_gap={report.AvailabilityGap:F4} lookback={report.OneSiteLookbackOracle:F4} "
            + $"best_counter={report.BestCounterFamily} decision={report.DecisionCell} "
            + $"report={artifacts.DiagnosisReportPath}");
    }

    private static IReadOnlyList<H100SunkenCapturedArrival> CaptureArrivals(
        RuntimeCombatContentLookup lookup,
        H100SunkenDiagnosisSettings settings)
    {
        var arrivalsByPolicy = new Dictionary<string, IReadOnlyList<H100SunkenCapturedArrival>>(StringComparer.Ordinal);
        foreach (var policyId in settings.PolicyIds)
        {
            var campaignSettings = new H100MetricsRunSettings(
                BattleCount: 1,
                CampaignCount: settings.ArrivalSeedAttempts,
                ReplayCopies: 2,
                SeedBase: settings.SeedBase,
                CampaignSiteSafety: settings.CampaignSiteSafety,
                MaxBattleSteps: settings.MaxBattleSteps,
                WriteCsv: false,
                OutputDirectory: settings.OutputDirectory,
                PolicyId: policyId);
            var collector = new H100SunkenCaptureCollector(
                lookup,
                settings.RunId,
                policyId,
                H100SunkenDiagnosisSettings.TargetSiteId,
                settings.CampaignsPerPolicy);
            H100CampaignCorpusRunner.Run(
                lookup,
                campaignSettings,
                TargetBattleSeconds,
                observationHooks: collector.Hooks);
            arrivalsByPolicy[policyId] = collector.Arrivals.ToArray();
        }

        var insufficient = arrivalsByPolicy
            .Where(pair => pair.Value.Count < settings.CampaignsPerPolicy)
            .ToArray();
        if (insufficient.Length > 0)
        {
            var counts = string.Join(
                ", ",
                arrivalsByPolicy.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value.Count}"));
            throw new InvalidOperationException(
                $"Could not capture {settings.CampaignsPerPolicy} sunken arrivals per policy "
                + $"from {settings.ArrivalSeedAttempts} attempts ({counts}).");
        }

        return settings.PolicyIds
            .SelectMany(policyId => arrivalsByPolicy[policyId]
                .OrderBy(value => value.CampaignSeed)
                .Take(settings.CampaignsPerPolicy))
            .ToArray();
    }

    private static void VerifyPolicyChoiceDeterminism(
        RuntimeCombatContentLookup lookup,
        H100SunkenDiagnosisSettings settings,
        H100SunkenCapturedArrival arrival,
        H100SunkenOracleCase policyCase,
        SunkenOracleCandidateRecord first)
    {
        var replay = H100SunkenSiteRunner.Run(
            lookup,
            settings.RunId,
            arrival,
            arrival.ProfileSnapshot,
            policyCase,
            settings.MaxBattleSteps);
        if (first.SiteCompleted != replay.SiteCompleted
            || first.BattleCount != replay.BattleCount
            || first.BattleWinCount != replay.BattleWinCount
            || !first.BattleSeeds.SequenceEqual(replay.BattleSeeds)
            || !string.Equals(first.ReplayManifestHash, replay.ReplayManifestHash, StringComparison.Ordinal)
            || !string.Equals(first.FailureCode, replay.FailureCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Policy-choice replay is not deterministic for sample '{arrival.Snapshot.SampleId}'.");
        }
    }

    private static void RequireCases(
        string sampleId,
        string scope,
        IReadOnlyCollection<H100SunkenOracleCase> cases)
    {
        if (cases.Count == 0)
        {
            throw new InvalidOperationException($"No legal oracle cases for sample='{sampleId}' scope='{scope}'.");
        }
    }

    private static void LogProgress(int processed, string sampleId, string scope)
    {
        if (processed == 1 || processed % 250 == 0)
        {
            Debug.Log($"[H100SunkenDiagnosis] candidates={processed} sample={sampleId} scope={scope}");
        }
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 sunken output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
