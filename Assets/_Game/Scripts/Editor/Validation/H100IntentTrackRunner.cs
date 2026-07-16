using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>coverage campaign의 실제 offer stream을 종료 후 E05 oracle과 BT6/BT7 집계로 연결한다.</summary>
public static class H100IntentTrackRunner
{
    private const string GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-v1.json";
    private const string Bt1GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-bt1-v1.json";
    private const string AgencyWindowDefinition =
        "A player choice point; v1 records one deployment choice and one reward choice per reached campaign site.";
    private const string V1LeverCaveat =
        "V1 exposes deployment and three-card reward choices only. Recruit, level-node, and refit levers remain parameterized but unobserved until E07, so agency gaps can be overstated.";
    private const string RightSizeNote =
        "Owner coverage uses the E03-stable first variant for every anchor. Derived coverage samples the largest isomorphic medoid clusters first; search keeps contract-relevant state and one best path per memoized state.";

    public static void RunFromCli()
    {
        var settings = H100IntentTrackRunSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100IntentTrackRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var legacySpec = H100GateSpec.LoadFromFile(Path.Combine(projectRoot, GateSpecRelativePath));
        var bt1Spec = H100Bt1GateSpec.LoadFromFile(Path.Combine(projectRoot, Bt1GateSpecRelativePath));
        var catalog = H100ConceptCatalogRunner.DeriveForSnapshot(snapshot);
        if (catalog.OwnerAnchors.Count != 10 || catalog.AnchorDerivations.Count != 10)
        {
            throw new InvalidOperationException(
                $"Intent-track owner coverage requires 10 anchors (anchors={catalog.OwnerAnchors.Count}, derivations={catalog.AnchorDerivations.Count}).");
        }

        var census = BuildSpaceEnumerator.Generate(H100BuildSpaceContentAdapter.BuildCanonicalRosterFromSnapshot(snapshot));
        var surfaceAudit = H100SurfaceAuditRunner.RunForSnapshot(snapshot, outputDirectory);
        ConceptCatalogArtifactWriter.Write(outputDirectory, catalog);
        var targets = SelectTargets(catalog, settings.SystemMedoidSampleCount);
        var runRecords = new List<IntentTrackRunRecord>(targets.Count * settings.SeedCount);
        var audits = new List<PlayerVisibleFactAuditResult>(targets.Count);
        var traces = new List<IntentTraceRecord>();

        foreach (var target in targets)
        {
            RunTarget(
                target,
                settings,
                lookup,
                snapshot,
                census.Formations,
                legacySpec.TargetBattleSeconds,
                surfaceAudit,
                runRecords,
                audits,
                traces);
        }

        var report = IntentTrackMetricsCalculator.Calculate(
            runRecords,
            settings.SeedBase,
            settings.SeedCount,
            catalog.OwnerAnchors.Count,
            catalog.SystemDerivedMedoids.Count,
            settings.SystemMedoidSampleCount,
            settings.EnabledLeverIds,
            AgencyWindowDefinition,
            V1LeverCaveat,
            RightSizeNote,
            IntentTrackSearchResult.CurrentEvaluatorVersion);
        var observations = CombineBt2(audits)
            .Concat(surfaceAudit.ToBt3Observations())
            .Concat(IntentTrackMetricsCalculator.ToBt67Observations(report))
            .ToArray();
        var bt1Report = H100Bt1GateEvaluator.Generate(bt1Spec, observations);
        var bt6 = bt1Report.Gates.Single(value => value.GateId == "BT6");
        var bt7 = bt1Report.Gates.Single(value => value.GateId == "BT7");
        report = report with { Bt6Status = bt6.Status, Bt7Status = bt7.Status };
        var reportPath = IntentTrackReportWriter.Write(outputDirectory, report);
        H100Bt1GateReportWriter.Write(outputDirectory, bt1Report);
        IntentTraceArtifactWriter.Write(outputDirectory, traces);

        Debug.Log(
            $"[H100IntentTrack] owners={catalog.OwnerAnchors.Count}x{settings.SeedCount} "
            + $"medoids={settings.SystemMedoidSampleCount}x{settings.SeedCount} runs={runRecords.Count} "
            + $"bt6={bt6.Status} bt7={bt7.Status} gaps=[{string.Join(",", report.GapDistribution.Select(value => $"{value.Id}:{value.Count}"))}] "
            + $"output={reportPath}");
    }

