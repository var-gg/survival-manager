using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessMetrics;
using SM.SealedLlmBridge;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class H100LiveCaptureIdentityFastTests
{
    [Test]
    public void SixRunIdsAndSeeds_ProduceOneCohortIdentity()
    {
        var identities = Enumerable.Range(0, 6)
            .Select(index => new
            {
                RunId = $"live-slot-{index + 1}",
                Seed = 1701 + index,
                Identity = H100LiveCaptureIdentity.Create(
                    campaignSiteSafety: 32,
                    maxBattleSteps: 3600,
                    targetBattleSeconds: 45f,
                    policyId: "greedy"),
            })
            .ToArray();

        Assert.That(identities.Select(value => value.RunId).Distinct().Count(), Is.EqualTo(6));
        Assert.That(identities.Select(value => value.Seed).Distinct().Count(), Is.EqualTo(6));
        Assert.That(identities.Select(value => value.Identity.BuildManifestHash).Distinct().Count(), Is.EqualTo(1));
        Assert.That(identities.Select(value => value.Identity.ScorerConfigHash).Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public void LiveIdentitySignature_HasNoRunIdOrSeedBaseInput()
    {
        var parameters = typeof(H100LiveCaptureIdentity).GetMethod(nameof(H100LiveCaptureIdentity.Create))
            ?.GetParameters()
            .Select(parameter => parameter.Name)
            .ToArray();
        Assert.That(parameters, Is.Not.Null);
        Assert.That(parameters, Does.Not.Contain("runId"));
        Assert.That(parameters, Does.Not.Contain("seedBase"));
        Assert.That(H100LiveCaptureIdentity.LiveScorerConfigToken,
            Is.EqualTo("live-cold-start:bt5bt10-v1"));
    }

    [Test]
    public void LegacySyntheticBuildPreimage_StillIncludesRunIdAndSeedBase()
    {
        var first = LegacySyntheticBuildHash("run-a", 1701);
        var changedRun = LegacySyntheticBuildHash("run-b", 1701);
        var changedSeed = LegacySyntheticBuildHash("run-a", 1702);

        Assert.That(changedRun, Is.Not.EqualTo(first));
        Assert.That(changedSeed, Is.Not.EqualTo(first));
    }

    private static string LegacySyntheticBuildHash(string runId, int seedBase)
    {
        using var payload = new MemoryStream();
        foreach (var value in new[]
                 {
                     "H100SealedBuildManifestV1",
                     runId,
                     seedBase.ToString(CultureInfo.InvariantCulture),
                     "32",
                     "3600",
                     "45",
                     "greedy",
                 })
        {
            LengthPrefixedStableHash.AppendPart(payload, value);
        }

        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }
}
