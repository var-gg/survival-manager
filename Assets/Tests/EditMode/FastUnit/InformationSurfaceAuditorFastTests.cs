using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class InformationSurfaceAuditorFastTests
{
    private static readonly string Bt1SpecPath = Path.Combine(
        "Assets", "_Game", "Scripts", "Runtime", "HeadlessMetrics", "h100-gates-bt1-v1.json");

    [Test]
    public void CompleteSurface_PassesFourHardMetricsAndFeedbackTarget()
    {
        var edges = CompleteEdges();
        var input = new InformationSurfaceAuditInput(
            edges,
            edges.Select(edge => Visible(edge, edge.TruthValue)).ToArray(),
            new[]
            {
                new PlayerVisibleTokenUse("fact-definition", "tag", "tag.melee", IsDefinition: true),
                new PlayerVisibleTokenUse("fact-use", "tag", "tag.melee", IsDefinition: false),
            },
            new[] { "telemetry.damage_applied" });

        var result = InformationSurfaceAuditor.Audit(input);

        Assert.That(result.ActionableOfferMissingSemantics, Is.Zero);
        Assert.That(result.UndefinedVisibleToken, Is.Zero);
        Assert.That(result.HiddenPrerequisite, Is.Zero);
        Assert.That(result.DescriptionBehaviorMismatchCount, Is.Zero);
        Assert.That(result.InteractionFeedbackCoverage, Is.EqualTo(1d));
        Assert.That(result.Gaps, Is.Empty);
    }

    [Test]
    public void MissingActionableSemantic_IsGroupedPerOffer()
    {
        var input = new InformationSurfaceAuditInput(
            new[]
            {
                Edge("edge-00000", "item.alpha", "acquired_by", "acquisition", "reward"),
                Edge("edge-00001", "item.alpha", "amplifies", "stat", "phys_power", "value=2"),
            },
            Array.Empty<PlayerVisibleBuildGrammarSemantic>(),
            Array.Empty<PlayerVisibleTokenUse>(),
            Array.Empty<string>());

        var result = InformationSurfaceAuditor.Audit(input);

        Assert.That(result.ActionableOfferMissingSemantics, Is.EqualTo(1));
        Assert.That(result.Gaps.Single().SubjectId, Is.EqualTo("item.alpha"));
    }

    [Test]
    public void VisibleTokenWithoutDefinition_IsUndefined()
    {
        var result = InformationSurfaceAuditor.Audit(new InformationSurfaceAuditInput(
            Array.Empty<BuildGrammarAuditEdge>(),
            Array.Empty<PlayerVisibleBuildGrammarSemantic>(),
            new[] { new PlayerVisibleTokenUse("fact-token", "status", "status.burn", IsDefinition: false) },
            Array.Empty<string>()));

        Assert.That(result.UndefinedVisibleToken, Is.EqualTo(1));
        Assert.That(result.Gaps.Single().Kind, Is.EqualTo(InformationSurfaceGapKind.UndefinedVisibleToken));
    }

    [Test]
    public void MissingRequiredEdge_IsHiddenPrerequisite()
    {
        var result = InformationSurfaceAuditor.Audit(new InformationSurfaceAuditInput(
            new[] { Edge("edge-00000", "affix.alpha", "requires", "tag", "tag.melee") },
            Array.Empty<PlayerVisibleBuildGrammarSemantic>(),
            Array.Empty<PlayerVisibleTokenUse>(),
            Array.Empty<string>()));

        Assert.That(result.HiddenPrerequisite, Is.EqualTo(1));
        Assert.That(result.Gaps, Has.Some.Matches<InformationSurfaceGap>(gap =>
            gap.Kind == InformationSurfaceGapKind.HiddenPrerequisite && gap.SubjectId == "affix.alpha"));
    }

    [Test]
    public void VisibleValueDifferentFromTruth_IsDescriptionBehaviorMismatch()
    {
        var edge = Edge("edge-00000", "augment.alpha", "amplifies", "stat", "speed", "value=2");
        var result = InformationSurfaceAuditor.Audit(new InformationSurfaceAuditInput(
            new[] { edge },
            new[] { Visible(edge, "value=1") },
            Array.Empty<PlayerVisibleTokenUse>(),
            Array.Empty<string>()));

        Assert.That(result.ActionableOfferMissingSemantics, Is.Zero);
        Assert.That(result.DescriptionBehaviorMismatchCount, Is.EqualTo(1));
    }

    [Test]
    public void MissingFeedbackWitness_LowersAuxiliaryCoverage()
    {
        var edge = Edge(
            "edge-00000",
            "skill.alpha",
            "pays_off",
            "combat_effect",
            "damage",
            feedbackRequired: true,
            expectedFeedbackWitness: "telemetry.damage_applied");
        var result = InformationSurfaceAuditor.Audit(new InformationSurfaceAuditInput(
            new[] { edge },
            new[] { Visible(edge, string.Empty) },
            Array.Empty<PlayerVisibleTokenUse>(),
            Array.Empty<string>()));

        Assert.That(result.InteractionFeedbackCoverage, Is.Zero);
        Assert.That(result.Gaps.Single().Kind, Is.EqualTo(InformationSurfaceGapKind.InteractionFeedbackMissing));
    }

    [Test]
    public void ArtifactWriter_IsByteDeterministicWithoutClockOrGuid()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm-h100-information-surface-audit-tests");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        try
        {
            var result = InformationSurfaceAuditor.Audit(new InformationSurfaceAuditInput(
                CompleteEdges(),
                CompleteEdges().Select(edge => Visible(edge, edge.TruthValue)).ToArray(),
                Array.Empty<PlayerVisibleTokenUse>(),
                new[] { "telemetry.damage_applied" }));

            var path = InformationSurfaceAuditArtifactWriter.Write(root, result);
            var first = File.ReadAllBytes(path);
            InformationSurfaceAuditArtifactWriter.Write(root, result);

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(first));
            Assert.That(Path.GetFileName(path), Is.EqualTo("information_surface_audit.json"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void Bt3Observations_EvaluateCheckedInGate()
    {
        var result = InformationSurfaceAuditor.Audit(new InformationSurfaceAuditInput(
            CompleteEdges(),
            CompleteEdges().Select(edge => Visible(edge, edge.TruthValue)).ToArray(),
            Array.Empty<PlayerVisibleTokenUse>(),
            new[] { "telemetry.damage_applied" }));
        var report = H100Bt1GateEvaluator.Generate(
            H100Bt1GateSpec.LoadFromFile(Bt1SpecPath),
            result.ToBt3Observations());
        var bt3 = report.Gates.Single(gate => gate.GateId == "BT3");

        Assert.That(bt3.Status, Is.EqualTo("pass"));
        Assert.That(bt3.Thresholds, Has.All.Matches<H100Bt1GateReport.ThresholdResult>(threshold =>
            threshold.Observed && threshold.ObservedValue == 0d && threshold.Pass == true));
    }

    private static BuildGrammarAuditEdge[] CompleteEdges()
        => new[]
        {
            Edge("edge-00000", "item.alpha", "acquired_by", "acquisition", "reward"),
            Edge("edge-00001", "item.alpha", "requires", "tag", "tag.melee"),
            Edge(
                "edge-00002",
                "item.alpha",
                "amplifies",
                "stat",
                "phys_power",
                "value=2",
                feedbackRequired: true,
                expectedFeedbackWitness: "telemetry.damage_applied"),
        };

    private static BuildGrammarAuditEdge Edge(
        string edgeId,
        string subjectId,
        string relation,
        string targetKind,
        string targetId,
        string truthValue = "",
        bool feedbackRequired = false,
        string expectedFeedbackWitness = "")
        => new(
            edgeId,
            "item",
            subjectId,
            relation,
            targetKind,
            targetId,
            truthValue,
            Actionable: true,
            feedbackRequired,
            expectedFeedbackWitness);

    private static PlayerVisibleBuildGrammarSemantic Visible(
        BuildGrammarAuditEdge edge,
        string visibleValue)
        => new(
            $"fact-{edge.EdgeId}",
            "reward_card",
            edge.SubjectKind,
            edge.SubjectId,
            edge.Relation,
            edge.TargetKind,
            edge.TargetId,
            visibleValue,
            AvailableBeforeChoice: true);
}
