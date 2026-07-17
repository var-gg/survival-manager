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

        if (observation.Wallet.Echo < item.EchoCost)
        {
            throw new InvalidOperationException("Refit decision is not affordable from the visible wallet.");
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
