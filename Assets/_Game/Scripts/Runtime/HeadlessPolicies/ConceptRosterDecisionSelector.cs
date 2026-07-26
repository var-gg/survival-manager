using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>concept intent를 현재 Town UI의 합법 recruit/node/refit 선택에만 대조한다.</summary>
internal static class ConceptRosterDecisionSelector
{
    private const double SealQualityThreshold = 0.70d;
    private const double NeutralRerollQuality = 0.50d;
    private const double MinimumSealNetValue = 0.01d;

    internal sealed class RecruitSelection
    {
        public RecruitSelection(
            HeadlessRecruitDecision decision,
            bool milestoneAdvanced,
            int progressScore,
            IReadOnlyList<string> completedMilestones)
        {
            Decision = decision;
            MilestoneAdvanced = milestoneAdvanced;
            ProgressScore = progressScore;
            CompletedMilestones = completedMilestones;
        }

        public HeadlessRecruitDecision Decision { get; }
        public bool MilestoneAdvanced { get; }
        public int ProgressScore { get; }
        public IReadOnlyList<string> CompletedMilestones { get; }
    }

    internal sealed class PassiveSelection
    {
        public PassiveSelection(
            HeadlessPassiveDecision decision,
            bool milestoneAdvanced,
            int progressScore,
            IReadOnlyList<string> completedMilestones)
        {
            Decision = decision;
            MilestoneAdvanced = milestoneAdvanced;
            ProgressScore = progressScore;
            CompletedMilestones = completedMilestones;
        }

        public HeadlessPassiveDecision Decision { get; }
        public bool MilestoneAdvanced { get; }
        public int ProgressScore { get; }
        public IReadOnlyList<string> CompletedMilestones { get; }
    }

    internal sealed class RefitSelection
    {
        public RefitSelection(HeadlessRefitDecision decision) => Decision = decision;
        public HeadlessRefitDecision Decision { get; }
    }

    public static RecruitSelection SelectRecruit(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessRosterPolicyObservation observation,
        Func<string, string> rationale)
    {
        var candidates = observation.RecruitOffers
            .Where(offer => observation.Wallet.Gold >= offer.GoldCost
                            && observation.Roster.Count < observation.RosterCapacity
                            && !offer.IsDuplicate)
            .Select(offer =>
            {
                var advanced = NewlyCompletedRecruitMilestones(intent, state, observation, offer);
                var identityMatches = intent.IdentityPredicates.Count(value => OfferMatchesClaim(offer, value));
                var substitutionMatches = intent.AllowedSubstitutions.Count(value => OfferMatchesClaim(offer, value));
                return new { Offer = offer, Advanced = advanced, IdentityMatches = identityMatches, SubstitutionMatches = substitutionMatches };
            })
            .Where(value => value.Advanced.Count > 0 || value.IdentityMatches > 0 || value.SubstitutionMatches > 0)
            .OrderByDescending(value => value.Advanced.Count > 0)
            .ThenByDescending(value => value.IdentityMatches)
            .ThenByDescending(value => value.SubstitutionMatches)
            .ThenBy(value => value.Offer.OfferIndex)
            .FirstOrDefault();
        if (candidates == null)
        {
            return new RecruitSelection(
                new HeadlessRecruitDecision(
                    -1,
                    rationale("offer=no_relevant_legal_recruit"),
                    0d,
                    HeadlessRosterPolicyEvidence.ForRecruit(observation, -1)),
                false,
                state.ProgressScore,
                state.CompletedMilestones);
        }

        var completed = state.CompletedMilestones.Concat(candidates.Advanced)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new RecruitSelection(
            new HeadlessRecruitDecision(
                candidates.Offer.OfferIndex,
                rationale($"offer={candidates.Offer.ArchetypeId};milestones={candidates.Advanced.Count}"),
                candidates.IdentityMatches + candidates.SubstitutionMatches,
                HeadlessRosterPolicyEvidence.ForRecruit(observation, candidates.Offer.OfferIndex)),
            candidates.Advanced.Count > 0,
            state.ProgressScore + Math.Max(candidates.IdentityMatches, candidates.Advanced.Count),
            completed);
    }

