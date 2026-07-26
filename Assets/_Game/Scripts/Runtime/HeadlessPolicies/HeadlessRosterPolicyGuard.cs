using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>Town roster 결정을 affordability/cap/budget/prerequisite 기준으로 fail-closed 검증한다.</summary>
public static class HeadlessRosterPolicyGuard
{
    public static void ValidateObservation(HeadlessRosterPolicyObservation observation)
    {
        if (observation == null)
        {
            throw new ArgumentNullException(nameof(observation));
        }

        if (observation.RosterCapacity < 0 || observation.Roster.Count > observation.RosterCapacity)
        {
            throw new InvalidOperationException("Roster observation exceeds its visible capacity.");
        }

        if (observation.Wallet == null || observation.Wallet.Gold < 0 || observation.Wallet.Echo < 0)
        {
            throw new InvalidOperationException("Roster observation contains an invalid visible wallet.");
        }

        if (observation.RecruitOffers.Any(value => value.OfferIndex < 0 || value.GoldCost < 0)
            || observation.RefitItems.Any(value => value.EchoCost < 0))
        {
            throw new InvalidOperationException("Roster observation contains an invalid visible cost.");
        }

        if (observation.RecruitOffers.GroupBy(value => value.OfferIndex).Any(group => group.Count() != 1)
            || observation.PassiveHeroes.GroupBy(value => value.HeroId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || observation.RefitItems.GroupBy(value => value.ItemInstanceId, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Roster observation contains duplicate visible identities.");
        }

        foreach (var item in observation.RefitItems)
        {
            ValidateRefitItemObservation(item);
        }
    }

    public static void ValidateRecruitDecision(
        HeadlessRosterPolicyObservation observation,
        HeadlessRecruitDecision decision)
    {
        ValidateObservation(observation);
        if (decision == null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        ValidateDecisionEnvelope(decision.Rationale, decision.EstimatedValue, decision.EvidenceFactIds);

        if (decision.IsNoOp)
        {
            return;
        }

        var offer = observation.RecruitOffers.SingleOrDefault(value => value.OfferIndex == decision.OfferIndex)
                    ?? throw new InvalidOperationException("Recruit decision references an unavailable offer.");
        if (observation.Wallet.Gold < offer.GoldCost)
        {
            throw new InvalidOperationException("Recruit decision is not affordable from the visible wallet.");
        }

        if (observation.Roster.Count >= observation.RosterCapacity)
        {
            throw new InvalidOperationException("Recruit decision exceeds the visible Town roster cap.");
        }
    }

    public static void ValidatePassiveDecision(
        HeadlessRosterPolicyObservation observation,
        HeadlessPassiveDecision decision)
    {
        ValidateObservation(observation);
        if (decision == null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        ValidateDecisionEnvelope(decision.Rationale, decision.EstimatedValue, decision.EvidenceFactIds);

        if (decision.IsNoOp)
        {
            return;
        }

        if (!TryGetPassiveSelection(observation, decision, out var hero, out var board, out var node))
        {
            throw new InvalidOperationException("Passive decision references an unavailable hero, board, or node.");
        }

        var selected = hero.SelectedNodeIds ?? Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(hero.SelectedBoardId)
            && !string.Equals(hero.SelectedBoardId, board.BoardId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Passive decision cannot silently discard a different selected board.");
        }

        if (selected.Contains(node.NodeId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Passive decision references an already selected node.");
        }

        if (selected.Count >= hero.MaxActiveNodeCount)
        {
            throw new InvalidOperationException("Passive decision exceeds the visible active-node budget.");
        }

        if ((node.PrerequisiteNodeIds ?? Array.Empty<string>()).Any(required => !selected.Contains(required, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Passive decision is missing a visible prerequisite node.");
        }

        var nodesById = board.Nodes.ToDictionary(value => value.NodeId, StringComparer.Ordinal);
        if (string.Equals(node.NodeKind, "Keystone", StringComparison.OrdinalIgnoreCase)
            && selected.Where(nodesById.ContainsKey)
                .Count(id => string.Equals(nodesById[id].NodeKind, "Keystone", StringComparison.OrdinalIgnoreCase))
            >= hero.MaxKeystoneCount)
        {
            throw new InvalidOperationException("Passive decision exceeds the visible keystone cap.");
        }

        var selectedExclusions = selected.Where(nodesById.ContainsKey)
            .SelectMany(id => nodesById[id].MutualExclusionTagIds ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        if ((node.MutualExclusionTagIds ?? Array.Empty<string>()).Any(selectedExclusions.Contains))
        {
            throw new InvalidOperationException("Passive decision conflicts with a selected mutual-exclusion tag.");
        }
    }

    public static void ValidateRefitDecision(
        HeadlessRosterPolicyObservation observation,
        HeadlessRefitDecision decision)
    {
        ValidateObservation(observation);
        if (decision == null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        ValidateDecisionEnvelope(decision.Rationale, decision.EstimatedValue, decision.EvidenceFactIds);

        if (decision.IsNoOp)
        {
            if (decision.SealedAffixIds.Count != 0)
            {
                throw new InvalidOperationException("A no-op Refit decision cannot carry Seal locks.");
            }

            return;
        }

        var item = observation.RefitItems.SingleOrDefault(
                       value => string.Equals(value.ItemInstanceId, decision.ItemInstanceId, StringComparison.Ordinal))
                   ?? throw new InvalidOperationException("Refit decision references an unavailable item.");
        var slot = item.AffixSlots.SingleOrDefault(value => value.SlotIndex == decision.AffixSlotIndex)
                   ?? throw new InvalidOperationException("Refit decision references an unavailable affix slot.");
        if (!slot.CanRefit)
        {
            throw new InvalidOperationException("Refit decision references a slot without a visible legal candidate.");
        }

        if (decision.SealedAffixIds.Count == 0)
        {
            if (observation.Wallet.Echo < item.EchoCost)
            {
                throw new InvalidOperationException("Refit decision is not affordable from the visible wallet.");
            }

            return;
        }

        if (decision.SealedAffixIds.Any(string.IsNullOrWhiteSpace)
            || decision.SealedAffixIds.Distinct(StringComparer.Ordinal).Count() != decision.SealedAffixIds.Count)
        {
            throw new InvalidOperationException("Seal lock selection contains a blank or duplicate affix id.");
        }

        var knownAffixIds = observation.RefitItems
            .SelectMany(value => value.AffixSlots)
            .Where(value => value.CurrentAffix != null)
            .Select(value => value.CurrentAffix.AffixId)
            .ToHashSet(StringComparer.Ordinal);
        var unknown = decision.SealedAffixIds
            .Where(value => !knownAffixIds.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (unknown != null)
        {
            throw new InvalidOperationException($"Seal lock selection references unknown affix '{unknown}'.");
        }

        var itemAffixIds = item.AffixSlots
            .Where(value => value.CurrentAffix != null)
            .Select(value => value.CurrentAffix.AffixId)
            .ToHashSet(StringComparer.Ordinal);
        var notOnItem = decision.SealedAffixIds
            .Where(value => !itemAffixIds.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (notOnItem != null)
        {
            throw new InvalidOperationException(
                $"Seal lock selection references affix '{notOnItem}' that is not on the selected item.");
        }

        if (decision.SealedAffixIds.Count >= itemAffixIds.Count)
        {
            throw new InvalidOperationException("Seal lock selection cannot lock all affixes on the selected item.");
        }

        if (!item.AllowsSeal)
        {
            throw new InvalidOperationException("Seal operation is excluded for the selected item.");
        }

        var sealCost = item.SealCosts.SingleOrDefault(
            value => value.LockedAffixCount == decision.SealedAffixIds.Count);
        if (sealCost == null)
        {
            throw new InvalidOperationException(
                "Seal lock count has no visible legal quote for the selected item.");
        }

        if (observation.Wallet.Echo < sealCost.EchoCost)
        {
            throw new InvalidOperationException("Seal decision is not affordable from the visible wallet.");
        }
    }

    internal static bool IsPassiveDecisionLegal(
        HeadlessRosterPolicyObservation observation,
        HeadlessPassiveDecision decision)
    {
        try
        {
            ValidatePassiveDecision(observation, decision);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void ValidateDecisionEnvelope(
        string rationale,
        double estimatedValue,
        IReadOnlyList<string> evidenceFactIds)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            throw new InvalidOperationException("Roster policy rationale must be non-empty.");
        }

        if (double.IsNaN(estimatedValue) || double.IsInfinity(estimatedValue))
        {
            throw new InvalidOperationException("Roster policy estimated value must be finite.");
        }

        if (evidenceFactIds == null
            || evidenceFactIds.Count == 0
            || evidenceFactIds.Any(string.IsNullOrWhiteSpace)
            || evidenceFactIds.Distinct(StringComparer.Ordinal).Count() != evidenceFactIds.Count)
        {
            throw new HeadlessPolicyEvidenceException(
                "Every roster policy decision must cite distinct player-visible fact ids.");
        }
    }

    private static void ValidateRefitItemObservation(HeadlessRefitItemObservation item)
    {
        if (item == null
            || string.IsNullOrWhiteSpace(item.ItemInstanceId)
            || item.AffixSlots == null
            || item.AffixSlots.Any(value => value == null))
        {
            throw new InvalidOperationException("Roster observation contains an invalid Refit item surface.");
        }

        if (item.AffixSlots.GroupBy(value => value.SlotIndex).Any(group => group.Count() != 1)
            || item.AffixSlots.Any(value => value.SlotIndex < 0))
        {
            throw new InvalidOperationException("Roster observation contains duplicate or invalid affix slots.");
        }

        if (item.AffixSlots.Any(value =>
                !double.IsFinite(value.RollQuality)
                || value.RollQuality < 0d
                || value.RollQuality > 1d)
            || item.AffixSlots
                .Where(value => value.CurrentAffix != null)
                .GroupBy(value => value.CurrentAffix.AffixId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Roster observation contains an invalid affix identity or roll quality.");
        }

        if (item.SealCosts == null
            || item.SealCosts.Any(value =>
                value == null
                || value.LockedAffixCount <= 0
                || value.LockedAffixCount >= item.AffixSlots.Count
                || value.EchoCost < 0)
            || item.SealCosts.GroupBy(value => value.LockedAffixCount).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Roster observation contains an invalid visible Seal cost.");
        }

        if (!item.AllowsSeal && item.SealCosts.Count != 0)
        {
            throw new InvalidOperationException(
                "Roster observation exposes Seal costs for an item that excludes Seal.");
        }

        if (item.AllowsSeal
            && item.AffixSlots.Any(value =>
                value.CurrentAffix == null
                || string.IsNullOrWhiteSpace(value.CurrentAffix.AffixId)))
        {
            throw new InvalidOperationException(
                "A Seal-capable item must expose every current affix identity.");
        }
    }

    private static bool TryGetPassiveSelection(
        HeadlessRosterPolicyObservation observation,
        HeadlessPassiveDecision decision,
        out HeadlessPassiveHeroObservation hero,
        out HeadlessPassiveBoardObservation board,
        out HeadlessPassiveNodeObservation node)
    {
        hero = observation.PassiveHeroes.SingleOrDefault(
            value => string.Equals(value.HeroId, decision.HeroId, StringComparison.Ordinal));
        board = hero?.Boards.SingleOrDefault(
            value => string.Equals(value.BoardId, decision.BoardId, StringComparison.Ordinal));
        node = board?.Nodes.SingleOrDefault(
            value => string.Equals(value.NodeId, decision.NodeId, StringComparison.Ordinal));
        return hero != null && board != null && node != null;
    }
}
