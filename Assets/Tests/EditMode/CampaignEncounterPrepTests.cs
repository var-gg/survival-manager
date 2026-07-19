using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Editor.Validation;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class CampaignEncounterPrepTests
{
    [Test]
    public void PreviewGroundedPrep_ChangesAtLeastOneEliteOrBossFormationVersusNaiveHold()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignEncounterPrepTests));
        var witness = CampaignTwoArmSweepRunner.RunPrepMechanismWitness();

        TestContext.WriteLine(
            $"formation_divergence={witness.FormationDivergenceCount} "
            + $"outcome_divergence={witness.OutcomeDivergenceCount} "
            + $"informed_only_wins={witness.InformedOnlyWinCount} "
            + $"naive_only_wins={witness.NaiveOnlyWinCount}");
        Assert.That(witness.FormationDivergenceCount, Is.GreaterThan(0),
            "informed elite/boss prep never changed a formation versus the paired naive hold");
    }
}
