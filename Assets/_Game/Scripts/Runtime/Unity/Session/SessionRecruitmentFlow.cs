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

        var heroId = $"hero-{Guid.NewGuid():N}";
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

    private static int BuildStableSeed(string value, int salt)
    {
        return Math.Abs(HashCode.Combine(value, salt));
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
        EnsureAssignmentMapInitialized();
    }

    private void EnsureDefaultDeploymentAssignments()
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

        foreach (var heroId in _expeditionSquadHeroIds.Take(MetaBalanceDefaults.BattleDeployCap))
        {
            if (BattleDeployHeroIds.Contains(heroId))
            {
                continue;
            }

            AssignHeroToAnchor(ResolvePreferredAnchor(heroId), heroId);
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
