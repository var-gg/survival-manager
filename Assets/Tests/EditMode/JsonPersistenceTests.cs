using System;
using System.IO;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;

namespace SM.Tests.EditMode;

public class JsonPersistenceTests
{
    [Test]
    public void JsonSaveRepository_RoundTrips_Profile_Data()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm_json_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new JsonSaveRepository(root);
            var profile = repo.LoadOrCreate("default");
            profile.DisplayName = "Player";
            profile.Currencies.Gold = 12;
            profile.UnlockedPermanentAugmentIds.Add("augment_perm_legacy_blade");
            profile.RunSummaries.Add(new RunSummaryRecord
            {
                RunId = "run_001",
                ExpeditionId = "expedition_mvp_demo",
                Result = "victory",
                GoldEarned = 12,
                NodesCleared = 5,
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
            });

            repo.Save(profile);
            var loaded = repo.LoadOrCreate("default");

            Assert.That(loaded.Currencies.Gold, Is.EqualTo(12));
            Assert.That(loaded.UnlockedPermanentAugmentIds, Has.Count.EqualTo(1));
            Assert.That(loaded.RunSummaries, Has.Count.EqualTo(1));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
