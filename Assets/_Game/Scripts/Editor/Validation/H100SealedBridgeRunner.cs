using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SM.Editor.SeedData;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.SealedLlmBridge;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>실 H100 campaign 한 건을 sealed bridge capture로 실행하고 trace/witness를 기록한다.</summary>
public static class H100SealedBridgeRunner
{
    private const string GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-v1.json";
    private const string Bt1GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-bt1-v1.json";
    private const string DefaultOutputDirectory = "Logs/h100-sealed-bridge";
    private const string TraceFileName = "sealed-decision-trace-v1.json";
    private const string RebuiltTraceFileName = "rebuilt-trace.json";
    private const string ReplayEnvironmentFileName = "replay-env.json";
    private const string Bt1ReplayWitnessFileName = "bt1-replay-witness.json";
    private const string PairedWitnessFileName = "paired-witness.json";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private static readonly string[] ComparedCampaignMetricFields =
    {
        "schema_version",
        "run_id",
        "campaign_id",
        "policy_id",
        "difficulty_id",
        "seed",
        "completed",
        "truncated",
        "terminal_reason",
        "site_count",
        "battle_count",
        "win_count",
        "loss_count",
        "timeout_count",
        "stomp_count",
        "forced_timeout_count",
        "total_battle_seconds",
        "decision_count",
        "decision_metrics_available",
        "important_decision_count",
        "near_best_alternative_decision_count",
        "high_leverage_decision_count",
        "macro_family_id",
        "build_family_selection_counts",
        "crash_count",
        "softlock_count",
        "non_finite_state_count",
        "illegal_negative_state_count",
        "non_terminating_battle_count",
        "replay_manifest_hash",
    };

