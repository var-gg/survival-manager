using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.HeadlessMetrics;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>실 campaign trace가 hostile locale에서도 동일 byte/state-event로 재도출되는 witness.</summary>
[Category("BatchOnly")]
public sealed class H100SealedBridgeHostileLocaleReplayTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private H100MetricsRunSettings _settings;
    private float _targetBattleSeconds;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100SealedBridgeHostileLocaleReplayTests));
        _settings = H100MetricsRunSettings.Smoke with
        {
            BattleCount = 1,
            CampaignCount = 1,
            WriteCsv = false,
            OutputDirectory = "Logs/h100-sealed-bridge-hostile-locale-tests",
        };
        _targetBattleSeconds = H100SealedBridgeRunner.ResolveTargetBattleSeconds();
    }

    [Test]
    public void CaptureThenThreeHostileLocaleReplays_RebuildExactTraceAndStateEventHashes()
    {
        var capture = H100SealedBridgeRunner.Capture(
            CreateLookup(),
            _settings,
            _targetBattleSeconds);
        Assert.That(capture.TerminalFailure, Is.False, "hostile-locale witness requires a normal capture");
        var sealedTraceBytes = File.ReadAllBytes(capture.TracePath);

        foreach (var cultureName in new[] { "tr-TR", "de-DE", "ja-JP" })
        {
            var previousCurrentCulture = CultureInfo.CurrentCulture;
            var previousCurrentUiCulture = CultureInfo.CurrentUICulture;
            var previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
            var previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            try
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                var replay = H100SealedBridgeRunner.Replay(
                    CreateLookup(),
                    _settings,
                    _targetBattleSeconds,
                    capture.Trace);
                var rebuiltTraceBytes = Utf8WithoutBom.GetBytes(
                    HeadlessMetricJson.Serialize(replay.RebuiltTrace) + "\n");
                var verification = SealedDecisionTraceReplayVerifier.Verify(
                    capture.Trace,
                    replay.RebuiltTrace);

                TestContext.WriteLine(
                    $"culture={cultureName} verification={verification.VerificationPassed} "
                    + $"byte_identical={sealedTraceBytes.AsSpan().SequenceEqual(rebuiltTraceBytes.AsSpan())} "
                    + $"entries={verification.ComparedEntryCount}");
                Assert.That(rebuiltTraceBytes, Is.EqualTo(sealedTraceBytes),
                    $"persisted trace bytes diverged under {cultureName}");
                Assert.That(verification.VerificationPassed, Is.True,
                    $"{verification.FirstDivergenceReason}: {verification.FirstDivergenceDetail}");
                Assert.That(replay.Passed, Is.True);
                Assert.That(replay.RebuiltTrace.Entries.Count, Is.EqualTo(capture.Trace.Entries.Count));
                for (var index = 0; index < capture.Trace.Entries.Count; index++)
                {
                    var sealedEntry = capture.Trace.Entries[index];
                    var rebuiltEntry = replay.RebuiltTrace.Entries[index];
                    Assert.That(
                        string.Equals(sealedEntry.PreStateHash, rebuiltEntry.PreStateHash, StringComparison.Ordinal),
                        Is.True,
                        $"pre_state_hash diverged at {index} under {cultureName}");
                    Assert.That(
                        string.Equals(sealedEntry.PostStateHash, rebuiltEntry.PostStateHash, StringComparison.Ordinal),
                        Is.True,
                        $"post_state_hash diverged at {index} under {cultureName}");
                    Assert.That(
                        string.Equals(sealedEntry.ResultEventHash, rebuiltEntry.ResultEventHash, StringComparison.Ordinal),
                        Is.True,
                        $"result_event_hash diverged at {index} under {cultureName}");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCurrentCulture;
                CultureInfo.CurrentUICulture = previousCurrentUiCulture;
                CultureInfo.DefaultThreadCurrentCulture = previousDefaultCulture;
                CultureInfo.DefaultThreadCurrentUICulture = previousDefaultUiCulture;
            }
        }
    }

    private static RuntimeCombatContentLookup CreateLookup()
    {
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        Assert.That(lookup.TryGetCombatSnapshot(out _, out var error), Is.True, error);
        return lookup;
    }
}
