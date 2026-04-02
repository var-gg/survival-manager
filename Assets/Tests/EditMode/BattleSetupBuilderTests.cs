using System;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleSetupBuilderTests
{
    [Test]
    public void BuildBattleSetup_UsesDeploymentAnchors_FromSessionWithoutSceneTables()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());

        var heroA = session.ExpeditionSquadHeroIds[0];
        var heroB = session.ExpeditionSquadHeroIds[1];
        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, heroA), Is.True);
        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, heroB), Is.True);

        var allies = session.BuildBattleParticipants();
        Assert.That(allies.First(spec => spec.ParticipantId == heroA).Anchor, Is.EqualTo(DeploymentAnchorId.BackBottom));
        Assert.That(allies.First(spec => spec.ParticipantId == heroB).Anchor, Is.EqualTo(DeploymentAnchorId.FrontCenter));

        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);
        var result = BattleSetupBuilder.Build(allies, new BattleEncounterPlan(Array.Empty<BattleParticipantSpec>(), TeamPostureType.StandardAdvance), snapshot);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(result.Allies.First(definition => definition.Id == heroA).PreferredAnchor, Is.EqualTo(DeploymentAnchorId.BackBottom));
        Assert.That(result.Allies.First(definition => definition.Id == heroB).PreferredAnchor, Is.EqualTo(DeploymentAnchorId.FrontCenter));
    }

    [Test]
    public void SameArchetypeHeroes_WithDifferentTraitItemAndAugmentInputs_EndWithDifferentStats()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        var archetypeId = lookup.GetCanonicalArchetypeIds().First();
        Assert.That(lookup.TryGetTraitIds(archetypeId, out var positiveTraits, out var negativeTraits), Is.True);
        var itemId = lookup.GetCanonicalItemIds().First();
        var affixId = lookup.GetCanonicalAffixIds().First();
        var augmentId = lookup.GetCanonicalTemporaryAugmentIds().First();

        var allies = new[]
        {
            new BattleParticipantSpec(
                "ally_base",
                "Base",
                archetypeId,
                DeploymentAnchorId.FrontCenter,
                positiveTraits[0],
                negativeTraits[0],
                Array.Empty<BattleEquippedItemSpec>(),
                Array.Empty<string>()),
            new BattleParticipantSpec(
                "ally_variant",
                "Variant",
                archetypeId,
                DeploymentAnchorId.BackCenter,
                positiveTraits[Math.Min(1, positiveTraits.Count - 1)],
                negativeTraits[Math.Min(1, negativeTraits.Count - 1)],
                new[]
                {
                    new BattleEquippedItemSpec(itemId, new[] { affixId })
                },
                new[] { augmentId })
        };

        var result = BattleSetupBuilder.Build(allies, new BattleEncounterPlan(Array.Empty<BattleParticipantSpec>(), TeamPostureType.StandardAdvance), snapshot);
        Assert.That(result.IsSuccess, Is.True, result.Error);

        var state = BattleFactory.Create(result.Allies, result.Enemies);
        var baseUnit = state.Allies.First(unit => unit.Definition.Id == "ally_base");
        var variantUnit = state.Allies.First(unit => unit.Definition.Id == "ally_variant");

        Assert.That(result.Allies[1].Packages!.Any(package => package.Source == ModifierSource.Item), Is.True);
        Assert.That(result.Allies[1].Packages!.Any(package => package.Source == ModifierSource.Augment), Is.True);
        Assert.That(result.Allies[1].Packages!.Any(package => package.Source == ModifierSource.Trait), Is.True);
        Assert.That(variantUnit.Attack + variantUnit.Defense + variantUnit.Speed + variantUnit.MaxHealth,
            Is.Not.EqualTo(baseUnit.Attack + baseUnit.Defense + baseUnit.Speed + baseUnit.MaxHealth).Within(0.01f));
    }
}
