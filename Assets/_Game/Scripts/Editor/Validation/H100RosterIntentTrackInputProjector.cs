using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessCensus;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>현재 Town 영입·패시브·Refit 선택을 evaluator-only delta DTO로 낮춘다.</summary>
internal static class H100RosterIntentTrackInputProjector
{
    public static IReadOnlyList<IntentTrackChoice> ProjectRecruitChoices(
        HeadlessRosterPolicyObservation observation,
        IReadOnlyList<ConceptContract> contracts)
    {
        var choices = new List<IntentTrackChoice> { IntentTrackChoice.NoOp("recruit:none") };
        if (observation.Roster.Count >= observation.RosterCapacity)
        {
            return choices;
        }

        var recruitOffers = observation.RecruitOffers
            .Where(value => !value.IsDuplicate && observation.Wallet.Gold >= value.GoldCost)
            .Where(value => ContractReferences(
                                contracts,
                                RecruitSemanticIds(value))
                            || HasTeamRuleContract(contracts))
            .GroupBy(value => RecruitSemanticSignature(value, contracts), StringComparer.Ordinal)
            .Select(group => group.OrderBy(value => value.OfferIndex).First())
            .OrderBy(value => value.OfferIndex)
            .ToArray();
        foreach (var offer in recruitOffers)
        {
            var skillIds = new[] { offer.FlexActiveSkillId }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => $"skill:{value}")
                .ToArray();
            var passiveIds = new[] { offer.FlexPassiveSkillId }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => $"passive:{value}")
                .ToArray();
            var components = new[] { $"archetype:{offer.ArchetypeId}" }
                .Concat(skillIds)
                .Concat(passiveIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var member = new IntentTrackRosterMember(
                offer.ArchetypeId,
                new[] { offer.ArchetypeId, offer.RaceId, offer.ClassId, offer.RoleTag }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                components,
                Array.Empty<string>());
            choices.Add(new IntentTrackChoice(
                $"recruit:{offer.OfferIndex:D2}:{offer.ArchetypeId}",
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { member },
                Array.Empty<string>(),
                skillIds,
                passiveIds,
                components,
                0,
                offer.GoldCost,
                0,
                0,
                0,
                0,
                Array.Empty<string>(),
                Array.Empty<IntentTrackTagCount>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                member.Tags.Concat(components).ToArray(),
                true));
        }

