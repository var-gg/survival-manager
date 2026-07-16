using System;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.HeadlessCensus;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BuildGrammarTruthGraphFastTests
{
    [Test]
    public void Build_DerivesOnlyDeclaredActionableRelations_InOrdinalOrder()
    {
        var sources = new[]
        {
            new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Item,
                "item.beta",
                Actionable: true,
                SlotId: "weapon",
                RequiredTags: new[] { "tag.melee" },
                ExcludedTags: new[] { "tag.ranged" },
                AcquisitionPaths: new[] { "reward" }),
            new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Item,
                "item.alpha",
                Actionable: true,
                SlotId: "weapon",
                AcquisitionPaths: new[] { "reward" }),
            new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Passive,
                "passive.child",
                Actionable: true,
                PrerequisiteIds: new[] { "passive.root" },
                AcquisitionPaths: new[] { "level_node" }),
        };

        var graph = BuildGrammarTruthGraphBuilder.Build(sources);

        Assert.That(graph.Edges.Select(edge => edge.EdgeId),
            Is.EqualTo(Enumerable.Range(0, graph.Edges.Count).Select(index => $"edge-{index:D5}")));
        Assert.That(graph.Edges, Has.Some.Matches<BuildGrammarTruthEdge>(edge =>
            edge.SubjectId == "item.beta" && edge.Relation == BuildGrammarRelation.Requires
                                          && edge.TargetKind == "tag" && edge.TargetId == "tag.melee"));
        Assert.That(graph.Edges, Has.Some.Matches<BuildGrammarTruthEdge>(edge =>
            edge.SubjectId == "item.beta" && edge.Relation == BuildGrammarRelation.Conflicts
                                          && edge.TargetId == "tag.ranged"));
        Assert.That(graph.Edges, Has.Some.Matches<BuildGrammarTruthEdge>(edge =>
            edge.SubjectId == "item.alpha" && edge.Relation == BuildGrammarRelation.Substitutes
                                           && edge.TargetId == "item.beta"));
        Assert.That(graph.Edges, Has.Some.Matches<BuildGrammarTruthEdge>(edge =>
            edge.SubjectId == "passive.child" && edge.Relation == BuildGrammarRelation.Requires
                                             && edge.TargetKind == "passive_node"
                                             && edge.TargetId == "passive.root"));
    }

    [Test]
    public void Build_InputOrderDoesNotChangeGraphBytesOrEdgeIds()
    {
        var alpha = new BuildGrammarTruthSource(
            BuildGrammarSubjectKind.Augment,
            "augment.alpha",
            Actionable: true,
            SlotId: "family.a",
            ConflictIds: new[] { "exclusive.a" },
            AcquisitionPaths: new[] { "reward" });
        var beta = new BuildGrammarTruthSource(
            BuildGrammarSubjectKind.Augment,
            "augment.beta",
            Actionable: true,
            SlotId: "family.a",
            AcquisitionPaths: new[] { "reward" });

        var first = BuildGrammarTruthGraphBuilder.Build(new[] { beta, alpha });
        var second = BuildGrammarTruthGraphBuilder.Build(new[] { alpha, beta });

        Assert.That(second.Edges, Is.EqualTo(first.Edges));
    }

    [Test]
    public void SynergyRule_UsesAuthoredThresholdAndGrantedRuleWithoutInventingPayoff()
    {
        var authored = new BuildGrammarTruthSource(
            BuildGrammarSubjectKind.Synergy,
            "synergy.alpha@2",
            Actionable: true,
            AcquisitionPaths: new[] { "squad_composition" },
            SynergyRule: new TeamSynergyTierRule(
                "synergy.alpha",
                "tag.alpha",
                2,
                Array.Empty<SM.Core.Stats.StatModifier>(),
                "rule.alpha"));
        var statOnly = new BuildGrammarTruthSource(
            BuildGrammarSubjectKind.Synergy,
            "synergy.beta@2",
            Actionable: true,
            AcquisitionPaths: new[] { "squad_composition" },
            SynergyRule: new TeamSynergyTierRule(
                "synergy.beta",
                "tag.beta",
                2,
                Array.Empty<SM.Core.Stats.StatModifier>()));

        var graph = BuildGrammarTruthGraphBuilder.Build(new[] { statOnly, authored });

        Assert.That(graph.Edges, Has.Some.Matches<BuildGrammarTruthEdge>(edge =>
            edge.SubjectId == "synergy.alpha@2" && edge.Relation == BuildGrammarRelation.Requires
                                                && edge.TruthValue == "threshold=2"));
        Assert.That(graph.Edges, Has.Some.Matches<BuildGrammarTruthEdge>(edge =>
            edge.SubjectId == "synergy.alpha@2" && edge.Relation == BuildGrammarRelation.PaysOff
                                                && edge.TargetId == "rule.alpha"));
        Assert.That(graph.Edges.Any(edge => edge.SubjectId == "synergy.beta@2"
                                                 && edge.Relation == BuildGrammarRelation.PaysOff), Is.False);
    }
}
