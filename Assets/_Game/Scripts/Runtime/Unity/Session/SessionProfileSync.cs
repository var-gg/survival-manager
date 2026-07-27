using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Combat.Model;
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
    internal sealed class SessionProfileSync
    {
        private readonly GameSessionState _session;

        internal SessionProfileSync(GameSessionState session)
        {
            _session = session;
        }

        internal void BindProfile(SaveProfile profile) => _session.BindProfileCore(profile);

        internal void AdvanceNarrative(NarrativeMoment moment, StoryMomentContext? context)
        {
            _session.StoryDirector.Advance(moment, context ?? StoryMomentContext.Empty);
            _session.SyncNarrativeProgress();
        }

        internal bool TryDequeueNarrativePresentation(out StoryPresentationRequest? request)
        {
            var dequeued = _session.StoryDirector.TryDequeuePendingPresentation(out request);
            _session.SyncNarrativeProgress();
            return dequeued;
        }

        internal void ResetNarrativeRunScopedProgress()
        {
            _session.StoryDirector.ResetRunScopedProgress();
            _session.SyncNarrativeProgress();
        }

        internal void SetCurrentScene(string sceneName)
        {
            _session.CurrentSceneName = sceneName;
            if (string.Equals(sceneName, SceneNames.Town, StringComparison.Ordinal))
            {
                _session.ResetRecruitPhaseForTownEntry();
                _session.AppendRuntimeTelemetry(_session.BuildEconomySnapshot("town_entry"));
            }
        }

        internal bool CanManualProfileReload(out string reason)
        {
            if (!string.Equals(_session.CurrentSceneName, SceneNames.Town, StringComparison.Ordinal))
            {
                reason = "프로필 재로드는 Town에서만 허용됩니다.";
                return false;
            }

            if (_session.HasActiveExpeditionRun)
            {
                reason = "진행 중인 expedition이 있어 프로필을 다시 불러올 수 없습니다.";
                return false;
            }

            if (_session._hasPendingRewardSettlement)
            {
                reason = "보상 settlement가 남아 있어 프로필을 다시 불러올 수 없습니다.";
                return false;
            }

            if (_session.IsQuickBattleSmokeActive)
            {
                reason = "Quick Battle smoke overlay 중에는 프로필을 다시 불러올 수 없습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal void SaveDebugSnapshot(string note)
        {
            _session.Profile.RunSummaries.Add(new RunSummaryRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                ExpeditionId = note,
                Result = "debug-save",
                GoldEarned = 0,
                NodesCleared = _session.CurrentExpeditionNodeIndex,
                CompletedAtUtc = DateTime.UtcNow.ToString("O")
            });
        }

        internal void ClearRuntimeTelemetry()
        {
            _session._runtimeTelemetryEvents.Clear();
        }

        internal void RecordOperationalTelemetry(TelemetryEventRecord record)
        {
            _session.AppendRuntimeTelemetry(record);
        }
    }

    private void SeedDemoProfile()
    {
        Profile.DisplayName = "Demo Player";
        Profile.Currencies = new CurrencyRecord { Gold = 12, Echo = 45 };
        Profile.UnlockedPermanentAugmentIds = new List<string>();
        Profile.Inventory = new List<InventoryItemRecord>();
        Profile.Heroes.Clear();

        var snapshot = _sessionContentLookup.Snapshot;
        var archetypeIds = _sessionContentLookup.GetCanonicalArchetypeIds();
        var itemIds = _sessionContentLookup.GetCanonicalItemIds();
        for (var i = 0; i < Math.Min(MetaBalanceDefaults.ExpeditionSquadCap, archetypeIds.Count); i++)
        {
            var archetypeId = archetypeIds[i];
            snapshot.Archetypes.TryGetValue(archetypeId, out var archetype);
            var heroId = $"hero-{i + 1}";
            var equippedItems = new List<string>();
            if (itemIds.Count > 0 && i < 4)
            {
                var itemInstanceId = $"demo-item-{i + 1}";
                Profile.Inventory.Add(CreateGeneratedInventoryItem(itemIds[i % itemIds.Count], itemInstanceId, heroId));
                equippedItems.Add(itemInstanceId);
            }

            Profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = heroId,
                Name = ResolveCharacterNameKey(archetypeId),
                CharacterId = archetypeId,
                ArchetypeId = archetypeId,
                RaceId = archetype?.RaceId ?? string.Empty,
                ClassId = archetype?.ClassId ?? string.Empty,
                PositiveTraitId = _sessionContentLookup.NormalizePositiveTraitId(archetypeId, string.Empty, i),
                NegativeTraitId = _sessionContentLookup.NormalizeNegativeTraitId(archetypeId, string.Empty, i + 1),
                FlexActiveId = archetype?.FlexActive?.Id ?? string.Empty,
                FlexPassiveId = archetype?.FlexPassive?.Id ?? string.Empty,
                RecruitTier = archetype?.RecruitTier ?? RecruitTier.Common,
                RecruitSource = RecruitOfferSource.DirectGrant,
                DominantHand = DominantHandDistributionService.ResolveGenerated(heroId, archetype?.ClassId ?? string.Empty),
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
                    .ToList(),
                ToAffixMagnitudeMap(item)))
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
            hero.ArchetypeId = _sessionContentLookup.NormalizeArchetypeId(hero.ArchetypeId, hero.RaceId, hero.ClassId, i);
            if (_sessionContentLookup.Snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype))
            {
                hero.RaceId = archetype.RaceId;
                hero.ClassId = archetype.ClassId;
                hero.FlexActiveId = string.IsNullOrWhiteSpace(hero.FlexActiveId)
                    ? archetype.FlexActive?.Id ?? string.Empty
                    : hero.FlexActiveId;
                hero.FlexPassiveId = string.IsNullOrWhiteSpace(hero.FlexPassiveId)
                    ? archetype.FlexPassive?.Id ?? string.Empty
                    : hero.FlexPassiveId;
                hero.RecruitTier = archetype.RecruitTier;
            }

            hero.CharacterId = NormalizeCharacterId(hero.CharacterId, hero.ArchetypeId);
            if (_sessionContentLookup.Snapshot.Characters is { } characters
                && characters.TryGetValue(hero.CharacterId, out var character))
            {
                hero.Name = ResolveCharacterNameKey(hero.CharacterId);
                if (!string.IsNullOrWhiteSpace(character.RaceId))
                {
                    hero.RaceId = character.RaceId;
                }

                if (!string.IsNullOrWhiteSpace(character.ClassId))
                {
                    hero.ClassId = character.ClassId;
                }
            }

            hero.PositiveTraitId = _sessionContentLookup.NormalizePositiveTraitId(hero.ArchetypeId, hero.PositiveTraitId, i);
            hero.NegativeTraitId = _sessionContentLookup.NormalizeNegativeTraitId(hero.ArchetypeId, hero.NegativeTraitId, i + 1);
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
            item.AffixMagnitudeRolls ??= new List<InventoryAffixMagnitudeRecord>();
            if (string.IsNullOrWhiteSpace(item.ItemInstanceId))
            {
                Profile.ItemInstanceCounter = checked(Profile.ItemInstanceCounter + 1L);
                item.ItemInstanceId = $"inventory-i{Profile.ItemInstanceCounter.ToString(CultureInfo.InvariantCulture)}";
            }

            item.ItemBaseId = _sessionContentLookup.NormalizeItemBaseId(item.ItemBaseId, i);
            item.AffixIds = item.AffixIds
                .Select((affixId, affixIndex) => _sessionContentLookup.NormalizeAffixId(affixId, i + affixIndex))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var liveAffixIds = item.AffixIds.ToHashSet(StringComparer.Ordinal);
            item.AffixMagnitudeRolls = item.AffixMagnitudeRolls
                .Where(roll => roll != null
                               && !string.IsNullOrWhiteSpace(roll.AffixId)
                               && !float.IsNaN(roll.Magnitude)
                               && !float.IsInfinity(roll.Magnitude))
                .Select((roll, rollIndex) => new InventoryAffixMagnitudeRecord
                {
                    AffixId = _sessionContentLookup.NormalizeAffixId(roll.AffixId, i + rollIndex),
                    Magnitude = roll.Magnitude,
                })
                .Where(roll => liveAffixIds.Contains(roll.AffixId))
                .GroupBy(roll => roll.AffixId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(roll => item.AffixIds.IndexOf(roll.AffixId))
                .ToList();
        }
    }

    private void NormalizeExpeditionContentIds()
    {
        var normalizedAugments = Expedition.TemporaryAugmentIds
                .Select((augmentId, index) => _sessionContentLookup.NormalizeTemporaryAugmentId(augmentId, index))
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
        return _sessionContentLookup.NormalizeItemBaseId(string.Empty, index);
    }

    private string ResolveCharacterNameKey(string characterId)
    {
        if (_combatContentLookup.TryGetCharacterDefinition(characterId, out var character)
            && !string.IsNullOrWhiteSpace(character.NameKey))
        {
            return character.NameKey;
        }

        return ContentLocalizationTables.BuildCharacterNameKey(characterId);
    }

    private const int DynamicOfferPoolSize = 6;

    private string ResolveRewardAugmentId(int index, params string[] preferredAugmentIds)
    {
        // 동적 build-aware offer(AugmentOfferService) 우선. 실패하면 preferred/canonical fallback 으로 데모 안전성 유지.
        var dynamicId = ResolveDynamicOfferAugmentId(index);
        if (!string.IsNullOrWhiteSpace(dynamicId))
        {
            return dynamicId;
        }

        foreach (var preferredAugmentId in preferredAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (_sessionContentLookup.Snapshot.AugmentCatalog.TryGetValue(preferredAugmentId, out var augment)
                && !augment.IsPermanent)
            {
                return preferredAugmentId;
            }
        }

        return _sessionContentLookup.NormalizeTemporaryAugmentId(string.Empty, index);
    }

    // AugmentOfferService 로 현재 build 와 어울리는 temporary augment 를 점수 기반 선택한다. canonical temporary 집합
    // 안에서만 고르므로 reward payload 계약(GetCanonicalTemporaryAugmentIds 멤버십)이 유지된다. index 로 슬롯별 다른 후보.
    private string ResolveDynamicOfferAugmentId(int index)
    {
        if (!_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            || snapshot.AugmentCatalog is not { Count: > 0 } catalog)
        {
            return string.Empty;
        }

        var canonicalTemporary = new HashSet<string>(_sessionContentLookup.GetCanonicalTemporaryAugmentIds(), StringComparer.Ordinal);
        var permanentEquipped = ResolveEquippedPermanentAugmentIds();
        var offerCatalog = catalog
            .Where(pair => canonicalTemporary.Contains(pair.Key) || permanentEquipped.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (offerCatalog.Count == 0)
        {
            return string.Empty;
        }

        var acquiredAugmentIds = ActiveRun?.Overlay.TemporaryAugmentIds ?? Array.Empty<string>();
        var offer = AugmentOfferService.BuildOffer(
            offerCatalog,
            new AugmentOfferContext(
                string.Empty,
                ResolveActiveBuildTags(),
                acquiredAugmentIds,
                permanentEquipped,
                ActiveRun?.Overlay.BattleSeed ?? 0,
                index,
                DynamicOfferPoolSize));
        if (offer.Count == 0)
        {
            return string.Empty;
        }

        var slot = ((index % offer.Count) + offer.Count) % offer.Count;
        return offer[slot].Id;
    }

    // 현재 run 에 장착된 temporary augment 의 태그를 build 신호로 노출 → offer 가 빌드와 어울리는 augment 를 더 높게 점수화.
    private IReadOnlyCollection<string> ResolveActiveBuildTags()
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        tags.Add(SelectedTeamPosture.ToString());

        foreach (var (anchor, heroId) in EnumerateDeploymentAssignments())
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                continue;
            }

            tags.Add(heroId);
            if (!TryGetHero(heroId, out var hero))
            {
                continue;
            }

            AddIfNotEmpty(tags, hero.CharacterId);
            AddIfNotEmpty(tags, hero.ArchetypeId);
            AddIfNotEmpty(tags, hero.ClassId);
            AddIfNotEmpty(tags, ResolveRoleTag(hero.ClassId, anchor));
        }

        if (ActiveRun?.Overlay.TemporaryAugmentIds is not { Count: > 0 } activeAugmentIds
            || !_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            || snapshot.AugmentCatalog is not { Count: > 0 } catalog)
        {
            return tags;
        }

        foreach (var augmentId in activeAugmentIds)
        {
            if (catalog.TryGetValue(augmentId, out var entry))
            {
                foreach (var tag in entry.Tags)
                {
                    tags.Add(tag);
                }
            }
        }

        return tags;
    }

    private IReadOnlyCollection<string> ResolveEquippedPermanentAugmentIds()
    {
        var blueprintId = ActiveRun?.Blueprint.BlueprintId;
        if (string.IsNullOrWhiteSpace(blueprintId))
        {
            blueprintId = string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId)
                ? "blueprint.default"
                : Profile.ActiveBlueprintId;
        }

        var equippedAugmentIds = Profile.PermanentAugmentLoadouts
            .FirstOrDefault(loadout => string.Equals(loadout.BlueprintId, blueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds;
        if (equippedAugmentIds == null)
        {
            return Array.Empty<string>();
        }

        return equippedAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void AddIfNotEmpty(ISet<string> tags, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(value);
        }
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
        if (!_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return Array.Empty<AugmentCatalogEntry>();
        }

        var augmentIds = explicitAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Concat(_sessionContentLookup.GetCanonicalTemporaryAugmentIds())
            .Concat(_sessionContentLookup.GetCanonicalPermanentAugmentIds())
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
            || !_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
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
                hero.CharacterId,
                hero.DominantHand);
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
            if (!HasPassiveBoardContent(loadout.PassiveBoardId))
            {
                loadout.PassiveBoardId = string.Empty;
                loadout.SelectedPassiveNodeIds = new List<string>();
                continue;
            }

            var nodesById = BuildPassiveBoardNodeDictionary(loadout.PassiveBoardId);
            // 노드 예산은 영웅 레벨 계단(오너 게이트③) — 레벨은 단조 증가라 정상 세이브에서 trim은 발생하지
            // 않고, 손편집/오염 세이브의 초과 선택만 잘린다.
            var progression = Profile.HeroProgressions.FirstOrDefault(record =>
                string.Equals(record.HeroId, loadout.HeroId, StringComparison.Ordinal));
            var normalized = PassiveBoardSelectionValidator.Normalize(
                loadout.PassiveBoardId,
                loadout.SelectedPassiveNodeIds,
                nodesById,
                PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(progression?.Level ?? 1));
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
                Profile.ActiveRun.PendingPermanentUnlockId,
                Profile.ActiveRun.RewardCommitId,
                Profile.ActiveRun.PledgedWarrantId,
                Profile.ActiveRun.RewardedRevisitIndex,
                Profile.ActiveRun.RevisitItemRollsGranted,
                Profile.ActiveRun.RevisitCurrencyGranted),
            Profile.ActiveRun.BattleDeployHeroIds,
            Profile.ActiveRun.IsQuickBattle,
            string.IsNullOrWhiteSpace(Profile.ActiveRun.LastBattleMatchId) ? null : Profile.ActiveRun.LastBattleMatchId,
            Profile.ActiveRun.LastSettlementWasVictory,
            Profile.ActiveRun.StoryCleared,
            Profile.ActiveRun.EndlessUnlocked,
            Profile.ActiveRun.EndlessCycleIndex,
            Profile.ActiveRun.ActiveWoundHeroIds);
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

    /// <summary>
    /// task-reward-settlement-commit-v1 acceptance #3: RewardCommitId 기반 dedup. 같은 commitId가
    /// 이미 RewardLedger에 reward_choice entry로 기록돼 있으면 두 번째 commit은 mutation 없이 통과.
    /// </summary>
    private bool HasRecordedRewardSettlementByCommitId(string commitId)
    {
        return !string.IsNullOrWhiteSpace(commitId)
               && Profile.RewardLedger.Any(entry =>
                   string.Equals(entry.CommitId, commitId, StringComparison.Ordinal)
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
            ActiveWoundHeroIds = (ActiveRun.ActiveWoundHeroIds ?? Array.Empty<string>()).ToList(),
            ResolvedExpeditionNodeIds = _resolvedExpeditionNodeIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
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
            RewardCommitId = ActiveRun.Overlay.RewardCommitId,
            PledgedWarrantId = ActiveRun.Overlay.PledgedWarrantId,
            FirstSelectedTemporaryAugmentId = ActiveRun.Overlay.FirstSelectedTemporaryAugmentId,
            PendingPermanentUnlockId = ActiveRun.Overlay.PendingPermanentUnlockId,
            RewardedRevisitIndex = ActiveRun.Overlay.RewardedRevisitIndex,
            RevisitItemRollsGranted = ActiveRun.Overlay.RevisitItemRollsGranted,
            RevisitCurrencyGranted = ActiveRun.Overlay.RevisitCurrencyGranted,
            StoryCleared = ActiveRun.StoryCleared,
            EndlessUnlocked = ActiveRun.EndlessUnlocked,
            EndlessCycleIndex = ActiveRun.EndlessCycleIndex,
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

        if (ActiveRun == null)
        {
            Profile.ActiveRun ??= new ActiveRunRecord();
            Profile.ActiveRun.RecruitPhase = _recruitPhaseState.Clone();
            Profile.ActiveRun.RecruitPity = _recruitPityState.Clone();
            return;
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
            Profile.Heroes.ToDictionary(hero => hero.HeroId, hero => ResolveBlueprintRoleInstructionId(hero.HeroId, hero.ClassId, ResolvePreferredAnchor(hero.HeroId)), StringComparer.Ordinal),
            _heroTargetDirectives.ToDictionary(
                pair => pair.Key,
                pair => PlayerTargetDirectiveRules.ToStableId(pair.Value),
                StringComparer.Ordinal));

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
        record.HeroTargetDirectives = (blueprint.HeroTargetDirectives ?? new Dictionary<string, string>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        record.DeploymentUserAuthored = _deploymentUserAuthored;
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
            record.HeroRoleIds,
            record.HeroTargetDirectives);
    }

    // 유저가 명시 편성한 출전 배치를 active blueprint record에서 live _deploymentAssignments로 복원한다.
    // record.DeploymentUserAuthored == true일 때만 복원 — 자동배치 결과(false)는 복원 대상이 아니다.
    // squad에 없는 hero는 건너뛴다(reconcile과 일관). 복원 시 _deploymentUserAuthored를 세워 후속
    // EnsureBattleDeployReady의 자동 채움이 복원된(또는 유저가 의도적으로 비운) 배치를 덮지 않게 한다.
    private void RestoreDeploymentFromActiveBlueprint()
    {
        if (string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId))
        {
            return;
        }

        var record = Profile.SquadBlueprints.FirstOrDefault(existing => existing.BlueprintId == Profile.ActiveBlueprintId);
        if (record is not { DeploymentUserAuthored: true })
        {
            return;
        }

        EnsureAssignmentMapInitialized();
        foreach (var pair in record.DeploymentAssignments)
        {
            if (string.IsNullOrWhiteSpace(pair.Value)
                || !Enum.TryParse<DeploymentAnchorId>(pair.Key, out var anchor)
                || !_expeditionSquadHeroIds.Contains(pair.Value))
            {
                continue;
            }

            _deploymentAssignments[anchor] = pair.Value;
        }

        _deploymentUserAuthored = true;
    }

    // P1 유닛별 타겟 지시 복원 — 배치와 달리 user-authored 플래그가 없다(지시 존재 자체가 사용자 의도).
    private void RestoreHeroTargetDirectivesFromActiveBlueprint()
    {
        if (string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId))
        {
            return;
        }

        var record = Profile.SquadBlueprints.FirstOrDefault(existing => existing.BlueprintId == Profile.ActiveBlueprintId);
        if (record?.HeroTargetDirectives is not { Count: > 0 })
        {
            return;
        }

        _heroTargetDirectives.Clear();
        foreach (var pair in record.HeroTargetDirectives)
        {
            var directive = PlayerTargetDirectiveRules.ParseStableId(pair.Value);
            if (directive != PlayerTargetDirective.Default)
            {
                _heroTargetDirectives[pair.Key] = directive;
            }
        }
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
                record.EquippedHeroId,
                record.RolledRarityTier >= 0
                    ? (ItemRarityTierValue)record.RolledRarityTier
                    : null,
                ToAffixMagnitudeMap(record)),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, float> ToAffixMagnitudeMap(InventoryItemRecord item)
    {
        return (item.AffixMagnitudeRolls ?? new List<InventoryAffixMagnitudeRecord>())
            .Where(roll => roll != null
                           && !string.IsNullOrWhiteSpace(roll.AffixId)
                           && !float.IsNaN(roll.Magnitude)
                           && !float.IsInfinity(roll.Magnitude))
            .GroupBy(roll => roll.AffixId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Magnitude, StringComparer.Ordinal);
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
