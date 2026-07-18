using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.HeadlessMetrics;
using SM.SealedLlmBridge;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class H100LiveColdStartDryRunTests
{
    [Test]
    public void InProcessStub_CapturesLedgersReplaysAndRemainsCertificationIneligible()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100LiveColdStartDryRunTests));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var root = Path.Combine(
            projectRoot,
            "Logs",
            "h100-live-coldstart-dryrun-tests",
            Guid.NewGuid().ToString("N"));
        var exchange = Path.Combine(root, "exchange");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(exchange);
        var manifest = Manifest();
        var settings = H100MetricsRunSettings.Smoke with
        {
            BattleCount = 1,
            CampaignCount = 1,
            CampaignSiteSafety = 1,
            WriteCsv = false,
            OutputDirectory = output,
        };
        Exception serviceFailure = null;
        var service = new Thread(() =>
        {
            try
            {
                ServiceExchange(exchange);
            }
            catch (Exception exception)
            {
                serviceFailure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "H100 scripted-stub exchange",
        };

        var environment = new EnvironmentSnapshot(
            "SM_H100_PROMPT_ARCHIVE_DIR",
            "SM_H100_PROMPT_TEMPLATE_ID",
            "SM_H100_PROMPT_TEMPLATE",
            "SM_H100_PROMPT_TEMPLATE_FILE",
            "SM_H100_COLD_START_BRIEFING",
            "SM_H100_COLD_START_BRIEFING_FILE",
            "SM_H100_MODEL_SNAPSHOT",
            "SM_H100_DECODING_CONFIG");
        try
        {
            service.Start();
            var targetBattleSeconds = H100SealedBridgeRunner.ResolveTargetBattleSeconds();
            var capture = H100LiveColdStartCaptureRunner.CaptureDryRun(
                CreateLookup(),
                settings,
                targetBattleSeconds,
                exchange,
                manifest,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(5));
            Assert.That(service.Join(TimeSpan.FromSeconds(10)), Is.True, "stub service did not finish");
            Assert.That(serviceFailure, Is.Null);
            Assert.That(capture.Capture.TerminalFailure, Is.False);
            Assert.That(File.Exists(capture.Capture.TracePath), Is.True);
            Assert.That(File.Exists(capture.LedgerPath), Is.True);
            Assert.That(new FileInfo(capture.LedgerPath).Length, Is.GreaterThan(0));
            Assert.That(capture.Capture.Trace.Header.CaptureSource,
                Is.EqualTo(SealedDecisionTraceCaptureSource.SyntheticStandIn));

            ConfigureReplayEnvironment(exchange, manifest);
            var replay = H100SealedBridgeRunner.Replay(
                CreateLookup(),
                settings,
                targetBattleSeconds,
                capture.Capture.Trace);
            Assert.That(replay.Passed, Is.True,
                $"{replay.Verification.FirstDivergenceReason}: {replay.Verification.FirstDivergenceDetail}");

            var archivedPrompt = Directory.EnumerateFiles(exchange, "d*.prompt.md")
                .OrderBy(path => path, StringComparer.Ordinal)
                .First();
            File.AppendAllText(archivedPrompt, "\nPROVENANCE_TAMPER");
            Assert.That(
                Assert.Throws<PlayerVisibleProvenanceException>(() => H100SealedBridgeRunner.Replay(
                    CreateLookup(),
                    settings,
                    targetBattleSeconds,
                    capture.Capture.Trace))?.Message,
                Does.Contain("Archived prompt byte mismatch"));

            var decoded = SealedRunDecoder.Decode(capture.Capture.Trace);
            Assert.That(decoded.Shape.Valid, Is.False);
            Assert.That(decoded.Shape.CertificationEligible, Is.False);
            Assert.That(decoded.Shape.FailureReason, Is.EqualTo("capture_source_not_live_cold_start_llm"));
        }
        finally
        {
            environment.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void ThreeIllegalAnswers_KeepTerminalTraceAndCallerOwnedFactLedger()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100LiveColdStartDryRunTests));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var root = Path.Combine(
            projectRoot,
            "Logs",
            "h100-live-coldstart-dryrun-terminal-tests",
            Guid.NewGuid().ToString("N"));
        var exchange = Path.Combine(root, "exchange");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(exchange);
        var manifest = Manifest();
        var settings = H100MetricsRunSettings.Smoke with
        {
            BattleCount = 1,
            CampaignCount = 1,
            CampaignSiteSafety = 1,
            WriteCsv = false,
            OutputDirectory = output,
        };
        Exception serviceFailure = null;
        var service = new Thread(() =>
        {
            try
            {
                ServiceExchange(exchange, terminalIllegal: true);
            }
            catch (Exception exception)
            {
                serviceFailure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "H100 terminal scripted-stub exchange",
        };

        var environment = new EnvironmentSnapshot(
            "SM_H100_PROMPT_ARCHIVE_DIR",
            "SM_H100_PROMPT_TEMPLATE_ID",
            "SM_H100_PROMPT_TEMPLATE",
            "SM_H100_PROMPT_TEMPLATE_FILE",
            "SM_H100_COLD_START_BRIEFING",
            "SM_H100_COLD_START_BRIEFING_FILE",
            "SM_H100_MODEL_SNAPSHOT",
            "SM_H100_DECODING_CONFIG");
        try
        {
            service.Start();
            var targetBattleSeconds = H100SealedBridgeRunner.ResolveTargetBattleSeconds();
            var capture = H100LiveColdStartCaptureRunner.CaptureDryRun(
                CreateLookup(),
                settings,
                targetBattleSeconds,
                exchange,
                manifest,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(5));
            Assert.That(service.Join(TimeSpan.FromSeconds(10)), Is.True, "stub service did not finish");
            Assert.That(serviceFailure, Is.Null);
            Assert.That(capture.Capture.TerminalFailure, Is.True);
            Assert.That(capture.Capture.Corpus, Is.Null);
            Assert.That(File.Exists(capture.Capture.TracePath), Is.True);
            Assert.That(File.Exists(capture.LedgerPath), Is.True);
            Assert.That(new FileInfo(capture.LedgerPath).Length, Is.GreaterThan(0));
            var terminal = capture.Capture.Trace.Entries.Single(entry => entry.TerminalFailure);
            Assert.That(terminal.SelectedAction, Is.EqualTo("999999"));

            ConfigureReplayEnvironment(exchange, manifest);
            var replay = H100SealedBridgeRunner.Replay(
                CreateLookup(),
                settings,
                targetBattleSeconds,
                capture.Capture.Trace);
            Assert.That(replay.Passed, Is.True,
                $"{replay.Verification.FirstDivergenceReason}: {replay.Verification.FirstDivergenceDetail}");
        }
        finally
        {
            environment.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void ServiceExchange(string exchange, bool terminalIllegal = false)
    {
        var serviced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var requestPath in Directory.EnumerateFiles(exchange, "*.request.json")
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!serviced.Add(requestPath))
                {
                    continue;
                }

                var request = SealedLlmExchangeEnvelope.ReadRequest(requestPath);
                var prefix = Path.GetFileName(requestPath).Replace(".request.json", string.Empty);
                var prompt = File.ReadAllText(Path.Combine(exchange, request.PromptFile));
                var isRunReport = string.Equals(
                    request.SeamKey.SeamType,
                    SealedLlmSeamTypes.RunReport,
                    StringComparison.Ordinal);
                var rawResponse = isRunReport
                    ? LlmWireCanonicalSerializer.CanonicalJson(RunReport())
                    : LlmWireCanonicalSerializer.CanonicalJson(Decision(
                        terminalIllegal ? "999999" : SelectAction(request),
                        ExtractEvidenceFactIds(prompt)));
                var responseCount = terminalIllegal && !isRunReport ? 3 : 1;
                for (var attempt = 1; attempt <= responseCount; attempt++)
                {
                    SealedLlmExchangeEnvelope.WriteResponse(
                        Path.Combine(exchange, $"{prefix}.a{attempt}.response.json"),
                        new SealedLlmExchangeResponseV1(
                            SealedLlmExchangeResponseV1.CurrentSchemaVersion,
                            request.SeamKey,
                            request.RequestCanonicalHash,
                            "scripted-stub",
                            rawResponse));
                }

                if (isRunReport)
                {
                    return;
                }
            }

            Thread.Sleep(2);
        }

        throw new TimeoutException("Scripted stub did not receive a run-report request.");
    }

    private static void ConfigureReplayEnvironment(string exchange, LlmPromptManifestV1 manifest)
    {
        Environment.SetEnvironmentVariable("SM_H100_PROMPT_ARCHIVE_DIR", exchange);
        Environment.SetEnvironmentVariable("SM_H100_PROMPT_TEMPLATE_ID", manifest.PromptTemplateId);
        Environment.SetEnvironmentVariable("SM_H100_PROMPT_TEMPLATE", manifest.PromptTemplate);
        Environment.SetEnvironmentVariable("SM_H100_PROMPT_TEMPLATE_FILE", null);
        Environment.SetEnvironmentVariable("SM_H100_COLD_START_BRIEFING", manifest.ColdStartBriefing);
        Environment.SetEnvironmentVariable("SM_H100_COLD_START_BRIEFING_FILE", null);
        Environment.SetEnvironmentVariable("SM_H100_MODEL_SNAPSHOT", manifest.ModelSnapshotId);
        Environment.SetEnvironmentVariable("SM_H100_DECODING_CONFIG", manifest.DecodingConfigCanonical);
    }

    private static string SelectAction(SealedLlmExchangeRequestV1 request)
    {
        if (request.DeploymentActionSpace == null)
        {
            return request.LegalActionKeys.First();
        }

        var space = request.DeploymentActionSpace;
        if (space.AvailableAnchorIds.Count < space.DeployCapacity
            || space.AvailableHeroIds.Count < space.DeployCapacity)
        {
            throw new InvalidOperationException("Stub deployment action space is undersized.");
        }

        return string.Join(";", space.AvailableAnchorIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(space.DeployCapacity)
            .Zip(
                space.AvailableHeroIds.OrderBy(value => value, StringComparer.Ordinal),
                (anchor, hero) => anchor + "=" + hero));
    }

    private static IReadOnlyList<string> ExtractEvidenceFactIds(string prompt)
    {
        var map = Regex.Match(prompt, "\\\"EvidenceFactIdsBySignal\\\":\\{(?<body>[^}]*)\\}");
        if (!map.Success)
        {
            throw new InvalidOperationException("Prompt omitted EvidenceFactIdsBySignal.");
        }

        var values = Regex.Matches(map.Groups["body"].Value, ":\\\"(?<value>[^\\\"]+)\\\"")
            .Cast<Match>()
            .Select(match => match.Groups["value"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0)
        {
            throw new InvalidOperationException("Prompt evidence map was empty.");
        }

        return values;
    }

    private static LlmDecisionResponseV1 Decision(
        string selectedAction,
        IReadOnlyList<string> evidenceFactIds)
        => new(
            selectedAction,
            new LlmDeclaredIntentV1(
                "scripted-stub-intent",
                Array.Empty<string>(),
                "exercise the selected visible action",
                evidenceFactIds,
                "inspect the next visible decision",
                Array.Empty<string>(),
                new[] { "pivot if visible evidence changes" },
                1d),
            "scripted-stub-intent-ref",
            Array.Empty<LlmBuildHypothesisV1>());

    private static LlmRunReportResponseV1 RunReport()
        => new(
            "scripted stub formed a visible test desire",
            "scripted stub observed a visible payoff or near miss",
            "scripted stub would try another visible concept",
            Array.Empty<string>(),
            Array.Empty<LlmEvaluationSentenceV1>(),
            "retry with another visible concept");

    private static LlmPromptManifestV1 Manifest()
        => new(
            "dry-run-prompt-v1",
            "TEST PLACEHOLDER: choose exactly from the rendered player-visible menu.",
            "TEST PLACEHOLDER: blind player; do not use files, tools, or hidden knowledge.",
            "scripted-stub/dry-run",
            "mechanism=scripted-stub");

    private static RuntimeCombatContentLookup CreateLookup()
    {
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        Assert.That(lookup.TryGetCombatSnapshot(out _, out var error), Is.True, error);
        return lookup;
    }

    private sealed class EnvironmentSnapshot : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public EnvironmentSnapshot(params string[] names)
        {
            _values = names.ToDictionary(
                name => name,
                Environment.GetEnvironmentVariable,
                StringComparer.Ordinal);
        }

        public void Dispose()
        {
            foreach (var pair in _values)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
