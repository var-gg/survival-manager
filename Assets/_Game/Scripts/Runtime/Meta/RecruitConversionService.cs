using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Meta;

public sealed class RecruitConversionService
{
    private readonly Dictionary<string, string> _encounterFamilyToHeroId;
    private readonly Dictionary<string, int> _unlockThresholdByEncounterFamily;

    public RecruitConversionService(
        Dictionary<string, string> encounterFamilyToHeroId,
        Dictionary<string, int> unlockThresholdByEncounterFamily)
    {
        _encounterFamilyToHeroId = encounterFamilyToHeroId != null
            ? new Dictionary<string, string>(encounterFamilyToHeroId, StringComparer.Ordinal)
            : throw new ArgumentNullException(nameof(encounterFamilyToHeroId));
        _unlockThresholdByEncounterFamily = unlockThresholdByEncounterFamily != null
            ? new Dictionary<string, int>(unlockThresholdByEncounterFamily, StringComparer.Ordinal)
            : throw new ArgumentNullException(nameof(unlockThresholdByEncounterFamily));
    }

    public NarrativeProgressRecord ApplyEncounterFamily(
        string encounterFamilyId,
        NarrativeProgressRecord progress,
        out string? newlyUnlockedHeroId)
    {
        if (!_encounterFamilyToHeroId.TryGetValue(encounterFamilyId, out var heroId)
            || !_unlockThresholdByEncounterFamily.TryGetValue(encounterFamilyId, out var threshold))
        {
            newlyUnlockedHeroId = null;
            return progress;
        }

        var conversions = progress.RecruitConversions?.Conversions is { Count: > 0 } existingConversions
            ? new Dictionary<string, int>(existingConversions, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);
        conversions.TryGetValue(encounterFamilyId, out var count);
        count += 1;
        conversions[encounterFamilyId] = count;

        var updatedProgress = progress with
        {
            RecruitConversions = new RecruitConversionStateRecord
            {
                Conversions = conversions,
            },
        };

        if (count < threshold || string.IsNullOrWhiteSpace(heroId))
        {
            newlyUnlockedHeroId = null;
            return updatedProgress;
        }

        var unlockedHeroIds = new HashSet<string>(progress.UnlockedStoryHeroIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (!unlockedHeroIds.Add(heroId))
        {
            newlyUnlockedHeroId = null;
            return updatedProgress;
        }

        newlyUnlockedHeroId = heroId;
        return updatedProgress with
        {
            UnlockedStoryHeroIds = unlockedHeroIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
        };
    }
}
