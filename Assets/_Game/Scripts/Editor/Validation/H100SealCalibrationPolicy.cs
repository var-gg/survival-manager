using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>
/// Measurement-only policy adapter. It preserves ConceptCommitPolicy state and every
/// non-refit decision while replacing only the visible Seal lock calibration.
/// </summary>
internal sealed class H100SealCalibrationPolicy :
    IHeadlessPolicy,
    IHeadlessRosterPolicy,
    IHeadlessPrepPolicy
{
    private readonly ConceptCommitPolicy _inner;
    private readonly H100SealPolicyCalibration? _calibration;
    private readonly bool _forceNoSeal;

    public H100SealCalibrationPolicy(
        HeadlessConceptIntent intent,
        H100SealPolicyCalibration? calibration,
        bool forceNoSeal = false)
    {
        _inner = new ConceptCommitPolicy(
            intent ?? throw new ArgumentNullException(nameof(intent)));
        _calibration = calibration;
        _forceNoSeal = forceNoSeal;
        if (!forceNoSeal)
        {
            ValidateCalibration(
                calibration ?? throw new ArgumentNullException(nameof(calibration)));
        }
    }

    public string Id => _inner.Id;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
        => _inner.DecideDeployment(observation);

    public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
        => _inner.DecideReward(observation);

    public HeadlessPrepDecision DecidePrep(HeadlessPolicyObservation observation)
        => _inner.DecidePrep(observation);

    public HeadlessRecruitDecision DecideRecruit(HeadlessRosterPolicyObservation observation)
        => _inner.DecideRecruit(observation);

    public HeadlessPassiveDecision DecidePassiveAllocation(
        HeadlessRosterPolicyObservation observation)
        => _inner.DecidePassiveAllocation(observation);

    public HeadlessRefitDecision DecideRefit(HeadlessRosterPolicyObservation observation)
    {
        var ordinary = _inner.DecideRefit(observation);
        if (ordinary.IsNoOp)
        {
            return ordinary;
        }

        var item = observation.RefitItems.Single(value =>
            string.Equals(
                value.ItemInstanceId,
                ordinary.ItemInstanceId,
                StringComparison.Ordinal));
        if (_forceNoSeal)
        {
            if (ordinary.SealedAffixIds.Count != 0)
            {
                throw new InvalidOperationException(
                    "The no-Seal substrate exposed a non-empty Seal lock set.");
            }

            return ordinary;
        }

        var locks = SelectLocks(
            item,
            observation.Wallet.Echo,
            _calibration!,
            out var sealCost,
            out var netValue);
        var detail = locks.Count == 0
            ? $"calibration={_calibration!.Id};seal=none"
            : $"calibration={_calibration!.Id};seal={string.Join(",", locks)};"
              + $"seal_cost={sealCost.ToString(CultureInfo.InvariantCulture)};"
              + $"net={netValue.ToString("R", CultureInfo.InvariantCulture)}";
        return BuildDecision(
            observation,
            ordinary,
            item,
            locks,
            locks.Count == 0 ? ordinary.EstimatedValue : netValue,
            detail);
    }

    internal static IReadOnlyList<string> SelectLocks(
        HeadlessRefitItemObservation item,
        int visibleEcho,
        H100SealPolicyCalibration calibration,
        out int sealCost,
        out double netValue)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        ValidateCalibration(calibration);
        var rerollTarget = item.AffixSlots
            .OrderBy(value => value.RollQuality)
            .ThenBy(value => value.SlotIndex)
            .ThenBy(value => value.CurrentAffix.AffixId, StringComparer.Ordinal)
            .First();
        sealCost = item.EchoCost;
        netValue = 0d;
        if (!item.AllowsSeal || item.AffixSlots.Count < 2)
        {
            return Array.Empty<string>();
        }

        var candidates = item.AffixSlots
            .Where(value => !ReferenceEquals(value, rerollTarget)
                            && value.RollQuality >= calibration.Threshold)
            .OrderByDescending(value => value.RollQuality)
            .ThenBy(value => value.CurrentAffix.AffixId, StringComparer.Ordinal)
            .ThenBy(value => value.SlotIndex)
            .ToArray();
        var options = new List<LockOption>();
        for (var count = 1; count <= candidates.Length; count++)
        {
            var quote = item.SealCosts.SingleOrDefault(value =>
                value.LockedAffixCount == count);
            if (quote == null || quote.EchoCost > visibleEcho)
            {
                continue;
            }

            var locked = candidates.Take(count).ToArray();
            var preservationValue = locked.Sum(value =>
                value.RollQuality - calibration.Baseline);
            var premiumShareOfWallet =
                Math.Max(0, quote.EchoCost - item.EchoCost)
                / (double)Math.Max(1, visibleEcho);
            var candidateNetValue = preservationValue - premiumShareOfWallet;
            if (candidateNetValue <= calibration.NetValueFloor)
            {
                continue;
            }

            var ids = locked
                .Select(value => value.CurrentAffix.AffixId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            options.Add(new LockOption(ids, quote.EchoCost, candidateNetValue));
        }

        var selected = options
            .OrderByDescending(value => value.NetValue)
            .ThenBy(value => value.EchoCost)
            .ThenBy(value => value.Signature, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected == null)
        {
            return Array.Empty<string>();
        }

        sealCost = selected.EchoCost;
        netValue = selected.NetValue;
        return selected.AffixIds;
    }

    private static HeadlessRefitDecision BuildDecision(
        HeadlessRosterPolicyObservation observation,
        HeadlessRefitDecision ordinary,
        HeadlessRefitItemObservation item,
        IReadOnlyList<string> locks,
        double estimatedValue,
        string detail)
    {
        var evidence = ResolveEvidence(
            observation,
            item.ItemInstanceId,
            ordinary.AffixSlotIndex,
            locks.Count != 0);
        var decision = new HeadlessRefitDecision(
            item.ItemInstanceId,
            ordinary.AffixSlotIndex,
            $"{ordinary.Rationale};measurement={detail}",
            estimatedValue,
            evidence,
            locks);
        HeadlessRosterPolicyGuard.ValidateRefitDecision(observation, decision);
        return decision;
    }

    private static IReadOnlyList<string> ResolveEvidence(
        HeadlessRosterPolicyObservation observation,
        string itemInstanceId,
        int slotIndex,
        bool includeSeal)
    {
        var signals = new List<string>
        {
            HeadlessRosterPolicyEvidence.CampaignContextSignal,
            HeadlessRosterPolicyEvidence.WalletSignal,
            HeadlessRosterPolicyEvidence.RefitSurfaceSignal,
            HeadlessRosterPolicyEvidence.RefitSlotSignal(itemInstanceId, slotIndex),
        };
        if (includeSeal)
        {
            signals.Add(HeadlessRosterPolicyEvidence.RefitSealSignal(itemInstanceId));
        }

        return signals.Select(signal =>
        {
            if (!observation.EvidenceFactIdsBySignal.TryGetValue(signal, out var factId)
                || string.IsNullOrWhiteSpace(factId))
            {
                throw new HeadlessPolicyEvidenceException(
                    $"Measurement policy did not receive evidence signal '{signal}'.");
            }

            return factId;
        }).ToArray();
    }

    private static void ValidateCalibration(H100SealPolicyCalibration calibration)
    {
        if (calibration == null
            || !double.IsFinite(calibration.Threshold)
            || !double.IsFinite(calibration.NetValueFloor)
            || !double.IsFinite(calibration.Baseline)
            || calibration.Threshold < 0d
            || calibration.Threshold > 1d
            || calibration.NetValueFloor < 0d
            || calibration.NetValueFloor > 1d
            || calibration.Baseline < 0d
            || calibration.Baseline > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(calibration),
                "Seal calibration values must be finite and inside [0,1].");
        }
    }

    private sealed record LockOption(
        IReadOnlyList<string> AffixIds,
        int EchoCost,
        double NetValue)
    {
        public string Signature => string.Join("|", AffixIds);
    }
}