    public static PassiveSelection SelectPassive(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessRosterPolicyObservation observation,
        Func<string, string> rationale)
    {
        var candidates = observation.PassiveHeroes
            .OrderBy(value => value.HeroId, StringComparer.Ordinal)
            .SelectMany(hero => hero.Boards.OrderBy(value => value.BoardId, StringComparer.Ordinal)
                .SelectMany(board =>
                {
                    var supportedNodeIds = TargetSupportNodeIds(board, intent, state);
                    return board.Nodes.OrderBy(value => value.BoardDepth).ThenBy(value => value.NodeId, StringComparer.Ordinal)
                        .Select(node =>
                        {
                            var decision = new HeadlessPassiveDecision(
                                hero.HeroId,
                                board.BoardId,
                                node.NodeId,
                                "candidate_legality_probe",
                                0d,
                                HeadlessRosterPolicyEvidence.ForPassive(
                                    observation,
                                    hero.HeroId,
                                    node.NodeId));
                            var advanced = NewlyCompletedPassiveMilestones(intent, state, node);
                            var identityMatches = intent.IdentityPredicates.Count(value => NodeMatchesClaim(node, value));
                            var directlyRelevant = advanced.Count > 0 || identityMatches > 0;
                            return new
                            {
                                Hero = hero,
                                Board = board,
                                Node = node,
                                Decision = decision,
                                Advanced = advanced,
                                IdentityMatches = identityMatches,
                                DirectlyRelevant = directlyRelevant,
                                SupportsTarget = supportedNodeIds.Contains(node.NodeId),
                            };
                        });
                }))
            .Where(value => HeadlessRosterPolicyGuard.IsPassiveDecisionLegal(observation, value.Decision))
            .Where(value => value.SupportsTarget)
            .OrderByDescending(value => value.Advanced.Count > 0)
            .ThenByDescending(value => value.IdentityMatches)
            .ThenByDescending(value => value.DirectlyRelevant)
            .ThenBy(value => value.Node.BoardDepth)
            .ThenBy(value => value.Hero.HeroId, StringComparer.Ordinal)
            .ThenBy(value => value.Node.NodeId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidates == null)
        {
            return new PassiveSelection(
                new HeadlessPassiveDecision(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    rationale("node=no_relevant_legal_node"),
                    0d,
                    HeadlessRosterPolicyEvidence.ForPassive(observation, string.Empty, string.Empty)),
                false,
                state.ProgressScore,
                state.CompletedMilestones);
        }

        var completed = state.CompletedMilestones.Concat(candidates.Advanced)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new PassiveSelection(
                candidates.Decision.WithOutcome(
                rationale($"hero={candidates.Hero.HeroId};node={candidates.Node.NodeId};milestones={candidates.Advanced.Count};path={(candidates.DirectlyRelevant ? "target" : "prerequisite")}"),
                candidates.IdentityMatches,
                HeadlessRosterPolicyEvidence.ForPassive(
                    observation,
                    candidates.Hero.HeroId,
                    candidates.Node.NodeId)),
            candidates.Advanced.Count > 0,
            state.ProgressScore + Math.Max(candidates.IdentityMatches, candidates.Advanced.Count),
            completed);
    }

