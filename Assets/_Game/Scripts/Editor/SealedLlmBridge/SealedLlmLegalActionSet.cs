using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>각 decision seam에서 player에게 실제로 열려 있던 canonical legal menu를 만든다.</summary>
public static class SealedLlmLegalActionSet
{
    /// <summary>
    /// Deployment legality is combinatorial, so this pins only the visible action space. The decoded choice is
    /// still fail-closed by <see cref="HeadlessPolicyGuard.ValidateDeploymentDecision"/>; descriptor plus guard is
    /// the honest no-cheat contract without enumerating every placement permutation.
    /// </summary>
    public static byte[] DeploymentDescriptor(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        using var payload = new MemoryStream();
        SealedLlmCanonicalValue.Append(payload, "SealedLlmDeploymentActionSpaceV1", "schema");
        SealedLlmCanonicalValue.AppendSortedStrings(
            payload,
            observation.Roster.Select(hero => hero.HeroId).ToArray(),
            "availableHeroIds");
        SealedLlmCanonicalValue.AppendSortedStrings(
            payload,
            observation.Anchors
                .Select(anchor => SealedLlmCanonicalValue.EnumName(anchor, nameof(observation.Anchors)))
                .ToArray(),
            "availableAnchorIds");
        SealedLlmCanonicalValue.AppendInteger(payload, observation.DeployCapacity);
        return payload.ToArray();
    }

    public static byte[] RewardDescriptor(HeadlessPolicyObservation observation)
        => Descriptor("SealedLlmRewardLegalActionSetV1", RewardKeys(observation));

    public static byte[] RecruitDescriptor(HeadlessRosterPolicyObservation observation)
        => Descriptor("SealedLlmRecruitLegalActionSetV1", RecruitKeys(observation));

    public static byte[] PassiveDescriptor(HeadlessRosterPolicyObservation observation)
        => Descriptor("SealedLlmPassiveLegalActionSetV1", PassiveKeys(observation));

    public static byte[] RefitDescriptor(HeadlessRosterPolicyObservation observation)
        => Descriptor("SealedLlmRefitLegalActionSetV1", RefitKeys(observation));

    public static string DeploymentLegalActionSetHash(HeadlessPolicyObservation observation)
        => LegalActionSetHash(DeploymentDescriptor(observation));

    public static string RewardLegalActionSetHash(HeadlessPolicyObservation observation)
        => LegalActionSetHash(RewardDescriptor(observation));

    public static string RecruitLegalActionSetHash(HeadlessRosterPolicyObservation observation)
        => LegalActionSetHash(RecruitDescriptor(observation));

    public static string PassiveLegalActionSetHash(HeadlessRosterPolicyObservation observation)
        => LegalActionSetHash(PassiveDescriptor(observation));

    public static string RefitLegalActionSetHash(HeadlessRosterPolicyObservation observation)
        => LegalActionSetHash(RefitDescriptor(observation));

    public static string LegalActionSetHash(byte[] descriptor)
        => SealedLlmCanonicalValue.Hash(
            descriptor ?? throw new ArgumentNullException(nameof(descriptor)));

