using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class CampaignCellBattleRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string CellSelectionRelativePath = "tools/HeadlessSweep/campaign-battle-cells.json";
    private const string DefaultOutputRelativePath = "Temp/HeadlessSweep/headless-campaign-battles.json";

    private static readonly DeploymentAnchorId[] DeploymentAnchors =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom,
    };

    private static readonly IReadOnlyDictionary<string, SquadSpec> Squads =
        new Dictionary<string, SquadSpec>(StringComparer.Ordinal)
        {
            ["frontline"] = new(
                new[] { "warden", "guardian", "slayer", "raider" },
                new[] { "marksman", "hunter", "priest", "shaman" }),
            ["ranged"] = new(
                new[] { "warden", "marksman", "hunter", "scout" },
                new[] { "guardian", "slayer", "priest", "shaman" }),
            ["mixed"] = new(
                new[] { "warden", "guardian", "marksman", "hunter" },
                new[] { "raider", "scout", "priest", "shaman" }),
        };

    private static readonly IReadOnlyDictionary<string, BuildPowerSpec> BuildPowers =
        new Dictionary<string, BuildPowerSpec>(StringComparer.Ordinal)
        {
            ["P20"] = new(0, false),
            ["P35"] = new(1, false),
            ["P50"] = new(2, false),
            ["P65"] = new(3, false),
            ["P80"] = new(3, true),
        };

    private static readonly IReadOnlyDictionary<string, int> EnemyVariants =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["authored"] = 0,
            ["mirror-lanes"] = 1,
            ["swap-rows"] = 2,
            ["mirror-lanes-swap-rows"] = 3,
            ["rotate-anchor-forward"] = 4,
            ["rotate-anchor-back"] = 5,
            ["rotate-member-anchors"] = 6,
            ["reverse-member-order"] = 7,
        };

    private static readonly IReadOnlyDictionary<string, int> CoverageCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["C0-core-only"] = 0,
            ["C1-one-counter"] = 1,
            ["C2-two-counters"] = 2,
            ["C3-full-counter-bench"] = 4,
        };

    internal static int Run(string repositoryRoot, string? unityReportPath, string? outputPath)
    {
        try
        {
            var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
            var selectionPath = Resolve(repositoryRoot, CellSelectionRelativePath);
            var snapshot = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var selection = JsonConvert.DeserializeObject<CellSelection>(File.ReadAllText(selectionPath))
                            ?? throw new InvalidDataException("Failed to deserialize campaign battle cell selection.");
            var outcomes = selection.CellIds.Select(cellId => RunCell(lookup, ParseCell(cellId))).ToArray();
            var report = new CampaignBattleReport("campaign-headless-battle-parity-v1", outcomes);

            var resolvedOutputPath = string.IsNullOrWhiteSpace(outputPath)
                ? Resolve(repositoryRoot, DefaultOutputRelativePath)
                : Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutputPath)!);
            File.WriteAllText(resolvedOutputPath, JsonConvert.SerializeObject(report, Formatting.Indented));

            if (string.IsNullOrWhiteSpace(unityReportPath))
            {
                Console.WriteLine($"headless-battle RUN ({outcomes.Length} cells): {resolvedOutputPath}");
                return 0;
            }

            var unity = JsonConvert.DeserializeObject<UnityCampaignBattleReport>(
                            File.ReadAllText(Path.GetFullPath(unityReportPath)))
                        ?? throw new InvalidDataException("Failed to deserialize Unity campaign battle report.");
            var divergence = FindFirstDivergence(unity.Outcomes, outcomes);
            if (divergence == null)
            {
                Console.WriteLine($"headless-battle MATCH ({outcomes.Length} cells): .NET 8 == Unity");
                return 0;
            }

            Console.Error.WriteLine("== headless-battle DIVERGENCE ==");
            Console.Error.WriteLine($"  cell     : {divergence.Cell}");
            Console.Error.WriteLine($"  field    : {divergence.Field}");
            Console.Error.WriteLine($"  unity    : {divergence.Unity}");
            Console.Error.WriteLine($"  headless : {divergence.Headless}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"headless-battle ERROR: {exception.Message}");
            return 2;
        }
    }

    private static CampaignBattleOutcome RunCell(SnapshotSessionContentLookup lookup, CellSpec cell)
    {
        var rosterArchetypes = cell.Squad.CoreArchetypeIds
            .Concat(cell.Squad.BenchArchetypeIds.Take(cell.BenchArchetypeCount))
            .ToArray();
        var cellTag = $"{cell.SquadId}-{cell.QuantileId.ToLowerInvariant()}-e{cell.EnemyVariantIndex}-c{cell.BenchArchetypeCount}";
        var heroes = rosterArchetypes.Select((archetypeId, index) => BuildHero(lookup, cellTag, archetypeId, index)).ToArray();
        var items = BuildStarterItems(lookup);
        var equippedByHero = heroes.ToDictionary(hero => hero.Id, _ => new List<string>(), StringComparer.Ordinal);
        EquipBuildPower(lookup.Snapshot, heroes, items, equippedByHero, cell.BuildPower.EquipmentSlotsPerHero);
        var passivesByHero = cell.BuildPower.GrowAvailablePassives
            ? heroes.ToDictionary(hero => hero.Id, hero => GrowPassives(lookup.Snapshot, hero), StringComparer.Ordinal)
            : new Dictionary<string, PassiveSelection>(StringComparer.Ordinal);

        var assignments = BuildGreedyDeployment(heroes);
        var roleIds = heroes.ToDictionary(
            hero => hero.Id,
            hero => ResolveRoleId(hero.ClassId, assignments.FirstOrDefault(pair => pair.Value == hero.Id).Key),
            StringComparer.Ordinal);
        var blueprint = new SquadBlueprintState(
            "blueprint.default",
            "Default Build",
            TeamPostureType.StandardAdvance,
            string.Empty,
            assignments,
            heroes.Select(hero => hero.Id).ToArray(),
            roleIds,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var run = RunStateService.StartRun($"campaign-two-arm-{cellTag}", blueprint, false);

        var loadouts = BuildHeroLoadouts(heroes, equippedByHero, passivesByHero);
        var itemStates = items.ToDictionary(
            item => item.InstanceId,
            item => new ItemInstanceState(item.InstanceId, item.ItemBaseId, item.AffixIds, item.EquippedHeroId),
            StringComparer.Ordinal);
        var passiveStates = passivesByHero.ToDictionary(
            pair => pair.Key,
            pair => new PassiveBoardSelectionState(pair.Key, pair.Value.BoardId, pair.Value.NodeIds),
            StringComparer.Ordinal);
        var compiled = new LoadoutCompiler().Compile(
            heroes,
            loadouts,
            new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal),
            itemStates,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            passiveStates,
            new PermanentAugmentLoadoutState(blueprint.BlueprintId, Array.Empty<string>()),
            blueprint,
            run.Overlay,
            lookup.Snapshot);

        var encounterResolver = new EncounterResolutionService(lookup.Snapshot);
        var progress = encounterResolver.NormalizeCampaignProgress(new CampaignProgressState(
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            false));
        var context = encounterResolver.BuildBattleContext(
            run,
            progress.SelectedChapterId,
            progress.SelectedSiteId,
            0);
        if (!encounterResolver.TryResolveEncounter(context, out var authoredEncounter, out var encounterError))
        {
            throw new InvalidOperationException($"Encounter resolution failed for {cell.CellId}: {encounterError}");
        }

        var measuredEncounter = authoredEncounter with
        {
            Context = authoredEncounter.Context with
            {
                BattleSeed = DeriveH100Seed(authoredEncounter.Context.BattleContextHash, 1000 + cell.EnemyVariantIndex),
            },
            Enemies = EnemyCompositionVariantProjector.Project(authoredEncounter.Enemies, cell.EnemyVariantIndex),
        };
        if (!SessionBattleStateComposer.TryCompose(lookup, compiled, measuredEncounter, out var state, out var composeError))
        {
            throw new InvalidOperationException($"Battle composition failed for {cell.CellId}: {composeError}");
        }

        var setup = BuildSetupCheckpoint(lookup.Snapshot, compiled, items, measuredEncounter, state);
        var stepHashes = new List<string>();
        var result = BattleResolver.Run(
            state,
            BattleSimulator.DefaultMaxSteps,
            _ =>
            {
                stepHashes.Add(BattleStateCanonicalHash.Compute(state));
            });
        return new CampaignBattleOutcome(
            cell.CellId,
            result.Winner.ToString(),
            BattleStateCanonicalHash.Compute(state),
            measuredEncounter.Context.EncounterId,
            measuredEncounter.Context.BattleSeed,
            result.StepCount,
            setup,
            stepHashes);
    }

    private static CampaignBattleSetupCheckpoint BuildSetupCheckpoint(
        CombatContentSnapshot content,
        BattleLoadoutSnapshot allySnapshot,
        IReadOnlyList<HeadlessItem> items,
        ResolvedEncounterContext encounter,
        BattleState state)
    {
        var inventory = items
            .Select(item => BuildItemCheckpoint(content, item))
            .ToArray();
        var allies = allySnapshot.Allies
            .Select(unit => new CampaignUnitCheckpoint(
                unit.Id,
                unit.ArchetypeId,
                unit.PreferredAnchor.ToString(),
                unit.RoleTag,
                BuildStats(unit),
                inventory
                    .Where(item => string.Equals(item.EquippedHeroId, unit.Id, StringComparison.Ordinal))
                    .OrderBy(item => item.AcquisitionIndex)
                    .ToArray()))
            .ToArray();

        return new CampaignBattleSetupCheckpoint(
            allies,
            SerializeCanonical(allySnapshot),
            inventory,
            BuildEncounterCheckpoint(encounter),
            SerializeCanonical(encounter),
            BattleStateCanonicalHash.Compute(state));
    }

    private static CampaignItemCheckpoint BuildItemCheckpoint(CombatContentSnapshot content, HeadlessItem item)
    {
        var slotType = content.ItemCatalog is { } catalog && catalog.TryGetValue(item.ItemBaseId, out var template)
            ? template.SlotType
            : string.Empty;
        return new CampaignItemCheckpoint(
            item.AcquisitionIndex,
            item.InstanceId,
            item.ItemBaseId,
            slotType,
            item.EquippedHeroId,
            item.AffixIds.ToArray());
    }

    private static CampaignEncounterCheckpoint BuildEncounterCheckpoint(ResolvedEncounterContext encounter)
        => new(
            encounter.Context.EncounterId,
            encounter.Context.BattleSeed,
            encounter.Context.BattleContextHash,
            encounter.Context.BossOverlayId,
            encounter.EnemyPosture.ToString(),
            encounter.Enemies.Select(unit => new CampaignEnemyCheckpoint(
                    unit.Id,
                    unit.ArchetypeId,
                    unit.PreferredAnchor.ToString(),
                    BuildStats(unit),
                    (unit.Packages ?? Array.Empty<CombatModifierPackage>())
                        .Select(package => package.SourceId)
                        .ToArray(),
                    (unit.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
                        .Select(package => package.SourceId)
                        .ToArray(),
                    (unit.CompileTags ?? Array.Empty<string>()).ToArray()))
                .ToArray());

    private static IReadOnlyList<CampaignStatCheckpoint> BuildStats(BattleUnitLoadout unit)
        => unit.BaseStats
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => new CampaignStatCheckpoint(
                pair.Key.ToString(),
                pair.Value.ToString("R", CultureInfo.InvariantCulture)))
            .ToArray();

    private static string SerializeCanonical(object value)
        => JsonConvert.SerializeObject(
            value,
            Formatting.None,
            new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            });

    private static HeroRecord BuildHero(
        SnapshotSessionContentLookup lookup,
        string cellTag,
        string archetypeId,
        int index)
    {
        if (!lookup.Snapshot.Archetypes.TryGetValue(archetypeId, out var archetype))
        {
            throw new InvalidDataException($"Campaign battle archetype missing: {archetypeId}");
        }

        var heroId = $"sweep-{cellTag}-{index + 1}-{archetypeId}";
        return new HeroRecord(
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
            DominantHandDistributionService.ResolveGenerated(heroId, archetype.ClassId));
    }

    private static List<HeadlessItem> BuildStarterItems(SnapshotSessionContentLookup lookup)
    {
        var items = new List<HeadlessItem>();
        foreach (var itemBaseId in lookup.GetCanonicalItemIds().Take(4))
        {
            var index = items.Count;
            var seed = BuildStableSeed($"{itemBaseId}|{index}", index);
            items.Add(new HeadlessItem(
                $"demo-item-{index + 1}",
                itemBaseId,
                GeneratedItemAffixSelector.Select(lookup, itemBaseId, seed).ToArray(),
                string.Empty,
                index));
        }

        return items;
    }

    private static void EquipBuildPower(
        CombatContentSnapshot snapshot,
        IReadOnlyList<HeroRecord> heroes,
        IList<HeadlessItem> items,
        IReadOnlyDictionary<string, List<string>> equippedByHero,
        int slotsPerHero)
    {
        if (slotsPerHero <= 0 || snapshot.ItemCatalog is not { } catalog)
        {
            return;
        }

        foreach (var item in items.OrderBy(value => value.ItemBaseId, StringComparer.Ordinal).ThenBy(value => value.AcquisitionIndex))
        {
            if (!catalog.TryGetValue(item.ItemBaseId, out var template))
            {
                continue;
            }

            foreach (var hero in heroes)
            {
                var equippedIds = equippedByHero[hero.Id];
                if (equippedIds.Count >= slotsPerHero
                    || template.AllowedClassIds is { Count: > 0 }
                        && !template.AllowedClassIds.Contains(hero.ClassId, StringComparer.Ordinal)
                    || equippedIds.Any(id =>
                        catalog.TryGetValue(items.Single(value => value.InstanceId == id).ItemBaseId, out var equipped)
                        && string.Equals(equipped.SlotType, template.SlotType, StringComparison.Ordinal)))
                {
                    continue;
                }

                equippedIds.Add(item.InstanceId);
                item.EquippedHeroId = hero.Id;
                break;
            }
        }
    }

    private static PassiveSelection GrowPassives(CombatContentSnapshot snapshot, HeroRecord hero)
    {
        var boardId = $"board_{hero.ClassId}";
        var nodes = snapshot.PassiveNodes.Values
            .Where(node => string.Equals(node.BoardId, boardId, StringComparison.Ordinal))
            .OrderBy(node => node.BoardDepth)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var selected = new List<string>();
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
                    boardId,
                    selected,
                    node.Id,
                    nodesById,
                    PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(1));
                if (!result.IsValid)
                {
                    continue;
                }

                selected = result.NormalizedNodeIds.ToList();
                changed = true;
            }
        }

        return new PassiveSelection(boardId, selected);
    }

    private static IReadOnlyDictionary<DeploymentAnchorId, string> BuildGreedyDeployment(IReadOnlyList<HeroRecord> heroes)
    {
        var front = new Queue<DeploymentAnchorId>(DeploymentAnchors.Where(anchor => anchor.IsFrontRow()));
        var back = new Queue<DeploymentAnchorId>(DeploymentAnchors.Where(anchor => !anchor.IsFrontRow()));
        var result = new Dictionary<DeploymentAnchorId, string>();
        foreach (var hero in heroes.Take(4))
        {
            var prefersFront = hero.ClassId is "vanguard" or "duelist";
            var primary = prefersFront ? front : back;
            var fallback = prefersFront ? back : front;
            var anchor = primary.Count > 0 ? primary.Dequeue() : fallback.Dequeue();
            result[anchor] = hero.Id;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, HeroLoadoutState> BuildHeroLoadouts(
        IReadOnlyList<HeroRecord> heroes,
        IReadOnlyDictionary<string, List<string>> equippedByHero,
        IReadOnlyDictionary<string, PassiveSelection> passivesByHero)
    {
        var result = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        foreach (var hero in heroes)
        {
            var equipped = equippedByHero[hero.Id];
            passivesByHero.TryGetValue(hero.Id, out var passive);
            if (equipped.Count == 0 && passive == null)
            {
                continue;
            }

            result[hero.Id] = new HeroLoadoutState(
                hero.Id,
                equipped,
                Array.Empty<string>(),
                passive?.BoardId ?? string.Empty,
                passive?.NodeIds ?? Array.Empty<string>(),
                Array.Empty<string>());
        }

        return result;
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

    private static int DeriveH100Seed(string contextHash, int salt)
    {
        unchecked
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var hash = offset;
            var payload = Encoding.UTF8.GetBytes($"h100|{contextHash}|{salt.ToString(CultureInfo.InvariantCulture)}");
            foreach (var value in payload)
            {
                hash ^= value;
                hash *= prime;
            }

            var result = (int)(hash & 0x7fffffffu);
            return result == 0 ? 1 : result;
        }
    }

    private static CellSpec ParseCell(string cellId)
    {
        var parts = cellId.Split('|');
        if (parts.Length != 4
            || !Squads.TryGetValue(parts[0], out var squad)
            || !BuildPowers.TryGetValue(parts[1], out var buildPower)
            || !EnemyVariants.TryGetValue(parts[2], out var enemyVariantIndex)
            || !CoverageCounts.TryGetValue(parts[3], out var benchCount))
        {
            throw new InvalidDataException($"Invalid canonical campaign battle cell: {cellId}");
        }

        return new CellSpec(cellId, parts[0], parts[1], squad, buildPower, enemyVariantIndex, benchCount);
    }

    private static BattleDivergence? FindFirstDivergence(
        IReadOnlyList<CampaignBattleOutcome> unity,
        IReadOnlyList<CampaignBattleOutcome> headless)
    {
        if (unity.Count != headless.Count)
        {
            return new BattleDivergence("(report)", "cell_count", unity.Count.ToString(), headless.Count.ToString());
        }

        for (var index = 0; index < unity.Count; index++)
        {
            var left = unity[index];
            var right = headless[index];
            foreach (var checkpoint in new[]
                     {
                         ("loadout", left.Setup.CompiledAllyLoadoutCanonicalJson, right.Setup.CompiledAllyLoadoutCanonicalJson),
                         ("affix", JsonConvert.SerializeObject(left.Setup.GeneratedAffixAssignments), JsonConvert.SerializeObject(right.Setup.GeneratedAffixAssignments)),
                         ("encounter", left.Setup.ResolvedEncounterCanonicalJson, right.Setup.ResolvedEncounterCanonicalJson),
                         ("initial_hash", left.Setup.InitialBattleStateHash, right.Setup.InitialBattleStateHash),
                     })
            {
                if (!string.Equals(checkpoint.Item2, checkpoint.Item3, StringComparison.Ordinal))
                {
                    return new BattleDivergence(left.CellId, checkpoint.Item1, checkpoint.Item2, checkpoint.Item3);
                }
            }

            var stepCount = Math.Min(left.StepBattleStateHashes.Count, right.StepBattleStateHashes.Count);
            for (var stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                if (!string.Equals(
                        left.StepBattleStateHashes[stepIndex],
                        right.StepBattleStateHashes[stepIndex],
                        StringComparison.Ordinal))
                {
                    return new BattleDivergence(
                        left.CellId,
                        $"simulation_step_{stepIndex + 1}",
                        left.StepBattleStateHashes[stepIndex],
                        right.StepBattleStateHashes[stepIndex]);
                }
            }

            if (left.StepBattleStateHashes.Count != right.StepBattleStateHashes.Count)
            {
                return new BattleDivergence(
                    left.CellId,
                    "simulation_step_count",
                    left.StepBattleStateHashes.Count.ToString(CultureInfo.InvariantCulture),
                    right.StepBattleStateHashes.Count.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var candidate in new[]
                     {
                         ("cell", left.CellId, right.CellId),
                         ("winner", left.Winner, right.Winner),
                         ("final_battle_state_hash", left.FinalBattleStateHash, right.FinalBattleStateHash),
                         ("encounter_id", left.EncounterId, right.EncounterId),
                         ("battle_seed", left.BattleSeed.ToString(CultureInfo.InvariantCulture), right.BattleSeed.ToString(CultureInfo.InvariantCulture)),
                         ("step_count", left.StepCount.ToString(CultureInfo.InvariantCulture), right.StepCount.ToString(CultureInfo.InvariantCulture)),
                     })
            {
                if (!string.Equals(candidate.Item2, candidate.Item3, StringComparison.Ordinal))
                {
                    return new BattleDivergence(left.CellId, candidate.Item1, candidate.Item2, candidate.Item3);
                }
            }
        }

        return null;
    }

    private static string Resolve(string repositoryRoot, string relativePath) =>
        Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private sealed class CellSelection
    {
        public List<string> CellIds { get; set; } = new();
    }

    private sealed record SquadSpec(IReadOnlyList<string> CoreArchetypeIds, IReadOnlyList<string> BenchArchetypeIds);
    private sealed record BuildPowerSpec(int EquipmentSlotsPerHero, bool GrowAvailablePassives);
    private sealed record CellSpec(
        string CellId,
        string SquadId,
        string QuantileId,
        SquadSpec Squad,
        BuildPowerSpec BuildPower,
        int EnemyVariantIndex,
        int BenchArchetypeCount);
    private sealed record PassiveSelection(string BoardId, IReadOnlyList<string> NodeIds);

    private sealed class HeadlessItem
    {
        internal HeadlessItem(
            string instanceId,
            string itemBaseId,
            IReadOnlyList<string> affixIds,
            string equippedHeroId,
            int acquisitionIndex)
        {
            InstanceId = instanceId;
            ItemBaseId = itemBaseId;
            AffixIds = affixIds;
            EquippedHeroId = equippedHeroId;
            AcquisitionIndex = acquisitionIndex;
        }

        internal string InstanceId { get; }
        internal string ItemBaseId { get; }
        internal IReadOnlyList<string> AffixIds { get; }
        internal string EquippedHeroId { get; set; }
        internal int AcquisitionIndex { get; }
    }

    private sealed record CampaignBattleReport(string SchemaVersion, IReadOnlyList<CampaignBattleOutcome> Outcomes);
    private sealed record UnityCampaignBattleReport(
        string SchemaVersion,
        IReadOnlyList<string> PureLookupMethods,
        IReadOnlyList<string> ScriptableObjectLookupMethods,
        IReadOnlyList<CampaignBattleOutcome> Outcomes);
    private sealed record CampaignBattleOutcome(
        string CellId,
        string Winner,
        string FinalBattleStateHash,
        string EncounterId,
        int BattleSeed,
        int StepCount,
        CampaignBattleSetupCheckpoint Setup,
        IReadOnlyList<string> StepBattleStateHashes);
    private sealed record CampaignBattleSetupCheckpoint(
        IReadOnlyList<CampaignUnitCheckpoint> CompiledAllyLoadout,
        string CompiledAllyLoadoutCanonicalJson,
        IReadOnlyList<CampaignItemCheckpoint> GeneratedAffixAssignments,
        CampaignEncounterCheckpoint ResolvedEncounter,
        string ResolvedEncounterCanonicalJson,
        string InitialBattleStateHash);
    private sealed record CampaignUnitCheckpoint(
        string UnitId,
        string ArchetypeId,
        string PreferredAnchor,
        string RoleTag,
        IReadOnlyList<CampaignStatCheckpoint> Stats,
        IReadOnlyList<CampaignItemCheckpoint> Equipment);
    private sealed record CampaignStatCheckpoint(string Key, string Value);
    private sealed record CampaignItemCheckpoint(
        int AcquisitionIndex,
        string ItemInstanceId,
        string ItemBaseId,
        string SlotType,
        string EquippedHeroId,
        IReadOnlyList<string> AffixIds);
    private sealed record CampaignEncounterCheckpoint(
        string EncounterId,
        int BattleSeed,
        string BattleContextHash,
        string BossOverlayId,
        string EnemyPosture,
        IReadOnlyList<CampaignEnemyCheckpoint> Enemies);
    private sealed record CampaignEnemyCheckpoint(
        string UnitId,
        string ArchetypeId,
        string PreferredAnchor,
        IReadOnlyList<CampaignStatCheckpoint> Stats,
        IReadOnlyList<string> PackageIds,
        IReadOnlyList<string> RulePackageIds,
        IReadOnlyList<string> CompileTags);
    private sealed record BattleDivergence(string Cell, string Field, string Unity, string Headless);
}
