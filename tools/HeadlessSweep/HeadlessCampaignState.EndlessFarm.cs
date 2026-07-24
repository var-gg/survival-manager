using System.Globalization;
using SM.Core.Content;
using SM.Meta.Services;

internal sealed partial class HeadlessCampaignState
{
    internal HeadlessCampaignFarmResult FarmSiteMaps(string siteId, int mapCount)
    {
        if (mapCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapCount), mapCount, "Farm map count must be positive.");
        }

        if (Snapshot.ExpeditionSites is not { } sites
            || !sites.TryGetValue(siteId, out var site))
        {
            throw new InvalidDataException($"Endless farm site '{siteId}' was not found.");
        }

        var track = _encounterResolver.BuildSiteTrack(site.ChapterId, siteId)
            .Where(node => node.RequiresBattle && !string.IsNullOrWhiteSpace(node.RewardSourceId))
            .OrderBy(node => node.Index)
            .ToArray();
        if (track.Length == 0)
        {
            throw new InvalidDataException($"Endless farm site '{siteId}' has no battle reward nodes.");
        }

        var service = new LootResolutionService(Snapshot);
        var itemDrops = 0;
        for (var mapIndex = 0; mapIndex < mapCount; mapIndex++)
        {
            foreach (var node in track)
            {
                var seed = CampaignEncounterSeed.Derive(
                    CampaignSeed,
                    $"endless-farm|{siteId}|map={mapIndex.ToString(CultureInfo.InvariantCulture)}|node={node.NodeId}");
                var contextTags = ResolveFarmRewardContextTags(site.ChapterId, siteId, node.EncounterId);
                if (!service.TryResolveBundle(
                        node.RewardSourceId,
                        seed,
                        contextTags,
                        out var bundle,
                        out var error,
                        Heat))
                {
                    throw new InvalidDataException(
                        $"Endless farm loot failed ({siteId}/{node.NodeId}/map={mapIndex}): {error}");
                }

                foreach (var entry in bundle.Entries.Where(entry => entry.RewardType == RewardType.Item))
                {
                    for (var amount = 0; amount < Math.Max(1, entry.Amount); amount++)
                    {
                        AddGeneratedItem(entry.Id, entry.ItemGrade);
                        itemDrops++;
                    }
                }
            }
        }

        return new HeadlessCampaignFarmResult(mapCount, track.Length, itemDrops);
    }

    private IReadOnlyList<string> ResolveFarmRewardContextTags(
        string chapterId,
        string siteId,
        string encounterId)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal)
        {
            chapterId,
            siteId,
        };
        if (Snapshot.Encounters is { } encounters
            && encounters.TryGetValue(encounterId, out var encounter))
        {
            foreach (var tag in encounter.RewardDropTags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
            {
                tags.Add(tag);
            }

            if (!string.IsNullOrWhiteSpace(encounter.BossOverlayId)
                && Snapshot.BossOverlays is { } overlays
                && overlays.TryGetValue(encounter.BossOverlayId, out var overlay))
            {
                foreach (var tag in overlay.RewardDropTags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
                {
                    tags.Add(tag);
                }
            }
        }

        return tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
    }
}
