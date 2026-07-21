using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Core.Stats;
using SM.Editor.Determinism;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class DuelistBattleHashCorpusGoldenTests
{
    private static string GoldenPath => Path.Combine(
        Application.dataPath,
        "Tests/EditMode/FastUnit/Golden/duelist-battle-hash-corpus.golden.txt");

    [Test]
    public void RatifiedDuelistBattles_MatchCheckedInGolden()
    {
        Assert.That(File.Exists(GoldenPath), Is.True, $"Missing Duelist battle golden: {GoldenPath}");
        var content = new RuntimeCombatContentLookup().Snapshot;
        var expected = Normalize(File.ReadAllText(GoldenPath));
        var current = Normalize(DuelistBattleHashCorpus.Generate(content));
        Assert.That(
            current,
            Is.EqualTo(expected),
            "Ratified Duelist content battle hashes diverged; recapture only for an intended Duelist balance change.");
    }

    [Test]
    public void RatifiedDuelistBattles_DifferFromPreRatificationValues()
    {
        var content = new RuntimeCombatContentLookup().Snapshot;
        var archetypes = content.Archetypes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var slayer = archetypes["slayer"];
        var classPackage = slayer.ClassStatPackage
                           ?? throw new AssertionException("Slayer class package is missing.");
        archetypes["slayer"] = slayer with
        {
            ClassStatPackage = classPackage with
            {
                Modifiers = classPackage.Modifiers
                    .Where(modifier => modifier.Stat != StatKey.PhysPower || modifier.Op != ModifierOp.More)
                    .ToArray(),
            },
        };

        var passives = content.PassiveNodes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var gate = passives["passive_duelist_notable_01"];
        passives[gate.Id] = gate with
        {
            Package = gate.Package with
            {
                Modifiers = gate.Package.Modifiers.Select(modifier =>
                        modifier.Stat == StatKey.MaxHealth && modifier.Op == ModifierOp.More
                            ? modifier with { Value = -0.06f }
                            : modifier)
                    .ToArray(),
            },
        };

        var before = content with
        {
            Archetypes = new Dictionary<string, CombatArchetypeTemplate>(archetypes, StringComparer.Ordinal),
            PassiveNodes = new Dictionary<string, PassiveNodeTemplate>(passives, StringComparer.Ordinal),
        };
        Assert.That(
            DuelistBattleHashCorpus.Generate(content),
            Is.Not.EqualTo(DuelistBattleHashCorpus.Generate(before)),
            "The ratified HP gate and baseline coefficient must change Duelist-involved battle hashes.");
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