    private static void RunTarget(
        H100IntentTrackTarget target,
        H100IntentTrackRunSettings settings,
        RuntimeCombatContentLookup lookup,
        CombatContentSnapshot snapshot,
        IReadOnlyList<FormationPlacement> formations,
        float targetBattleSeconds,
        InformationSurfaceAuditResult surfaceAudit,
        ICollection<IntentTrackRunRecord> runRecords,
        ICollection<PlayerVisibleFactAuditResult> audits,
        ICollection<IntentTraceRecord> allTraces)
    {
        var collector = new H100IntentTrackCaptureCollector(target.Contract, snapshot, formations);
        var runSettings = new H100MetricsRunSettings(
            BattleCount: 1,
            CampaignCount: settings.SeedCount,
            ReplayCopies: 2,
            SeedBase: settings.SeedBase,
            CampaignSiteSafety: settings.CampaignSiteSafety,
            MaxBattleSteps: settings.MaxBattleSteps,
            WriteCsv: false,
            OutputDirectory: settings.OutputDirectory,
            PolicyId: ConceptCommitPolicy.PolicyId);
        var intent = H100ConceptIntentProjector.Project(
            target.Contract,
            $"coverage-{target.ConceptId}-{target.VariantId}");
        var corpus = H100CampaignCorpusRunner.Run(
            lookup,
            runSettings,
            targetBattleSeconds,
            decisionLog: null,
            collector.Hooks,
            _ => new ConceptCommitPolicy(intent));
        if (corpus.Campaigns.Count != settings.SeedCount)
        {
            throw new InvalidOperationException(
                $"Intent-track campaign count mismatch for {target.ConceptId}: {corpus.Campaigns.Count}/{settings.SeedCount}");
        }

        audits.Add(corpus.FactAudit);
        foreach (var trace in corpus.IntentTraces) allTraces.Add(trace);
        var tracesByCampaign = corpus.IntentTraces
            .GroupBy(value => value.CampaignId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(value => value.DecidedAt.DecisionIndex).ToArray(), StringComparer.Ordinal);
        var relevantSurfaceGap = H100IntentTrackSurfaceJoin.HasRelevantGap(
            target.Contract,
            target.RecipeComponentIds,
            surfaceAudit.Gaps);
        var policyTargetScore = PolicyTargetScore(target.Contract);
        foreach (var campaign in corpus.Campaigns.OrderBy(value => value.CampaignId, StringComparer.Ordinal))
        {
            var capture = collector.Require(campaign.CampaignId);
            var campaignTraces = tracesByCampaign.TryGetValue(campaign.CampaignId, out var found)
                ? found
                : Array.Empty<IntentTraceRecord>();
            var commit = campaignTraces.FirstOrDefault(value => value.IsCommit);
            var commitIndex = commit?.DecidedAt.DecisionIndex ?? 0;
            var windows = capture.Windows.OrderBy(value => value.WindowIndex).ToArray();
            var input = new IntentTrackSearchInput(
                target.Contract,
                capture.InitialState!,
                windows,
                settings.EnabledLeverIds,
                commitIndex,
                windows.Count(value => value.WindowIndex >= commitIndex));
            var search = IntentTrackEvaluator.Evaluate(input);
            var realizationTrace = campaignTraces.FirstOrDefault(value =>
                value.DecidedAt.DecisionIndex >= commitIndex
                && value.IntentState.ProgressScore >= policyTargetScore);
            var policyRealized = realizationTrace != null;
            var realizationWindow = realizationTrace?.DecidedAt.DecisionIndex ?? -1;
            var realizedBeforeFinalTwenty = policyRealized
                                            && realizationWindow < windows.Length * 0.80d;
            var realizationBattleStart = policyRealized
                ? windows.Where(value => value.WindowIndex <= realizationWindow)
                    .OrderByDescending(value => value.WindowIndex)
                    .Select(value => value.BattleOpportunityStartIndex)
                    .FirstOrDefault()
                : campaign.BattleCount;
            var payoffRunway = policyRealized ? Math.Max(0, campaign.BattleCount - realizationBattleStart) : 0;
            var payoffWitnessed = policyRealized && capture.Battles
                .Where(value => value.BattleIndex >= realizationBattleStart)
                .Any(value => value.PayoffWitnessIds.Contains(target.Contract.PayoffWitness, StringComparer.Ordinal));
            var counterTraces = campaignTraces.Where(value =>
                    value.DecidedAt.DecisionIndex >= commitIndex
                    && string.Equals(value.Reason, IntentDecisionReason.CounterAdapt, StringComparison.Ordinal))
                .ToArray();
            var warningIssued = campaignTraces.Any(value =>
                value.DecidedAt.DecisionIndex >= commitIndex
                && (string.Equals(value.Reason, IntentDecisionReason.Pivot, StringComparison.Ordinal)
                    || string.Equals(value.Reason, IntentDecisionReason.Abandon, StringComparison.Ordinal)));
            var irreversibleContinued = campaignTraces.Any(value =>
                value.DecidedAt.DecisionIndex >= commitIndex
                && string.Equals(value.DecisionKind, "reward", StringComparison.Ordinal));
            var silentDeadEnd = !search.TrackAvailable
                                && commit != null
                                && irreversibleContinued
                                && !warningIssued;
            var gap = IntentTrackGapClassifier.Classify(
                search.TrackAvailable,
                policyRealized,
                relevantSurfaceGap,
                payoffWitnessed);
            runRecords.Add(new IntentTrackRunRecord
            {
                RunId = $"{target.ConceptKind}:{target.ConceptId}:s{(settings.SeedBase + capture.CampaignIndex).ToString("D4", CultureInfo.InvariantCulture)}",
                ConceptId = target.ConceptId,
                ConceptKind = target.ConceptKind,
                AvailabilityTier = target.Contract.AvailabilityTier,
                Seed = campaign.Seed,
                AgencyWindowCount = windows.Length,
                BattleCount = campaign.BattleCount,
                OfferStreamHash = HashOfferStream(windows),
                TrackAvailable = search.TrackAvailable,
                FirstProgressTime = search.FirstProgressTime,
                OracleRealizationTime = search.RealizationTime,
                MaxAgencyDrought = search.MaxAgencyDrought,
                Starved = search.Starved,
                PolicyCommitted = commit != null,
                PolicyRealized = policyRealized,
                PolicyRealizationWindowIndex = realizationWindow,
                RealizedBeforeFinalTwentyPercent = realizedBeforeFinalTwenty,
                PayoffRunway = payoffRunway,
                PayoffWitnessed = payoffWitnessed,
                CounterDecisionCount = counterTraces.Length,
                IdentityRetainedCounterDecisionCount = counterTraces.Count(value => value.IntentState.ProgressScore >= policyTargetScore),
                WarningIssued = warningIssued,
                SilentDeadEnd = silentDeadEnd,
                RelevantSurfaceGap = relevantSurfaceGap,
                GapKind = gap,
            });
        }

        Debug.Log(
            $"[H100IntentTrack] target={target.ConceptKind}/{target.ConceptId} "
            + $"campaigns={corpus.Campaigns.Count} traces={corpus.IntentTraces.Count}");
    }