    private static ISet<string> TargetSupportNodeIds(
        HeadlessPassiveBoardObservation board,
        HeadlessConceptIntent intent,
        IntentState state)
    {
        var nodesById = board.Nodes.ToDictionary(value => value.NodeId, StringComparer.Ordinal);
        var result = board.Nodes
            .Where(node => intent.IdentityPredicates.Any(value => NodeMatchesClaim(node, value))
                           || intent.ProgressMilestones
                               .Except(state.CompletedMilestones, StringComparer.Ordinal)
                               .Any(value => NodeMatchesClaim(node, value)))
            .Select(value => value.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var pending = new Queue<string>(result);
        while (pending.Count > 0)
        {
            var nodeId = pending.Dequeue();
            if (!nodesById.TryGetValue(nodeId, out var node)) continue;
            foreach (var prerequisiteId in node.PrerequisiteNodeIds ?? Array.Empty<string>())
            {
                if (result.Add(prerequisiteId)) pending.Enqueue(prerequisiteId);
            }
        }

        return result;
    }

    public static RefitSelection SelectRefit(
        HeadlessConceptIntent intent,
        HeadlessRosterPolicyObservation observation,
        Func<string, string> rationale)
    {
        var ownedAffixes = observation.Roster.SelectMany(hero => hero.EquippedItems)
            .SelectMany(item => item.Affixes)
            .Select(affix => affix.AffixId)
            .ToHashSet(StringComparer.Ordinal);
        var missingAffixes = intent.IdentityPredicates
            .Where(value => value.StartsWith("owned:affix:", StringComparison.Ordinal))
            .Select(value => value.Substring("owned:affix:".Length))
            .Where(value => !ownedAffixes.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var choice = observation.RefitItems
            .Where(item => observation.Wallet.Echo >= item.EchoCost)
            .SelectMany(item => item.AffixSlots.OrderBy(slot => slot.SlotIndex)
                .Where(slot => slot.CanRefit)
                .Select(slot => new { Item = item, Slot = slot }))
            .Select(value =>
            {
                var sealedAffixIds = SelectSealLocks(
                    value.Item,
                    observation.Wallet.Echo,
                    out var rerollTarget,
                    out var sealCost,
                    out var sealNetValue);
                return new RefitCandidate(
                    value.Item,
                    value.Slot,
                    sealedAffixIds,
                    rerollTarget,
                    sealCost,
                    sealNetValue,
                    value.Item.AffixSlots.Average(slot => slot.RollQuality));
            })
            .OrderByDescending(value => value.SealedAffixIds.Count != 0)
            .ThenByDescending(value => value.SealNetValue)
            .ThenBy(value => value.MeanRollQuality)
            .ThenByDescending(value => value.Item.AffixSlots.Count)
            .ThenBy(value => value.Item.ItemId, StringComparer.Ordinal)
            .ThenBy(value => value.Item.ItemInstanceId, StringComparer.Ordinal)
            .ThenBy(value => value.Slot.SlotIndex)
            .FirstOrDefault();
        if (missingAffixes.Length == 0 || choice == null)
        {
            return new RefitSelection(new HeadlessRefitDecision(
                string.Empty,
                -1,
                rationale(missingAffixes.Length == 0 ? "refit=no_missing_affix_intent" : "refit=no_affordable_legal_slot"),
                0d,
                HeadlessRosterPolicyEvidence.ForRefit(observation, string.Empty, -1)));
        }

        var detail = choice.SealedAffixIds.Count == 0
            ? $"item={choice.Item.ItemId};slot={choice.Slot.SlotIndex};target_unknown_until_roll"
            : $"item={choice.Item.ItemId};slot={choice.Slot.SlotIndex};"
              + $"reroll_target={choice.RerollTarget.CurrentAffix.AffixId};"
              + $"target_quality={choice.RerollTarget.RollQuality.ToString("0.###", CultureInfo.InvariantCulture)};"
              + $"seal={string.Join(",", choice.SealedAffixIds)};"
              + $"seal_cost={choice.SealCost.ToString(CultureInfo.InvariantCulture)}";
        return new RefitSelection(new HeadlessRefitDecision(
            choice.Item.ItemInstanceId,
            choice.Slot.SlotIndex,
            rationale(detail),
            choice.SealNetValue,
            HeadlessRosterPolicyEvidence.ForRefit(
                observation,
                choice.Item.ItemInstanceId,
                choice.Slot.SlotIndex,
                choice.SealedAffixIds),
            choice.SealedAffixIds));
    }

    private static IReadOnlyList<string> SelectSealLocks(
        HeadlessRefitItemObservation item,
        int visibleEcho,
        out HeadlessRefitSlotObservation rerollTarget,
        out int sealCost,
        out double sealNetValue)
    {
        var selectedTarget = item.AffixSlots
            .OrderBy(value => value.RollQuality)
            .ThenBy(value => value.SlotIndex)
            .ThenBy(value => value.CurrentAffix.AffixId, StringComparer.Ordinal)
            .First();
        rerollTarget = selectedTarget;
        sealCost = item.EchoCost;
        sealNetValue = 0d;
        if (!item.AllowsSeal || item.AffixSlots.Count < 2)
        {
            return Array.Empty<string>();
        }

        var candidates = item.AffixSlots
            .Where(value => !ReferenceEquals(value, selectedTarget)
                            && value.RollQuality >= SealQualityThreshold)
            .OrderByDescending(value => value.RollQuality)
            .ThenBy(value => value.CurrentAffix.AffixId, StringComparer.Ordinal)
            .ThenBy(value => value.SlotIndex)
            .ToArray();
        var options = new List<SealLockOption>();
        for (var count = 1; count <= candidates.Length; count++)
        {
            var cost = item.SealCosts.SingleOrDefault(value => value.LockedAffixCount == count);
            if (cost == null || cost.EchoCost > visibleEcho)
            {
                continue;
            }

            var locked = candidates.Take(count).ToArray();
            var preservationValue = locked.Sum(value => value.RollQuality - NeutralRerollQuality);
            var premiumShareOfWallet = Math.Max(0, cost.EchoCost - item.EchoCost)
                                       / (double)Math.Max(1, visibleEcho);
            var netValue = preservationValue - premiumShareOfWallet;
            if (netValue <= MinimumSealNetValue)
            {
                continue;
            }

            var ids = locked.Select(value => value.CurrentAffix.AffixId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            options.Add(new SealLockOption(ids, cost.EchoCost, netValue));
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
        sealNetValue = selected.NetValue;
        return selected.AffixIds;
    }

    private sealed class SealLockOption
    {
        public SealLockOption(IReadOnlyList<string> affixIds, int echoCost, double netValue)
        {
            AffixIds = affixIds;
            EchoCost = echoCost;
            NetValue = netValue;
            Signature = string.Join("|", affixIds);
        }

        public IReadOnlyList<string> AffixIds { get; }
        public int EchoCost { get; }
        public double NetValue { get; }
        public string Signature { get; }
    }

    private sealed class RefitCandidate
    {
        public RefitCandidate(
            HeadlessRefitItemObservation item,
            HeadlessRefitSlotObservation slot,
            IReadOnlyList<string> sealedAffixIds,
            HeadlessRefitSlotObservation rerollTarget,
            int sealCost,
            double sealNetValue,
            double meanRollQuality)
        {
            Item = item;
            Slot = slot;
            SealedAffixIds = sealedAffixIds;
            RerollTarget = rerollTarget;
            SealCost = sealCost;
            SealNetValue = sealNetValue;
            MeanRollQuality = meanRollQuality;
        }

        public HeadlessRefitItemObservation Item { get; }
        public HeadlessRefitSlotObservation Slot { get; }
        public IReadOnlyList<string> SealedAffixIds { get; }
        public HeadlessRefitSlotObservation RerollTarget { get; }
        public int SealCost { get; }
        public double SealNetValue { get; }
        public double MeanRollQuality { get; }
    }

    private static IReadOnlyList<string> NewlyCompletedRecruitMilestones(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessRosterPolicyObservation observation,
        HeadlessRecruitOfferObservation offer)
        => intent.ProgressMilestones
            .Except(state.CompletedMilestones, StringComparer.Ordinal)
            .Where(milestone => RecruitCompletesMilestone(milestone, observation.Roster, offer))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> NewlyCompletedPassiveMilestones(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessPassiveNodeObservation node)
        => intent.ProgressMilestones
            .Except(state.CompletedMilestones, StringComparer.Ordinal)
            .Where(milestone => NodeMatchesClaim(node, milestone))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static bool RecruitCompletesMilestone(
        string milestone,
        IReadOnlyList<HeadlessHeroObservation> roster,
        HeadlessRecruitOfferObservation offer)
    {
        if (TryParseCountMilestone(milestone, out var tag, out var required))
        {
            var current = roster.Count(hero => HeroHasTag(hero, tag));
            return current < required && current + (OfferHasTag(offer, tag) ? 1 : 0) >= required;
        }

        return milestone.StartsWith("acquire:", StringComparison.Ordinal)
               && OfferMatchesClaim(offer, milestone.Substring("acquire:".Length));
    }

    private static bool OfferMatchesClaim(HeadlessRecruitOfferObservation offer, string claim)
    {
        if (TryParseCountIdentity(claim, out var tag, out _))
        {
            return OfferHasTag(offer, tag);
        }

        return SemanticIds(offer).Any(id => ClaimContains(claim, id));
    }

    private static bool NodeMatchesClaim(HeadlessPassiveNodeObservation node, string claim)
        => NodeSemanticIds(node).Any(id => ClaimContains(claim, id));

    private static IReadOnlyList<string> SemanticIds(HeadlessRecruitOfferObservation offer)
        => new[]
        {
            offer.ArchetypeId,
            offer.RaceId,
            offer.ClassId,
            offer.RoleTag,
            offer.FlexActiveSkillId,
            offer.FlexPassiveSkillId,
        }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

    private static IEnumerable<string> NodeSemanticIds(HeadlessPassiveNodeObservation node)
        => new[] { node.NodeId, node.GrantedSkillId }
            .Concat(node.CompileTags ?? Array.Empty<string>())
            .Concat((node.StatModifiers ?? Array.Empty<HeadlessStatModifierObservation>()).Select(value => value.StatId))
            .Concat((node.RuleModifiers ?? Array.Empty<HeadlessRuleModifierObservation>()).SelectMany(value => new[] { value.Kind, value.Value }))
            .Where(value => !string.IsNullOrWhiteSpace(value));

    private static bool ClaimContains(string claim, string semanticId)
        => !string.IsNullOrWhiteSpace(claim)
           && !string.IsNullOrWhiteSpace(semanticId)
           && claim.Contains(semanticId, StringComparison.OrdinalIgnoreCase);

    private static bool OfferHasTag(HeadlessRecruitOfferObservation offer, string tag)
        => string.Equals(offer.ArchetypeId, tag, StringComparison.Ordinal)
           || string.Equals(offer.RaceId, tag, StringComparison.Ordinal)
           || string.Equals(offer.ClassId, tag, StringComparison.Ordinal)
           || string.Equals(offer.RoleTag, tag, StringComparison.Ordinal);

    private static bool HeroHasTag(HeadlessHeroObservation hero, string tag)
        => string.Equals(hero.ArchetypeId, tag, StringComparison.Ordinal)
           || string.Equals(hero.RaceId, tag, StringComparison.Ordinal)
           || string.Equals(hero.ClassId, tag, StringComparison.Ordinal)
           || string.Equals(hero.RoleTag, tag, StringComparison.Ordinal);

    private static bool TryParseCountIdentity(string value, out string tag, out int threshold)
    {
        tag = string.Empty;
        threshold = 0;
        const string prefix = "build.count_tag(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var close = value.IndexOf(')', prefix.Length);
        if (close < 0 || !value.Substring(close + 1).StartsWith(">=", StringComparison.Ordinal)) return false;
        tag = value.Substring(prefix.Length, close - prefix.Length);
        return int.TryParse(value.Substring(close + 3), out threshold);
    }

    private static bool TryParseCountMilestone(string value, out string tag, out int required)
    {
        tag = string.Empty;
        required = 0;
        const string prefix = "build.count_tag(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var close = value.IndexOf(')', prefix.Length);
        if (close < 0 || close + 1 >= value.Length || value[close + 1] != '=') return false;
        tag = value.Substring(prefix.Length, close - prefix.Length);
        return int.TryParse(value.Substring(close + 2).Split('/')[0], out required);
    }
}