    internal static IReadOnlyList<string> RewardKeys(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        if (observation.RewardOptions.Count == 0)
        {
            return new[] { "-1" };
        }

        return observation.RewardOptions
            .Select(option => SealedLlmCanonicalValue.Invariant(option.Index))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<string> RecruitKeys(HeadlessRosterPolicyObservation observation)
    {
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        var result = new List<string> { SealedLlmActionGrammar.Skip };
        var hasCapacity = observation.Roster.Count < observation.RosterCapacity;
        if (hasCapacity)
        {
            foreach (var offer in observation.RecruitOffers)
            {
                if (offer.GoldCost > observation.Wallet.Gold)
                {
                    continue;
                }

                var decision = new HeadlessRecruitDecision(
                    offer.OfferIndex,
                    SealedLlmDecisionEnvelope.Rationale,
                    0d,
                    SealedLlmDecisionEnvelope.EvidenceFactIds);
                HeadlessRosterPolicyGuard.ValidateRecruitDecision(observation, decision);
                result.Add(SealedLlmCanonicalValue.Invariant(offer.OfferIndex));
            }
        }

        return OrderedDistinct(result, "recruit legal action");
    }

    internal static IReadOnlyList<string> PassiveKeys(HeadlessRosterPolicyObservation observation)
    {
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        var result = new List<string> { SealedLlmActionGrammar.Skip };
        foreach (var hero in observation.PassiveHeroes)
        {
            foreach (var board in hero.Boards)
            {
                EnsureDistinctNodeIds(board);
                foreach (var node in board.Nodes)
                {
                    if (!IsPassiveSelectable(hero, board, node))
                    {
                        continue;
                    }

                    var decision = new HeadlessPassiveDecision(
                        hero.HeroId,
                        board.BoardId,
                        node.NodeId,
                        SealedLlmDecisionEnvelope.Rationale,
                        0d,
                        SealedLlmDecisionEnvelope.EvidenceFactIds);
                    HeadlessRosterPolicyGuard.ValidatePassiveDecision(observation, decision);
                    result.Add(SealedLlmActionGrammar.PassiveKey(hero.HeroId, board.BoardId, node.NodeId));
                }
            }
        }

        return OrderedDistinct(result, "passive legal action");
    }

    internal static IReadOnlyList<string> RefitKeys(HeadlessRosterPolicyObservation observation)
    {
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        var result = new List<string> { SealedLlmActionGrammar.Skip };
        foreach (var item in observation.RefitItems)
        {
            EnsureDistinctSlotIndices(item);
            if (item.EchoCost > observation.Wallet.Echo)
            {
                continue;
            }

            foreach (var slot in item.AffixSlots.Where(slot => slot.CanRefit))
            {
                var decision = new HeadlessRefitDecision(
                    item.ItemInstanceId,
                    slot.SlotIndex,
                    SealedLlmDecisionEnvelope.Rationale,
                    0d,
                    SealedLlmDecisionEnvelope.EvidenceFactIds);
                HeadlessRosterPolicyGuard.ValidateRefitDecision(observation, decision);
                result.Add(SealedLlmActionGrammar.RefitKey(item.ItemInstanceId, slot.SlotIndex));
            }
        }

        return OrderedDistinct(result, "refit legal action");
    }

    private static byte[] Descriptor(string schema, IReadOnlyList<string> keys)
    {
        using var payload = new MemoryStream();
        SealedLlmCanonicalValue.Append(payload, schema, nameof(schema));
        SealedLlmCanonicalValue.AppendSortedStrings(payload, keys, nameof(keys));
        return payload.ToArray();
    }

    private static bool IsPassiveSelectable(
        HeadlessPassiveHeroObservation hero,
        HeadlessPassiveBoardObservation board,
        HeadlessPassiveNodeObservation node)
    {
        var selected = hero.SelectedNodeIds ?? throw new InvalidOperationException("SelectedNodeIds must be materialized.");
        if (!string.IsNullOrWhiteSpace(hero.SelectedBoardId)
            && !string.Equals(hero.SelectedBoardId, board.BoardId, StringComparison.Ordinal))
        {
            return false;
        }

        if (selected.Contains(node.NodeId, StringComparer.Ordinal)
            || selected.Count >= hero.MaxActiveNodeCount
            || node.PrerequisiteNodeIds.Any(required => !selected.Contains(required, StringComparer.Ordinal)))
        {
            return false;
        }

        var nodesById = board.Nodes.ToDictionary(value => value.NodeId, StringComparer.Ordinal);
        if (string.Equals(node.NodeKind, "Keystone", StringComparison.OrdinalIgnoreCase)
            && selected.Where(nodesById.ContainsKey)
                .Count(id => string.Equals(nodesById[id].NodeKind, "Keystone", StringComparison.OrdinalIgnoreCase))
            >= hero.MaxKeystoneCount)
        {
            return false;
        }

        var selectedExclusions = selected.Where(nodesById.ContainsKey)
            .SelectMany(id => nodesById[id].MutualExclusionTagIds)
            .ToHashSet(StringComparer.Ordinal);
        return !node.MutualExclusionTagIds.Any(selectedExclusions.Contains);
    }

    private static void EnsureDistinctNodeIds(HeadlessPassiveBoardObservation board)
    {
        if (board.Nodes == null
            || board.Nodes.Any(node => node == null)
            || board.Nodes.GroupBy(node => node.NodeId, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException($"Passive board '{board.BoardId}' must expose distinct materialized nodes.");
        }
    }

    private static void EnsureDistinctSlotIndices(HeadlessRefitItemObservation item)
    {
        if (item.AffixSlots == null
            || item.AffixSlots.Any(slot => slot == null)
            || item.AffixSlots.GroupBy(slot => slot.SlotIndex).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException($"Refit item '{item.ItemInstanceId}' must expose distinct materialized slots.");
        }
    }

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values, string label)
    {
        var ordered = values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException($"The {label} set contains duplicate keys.");
        }

        return ordered;
    }
}