    public static void RunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100SealedBridgeRunner));
        var settings = ResolveSettings();
        var targetBattleSeconds = ResolveTargetBattleSeconds();
        var result = Capture(CreateLookup(), settings, targetBattleSeconds);
        Debug.Log(
            $"[H100SealedBridge] capture complete terminal={result.TerminalFailure} "
            + $"entries={result.Trace.Entries.Count.ToString(CultureInfo.InvariantCulture)} "
            + $"trace={result.TracePath}");
    }

    /// <summary>SM_H100_TRACE_PATH의 sealed trace를 fresh campaign으로 재도출하고 exact manifest를 검증한다.</summary>
    public static void RunReplayFromCli()
    {
        var forceCulture = Environment.GetEnvironmentVariable("SM_H100_FORCE_CULTURE");
        if (!string.IsNullOrWhiteSpace(forceCulture))
        {
            var culture = CultureInfo.GetCultureInfo(forceCulture);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100SealedBridgeRunner));
        var requestedTracePath = Environment.GetEnvironmentVariable("SM_H100_TRACE_PATH");
        if (string.IsNullOrWhiteSpace(requestedTracePath))
        {
            throw new InvalidOperationException("SM_H100_TRACE_PATH must identify a sealed decision trace.");
        }

        var tracePath = Path.GetFullPath(
            Path.IsPathRooted(requestedTracePath)
                ? requestedTracePath
                : Path.Combine(ProjectRoot(), requestedTracePath));
        if (!File.Exists(tracePath))
        {
            throw new FileNotFoundException("Sealed decision trace does not exist.", tracePath);
        }

        var sealedTrace = HeadlessMetricJson.Deserialize<SealedDecisionTraceV1>(File.ReadAllText(tracePath));
        var result = Replay(
            CreateLookup(),
            ResolveSettings(),
            ResolveTargetBattleSeconds(),
            sealedTrace);
        if (!result.Passed)
        {
            throw new InvalidOperationException(
                "H100 sealed replay diverged: "
                + $"reason={result.Verification.FirstDivergenceReason} "
                + $"detail={result.Verification.FirstDivergenceDetail} "
                + $"manifest_equal={result.ManifestEqual}");
        }

        Debug.Log(
            $"[H100SealedBridge] replay PASS verification=true manifest_equal=true "
            + $"entries={result.RebuiltTrace.Entries.Count.ToString(CultureInfo.InvariantCulture)} "
            + $"manifest={result.RebuiltManifestHash} trace={tracePath}");

        var replayResultDirectory = Environment.GetEnvironmentVariable("SM_H100_REPLAY_RESULT_DIR");
        if (!string.IsNullOrWhiteSpace(replayResultDirectory))
        {
            WriteReplayArtifacts(replayResultDirectory, result, forceCulture);
        }
    }

    /// <summary>persisted capture/replay bytes를 다시 읽어 BT1 세 metric과 gate report를 생성한다.</summary>
    public static void RunBt1GateFromCli()
    {
        var requestedTracePath = RequireEnvironment("SM_H100_TRACE_PATH");
        var tracePath = Path.GetFullPath(
            Path.IsPathRooted(requestedTracePath)
                ? requestedTracePath
                : Path.Combine(ProjectRoot(), requestedTracePath));
        if (!File.Exists(tracePath))
        {
            throw new FileNotFoundException("Sealed decision trace does not exist.", tracePath);
        }

        var replayResultDirectories = RequireEnvironment("SM_H100_REPLAY_RESULT_DIRS")
            .Split(';')
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
        if (replayResultDirectories.Length == 0)
        {
            throw new InvalidOperationException("SM_H100_REPLAY_RESULT_DIRS must identify at least one replay directory.");
        }

        var outputDirectory = ResolveOutputDirectory(RequireEnvironment("SM_H100_SEALED_OUTPUT"));
        var sealedTraceBytes = File.ReadAllBytes(tracePath);
        var sealedTrace = HeadlessMetricJson.Deserialize<SealedDecisionTraceV1>(
            Utf8WithoutBom.GetString(sealedTraceBytes));
        var inputs = replayResultDirectories.Select(ReadReplayInput).ToArray();
        var aggregate = Bt1ReplayAggregate.Aggregate(sealedTrace, sealedTraceBytes, inputs);
        var specPath = Path.GetFullPath(Path.Combine(ProjectRoot(), Bt1GateSpecRelativePath));
        var spec = H100Bt1GateSpec.LoadFromFile(specPath);
        var report = H100Bt1GateEvaluator.Generate(
            spec,
            aggregate.ToBt1Observations().ToArray(),
            legacyReport: null);
        var reportPath = H100Bt1GateReportWriter.Write(outputDirectory, report);
        var witnessPath = Path.Combine(outputDirectory, Bt1ReplayWitnessFileName);
        File.WriteAllText(
            witnessPath,
            HeadlessMetricJson.Serialize(aggregate) + "\n",
            Utf8WithoutBom);

        var bt1 = report.Gates.SingleOrDefault(gate =>
            string.Equals(gate.GateId, "BT1", StringComparison.Ordinal));
        if (bt1 == null)
        {
            throw new InvalidOperationException("H100 BT1 gate report does not contain BT1.");
        }

        if (!string.Equals(bt1.Status, "pass", StringComparison.Ordinal))
        {
            var firstDivergence = aggregate.PerReplay.FirstOrDefault(replay =>
                !replay.VerificationPassed || !replay.ByteIdentical);
            var divergence = firstDivergence == null
                ? "none"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "pid={0} reason={1} detail={2} byte_identical={3}",
                    firstDivergence.Fingerprint.ProcessId,
                    firstDivergence.FirstDivergenceReason,
                    firstDivergence.FirstDivergenceDetail,
                    firstDivergence.ByteIdentical);
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "H100 BT1 replay gate failed: independent_process_replay_count={0} "
                + "sealed_llm_decision_trace_replay_match_rate={1:R} "
                + "state_event_result_hash_match_rate={2:R} first_divergence={3}",
                aggregate.IndependentProcessReplayCount,
                aggregate.SealedLlmDecisionTraceReplayMatchRate,
                aggregate.StateEventResultHashMatchRate,
                divergence));
        }

        Debug.Log(
            $"[H100SealedBridge] BT1_GATE pass "
            + $"count={aggregate.IndependentProcessReplayCount.ToString(CultureInfo.InvariantCulture)} "
            + $"match={aggregate.SealedLlmDecisionTraceReplayMatchRate.ToString("R", CultureInfo.InvariantCulture)} "
            + $"state_event={aggregate.StateEventResultHashMatchRate.ToString("R", CultureInfo.InvariantCulture)} "
            + $"distinct_culture={aggregate.DistinctAppliedCultureCount.ToString(CultureInfo.InvariantCulture)} "
            + $"report={reportPath}");
    }

    /// <summary>bare factory policy와 byte-transparent stand-in bridge를 같은 seed의 real corpus로 대조한다.</summary>
    public static void RunPairedWitnessFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100SealedBridgeRunner));
        var settings = ResolveSettings();
        var targetBattleSeconds = ResolveTargetBattleSeconds();
        var bareCorpus = H100CampaignCorpusRunner.Run(
            CreateLookup(),
            settings,
            targetBattleSeconds,
            policyFactory: _ => CreateReferencePolicy(settings.PolicyId));
        var captured = Capture(CreateLookup(), settings, targetBattleSeconds);
        if (captured.TerminalFailure || captured.Corpus == null)
        {
            throw new InvalidOperationException("Paired witness bridge run did not produce a normal campaign metric.");
        }

        var bareMetric = RequireSingleCampaign(bareCorpus, "bare scripted-policy");
        var bridgeMetric = RequireSingleCampaign(captured.Corpus, "sealed bridge");
        var bareBytes = Utf8WithoutBom.GetBytes(HeadlessMetricJson.Serialize(bareMetric));
        var bridgeBytes = Utf8WithoutBom.GetBytes(HeadlessMetricJson.Serialize(bridgeMetric));
        if (!bareBytes.SequenceEqual(bridgeBytes))
        {
            throw new InvalidOperationException(
                "Paired witness campaign metrics diverged. "
                + $"bare={Utf8WithoutBom.GetString(bareBytes)} bridge={Utf8WithoutBom.GetString(bridgeBytes)}");
        }

        var outputDirectory = Path.GetDirectoryName(captured.TracePath)
                              ?? throw new InvalidOperationException("Trace output directory is unavailable.");
        var witnessPath = Path.Combine(outputDirectory, PairedWitnessFileName);
        var witness = new PairedWitnessArtifact(
            "H100SealedBridgePairedWitnessV1",
            RealCorpusRun: true,
            ByteIdentical: true,
            settings.SeedBase,
            settings.CampaignSiteSafety,
            settings.PolicyId,
            ComparedCampaignMetricFields,
            bareMetric,
            bridgeMetric,
            captured.TracePath,
            SealedDecisionTraceHash.ComputeManifest(captured.Trace));
        File.WriteAllText(witnessPath, HeadlessMetricJson.Serialize(witness) + "\n", Utf8WithoutBom);

        Debug.Log(
            $"[H100SealedBridge] PAIRED_WITNESS PASS real_corpus=true byte_identical=true "
            + $"campaigns=2 fields={string.Join(",", ComparedCampaignMetricFields)} artifact={witnessPath}");
    }

    internal static CaptureResult Capture(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds)
    {
        var promptManifest = CreatePromptManifest();
        var campaignSeed = H100SessionDriver.DeriveSeed("campaign", settings.SeedBase);
        var builder = new SealedDecisionTraceBuilder(
            settings.RunId,
            "h100-real-campaign-capture-v1",
            campaignSeed,
            ComputeBuildManifestHash(settings, targetBattleSeconds),
            ComputeContentManifestHash(lookup),
            ComputeVisibleSurfaceHash(promptManifest),
            promptManifest.PromptSchemaHash,
            ComputeScorerConfigHash(settings),
            SealedDecisionTraceCaptureSource.SyntheticStandIn);
        var referencePolicy = CreateReferencePolicy(settings.PolicyId);
        var source = new SyntheticStandInSource(referencePolicy, referencePolicy);
        var bridgePolicy = new SealedLlmBridgePolicy(source, builder, promptManifest);
        var coordinator = new CaptureCoordinator(builder);
        H100CampaignCorpusRunner.Corpus? corpus = null;
        var terminalFailure = false;

        try
        {
            corpus = H100CampaignCorpusRunner.Run(
                lookup,
                settings,
                targetBattleSeconds,
                observationHooks: coordinator.Hooks,
                policyFactory: _ => bridgePolicy);
        }
        catch (SealedLlmTerminalFailureException exception)
        {
            coordinator.CompleteTerminalFailure(exception);
            terminalFailure = true;
        }

        var finalSession = coordinator.LastSession
                           ?? throw new InvalidOperationException("Capture run never exposed a campaign session.");
        if (builder.PendingSeamKey != null)
        {
            throw new InvalidOperationException("Capture run ended with an unsealed decision exchange.");
        }

        var finalStateHash = H100SessionStateCanonicalHash.Compute(finalSession);
        bridgePolicy.SealRunReport(
            BuildRunReportRequest(settings, corpus, terminalFailure),
            finalStateHash);
        var trace = builder.Build();
        var verification = SealedDecisionTraceReplayVerifier.Verify(trace, trace);
        if (!verification.VerificationPassed)
        {
            throw new InvalidOperationException(
                $"Sealed trace self-verification failed: {verification.FirstDivergenceReason} {verification.FirstDivergenceDetail}");
        }

        var tracePath = WriteTrace(settings.OutputDirectory, trace);
        return new CaptureResult(trace, corpus, terminalFailure, tracePath);
    }

    /// <summary>
    /// Re-runs the real campaign with sealed responses while rebuilding every observation and state hash locally.
    /// Only stable run identity is carried from the seal; environment/configuration hashes are recomputed here.
    /// </summary>
    internal static ReplayResult Replay(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        SealedDecisionTraceV1 sealedTrace)
    {
        if (lookup == null) throw new ArgumentNullException(nameof(lookup));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (sealedTrace == null) throw new ArgumentNullException(nameof(sealedTrace));
        var sealedHeader = sealedTrace.Header
                           ?? throw new ArgumentException("Sealed trace header is required.", nameof(sealedTrace));

        var replayCampaignSeed = H100SessionDriver.DeriveSeed("campaign", settings.SeedBase);
        if (replayCampaignSeed != sealedHeader.CampaignSeed)
        {
            throw new InvalidOperationException(
                "Replay settings do not derive the sealed campaign seed: "
                + $"sealed={sealedHeader.CampaignSeed.ToString(CultureInfo.InvariantCulture)} "
                + $"replayed={replayCampaignSeed.ToString(CultureInfo.InvariantCulture)}.");
        }

        var promptManifest = CreatePromptManifest();
        var builder = new SealedDecisionTraceBuilder(
            sealedHeader.RunId,
            sealedHeader.ScenarioId,
            sealedHeader.CampaignSeed,
            ComputeBuildManifestHash(settings, targetBattleSeconds),
            ComputeContentManifestHash(lookup),
            ComputeVisibleSurfaceHash(promptManifest),
            promptManifest.PromptSchemaHash,
            ComputeScorerConfigHash(settings),
            sealedHeader.CaptureSource);
        var source = new SealedTraceSource(sealedTrace);
        var bridgePolicy = new SealedLlmBridgePolicy(source, builder, promptManifest);
        var coordinator = new CaptureCoordinator(builder);
        H100CampaignCorpusRunner.Corpus? corpus = null;
        var terminalFailure = false;

        try
        {
            corpus = H100CampaignCorpusRunner.Run(
                lookup,
                settings,
                targetBattleSeconds,
                observationHooks: coordinator.Hooks,
                policyFactory: _ => bridgePolicy);
        }
        catch (SealedLlmTerminalFailureException exception)
        {
            coordinator.CompleteTerminalFailure(exception);
            terminalFailure = true;
        }

        var finalSession = coordinator.LastSession
                           ?? throw new InvalidOperationException("Replay run never exposed a campaign session.");
        if (builder.PendingSeamKey != null)
        {
            if (source.LastDivergence != null)
            {
                throw source.LastDivergence;
            }

            throw new InvalidOperationException("Replay run ended with an unsealed decision exchange.");
        }

        var finalStateHash = H100SessionStateCanonicalHash.Compute(finalSession);
        bridgePolicy.SealRunReport(
            BuildRunReportRequest(settings, corpus, terminalFailure),
            finalStateHash);
        source.EnsureFullyConsumed();

        var rebuilt = builder.Build();
        var verification = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, rebuilt);
        var sealedManifestHash = SealedDecisionTraceHash.ComputeManifest(sealedTrace);
        var rebuiltManifestHash = SealedDecisionTraceHash.ComputeManifest(rebuilt);
        return new ReplayResult(
            rebuilt,
            verification,
            sealedManifestHash,
            rebuiltManifestHash);
    }

    internal static ReplayWitnessResult RunInProcessReplayWitness(
        RuntimeCombatContentLookup captureLookup,
        RuntimeCombatContentLookup replayLookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds)
    {
        var capture = Capture(captureLookup, settings, targetBattleSeconds);
        if (capture.TerminalFailure)
        {
            throw new InvalidOperationException("Replay witness requires a normally completed capture.");
        }

        var replay = Replay(replayLookup, settings, targetBattleSeconds, capture.Trace);
        return new ReplayWitnessResult(
            replay.VerificationPassed,
            replay.ManifestEqual,
            replay.SealedManifestHash,
            replay.RebuiltManifestHash,
            replay.RebuiltTrace.Entries.Count,
            capture.TracePath,
            replay.Verification.FirstDivergenceReason.ToString(),
            replay.Verification.FirstDivergenceDetail);
    }

    internal static TamperWitnessResult RunTamperWitness(
        RuntimeCombatContentLookup captureLookup,
        Func<RuntimeCombatContentLookup> replayLookupFactory,
        H100MetricsRunSettings settings,
        float targetBattleSeconds)
    {
        if (replayLookupFactory == null) throw new ArgumentNullException(nameof(replayLookupFactory));
        var capture = Capture(captureLookup, settings, targetBattleSeconds);
        if (capture.TerminalFailure)
        {
            throw new InvalidOperationException("Tamper witness requires a normally completed capture.");
        }

        var decisionIndex = FirstDecisionEntryIndex(capture.Trace);
        var observationTampered = MutateEntryBytes(capture.Trace, decisionIndex, response: false);
        var observationReplay = Replay(
            replayLookupFactory(),
            settings,
            targetBattleSeconds,
            observationTampered);
        var observationResult = new SingleTamperResult(
            ReplayFailed: !observationReplay.Passed,
            observationReplay.Verification.FirstDivergenceReason.ToString(),
            observationReplay.Verification.FirstDivergenceDetail);

        var responseTampered = MutateEntryBytes(capture.Trace, decisionIndex, response: true);
        SingleTamperResult responseResult;
        try
        {
            var responseReplay = Replay(
                replayLookupFactory(),
                settings,
                targetBattleSeconds,
                responseTampered);
            responseResult = new SingleTamperResult(
                ReplayFailed: !responseReplay.Passed,
                responseReplay.Verification.FirstDivergenceReason.ToString(),
                responseReplay.Verification.FirstDivergenceDetail);
        }
        catch (SealedTraceSource.DivergenceException exception)
        {
            responseResult = new SingleTamperResult(
                ReplayFailed: true,
                exception.Reason.ToString(),
                exception.Detail);
        }

        var headerTampered = MutateHeaderHash(capture.Trace);
        var headerReplay = Replay(
            replayLookupFactory(),
            settings,
            targetBattleSeconds,
            headerTampered);
        var headerResult = new SingleTamperResult(
            ReplayFailed: !headerReplay.Passed,
            headerReplay.Verification.FirstDivergenceReason.ToString(),
            headerReplay.Verification.FirstDivergenceDetail);

        return new TamperWitnessResult(
            decisionIndex,
            observationResult,
            responseResult,
            headerResult,
            capture.TracePath);
    }

    private static int FirstDecisionEntryIndex(SealedDecisionTraceV1 trace)
    {
        for (var index = 0; index < trace.Entries.Count; index++)
        {
            if (!string.Equals(
                    trace.Entries[index].SeamKey.SeamType,
                    SealedLlmSeamTypes.RunReport,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException("Captured trace has no normal decision entry to tamper.");
    }

    private static SealedDecisionTraceV1 MutateEntryBytes(
        SealedDecisionTraceV1 trace,
        int entryIndex,
        bool response)
    {
        var entries = trace.Entries.Select(CloneTraceEntry).ToArray();
        var entry = entries[entryIndex];
        var original = response
            ? entry.ResponseCanonicalBytes
            : entry.ObservationCanonicalBytes;
        if (original == null || original.Length == 0)
        {
            throw new InvalidOperationException("Tamper witness requires a non-empty canonical payload.");
        }

        var mutated = (byte[])original.Clone();
        mutated[mutated.Length / 2] ^= 0x01;
        RequireSingleByteDifference(original, mutated);
        entries[entryIndex] = response
            ? entry with { ResponseCanonicalBytes = mutated }
            : entry with { ObservationCanonicalBytes = mutated };
        return new SealedDecisionTraceV1(trace.Header with { }, entries);
    }

    private static SealedDecisionTraceV1 MutateHeaderHash(SealedDecisionTraceV1 trace)
    {
        var original = trace.Header.BuildManifestHash;
        if (string.IsNullOrEmpty(original))
        {
            throw new InvalidOperationException("Tamper witness requires a non-empty build manifest hash.");
        }

        var characters = original.ToCharArray();
        characters[0] = characters[0] == '0' ? '1' : '0';
        var mutated = new string(characters);
        RequireSingleByteDifference(
            Utf8WithoutBom.GetBytes(original),
            Utf8WithoutBom.GetBytes(mutated));
        return new SealedDecisionTraceV1(
            trace.Header with { BuildManifestHash = mutated },
            trace.Entries.Select(CloneTraceEntry).ToArray());
    }

    private static SealedDecisionEntry CloneTraceEntry(SealedDecisionEntry entry)
        => entry with
        {
            SeamKey = entry.SeamKey with { },
            ObservationCanonicalBytes = (byte[])entry.ObservationCanonicalBytes.Clone(),
            RequestCanonicalBytes = (byte[])entry.RequestCanonicalBytes.Clone(),
            ResponseCanonicalBytes = (byte[])entry.ResponseCanonicalBytes.Clone(),
        };

    private static void RequireSingleByteDifference(byte[] original, byte[] mutated)
    {
        if (original.Length != mutated.Length)
        {
            throw new InvalidOperationException("Tamper witness changed the payload length.");
        }

        var differenceCount = 0;
        for (var index = 0; index < original.Length; index++)
        {
            if (original[index] != mutated[index])
            {
                differenceCount++;
            }
        }

        if (differenceCount != 1)
        {
            throw new InvalidOperationException(
                $"Tamper witness must flip exactly one byte (actual={differenceCount}).");
        }
    }

    private static H100MetricsRunSettings ResolveSettings()
    {
        var requested = H100MetricsRunSettings.FromEnvironment();
        return requested with
        {
            BattleCount = 1,
            CampaignCount = 1,
            OutputDirectory = Environment.GetEnvironmentVariable("SM_H100_SEALED_OUTPUT")
                              ?? DefaultOutputDirectory,
        };
    }

    internal static float ResolveTargetBattleSeconds()
    {
        var projectRoot = ProjectRoot();
        var specPath = Path.GetFullPath(Path.Combine(projectRoot, GateSpecRelativePath));
        return H100GateSpec.LoadFromFile(specPath).TargetBattleSeconds;
    }

    private static RuntimeCombatContentLookup CreateLookup()
    {
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out _, out var error))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {error}");
        }

        return lookup;
    }

    private static ScriptedReferencePolicy CreateReferencePolicy(string policyId)
        => new(HeadlessPolicyFactory.Create(policyId));

    private static LlmPromptManifestV1 CreatePromptManifest()
        => new(
            ReadEnvironment("SM_H100_PROMPT_TEMPLATE_ID", "h100-sealed-synthetic-v1"),
            ReadEnvironment(
                "SM_H100_PROMPT_TEMPLATE",
                "Choose exactly one canonical selected_action from the supplied player-visible legal menu."),
            ReadEnvironment(
                "SM_H100_COLD_START_BRIEFING",
                "Synthetic stand-in paired witness; no hidden state or inference is available."),
            ReadEnvironment("SM_H100_MODEL_SNAPSHOT", "synthetic-stand-in-v1"),
            ReadEnvironment("SM_H100_DECODING_CONFIG", "temperature=0;top_p=1;synthetic=true"));

    private static string ComputeBuildManifestHash(H100MetricsRunSettings settings, float targetBattleSeconds)
        => HashParts(
            "H100SealedBuildManifestV1",
            settings.RunId,
            settings.SeedBase.ToString(CultureInfo.InvariantCulture),
            settings.CampaignSiteSafety.ToString(CultureInfo.InvariantCulture),
            settings.MaxBattleSteps.ToString(CultureInfo.InvariantCulture),
            targetBattleSeconds.ToString("R", CultureInfo.InvariantCulture),
            settings.PolicyId);

    private static string ComputeContentManifestHash(RuntimeCombatContentLookup lookup)
    {
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {error}");
        }

        var parts = new List<string> { "H100SealedContentManifestV1" };
        AddKeys(parts, "archetype", snapshot.Archetypes.Keys);
        AddKeys(parts, "item", snapshot.ItemCatalog?.Keys);
        AddKeys(parts, "affix", snapshot.AffixCatalog?.Keys);
        AddKeys(parts, "augment", snapshot.AugmentCatalog.Keys);
        AddKeys(parts, "campaign_chapter", snapshot.CampaignChapters?.Keys);
        AddKeys(parts, "expedition_site", snapshot.ExpeditionSites?.Keys);
        AddKeys(parts, "encounter", snapshot.Encounters?.Keys);
        AddKeys(parts, "enemy_squad", snapshot.EnemySquads?.Keys);
        AddKeys(parts, "reward_source", snapshot.RewardSources?.Keys);
        return HashParts(parts.ToArray());
    }

    private static string ComputeVisibleSurfaceHash(LlmPromptManifestV1 manifest)
        => HashParts(
            "H100SealedVisibleSurfaceV1",
            manifest.PromptSchemaHash,
            SealedLlmSeamTypes.Deployment,
            SealedLlmSeamTypes.Reward,
            SealedLlmSeamTypes.Recruit,
            SealedLlmSeamTypes.LevelNode,
            SealedLlmSeamTypes.Refit);

    private static string ComputeScorerConfigHash(H100MetricsRunSettings settings)
        => HashParts("H100SealedScorerConfigV1", "synthetic-stand-in:no-scorer", settings.PolicyId);

    private static byte[] BuildRunReportRequest(
        H100MetricsRunSettings settings,
        H100CampaignCorpusRunner.Corpus? corpus,
        bool terminalFailure)
    {
        using var payload = new MemoryStream();
        AppendPart(payload, "H100SealedRunReportRequestV1");
        AppendPart(payload, settings.RunId);
        AppendPart(payload, terminalFailure ? "terminal_failure" : "completed");
        AppendPart(payload, (corpus?.Campaigns.Count ?? 0).ToString(CultureInfo.InvariantCulture));
        var metric = corpus?.Campaigns.SingleOrDefault();
        AppendPart(payload, metric?.TerminalReason ?? "terminal_failure");
        AppendPart(payload, metric?.ReplayManifestHash ?? string.Empty);
        return payload.ToArray();
    }

    private static void WriteReplayArtifacts(
        string requestedOutputDirectory,
        ReplayResult result,
        string? forceCulture)
    {
        var outputDirectory = ResolveOutputDirectory(requestedOutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(
            Path.Combine(outputDirectory, RebuiltTraceFileName),
            HeadlessMetricJson.Serialize(result.RebuiltTrace) + "\n",
            Utf8WithoutBom);
        var fingerprint = new Bt1ReplayEnvFingerprint(
            string.IsNullOrWhiteSpace(forceCulture) ? CultureInfo.CurrentCulture.Name : forceCulture,
            TimeZoneInfo.Local.Id,
            Directory.GetCurrentDirectory(),
            System.Diagnostics.Process.GetCurrentProcess().Id,
            Environment.MachineName,
            result.SealedManifestHash,
            result.RebuiltManifestHash);
        File.WriteAllText(
            Path.Combine(outputDirectory, ReplayEnvironmentFileName),
            HeadlessMetricJson.Serialize(fingerprint) + "\n",
            Utf8WithoutBom);
    }

    private static Bt1ReplayInput ReadReplayInput(string requestedReplayDirectory)
    {
        var replayDirectory = ResolveOutputDirectory(requestedReplayDirectory);
        var rebuiltTracePath = Path.Combine(replayDirectory, RebuiltTraceFileName);
        var environmentPath = Path.Combine(replayDirectory, ReplayEnvironmentFileName);
        if (!File.Exists(rebuiltTracePath))
        {
            throw new FileNotFoundException("Rebuilt replay trace does not exist.", rebuiltTracePath);
        }

        if (!File.Exists(environmentPath))
        {
            throw new FileNotFoundException("Replay environment fingerprint does not exist.", environmentPath);
        }

        var rebuiltTraceBytes = File.ReadAllBytes(rebuiltTracePath);
        var rebuiltTrace = HeadlessMetricJson.Deserialize<SealedDecisionTraceV1>(
            Utf8WithoutBom.GetString(rebuiltTraceBytes));
        var fingerprint = HeadlessMetricJson.Deserialize<Bt1ReplayEnvFingerprint>(
            File.ReadAllText(environmentPath, Utf8WithoutBom));
        return new Bt1ReplayInput(rebuiltTrace, rebuiltTraceBytes, fingerprint);
    }

    private static string WriteTrace(string requestedOutputDirectory, SealedDecisionTraceV1 trace)
    {
        var outputDirectory = ResolveOutputDirectory(requestedOutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var tracePath = Path.Combine(outputDirectory, TraceFileName);
        File.WriteAllText(tracePath, HeadlessMetricJson.Serialize(trace) + "\n", Utf8WithoutBom);
        return tracePath;
    }

    private static string ResolveOutputDirectory(string requested)
    {
        var projectRoot = ProjectRoot();
        var candidate = Path.GetFullPath(
            Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 sealed output must stay inside project root: {candidate}");
        }

        return candidate;
    }

    private static string ProjectRoot()
        => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static CampaignMetricRecord RequireSingleCampaign(
        H100CampaignCorpusRunner.Corpus corpus,
        string label)
    {
        if (corpus == null) throw new ArgumentNullException(nameof(corpus));
        if (corpus.Campaigns.Count != 1)
        {
            throw new InvalidOperationException(
                $"{label} corpus must contain exactly one campaign metric (actual={corpus.Campaigns.Count}).");
        }

        return corpus.Campaigns[0];
    }

    private static string ReadEnvironment(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string RequireEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        return value;
    }

    private static void AddKeys(List<string> parts, string prefix, IEnumerable<string>? keys)
    {
        foreach (var key in (keys ?? Array.Empty<string>())
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            parts.Add(prefix);
            parts.Add(key);
        }
    }

    private static string HashParts(params string[] values)
    {
        using var payload = new MemoryStream();
        foreach (var value in values)
        {
            AppendPart(payload, value);
        }

        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }

    private static void AppendPart(Stream payload, string value)
        => LengthPrefixedStableHash.AppendPart(payload, value ?? string.Empty);

    private sealed class CaptureCoordinator
    {
        private readonly SealedDecisionTraceBuilder _builder;
        private readonly Dictionary<int, OfferedState> _offered = new();

        public CaptureCoordinator(SealedDecisionTraceBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            Hooks = new H100CampaignObservationHooks(
                DeploymentOffered: context => Stash(
                    context.DecisionIndex,
                    SealedLlmSeamTypes.Deployment,
                    context.Session),
                RewardOffered: context => Stash(
                    context.DecisionIndex,
                    SealedLlmSeamTypes.Reward,
                    context.Session),
                RosterDecisionOffered: context => Stash(
                    context.DecisionIndex,
                    context.LeverId,
                    context.Session),
                DecisionApplied: CompleteApplied);
        }

        public H100CampaignObservationHooks Hooks { get; }
        public GameSessionState? LastSession { get; private set; }

        public void CompleteTerminalFailure(SealedLlmTerminalFailureException exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            var pending = _builder.PendingSeamKey
                          ?? throw new InvalidOperationException("Terminal failure has no pending seam.");
            if (!Equals(pending, exception.SeamKey))
            {
                throw new InvalidOperationException("Terminal failure seam does not match the builder pending seam.");
            }

            var offered = RequireOffered(pending.DecisionIndex, pending.SeamType);
            _builder.CompleteTerminalFailure(pending, offered.PreStateHash);
            _offered.Remove(pending.DecisionIndex);
        }

        private void Stash(int decisionIndex, string seamType, GameSessionState session)
        {
            if (decisionIndex < 0) throw new ArgumentOutOfRangeException(nameof(decisionIndex));
            if (string.IsNullOrWhiteSpace(seamType)) throw new ArgumentException("Seam type is required.", nameof(seamType));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (_offered.ContainsKey(decisionIndex))
            {
                throw new InvalidOperationException($"Decision index {decisionIndex} was offered more than once.");
            }

            LastSession = session;
            _offered.Add(
                decisionIndex,
                new OfferedState(seamType, H100SessionStateCanonicalHash.Compute(session)));
        }

        private void CompleteApplied(H100DecisionAppliedContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var pending = _builder.PendingSeamKey
                          ?? throw new InvalidOperationException("DecisionApplied fired without a pending seam.");
            var expected = new SealedDecisionSeamKey(context.DecisionIndex, context.SeamType, context.Ordinal);
            if (!Equals(pending, expected))
            {
                throw new InvalidOperationException(
                    $"DecisionApplied seam drift: pending={Format(pending)} applied={Format(expected)}.");
            }

            var offered = RequireOffered(context.DecisionIndex, context.SeamType);
            var postStateHash = H100SessionStateCanonicalHash.Compute(context.Session);
            var appliedActionHash = HashParts(
                "H100AppliedActionDescriptorV1",
                context.SeamType,
                context.AppliedActionDescriptor);
            var resultEventHash = HashParts(
                "H100DecisionResultEventV1",
                context.SeamType,
                context.AppliedActionDescriptor,
                context.ResultMarker);
            _builder.Complete(
                pending,
                offered.PreStateHash,
                appliedActionHash,
                resultEventHash,
                postStateHash);
            _offered.Remove(context.DecisionIndex);
            LastSession = context.Session;
        }

        private OfferedState RequireOffered(int decisionIndex, string seamType)
        {
            if (!_offered.TryGetValue(decisionIndex, out var offered))
            {
                throw new InvalidOperationException($"No pre-state hash was stashed for decision {decisionIndex}.");
            }

            if (!string.Equals(offered.SeamType, seamType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Offered seam drift for decision {decisionIndex}: {offered.SeamType} != {seamType}.");
            }

            return offered;
        }

        private static string Format(SealedDecisionSeamKey key)
            => $"{key.DecisionIndex}:{key.SeamType}:{key.Ordinal}";
    }

    private sealed class ScriptedReferencePolicy : IHeadlessPolicy, IHeadlessRosterPolicy
    {
        private readonly IHeadlessPolicy _policy;
        private readonly IHeadlessRosterPolicy? _rosterPolicy;

        public ScriptedReferencePolicy(IHeadlessPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _rosterPolicy = policy as IHeadlessRosterPolicy;
        }

        public string Id => _policy.Id;

        public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
            => _policy.DecideDeployment(observation);

        public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
            => _policy.DecideReward(observation);

        public HeadlessRecruitDecision DecideRecruit(HeadlessRosterPolicyObservation observation)
            => _rosterPolicy?.DecideRecruit(observation)
               ?? new HeadlessRecruitDecision(
                   -1,
                   "factory policy does not opt into recruit changes",
                   0d,
                   Evidence(
                       observation,
                       HeadlessRosterPolicyEvidence.CampaignContextSignal,
                       HeadlessRosterPolicyEvidence.WalletSignal,
                       HeadlessRosterPolicyEvidence.RecruitSurfaceSignal));

        public HeadlessPassiveDecision DecidePassiveAllocation(HeadlessRosterPolicyObservation observation)
            => _rosterPolicy?.DecidePassiveAllocation(observation)
               ?? new HeadlessPassiveDecision(
                   string.Empty,
                   string.Empty,
                   string.Empty,
                   "factory policy does not opt into passive changes",
                   0d,
                   Evidence(
                       observation,
                       HeadlessRosterPolicyEvidence.CampaignContextSignal,
                       HeadlessRosterPolicyEvidence.PassiveSurfaceSignal));

        public HeadlessRefitDecision DecideRefit(HeadlessRosterPolicyObservation observation)
            => _rosterPolicy?.DecideRefit(observation)
               ?? new HeadlessRefitDecision(
                   string.Empty,
                   -1,
                   "factory policy does not opt into refit changes",
                   0d,
                   Evidence(
                       observation,
                       HeadlessRosterPolicyEvidence.CampaignContextSignal,
                       HeadlessRosterPolicyEvidence.WalletSignal,
                       HeadlessRosterPolicyEvidence.RefitSurfaceSignal));

        private static IReadOnlyList<string> Evidence(
            HeadlessRosterPolicyObservation observation,
            params string[] signals)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            var result = new string[signals.Length];
            for (var index = 0; index < signals.Length; index++)
            {
                if (!observation.EvidenceFactIdsBySignal.TryGetValue(signals[index], out var factId)
                    || string.IsNullOrWhiteSpace(factId))
                {
                    throw new HeadlessPolicyEvidenceException(
                        $"Player-visible roster evidence signal '{signals[index]}' is unavailable.");
                }

                result[index] = factId;
            }

            return result;
        }
    }

    private sealed record OfferedState(string SeamType, string PreStateHash);

    internal sealed record CaptureResult(
        SealedDecisionTraceV1 Trace,
        H100CampaignCorpusRunner.Corpus? Corpus,
        bool TerminalFailure,
        string TracePath);

    internal sealed record ReplayResult(
        SealedDecisionTraceV1 RebuiltTrace,
        SealedDecisionTraceReplayResult Verification,
        string SealedManifestHash,
        string RebuiltManifestHash)
    {
        public bool VerificationPassed => Verification.VerificationPassed;

        public bool ManifestEqual
            => string.Equals(SealedManifestHash, RebuiltManifestHash, StringComparison.Ordinal);

        public bool Passed => VerificationPassed && ManifestEqual;
    }

    internal sealed record ReplayWitnessResult(
        bool VerificationPassed,
        bool ManifestEqual,
        string SealedManifestHash,
        string RebuiltManifestHash,
        int EntryCount,
        string TracePath,
        string FirstDivergenceReason,
        string FirstDivergenceDetail)
    {
        public bool Passed => VerificationPassed && ManifestEqual;
    }

    internal sealed record SingleTamperResult(
        bool ReplayFailed,
        string DivergenceReason,
        string DivergenceDetail);

    internal sealed record TamperWitnessResult(
        int TamperedEntryIndex,
        SingleTamperResult Observation,
        SingleTamperResult Response,
        SingleTamperResult HeaderHash,
        string TracePath)
    {
        public bool AllDetected
            => Observation.ReplayFailed
               && Response.ReplayFailed
               && HeaderHash.ReplayFailed;
    }

    private sealed record PairedWitnessArtifact(
        string SchemaVersion,
        bool RealCorpusRun,
        bool ByteIdentical,
        int SeedBase,
        int CampaignSiteSafety,
        string PolicyId,
        IReadOnlyList<string> ComparedFields,
        CampaignMetricRecord BareMetric,
        CampaignMetricRecord BridgeMetric,
        string TracePath,
        string TraceManifestHash);
}
