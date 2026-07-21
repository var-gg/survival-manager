using System;
using System.IO;
using NUnit.Framework;
using SM.Editor.Determinism;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BossDeckBattleHashCorpusGoldenTests
{
    private static string GoldenPath => Path.Combine(
        Application.dataPath,
        "Tests/EditMode/FastUnit/Golden/boss-deck-battle-hash-corpus.golden.txt");

    [Test]
    public void AuthoredBossDeckBattles_MatchCheckedInGolden()
    {
        Assert.That(File.Exists(GoldenPath), Is.True, $"Missing boss deck battle golden: {GoldenPath}");
        var expected = Normalize(File.ReadAllText(GoldenPath));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        var current = Normalize(BossDeckBattleHashCorpus.Generate(lookup));
        Assert.That(
            current,
            Is.EqualTo(expected),
            "An intended boss deck content change requires recapturing the focused boss battle golden.");
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
