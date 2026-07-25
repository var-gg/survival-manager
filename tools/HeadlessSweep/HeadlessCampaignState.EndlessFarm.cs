using System.Globalization;
using SM.Core.Content;
using SM.Meta.Services;

internal sealed partial class HeadlessCampaignState
{
    internal HeadlessCampaignFarmResult FarmSiteMaps(string siteId, int mapCount)
        => FarmSiteMaps(
            siteId,
            mapCount,
            EndlessCycleService.HeatDropLatentMeanNumerator,
            EndlessCycleService.HeatDropJackpotWeightStep);

    internal HeadlessCampaignFarmResult FarmSiteMaps(
        string siteId,
        int mapCount,
        double heatDropLatentMeanNumerator,
        double heatDropJackpotWeightStep)
    {
        if (mapCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapCount), mapCount, "Farm map count must be positive.");
        }

        var gradeRolls = new List<DropGradeRollObservation>();
        var naturalDrops = new List<HeadlessCampaignNaturalDrop>();
        var itemDrops = 0;
        var echoEarned = 0;
        int? battleRewardNodesPerMap = null;
        for (var mapIndex = 0; mapIndex < mapCount; mapIndex++)
        {
            var map = FarmSiteMap(
                siteId,
                mapIndex,
                heatDropLatentMeanNumerator,
                heatDropJackpotWeightStep);
            if (battleRewardNodesPerMap.HasValue
                && battleRewardNodesPerMap.Value != map.BattleRewardNodesPerMap)
            {
                throw new InvalidDataException(
                    $"Endless farm changed battle reward node count within one horizon: "
                    + $"{battleRewardNodesPerMap.Value} -> {map.BattleRewardNodesPerMap}.");
            }

            battleRewardNodesPerMap = map.BattleRewardNodesPerMap;
            itemDrops += map.ItemDrops;
            echoEarned += map.EchoEarned;
            gradeRolls.AddRange(map.GradeRolls);
            naturalDrops.AddRange(map.NaturalDrops ?? Array.Empty<HeadlessCampaignNaturalDrop>());
        }

        if (gradeRolls.Count != itemDrops)
        {
            throw new InvalidDataException(
                $"Endless farm measured {gradeRolls.Count} grade rolls for {itemDrops} item drops. "
                + "The reward harness requires one grade roll per dropped item.");
        }

        return new HeadlessCampaignFarmResult(
            mapCount,
            battleRewardNodesPerMap ?? 0,
            itemDrops,
            gradeRolls,
            echoEarned,
            naturalDrops);
    }

    internal HeadlessCampaignFarmResult FarmSiteMap(string siteId, int mapIndex)
        => FarmSiteMap(
            siteId,
            mapIndex,
            EndlessCycleService.HeatDropLatentMeanNumerator,
            EndlessCycleService.HeatDropJackpotWeightStep);

    internal HeadlessCampaignFarmResult FarmSiteMap(
        string siteId,
        int mapIndex,
        double heatDropLatentMeanNumerator,
        double heatDropJackpotWeightStep)
    {
        if (mapIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapIndex), mapIndex, "Farm map index must be non-negative.");
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

        var gradeRolls = new List<DropGradeRollObservation>();
        var naturalDrops = new List<HeadlessCampaignNaturalDrop>();
        var service = new LootResolutionService(
            Snapshot,
            gradeRolls.Add,
            heatDropLatentMeanNumerator,
            heatDropJackpotWeightStep);
        var itemDrops = 0;
        var echoEarned = 0;
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

            foreach (var entry in bundle.Entries)
            {
                if (entry.RewardType == RewardType.Echo)
                {
                    var amount = EndlessCycleService.ScaleEchoAmount(entry.Amount, Heat);
                    Echo = checked(Echo + amount);
                    echoEarned = checked(echoEarned + amount);
                    continue;
                }

                if (entry.RewardType != RewardType.Item)
                {
                    continue;
                }

                for (var amount = 0; amount < Math.Max(1, entry.Amount); amount++)
                {
                    var item = AddGeneratedItem(entry.Id, entry.ItemGrade);
                    naturalDrops.Add(new HeadlessCampaignNaturalDrop(
                        item.InstanceId,
                        item.ItemBaseId,
                        item.AcquisitionIndex,
                        item.RarityTier,
                        item.AffixIds.ToArray(),
                        item.AffixMagnitudes.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value,
                            StringComparer.Ordinal)));
                    itemDrops++;
                }
            }
        }

        if (gradeRolls.Count != itemDrops)
        {
            throw new InvalidDataException(
                $"Endless farm measured {gradeRolls.Count} grade rolls for {itemDrops} item drops. "
                + "The reward harness requires one grade roll per dropped item.");
        }

        return new HeadlessCampaignFarmResult(
            Maps: 1,
            BattleRewardNodesPerMap: track.Length,
            ItemDrops: itemDrops,
            GradeRolls: gradeRolls,
            EchoEarned: echoEarned,
            NaturalDrops: naturalDrops);
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
