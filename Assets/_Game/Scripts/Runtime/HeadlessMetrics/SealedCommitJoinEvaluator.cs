using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessMetrics;

internal enum SealedCommitJoinIssue
{
    None = 0,
    ActionJoinFailure = 1,
    NoTargetRewardPick = 2,
    RewardMechanicsHole = 3,
}

internal sealed record SealedCommitTarget(string Kind, string Id)
{
    public string WireToken => $"{Kind}:{Id}";
}

internal sealed record SealedCommitExactMatch(string TrackToken, SealedCommitTarget Target);

internal sealed record SealedAppliedTargetResult(
    IReadOnlyList<SealedCommitTarget> Targets,
    IReadOnlyList<string> FamilyIds,
    SealedCommitJoinIssue Issue)
{
    public static SealedAppliedTargetResult Empty(SealedCommitJoinIssue issue)
        => new(Array.Empty<SealedCommitTarget>(), Array.Empty<string>(), issue);
}

/// <summary>봉인 action grammar와 observation join table만으로 exact commit target을 복원한다.</summary>
internal static class SealedCommitJoinEvaluator
{
    private static readonly HashSet<string> TrackKinds = new(StringComparer.Ordinal)
    {
        "archetype",
        "skill",
        "item",
        "item_instance",
        "affix",
        "augment",
        "passive",
        "synergy",
        "status",
        "tag",
        "team_rule",
    };

    public static SealedAppliedTargetResult Resolve(
        DecodedSealedDecision decision,
        string selectedAction)
    {
        var join = decision.Join;
        if (join?.Available != true)
        {
            return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
        }

        switch (decision.SeamType)
        {
            case "reward":
                if (!TryCanonicalInteger(selectedAction, nonNegative: false, out var rewardIndex))
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var rewards = join.RewardOptions.Where(row => row.Index == rewardIndex).ToArray();
                if (rewards.Length != 1)
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var reward = rewards[0];
                if (string.Equals(reward.Kind, "Item", StringComparison.Ordinal))
                {
                    return !reward.ItemMechanicsPresent || string.IsNullOrEmpty(reward.ItemId)
                        ? SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.RewardMechanicsHole)
                        : One(new SealedCommitTarget("item", reward.ItemId), reward.FamilyIds);
                }

                if (string.Equals(reward.Kind, "TemporaryAugment", StringComparison.Ordinal))
                {
                    return !reward.TemporaryAugmentMechanicsPresent
                           || string.IsNullOrEmpty(reward.TemporaryAugmentId)
                        ? SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.RewardMechanicsHole)
                        : One(
                            new SealedCommitTarget("augment", reward.TemporaryAugmentId),
                            reward.FamilyIds);
                }

