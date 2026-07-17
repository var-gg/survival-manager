using System;
using System.Collections.Generic;
using System.Globalization;

namespace SM.HeadlessPolicies;

/// <summary>Town roster observation의 player-visible fact id만 정책 결정에 연결한다.</summary>
public static class HeadlessRosterPolicyEvidence
{
    public const string CampaignContextSignal = "roster.campaign.context";
    public const string WalletSignal = "roster.wallet";
    public const string RecruitSurfaceSignal = "roster.recruit.surface";
    public const string PassiveSurfaceSignal = "roster.passive.surface";
    public const string RefitSurfaceSignal = "roster.refit.surface";

    public static string RecruitOfferSignal(int offerIndex)
        => $"roster.recruit.offer.{offerIndex.ToString(CultureInfo.InvariantCulture)}";

    public static string PassiveHeroSignal(string heroId)
        => $"roster.passive.hero.{heroId}";

    public static string PassiveNodeSignal(string heroId, string nodeId)
        => $"roster.passive.hero.{heroId}.node.{nodeId}";

    public static string RefitSlotSignal(string itemInstanceId, int slotIndex)
        => $"roster.refit.item.{itemInstanceId}.slot.{slotIndex.ToString(CultureInfo.InvariantCulture)}";

    internal static IReadOnlyList<string> ForRecruit(
        HeadlessRosterPolicyObservation observation,
        int offerIndex)
        => Resolve(
            observation,
            offerIndex < 0
                ? new[] { CampaignContextSignal, WalletSignal, RecruitSurfaceSignal }
                : new[] { CampaignContextSignal, WalletSignal, RecruitSurfaceSignal, RecruitOfferSignal(offerIndex) });

    internal static IReadOnlyList<string> ForPassive(
        HeadlessRosterPolicyObservation observation,
        string heroId,
        string nodeId)
        => Resolve(
            observation,
            string.IsNullOrWhiteSpace(heroId) || string.IsNullOrWhiteSpace(nodeId)
                ? new[] { CampaignContextSignal, PassiveSurfaceSignal }
                : new[] { CampaignContextSignal, PassiveSurfaceSignal, PassiveHeroSignal(heroId), PassiveNodeSignal(heroId, nodeId) });

    internal static IReadOnlyList<string> ForRefit(
        HeadlessRosterPolicyObservation observation,
        string itemInstanceId,
        int slotIndex)
        => Resolve(
            observation,
            string.IsNullOrWhiteSpace(itemInstanceId) || slotIndex < 0
                ? new[] { CampaignContextSignal, WalletSignal, RefitSurfaceSignal }
                : new[] { CampaignContextSignal, WalletSignal, RefitSurfaceSignal, RefitSlotSignal(itemInstanceId, slotIndex) });

    private static IReadOnlyList<string> Resolve(
        HeadlessRosterPolicyObservation observation,
        IReadOnlyList<string> signalKeys)
    {
        var result = new string[signalKeys.Count];
        for (var index = 0; index < signalKeys.Count; index++)
        {
            var key = signalKeys[index];
            if (!observation.EvidenceFactIdsBySignal.TryGetValue(key, out var factId)
                || string.IsNullOrWhiteSpace(factId))
            {
                throw new HeadlessPolicyEvidenceException(
                    $"Player-visible roster evidence signal '{key}' was not projected before policy execution.");
            }

            result[index] = factId;
        }

        return result;
    }
}
