using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record PermanentAugmentUnlockResolution(
    bool HasUnlock,
    string UnlockAugmentId,
    string FamilyId)
{
    public static PermanentAugmentUnlockResolution None { get; } = new(false, string.Empty, string.Empty);
}

public static class PermanentAugmentProgressionService
{
    public static PermanentAugmentUnlockResolution ResolvePendingUnlock(
        string temporaryAugmentId,
        IEnumerable<AugmentCatalogEntry> augmentDefinitions,
        IReadOnlyCollection<string> knownPermanentAugmentIds)
    {
        if (string.IsNullOrWhiteSpace(temporaryAugmentId))
        {
            return PermanentAugmentUnlockResolution.None;
        }

        var definitions = augmentDefinitions?
            .Where(definition => !ReferenceEquals(definition, null) && !string.IsNullOrWhiteSpace(definition.Id))
            .GroupBy(definition => definition.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList() ?? new List<AugmentCatalogEntry>();

        var selectedTemporary = definitions.FirstOrDefault(definition =>
            string.Equals(definition.Id, temporaryAugmentId, StringComparison.Ordinal));
        if (selectedTemporary == null
            || selectedTemporary.IsPermanent
            || string.IsNullOrWhiteSpace(selectedTemporary.FamilyId))
        {
            return PermanentAugmentUnlockResolution.None;
        }

        var permanentCandidateIds = definitions
            .Where(definition => definition.IsPermanent
                                 && string.Equals(definition.FamilyId, selectedTemporary.FamilyId, StringComparison.Ordinal))
            .Select(definition => definition.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (permanentCandidateIds.Length != 1)
        {
            return PermanentAugmentUnlockResolution.None;
        }

        var unlockAugmentId = permanentCandidateIds[0];
        if (knownPermanentAugmentIds.Any(existingId => string.Equals(existingId, unlockAugmentId, StringComparison.Ordinal)))
        {
            return PermanentAugmentUnlockResolution.None;
        }

        return new PermanentAugmentUnlockResolution(true, unlockAugmentId, selectedTemporary.FamilyId);
    }
}
