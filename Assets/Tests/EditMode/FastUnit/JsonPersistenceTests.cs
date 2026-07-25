using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;
using SM.Tests.EditMode.Fakes;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public class JsonPersistenceTests
{
    [Test]
    public void InventoryAffixMagnitudes_LegacyJsonFallsBackEmpty_AndRoundTripsExactly()
    {
        const string legacyJson =
            "{\"ItemInstanceId\":\"legacy-item\",\"ItemBaseId\":\"legacy-base\",\"AffixIds\":[\"affix_legacy\"]}";
        var legacy = DeserializeNewtonsoft<InventoryItemRecord>(legacyJson);

        Assert.That(legacy, Is.Not.Null);
        Assert.That(legacy!.AffixMagnitudeRolls, Is.Empty);

        var root = Path.Combine(Path.GetTempPath(), "sm_affix_magnitude_" + Guid.NewGuid().ToString("N"));
        try
        {
            legacy.AffixMagnitudeRolls.Add(new InventoryAffixMagnitudeRecord
            {
                AffixId = "affix_legacy",
                Magnitude = 1.2345678f,
            });
            var repository = new JsonSaveRepository(root);
            var profile = new SaveProfile { ProfileId = "affix-magnitude" };
            profile.Inventory.Add(legacy);
            repository.Save(profile);

            var loaded = repository.LoadOrCreate("affix-magnitude").Inventory.Single();
            Assert.That(loaded.AffixMagnitudeRolls, Has.Count.EqualTo(1));
            Assert.That(loaded.AffixMagnitudeRolls[0].AffixId, Is.EqualTo("affix_legacy"));
            Assert.That(
                BitConverter.SingleToInt32Bits(loaded.AffixMagnitudeRolls[0].Magnitude),
                Is.EqualTo(BitConverter.SingleToInt32Bits(1.2345678f)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Test]
    public void InventoryItemRefitLevel_LegacyJsonDefaultsToZero_AndRoundTrips()
    {
        const string legacyJson =
            "{\"ItemInstanceId\":\"legacy-item\",\"ItemBaseId\":\"legacy-base\",\"AffixIds\":[]}";
        var legacy = DeserializeNewtonsoft<InventoryItemRecord>(legacyJson);

        Assert.That(legacy, Is.Not.Null);
        Assert.That(legacy!.RefitLevel, Is.Zero);

        var root = Path.Combine(Path.GetTempPath(), "sm_refit_level_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonSaveRepository(root);
            var profile = new SaveProfile { ProfileId = "refit-level" };
            legacy.RefitLevel = 7;
            profile.Inventory.Add(legacy);
            repository.Save(profile);

            Assert.That(repository.LoadOrCreate("refit-level").Inventory.Single().RefitLevel, Is.EqualTo(7));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Test]
    public void RefitSaveRoundTrip_PreservesTheSameNextConditionedResult()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.AccessoryItemId,
            ItemRarityTierValue.Legendary,
            0);
        var saved = new InventoryItemRecord
        {
            ItemInstanceId = "persistent-refit-item",
            ItemBaseId = RefitTestFixture.AccessoryItemId,
            RolledRarityTier = (int)ItemRarityTierValue.Legendary,
            AffixIds = affixes.ToList(),
            RefitLevel = 0,
        };
        var root = Path.Combine(Path.GetTempPath(), "sm_refit_output_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonSaveRepository(root);
            var profile = new SaveProfile { ProfileId = "refit-output" };
            profile.Inventory.Add(saved);
            repository.Save(profile);
            var loaded = repository.LoadOrCreate("refit-output").Inventory.Single();
            var service = RefitTestFixture.CreateService(lookup);

            var before = service.RefitNextEffective(
                ToState(saved),
                RefitTestFixture.CreateEconomy(lookup),
                0xBEEFUL);
            var after = service.RefitNextEffective(
                ToState(loaded),
                RefitTestFixture.CreateEconomy(lookup),
                0xBEEFUL);

            Assert.That(before.Applied, Is.True, before.Error);
            Assert.That(after.Applied, Is.True, after.Error);
            Assert.That(after.Quote, Is.EqualTo(before.Quote));
            Assert.That(after.AffixIds, Is.EqualTo(before.AffixIds));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Test]
    public void JsonSaveRepository_RoundTrips_Profile_Data()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm_json_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new JsonSaveRepository(root);
            var profile = repo.LoadOrCreate("default");
            profile.DisplayName = "Player";
            profile.HeroInstanceCounter = 17;
            profile.ItemInstanceCounter = 29;
            profile.Currencies.Gold = 12;
            profile.UnlockedPermanentAugmentIds.Add("augment_perm_legacy_blade");
            profile.CampaignProgress.RewardedRevisitCountsByChapter["chapter_alpha"] = 4;
            profile.CampaignProgress.DefeatConsolationCountsByChapter["chapter_alpha"] = 2;
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
                RewardedRevisitIndex = 3,
                RevisitItemRollsGranted = 2,
                RevisitCurrencyGranted = true,
                ActiveWoundHeroIds = new System.Collections.Generic.List<string> { "hero-1" },
                ResolvedExpeditionNodeIds = new System.Collections.Generic.List<string> { "entry", "risk" },
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
                EventStream = new System.Collections.Generic.List<string> { "0|actor|BasicAttack|target|4|basic_attack" },
                PoliticalConditions = new System.Collections.Generic.List<string> { "faction_solarum|AllySupport|support.trust_threshold" }
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
                PoliticalEffects = new System.Collections.Generic.List<DossierPoliticalEffectRecord>
                {
                    new DossierPoliticalEffectRecord { FactionId = "faction_solarum", Delta = 2, Reason = "kept_issuer" },
                },
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
            });
            profile.FactionStanding.Add(new FactionStandingRecord { FactionId = "faction_solarum", Trust = 5 });

            repo.Save(profile);
            var loaded = repo.LoadOrCreate("default");

            Assert.That(loaded.Currencies.Gold, Is.EqualTo(12));
            Assert.That(loaded.HeroInstanceCounter, Is.EqualTo(17));
            Assert.That(loaded.ItemInstanceCounter, Is.EqualTo(29));
            Assert.That(loaded.UnlockedPermanentAugmentIds, Has.Count.EqualTo(1));
            Assert.That(loaded.CampaignProgress.RewardedRevisitCountsByChapter["chapter_alpha"], Is.EqualTo(4));
            Assert.That(loaded.CampaignProgress.DefeatConsolationCountsByChapter["chapter_alpha"], Is.EqualTo(2));
            Assert.That(loaded.ActiveRun.RunId, Is.EqualTo("run_active_001"));
            Assert.That(loaded.ActiveRun.RewardedRevisitIndex, Is.EqualTo(3));
            Assert.That(loaded.ActiveRun.RevisitItemRollsGranted, Is.EqualTo(2));
            Assert.That(loaded.ActiveRun.RevisitCurrencyGranted, Is.True);
            Assert.That(loaded.HeroLoadouts, Has.Count.EqualTo(1));
            Assert.That(loaded.MatchHeaders, Has.Count.EqualTo(1));
            Assert.That(loaded.MatchBlobs[0].CompileHash, Is.EqualTo("abc123"));
            Assert.That(loaded.MatchBlobs[0].PoliticalConditions, Is.EquivalentTo(new[] { "faction_solarum|AllySupport|support.trust_threshold" }), "정치 condition provenance가 audit round-trip된다.");
            Assert.That(loaded.RewardLedger, Has.Count.EqualTo(1));
            Assert.That(loaded.RunSummaries, Has.Count.EqualTo(1));
            Assert.That(loaded.Dossier, Has.Count.EqualTo(1));
            Assert.That(loaded.Dossier[0].PoliticalEffects, Has.Count.EqualTo(1), "incident-centric 정치 효과가 round-trip된다.");
            Assert.That(loaded.Dossier[0].PoliticalEffects[0].FactionId, Is.EqualTo("faction_solarum"));
            Assert.That(loaded.Dossier[0].PoliticalEffects[0].Delta, Is.EqualTo(2));
            Assert.That(loaded.Dossier[0].PoliticalEffects[0].Reason, Is.EqualTo("kept_issuer"));
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
            Assert.That(loaded.ActiveRun.ActiveWoundHeroIds, Is.EqualTo(new[] { "hero-1" }));
            Assert.That(loaded.ActiveRun.ResolvedExpeditionNodeIds, Is.EqualTo(new[] { "entry", "risk" }));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static RefitItemState ToState(InventoryItemRecord item)
        => new(
            item.ItemBaseId,
            $"{item.ItemBaseId}|0",
            (ItemRarityTierValue)item.RolledRarityTier,
            item.AffixIds,
            item.RefitLevel);

    private static T DeserializeNewtonsoft<T>(string json)
    {
        var jsonConvert = Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert");
        Assert.That(jsonConvert, Is.Not.Null);
        var method = jsonConvert!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate =>
                candidate.Name == "DeserializeObject"
                && candidate.IsGenericMethodDefinition
                && candidate.GetParameters().Length == 1
                && candidate.GetParameters()[0].ParameterType == typeof(string));
        return (T)method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { json })!;
    }
}
