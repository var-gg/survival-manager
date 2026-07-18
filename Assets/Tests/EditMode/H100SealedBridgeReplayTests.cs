using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>실 H100 campaign capture를 fresh in-process replay로 재도출하는 anti-tautology witness.</summary>
[Category("BatchOnly")]
public sealed class H100SealedBridgeReplayTests
{
    private H100MetricsRunSettings _settings;
    private float _targetBattleSeconds;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100SealedBridgeReplayTests));
        _settings = H100MetricsRunSettings.Smoke with
        {
            BattleCount = 1,
            CampaignCount = 1,
            WriteCsv = false,
            OutputDirectory = "Logs/h100-sealed-bridge-replay-tests",
        };
        _targetBattleSeconds = H100SealedBridgeRunner.ResolveTargetBattleSeconds();
    }

    [Test]
    public void CaptureThenFreshReplay_RebuildsByteIdenticalTrace()
    {
        var witness = H100SealedBridgeRunner.RunInProcessReplayWitness(
            CreateLookup(),
            CreateLookup(),
            _settings,
            _targetBattleSeconds);

        TestContext.WriteLine(
            $"replay verification={witness.VerificationPassed} manifest_equal={witness.ManifestEqual} "
            + $"sealed={witness.SealedManifestHash} rebuilt={witness.RebuiltManifestHash} "
            + $"entries={witness.EntryCount} trace={witness.TracePath}");
        Assert.That(witness.VerificationPassed, Is.True,
            $"fresh replay diverged: {witness.FirstDivergenceReason} {witness.FirstDivergenceDetail}");
        Assert.That(witness.ManifestEqual, Is.True, "fresh replay manifest is not byte-identical");
        Assert.That(witness.Passed, Is.True);
        Assert.That(witness.EntryCount, Is.GreaterThan(0));
    }

    [Test]
    public void FullReplay_DetectsObservationResponseAndHeaderHashSingleByteTampers()
    {
        var witness = H100SealedBridgeRunner.RunTamperWitness(
            CreateLookup(),
            CreateLookup,
            _settings,
            _targetBattleSeconds);

        TestContext.WriteLine(
            $"observation_tamper passed={!witness.Observation.ReplayFailed} "
            + $"reason={witness.Observation.DivergenceReason} "
            + $"detail={witness.Observation.DivergenceDetail}");
        TestContext.WriteLine(
            $"response_tamper passed={!witness.Response.ReplayFailed} "
            + $"reason={witness.Response.DivergenceReason} "
            + $"detail={witness.Response.DivergenceDetail}");
        TestContext.WriteLine(
            $"header_hash_tamper passed={!witness.HeaderHash.ReplayFailed} "
            + $"reason={witness.HeaderHash.DivergenceReason} "
            + $"detail={witness.HeaderHash.DivergenceDetail}");

        Assert.That(witness.Observation.ReplayFailed, Is.True,
            "TAUTOLOGY: replay accepted a sealed trace with one observation byte flipped");
        Assert.That(witness.Observation.DivergenceReason, Is.EqualTo("ObservationHashMismatch"));
        Assert.That(witness.Response.ReplayFailed, Is.True,
            "TAUTOLOGY: replay accepted a sealed trace with one response byte flipped");
        Assert.That(witness.Response.DivergenceReason, Is.EqualTo("ResponseHashMismatch"));
        Assert.That(witness.HeaderHash.ReplayFailed, Is.True,
            "TAUTOLOGY: replay accepted a sealed trace with one header-hash byte flipped");
        Assert.That(witness.HeaderHash.DivergenceReason, Is.EqualTo("HeaderMismatch"));
        Assert.That(witness.HeaderHash.DivergenceDetail, Does.Contain("build_manifest_hash"));
        Assert.That(witness.AllDetected, Is.True);
        Assert.That(witness.TamperedEntryIndex, Is.GreaterThanOrEqualTo(0));
    }

    private static RuntimeCombatContentLookup CreateLookup()
    {
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        Assert.That(lookup.TryGetCombatSnapshot(out _, out var error), Is.True, error);
        return lookup;
    }
}
