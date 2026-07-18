using System;
using System.Collections.Generic;
using System.Globalization;
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
    internal sealed class SessionRecruitmentFlow
    {
        private readonly GameSessionState _session;

        internal SessionRecruitmentFlow(GameSessionState session)
        {
            _session = session;
        }

        internal Result RerollRecruitOffers() => _session.RerollRecruitOffersCore();

        internal Result Recruit(int offerIndex) => _session.RecruitCore(offerIndex);

        internal Result UseScout(ScoutDirective directive) => _session.UseScoutCore(directive);

        internal Result RetrainHero(string heroId, RetrainOperationKind operation) =>
            _session.RetrainHeroCore(heroId, operation);

        internal Result DismissHero(string heroId) => _session.DismissHeroCore(heroId);

        internal Result GrantHeroDirect(string archetypeId, RecruitOfferSource source) =>
            _session.GrantHeroDirectCore(archetypeId, source);
    }

    // recruit 책임 축 transactional 본체 — public facade(GameSessionState.cs)는 _recruitmentFlow로
    // 위임만 하고, 실제 통화 검증/telemetry/state sync는 여기 *Core 메서드가 소유한다.
    private Result RerollRecruitOffersCore()
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Refresh는 Town에서만 사용할 수 있습니다.");
        }

        var refreshCost = RefreshCostService.GetRefreshCost(_recruitPhaseState);
        if (refreshCost > 0 && Profile.Currencies.Gold < refreshCost)
        {
            return Result.Fail($"Gold가 부족합니다. refresh에는 {refreshCost} Gold가 필요합니다.");
        }

        Profile.Currencies.Gold -= refreshCost;
        _recruitPhaseState = RefreshCostService.ConsumeRefresh(_recruitPhaseState);
        _recruitOfferGeneration += 1;
        _recruitOffers.Clear();
        EnsureRecruitOffers();
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRecruitRefreshed(
            ResolveTelemetryRunId(),
            refreshCost,
            _recruitPhaseState.PaidRefreshCountThisPhase));
        SyncRecruitState();
        return Result.Success();
    }

    private Result RecruitCore(int offerIndex)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Recruit는 Town에서만 사용할 수 있습니다.");
        }

        if (offerIndex < 0 || offerIndex >= _recruitOffers.Count)
        {
            return Result.Fail("유효하지 않은 영입 후보입니다.");
        }

        var offer = _recruitOffers[offerIndex];
        if (Profile.Currencies.Gold < offer.Metadata.GoldCost)
        {
            return Result.Fail($"Gold가 부족합니다. 영입에는 {offer.Metadata.GoldCost} Gold가 필요합니다.");
        }

        if (Profile.Heroes.Count >= MetaBalanceDefaults.TownRosterCap)
        {
            return Result.Fail($"Town roster cap {MetaBalanceDefaults.TownRosterCap}에 도달했습니다.");
        }

        if (!TryGrantRecruitPreview(offer, RecruitOfferSource.RecruitPhase, out _, out var error))
        {
            return Result.Fail(error);
        }

        Profile.Currencies.Gold -= offer.Metadata.GoldCost;
        _recruitOffers.RemoveAt(offerIndex);
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRecruitPurchased(
            ResolveTelemetryRunId(),
            offer,
            offerIndex));
        SyncRecruitState();
        return Result.Success();
    }

    private Result UseScoutCore(ScoutDirective directive)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Scout는 Town에서만 사용할 수 있습니다.");
        }

        directive ??= new ScoutDirective();
        if (directive.IsNone)
        {
            return Result.Fail("Scout directive가 필요합니다.");
        }

        if (_recruitPhaseState.ScoutUsedThisPhase)
        {
            return Result.Fail("이번 recruit phase에서는 이미 scout를 사용했습니다.");
        }

        if (Profile.Currencies.Echo < RecruitmentBalanceCatalog.ScoutEchoCost)
        {
            return Result.Fail($"잔향이 부족합니다. 정찰에는 {RecruitmentBalanceCatalog.ScoutEchoCost} 잔향이 필요합니다.");
        }

        Profile.Currencies.Echo -= RecruitmentBalanceCatalog.ScoutEchoCost;
        _recruitPhaseState.ScoutUsedThisPhase = true;
        _recruitPhaseState.PendingScoutDirective = directive.Clone();
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateScoutUsed(
            ResolveTelemetryRunId(),
            directive,
            RecruitmentBalanceCatalog.ScoutEchoCost));
        SyncRecruitState();
        return Result.Success();
    }

    private Result RetrainHeroCore(string heroId, RetrainOperationKind operation)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Retrain은 Town에서만 사용할 수 있습니다.");
        }

        if (!TryGetHero(heroId, out var hero))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        if (!_combatContentLookup.Snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype))
        {
            return Result.Fail($"Archetype '{hero.ArchetypeId}'를 찾을 수 없습니다.");
        }

        var currentFlexActiveId = ResolveHeroFlexActiveId(hero, archetype);
        var currentFlexPassiveId = ResolveHeroFlexPassiveId(hero, archetype);
        var retrainState = hero.RetrainState?.Clone() ?? new UnitRetrainState();
        var cost = RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(operation, retrainState);
        if (Profile.Currencies.Echo < cost)
        {
            return Result.Fail($"잔향이 부족합니다. 재훈련에는 {cost} 잔향이 필요합니다.");
        }

        var result = RetrainService.Retrain(
            archetype,
            currentFlexActiveId,
            currentFlexPassiveId,
            operation,
            retrainState,
            BuildTeamPlanProfile(),
            RecruitmentBalanceCatalog.DefaultRetrainCosts,
            BuildStableSeed(heroId, retrainState.RetrainCount + (int)operation + _recruitOfferGeneration));

        Profile.Currencies.Echo -= result.EchoCost;
        hero.FlexActiveId = result.FlexActiveId;
        hero.FlexPassiveId = result.FlexPassiveId;
        hero.RetrainState = result.RetrainState;
        hero.EconomyFootprint ??= new UnitEconomyFootprint();
        hero.EconomyFootprint.RetrainEchoPaid += result.EchoCost;
        Roster = new RosterState(ToHeroRecords(Profile));
        SyncHeroBuildState(hero);
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRetrainPerformed(
            ResolveTelemetryRunId(),
            hero.HeroId,
            hero.ArchetypeId,
            operation,
            result));
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    private Result DismissHeroCore(string heroId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Dismiss는 Town에서만 사용할 수 있습니다.");
        }

        if (Profile.Heroes.Count <= 1)
        {
            return Result.Fail("마지막 roster unit은 dismiss할 수 없습니다.");
        }

        if (!TryGetHero(heroId, out var hero))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        var refund = DismissService.CalculateRefund(hero.EconomyFootprint ?? new UnitEconomyFootprint());
        Profile.Currencies.Gold += refund.GoldRefund;
        Profile.Currencies.Echo += refund.EchoRefund;
        UnequipHeroItems(hero.HeroId);
        RemoveHeroFromRoster(hero.HeroId);
        Roster = new RosterState(ToHeroRecords(Profile));
        _recruitOffers.RemoveAll(offer => string.Equals(offer.UnitBlueprintId, hero.ArchetypeId, StringComparison.Ordinal));
        EnsureRecruitOffers();
        SyncRecruitState();
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    private Result GrantHeroDirectCore(string archetypeId, RecruitOfferSource source = RecruitOfferSource.DirectGrant)
    {
        if (!_combatContentLookup.Snapshot.Archetypes.TryGetValue(archetypeId, out var template))
        {
            return Result.Fail($"Archetype '{archetypeId}'를 찾을 수 없습니다.");
        }

        var preview = RecruitPreviewBuilder.Roll(
            template,
            BuildTeamPlanProfile(),
            null,
            FlexRollBiasMode.NativeBiased,
            BuildStableSeed(archetypeId, Profile.Heroes.Count + _recruitOfferGeneration));
        var directPreview = new RecruitUnitPreview
        {
            UnitBlueprintId = archetypeId,
            UnitInstanceSeed = $"grant:{source}:{archetypeId}:{Profile.Heroes.Count}",
            FlexActiveId = preview.FlexActiveId,
            FlexPassiveId = preview.FlexPassiveId,
            Metadata = new RecruitOfferMetadata
            {
                SlotType = RecruitOfferSlotType.StandardA,
                Tier = template.RecruitTier,
                GoldCost = RecruitmentBalanceCatalog.DefaultRecruitTierCosts.GetCost(template.RecruitTier),
            }
        };

        return TryGrantRecruitPreview(directPreview, source, out _, out var error)
            ? Result.Success()
            : Result.Fail(error);
    }

    private void EnsureRecruitOffers()
    {
        if (_recruitOffers.Count > 0)
        {
            return;
        }

        var snapshot = _combatContentLookup.Snapshot;
        if (snapshot.Archetypes.Count == 0)
        {
            return;
        }

        var result = RecruitPackGenerator.GeneratePack(
            snapshot.Archetypes,
            snapshot,
            ToHeroRecords(Profile).ToList(),
            ActiveRun?.Overlay.TemporaryAugmentIds ?? Array.Empty<string>(),
            ToPermanentAugmentLoadout(Profile, string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId) ? "blueprint.default" : Profile.ActiveBlueprintId).EquippedAugmentIds,
            _recruitPityState.Clone(),
            _recruitPhaseState.Clone(),
            BuildStableSeed("recruit-pack", _recruitOfferGeneration + Profile.Heroes.Count));
        _recruitOffers.Clear();
        _recruitOffers.AddRange(result.Offers);
        _recruitPityState = result.UpdatedPity;
        _recruitPhaseState = result.UpdatedPhase;
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRecruitPackGenerated(
            ResolveTelemetryRunId(),
            BuildStableSeed("recruit-pack", _recruitOfferGeneration + Profile.Heroes.Count),
            result.Offers.Count,
            _recruitPhaseState));
        SyncRecruitState();
    }

    private void ResetRecruitPhaseForTownEntry()
    {
        _recruitPhaseState = new RecruitPhaseState();
        _recruitOfferGeneration = 0;
        _recruitOffers.Clear();
        EnsureRecruitOffers();
        SyncRecruitState();
    }

    private TeamPlanProfile BuildTeamPlanProfile()
    {
        var snapshot = _combatContentLookup.Snapshot;
        var permanentAugments = ToPermanentAugmentLoadout(
                Profile,
                string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId) ? "blueprint.default" : Profile.ActiveBlueprintId)
            .EquippedAugmentIds;
        return TeamPlanEvaluator.Evaluate(
            ToHeroRecords(Profile).ToList(),
            snapshot.Archetypes,
            snapshot,
            ActiveRun?.Overlay.TemporaryAugmentIds ?? Array.Empty<string>(),
            permanentAugments);
    }

    private bool IsTownEconomyPhase()
    {
        return string.Equals(CurrentSceneName, SceneNames.Town, StringComparison.Ordinal);
    }

    private bool TryGrantRecruitPreview(
        RecruitUnitPreview preview,
        RecruitOfferSource source,
        out DuplicateConversionResult? duplicateResult,
        out string error)
    {
        duplicateResult = null;
        error = string.Empty;
        if (!_combatContentLookup.TryGetArchetype(preview.UnitBlueprintId, out var archetype))
        {
            error = $"Archetype '{preview.UnitBlueprintId}'를 찾을 수 없습니다.";
            return false;
        }

        if (DuplicateResolver.TryResolveDuplicate(
                Profile.Heroes.Any(hero => string.Equals(hero.ArchetypeId, preview.UnitBlueprintId, StringComparison.Ordinal)),
                preview.Metadata.Tier,
                RecruitmentBalanceCatalog.DefaultDuplicateEchoValues,
                out var duplicate))
        {
            Profile.Currencies.Echo += duplicate.EchoGranted;
            duplicateResult = duplicate;
            _lastDuplicateConversion = duplicate;
            AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateDuplicateConverted(
                ResolveTelemetryRunId(),
                preview,
                duplicate));
            SyncRecruitState();
            return true;
        }

        Profile.HeroInstanceCounter = checked(Profile.HeroInstanceCounter + 1L);
        var heroId = $"hero-c{Profile.HeroInstanceCounter.ToString(CultureInfo.InvariantCulture)}";
        Profile.Heroes.Add(new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = ResolveArchetypeDisplayName(archetype),
            ArchetypeId = preview.UnitBlueprintId,
            RaceId = archetype.Race.Id,
            ClassId = archetype.Class.Id,
            PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(preview.UnitBlueprintId, string.Empty, Profile.Heroes.Count),
            NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(preview.UnitBlueprintId, string.Empty, Profile.Heroes.Count + 1),
            FlexActiveId = preview.FlexActiveId,
            FlexPassiveId = preview.FlexPassiveId,
            RecruitTier = preview.Metadata.Tier,
            RecruitSource = source,
            DominantHand = DominantHandDistributionService.ResolveGenerated(heroId, archetype.Class.Id),
            RetrainState = new UnitRetrainState(),
            EconomyFootprint = new UnitEconomyFootprint
            {
                RecruitGoldPaid = source == RecruitOfferSource.RecruitPhase ? preview.Metadata.GoldCost : 0,
            },
            EquippedItemIds = new List<string>(),
        });

        Roster = new RosterState(ToHeroRecords(Profile));
        EnsureProfileBuildState();
        _lastDuplicateConversion = null;
        SyncActiveRunIfPresent();
        return true;
    }

    private bool TryGetHero(string heroId, out HeroInstanceRecord hero)
    {
        hero = Profile.Heroes.FirstOrDefault(entry => entry.HeroId == heroId)!;
        return hero != null;
    }

    // 프로세스 안정 시드. HashCode.Combine은 .NET/Mono에서 프로세스마다 Marvin 시드가 randomize되어
    // 같은 입력도 프로세스마다 다른 값을 낸다 — 이름과 달리 "Stable"이 아니다. 데모/영입 아이템 어픽스 선택
    // (SessionInventoryItemBuilder.BuildGeneratedAffixIds의 new Random(seed))과 영입 풀 생성이 이 시드를 쓰므로,
    // 프로세스마다 분대 스탯이 달라져 같은 시드·분대인데도 전투 결과가 갈렸다(헤드리스 캠페인 런간 W/L 변동).
    // 코드베이스 공용 FNV 패턴(StableHash: hash*31+ch)으로 유도해 프로세스 간 결정성을 복원한다.
    internal static int BuildStableSeed(string value, int salt)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value ?? string.Empty)
            {
                hash = (hash * 31) + ch;
            }

            hash = (hash * 31) + salt;
            return hash & int.MaxValue;
        }
    }

    public void ClearRuntimeTelemetry() => _profileSync.ClearRuntimeTelemetry();

    private void AppendRuntimeTelemetry(TelemetryEventRecord record)
    {
        if (record == null)
        {
            return;
        }

        record.TimeSeconds = _runtimeTelemetryEvents.Count;
        _runtimeTelemetryEvents.Add(record);
    }

    internal void RecordOperationalTelemetry(TelemetryEventRecord record) => _profileSync.RecordOperationalTelemetry(record);

    private string ResolveTelemetryRunId()
    {
        return ActiveRun?.RunId
               ?? Profile.ActiveRun?.RunId
               ?? (IsQuickBattleSmokeActive ? "quick-battle" : GetExpeditionRunId());
    }

    private static string ResolveHeroFlexActiveId(HeroInstanceRecord hero, CombatArchetypeTemplate archetype)
    {
        return string.IsNullOrWhiteSpace(hero.FlexActiveId)
            ? archetype.FlexActive?.Id ?? string.Empty
            : hero.FlexActiveId;
    }

    private static string ResolveHeroFlexPassiveId(HeroInstanceRecord hero, CombatArchetypeTemplate archetype)
    {
        return string.IsNullOrWhiteSpace(hero.FlexPassiveId)
            ? archetype.FlexPassive?.Id ?? string.Empty
            : hero.FlexPassiveId;
    }

    private void SyncHeroBuildState(HeroInstanceRecord hero)
    {
        var loadout = Profile.HeroLoadouts.FirstOrDefault(record => record.HeroId == hero.HeroId);
        if (loadout == null)
        {
            Profile.HeroLoadouts.Add(new HeroLoadoutRecord
            {
                HeroId = hero.HeroId,
                EquippedItemInstanceIds = hero.EquippedItemIds.ToList(),
            });
        }
        else
        {
            loadout.EquippedItemInstanceIds = hero.EquippedItemIds.ToList();
        }
    }

    private void UnequipHeroItems(string heroId)
    {
        foreach (var inventoryItem in Profile.Inventory.Where(item => string.Equals(item.EquippedHeroId, heroId, StringComparison.Ordinal)))
        {
            inventoryItem.EquippedHeroId = string.Empty;
        }

        var hero = Profile.Heroes.FirstOrDefault(entry => entry.HeroId == heroId);
        if (hero != null)
        {
            hero.EquippedItemIds = new List<string>();
        }
    }

    private void RemoveHeroFromRoster(string heroId)
    {
        var removedLoadout = Profile.HeroLoadouts.FirstOrDefault(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        if (removedLoadout != null)
        {
            var removedSkillIds = removedLoadout.EquippedSkillInstanceIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            Profile.SkillInstances.RemoveAll(record => removedSkillIds.Contains(record.SkillInstanceId));
        }

        Profile.Heroes.RemoveAll(hero => string.Equals(hero.HeroId, heroId, StringComparison.Ordinal));
        Profile.HeroLoadouts.RemoveAll(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        Profile.HeroProgressions.RemoveAll(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        Profile.PassiveSelections.RemoveAll(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        _expeditionSquadHeroIds.RemoveAll(id => string.Equals(id, heroId, StringComparison.Ordinal));
        ClearDeploymentForHero(heroId);
        EnsureBattleDeployReady();
    }

    private void EnsureDefaultSquad()
    {
        if (_expeditionSquadHeroIds.Count > 0)
        {
            return;
        }

        foreach (var hero in Profile.Heroes.Take(MetaBalanceDefaults.ExpeditionSquadCap))
        {
            _expeditionSquadHeroIds.Add(hero.HeroId);
        }
    }

    private void EnsureAssignmentMapInitialized()
    {
        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (!_deploymentAssignments.ContainsKey(anchor))
            {
                _deploymentAssignments[anchor] = null;
            }
        }
    }

    private void ResetDeploymentAssignments()
    {
        _deploymentAssignments.Clear();
        _deploymentUserAuthored = false;
        EnsureAssignmentMapInitialized();
    }

    // squad에서 빠진 hero의 유령 배치를 정리한다. 유저 편성 의도와 무관하게 항상 안전하게 실행 가능 —
    // 자동 채움(AutoFill)과 분리해, 출전 편성을 필수 게이트로 만들 때 "정리는 항상 / 채움은 fallback"을 구현한다.
    private void ReconcileDeploymentWithSquad()
    {
        EnsureAssignmentMapInitialized();

        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (_deploymentAssignments.TryGetValue(anchor, out var heroId) && !string.IsNullOrWhiteSpace(heroId) && !_expeditionSquadHeroIds.Contains(heroId))
            {
                _deploymentAssignments[anchor] = null;
            }
        }

        foreach (var heroId in BattleDeployHeroIds.Where(heroId => !_expeditionSquadHeroIds.Contains(heroId)).ToList())
        {
            ClearDeploymentForHero(heroId);
        }
    }

    // 빈 anchor를 원정대 앞쪽 hero로 자동 채운다. 유저 미편성 상태의 fallback 전용 —
    // markUserAuthored: false로 배치하므로 이 자동배치는 "유저가 편성했다"는 신호를 남기지 않는다.
    private void AutoFillDeploymentFromSquad()
    {
        EnsureAssignmentMapInitialized();

        foreach (var heroId in _expeditionSquadHeroIds.Take(MetaBalanceDefaults.BattleDeployCap))
        {
            if (BattleDeployHeroIds.Contains(heroId))
            {
                continue;
            }

            AssignHeroToAnchorCore(ResolvePreferredAnchor(heroId), heroId, markUserAuthored: false);
            if (BattleDeployHeroIds.Count >= MetaBalanceDefaults.BattleDeployCap)
            {
                break;
            }
        }
    }

    private void ClearDeploymentForHero(string heroId)
    {
        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (_deploymentAssignments.TryGetValue(anchor, out var assignedHero) && assignedHero == heroId)
            {
                _deploymentAssignments[anchor] = null;
            }
        }
    }

    private DeploymentAnchorId ResolvePreferredAnchor(string heroId)
    {
        var hero = Profile.Heroes.FirstOrDefault(entry => entry.HeroId == heroId);
        var preferredOrder = hero?.ClassId switch
        {
            "vanguard" => new[]
            {
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom
            },
            "duelist" => new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.BackCenter
            },
            "ranger" => new[]
            {
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom
            },
            "mystic" => new[]
            {
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom
            },
            _ => DeploymentAnchorOrder
        };

        foreach (var anchor in preferredOrder)
        {
            if (string.IsNullOrWhiteSpace(GetAssignedHeroId(anchor)))
            {
                return anchor;
            }
        }

        return preferredOrder[0];
    }
}
