using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.HeadlessCensus;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class ConceptCatalogDeriverFastTests
{
    private static readonly string[] ObservableWitnesses =
    {
        "beat.on_kill_effect",
        "beat.synergy_activated",
        "telemetry.damage_applied",
        "telemetry.healing_applied",
        "telemetry.status_applied",
    };

    private BuildSpaceCensus _census = null!;

    [OneTimeSetUp]
    public void BuildCensus()
    {
        _census = BuildSpaceEnumerator.Generate(CanonicalGrid());
        CanonicalBuildSpaceContract.RequireExpected(_census);
    }

    [Test]
    public void Derive_SameInputsProduceByteIdenticalCatalog()
    {
        var graph = HandBuiltGraph();
        var first = ConceptCatalogDeriver.Derive(
            OwnerConceptAnchorCatalog.CreateRatificationPendingDraft(),
            _census,
            graph,
            ObservableWitnesses);
        var repeated = ConceptCatalogDeriver.Derive(
            OwnerConceptAnchorCatalog.CreateRatificationPendingDraft().Reverse(),
            _census,
            new BuildGrammarTruthGraph(graph.Edges.Reverse()),
            ObservableWitnesses.Reverse());

        Assert.That(
            ConceptCatalogArtifactWriter.Serialize(repeated),
            Is.EqualTo(ConceptCatalogArtifactWriter.Serialize(first)));
        Assert.That(first.OwnerAnchors.Count, Is.EqualTo(10));
        Assert.That(first.AnchorDerivations.Count, Is.EqualTo(10));
    }

    [Test]
    public void Derive_ExcludesRawStatOnlyAndClustersIsomorphicRecipes()
    {
        var catalog = ConceptCatalogDeriver.Derive(
            OwnerConceptAnchorCatalog.CreateRatificationPendingDraft(),
            _census,
            HandBuiltGraph(),
            ObservableWitnesses);

        Assert.That(catalog.Summary.RawStatOnlyExcludedCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(catalog.Summary.IsomorphicDuplicateCount, Is.GreaterThan(0));
        Assert.That(catalog.Summary.CandidateRecipeCount, Is.GreaterThan(catalog.Summary.IsomorphicClusterCount));
        Assert.That(AllVariants(catalog).SelectMany(variant => variant.MedoidRecipe.ComponentIds),
            Does.Not.Contain("item:item.raw_armor"));
    }

    [Test]
    public void Derive_ContractsUseOnlyInjectedObservableWitnessesAndExactSchemaFields()
    {
        var catalog = ConceptCatalogDeriver.Derive(
            OwnerConceptAnchorCatalog.CreateRatificationPendingDraft(),
            _census,
            HandBuiltGraph(),
            ObservableWitnesses);

        Assert.That(
            AllVariants(catalog).All(variant => ObservableWitnesses.Contains(
                variant.Contract.PayoffWitness,
                StringComparer.Ordinal)),
            Is.True);
        Assert.That(
            typeof(ConceptContract).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[]
            {
                "IdentityPredicates",
                "ProgressMilestones",
                "PayoffWitness",
                "AllowedSubstitutions",
                "FlexSlots",
                "CounterAffordances",
                "AvailabilityTier",
                "PivotConditions",
            }));
    }

    [Test]
    public void Derive_FailsClosedForUnobservablePayoffWitness()
    {
        var edges = HandBuiltGraph().Edges.Concat(new[]
        {
            Edge(
                "edge-unknown-0",
                BuildGrammarSubjectKind.Skill,
                "skill.unknown",
                BuildGrammarRelation.PaysOff,
                "combat_effect",
                "damage",
                witness: "telemetry.future_only"),
        });

        Assert.Throws<InvalidOperationException>(() => ConceptCatalogDeriver.Derive(
            OwnerConceptAnchorCatalog.CreateRatificationPendingDraft(),
            _census,
            new BuildGrammarTruthGraph(edges),
            ObservableWitnesses));
    }

    [Test]
    public void Derive_FailsClosedForUnreachableThreshold()
    {
        var edges = HandBuiltGraph().Edges.Concat(new[]
        {
            Edge(
                "edge-unreachable-0",
                BuildGrammarSubjectKind.Synergy,
                "synergy.undead@5",
                BuildGrammarRelation.Requires,
                "tag",
                "undead",
                truthValue: "threshold=5",
                witness: "beat.synergy_activated"),
            Edge(
                "edge-unreachable-1",
                BuildGrammarSubjectKind.Synergy,
                "synergy.undead@5",
                BuildGrammarRelation.PaysOff,
                "team_rule",
                TeamRuleSet.DeathTollRuleId,
                witness: "beat.on_kill_effect"),
        });

        Assert.Throws<InvalidOperationException>(() => ConceptCatalogDeriver.Derive(
            OwnerConceptAnchorCatalog.CreateRatificationPendingDraft(),
            _census,
            new BuildGrammarTruthGraph(edges),
            ObservableWitnesses));
    }

    private static IEnumerable<ConceptVariant> AllVariants(ConceptCatalog catalog)
        => catalog.AnchorDerivations.SelectMany(derivation => derivation.Variants)
            .Concat(catalog.SystemDerivedMedoids);

    private static BuildGrammarTruthGraph HandBuiltGraph()
    {
        return new BuildGrammarTruthGraph(new[]
        {
            Edge("edge-000", BuildGrammarSubjectKind.Synergy, "synergy.undead@4", BuildGrammarRelation.AcquiredBy, "acquisition", "squad_composition"),
            Edge("edge-001", BuildGrammarSubjectKind.Synergy, "synergy.undead@4", BuildGrammarRelation.Requires, "tag", "undead", "threshold=4", "beat.synergy_activated"),
            Edge("edge-002", BuildGrammarSubjectKind.Synergy, "synergy.undead@4", BuildGrammarRelation.PaysOff, "team_rule", TeamRuleSet.DeathTollRuleId, witness: "beat.on_kill_effect"),
            Edge("edge-010", BuildGrammarSubjectKind.Skill, "skill.slow_a", BuildGrammarRelation.AcquiredBy, "acquisition", "recruit"),
            Edge("edge-011", BuildGrammarSubjectKind.Skill, "skill.slow_a", BuildGrammarRelation.Produces, "status", "slow", witness: "telemetry.status_applied"),
            Edge("edge-012", BuildGrammarSubjectKind.Skill, "skill.slow_b", BuildGrammarRelation.AcquiredBy, "acquisition", "recruit"),
            Edge("edge-013", BuildGrammarSubjectKind.Skill, "skill.slow_b", BuildGrammarRelation.Produces, "status", "slow", witness: "telemetry.status_applied"),
            Edge("edge-020", BuildGrammarSubjectKind.Item, "item.potency_a", BuildGrammarRelation.AcquiredBy, "acquisition", "reward"),
            Edge("edge-021", BuildGrammarSubjectKind.Item, "item.potency_a", BuildGrammarRelation.Amplifies, "stat", "status_potency"),
            Edge("edge-022", BuildGrammarSubjectKind.Item, "item.potency_a", BuildGrammarRelation.Substitutes, BuildGrammarSubjectKind.Item, "item.potency_b"),
            Edge("edge-023", BuildGrammarSubjectKind.Item, "item.potency_b", BuildGrammarRelation.AcquiredBy, "acquisition", "reward"),
            Edge("edge-024", BuildGrammarSubjectKind.Item, "item.potency_b", BuildGrammarRelation.Amplifies, "stat", "status_potency"),
            Edge("edge-025", BuildGrammarSubjectKind.Item, "item.potency_b", BuildGrammarRelation.Substitutes, BuildGrammarSubjectKind.Item, "item.potency_a"),
            Edge("edge-030", BuildGrammarSubjectKind.Skill, "skill.heal", BuildGrammarRelation.AcquiredBy, "acquisition", "recruit"),
            Edge("edge-031", BuildGrammarSubjectKind.Skill, "skill.heal", BuildGrammarRelation.PaysOff, "combat_effect", "healing", witness: "telemetry.healing_applied"),
            Edge("edge-032", BuildGrammarSubjectKind.Item, "item.heal", BuildGrammarRelation.AcquiredBy, "acquisition", "reward"),
            Edge("edge-033", BuildGrammarSubjectKind.Item, "item.heal", BuildGrammarRelation.Amplifies, "stat", "heal_power"),
            Edge("edge-040", BuildGrammarSubjectKind.Skill, "skill.strike", BuildGrammarRelation.AcquiredBy, "acquisition", "recruit"),
            Edge("edge-041", BuildGrammarSubjectKind.Skill, "skill.strike", BuildGrammarRelation.PaysOff, "combat_effect", "damage", witness: "telemetry.damage_applied"),
            Edge("edge-042", BuildGrammarSubjectKind.Passive, "passive.power", BuildGrammarRelation.AcquiredBy, "acquisition", "level_node"),
            Edge("edge-043", BuildGrammarSubjectKind.Passive, "passive.power", BuildGrammarRelation.Amplifies, "stat", "phys_power"),
            Edge("edge-050", BuildGrammarSubjectKind.Item, "item.raw_armor", BuildGrammarRelation.AcquiredBy, "acquisition", "reward"),
            Edge("edge-051", BuildGrammarSubjectKind.Item, "item.raw_armor", BuildGrammarRelation.Amplifies, "stat", "armor"),
        });
    }

    private static BuildGrammarTruthEdge Edge(
        string edgeId,
        string subjectKind,
        string subjectId,
        string relation,
        string targetKind,
        string targetId,
        string truthValue = "",
        string witness = "")
        => new(
            edgeId,
            subjectKind,
            subjectId,
            relation,
            targetKind,
            targetId,
            truthValue,
            Actionable: true,
            FeedbackRequired: !string.IsNullOrWhiteSpace(witness),
            ExpectedFeedbackWitness: witness);

    private static IReadOnlyList<BuildArchetype> CanonicalGrid()
        => new[]
        {
            Archetype("warden", "human", "vanguard", BuildRole.Tank, DeploymentAnchorId.FrontCenter),
            Archetype("guardian", "undead", "vanguard", BuildRole.Tank, DeploymentAnchorId.FrontTop),
            Archetype("slayer", "human", "duelist", BuildRole.Damage, DeploymentAnchorId.FrontBottom),
            Archetype("raider", "beastkin", "duelist", BuildRole.Damage, DeploymentAnchorId.FrontTop),
            Archetype("hunter", "human", "ranger", BuildRole.Ranged, DeploymentAnchorId.BackTop),
            Archetype("scout", "beastkin", "ranger", BuildRole.Ranged, DeploymentAnchorId.BackBottom),
            Archetype("priest", "human", "mystic", BuildRole.Healer, DeploymentAnchorId.BackCenter),
            Archetype("hexer", "undead", "mystic", BuildRole.Healer, DeploymentAnchorId.BackCenter),
            Archetype("bulwark", "beastkin", "vanguard", BuildRole.Tank, DeploymentAnchorId.FrontBottom),
            Archetype("reaver", "undead", "duelist", BuildRole.Damage, DeploymentAnchorId.FrontCenter),
            Archetype("marksman", "undead", "ranger", BuildRole.Ranged, DeploymentAnchorId.BackCenter),
            Archetype("shaman", "beastkin", "mystic", BuildRole.Healer, DeploymentAnchorId.BackTop),
        };

    private static BuildArchetype Archetype(
        string id,
        string race,
        string @class,
        BuildRole role,
        DeploymentAnchorId anchor)
        => new(id, race, @class, role, anchor);
}
