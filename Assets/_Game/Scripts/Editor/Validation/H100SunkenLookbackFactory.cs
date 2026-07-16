using System;
using System.Collections.Generic;
using System.Linq;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>직전 reward 선택과 그 직후 합법 recruit 한 번을 열거해 one-site lookback profile을 만든다.</summary>
internal static class H100SunkenLookbackFactory
{
    public static IReadOnlyList<H100SunkenLookbackVariant> Build(
        RuntimeCombatContentLookup lookup,
        H100SunkenCapturedArrival arrival)
    {
        if (arrival.Lookback == null)
        {
            return Array.Empty<H100SunkenLookbackVariant>();
        }

        var variants = new List<H100SunkenLookbackVariant>();
        var arrivalHeroIds = H100ProfileSnapshotCodec.Restore(arrival.ProfileSnapshot).Heroes
            .Select(value => value.HeroId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var reward in arrival.Lookback.RewardOptions.OrderBy(value => value.OptionIndex))
        {
            var rewardSession = H100SessionDriver.CreateSession(
                lookup,
                H100ProfileSnapshotCodec.Restore(arrival.Lookback.ProfileSnapshot));
            if (!rewardSession.ApplyRewardChoice(reward.OptionIndex))
            {
                continue;
            }

            rewardSession.ReturnToTownAfterReward();
            var offers = rewardSession.RecruitOffers
                .Select((offer, index) => new { Offer = offer, Index = index })
                .ToArray();
            AddTargetVariant(
                variants,
                rewardSession,
                arrivalHeroIds,
                $"reward-{reward.OptionIndex:D2}",
                reward.OptionIndex,
                reward.PayloadId,
                arrival.Lookback.ProfileSnapshot,
                recruitOfferIndex: -1);

            foreach (var offer in offers)
            {
                var recruitSession = H100SessionDriver.CreateSession(
                    lookup,
                    H100ProfileSnapshotCodec.Restore(arrival.Lookback.ProfileSnapshot));
                if (!recruitSession.ApplyRewardChoice(reward.OptionIndex))
                {
                    continue;
                }

                recruitSession.ReturnToTownAfterReward();
                var heroIdsBefore = recruitSession.Profile.Heroes
                    .Select(value => value.HeroId)
                    .ToHashSet(StringComparer.Ordinal);
                var recruitResult = recruitSession.Recruit(offer.Index);
                if (!recruitResult.IsSuccess)
                {
                    continue;
                }

                var added = recruitSession.Profile.Heroes
                    .Where(value => !heroIdsBefore.Contains(value.HeroId))
                    .OrderBy(value => value.HeroId, StringComparer.Ordinal)
                    .Select(value => value.ArchetypeId)
                    .FirstOrDefault() ?? offer.Offer.UnitBlueprintId;
                AddTargetVariant(
                    variants,
                    recruitSession,
                    arrivalHeroIds,
                    $"reward-{reward.OptionIndex:D2}-recruit-{offer.Index:D2}-{added}",
                    reward.OptionIndex,
                    reward.PayloadId,
                    arrival.Lookback.ProfileSnapshot,
                    offer.Index,
                    added);
            }
        }

        return variants
            .OrderBy(value => value.VariantId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddTargetVariant(
        ICollection<H100SunkenLookbackVariant> variants,
        GameSessionState session,
        ISet<string> arrivalHeroIds,
        string variantId,
        int rewardOptionIndex,
        string rewardPayloadId,
        string sourceProfileSnapshot,
        int recruitOfferIndex,
        string addedRosterArchetypeId = "")
    {
        H100SessionDriver.AdvanceToNextUnclearedSite(session);
        if (!string.Equals(
                session.SelectedCampaignSiteId,
                H100SunkenDiagnosisSettings.TargetSiteId,
                StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(addedRosterArchetypeId))
        {
            addedRosterArchetypeId = session.Profile.Heroes
                .Where(value => !arrivalHeroIds.Contains(value.HeroId))
                .OrderBy(value => value.HeroId, StringComparer.Ordinal)
                .Select(value => value.ArchetypeId)
                .FirstOrDefault() ?? string.Empty;
        }

        variants.Add(new H100SunkenLookbackVariant(
            variantId,
            H100ProfileSnapshotCodec.Capture(session.Profile),
            sourceProfileSnapshot,
            recruitOfferIndex,
            addedRosterArchetypeId,
            rewardOptionIndex,
            rewardPayloadId));
    }
}
