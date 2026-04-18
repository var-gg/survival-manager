using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core;
using SM.Core.Contracts;
using SM.Core.Results;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity.Sandbox;
using Unity.Profiling;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    private void SeedDemoProfile()
    {
        Profile.DisplayName = "Demo Player";
        Profile.Currencies = new CurrencyRecord { Gold = 12, Echo = 45 };
        Profile.UnlockedPermanentAugmentIds = new List<string>();
        Profile.Inventory = new List<InventoryItemRecord>();
        Profile.Heroes.Clear();

        var archetypeIds = _combatContentLookup.GetCanonicalArchetypeIds();
        var itemIds = _combatContentLookup.GetCanonicalItemIds();
        var affixIds = _combatContentLookup.GetCanonicalAffixIds();
        for (var i = 0; i < Math.Min(MetaBalanceDefaults.ExpeditionSquadCap, archetypeIds.Count); i++)
        {
            var archetypeId = archetypeIds[i];
            _combatContentLookup.TryGetArchetype(archetypeId, out var archetype);
            var heroId = $"hero-{i + 1}";
            var equippedItems = new List<string>();
            if (itemIds.Count > 0 && i < 4)
            {
                var itemInstanceId = $"demo-item-{i + 1}";
                Profile.Inventory.Add(new InventoryItemRecord
                {
                    ItemInstanceId = itemInstanceId,
                    ItemBaseId = itemIds[i % itemIds.Count],
                    EquippedHeroId = heroId,
                    AffixIds = affixIds.Count == 0
                        ? new List<string>()
                        : new List<string> { affixIds[i % affixIds.Count] }
                });
                equippedItems.Add(itemInstanceId);
            }

            Profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = heroId,
                Name = archetype != null ? ResolveArchetypeDisplayName(archetype) : $"Hero {i + 1}",
                ArchetypeId = archetypeId,
                RaceId = archetype?.Race.Id ?? string.Empty,
                ClassId = archetype?.Class.Id ?? string.Empty,
                PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(archetypeId, string.Empty, i),
                NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(archetypeId, string.Empty, i + 1),
                FlexActiveId = archetype?.Loadout?.FlexActive?.Id ?? string.Empty,
                FlexPassiveId = archetype?.Loadout?.FlexPassive?.Id ?? string.Empty,
                RecruitTier = archetype?.RecruitTier ?? RecruitTier.Common,
                RecruitSource = RecruitOfferSource.DirectGrant,
                RetrainState = new UnitRetrainState(),
                EconomyFootprint = new UnitEconomyFootprint(),
                EquippedItemIds = equippedItems
            });
        }
    }

    private IReadOnlyList<BattleEquippedItemSpec> BuildEquippedItemSpecs(
        HeroInstanceRecord hero,
        IReadOnlyDictionary<string, InventoryItemRecord> inventoryByInstanceId)
    {
        var instanceIds = new HashSet<string>(hero.EquippedItemIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        foreach (var inventoryItem in Profile.Inventory.Where(item => item.EquippedHeroId == hero.HeroId))
        {
            instanceIds.Add(inventoryItem.ItemInstanceId);
        }

        return instanceIds
            .Where(inventoryByInstanceId.ContainsKey)
            .Select(id => inventoryByInstanceId[id])
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemBaseId))
            .Select(item => new BattleEquippedItemSpec(
                item.ItemBaseId,
                item.AffixIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    private void NormalizeProfileContentIds()
    {
        NormalizeHeroContentIds();
        NormalizeInventoryContentIds();
        NormalizeExpeditionContentIds();
        NormalizeEquippedItemReferences();
        NormalizeBuildStateRecords();
    }

    private void NormalizeHeroContentIds()
    {
        for (var i = 0; i < Profile.Heroes.Count; i++)
        {
            var hero = Profile.Heroes[i];
            hero.EquippedItemIds ??= new List<string>();
            hero.RetrainState ??= new UnitRetrainState();
            hero.EconomyFootprint ??= new UnitEconomyFootprint();
            hero.ArchetypeId = _combatContentLookup.NormalizeArchetypeId(hero.ArchetypeId, hero.RaceId, hero.ClassId, i);
            if (_combatContentLookup.TryGetArchetype(hero.ArchetypeId, out var archetype))
            {
                hero.RaceId = archetype.Race.Id;
                hero.ClassId = archetype.Class.Id;
                hero.FlexActiveId = string.IsNullOrWhiteSpace(hero.FlexActiveId)
                    ? archetype.Loadout?.FlexActive?.Id ?? string.Empty
                    : hero.FlexActiveId;
                hero.FlexPassiveId = string.IsNullOrWhiteSpace(hero.FlexPassiveId)
                    ? archetype.Loadout?.FlexPassive?.Id ?? string.Empty
                    : hero.FlexPassiveId;
                hero.RecruitTier = archetype.RecruitTier;
            }

            hero.CharacterId = NormalizeCharacterId(hero.CharacterId, hero.ArchetypeId);
            if (_combatContentLookup.TryGetCharacterDefinition(hero.CharacterId, out var character))
            {
                if (character.Race != null)
                {
                    hero.RaceId = character.Race.Id;
                }

                if (character.Class != null)
                {
                    hero.ClassId = character.Class.Id;
                }
            }

            hero.PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(hero.ArchetypeId, hero.PositiveTraitId, i);
            hero.NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(hero.ArchetypeId, hero.NegativeTraitId, i + 1);
            hero.EquippedItemIds = hero.EquippedItemIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private void NormalizeInventoryContentIds()
    {
        for (var i = 0; i < Profile.Inventory.Count; i++)
        {
            var item = Profile.Inventory[i];
            item.AffixIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(item.ItemInstanceId))
            {
                item.ItemInstanceId = $"inventory-{Guid.NewGuid():N}";
            }

            item.ItemBaseId = _combatContentLookup.NormalizeItemBaseId(item.ItemBaseId, i);
            item.AffixIds = item.AffixIds
                .Select((affixId, affixIndex) => _combatContentLookup.NormalizeAffixId(affixId, i + affixIndex))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private void NormalizeExpeditionContentIds()
    {
        var normalizedAugments = Expedition.TemporaryAugmentIds
                .Select((augmentId, index) => _combatContentLookup.NormalizeTemporaryAugmentId(augmentId, index))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        Expedition = new ExpeditionState(Expedition.CurrentNodeIndex, normalizedAugments);
    }

    private void NormalizeEquippedItemReferences()
    {
        var inventoryById = Profile.Inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemInstanceId))
            .GroupBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var heroIds = Profile.Heroes.Select(hero => hero.HeroId).ToHashSet(StringComparer.Ordinal);

        foreach (var inventoryItem in Profile.Inventory)
        {
            if (!string.IsNullOrWhiteSpace(inventoryItem.EquippedHeroId) && !heroIds.Contains(inventoryItem.EquippedHeroId))
            {
                inventoryItem.EquippedHeroId = string.Empty;
            }
        }

        foreach (var hero in Profile.Heroes)
        {
            var equippedIds = new HashSet<string>(hero.EquippedItemIds.Where(inventoryById.ContainsKey), StringComparer.Ordinal);
            foreach (var inventoryItem in Profile.Inventory.Where(item => item.EquippedHeroId == hero.HeroId))
            {
                equippedIds.Add(inventoryItem.ItemInstanceId);
            }

            hero.EquippedItemIds = equippedIds.ToList();
            foreach (var equippedId in equippedIds)
            {
                inventoryById[equippedId].EquippedHeroId = hero.HeroId;
            }
        }
    }

    private string ResolveRewardItemId(int index)
    {
        return _combatContentLookup.NormalizeItemBaseId(string.Empty, index);
    }

    private static string ResolveArchetypeDisplayName(UnitArchetypeDefinition archetype)
    {
        if (!string.IsNullOrWhiteSpace(archetype.LegacyDisplayName))
        {
            return archetype.LegacyDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(archetype.NameKey))
        {
            return archetype.NameKey;
        }

        return archetype.Id;
    }

    private string ResolveRewardAugmentId(int index, params string[] preferredAugmentIds)
    {
        foreach (var preferredAugmentId in preferredAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (_combatContentLookup.TryGetAugmentDefinition(preferredAugmentId, out var augment) && !augment.IsPermanent)
            {
                return preferredAugmentId;
            }
        }

        return _combatContentLookup.NormalizeTemporaryAugmentId(string.Empty, index);
    }

    private string ResolvePendingPermanentUnlockId(string temporaryAugmentId)
    {
        var definitions = BuildPermanentProgressionAugmentDefinitions(temporaryAugmentId);
        var resolution = PermanentAugmentProgressionService.ResolvePendingUnlock(
            temporaryAugmentId,
            definitions,
            Profile.UnlockedPermanentAugmentIds);
        return resolution.HasUnlock ? resolution.UnlockAugmentId : string.Empty;
    }

    private IReadOnlyList<AugmentCatalogEntry> BuildPermanentProgressionAugmentDefinitions(params string[] explicitAugmentIds)
    {
        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return Array.Empty<AugmentCatalogEntry>();
        }

        var augmentIds = explicitAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Concat(_combatContentLookup.GetCanonicalTemporaryAugmentIds())
            .Concat(_combatContentLookup.GetCanonicalPermanentAugmentIds())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);
        var definitions = new List<AugmentCatalogEntry>();
        foreach (var augmentId in augmentIds)
        {
            if (snapshot.AugmentCatalog.TryGetValue(augmentId, out var augment))
            {
                definitions.Add(augment);
            }
        }

        return definitions;
    }

    private void TrackPermanentAugmentProgression(string temporaryAugmentId)
    {
        if (ActiveRun == null || string.IsNullOrWhiteSpace(temporaryAugmentId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ActiveRun.Overlay.FirstSelectedTemporaryAugmentId))
        {
            return;
        }

        ActiveRun = ActiveRun with
        {
            Overlay = ActiveRun.Overlay with
            {
                FirstSelectedTemporaryAugmentId = temporaryAugmentId,
                PendingPermanentUnlockId = ResolvePendingPermanentUnlockId(temporaryAugmentId),
            }
        };
    }

    private void ConsumePendingPermanentUnlock()
    {
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        if (ActiveRun == null || string.IsNullOrWhiteSpace(ActiveRun.Overlay.PendingPermanentUnlockId))
        {
            return;
        }

        var unlockAugmentId = ActiveRun.Overlay.PendingPermanentUnlockId;
        var unlockResult = UnlockPermanentAugmentCandidate(unlockAugmentId);
        if (!unlockResult.IsSuccess)
        {
            ActiveRun = ActiveRun with
            {
                Overlay = ActiveRun.Overlay with
                {
                    PendingPermanentUnlockId = string.Empty,
                }
            };
            SyncActiveRunRecord();
            return;
        }

        LastPermanentUnlockSummary = new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.permanent_unlock",
            "Permanent candidate unlocked: {0}",
            SessionTextArg.AugmentName(unlockAugmentId));
        ActiveRun = ActiveRun with
        {
            Overlay = ActiveRun.Overlay with
            {
                PendingPermanentUnlockId = string.Empty,
            }
        };
        SyncActiveRunRecord();
    }

    private IReadOnlyDictionary<string, PassiveNodeTemplate> BuildPassiveBoardNodeDictionary(string boardId)
    {
        if (string.IsNullOrWhiteSpace(boardId)
            || !_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal);
        }

        return snapshot.PassiveNodes
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key)
                           && pair.Value != null
                           && string.Equals(pair.Value.BoardId, boardId, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static IEnumerable<HeroRecord> ToHeroRecords(SaveProfile profile)
    {
        foreach (var hero in profile.Heroes)
        {
            yield return new HeroRecord(
                hero.HeroId,
                hero.Name,
                hero.ArchetypeId,
                hero.RaceId,
                hero.ClassId,
                hero.PositiveTraitId,
                hero.NegativeTraitId,
                hero.FlexActiveId,
                hero.FlexPassiveId,
                hero.RecruitTier,
                hero.RecruitSource,
                hero.RetrainState?.Clone() ?? new UnitRetrainState(),
                hero.EconomyFootprint?.Clone() ?? new UnitEconomyFootprint(),
                hero.CharacterId);
        }
    }

    private void EnsureProfileBuildState()
    {
        foreach (var hero in Profile.Heroes)
        {
            if (Profile.HeroLoadouts.All(record => record.HeroId != hero.HeroId))
            {
                Profile.HeroLoadouts.Add(new HeroLoadoutRecord
                {
                    HeroId = hero.HeroId,
                    EquippedItemInstanceIds = hero.EquippedItemIds.ToList(),
                });
            }

            if (Profile.HeroProgressions.All(record => record.HeroId != hero.HeroId))
            {
                Profile.HeroProgressions.Add(new HeroProgressionRecord { HeroId = hero.HeroId, Level = 1 });
            }

            if (Profile.PassiveSelections.All(record => record.HeroId != hero.HeroId))
            {
                Profile.PassiveSelections.Add(new PassiveSelectionRecord { HeroId = hero.HeroId });
            }
        }

        if (Profile.PermanentAugmentLoadouts.All(record => record.BlueprintId != Profile.ActiveBlueprintId))
        {
            Profile.PermanentAugmentLoadouts.Add(new PermanentAugmentLoadoutRecord
            {
                BlueprintId = Profile.ActiveBlueprintId,
                EquippedAugmentIds = new List<string>()
            });
        }

        if (Profile.SquadBlueprints.All(record => record.BlueprintId != Profile.ActiveBlueprintId))
        {
            CaptureBlueprintState();
        }
    }

    private void NormalizeBuildStateRecords()
    {
        Profile.ActiveRun ??= new ActiveRunRecord();
        Profile.ActiveRun.RecruitPhase ??= new RecruitPhaseState();
        Profile.ActiveRun.RecruitPity ??= new RecruitPityState();

        foreach (var loadout in Profile.HeroLoadouts)
        {
            loadout.EquippedItemInstanceIds ??= new List<string>();
            loadout.EquippedSkillInstanceIds ??= new List<string>();
            loadout.SelectedPassiveNodeIds ??= new List<string>();
            loadout.EquippedPermanentAugmentIds ??= new List<string>();
        }

        foreach (var progression in Profile.HeroProgressions)
        {
            progression.UnlockedPassiveNodeIds ??= new List<string>();
            progression.UnlockedSkillIds ??= new List<string>();
        }

        foreach (var skillInstance in Profile.SkillInstances)
        {
            skillInstance.CompileTags ??= new List<string>();
        }

        foreach (var selection in Profile.PassiveSelections)
        {
            selection.SelectedNodeIds ??= new List<string>();
        }

        foreach (var loadout in Profile.PermanentAugmentLoadouts)
        {
            loadout.EquippedAugmentIds ??= new List<string>();
        }

        foreach (var blueprint in Profile.SquadBlueprints)
        {
            blueprint.DeploymentAssignments ??= new Dictionary<string, string>();
            blueprint.ExpeditionSquadHeroIds ??= new List<string>();
            blueprint.HeroRoleIds ??= new Dictionary<string, string>();
        }

        PermanentAugmentSlotCount = GameSessionProfileNormalizer.NormalizePermanentAugments(Profile, _combatContentLookup);
        NormalizePassiveBoardStates();
    }

    private void NormalizePassiveBoardStates()
    {
        foreach (var loadout in Profile.HeroLoadouts)
        {
            loadout.SelectedPassiveNodeIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(loadout.PassiveBoardId)
                || !_combatContentLookup.TryGetPassiveBoardDefinition(loadout.PassiveBoardId, out _))
            {
                loadout.PassiveBoardId = string.Empty;
                loadout.SelectedPassiveNodeIds = new List<string>();
                continue;
            }

            var nodesById = BuildPassiveBoardNodeDictionary(loadout.PassiveBoardId);
            var normalized = PassiveBoardSelectionValidator.Normalize(
                loadout.PassiveBoardId,
                loadout.SelectedPassiveNodeIds,
                nodesById);
            loadout.SelectedPassiveNodeIds = normalized.NormalizedNodeIds.ToList();
        }

        foreach (var selection in Profile.PassiveSelections)
        {
            selection.SelectedNodeIds ??= new List<string>();
            var loadout = Profile.HeroLoadouts.FirstOrDefault(record =>
                string.Equals(record.HeroId, selection.HeroId, StringComparison.Ordinal));
            if (loadout == null)
            {
                selection.BoardId = string.Empty;
                selection.SelectedNodeIds = new List<string>();
                continue;
            }

            selection.BoardId = loadout.PassiveBoardId;
            selection.SelectedNodeIds = loadout.SelectedPassiveNodeIds.ToList();
        }
    }

    private void RestoreActiveRunFromProfile()
    {
        RestoreRecruitStates();
        if (Profile.ActiveRun == null || string.IsNullOrWhiteSpace(Profile.ActiveRun.RunId))
        {
            ActiveRun = null;
            return;
        }

        var blueprint = TryGetBlueprintState(Profile.ActiveRun.BlueprintId) ?? CaptureBlueprintState();
        ActiveRun = new ActiveRunState(
            Profile.ActiveRun.RunId,
            Profile.ActiveRun.ExpeditionId,
            blueprint,
            new RunOverlayState(
                Profile.ActiveRun.CurrentNodeIndex,
                Profile.ActiveRun.TemporaryAugmentIds,
                Profile.ActiveRun.PendingRewardIds,
                Profile.ActiveRun.CompileVersion,
                Profile.ActiveRun.CompileHash,
                Profile.ActiveRun.RecruitPhase?.Clone() ?? new RecruitPhaseState(),
                Profile.ActiveRun.RecruitPity?.Clone() ?? new RecruitPityState(),
                Profile.ActiveRun.ChapterId,
                Profile.ActiveRun.SiteId,
                Profile.ActiveRun.SiteNodeIndex,
                Profile.ActiveRun.EncounterId,
                Profile.ActiveRun.BattleSeed,
                Profile.ActiveRun.BattleContextHash,
                Profile.ActiveRun.RewardSourceId,
                Profile.ActiveRun.FirstSelectedTemporaryAugmentId,
                Profile.ActiveRun.PendingPermanentUnlockId),
            Profile.ActiveRun.BattleDeployHeroIds,
            Profile.ActiveRun.IsQuickBattle,
            string.IsNullOrWhiteSpace(Profile.ActiveRun.LastBattleMatchId) ? null : Profile.ActiveRun.LastBattleMatchId,
            Profile.ActiveRun.LastSettlementWasVictory,
            Profile.ActiveRun.StoryCleared,
            Profile.ActiveRun.EndlessUnlocked);
        LastBattleVictory = ActiveRun.LastSettlementWasVictory;
        IsQuickBattleSmokeActive = ActiveRun.IsQuickBattle;
        QuickBattleLaneKind = ActiveRun.IsQuickBattle
            ? CombatSandboxLaneKind.TownIntegrationSmoke
            : CombatSandboxLaneKind.None;
        HasActiveExpeditionRun = !ActiveRun.IsQuickBattle;
        CurrentExpeditionNodeIndex = ActiveRun.Overlay.CurrentNodeIndex;

        var resumedRewardSourceId = ActiveRun.Overlay.RewardSourceId;
        if (TryResumeRecoveredRewardSettlement())
        {
            AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardSettlementResumed(
                ResolveTelemetryRunId(),
                resumedRewardSourceId));
        }

        _hasPendingRewardSettlement = ActiveRun?.Overlay.PendingRewardIds.Count > 0;
        SelectedTeamPosture = blueprint.TeamPosture;
        SelectedTeamTacticId = blueprint.TeamTacticId;
        RestoreResolvedProgressMarkers(includeCurrentNode: _hasPendingRewardSettlement);
        if (_hasPendingRewardSettlement)
        {
            SelectedExpeditionNodeIndex = CurrentExpeditionNodeIndex;
        }
        else
        {
            AutoSelectNextExpeditionNode();
        }
    }

    private bool TryResumeRecoveredRewardSettlement()
    {
        if (ActiveRun == null
            || string.IsNullOrWhiteSpace(ActiveRun.Overlay.RewardSourceId)
            || ActiveRun.Overlay.PendingRewardIds.Count > 0
            || !HasRecordedRewardSettlement(ActiveRun.Overlay.RewardSourceId))
        {
            return false;
        }

        _hasPendingRewardSettlement = true;
        LastRewardApplicationSummary = new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.status.recovered_choice",
            "Recovered previous reward settlement.");
        FinalizeRewardSettlement();
        return true;
    }

    private bool HasRecordedRewardSettlement(string sourceId)
    {
        return !string.IsNullOrWhiteSpace(sourceId)
               && Profile.RewardLedger.Any(entry =>
                   string.Equals(entry.SourceId, sourceId, StringComparison.Ordinal)
                   && entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));
    }

    private void RebindNarrativeServices()
    {
        StoryDirector = _narrativeRuntimeBootstrap.CreateStoryDirector(Profile.Narrative);
        SyncNarrativeProgress();
    }

    private void SyncNarrativeProgress()
    {
        Profile.Narrative = StoryDirector.Progress;
    }

    private TelemetryEventRecord BuildEconomySnapshot(string label)
    {
        return RuntimeOperationalTelemetry.CreateEconomySnapshot(
            ResolveTelemetryRunId(),
            label,
            Profile.Currencies.Gold,
            Profile.Currencies.Echo,
            _pendingRewardChoices.Count,
            !LastBattleVictory);
    }

    private void SyncActiveRunIfPresent()
    {
        if (ActiveRun == null)
        {
            return;
        }

        var compileHash = LastCompiledBattleSnapshot?.CompileHash ?? ActiveRun.Overlay.LastCompileHash;
        var blueprint = IsQuickBattleSmokeActive && _compiledQuickBattleScenario != null
            ? _compiledQuickBattleScenario.LeftTeam.Blueprint
            : CaptureBlueprintState();
        ActiveRun = RunStateService.SyncBlueprint(
            ActiveRun,
            blueprint,
            compileHash,
            _pendingRewardChoices.Select(choice => choice.PayloadId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList());
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    private void SyncActiveRunRecord()
    {
        if (ActiveRun == null)
        {
            Profile.ActiveRun = new ActiveRunRecord
            {
                RecruitPhase = _recruitPhaseState.Clone(),
                RecruitPity = _recruitPityState.Clone(),
            };
            return;
        }

        Profile.ActiveRun = new ActiveRunRecord
        {
            RunId = ActiveRun.RunId,
            ExpeditionId = ActiveRun.ExpeditionId,
            BlueprintId = ActiveRun.Blueprint.BlueprintId,
            IsQuickBattle = ActiveRun.IsQuickBattle,
            CurrentNodeIndex = ActiveRun.Overlay.CurrentNodeIndex,
            TemporaryAugmentIds = ActiveRun.Overlay.TemporaryAugmentIds.ToList(),
            PendingRewardIds = ActiveRun.Overlay.PendingRewardIds.ToList(),
            BattleDeployHeroIds = ActiveRun.BattleDeployHeroIds.ToList(),
            RecruitPhase = _recruitPhaseState.Clone(),
            RecruitPity = _recruitPityState.Clone(),
            CompileVersion = ActiveRun.Overlay.CompileVersion,
            CompileHash = ActiveRun.Overlay.LastCompileHash,
            LastBattleMatchId = ActiveRun.LastBattleMatchId ?? string.Empty,
            LastSettlementWasVictory = ActiveRun.LastSettlementWasVictory,
            ChapterId = ActiveRun.Overlay.ChapterId,
            SiteId = ActiveRun.Overlay.SiteId,
            SiteNodeIndex = ActiveRun.Overlay.SiteNodeIndex,
            EncounterId = ActiveRun.Overlay.EncounterId,
            BattleSeed = ActiveRun.Overlay.BattleSeed,
            BattleContextHash = ActiveRun.Overlay.BattleContextHash,
            RewardSourceId = ActiveRun.Overlay.RewardSourceId,
            FirstSelectedTemporaryAugmentId = ActiveRun.Overlay.FirstSelectedTemporaryAugmentId,
            PendingPermanentUnlockId = ActiveRun.Overlay.PendingPermanentUnlockId,
            StoryCleared = ActiveRun.StoryCleared,
            EndlessUnlocked = ActiveRun.EndlessUnlocked,
        };
    }

    private void RestoreRecruitStates()
    {
        _recruitPhaseState = Profile.ActiveRun?.RecruitPhase?.Clone() ?? new RecruitPhaseState();
        _recruitPityState = Profile.ActiveRun?.RecruitPity?.Clone() ?? new RecruitPityState();
    }

    private void SyncRecruitState()
    {
        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with
            {
                Overlay = ActiveRun.Overlay with
                {
                    RecruitPhase = _recruitPhaseState.Clone(),
                    RecruitPity = _recruitPityState.Clone(),
                }
            };
        }

        SyncActiveRunRecord();
    }

    private SquadBlueprintState CaptureBlueprintState()
    {
        EnsureAssignmentMapInitialized();

        var deploymentAssignments = DeploymentAnchorOrder
            .Where(anchor => _deploymentAssignments.TryGetValue(anchor, out var heroId) && !string.IsNullOrWhiteSpace(heroId))
            .ToDictionary(anchor => anchor, anchor => _deploymentAssignments[anchor]!, EqualityComparer<DeploymentAnchorId>.Default);

        var blueprint = new SquadBlueprintState(
            string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId) ? "blueprint.default" : Profile.ActiveBlueprintId,
            "Default Build",
            SelectedTeamPosture,
            SelectedTeamTacticId,
            deploymentAssignments,
            _expeditionSquadHeroIds.ToList(),
            Profile.Heroes.ToDictionary(hero => hero.HeroId, hero => ResolveBlueprintRoleInstructionId(hero.HeroId, hero.ClassId, ResolvePreferredAnchor(hero.HeroId)), StringComparer.Ordinal));

        var record = Profile.SquadBlueprints.FirstOrDefault(existing => existing.BlueprintId == blueprint.BlueprintId);
        if (record == null)
        {
            record = new SquadBlueprintRecord { BlueprintId = blueprint.BlueprintId };
            Profile.SquadBlueprints.Add(record);
        }

        record.DisplayName = blueprint.DisplayName;
        record.TeamPosture = blueprint.TeamPosture.ToString();
        record.TeamTacticId = blueprint.TeamTacticId;
        record.DeploymentAssignments = blueprint.DeploymentAssignments.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value, StringComparer.Ordinal);
        record.ExpeditionSquadHeroIds = blueprint.ExpeditionSquadHeroIds.ToList();
        record.HeroRoleIds = blueprint.HeroRoleIds.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Profile.ActiveBlueprintId = blueprint.BlueprintId;
        return blueprint;
    }

    private SquadBlueprintState? TryGetBlueprintState(string blueprintId)
    {
        var record = Profile.SquadBlueprints.FirstOrDefault(existing => existing.BlueprintId == blueprintId);
        if (record == null)
        {
            return null;
        }

        if (!Enum.TryParse<TeamPostureType>(record.TeamPosture, out var posture))
        {
            posture = SelectedTeamPosture;
        }

        return new SquadBlueprintState(
            record.BlueprintId,
            string.IsNullOrWhiteSpace(record.DisplayName) ? "Default Build" : record.DisplayName,
            posture,
            record.TeamTacticId ?? string.Empty,
            record.DeploymentAssignments
                .Where(pair => Enum.TryParse<DeploymentAnchorId>(pair.Key, out _))
                .ToDictionary(pair => Enum.Parse<DeploymentAnchorId>(pair.Key), pair => pair.Value),
            record.ExpeditionSquadHeroIds,
            record.HeroRoleIds);
    }

    private static IReadOnlyDictionary<string, HeroLoadoutState> ToHeroLoadoutStates(SaveProfile profile)
    {
        return profile.HeroLoadouts.ToDictionary(
            record => record.HeroId,
            record => new HeroLoadoutState(
                record.HeroId,
                record.EquippedItemInstanceIds,
                record.EquippedSkillInstanceIds,
                record.PassiveBoardId,
                record.SelectedPassiveNodeIds,
                record.EquippedPermanentAugmentIds),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, HeroProgressionState> ToHeroProgressionStates(SaveProfile profile)
    {
        return profile.HeroProgressions.ToDictionary(
            record => record.HeroId,
            record => new HeroProgressionState(
                record.HeroId,
                record.Level,
                record.Experience,
                record.UnlockedPassiveNodeIds,
                record.UnlockedSkillIds),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ItemInstanceState> ToItemInstanceStates(SaveProfile profile)
    {
        return profile.Inventory.ToDictionary(
            record => record.ItemInstanceId,
            record => new ItemInstanceState(
                record.ItemInstanceId,
                record.ItemBaseId,
                record.AffixIds,
                record.EquippedHeroId),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, SkillInstanceState> ToSkillInstanceStates(SaveProfile profile)
    {
        return profile.SkillInstances.ToDictionary(
            record => record.SkillInstanceId,
            record => new SkillInstanceState(
                record.SkillInstanceId,
                record.SkillId,
                CompiledSkillSlots.Normalize(record.SlotKind),
                record.CompileTags,
                record.ResolvedSlotKind),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PassiveBoardSelectionState> ToPassiveSelections(SaveProfile profile)
    {
        return profile.PassiveSelections.ToDictionary(
            record => record.HeroId,
            record => new PassiveBoardSelectionState(
                record.HeroId,
                record.BoardId,
                record.SelectedNodeIds),
            StringComparer.Ordinal);
    }

    private static PermanentAugmentLoadoutState ToPermanentAugmentLoadout(SaveProfile profile, string blueprintId)
    {
        var record = profile.PermanentAugmentLoadouts.FirstOrDefault(existing => existing.BlueprintId == blueprintId)
            ?? new PermanentAugmentLoadoutRecord { BlueprintId = blueprintId };
        return new PermanentAugmentLoadoutState(record.BlueprintId, record.EquippedAugmentIds);
    }
}
