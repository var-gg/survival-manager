using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class CampaignEncounterPrepTests
{
    [Test]
    public void AuthoredCampaignEnemyGear_MatchesPerCapitaRampAndSignatureCaps()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignEncounterPrepTests));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        var expected = new Dictionary<(int Chapter, EncounterKindValue Kind), double>
        {
            [(1, EncounterKindValue.Skirmish)] = 0d,
            [(1, EncounterKindValue.Elite)] = 0d,
            [(1, EncounterKindValue.Boss)] = 0d,
            [(2, EncounterKindValue.Skirmish)] = 0d,
            [(2, EncounterKindValue.Elite)] = 0d,
            [(2, EncounterKindValue.Boss)] = 0d,
            [(3, EncounterKindValue.Skirmish)] = 1.0d,
            [(3, EncounterKindValue.Elite)] = 1.3d,
            [(3, EncounterKindValue.Boss)] = 1.6d,
            [(4, EncounterKindValue.Skirmish)] = 1.8d,
            [(4, EncounterKindValue.Elite)] = 2.2d,
            [(4, EncounterKindValue.Boss)] = 2.5d,
            [(5, EncounterKindValue.Skirmish)] = 2.7d,
            [(5, EncounterKindValue.Elite)] = 3.1d,
            [(5, EncounterKindValue.Boss)] = 3.5d,
        };
        var playerPerCapitaP50 = new Dictionary<(int Chapter, EncounterKindValue Kind), double>
        {
            [(3, EncounterKindValue.Skirmish)] = 2.0d,
            [(3, EncounterKindValue.Elite)] = 2.3d,
            [(3, EncounterKindValue.Boss)] = 2.6d,
            [(4, EncounterKindValue.Skirmish)] = 2.8d,
            [(4, EncounterKindValue.Elite)] = 3.1d,
            [(4, EncounterKindValue.Boss)] = 3.4d,
            [(5, EncounterKindValue.Skirmish)] = 3.6d,
            [(5, EncounterKindValue.Elite)] = 3.9d,
            [(5, EncounterKindValue.Boss)] = 4.2d,
        };
        var gearTotalRatioCaps = new Dictionary<EncounterKindValue, double>
        {
            [EncounterKindValue.Skirmish] = 0.95d,
            [EncounterKindValue.Elite] = 1.10d,
            [EncounterKindValue.Boss] = 1.25d,
        };

        foreach (var chapter in snapshot.CampaignChapters!.Values.OrderBy(value => value.StoryOrder))
        foreach (var siteId in chapter.SiteIds)
        foreach (var encounterId in snapshot.ExpeditionSites![siteId].EncounterIds)
        {
            var encounter = snapshot.Encounters![encounterId];
            var squad = snapshot.EnemySquads![encounter.EnemySquadTemplateId];
            var expectedHeadcount = encounter.Kind == EncounterKindValue.Boss ? 3 : 4;
            Assert.That(squad.Members, Has.Count.EqualTo(expectedHeadcount), encounter.Id);

            var average = squad.Members.Average(value => value.EquipmentBudget);
            Assert.That(
                average,
                Is.EqualTo(expected[(chapter.StoryOrder, encounter.Kind)]).Within(0.25d),
                encounter.Id);

            if (chapter.StoryOrder <= 2)
            {
                Assert.That(squad.Members.All(value => value.EquipmentBudget == 0f), Is.True, encounter.Id);
                Assert.That(squad.Members.All(value => string.IsNullOrWhiteSpace(value.EquipmentItemBaseId)), Is.True, encounter.Id);
            }
            else
            {
                Assert.That(squad.Members.All(value => value.EquipmentBudget > 0f), Is.True, encounter.Id);
                Assert.That(squad.Members.All(value => !string.IsNullOrWhiteSpace(value.EquipmentItemBaseId)), Is.True, encounter.Id);

                var gearTotalRatio = squad.Members.Sum(value => value.EquipmentBudget)
                                     / (4d * playerPerCapitaP50[(chapter.StoryOrder, encounter.Kind)]);
                Assert.That(
                    gearTotalRatio,
                    Is.LessThanOrEqualTo(gearTotalRatioCaps[encounter.Kind]),
                    encounter.Id);
            }

            var signatureCount = squad.Members.Count(value => value.EquipmentBudget >= 3.25f);
            var signatureCap = encounter.Kind == EncounterKindValue.Boss ? 2 : 1;
            Assert.That(signatureCount, Is.LessThanOrEqualTo(signatureCap), encounter.Id);
        }
    }

    [Test]
    public void PreviewGroundedPrep_ChangesAtLeastOneEliteOrBossFormationVersusNaiveHold()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignEncounterPrepTests));
        var witness = CampaignTwoArmSweepRunner.RunPrepMechanismWitness();

        TestContext.WriteLine(
            $"formation_divergence={witness.FormationDivergenceCount} "
            + $"outcome_divergence={witness.OutcomeDivergenceCount} "
            + $"informed_only_wins={witness.InformedOnlyWinCount} "
            + $"naive_only_wins={witness.NaiveOnlyWinCount} "
            + $"equipment_assignments={witness.EquipmentAssignmentCount} "
            + $"gear_counter_samples={witness.GearCounterSampleCount}");
        Assert.That(witness.FormationDivergenceCount, Is.GreaterThan(0),
            "informed elite/boss prep never changed a formation versus the paired naive hold");
        Assert.That(witness.EquipmentAssignmentCount, Is.GreaterThan(0),
            "informed prep never applied a bounded owned-item gear counter");
        Assert.That(witness.GearCounterSampleCount, Is.GreaterThan(0),
            "paired witness did not record any counterable enemy-gear samples");
    }
}