        return choices;
    }

    public static IReadOnlyList<IntentTrackChoice> ProjectPassiveChoices(
        HeadlessRosterPolicyObservation observation,
        IReadOnlyList<ConceptContract> contracts,
        CombatContentSnapshot snapshot)
    {
        var heroArchetypeById = observation.Roster.ToDictionary(value => value.HeroId, value => value.ArchetypeId, StringComparer.Ordinal);
        var choices = new List<IntentTrackChoice> { IntentTrackChoice.NoOp("level_node:none") };
        var allCandidates = observation.PassiveHeroes.OrderBy(value => value.HeroId, StringComparer.Ordinal)
            .Where(hero => heroArchetypeById.ContainsKey(hero.HeroId))
            .SelectMany(hero => hero.Boards
                .Where(value => string.IsNullOrWhiteSpace(hero.SelectedBoardId)
                                || string.Equals(value.BoardId, hero.SelectedBoardId, StringComparison.Ordinal))
                .SelectMany(board => board.Nodes.Select(node => new { Hero = hero, Board = board, Node = node })))
            .ToArray();
        var relevantNodeIds = allCandidates
            .Where(value => ContractReferences(contracts, PassiveSemanticIds(value.Node, snapshot)))
            .Select(value => value.Node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var nodeById = allCandidates
            .GroupBy(value => value.Node.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Node, StringComparer.Ordinal);
        var pendingPrerequisites = new Queue<string>(relevantNodeIds);
        while (pendingPrerequisites.Count > 0)
        {
            var nodeId = pendingPrerequisites.Dequeue();
            if (!nodeById.TryGetValue(nodeId, out var node)) continue;
            foreach (var prerequisiteId in node.PrerequisiteNodeIds)
            {
                if (relevantNodeIds.Add(prerequisiteId)) pendingPrerequisites.Enqueue(prerequisiteId);
            }
        }

        var nodeRepresentatives = allCandidates
            .Where(value => relevantNodeIds.Contains(value.Node.NodeId))
            .GroupBy(value => value.Node.NodeId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(value => value.Hero.HeroId, StringComparer.Ordinal)
                .ThenBy(value => value.Board.BoardId, StringComparer.Ordinal)
                .First())
            .OrderBy(value => value.Node.BoardDepth)
            .ThenBy(value => value.Node.NodeId, StringComparer.Ordinal)
            .ToArray();
        foreach (var candidate in nodeRepresentatives)
        {
            var node = candidate.Node;
            var memberId = heroArchetypeById[candidate.Hero.HeroId];
            var passiveId = $"passive:{node.NodeId}";
            var skillIds = string.IsNullOrWhiteSpace(node.GrantedSkillId)
                ? Array.Empty<string>()
                : new[] { $"skill:{node.GrantedSkillId}" };
            var skillEffects = !string.IsNullOrWhiteSpace(node.GrantedSkillId)
                               && snapshot.SkillCatalog.TryGetValue(node.GrantedSkillId, out var grantedSkill)
                ? H100IntentTrackInputProjector.SkillEffects(H100PolicyObservationBuilder.BuildSkillCards(new[] { grantedSkill }))
                : Array.Empty<string>();
            var owned = new[] { passiveId }.Concat(skillIds).ToArray();
            choices.Add(new IntentTrackChoice(
                $"level_node:{memberId}:{node.NodeId}",
                new[] { memberId },
                node.PrerequisiteNodeIds.Select(value => $"passive:{value}").ToArray(),
                Array.Empty<IntentTrackRosterMember>(),
                Array.Empty<string>(),
                skillIds,
                new[] { passiveId },
                owned,
                0,
                0,
                0,
                1,
                0,
                0,
                Array.Empty<string>(),
                Array.Empty<IntentTrackTagCount>(),
                Array.Empty<string>(),
                skillEffects,
                Array.Empty<string>(),
                null,
                owned.Concat(skillEffects).ToArray(),
                true));
        }

        return choices;
    }

    public static IReadOnlyList<IntentTrackChoice> ProjectRefitChoices(
        HeadlessRosterPolicyObservation observation,
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IReadOnlyList<ConceptContract> contracts)
    {
        var choices = new List<IntentTrackChoice> { IntentTrackChoice.NoOp("refit:none") };
        var recordById = session.Profile.Inventory
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ItemInstanceId))
            .ToDictionary(value => value.ItemInstanceId, StringComparer.Ordinal);
        foreach (var item in observation.RefitItems
                     .Where(value => observation.Wallet.Echo >= value.EchoCost)
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                     .ThenBy(value => value.ItemInstanceId, StringComparer.Ordinal))
        {
            if (!recordById.TryGetValue(item.ItemInstanceId, out var record)) continue;
            var actionAnchor = item.AffixSlots
                .Where(value => value.CanRefit)
                .OrderBy(value => value.SlotIndex)
                .FirstOrDefault();
            if (actionAnchor == null) continue;

            var result = session.PreviewRefitItem(
                item.ItemInstanceId,
                unchecked((ulong)(uint)observation.DecisionSeed));
            if (!result.Applied) continue;
            var addedAffixIds = result.AffixIds
                .Except(record.AffixIds, StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var semanticAffixIds = addedAffixIds.Length > 0
                ? addedAffixIds
                : result.AffixIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var affixRefs = semanticAffixIds
                .Select(value => $"affix:{value}")
                .ToArray();
            if (!ContractReferences(contracts, semanticAffixIds.Concat(affixRefs))) continue;
            choices.Add(new IntentTrackChoice(
                    $"refit:{item.ItemInstanceId}:{actionAnchor.SlotIndex}:{string.Join(",", semanticAffixIds)}",
                    Array.Empty<string>(),
                    new[] { $"item:{item.ItemId}" },
                    Array.Empty<IntentTrackRosterMember>(),
                    affixRefs,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    affixRefs,
                    0,
                    0,
                    0,
                    0,
                    0,
                    result.Quote.EchoCost,
                    Array.Empty<string>(),
                    Array.Empty<IntentTrackTagCount>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    null,
                    affixRefs,
                    true));
        }

        return choices.Take(1).Concat(choices.Skip(1)
                .GroupBy(value => $"{string.Join("|", value.AddedOwnedComponentIds)}|{value.RefitResourceCost}", StringComparer.Ordinal)
                .Select(group => group.OrderBy(value => value.ChoiceId, StringComparer.Ordinal).First())
                .OrderBy(value => value.ChoiceId, StringComparer.Ordinal))
            .ToArray();
    }

    private static IEnumerable<string> RecruitSemanticIds(HeadlessRecruitOfferObservation offer)
        => new[]
        {
            offer.ArchetypeId,
            $"archetype:{offer.ArchetypeId}",
            offer.RaceId,
            offer.ClassId,
            offer.RoleTag,
            offer.FlexActiveSkillId,
            $"skill:{offer.FlexActiveSkillId}",
            offer.FlexPassiveSkillId,
            $"passive:{offer.FlexPassiveSkillId}",
        }.Where(value => !string.IsNullOrWhiteSpace(value));

    private static string RecruitSemanticSignature(
        HeadlessRecruitOfferObservation offer,
        IReadOnlyList<ConceptContract> contracts)
    {
        var clauses = ContractClauses(contracts).ToArray();
        var semantics = RecruitSemanticIds(offer)
            .Where(id => clauses.Any(clause => clause.IndexOf(id, StringComparison.Ordinal) >= 0))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);
        return $"cost={offer.GoldCost}|semantic={string.Join(",", semantics)}";
    }

    private static IEnumerable<string> PassiveSemanticIds(
        HeadlessPassiveNodeObservation node,
        CombatContentSnapshot snapshot)
        => new[] { node.NodeId, $"passive:{node.NodeId}", node.GrantedSkillId, $"skill:{node.GrantedSkillId}" }
            .Concat(!string.IsNullOrWhiteSpace(node.GrantedSkillId)
                    && snapshot.SkillCatalog.TryGetValue(node.GrantedSkillId, out var grantedSkill)
                ? H100IntentTrackInputProjector.SkillEffects(H100PolicyObservationBuilder.BuildSkillCards(new[] { grantedSkill }))
                : Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value));

    private static bool ContractReferences(
        IReadOnlyList<ConceptContract> contracts,
        IEnumerable<string> semanticIds)
    {
        var ids = semanticIds.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return ContractClauses(contracts).Any(clause =>
            ids.Any(id => clause.IndexOf(id, StringComparison.Ordinal) >= 0));
    }

    private static bool HasTeamRuleContract(IReadOnlyList<ConceptContract> contracts)
        => ContractClauses(contracts).Any(value =>
            value.StartsWith("build.team_rule=", StringComparison.Ordinal));

    private static IEnumerable<string> ContractClauses(IReadOnlyList<ConceptContract> contracts)
        => (contracts ?? Array.Empty<ConceptContract>())
            .Where(contract => contract != null)
            .SelectMany(contract => (contract.IdentityPredicates ?? Array.Empty<string>())
                .Concat(contract.ProgressMilestones ?? Array.Empty<string>())
                .Concat(contract.AllowedSubstitutions ?? Array.Empty<string>())
                .Concat(contract.CounterAffordances ?? Array.Empty<string>()));

}
