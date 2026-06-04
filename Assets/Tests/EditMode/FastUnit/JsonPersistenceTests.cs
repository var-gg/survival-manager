using System;
using System.IO;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
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
            profile.ActiveBlueprintId = "blueprint.default";
            profile.HeroLoadouts.Add(new HeroLoadoutRecord
            {
                HeroId = "hero-1",
                EquippedItemInstanceIds = new System.Collections.Generic.List<string> { "item-1" },
                EquippedPermanentAugmentIds = new System.Collections.Generic.List<string> { "augment_perm_legacy_blade" }
            });
            profile.ActiveRun = new ActiveRunRecord
            {
                RunId = "run_active_001",
                ExpeditionId = "expedition_mvp_demo",
                BlueprintId = "blueprint.default",
                CompileVersion = "build-compile-audit.v1",
                CompileHash = "abc123",
                PledgedWarrantId = "warrant_intact",
                TemporaryAugmentIds = new System.Collections.Generic.List<string> { "augment_silver_guard" }
            };
            profile.MatchHeaders.Add(new MatchRecordHeader
            {
                MatchId = "match_001",
                RunId = "run_active_001",
                ContentVersion = "build-compile-audit.v1",
                SimVersion = "live-sim.v1",
                Winner = "Ally",
                FinalStateHash = "final-hash"
            });
            profile.MatchBlobs.Add(new MatchRecordBlob
            {
                MatchId = "match_001",
                CompileVersion = "build-compile-audit.v1",
                CompileHash = "abc123",
                EventStream = new System.Collections.Generic.List<string> { "0|actor|BasicAttack|target|4|basic_attack" }
            });
            profile.RewardLedger.Add(new RewardLedgerEntryRecord
            {
                EntryId = "reward_001",
                RunId = "run_active_001",
                RewardId = "reward.gold.10",
                RewardType = "Gold",
                Amount = 10,
                CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                Summary = "10 Gold"
            });
            profile.RunSummaries.Add(new RunSummaryRecord
            {
                RunId = "run_001",
                ExpeditionId = "expedition_mvp_demo",
                Result = "victory",
                GoldEarned = 12,
                NodesCleared = 5,
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
            });
            profile.Dossier.Add(new DossierEntryRecord
            {
                EntryId = "dossier_001",
                RunId = "run_active_001",
                ChapterId = "chapter_mvp_demo",
                SiteId = "site_mvp_demo",
                NodeId = "node-1",
                Result = "victory",
                Outcome = "clean_victory",
                SurvivorAllyCount = 4,
                TotalAllyCount = 4,
                FallenAllyIds = new System.Collections.Generic.List<string>(),
                WarrantId = "warrant_intact",
                WarrantOutcome = "kept",
                WarrantFailureReason = "none",
                WarrantSeverity = "none",
                WarrantObservedTurnCount = 7,
                WarrantResolvedTurnLimit = 8,
                IssuerFactionId = "faction_solarum",
                OpposedFactionId = "faction_pale_conclave",
                RejectedFactionIds = new System.Collections.Generic.List<string> { "faction_wolfpine_tribes", "faction_lattice_order" },
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
            });
            profile.FactionStanding.Add(new FactionStandingRecord { FactionId = "faction_solarum", Trust = 5 });

            repo.Save(profile);
            var loaded = repo.LoadOrCreate("default");

            Assert.That(loaded.Currencies.Gold, Is.EqualTo(12));
            Assert.That(loaded.UnlockedPermanentAugmentIds, Has.Count.EqualTo(1));
            Assert.That(loaded.ActiveRun.RunId, Is.EqualTo("run_active_001"));
            Assert.That(loaded.HeroLoadouts, Has.Count.EqualTo(1));
            Assert.That(loaded.MatchHeaders, Has.Count.EqualTo(1));
            Assert.That(loaded.MatchBlobs[0].CompileHash, Is.EqualTo("abc123"));
            Assert.That(loaded.RewardLedger, Has.Count.EqualTo(1));
            Assert.That(loaded.RunSummaries, Has.Count.EqualTo(1));
            Assert.That(loaded.Dossier, Has.Count.EqualTo(1));
            Assert.That(loaded.Dossier[0].Outcome, Is.EqualTo("clean_victory"));
            Assert.That(loaded.Dossier[0].SurvivorAllyCount, Is.EqualTo(4));
            Assert.That(loaded.Dossier[0].WarrantId, Is.EqualTo("warrant_intact"));
            Assert.That(loaded.Dossier[0].WarrantOutcome, Is.EqualTo("kept"));
            Assert.That(loaded.Dossier[0].WarrantFailureReason, Is.EqualTo("none"));
            Assert.That(loaded.Dossier[0].WarrantSeverity, Is.EqualTo("none"));
            Assert.That(loaded.Dossier[0].WarrantObservedTurnCount, Is.EqualTo(7));
            Assert.That(loaded.Dossier[0].WarrantResolvedTurnLimit, Is.EqualTo(8));
            Assert.That(loaded.Dossier[0].IssuerFactionId, Is.EqualTo("faction_solarum"));
            Assert.That(loaded.Dossier[0].OpposedFactionId, Is.EqualTo("faction_pale_conclave"));
            Assert.That(loaded.Dossier[0].RejectedFactionIds, Is.EquivalentTo(new[] { "faction_wolfpine_tribes", "faction_lattice_order" }));
            Assert.That(loaded.FactionStanding, Has.Count.EqualTo(1));
            Assert.That(loaded.FactionStanding[0].FactionId, Is.EqualTo("faction_solarum"));
            Assert.That(loaded.FactionStanding[0].Trust, Is.EqualTo(5));
            Assert.That(loaded.ActiveRun.PledgedWarrantId, Is.EqualTo("warrant_intact"));
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
