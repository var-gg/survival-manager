using SM.Meta.Model;
using SM.Meta.Services;

internal sealed partial class HeadlessCampaignState
{
    private HeadlessCampaignState(HeadlessCampaignState source, int heat)
    {
        Lookup = source.Lookup;
        Snapshot = source.Snapshot;
        Cell = source.Cell;
        CampaignSeedSalt = source.CampaignSeedSalt;
        Heat = heat;
        Heroes = source.Heroes.Select(CloneHero).ToList();
        Inventory = source.Inventory.Select(CloneItem).ToList();
        _itemInstanceCounter = source._itemInstanceCounter;
        _encounterResolver = new EncounterResolutionService(Snapshot);
        CampaignSeed = source.CampaignSeed;
        Progress = source.Progress with
        {
            ClearedChapterIds = source.Progress.ClearedChapterIds.ToArray(),
            ClearedSiteIds = source.Progress.ClearedSiteIds.ToArray(),
        };
        ActiveRun = CloneActiveRun(source.ActiveRun);
        CurrentNodeIndex = source.CurrentNodeIndex;
        Gold = source.Gold;
        Echo = source.Echo;
        _siteTrack = source._siteTrack.ToArray();
        foreach (var (anchor, heroId) in source._assignments)
        {
            _assignments[anchor] = heroId;
        }

        _expeditionSquadHeroIds.AddRange(source._expeditionSquadHeroIds);
        _latestItemGrades.AddRange(source._latestItemGrades);
    }

    internal HeadlessCampaignState CloneWithHeat(int heat)
    {
        if (heat < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heat), heat, "Heat must be non-negative.");
        }

        return new HeadlessCampaignState(this, heat);
    }

    private static HeadlessCampaignHero CloneHero(HeadlessCampaignHero source)
    {
        var clone = new HeadlessCampaignHero(source.Record)
        {
            Level = source.Level,
            Experience = source.Experience,
            CurrentHp = source.CurrentHp,
            MaxHp = source.MaxHp,
            PassiveBoardId = source.PassiveBoardId,
        };
        clone.EquippedItemIds.AddRange(source.EquippedItemIds);
        clone.SelectedPassiveNodeIds.AddRange(source.SelectedPassiveNodeIds);
        return clone;
    }

    private static HeadlessCampaignItem CloneItem(HeadlessCampaignItem source)
        => new(
            source.InstanceId,
            source.ItemBaseId,
            source.AffixIds.ToArray(),
            source.AffixMagnitudes.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            source.EquippedHeroId,
            source.AcquisitionIndex,
            source.RarityTier,
            source.RefitLevel);

    private static ActiveRunState? CloneActiveRun(ActiveRunState? source)
    {
        if (source == null)
        {
            return null;
        }

        var blueprint = source.Blueprint with
        {
            DeploymentAssignments = source.Blueprint.DeploymentAssignments.ToDictionary(
                pair => pair.Key,
                pair => pair.Value),
            ExpeditionSquadHeroIds = source.Blueprint.ExpeditionSquadHeroIds.ToArray(),
            HeroRoleIds = source.Blueprint.HeroRoleIds.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            HeroTargetDirectives = source.Blueprint.HeroTargetDirectives?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
        };
        var overlay = source.Overlay with
        {
            TemporaryAugmentIds = source.Overlay.TemporaryAugmentIds.ToArray(),
            PendingRewardIds = source.Overlay.PendingRewardIds.ToArray(),
        };
        return source with
        {
            Blueprint = blueprint,
            Overlay = overlay,
            BattleDeployHeroIds = source.BattleDeployHeroIds.ToArray(),
            ActiveWoundHeroIds = source.ActiveWoundHeroIds?.ToArray(),
        };
    }
}
