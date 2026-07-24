using System;
using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class AffixQualityProfileTractabilityTests
{
    [Test]
    public void LargestAccessoryPool_LegendaryProfile_StaysWithinHardGate()
    {
        var lookup = new RuntimeCombatContentLookup();
        var gradeStepBudget = lookup.Snapshot.DropTables!.Values
            .Select(table => table.GradeStepBudgetScore)
            .Distinct()
            .Single();
        const string itemBaseId = "item_bulwark_trinket";

        var compiler = new AffixQualityProfileCompiler();
        var profile = compiler.Compile(
            lookup,
            itemBaseId,
            ItemRarityTierValue.Legendary,
            gradeStepBudget,
            "shipped-affix-catalog-v1",
            out var metrics);

        TestContext.Out.WriteLine(
            "REFIT_A1_TRACTABILITY "
            + $"profile={profile.Key.ItemBaseId}|{profile.Key.SlotType}|{profile.Key.Grade} "
            + $"states={metrics.DistinctMemoizedStates} "
            + $"terminal_sequences={metrics.TerminalSequences} "
            + $"seconds={metrics.Elapsed.TotalSeconds:F6} "
            + $"peak_memory_mb={metrics.PeakMemoryBytes / (1024d * 1024d):F3} "
            + $"support_size={metrics.SupportSize}");

        Assert.That(profile.Key.SlotType, Is.EqualTo("Accessory"));
        Assert.That(profile.SupportScoreQ, Is.Not.Empty);
        Assert.That(metrics.Elapsed, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(60)));
        Assert.That(metrics.DistinctMemoizedStates, Is.LessThanOrEqualTo(10_000_000));
    }
}
