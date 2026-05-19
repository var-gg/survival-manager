using NUnit.Framework;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// task-slice-observability-summary-v1 acceptance #1/#2/#3/#5: VerticalSliceRunSummary
/// record + builder + markdown report 직렬화.
/// </summary>
[Category("FastUnit")]
public sealed class VerticalSliceRunSummaryFastTests
{
    [Test]
    public void Build_NullInput_ReturnsEmptySentinel()
    {
        var summary = VerticalSliceRunSummaryBuilder.Build(null);
        Assert.That(summary, Is.SameAs(VerticalSliceRunSummary.Empty));
        Assert.That(summary.IsPopulated, Is.False);
    }

    [Test]
    public void Build_FullInput_PopulatesAtlasBattleRewardSections()
    {
        var input = CreateFullInput();
        var summary = VerticalSliceRunSummaryBuilder.Build(input);

        Assert.That(summary.IsPopulated, Is.True);
        Assert.That(summary.Atlas.IsPopulated, Is.True);
        Assert.That(summary.Battle.IsPopulated, Is.True);
        Assert.That(summary.Reward.IsPopulated, Is.True);

        Assert.That(summary.Atlas.RunId, Is.EqualTo("run_smoke_001"));
        Assert.That(summary.Atlas.SiteNodeIndex, Is.EqualTo(0));
        Assert.That(summary.Atlas.RewardBiasPercent, Is.EqualTo(15));
        Assert.That(summary.Atlas.ResolvedModifierIds.Count, Is.EqualTo(2));

        Assert.That(summary.Battle.EncounterId, Is.EqualTo("encounter_smoke_01"));
        Assert.That(summary.Battle.BattleContextHash, Is.Not.Empty);
        Assert.That(summary.Battle.LastSettlementWasVictory, Is.True);

        Assert.That(summary.Reward.RewardCommitId, Is.EqualTo("commit_smoke_aabb"));
        Assert.That(summary.Reward.RewardChoiceLedgerCount, Is.EqualTo(1));
    }

    [Test]
    public void Build_MissingRunId_ProducesEmptyAtlasSection()
    {
        var input = CreateFullInput() with { RunId = string.Empty };
        var summary = VerticalSliceRunSummaryBuilder.Build(input);

        Assert.That(summary.Atlas.IsPopulated, Is.False);
        Assert.That(summary.Battle.IsPopulated, Is.True);
        Assert.That(summary.Reward.IsPopulated, Is.True);
    }

    [Test]
    public void Build_MissingRewardCommitId_ProducesEmptyRewardSection()
    {
        var input = CreateFullInput() with { RewardCommitId = string.Empty };
        var summary = VerticalSliceRunSummaryBuilder.Build(input);

        Assert.That(summary.Reward.IsPopulated, Is.False);
        Assert.That(summary.Atlas.IsPopulated, Is.True);
    }

    [Test]
    public void Build_ResolvedModifierIdsFilterBlanksAndNulls()
    {
        var input = CreateFullInput() with
        {
            ResolvedModifierIds = new[] { "mod_a", string.Empty, " ", "mod_b" },
        };
        var summary = VerticalSliceRunSummaryBuilder.Build(input);

        Assert.That(summary.Atlas.ResolvedModifierIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void ToMarkdown_PopulatedSummary_RendersAllThreeSectionsAndNextCycleNote()
    {
        var summary = VerticalSliceRunSummaryBuilder.Build(CreateFullInput());
        var markdown = VerticalSliceRunSummaryBuilder.ToMarkdown(summary);

        Assert.That(markdown, Does.StartWith("# Vertical Slice Run Summary"));
        Assert.That(markdown, Does.Contain("## Atlas"));
        Assert.That(markdown, Does.Contain("## Battle"));
        Assert.That(markdown, Does.Contain("## Reward"));
        Assert.That(markdown, Does.Contain("run_smoke_001"));
        Assert.That(markdown, Does.Contain("encounter_smoke_01"));
        Assert.That(markdown, Does.Contain("commit_smoke_aabb"));
        Assert.That(markdown, Does.Contain("Cinematic detector 10종"),
            "acceptance #7: next cycle scope 명시 (detector stub 위장 X).");
    }

    [Test]
    public void ToMarkdown_EmptySummary_RendersNoPopulatedSentinel()
    {
        var markdown = VerticalSliceRunSummaryBuilder.ToMarkdown(VerticalSliceRunSummary.Empty);
        Assert.That(markdown, Does.Contain("_no populated section yet._"));
    }

    private static VerticalSliceRunSummaryInput CreateFullInput()
    {
        return new VerticalSliceRunSummaryInput(
            RunId: "run_smoke_001",
            ChapterId: "chapter_alpha",
            SiteId: "site_alpha_gate",
            SiteNodeIndex: 0,
            EncounterId: "encounter_smoke_01",
            FactionId: "faction_wolfpine",
            BattleSeed: 12345,
            IsBoss: false,
            LastSettlementWasVictory: true,
            BattleContextHash: "ctx_smoke_aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899",
            NodeOverlayHash: "noh_smoke",
            SelectedRouteHash: "spch_smoke",
            RewardBiasPercent: 15,
            ThreatPressurePercent: 5,
            AffinityBoostPercent: 10,
            ResolvedModifierIds: new[] { "RewardBias:reward", "ThreatPressure:threat" },
            RewardSourceId: "reward_source_smoke",
            RewardCommitId: "commit_smoke_aabb",
            RewardChoiceLedgerCount: 1,
            HasPendingRewardSettlement: false);
    }
}
