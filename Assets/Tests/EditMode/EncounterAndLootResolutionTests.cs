using System.Linq;
using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

public sealed class EncounterAndLootResolutionTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(EncounterAndLootResolutionTests));
    }

    [Test]
    public void GameSessionState_SameNodeContext_ResolvesSameEncounterAndSeed()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());
        session.BeginNewExpedition();
        session.BuildBattleLoadoutSnapshot();

        Assert.That(session.TryResolveCurrentEncounter(out var first, out var firstError), Is.True, firstError);
        Assert.That(session.TryResolveCurrentEncounter(out var second, out var secondError), Is.True, secondError);

        Assert.That(first.Context.EncounterId, Is.EqualTo(second.Context.EncounterId));
        Assert.That(first.Context.BattleSeed, Is.EqualTo(second.Context.BattleSeed));
        Assert.That(first.Context.EncounterId, Does.Not.Contain("debug_smoke"));
        Assert.That(first.Context.RewardSourceId, Is.Not.EqualTo("reward_source_debug_smoke"));
    }

    [Test]
    public void EncounterResolutionService_AllStorySitesCleared_UnlocksEndless()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        var resolver = new EncounterResolutionService(snapshot);
        var normalized = resolver.NormalizeCampaignProgress(new CampaignProgressState(
            string.Empty,
            string.Empty,
            snapshot.CampaignChapters!.Keys.ToList(),
            snapshot.ExpeditionSites!.Keys.ToList(),
            false,
            false));

        Assert.That(normalized.StoryCleared, Is.True);
        Assert.That(normalized.EndlessUnlocked, Is.True);
    }

    [Test]
    public void LootResolutionService_SameSeedAndSource_ProducesDeterministicBundle()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        var service = new LootResolutionService(snapshot);
        Assert.That(service.TryResolveBundle("reward_source_boss", 12345, out var first, out var firstError), Is.True, firstError);
        Assert.That(service.TryResolveBundle("reward_source_boss", 12345, out var second, out var secondError), Is.True, secondError);

        Assert.That(first.Entries.Select(entry => $"{entry.Id}:{entry.RewardType}:{entry.Amount}:{entry.RarityBracket}"),
            Is.EqualTo(second.Entries.Select(entry => $"{entry.Id}:{entry.RewardType}:{entry.Amount}:{entry.RarityBracket}")));
    }
}
