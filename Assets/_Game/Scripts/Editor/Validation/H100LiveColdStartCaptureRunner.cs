using System;
using System.Globalization;
using System.IO;
using SM.Editor.SeedData;
using SM.HeadlessMetrics;
using SM.SealedLlmBridge;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>Live and explicitly non-certifying dry-run composition roots for the file handshake source.</summary>
public static class H100LiveColdStartCaptureRunner
{
    private static readonly string[] LiveAgentKinds = { "codex-exec", "claude-p" };
    private static readonly string[] DryRunAgentKinds = { "scripted-stub" };

    public static void RunLiveCaptureFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100LiveColdStartCaptureRunner));
        var runId = RequireEnvironment("SM_H100_LIVE_RUN_ID");
        _ = RequireEnvironment("SM_H100_SEALED_OUTPUT");
        var exchangeDirectory = H100SealedBridgeRunner.ResolveOutputDirectory(
            RequireEnvironment("SM_H100_LIVE_EXCHANGE_DIR"));
        var manifest = H100SealedBridgeRunner.CreatePromptManifest(requireExplicit: true);
        var settings = H100SealedBridgeRunner.ResolveSettings(runId);
        var result = Capture(
            H100SealedBridgeRunner.CreateLookup(),
            settings,
            H100SealedBridgeRunner.ResolveTargetBattleSeconds(),
            exchangeDirectory,
            manifest,
            SealedDecisionTraceCaptureSource.LiveColdStartLlm,
            LiveAgentKinds,
            ReadSeconds("SM_H100_DECISION_TIMEOUT_SECONDS", 900),
            ReadSeconds("SM_H100_RUN_REPORT_TIMEOUT_SECONDS", 1200),
            TimeSpan.FromMilliseconds(ReadPositiveInt("SM_H100_POLL_INTERVAL_MS", 500)));
        Debug.Log(
            $"[H100LiveColdStart] live capture complete terminal={result.Capture.TerminalFailure} "
            + $"trace={result.Capture.TracePath} ledger={result.LedgerPath}");
    }

    public static void RunDryRunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100LiveColdStartCaptureRunner));
        _ = RequireEnvironment("SM_H100_SEALED_OUTPUT");
        var exchangeDirectory = H100SealedBridgeRunner.ResolveOutputDirectory(
            RequireEnvironment("SM_H100_LIVE_EXCHANGE_DIR"));
        var manifest = H100SealedBridgeRunner.CreatePromptManifest();
        var settings = H100SealedBridgeRunner.ResolveSettings();
        var result = CaptureDryRun(
            H100SealedBridgeRunner.CreateLookup(),
            settings,
            H100SealedBridgeRunner.ResolveTargetBattleSeconds(),
            exchangeDirectory,
            manifest,
            ReadSeconds("SM_H100_DECISION_TIMEOUT_SECONDS", 30),
            TimeSpan.FromMilliseconds(ReadPositiveInt("SM_H100_POLL_INTERVAL_MS", 50)));
        Debug.Log(
            $"[H100LiveColdStart] dry-run capture complete certification_eligible=false "
            + $"terminal={result.Capture.TerminalFailure} trace={result.Capture.TracePath} ledger={result.LedgerPath}");
    }

    internal static LiveCaptureArtifacts CaptureDryRun(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        string exchangeDirectory,
        LlmPromptManifestV1 manifest,
        TimeSpan responseTimeout,
        TimeSpan pollInterval)
        => Capture(
            lookup,
            settings,
            targetBattleSeconds,
            exchangeDirectory,
            manifest,
            SealedDecisionTraceCaptureSource.SyntheticStandIn,
            DryRunAgentKinds,
            responseTimeout,
            responseTimeout,
            pollInterval);

    private static LiveCaptureArtifacts Capture(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        string exchangeDirectory,
        LlmPromptManifestV1 manifest,
        SealedDecisionTraceCaptureSource captureSource,
        string[] allowedAgentKinds,
        TimeSpan decisionTimeout,
        TimeSpan runReportTimeout,
        TimeSpan pollInterval)
    {
        if (lookup == null) throw new ArgumentNullException(nameof(lookup));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        var guardedExchangeDirectory = H100SealedBridgeRunner.ResolveOutputDirectory(exchangeDirectory);
        var factLedger = new H100PlayerVisibleFactLedgerCollector(settings.RunId);
        var identity = captureSource == SealedDecisionTraceCaptureSource.LiveColdStartLlm
            ? H100LiveCaptureIdentity.Create(
                settings.CampaignSiteSafety,
                settings.MaxBattleSteps,
                targetBattleSeconds,
                settings.PolicyId)
            : null;
        var capture = H100SealedBridgeRunner.Capture(
            lookup,
            settings,
            targetBattleSeconds,
            sourceFactory: promptManifest => new LiveColdStartFileHandshakeSource(
                guardedExchangeDirectory,
                promptManifest,
                allowedAgentKinds,
                decisionTimeout,
                runReportTimeout,
                pollInterval),
            captureSource: captureSource,
            identity: identity,
            externalFactLedger: factLedger,
            promptManifestOverride: manifest);
        var outputDirectory = Path.GetDirectoryName(capture.TracePath)
                              ?? throw new InvalidOperationException("Trace output directory is unavailable.");
        var ledgerPath = PlayerVisibleFactLedgerArtifactWriter.Write(
            outputDirectory,
            factLedger.Facts,
            factLedger.Decisions);
        return new LiveCaptureArtifacts(capture, ledgerPath);
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

    private static TimeSpan ReadSeconds(string name, int fallback)
        => TimeSpan.FromSeconds(ReadPositiveInt(name, fallback));

    private static int ReadPositiveInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        var value = string.IsNullOrWhiteSpace(raw)
            ? fallback
            : int.Parse(raw, NumberStyles.None, CultureInfo.InvariantCulture);
        if (value <= 0)
        {
            throw new InvalidOperationException($"{name} must be > 0 (actual={value.ToString(CultureInfo.InvariantCulture)}). ");
        }

        return value;
    }

    internal sealed record LiveCaptureArtifacts(
        H100SealedBridgeRunner.CaptureResult Capture,
        string LedgerPath);
}