                return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.NoTargetRewardPick);

            case "recruit":
                if (!TryCanonicalInteger(selectedAction, nonNegative: true, out var offerIndex))
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var offers = join.RecruitOffers.Where(row => row.OfferIndex == offerIndex).ToArray();
                if (offers.Length != 1)
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var offer = offers[0];
                return new SealedAppliedTargetResult(
                    Targets(
                        new SealedCommitTarget("archetype", offer.ArchetypeId),
                        new SealedCommitTarget("skill", offer.FlexActiveSkillId),
                        new SealedCommitTarget("skill", offer.FlexPassiveSkillId)),
                    offer.FamilyIds,
                    SealedCommitJoinIssue.None);

            case "level_node":
                var passiveParts = selectedAction.Split(new[] { ':' }, StringSplitOptions.None);
                if (passiveParts.Length != 3 || passiveParts.Any(string.IsNullOrEmpty))
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var nodes = join.PassiveNodes.Where(row =>
                    string.Equals(row.HeroId, passiveParts[0], StringComparison.Ordinal)
                    && string.Equals(row.BoardId, passiveParts[1], StringComparison.Ordinal)
                    && string.Equals(row.NodeId, passiveParts[2], StringComparison.Ordinal)).ToArray();
                if (nodes.Length != 1)
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var node = nodes[0];
                return new SealedAppliedTargetResult(
                    Targets(
                        new SealedCommitTarget("passive", node.NodeId),
                        new SealedCommitTarget("skill", node.GrantedSkillId)),
                    node.FamilyIds,
                    SealedCommitJoinIssue.None);

            case "refit":
                var refitParts = selectedAction.Split(new[] { ':' }, StringSplitOptions.None);
                if (refitParts.Length != 2
                    || string.IsNullOrEmpty(refitParts[0])
                    || !TryCanonicalInteger(refitParts[1], nonNegative: true, out var slotIndex))
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var items = join.RefitItems.Where(row => string.Equals(
                    row.ItemInstanceId,
                    refitParts[0],
                    StringComparison.Ordinal)).ToArray();
                if (items.Length != 1)
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                var slots = items[0].Slots.Where(slot => slot.SlotIndex == slotIndex).ToArray();
                if (slots.Length != 1)
                {
                    return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
                }

                return new SealedAppliedTargetResult(
                    Targets(
                        new SealedCommitTarget("item", items[0].ItemId),
                        new SealedCommitTarget("item_instance", items[0].ItemInstanceId)),
                    items[0].FamilyIds
                        .Concat(new[] { slots[0].CurrentAffixId })
                        .Where(value => !string.IsNullOrEmpty(value))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    SealedCommitJoinIssue.None);

            default:
                return SealedAppliedTargetResult.Empty(SealedCommitJoinIssue.ActionJoinFailure);
        }
    }

    public static SealedCommitExactMatch FirstExactMatch(
        IReadOnlyList<string> trackTokens,
        IReadOnlyList<SealedCommitTarget> targets)
    {
        foreach (var trackToken in trackTokens ?? Array.Empty<string>())
        {
            foreach (var target in targets ?? Array.Empty<SealedCommitTarget>())
            {
                if (string.Equals(trackToken, target.Id, StringComparison.Ordinal)
                    || string.Equals(trackToken, target.WireToken, StringComparison.Ordinal))
                {
                    return new SealedCommitExactMatch(trackToken, target);
                }
            }
        }

        return null;
    }

    public static bool HasFamilyMatch(
        IReadOnlyList<string> trackTokens,
        IReadOnlyList<string> familyIds)
    {
        var family = new HashSet<string>(familyIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        return (trackTokens ?? Array.Empty<string>())
            .Select(TrackTokenIdPart)
            .Any(family.Contains);
    }

    public static bool TrackTokenAdmissible(
        string token,
        HashSet<string> visibleIds,
        out bool unknownKind)
    {
        unknownKind = false;
        if (token == null || visibleIds == null)
        {
            return false;
        }

        var separator = token.IndexOf(':');
        if (separator < 0)
        {
            return visibleIds.Contains(token);
        }

        var kind = token.Substring(0, separator);
        if (!TrackKinds.Contains(kind))
        {
            unknownKind = true;
            return false;
        }

        return visibleIds.Contains(token.Substring(separator + 1));
    }

    public static string TrackTokenIdPart(string token)
    {
        if (token == null)
        {
            return string.Empty;
        }

        var separator = token.IndexOf(':');
        return separator < 0 ? token : token.Substring(separator + 1);
    }

    private static SealedAppliedTargetResult One(
        SealedCommitTarget target,
        IReadOnlyList<string> familyIds)
        => new(new[] { target }, familyIds ?? Array.Empty<string>(), SealedCommitJoinIssue.None);

    private static IReadOnlyList<SealedCommitTarget> Targets(params SealedCommitTarget[] values)
        => (values ?? Array.Empty<SealedCommitTarget>())
            .Where(value => value != null && !string.IsNullOrEmpty(value.Id))
            .ToArray();

    private static bool TryCanonicalInteger(string value, bool nonNegative, out int result)
    {
        if (!int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out result)
            || !string.Equals(value, result.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            || nonNegative && result < 0)
        {
            result = 0;
            return false;
        }

        return true;
    }
}
