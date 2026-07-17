using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>E06 policy를 captured arrival에 paired replay하고 acceptance 및 BT8 partial supplier를 출력한다.</summary>
public static class H100PreviewPolicyAcceptanceRunner
{
    private const float TargetBattleSeconds = 35f;

    public static void RunFromCli()
    {
        var settings = H100PreviewPolicyAcceptanceSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100PreviewPolicyAcceptanceRunner));
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
        var previewSnapshots = new List<SunkenArrivalSnapshotRecord>();
        var sunkenCandidates = new List<SunkenOracleCandidateRecord>();
        var pairs = new List<H100PreviewPolicyAcceptanceReport.PairedCase>();
        var processedCandidates = 0;

        foreach (var arrival in arrivals
                     .OrderBy(value => value.SiteId, StringComparer.Ordinal)
                     .ThenBy(value => value.SampleId, StringComparer.Ordinal))
        {
            var previewPolicy = ConceptCommitPolicy.CreatePreviewGrounded();
            var previewDecision = previewPolicy.DecideDeployment(arrival.Observation);
            HeadlessPolicyGuard.ValidateDeploymentDecision(arrival.Observation, previewDecision);
            var trace = previewPolicy.LastPreviewDecision
                        ?? throw new InvalidOperationException($"Preview trace missing for '{arrival.SampleId}'.");
            var profile = H100ProfileSnapshotCodec.Restore(arrival.ProfileSnapshot);
            var baselineCaptured = ToCapturedArrival(settings.RunId, arrival, arrival.BaselineDecision, arrival.BaselinePolicyId, "baseline");
            var previewCaptured = ToCapturedArrival(
                settings.RunId,
                arrival,
                previewDecision,
                HeadlessPolicyFactory.PreviewGroundedConceptId,
                "preview");
            var baselineCase = H100SunkenOracleCaseFactory.BuildPolicyChoice(
                profile,
                combatSnapshot,
                arrival.BaselineDecision,
                SunkenOracleCandidateRecord.SameStateScope,
                "arrival");
            var baselineResult = H100SunkenSiteRunner.Run(
                lookup,
                settings.RunId,
                baselineCaptured,
                arrival.ProfileSnapshot,
                baselineCase,
                settings.MaxBattleSteps);

            SunkenOracleCandidateRecord previewResult;
            if (string.Equals(arrival.SiteId, H100SunkenDiagnosisSettings.TargetSiteId, StringComparison.Ordinal))
            {
                previewSnapshots.Add(previewCaptured.Snapshot);
                var cases = H100SunkenOracleCaseFactory.Build(
                    census,
                    combatSnapshot,
                    profile,
                    settings.MedoidCount,
                    settings.OwnedBuildLimit,
                    SunkenOracleCandidateRecord.SameStateScope,
                    "arrival",
                    policyChoice: previewDecision);
                if (cases.Count == 0 || cases.Count(value => value.IsPolicyChoice) != 1)
                {
                    throw new InvalidOperationException($"Sunken acceptance case matrix invalid for '{arrival.SampleId}'.");
                }

                foreach (var acceptanceCase in cases)
                {
                    var result = H100SunkenSiteRunner.Run(
                        lookup,
                        settings.RunId,
                        previewCaptured,
                        arrival.ProfileSnapshot,
                        acceptanceCase,
                        settings.MaxBattleSteps);
                    sunkenCandidates.Add(result);
                    processedCandidates++;
                    if (processedCandidates == 1 || processedCandidates % 250 == 0)
                    {
                        Debug.Log(
                            $"[H100PreviewPolicyAcceptance] candidates={processedCandidates} sample={arrival.SampleId}");
                    }
                }

                previewResult = sunkenCandidates.Last(value =>
                    value.SampleId == previewCaptured.Snapshot.SampleId && value.IsPolicyChoice);
            }
            else
            {
                var previewCase = H100SunkenOracleCaseFactory.BuildPolicyChoice(
                    profile,
                    combatSnapshot,
                    previewDecision,
                    SunkenOracleCandidateRecord.SameStateScope,
                    "arrival");
                previewResult = H100SunkenSiteRunner.Run(
                    lookup,
                    settings.RunId,
                    previewCaptured,
                    arrival.ProfileSnapshot,
                    previewCase,
                    settings.MaxBattleSteps);
            }

            pairs.Add(BuildPair(arrival, trace, previewDecision, baselineResult, previewResult));
        }

        var sunken = SunkenSolvabilityEvaluator.Evaluate(
            settings.RunId,
            H100SunkenDiagnosisSettings.TargetSiteId,
            previewSnapshots,
            sunkenCandidates,
            $"captured-arrivals={previewSnapshots.Count}; owned-builds="
            + (settings.OwnedBuildLimit <= 0 ? "all" : settings.OwnedBuildLimit.ToString())
            + $"; placements=stage3-medoids-{settings.MedoidCount}; paired-site-seeds=true");
        var report = BuildReport(projectRoot, settings, sunken, sunkenCandidates, pairs);
        var artifacts = H100PreviewPolicyAcceptanceArtifactWriter.Write(
            outputDirectory,
            previewSnapshots,
            sunkenCandidates,
            report);
        Debug.Log(
            $"[H100PreviewPolicyAcceptance] status={report.Status} samples={sunken.SnapshotCount} "
            + $"chosen={sunken.ChosenWinRate:F4} regret={sunken.SelectionRegret:F4} "
            + $"heldout_max_degradation={report.HeldOut.Max(value => value.Degradation):F4} "
            + $"unsupported={report.Evidence.UnsupportedCounterDecisionCount} "
            + $"full_reset_rate={report.Reset.UnnecessaryFullResetRate:F4} report={artifacts.ReportPath}");
    }

    private static IReadOnlyList<H100PreviewPolicyArrival> CaptureArrivals(
        RuntimeCombatContentLookup lookup,
        H100PreviewPolicyAcceptanceSettings settings)
    {
        var all = new List<H100PreviewPolicyArrival>();
        foreach (var policyId in settings.BaselinePolicyIds)
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
            var collector = new H100PreviewPolicyArrivalCollector(
                policyId,
                H100PreviewPolicyAcceptanceSettings.TargetSiteIds,
                settings.ArrivalsPerPolicySite);
            H100CampaignCorpusRunner.Run(
                lookup,
                campaignSettings,
                TargetBattleSeconds,
                observationHooks: collector.Hooks);
            all.AddRange(collector.Arrivals);
        }

        var expectedPerSite = settings.BaselinePolicyIds.Count * settings.ArrivalsPerPolicySite;
        var missing = H100PreviewPolicyAcceptanceSettings.TargetSiteIds
            .Where(siteId => all.Count(value => value.SiteId == siteId) != expectedPerSite)
            .ToArray();
        if (missing.Length > 0)
        {
            var counts = string.Join(
                ", ",
                H100PreviewPolicyAcceptanceSettings.TargetSiteIds.Select(siteId =>
                    $"{siteId}={all.Count(value => value.SiteId == siteId)}/{expectedPerSite}"));
            throw new InvalidOperationException($"Preview acceptance arrival matrix incomplete ({counts}).");
        }

        return all;
    }

    private static H100PreviewPolicyAcceptanceReport BuildReport(
        string projectRoot,
        H100PreviewPolicyAcceptanceSettings settings,
        SunkenSolvabilityReport sunken,
        IReadOnlyList<SunkenOracleCandidateRecord> sunkenCandidates,
        IReadOnlyList<H100PreviewPolicyAcceptanceReport.PairedCase> pairs)
    {
        var heldOut = H100PreviewPolicyAcceptanceSettings.HeldOutSiteIds.Select(siteId =>
        {
            var sitePairs = pairs.Where(value => value.SiteId == siteId).ToArray();
            var baseline = Rate(sitePairs.Count(value => value.BaselineCompleted), sitePairs.Length);
            var preview = Rate(sitePairs.Count(value => value.PreviewCompleted), sitePairs.Length);
            return new H100PreviewPolicyAcceptanceReport.HeldOutSummary
            {
                SiteId = siteId,
                SampleCount = sitePairs.Length,
                BaselineCompletionRate = baseline,
                PreviewCompletionRate = preview,
                Degradation = Math.Max(0d, baseline - preview),
            };
        }).ToArray();
        var counterAdapt = pairs.Where(value => value.DecisionReason == IntentDecisionReason.CounterAdapt).ToArray();
        var identityOpportunities = pairs.Where(value => value.IdentityPreservingCandidateAvailable).ToArray();
        var fullResets = identityOpportunities.Count(value => value.FullReset);
        var resetRate = Rate(fullResets, identityOpportunities.Length);
        var technicalFailures = sunkenCandidates.Count(value => !string.IsNullOrWhiteSpace(value.FailureCode))
                                + pairs.Count(value => !string.IsNullOrWhiteSpace(value.BaselineFailureCode))
                                + pairs.Count(value => !string.IsNullOrWhiteSpace(value.PreviewFailureCode));
        var unsupported = counterAdapt.Count(value => !value.CounterEvidenceSupported);
        var checks = new[]
        {
            Check("sunken_chosen_win_rate", "gte", 0.70d, sunken.ChosenWinRate),
            Check("sunken_selection_regret", "lte", 0.25d, sunken.SelectionRegret),
            Check("unsupported_counter_count", "eq", 0d, unsupported),
            Check("heldout_site_count", "gte", 2d, heldOut.Length),
            Check("heldout_max_degradation", "lte", 0.10d, heldOut.Max(value => value.Degradation)),
            Check("unnecessary_full_reset_rate", "lte", 0.20d, resetRate),
            Check("technical_failure_count", "eq", 0d, technicalFailures),
        };
        var bt8 = BuildBt8Partial(projectRoot, sunken);
        return new H100PreviewPolicyAcceptanceReport
        {
            RunId = settings.RunId,
            PolicyId = HeadlessPolicyFactory.PreviewGroundedConceptId,
            Status = checks.All(value => value.Pass) ? "pass" : "fail",
            Sunken = new H100PreviewPolicyAcceptanceReport.SunkenSummary
            {
                SampleCount = sunken.SnapshotCount,
                CandidateCount = sunken.SameStateCandidateCount,
                SameStateOracleWinRate = sunken.SameStateOracleWinRate,
                ChosenWinRate = sunken.ChosenWinRate,
                SelectionRegret = sunken.SelectionRegret,
                WinningBuildCount = sunken.WinningBuildCount,
                WinningPlacementCount = sunken.WinningPlacementCount,
            },
            HeldOut = heldOut,
            Evidence = new H100PreviewPolicyAcceptanceReport.EvidenceSummary
            {
                CounterAdaptDecisionCount = counterAdapt.Length,
                SupportedCounterDecisionCount = counterAdapt.Count(value => value.CounterEvidenceSupported),
                UnsupportedCounterDecisionCount = unsupported,
            },
            Reset = new H100PreviewPolicyAcceptanceReport.ResetSummary
            {
                IdentityPreservingOpportunityCount = identityOpportunities.Length,
                FullResetCount = fullResets,
                UnnecessaryFullResetRate = resetRate,
            },
            TechnicalFailureCount = technicalFailures,
            Checks = checks,
            PairedCases = pairs,
            Bt8Partial = bt8,
        };
    }

    private static H100Bt1GateReport.GateResult BuildBt8Partial(
        string projectRoot,
        SunkenSolvabilityReport sunken)
    {
        var specPath = Path.Combine(
            projectRoot,
            "Assets",
            "_Game",
            "Scripts",
            "Runtime",
            "HeadlessMetrics",
            "h100-gates-bt1-v1.json");
        var spec = H100Bt1GateSpec.LoadFromFile(specPath);
        var observations = new[]
        {
            new H100GateEvaluator.ExternalObservation(
                "oracle_0_8_blocker_chosen_win_rate",
                sunken.ChosenWinRate,
                sunken.SnapshotCount,
                "preview-policy-acceptance.json"),
            new H100GateEvaluator.ExternalObservation(
                "oracle_0_8_blocker_selection_regret",
                sunken.SelectionRegret,
                sunken.SnapshotCount,
                "preview-policy-acceptance.json"),
        };
        return H100Bt1GateEvaluator.Generate(spec, observations).Gates.Single(value => value.GateId == "BT8");
    }

    private static H100PreviewPolicyAcceptanceReport.PairedCase BuildPair(
        H100PreviewPolicyArrival arrival,
        PreviewGroundedDecisionTrace trace,
        HeadlessDeploymentDecision previewDecision,
        SunkenOracleCandidateRecord baseline,
        SunkenOracleCandidateRecord preview)
    {
        var supported = !string.Equals(trace.Reason, IntentDecisionReason.CounterAdapt, StringComparison.Ordinal)
                        || trace.CounterConnections.Count > 0
                        && trace.CounterConnections.All(connection =>
                            EvidenceContains(arrival.Observation, previewDecision, connection.ThreatEvidenceSignalKey)
                            && EvidenceContains(arrival.Observation, previewDecision, connection.HeroEvidenceSignalKey));
        return new H100PreviewPolicyAcceptanceReport.PairedCase
        {
            SampleId = arrival.SampleId,
            SiteId = arrival.SiteId,
            BaselinePolicyId = arrival.BaselinePolicyId,
            BaselineCompleted = baseline.SiteCompleted,
            PreviewCompleted = preview.SiteCompleted,
            BaselineBuildId = baseline.BuildId,
            PreviewBuildId = preview.BuildId,
            BaselinePlacementId = baseline.PlacementId,
            PreviewPlacementId = preview.PlacementId,
            SelectedHeroIds = trace.SelectedHeroIds,
            FormationRule = trace.FormationRule,
            BaselineBattleWinRate = baseline.BattleWinRate,
            PreviewBattleWinRate = preview.BattleWinRate,
            ReplacementCount = trace.ReplacementCount,
            PreviousDeploymentCount = trace.PreviousDeploymentCount,
            FullReset = trace.IsFullReset,
            IdentityPreservingCandidateAvailable = trace.IdentityPreservingCandidateAvailable,
            DecisionReason = trace.Reason,
            ThreatTags = trace.ThreatTags,
            CounterConnectionCount = trace.CounterConnections.Count,
            CounterEvidenceSupported = supported,
            BaselineFailureCode = baseline.FailureCode,
            PreviewFailureCode = preview.FailureCode,
        };
    }

    private static bool EvidenceContains(
        HeadlessPolicyObservation observation,
        HeadlessDeploymentDecision decision,
        string signalKey)
        => observation.EvidenceFactIdsBySignal.TryGetValue(signalKey, out var factId)
           && !string.IsNullOrWhiteSpace(factId)
           && decision.EvidenceFactIds.Contains(factId, StringComparer.Ordinal);

    private static H100SunkenCapturedArrival ToCapturedArrival(
        string runId,
        H100PreviewPolicyArrival arrival,
        HeadlessDeploymentDecision decision,
        string policyId,
        string suffix)
    {
        var snapshot = new SunkenArrivalSnapshotRecord
        {
            RunId = runId,
            SampleId = $"{arrival.SampleId}-{suffix}",
            PolicyId = policyId,
            CampaignSeed = arrival.CampaignSeed,
            SiteIndex = arrival.SiteIndex,
            BattleStartIndex = arrival.BattleStartIndex,
            ChapterId = arrival.Observation.ChapterId,
            SiteId = arrival.SiteId,
            Gold = arrival.Observation.Wallet.Gold,
            Echo = arrival.Observation.Wallet.Echo,
            OwnedArchetypeIds = arrival.Observation.Roster.Select(value => value.ArchetypeId)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ExpeditionSquadHeroIds = arrival.Observation.Roster.Select(value => value.HeroId)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Roster = arrival.Observation.Roster.Select(value => new SunkenArrivalSnapshotRecord.RosterHero
            {
                HeroId = value.HeroId,
                ArchetypeId = value.ArchetypeId,
                RaceId = value.RaceId,
                ClassId = value.ClassId,
                RoleTag = value.RoleTag,
                Level = value.Level,
                CurrentHp = value.CurrentHp,
                MaxHp = value.MaxHp,
                EquippedItemCount = value.EquippedItemCount,
                InExpeditionSquad = true,
            }).ToArray(),
            ChosenPlacements = decision.Placements.OrderBy(value => value.Anchor)
                .Select(value => new SunkenArrivalSnapshotRecord.Placement
                {
                    AnchorId = (int)value.Anchor,
                    HeroId = value.HeroId,
                }).ToArray(),
            ChosenRationale = decision.Rationale,
            ChosenEstimatedValue = decision.EstimatedValue,
            CurrentEncounterId = arrival.Observation.EnemyPreview.EncounterId,
            CurrentEnemyArchetypeIds = arrival.Observation.EnemyPreview.Units.Select(value => value.ArchetypeId)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        };
        return new H100SunkenCapturedArrival(
            snapshot,
            arrival.ProfileSnapshot,
            decision,
            arrival.CampaignSeed,
            arrival.BattleStartIndex,
            null);
    }

    private static H100PreviewPolicyAcceptanceReport.CheckResult Check(
        string id,
        string comparison,
        double expected,
        double actual)
    {
        var pass = comparison switch
        {
            "eq" => Math.Abs(actual - expected) <= 1e-9d,
            "gte" => actual >= expected,
            "lte" => actual <= expected,
            _ => false,
        };
        return new H100PreviewPolicyAcceptanceReport.CheckResult
        {
            CheckId = id,
            Operator = comparison,
            Expected = expected,
            Actual = actual,
            Pass = pass,
        };
    }

    private static double Rate(int numerator, int denominator)
        => denominator <= 0 ? 0d : (double)numerator / denominator;

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 preview output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