    private static IReadOnlyList<H100IntentTrackTarget> SelectTargets(ConceptCatalog catalog, int medoidSampleCount)
    {
        var targets = new List<H100IntentTrackTarget>();
        foreach (var derivation in catalog.AnchorDerivations.OrderBy(value => value.AnchorId, StringComparer.Ordinal))
        {
            var variant = derivation.Variants.FirstOrDefault();
            if (derivation.DerivationGap || variant == null)
            {
                throw new InvalidOperationException($"Owner anchor has no E05 representative: {derivation.AnchorId}");
            }

            targets.Add(new H100IntentTrackTarget(
                derivation.AnchorId,
                "owner_anchor",
                variant.VariantId,
                variant.Contract,
                variant.MedoidRecipe.ComponentIds));
        }

        foreach (var variant in catalog.SystemDerivedMedoids
                     .OrderByDescending(value => value.IsomorphicRecipeCount)
                     .ThenBy(value => value.VariantId, StringComparer.Ordinal)
                     .Take(medoidSampleCount))
        {
            targets.Add(new H100IntentTrackTarget(
                variant.VariantId,
                "system_medoid",
                variant.VariantId,
                variant.Contract,
                variant.MedoidRecipe.ComponentIds));
        }

        return targets;
    }

    private static IReadOnlyList<H100GateEvaluator.ExternalObservation> CombineBt2(
        IEnumerable<PlayerVisibleFactAuditResult> audits)
        => audits.SelectMany(value => value.ToBt2Observations())
            .GroupBy(value => value.MetricId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new H100GateEvaluator.ExternalObservation(
                group.Key,
                group.Sum(value => value.Value),
                group.Sum(value => value.SampleCount),
                "all E05 coverage campaign fact ledgers"))
            .ToArray();

    private static int PolicyTargetScore(ConceptContract contract)
        => contract.IdentityPredicates.Sum(value => TryParseCountThreshold(value, out var threshold) ? threshold : 1);

    private static bool TryParseCountThreshold(string value, out int threshold)
    {
        threshold = 0;
        const string marker = ")>=";
        var index = value.IndexOf(marker, StringComparison.Ordinal);
        return value.StartsWith("build.count_tag(", StringComparison.Ordinal)
               && index >= 0
               && int.TryParse(value.Substring(index + marker.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out threshold);
    }

    private static string HashOfferStream(IReadOnlyList<IntentTrackAgencyWindow> windows)
    {
        var bytes = Encoding.UTF8.GetBytes(HeadlessMetricJson.Serialize(windows));
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 intent-track output must stay inside project root: {candidate}");
        }

        return candidate;
    }

    private sealed record H100IntentTrackTarget(
        string ConceptId,
        string ConceptKind,
        string VariantId,
        ConceptContract Contract,
        IReadOnlyList<string> RecipeComponentIds);
}
