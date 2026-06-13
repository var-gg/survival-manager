using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record AugmentOfferSchedulePick(int PickIndex, int EncounterIndex, string PreferredBucket);

public static class AugmentOfferScheduleService
{
    private static readonly AugmentOfferSchedulePick[] Picks =
    {
        new(0, 0, "TacticalRewrite"),
        new(1, 4, "HeroRewrite"),
        new(2, 8, "SynergyPact"),
        new(3, 16, "ScalingEngine"),
        new(4, 20, "EconomyAndLoot"),
    };

    public static IReadOnlyList<AugmentOfferSchedulePick> DefaultPicks => Picks;

    public static bool TryResolvePick(
        int encounterIndex,
        int acquiredTemporaryAugmentCount,
        out AugmentOfferSchedulePick pick)
    {
        pick = Picks.FirstOrDefault(candidate => candidate.EncounterIndex == encounterIndex);
        if (pick == null)
        {
            return false;
        }

        if (acquiredTemporaryAugmentCount > pick.PickIndex)
        {
            pick = null!;
            return false;
        }

        return true;
    }

    public static bool IsScheduledEncounter(int encounterIndex) =>
        Picks.Any(pick => pick.EncounterIndex == encounterIndex);
}

public sealed record AugmentOfferContext(
    string PreferredBucket,
    IReadOnlyCollection<string> ActiveTags,
    IReadOnlyCollection<string> AcquiredAugmentIds,
    IReadOnlyCollection<string> PermanentEquippedAugmentIds,
    int Seed,
    int PickIndex,
    int MaxChoices = 3,
    int MaxStatLightChoices = 1);
