using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>campaign observation hook을 immutable sunken arrival/lookback capture로 투영한다.</summary>
internal sealed class H100SunkenCaptureCollector
{
    private readonly RuntimeCombatContentLookup _lookup;
    private readonly string _runId;
    private readonly string _policyId;
    private readonly string _targetSiteId;
    private readonly int _targetArrivalCount;
    private readonly Dictionary<string, H100SunkenLookbackCheckpoint> _lastRewardByCampaign =
        new(StringComparer.Ordinal);
    private readonly List<H100SunkenCapturedArrival> _arrivals = new();

    public H100SunkenCaptureCollector(
        RuntimeCombatContentLookup lookup,
        string runId,
        string policyId,
        string targetSiteId,
        int targetArrivalCount)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _runId = runId ?? string.Empty;
        _policyId = policyId ?? string.Empty;
        _targetSiteId = targetSiteId ?? string.Empty;
        _targetArrivalCount = Math.Max(1, targetArrivalCount);
        Hooks = new H100CampaignObservationHooks(
            OnSiteArrived,
            OnRewardOffered,
            OnRewardChosen,
            () => _arrivals.Count >= _targetArrivalCount);
    }

    public H100CampaignObservationHooks Hooks { get; }

    public IReadOnlyList<H100SunkenCapturedArrival> Arrivals => _arrivals;

    private void OnRewardOffered(H100RewardOfferedContext context)
    {
        var session = context.Session;
        _lastRewardByCampaign[context.CampaignId] = new H100SunkenLookbackCheckpoint(
            context.CampaignId,
            session.SelectedCampaignSiteId,
            H100ProfileSnapshotCodec.Capture(session.Profile),
            session.PendingRewardChoices.Select((option, index) => new SunkenArrivalSnapshotRecord.RewardOption
            {
                OptionIndex = index,
                Kind = option.Kind.ToString(),
                PayloadId = option.PayloadId,
                GoldAmount = option.GoldAmount,
                EchoAmount = option.EchoAmount,
            }).ToArray(),
            session.RecruitOffers.Select((offer, index) => new SunkenArrivalSnapshotRecord.RecruitOffer
            {
                OfferIndex = index,
                ArchetypeId = offer.UnitBlueprintId,
                Tier = offer.Metadata.Tier.ToString(),
                GoldCost = offer.Metadata.GoldCost,
            }).ToArray(),
            ChosenOptionIndex: -1,
            ChosenPayloadId: string.Empty);
    }

    private void OnRewardChosen(H100RewardChosenContext context)
    {
        if (!_lastRewardByCampaign.TryGetValue(context.CampaignId, out var checkpoint))
        {
            return;
        }

        var payloadId = checkpoint.RewardOptions
            .FirstOrDefault(value => value.OptionIndex == context.Decision.OptionIndex)?.PayloadId ?? string.Empty;
        _lastRewardByCampaign[context.CampaignId] = checkpoint with
        {
            ChosenOptionIndex = context.Decision.OptionIndex,
            ChosenPayloadId = payloadId,
        };
    }

    private void OnSiteArrived(H100SiteArrivalContext context)
    {
        var session = context.Session;
        if (!string.Equals(session.SelectedCampaignSiteId, _targetSiteId, StringComparison.Ordinal))
        {
            return;
        }

        if (!_lookup.TryGetCombatSnapshot(out var combatSnapshot, out var contentError))
        {
            throw new InvalidOperationException($"Cannot capture sunken arrival: {contentError}");
        }

        var observation = H100PolicyObservationBuilder.Build(session, _lookup, context.DecisionSeed);
        var progressionByHero = session.Profile.HeroProgressions
            .Where(value => !string.IsNullOrWhiteSpace(value.HeroId))
            .GroupBy(value => value.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var squad = session.ExpeditionSquadHeroIds.ToHashSet(StringComparer.Ordinal);
        var inventory = session.Profile.Inventory ?? new List<SM.Persistence.Abstractions.Models.InventoryItemRecord>();
        _lastRewardByCampaign.TryGetValue(context.CampaignId, out var lookback);
        var snapshot = new SunkenArrivalSnapshotRecord
        {
            RunId = _runId,
            SampleId = $"{_policyId}-{context.CampaignId}-{context.CampaignSeed:D10}",
            PolicyId = _policyId,
            CampaignSeed = context.CampaignSeed,
            SiteIndex = context.SiteIndex,
            BattleStartIndex = context.BattleStartIndex,
            ChapterId = session.SelectedCampaignChapterId,
            SiteId = session.SelectedCampaignSiteId,
            Gold = session.Profile.Currencies.Gold,
            Echo = session.Profile.Currencies.Echo,
            OwnedArchetypeIds = session.Profile.Heroes
                .Select(value => value.ArchetypeId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            ExpeditionSquadHeroIds = session.ExpeditionSquadHeroIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Roster = session.Profile.Heroes
                .OrderBy(value => value.HeroId, StringComparer.Ordinal)
                .Select(hero =>
                {
                    combatSnapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype);
                    var itemCount = (hero.EquippedItemIds ?? new List<string>())
                        .Concat(inventory.Where(item => item.EquippedHeroId == hero.HeroId)
                            .Select(item => item.ItemInstanceId))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    return new SunkenArrivalSnapshotRecord.RosterHero
                    {
                        HeroId = hero.HeroId,
                        ArchetypeId = hero.ArchetypeId,
                        RaceId = hero.RaceId,
                        ClassId = hero.ClassId,
                        RoleTag = archetype?.RoleTag ?? string.Empty,
                        Level = progressionByHero.TryGetValue(hero.HeroId, out var progression) ? progression.Level : 1,
                        CurrentHp = hero.CurrentHp,
                        MaxHp = hero.MaxHp,
                        EquippedItemCount = itemCount,
                        InExpeditionSquad = squad.Contains(hero.HeroId),
                    };
                }).ToArray(),
            ChosenPlacements = context.Decision.Placements
                .OrderBy(value => value.Anchor)
                .Select(value => new SunkenArrivalSnapshotRecord.Placement
                {
                    AnchorId = (int)value.Anchor,
                    HeroId = value.HeroId,
                }).ToArray(),
            ChosenRationale = context.Decision.Rationale,
            ChosenEstimatedValue = context.Decision.EstimatedValue,
            CurrentEncounterId = observation.EnemyPreview.EncounterId,
            CurrentEnemyArchetypeIds = observation.EnemyPreview.Units
                .Select(value => value.ArchetypeId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            PreviousSiteId = lookback?.SiteId ?? string.Empty,
            PreviousRewardOptionIndex = lookback?.ChosenOptionIndex ?? -1,
            PreviousRewardPayloadId = lookback?.ChosenPayloadId ?? string.Empty,
            PreviousRewardOptions = lookback?.RewardOptions ?? Array.Empty<SunkenArrivalSnapshotRecord.RewardOption>(),
            PreviousRecruitOffers = lookback?.RecruitOffers ?? Array.Empty<SunkenArrivalSnapshotRecord.RecruitOffer>(),
        };

        _arrivals.Add(new H100SunkenCapturedArrival(
            snapshot,
            H100ProfileSnapshotCodec.Capture(session.Profile),
            context.Decision,
            context.CampaignSeed,
            context.BattleStartIndex,
            lookback));
    }
}
