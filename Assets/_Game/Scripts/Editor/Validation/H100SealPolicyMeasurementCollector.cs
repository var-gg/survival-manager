using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>
/// Observes campaign/refit state without changing the production execution path.
/// One collector is scoped to one arm and retains raw refit-window rows.
/// </summary>
internal sealed class H100SealPolicyMeasurementCollector
{
    private readonly CombatContentSnapshot _snapshot;
    private readonly int _seedBase;
    private readonly Dictionary<GameSessionState, CampaignIdentity> _campaignBySession =
        new(ReferenceComparer<GameSessionState>.Instance);
    private readonly Dictionary<GameSessionState, Dictionary<int, MutableWindow>> _pendingBySession =
        new(ReferenceComparer<GameSessionState>.Instance);
    private readonly Dictionary<int, GameSessionState> _lastSessionByCampaign = new();
    private readonly Dictionary<int, int> _windowCountByCampaign = new();
    private readonly List<MutableWindow> _windows = new();
    private readonly List<H100SealCraftingOperationRecord> _operations = new();
    private HeadlessRosterPolicyObservation? _originalRosterObservation;
    private int _skipCount;

    public H100SealPolicyMeasurementCollector(
        CombatContentSnapshot snapshot,
        int seedBase)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _seedBase = seedBase;
    }

    public H100CampaignObservationHooks CreateHooks(
        Func<HeadlessRosterPolicyObservation, HeadlessRosterPolicyObservation>? transform = null)
    {
        Func<HeadlessRosterPolicyObservation, HeadlessRosterPolicyObservation>?
            capturingTransform = transform == null
                ? null
                : observation => CaptureAndTransform(observation, transform);
        return new H100CampaignObservationHooks(
            SiteArrived: context => Track(
                context.CampaignIndex,
                context.CampaignSeed,
                context.Session),
            RewardOffered: context => Track(
                context.CampaignIndex,
                context.CampaignSeed,
                context.Session),
            RewardChosen: context => Track(
                context.CampaignIndex,
                context.CampaignSeed,
                context.Session),
            DeploymentOffered: context => Track(
                context.CampaignIndex,
                context.CampaignSeed,
                context.Session),
            RosterDecisionOffered: OnRosterDecisionOffered,
            DecisionApplied: OnDecisionApplied,
            PrepOffered: context => Track(
                context.CampaignIndex,
                context.CampaignSeed,
                context.Session),
            RosterObservationTransform: capturingTransform);
    }

    public H100SealPolicyArmReport BuildReport(
        string armId,
        H100SealPolicyCalibration? calibration,
        H100CampaignCorpusRunner.Corpus corpus)
    {
        if (corpus == null)
        {
            throw new ArgumentNullException(nameof(corpus));
        }

        var campaigns = corpus.Campaigns
            .OrderBy(value => value.CampaignId, StringComparer.Ordinal)
            .ToArray();
        var terminalCampaigns = campaigns.Select((campaign, index) =>
        {
            var campaignIndex = ParseCampaignIndex(campaign.CampaignId, index);
            if (!_lastSessionByCampaign.TryGetValue(campaignIndex, out var session))
            {
                return new H100SealCampaignTerminalRecord(
                    campaignIndex,
                    _seedBase + campaignIndex,
                    campaign.Seed,
                    false,
                    null,
                    null,
                    null,
                    0);
            }

            var (meanQuality, affixCount) = MeasureInventory(session.Profile.Inventory);
            return new H100SealCampaignTerminalRecord(
                campaignIndex,
                _seedBase + campaignIndex,
                campaign.Seed,
                true,
                session.Profile.Currencies.Gold,
                session.Profile.Currencies.Echo,
                meanQuality,
                affixCount);
        }).ToArray();
        var records = _windows
            .OrderBy(value => value.CampaignIndex)
            .ThenBy(value => value.WindowOrdinal)
            .Select(value => value.ToRecord())
            .ToArray();
        var sealCount = _operations.Count(value =>
            string.Equals(value.Operation, "seal", StringComparison.Ordinal));
        var campaignsWithSeal = _operations
            .Where(value => string.Equals(value.Operation, "seal", StringComparison.Ordinal))
            .Select(value => value.CampaignIndex)
            .Distinct()
            .Count();
        return new H100SealPolicyArmReport(
            armId,
            calibration,
            records.Length,
            sealCount,
            _operations.Count - sealCount,
            _skipCount,
            campaignsWithSeal,
            _operations.Sum(value => value.EchoSpent),
            records,
            _operations
                .OrderBy(value => value.CampaignIndex)
                .ThenBy(value => value.DecisionIndex)
                .ToArray(),
            terminalCampaigns,
            campaigns,
            corpus.FactAudit);
    }

    private void OnRosterDecisionOffered(H100RosterDecisionOfferedContext context)
    {
        Track(context.CampaignIndex, context.CampaignSeed, context.Session);
        var observation = ConsumeOriginalObservation(context);
        if (!string.Equals(context.LeverId, IntentTrackLeverId.Refit, StringComparison.Ordinal))
        {
            return;
        }

        var windowOrdinal = _windowCountByCampaign.GetValueOrDefault(context.CampaignIndex);
        _windowCountByCampaign[context.CampaignIndex] = windowOrdinal + 1;
        var candidate = observation.RefitItems
            .Where(item => observation.Wallet.Echo >= item.EchoCost)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ThenBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .SelectMany(item => item.AffixSlots
                .OrderBy(slot => slot.SlotIndex)
                .Where(slot => slot.CanRefit)
                .Select(slot => new Candidate(item, slot)))
            .FirstOrDefault();
        var visibleMean = AverageOrNull(observation.RefitItems
            .SelectMany(item => item.AffixSlots)
            .Select(slot => slot.RollQuality));
        var candidateAffixes = candidate == null
            ? Array.Empty<H100SealAffixObservationRecord>()
            : BuildAffixObservations(
                context.Session,
                candidate.Item.ItemInstanceId,
                candidate.Item.AffixSlots);
        var candidateMean = AverageOrNull(candidateAffixes.Select(value => value.RollQuality));
        var costs = candidate?.Item.SealCosts
            .OrderBy(value => value.LockedAffixCount)
            .Select(value => new H100SealCostRecord(
                value.LockedAffixCount,
                value.EchoCost,
                value.EchoCost <= observation.Wallet.Echo))
            .ToArray()
            ?? Array.Empty<H100SealCostRecord>();
        var window = new MutableWindow(
            context.CampaignIndex,
            _seedBase + context.CampaignIndex,
            context.CampaignSeed,
            context.SiteIndex,
            context.DecisionIndex,
            windowOrdinal,
            observation.Wallet.Gold,
            observation.Wallet.Echo,
            observation.RefitItems.Count,
            candidate?.Item.ItemId ?? string.Empty,
            candidate?.Item.ItemInstanceId ?? string.Empty,
            candidate?.Slot.SlotIndex ?? -1,
            candidate?.Item.EchoCost ?? 0,
            candidate?.Item.AllowsSeal == true,
            candidateAffixes,
            costs,
            candidateMean,
            visibleMean);
        _windows.Add(window);
        if (!_pendingBySession.TryGetValue(context.Session, out var pending))
        {
            pending = new Dictionary<int, MutableWindow>();
            _pendingBySession.Add(context.Session, pending);
        }

        pending.Add(context.DecisionIndex, window);
    }

    private HeadlessRosterPolicyObservation CaptureAndTransform(
        HeadlessRosterPolicyObservation observation,
        Func<HeadlessRosterPolicyObservation, HeadlessRosterPolicyObservation> transform)
    {
        if (_originalRosterObservation != null)
        {
            throw new InvalidOperationException(
                "A transformed roster observation was not consumed before the next offer.");
        }

        _originalRosterObservation = observation;
        return transform(observation);
    }

    private HeadlessRosterPolicyObservation ConsumeOriginalObservation(
        H100RosterDecisionOfferedContext context)
    {
        if (_originalRosterObservation == null)
        {
            return context.Observation;
        }

        var original = _originalRosterObservation;
        _originalRosterObservation = null;
        if (original.DecisionSeed != context.DecisionSeed)
        {
            throw new InvalidOperationException(
                $"Transformed roster observation seed {original.DecisionSeed} "
                + $"does not match offered seed {context.DecisionSeed}.");
        }

        return original;
    }

    private void OnDecisionApplied(H100DecisionAppliedContext context)
    {
        if (_campaignBySession.TryGetValue(context.Session, out var identity))
        {
            Track(identity.CampaignIndex, identity.DerivedSeed, context.Session);
        }

        if (!string.Equals(context.SeamType, IntentTrackLeverId.Refit, StringComparison.Ordinal))
        {
            return;
        }

        if (!_pendingBySession.TryGetValue(context.Session, out var pending)
            || !pending.Remove(context.DecisionIndex, out var window))
        {
            throw new InvalidOperationException(
                $"Refit decision {context.DecisionIndex} has no offered measurement row.");
        }

        if (string.Equals(context.AppliedActionDescriptor, "skip", StringComparison.Ordinal))
        {
            _skipCount++;
            window.ApplySkip();
            return;
        }

        var parts = context.AppliedActionDescriptor.Split(':');
        if (parts.Length is not (2 or 3)
            || (parts.Length == 3
                && !parts[2].StartsWith("seal=", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Unexpected refit descriptor '{context.AppliedActionDescriptor}'.");
        }

        var itemInstanceId = parts[0];
        if (!string.Equals(
                itemInstanceId,
                window.CandidateItemInstanceId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Applied refit item '{itemInstanceId}' differs from candidate "
                + $"'{window.CandidateItemInstanceId}'.");
        }

        var item = context.Session.Profile.Inventory.Single(value =>
            string.Equals(value.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
        var echoSpent = window.WalletEcho - context.Session.Profile.Currencies.Echo;
        if (echoSpent < 0)
        {
            throw new InvalidOperationException(
                $"Crafting increased Echo by {-echoSpent}.");
        }

        var afterQuality = MeasureItem(item);
        var beforeQuality = window.CandidateMeanRollQuality
                            ?? throw new InvalidOperationException(
                                "Applied refit candidate has no measured affix quality.");
        var operation = parts.Length == 3 ? "seal" : "refit";
        window.Apply(operation, echoSpent, afterQuality);
        _operations.Add(new H100SealCraftingOperationRecord(
            window.CampaignIndex,
            window.LogicalSeed,
            window.DerivedCampaignSeed,
            context.DecisionIndex,
            operation,
            window.CandidateItemId,
            itemInstanceId,
            echoSpent,
            beforeQuality,
            afterQuality,
            afterQuality - beforeQuality));
    }

    private void Track(
        int campaignIndex,
        int derivedSeed,
        GameSessionState session)
    {
        _campaignBySession[session] = new CampaignIdentity(campaignIndex, derivedSeed);
        _lastSessionByCampaign[campaignIndex] = session;
    }

    private IReadOnlyList<H100SealAffixObservationRecord> BuildAffixObservations(
        GameSessionState session,
        string itemInstanceId,
        IReadOnlyList<HeadlessRefitSlotObservation> slots)
    {
        var item = session.Profile.Inventory.Single(value =>
            string.Equals(value.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
        var rolled = (item.AffixMagnitudeRolls ?? new List<InventoryAffixMagnitudeRecord>())
            .Where(value => value != null)
            .GroupBy(value => value.AffixId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Magnitude,
                StringComparer.Ordinal);
        return slots.OrderBy(value => value.SlotIndex)
            .Select(slot =>
            {
                var affixId = slot.CurrentAffix.AffixId;
                if (_snapshot.AffixCatalog == null
                    || !_snapshot.AffixCatalog.TryGetValue(affixId, out var template))
                {
                    throw new InvalidOperationException(
                        $"Measurement could not resolve affix '{affixId}'.");
                }

                var magnitude = ResolveMagnitude(affixId, rolled);
                return new H100SealAffixObservationRecord(
                    affixId,
                    slot.SlotIndex,
                    slot.RollQuality,
                    magnitude,
                    template.ValueMin,
                    template.ValueMax);
            }).ToArray();
    }

    private (double? MeanQuality, int AffixCount) MeasureInventory(
        IReadOnlyList<InventoryItemRecord> inventory)
    {
        var qualities = new List<double>();
        foreach (var item in inventory ?? Array.Empty<InventoryItemRecord>())
        {
            var affixIds = (item.AffixIds ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (affixIds.Length == 0)
            {
                continue;
            }

            var rolled = (item.AffixMagnitudeRolls ?? new List<InventoryAffixMagnitudeRecord>())
                .Where(value => value != null)
                .GroupBy(value => value.AffixId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Magnitude,
                    StringComparer.Ordinal);
            foreach (var affixId in affixIds)
            {
                qualities.Add(RefitRollQuality.Measure(
                    _snapshot,
                    new[] { affixId },
                    new Dictionary<string, float>(StringComparer.Ordinal)
                    {
                        [affixId] = ResolveMagnitude(affixId, rolled),
                    }));
            }
        }

        return (AverageOrNull(qualities), qualities.Count);
    }

    private double MeasureItem(InventoryItemRecord item)
    {
        var affixIds = (item.AffixIds ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        var affixIdSet = affixIds.ToHashSet(StringComparer.Ordinal);
        var magnitudes = (item.AffixMagnitudeRolls
                          ?? new List<InventoryAffixMagnitudeRecord>())
            .Where(value => value != null && affixIdSet.Contains(value.AffixId))
            .GroupBy(value => value.AffixId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Magnitude,
                StringComparer.Ordinal);
        return RefitRollQuality.Measure(_snapshot, affixIds, magnitudes);
    }

    private float ResolveMagnitude(
        string affixId,
        IReadOnlyDictionary<string, float> rolled)
    {
        if (rolled.TryGetValue(affixId, out var magnitude))
        {
            return magnitude;
        }

        if (_snapshot.AffixPackages.TryGetValue(affixId, out var package)
            && package.Modifiers is { Count: > 0 })
        {
            return package.Modifiers[0].Value;
        }

        throw new InvalidOperationException(
            $"Measurement could not resolve magnitude for affix '{affixId}'.");
    }

    private static int ParseCampaignIndex(string campaignId, int fallback)
    {
        const string prefix = "campaign-";
        return campaignId.StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(
                   campaignId.Substring(prefix.Length),
                   System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var parsed)
            ? parsed
            : fallback;
    }

    private static double? AverageOrNull(IEnumerable<double> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 ? null : materialized.Average();
    }

    private sealed record Candidate(
        HeadlessRefitItemObservation Item,
        HeadlessRefitSlotObservation Slot);

    private sealed record CampaignIdentity(
        int CampaignIndex,
        int DerivedSeed);

    private sealed class MutableWindow
    {
        private readonly IReadOnlyList<H100SealAffixObservationRecord> _candidateAffixes;
        private readonly IReadOnlyList<H100SealCostRecord> _candidateSealCosts;
        private readonly double? _visibleInventoryMeanRollQuality;
        private string _appliedAction = string.Empty;
        private int? _echoSpent;
        private double? _candidateRollQualityAfter;

        public MutableWindow(
            int campaignIndex,
            int logicalSeed,
            int derivedCampaignSeed,
            int siteIndex,
            int decisionIndex,
            int windowOrdinal,
            int walletGold,
            int walletEcho,
            int visibleRefitItemCount,
            string candidateItemId,
            string candidateItemInstanceId,
            int candidateSlotIndex,
            int candidatePlainRefitCost,
            bool candidateAllowsSeal,
            IReadOnlyList<H100SealAffixObservationRecord> candidateAffixes,
            IReadOnlyList<H100SealCostRecord> candidateSealCosts,
            double? candidateMeanRollQuality,
            double? visibleInventoryMeanRollQuality)
        {
            CampaignIndex = campaignIndex;
            LogicalSeed = logicalSeed;
            DerivedCampaignSeed = derivedCampaignSeed;
            SiteIndex = siteIndex;
            DecisionIndex = decisionIndex;
            WindowOrdinal = windowOrdinal;
            WalletGold = walletGold;
            WalletEcho = walletEcho;
            VisibleRefitItemCount = visibleRefitItemCount;
            CandidateItemId = candidateItemId;
            CandidateItemInstanceId = candidateItemInstanceId;
            CandidateSlotIndex = candidateSlotIndex;
            CandidatePlainRefitCost = candidatePlainRefitCost;
            CandidateAllowsSeal = candidateAllowsSeal;
            _candidateAffixes = candidateAffixes;
            _candidateSealCosts = candidateSealCosts;
            CandidateMeanRollQuality = candidateMeanRollQuality;
            _visibleInventoryMeanRollQuality = visibleInventoryMeanRollQuality;
        }

        public int CampaignIndex { get; }
        public int LogicalSeed { get; }
        public int DerivedCampaignSeed { get; }
        public int SiteIndex { get; }
        public int DecisionIndex { get; }
        public int WindowOrdinal { get; }
        public int WalletGold { get; }
        public int WalletEcho { get; }
        public int VisibleRefitItemCount { get; }
        public string CandidateItemId { get; }
        public string CandidateItemInstanceId { get; }
        public int CandidateSlotIndex { get; }
        public int CandidatePlainRefitCost { get; }
        public bool CandidateAllowsSeal { get; }
        public double? CandidateMeanRollQuality { get; }

        public void ApplySkip()
        {
            _appliedAction = "skip";
            _echoSpent = 0;
        }

        public void Apply(string operation, int echoSpent, double qualityAfter)
        {
            _appliedAction = operation;
            _echoSpent = echoSpent;
            _candidateRollQualityAfter = qualityAfter;
        }

        public H100SealRefitWindowRecord ToRecord()
            => new(
                CampaignIndex,
                LogicalSeed,
                DerivedCampaignSeed,
                SiteIndex,
                DecisionIndex,
                WindowOrdinal,
                WalletGold,
                WalletEcho,
                VisibleRefitItemCount,
                CandidateItemId,
                CandidateItemInstanceId,
                CandidateSlotIndex,
                CandidatePlainRefitCost,
                CandidateAllowsSeal,
                _candidateAffixes.Count,
                _candidateSealCosts,
                _candidateAffixes,
                CandidateMeanRollQuality,
                _visibleInventoryMeanRollQuality,
                CandidateMeanRollQuality.HasValue
                && _visibleInventoryMeanRollQuality.HasValue
                    ? CandidateMeanRollQuality.Value
                      - _visibleInventoryMeanRollQuality.Value
                    : null,
                _candidateSealCosts.Count(value => value.Affordable),
                _appliedAction,
                _echoSpent,
                _candidateRollQualityAfter);
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static ReferenceComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
