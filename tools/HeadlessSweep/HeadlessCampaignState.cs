using System.Globalization;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Editor.Validation;
using SM.HeadlessPolicies;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;

internal sealed partial class HeadlessCampaignState
{
    internal static readonly DeploymentAnchorId[] DeploymentAnchors =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom,
    };

    private readonly EncounterResolutionService _encounterResolver;
    private readonly LoadoutCompiler _loadoutCompiler = new();
    private readonly Dictionary<DeploymentAnchorId, string> _assignments = new();
    private readonly List<string> _expeditionSquadHeroIds = new();
    private readonly List<ItemRarityTierValue> _latestItemGrades = new();
    private IReadOnlyList<SiteTrackNodeState> _siteTrack = Array.Empty<SiteTrackNodeState>();
    private int _itemInstanceCounter;

    private HeadlessCampaignState(
        SnapshotSessionContentLookup lookup,
        CampaignBalanceGridCell cell,
        int campaignSeedSalt,
        int heat,
        IReadOnlyList<HeadlessCampaignHero> heroes,
        IReadOnlyList<HeadlessCampaignItem> inventory)
    {
        Lookup = lookup;
        Snapshot = lookup.Snapshot;
        Cell = cell;
        CampaignSeedSalt = campaignSeedSalt;
        Heat = heat;
        Heroes = heroes.ToList();
        Inventory = inventory.ToList();
        _itemInstanceCounter = Inventory.Count;
        _encounterResolver = new EncounterResolutionService(Snapshot);
        var campaignIdentity = $"campaign-two-arm-{CellTag(cell)}";
        if (campaignSeedSalt != 0)
        {
            campaignIdentity += $"|seed-salt={campaignSeedSalt.ToString(CultureInfo.InvariantCulture)}";
        }

        CampaignSeed = CampaignEncounterSeed.FromCampaignIdentity(
            campaignIdentity);
        Progress = _encounterResolver.NormalizeCampaignProgress(new CampaignProgressState(
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            false));
        _expeditionSquadHeroIds.AddRange(Heroes.Take(MetaBalanceDefaults.BattleDeployCap).Select(hero => hero.Id));
        ApplyClassBasedDeployment();
    }

    internal SnapshotSessionContentLookup Lookup { get; }
    internal CombatContentSnapshot Snapshot { get; }
    internal CampaignBalanceGridCell Cell { get; }
    internal List<HeadlessCampaignHero> Heroes { get; }
    internal List<HeadlessCampaignItem> Inventory { get; }
    internal CampaignProgressState Progress { get; private set; }
    internal ActiveRunState? ActiveRun { get; private set; }
    internal int CurrentNodeIndex { get; private set; }
    internal int CampaignSeedSalt { get; }
    internal int Heat { get; }
    internal int Gold { get; private set; } = 12;
    internal int Echo { get; private set; } = 45;
    internal IReadOnlyList<string> ExpeditionSquadHeroIds => _expeditionSquadHeroIds;
    internal IReadOnlyDictionary<DeploymentAnchorId, string> Assignments => _assignments;
    internal IReadOnlyList<string> TemporaryAugmentIds => ActiveRun?.Overlay.TemporaryAugmentIds ?? Array.Empty<string>();
    internal string SelectedChapterId => Progress.SelectedChapterId;
    internal string SelectedSiteId => Progress.SelectedSiteId;
    internal bool StoryCleared => Progress.StoryCleared;
    internal int CampaignSeed { get; }
    internal IReadOnlyList<ItemRarityTierValue> LatestItemGrades => _latestItemGrades;
    internal string PanelCellId => CampaignSeedSalt == 0
        ? Cell.CellId
        : $"{Cell.CellId}|seed-salt={CampaignSeedSalt.ToString(CultureInfo.InvariantCulture)}";

    internal SiteTrackNodeState? SelectedNode
    {
        get
        {
            var track = ActiveRun == null
                ? _encounterResolver.BuildSiteTrack(SelectedChapterId, SelectedSiteId)
                : _siteTrack;
            return CurrentNodeIndex >= 0 && CurrentNodeIndex < track.Count
                ? track[CurrentNodeIndex]
                : null;
        }
    }

    internal static HeadlessCampaignState Create(
        SnapshotSessionContentLookup lookup,
        CampaignBalanceGridCell cell,
        int campaignSeedSalt = 0,
        int heat = 0)
    {
        if (heat < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heat), heat, "Heat must be non-negative.");
        }

        var cellTag = CellTag(cell);
        var heroes = cell.RosterArchetypeIds
            .Select((archetypeId, index) => BuildHero(lookup, cellTag, archetypeId, index))
            .ToArray();
        var inventory = BuildStarterItems(lookup);
        return new HeadlessCampaignState(lookup, cell, campaignSeedSalt, heat, heroes, inventory);
    }

    internal void AdvanceToNextUnclearedSite()
    {
        if (!Progress.ClearedSiteIds.Contains(SelectedSiteId, StringComparer.Ordinal))
        {
            return;
        }

        var siteIds = OrderedSiteIds(SelectedChapterId);
        if (siteIds.Count > 1)
        {
            var currentIndex = Math.Max(0, siteIds.FindIndex(id => string.Equals(id, SelectedSiteId, StringComparison.Ordinal)));
            Progress = Progress with { SelectedSiteId = siteIds[(currentIndex + 1) % siteIds.Count] };
        }

        if (!Progress.ClearedSiteIds.Contains(SelectedSiteId, StringComparer.Ordinal))
        {
            CurrentNodeIndex = 0;
            return;
        }

        var chapterIds = OrderedChapterIds();
        var chapterIndex = Math.Max(0, chapterIds.FindIndex(id => string.Equals(id, SelectedChapterId, StringComparison.Ordinal)));
        var nextChapterId = chapterIds[(chapterIndex + 1) % chapterIds.Count];
        var nextSiteIds = OrderedSiteIds(nextChapterId);
        Progress = Progress with
        {
            SelectedChapterId = nextChapterId,
            SelectedSiteId = nextSiteIds.FirstOrDefault() ?? string.Empty,
        };
        CurrentNodeIndex = 0;
    }

    internal void BeginSite()
    {
        _siteTrack = _encounterResolver.BuildSiteTrack(SelectedChapterId, SelectedSiteId);
        if (_siteTrack.Count == 0)
        {
            throw new InvalidOperationException($"Campaign site track is empty: {SelectedChapterId}/{SelectedSiteId}");
        }

        CurrentNodeIndex = ResolveEntryNodeIndex();
        AdvanceThroughDefaultNonBattleNodes();
        ActiveRun = RunStateService.StartRun(SelectedSiteId, BuildBlueprint(), false);
        ActiveRun = RunStateService.AdvanceNode(ActiveRun, CurrentNodeIndex);
    }

    internal void ApplyBuildPower(CampaignBuildPowerQuantileSpec quantile)
    {
        foreach (var hero in Heroes)
        {
            hero.EquippedItemIds.Clear();
        }

        foreach (var item in Inventory)
        {
            item.EquippedHeroId = string.Empty;
        }

        if (quantile.EquipmentSlotsPerHero > 0)
        {
            foreach (var item in Inventory
                         .OrderBy(value => value.ItemBaseId, StringComparer.Ordinal)
                         .ThenBy(value => value.AcquisitionIndex))
            {
                if (Snapshot.ItemCatalog is not { } catalog
                    || !catalog.TryGetValue(item.ItemBaseId, out var template))
                {
                    continue;
                }

                foreach (var hero in Heroes)
                {
                    if (hero.EquippedItemIds.Count >= quantile.EquipmentSlotsPerHero
                        || !CanWear(hero, template, catalog))
                    {
                        continue;
                    }

                    hero.EquippedItemIds.Add(item.InstanceId);
                    item.EquippedHeroId = hero.Id;
                    break;
                }
            }
        }

        if (quantile.GrowAvailablePassives)
        {
            foreach (var hero in Heroes)
            {
                GrowPassives(hero);
            }
        }
    }

    internal void ApplyDeploymentDecision(
        HeadlessPolicyObservation observation,
        HeadlessDeploymentDecision decision)
    {
        HeadlessPolicyGuard.ValidateDeploymentDecision(observation, decision);
        SynchronizeExpeditionSquad(decision.Placements.Select(value => value.HeroId));
        _assignments.Clear();
        foreach (var placement in decision.Placements)
        {
            Assign(placement.Anchor, placement.HeroId);
        }
    }

    internal void ApplyPrepDecision(
        HeadlessPolicyObservation observation,
        HeadlessPrepDecision decision)
    {
        HeadlessPrepPolicyGuard.ValidateDecision(observation, decision);
        SynchronizeExpeditionSquad(decision.Placements.Select(value => value.HeroId));
        _assignments.Clear();
        foreach (var placement in decision.Placements)
        {
            Assign(placement.Anchor, placement.HeroId);
        }

        foreach (var equipment in decision.EquipmentAssignments)
        {
            Reequip(equipment.ItemInstanceId, equipment.HeroId);
        }
    }

    internal HeadlessCampaignBattleSetup BuildBattleSetup()
    {
        if (ActiveRun == null)
        {
            throw new InvalidOperationException("Campaign site has not started.");
        }

        var blueprint = BuildBlueprint();
        var overlay = ActiveRun.Overlay with
        {
            CurrentNodeIndex = CurrentNodeIndex,
            SiteNodeIndex = CurrentNodeIndex,
            TemporaryAugmentIds = TemporaryAugmentIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            PendingRewardIds = Array.Empty<string>(),
        };
        var compiled = _loadoutCompiler.Compile(
            Heroes.Select(hero => hero.Record).ToArray(),
            BuildHeroLoadouts(),
            Heroes.ToDictionary(
                hero => hero.Id,
                hero => new HeroProgressionState(
                    hero.Id,
                    hero.Level,
                    hero.Experience,
                    Array.Empty<string>(),
                    Array.Empty<string>()),
                StringComparer.Ordinal),
            Inventory.ToDictionary(
                item => item.InstanceId,
                item => new ItemInstanceState(
                    item.InstanceId,
                    item.ItemBaseId,
                    item.AffixIds,
                    item.EquippedHeroId,
                    item.RarityTier,
                    item.AffixMagnitudes),
                StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            Heroes.Where(hero => hero.SelectedPassiveNodeIds.Count > 0)
                .ToDictionary(
                    hero => hero.Id,
                    hero => new PassiveBoardSelectionState(
                        hero.Id,
                        hero.PassiveBoardId,
                        hero.SelectedPassiveNodeIds.ToArray()),
                    StringComparer.Ordinal),
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            overlay,
            Snapshot,
            warWoundSpec: Snapshot.WarWound,
            activeWoundHeroIds: ActiveRun.ActiveWoundHeroIds);

        ActiveRun = ActiveRun with { Blueprint = blueprint, Overlay = overlay };
        var context = CampaignEncounterSeed.Apply(_encounterResolver.BuildBattleContext(
            ActiveRun,
            SelectedChapterId,
            SelectedSiteId,
            CurrentNodeIndex), CampaignSeed);
        if (!_encounterResolver.TryResolveEncounter(context, out var encounter, out var error))
        {
            throw new InvalidOperationException(
                $"Campaign encounter resolution failed ({Cell.CellId}/{context.EncounterId}): {error}");
        }

        encounter = ApplyCampaignEnvelope(encounter);
        ActiveRun = RunStateService.SetBattleContext(ActiveRun, context);
        ActiveRun = RunStateService.SyncBlueprint(ActiveRun, blueprint, compiled.CompileHash, Array.Empty<string>());
        return new HeadlessCampaignBattleSetup(compiled, encounter);
    }

    internal int ApplyBattleProgression(BattleResult result)
    {
        var woundsApplied = 0;
        if (ActiveRun != null && Snapshot.WarWound != null)
        {
            var woundResolution = WarWoundResolutionService.Resolve(
                ActiveRun,
                result.Winner == TeamSide.Ally,
                result.FinalUnits
                    .Where(unit => unit.Side == TeamSide.Ally && unit.EntityKind == CombatEntityKind.RosterUnit)
                    .Select(unit => new WarWoundCandidate(
                        ResolveRosterHeroId(unit.Id),
                        unit.CurrentHealth,
                        unit.MaxHealth))
                    .ToArray(),
                Snapshot.WarWound);
            ActiveRun = woundResolution.UpdatedRun;
            woundsApplied = woundResolution.AppliedHeroIds.Count;
        }

        var heroesById = Heroes.ToDictionary(hero => hero.Id, StringComparer.Ordinal);
        foreach (var unit in result.FinalUnits)
        {
            if (unit.Side != TeamSide.Ally || unit.EntityKind != CombatEntityKind.RosterUnit)
            {
                continue;
            }

            var heroId = ResolveRosterHeroId(unit.Id);
            if (!heroesById.TryGetValue(heroId, out var hero))
            {
                continue;
            }

            hero.MaxHp = (int)Math.Max(1, Math.Round(unit.MaxHealth));
            hero.CurrentHp = (int)Math.Max(0, Math.Round(unit.CurrentHealth));
            if (result.Winner != TeamSide.Ally)
            {
                continue;
            }

            hero.Experience += 50;
            while (hero.Experience >= HeroProgressionCurve.ExperienceToNextLevel(hero.Level))
            {
                hero.Experience -= HeroProgressionCurve.ExperienceToNextLevel(hero.Level);
                hero.Level++;
            }
        }

        if (result.Winner == TeamSide.Ally)
        {
            ApplyAutomaticLoot();
        }

        return woundsApplied;
    }

    internal void ResetNodeDropObservation()
    {
        _latestItemGrades.Clear();
    }

    internal double ExpectedEquippedGrade()
    {
        if (Snapshot.ItemCatalog is not { Count: > 0 } itemCatalog
            || Heroes.Count == 0)
        {
            return 0d;
        }

        var slotTypes = itemCatalog.Values
            .Select(item => item.SlotType)
            .Where(slot => !string.IsNullOrWhiteSpace(slot))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(slot => slot, StringComparer.Ordinal)
            .ToArray();
        if (slotTypes.Length == 0)
        {
            return 0d;
        }

        var occupiedSlots = Heroes.ToDictionary(
            hero => hero.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var gradeSum = 0d;
        foreach (var item in Inventory
                     .OrderByDescending(item => item.RarityTier)
                     .ThenBy(item => item.ItemBaseId, StringComparer.Ordinal)
                     .ThenBy(item => item.AcquisitionIndex))
        {
            if (!itemCatalog.TryGetValue(item.ItemBaseId, out var template))
            {
                continue;
            }

            var hero = Heroes.FirstOrDefault(candidate =>
                !occupiedSlots[candidate.Id].Contains(template.SlotType)
                && (template.AllowedClassIds is not { Count: > 0 }
                    || template.AllowedClassIds.Contains(
                        candidate.ClassId,
                        StringComparer.Ordinal)));
            if (hero == null)
            {
                continue;
            }

            occupiedSlots[hero.Id].Add(template.SlotType);
            gradeSum += (int)item.RarityTier;
        }

        return gradeSum / (Heroes.Count * (double)slotTypes.Length);
    }

    internal void AdvanceBattleNode()
    {
        if (CurrentNodeIndex >= 0 && CurrentNodeIndex < _siteTrack.Count)
        {
            CurrentNodeIndex = ResolveDefaultNextNodeIndex(_siteTrack[CurrentNodeIndex]);
            AdvanceThroughDefaultNonBattleNodes();
        }

        if (ActiveRun != null)
        {
            ActiveRun = RunStateService.AdvanceNode(ActiveRun, CurrentNodeIndex);
        }
    }

    private int ResolveEntryNodeIndex()
    {
        var targetedIndices = _siteTrack
            .SelectMany(node => node.NextNodeIndices ?? Array.Empty<int>())
            .Where(index => index >= 0 && index < _siteTrack.Count)
            .ToHashSet();
        var entries = _siteTrack
            .Select(node => node.Index)
            .Where(index => !targetedIndices.Contains(index))
            .ToList();
        return entries.Count == 1 ? entries[0] : 0;
    }

    private void AdvanceThroughDefaultNonBattleNodes()
    {
        var visited = new HashSet<int>();
        while (CurrentNodeIndex >= 0
               && CurrentNodeIndex < _siteTrack.Count
               && !_siteTrack[CurrentNodeIndex].RequiresBattle
               && _siteTrack[CurrentNodeIndex].NextNodeIndices.Count > 0
               && visited.Add(CurrentNodeIndex))
        {
            CurrentNodeIndex = ResolveDefaultNextNodeIndex(_siteTrack[CurrentNodeIndex]);
        }
    }

    private int ResolveDefaultNextNodeIndex(SiteTrackNodeState node)
    {
        foreach (var index in node.NextNodeIndices ?? Array.Empty<int>())
        {
            if (index >= 0 && index < _siteTrack.Count)
            {
                return index;
            }
        }

        return node.Index;
    }

    internal void CompleteSite()
    {
        var clearedSites = Progress.ClearedSiteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        clearedSites.Add(SelectedSiteId);
        var clearedChapters = Progress.ClearedChapterIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var chapter in Snapshot.CampaignChapters?.Values ?? Array.Empty<CampaignChapterTemplate>())
        {
            if (chapter.SiteIds.Count > 0 && chapter.SiteIds.All(clearedSites.Contains))
            {
                clearedChapters.Add(chapter.Id);
            }
        }

        var storyCleared = (Snapshot.CampaignChapters?.Values ?? Array.Empty<CampaignChapterTemplate>())
            .All(chapter => chapter.SiteIds.Count > 0 && chapter.SiteIds.All(clearedSites.Contains));
        Progress = Progress with
        {
            ClearedSiteIds = clearedSites.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            ClearedChapterIds = clearedChapters.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            StoryCleared = storyCleared,
            EndlessUnlocked = Progress.EndlessUnlocked || storyCleared,
        };
        ActiveRun = null;
        _siteTrack = Array.Empty<SiteTrackNodeState>();
        CurrentNodeIndex = 0;
    }

    internal string FormationHash()
    {
        var assignments = DeploymentAnchors
            .Select(anchor => $"{anchor}:{(_assignments.TryGetValue(anchor, out var heroId) ? heroId : "-")}");
        var expedition = _expeditionSquadHeroIds.OrderBy(id => id, StringComparer.Ordinal);
        return $"{string.Join("|", assignments)}||{string.Join(",", expedition)}";
    }

    private void ApplyClassBasedDeployment()
    {
        _assignments.Clear();
        var front = new Queue<DeploymentAnchorId>(new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontBottom,
        });
        var back = new Queue<DeploymentAnchorId>(new[]
        {
            DeploymentAnchorId.BackCenter,
            DeploymentAnchorId.BackTop,
            DeploymentAnchorId.BackBottom,
        });
        foreach (var hero in Heroes.Take(MetaBalanceDefaults.BattleDeployCap))
        {
            var prefersFront = hero.ClassId is "vanguard" or "duelist";
            var primary = prefersFront ? front : back;
            var fallback = prefersFront ? back : front;
            Assign(primary.Count > 0 ? primary.Dequeue() : fallback.Dequeue(), hero.Id);
        }
    }

    private void SynchronizeExpeditionSquad(IEnumerable<string> desiredHeroIds)
    {
        var desired = desiredHeroIds.ToHashSet(StringComparer.Ordinal);
        foreach (var heroId in desired.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (_expeditionSquadHeroIds.Contains(heroId, StringComparer.Ordinal))
            {
                continue;
            }

            var replace = _expeditionSquadHeroIds
                .Where(current => !desired.Contains(current))
                .OrderBy(current => current, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(replace))
            {
                _expeditionSquadHeroIds.RemoveAll(id => string.Equals(id, replace, StringComparison.Ordinal));
                foreach (var anchor in _assignments.Where(pair => pair.Value == replace).Select(pair => pair.Key).ToArray())
                {
                    _assignments.Remove(anchor);
                }
            }

            _expeditionSquadHeroIds.Add(heroId);
        }
    }

    private void Assign(DeploymentAnchorId anchor, string heroId)
    {
        if (!_expeditionSquadHeroIds.Contains(heroId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Hero '{heroId}' is not in the expedition squad.");
        }

        foreach (var existing in _assignments.Where(pair => pair.Value == heroId).Select(pair => pair.Key).ToArray())
        {
            _assignments.Remove(existing);
        }

        _assignments[anchor] = heroId;
    }

    private void Reequip(string itemInstanceId, string heroId)
    {
        var item = Inventory.SingleOrDefault(value => string.Equals(value.InstanceId, itemInstanceId, StringComparison.Ordinal))
                   ?? throw new InvalidOperationException($"Owned item '{itemInstanceId}' was not found.");
        var target = Heroes.Single(value => string.Equals(value.Id, heroId, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(item.EquippedHeroId))
        {
            var previous = Heroes.FirstOrDefault(value => string.Equals(value.Id, item.EquippedHeroId, StringComparison.Ordinal));
            previous?.EquippedItemIds.RemoveAll(id => string.Equals(id, itemInstanceId, StringComparison.Ordinal));
        }

        item.EquippedHeroId = target.Id;
        if (!target.EquippedItemIds.Contains(itemInstanceId, StringComparer.Ordinal))
        {
            target.EquippedItemIds.Add(itemInstanceId);
        }
    }

    private bool CanWear(
        HeadlessCampaignHero hero,
        ItemTemplate template,
        IReadOnlyDictionary<string, ItemTemplate> itemCatalog)
    {
        if (template.AllowedClassIds is { Count: > 0 }
            && !template.AllowedClassIds.Contains(hero.ClassId, StringComparer.Ordinal))
        {
            return false;
        }

        return !hero.EquippedItemIds.Any(instanceId =>
            Inventory.FirstOrDefault(item => item.InstanceId == instanceId) is { } equipped
            && itemCatalog.TryGetValue(equipped.ItemBaseId, out var equippedTemplate)
            && string.Equals(equippedTemplate.SlotType, template.SlotType, StringComparison.Ordinal));
    }

    private void GrowPassives(HeadlessCampaignHero hero)
    {
        hero.PassiveBoardId = $"board_{hero.ClassId}";
        var nodes = Snapshot.PassiveNodes.Values
            .Where(node => string.Equals(node.BoardId, hero.PassiveBoardId, StringComparison.Ordinal))
            .OrderBy(node => node.BoardDepth)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var selected = hero.SelectedPassiveNodeIds.ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in nodes)
            {
                if (selected.Contains(node.Id, StringComparer.Ordinal))
                {
                    continue;
                }

                var result = PassiveBoardSelectionValidator.Toggle(
                    hero.PassiveBoardId,
                    selected,
                    node.Id,
                    nodesById,
                    PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(hero.Level));
                if (!result.IsValid)
                {
                    continue;
                }

                selected = result.NormalizedNodeIds.ToList();
                changed = true;
            }
        }

        hero.SelectedPassiveNodeIds.Clear();
        hero.SelectedPassiveNodeIds.AddRange(selected);
    }

    private IReadOnlyDictionary<string, HeroLoadoutState> BuildHeroLoadouts()
    {
        var result = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        foreach (var hero in Heroes)
        {
            if (hero.EquippedItemIds.Count == 0 && hero.SelectedPassiveNodeIds.Count == 0)
            {
                continue;
            }

            result[hero.Id] = new HeroLoadoutState(
                hero.Id,
                hero.EquippedItemIds.ToArray(),
                Array.Empty<string>(),
                hero.PassiveBoardId,
                hero.SelectedPassiveNodeIds.ToArray(),
                Array.Empty<string>());
        }

        return result;
    }

    private SquadBlueprintState BuildBlueprint()
    {
        var roleIds = Heroes.ToDictionary(
            hero => hero.Id,
            hero => ResolveRoleId(
                hero.ClassId,
                _assignments.FirstOrDefault(pair => pair.Value == hero.Id).Key),
            StringComparer.Ordinal);
        return new SquadBlueprintState(
            "blueprint.default",
            "Default Build",
            TeamPostureType.StandardAdvance,
            string.Empty,
            _assignments.ToDictionary(pair => pair.Key, pair => pair.Value),
            _expeditionSquadHeroIds.ToArray(),
            roleIds,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private ResolvedEncounterContext ApplyCampaignEnvelope(ResolvedEncounterContext encounter)
    {
        var resolved = encounter;
        if (Snapshot.CampaignChapters is { } chapters
            && chapters.TryGetValue(encounter.Context.ChapterId, out var chapter))
        {
            var siteOrder = Snapshot.ExpeditionSites is { } sites
                            && sites.TryGetValue(encounter.Context.SiteId, out var site)
                ? site.SiteOrder
                : 1;
            var campaignPackages = CampaignEnvelopeService.BuildEnemyChapterPackages(
                chapter.StoryOrder,
                chapter.Balance,
                siteOrder);
            if (campaignPackages.Count > 0)
            {
                resolved = resolved with
                {
                    Enemies = PoliticalCombatConditionService.ApplyEnemyPackages(
                        resolved.Enemies,
                        campaignPackages),
                };
            }
        }

        if (Heat <= 0)
        {
            return resolved;
        }

        var enemies = PoliticalCombatConditionService.ApplyEnemyPackages(
            resolved.Enemies,
            EndlessCycleService.BuildEnemyHeatPackages(Heat));
        enemies = PoliticalCombatConditionService.ApplyEnemyRulePackages(
            enemies,
            EndlessCycleService.BuildEnemyHeatSecondaryPressurePackages(Heat));
        return resolved with { Enemies = enemies };
    }

    private void ApplyAutomaticLoot()
    {
        if (ActiveRun == null || string.IsNullOrWhiteSpace(ActiveRun.Overlay.RewardSourceId))
        {
            return;
        }

        var service = new LootResolutionService(Snapshot);
        if (!service.TryResolveBundle(
                ActiveRun.Overlay.RewardSourceId,
                ActiveRun.Overlay.BattleSeed,
                ResolveRewardContextTags(),
                out var bundle,
                out _,
                Heat))
        {
            return;
        }

        foreach (var entry in bundle.Entries)
        {
            switch (entry.RewardType)
            {
                case RewardType.Gold:
                    Gold += entry.Amount;
                    break;
                case RewardType.Echo:
                    Echo += entry.Amount;
                    break;
                case RewardType.TemporaryAugment:
                    for (var index = 0; index < Math.Max(1, entry.Amount); index++)
                    {
                        if (ActiveRun != null)
                        {
                            ActiveRun = RunStateService.ApplyTemporaryAugment(ActiveRun, entry.Id);
                        }
                    }

                    break;
                case RewardType.Item:
                    for (var index = 0; index < Math.Max(1, entry.Amount); index++)
                    {
                        AddGeneratedItem(entry.Id, entry.ItemGrade);
                    }

                    break;
            }
        }
    }

    private IReadOnlyList<string> ResolveRewardContextTags()
    {
        if (ActiveRun == null)
        {
            return Array.Empty<string>();
        }

        var tags = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(ActiveRun.Overlay.SiteId))
        {
            tags.Add(ActiveRun.Overlay.SiteId);
        }

        if (!string.IsNullOrWhiteSpace(SelectedChapterId))
        {
            tags.Add(SelectedChapterId);
        }

        if (Snapshot.Encounters is { } encounters
            && encounters.TryGetValue(ActiveRun.Overlay.EncounterId, out var encounter))
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

    internal bool TrySpendEcho(int amount)
    {
        if (amount <= 0 || Echo < amount)
        {
            return false;
        }

        Echo -= amount;
        return true;
    }

    private HeadlessCampaignItem AddGeneratedItem(string itemBaseId, ItemRarityTierValue? rolledGrade)
    {
        _itemInstanceCounter = checked(_itemInstanceCounter + 1);
        var acquisitionIndex = Inventory.Count;
        var seed = BuildStableSeed($"{itemBaseId}|{acquisitionIndex}", acquisitionIndex);
        var grade = rolledGrade
                    ?? (Snapshot.ItemCatalog != null
                        && Snapshot.ItemCatalog.TryGetValue(itemBaseId, out var template)
                    ? template.RarityTier
                    : ItemRarityTierValue.Common);
        _latestItemGrades.Add(grade);
        var affixIds = GeneratedItemAffixSelector.Select(
            Lookup,
            itemBaseId,
            seed,
            grade,
            ResolveGradeStepBudgetScore()).ToArray();
        var item = new HeadlessCampaignItem(
            $"{itemBaseId}-i{_itemInstanceCounter.ToString(CultureInfo.InvariantCulture)}",
            itemBaseId,
            affixIds,
            BuildAffixMagnitudes(Snapshot, seed, affixIds),
            string.Empty,
            acquisitionIndex,
            grade);
        Inventory.Add(item);
        return item;
    }

    private float ResolveGradeStepBudgetScore()
    {
        var values = (Snapshot.DropTables?.Values ?? Array.Empty<DropTableTemplate>())
            .Where(table => table.GradeProfiles is { Count: > 0 })
            .Select(table => table.GradeStepBudgetScore)
            .Distinct()
            .ToArray();
        if (values.Length != 1 || values[0] <= 0f)
        {
            throw new InvalidDataException(
                $"Headless grade generation requires one positive step budget, got [{string.Join(",", values)}].");
        }

        return values[0];
    }

    private List<string> OrderedChapterIds()
        => (Snapshot.CampaignChapters?.Values ?? Array.Empty<CampaignChapterTemplate>())
            .OrderBy(chapter => chapter.StoryOrder)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            .Select(chapter => chapter.Id)
            .ToList();

    private List<string> OrderedSiteIds(string chapterId)
    {
        if (Snapshot.CampaignChapters is not { } chapters
            || Snapshot.ExpeditionSites is not { } sites
            || !chapters.TryGetValue(chapterId, out var chapter))
        {
            return new List<string>();
        }

        return chapter.SiteIds
            .Where(sites.ContainsKey)
            .OrderBy(id => sites[id].SiteOrder)
            .ThenBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    private static HeadlessCampaignHero BuildHero(
        SnapshotSessionContentLookup lookup,
        string cellTag,
        string archetypeId,
        int index)
    {
        if (!lookup.Snapshot.Archetypes.TryGetValue(archetypeId, out var archetype))
        {
            throw new InvalidDataException($"Campaign sweep archetype missing: {archetypeId}");
        }

        var heroId = $"sweep-{cellTag}-{index + 1}-{archetypeId}";
        return new HeadlessCampaignHero(new HeroRecord(
            heroId,
            archetype.DisplayName,
            archetypeId,
            archetype.RaceId,
            archetype.ClassId,
            lookup.NormalizePositiveTraitId(archetypeId, string.Empty, index),
            lookup.NormalizeNegativeTraitId(archetypeId, string.Empty, index + 1),
            archetype.FlexActive?.Id ?? string.Empty,
            archetype.FlexPassive?.Id ?? string.Empty,
            archetype.RecruitTier,
            RecruitOfferSource.DirectGrant,
            new UnitRetrainState(),
            new UnitEconomyFootprint(),
            string.Empty,
            DominantHandDistributionService.ResolveGenerated(heroId, archetype.ClassId)));
    }

    private static IReadOnlyList<HeadlessCampaignItem> BuildStarterItems(SnapshotSessionContentLookup lookup)
    {
        var items = new List<HeadlessCampaignItem>();
        foreach (var itemBaseId in lookup.GetCanonicalItemIds().Take(4))
        {
            var index = items.Count;
            var seed = BuildStableSeed($"{itemBaseId}|{index}", index);
            var affixIds = GeneratedItemAffixSelector.Select(lookup, itemBaseId, seed).ToArray();
            items.Add(new HeadlessCampaignItem(
                $"demo-item-{index + 1}",
                itemBaseId,
                affixIds,
                BuildAffixMagnitudes(lookup.Snapshot, seed, affixIds),
                string.Empty,
                index,
                lookup.Snapshot.ItemCatalog != null
                && lookup.Snapshot.ItemCatalog.TryGetValue(itemBaseId, out var template)
                    ? template.RarityTier
                    : ItemRarityTierValue.Common));
        }

        return items;
    }

    private static IReadOnlyDictionary<string, float> BuildAffixMagnitudes(
        CombatContentSnapshot snapshot,
        int stableItemSeed,
        IReadOnlyList<string> affixIds)
    {
        var magnitudes = new Dictionary<string, float>(StringComparer.Ordinal);
        var affixCatalog = snapshot.AffixCatalog;
        if (affixCatalog == null)
        {
            return magnitudes;
        }

        for (var index = 0; index < affixIds.Count; index++)
        {
            var affixId = affixIds[index];
            if (!affixCatalog.TryGetValue(affixId, out var template))
            {
                continue;
            }

            magnitudes[affixId] = AffixMagnitudeRoller.Roll(
                stableItemSeed,
                affixId,
                index,
                template.ValueMin,
                template.ValueMax);
        }

        return magnitudes;
    }

    private static int BuildStableSeed(string value, int salt)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value ?? string.Empty)
            {
                hash = (hash * 31) + character;
            }

            hash = (hash * 31) + salt;
            return hash & int.MaxValue;
        }
    }

    private static string ResolveRoleId(string classId, DeploymentAnchorId anchor)
        => classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };

    private static string ResolveRosterHeroId(string unitId)
    {
        if (string.IsNullOrEmpty(unitId) || !unitId.StartsWith("ally_", StringComparison.Ordinal))
        {
            return unitId;
        }

        var parts = unitId.Split('_', 3);
        return parts.Length == 3 ? parts[2] : unitId;
    }

    private static string CellTag(CampaignBalanceGridCell cell)
        => $"{cell.Squad.SquadId}-{cell.BuildPower.QuantileId.ToLowerInvariant()}-"
           + $"e{cell.EnemyComposition.VariantIndex}-c{cell.RosterCoverage.BenchArchetypeCount}";
}
