using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>CombatContentSnapshot 하나만으로 동작하는 .NET/Unity 공용 세션 content lookup.</summary>
public sealed class SnapshotSessionContentLookup : ISessionContentLookup
{
    public SnapshotSessionContentLookup(CombatContentSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public CombatContentSnapshot Snapshot { get; }

    public bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error)
    {
        snapshot = Snapshot;
        error = string.Empty;
        return true;
    }

    public IReadOnlyList<string> GetCanonicalArchetypeIds() =>
        ResolveOrder(
            Snapshot.SessionContentOrder?.ArchetypeIds,
            Snapshot.FirstPlayableSlice?.UnitBlueprintIds,
            Snapshot.Archetypes.Keys);

    public IReadOnlyList<string> GetCanonicalItemIds() =>
        ResolveOrder(
            Snapshot.SessionContentOrder?.ItemIds,
            null,
            Snapshot.ItemCatalog?.Keys ?? Snapshot.ItemPackages.Keys);

    public IReadOnlyList<string> GetCanonicalAffixIds() =>
        ResolveOrder(
            Snapshot.SessionContentOrder?.AffixIds,
            Snapshot.FirstPlayableSlice?.AffixIds,
            Snapshot.AffixCatalog?.Keys ?? Snapshot.AffixPackages.Keys);

    public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds() =>
        ResolveOrder(
            Snapshot.SessionContentOrder?.TemporaryAugmentIds,
            Snapshot.FirstPlayableSlice?.TemporaryAugmentIds,
            Snapshot.AugmentCatalog.Values
                .Where(value => !value.IsPermanent)
                .Select(value => value.Id));

    public IReadOnlyList<string> GetCanonicalPermanentAugmentIds() =>
        ResolveOrder(
            Snapshot.SessionContentOrder?.PermanentAugmentIds,
            Snapshot.FirstPlayableSlice?.PermanentAugmentIds,
            Snapshot.AugmentCatalog.Values
                .Where(value => value.IsPermanent)
                .Select(value => value.Id));

    public IReadOnlyList<string> GetCanonicalPassiveBoardIds() =>
        ResolveOrder(
            Snapshot.SessionContentOrder?.PassiveBoardIds,
            Snapshot.FirstPlayableSlice?.PassiveBoardIds,
            Snapshot.PassiveNodes.Values.Select(value => value.BoardId));

    public IReadOnlyList<string> GetCanonicalSynergyFamilyIds() =>
        ResolveOrder(
            Snapshot.SessionContentOrder?.SynergyFamilyIds,
            Snapshot.FirstPlayableSlice?.SynergyFamilyIds,
            Snapshot.SynergyCatalog.Keys);

    public FirstPlayableSliceDefinition? GetFirstPlayableSlice() => Snapshot.FirstPlayableSlice;

    public bool TryGetTraitIds(
        string archetypeId,
        out IReadOnlyList<string> positiveTraitIds,
        out IReadOnlyList<string> negativeTraitIds)
    {
        if (Snapshot.ArchetypeTraitPools is { } pools
            && pools.TryGetValue(archetypeId, out var pool))
        {
            positiveTraitIds = pool.PositiveTraitIds;
            negativeTraitIds = pool.NegativeTraitIds;
            return positiveTraitIds.Count > 0 && negativeTraitIds.Count > 0;
        }

        positiveTraitIds = Array.Empty<string>();
        negativeTraitIds = Array.Empty<string>();
        return false;
    }

    public string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex)
    {
        if (!string.IsNullOrWhiteSpace(archetypeId) && Snapshot.Archetypes.ContainsKey(archetypeId))
        {
            return archetypeId;
        }

        var exactMatch = Snapshot.Archetypes.Values.FirstOrDefault(value =>
            string.Equals(value.RaceId, raceId, StringComparison.Ordinal)
            && string.Equals(value.ClassId, classId, StringComparison.Ordinal));
        if (exactMatch != null)
        {
            return exactMatch.Id;
        }

        var ordered = GetCanonicalArchetypeIds();
        if (ordered.Count == 0)
        {
            throw new InvalidOperationException("전투 archetype canonical 목록이 비어 있습니다.");
        }

        return ordered[Math.Abs(fallbackIndex) % ordered.Count];
    }

    public string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex)
        => NormalizeTraitId(archetypeId, traitId, fallbackIndex, positive: true);

    public string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex)
        => NormalizeTraitId(archetypeId, traitId, fallbackIndex, positive: false);

    public string NormalizeItemBaseId(string itemBaseId, int fallbackIndex)
        => NormalizeCatalogId(
            itemBaseId,
            fallbackIndex,
            GetCanonicalItemIds(),
            id => Snapshot.ItemCatalog?.ContainsKey(id) ?? Snapshot.ItemPackages.ContainsKey(id));

    public string NormalizeAffixId(string affixId, int fallbackIndex)
        => NormalizeCatalogId(
            affixId,
            fallbackIndex,
            GetCanonicalAffixIds(),
            id => Snapshot.AffixCatalog?.ContainsKey(id) ?? Snapshot.AffixPackages.ContainsKey(id));

    public string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex)
    {
        var isTemporary = !string.IsNullOrWhiteSpace(augmentId)
                          && Snapshot.AugmentCatalog.TryGetValue(augmentId, out var augment)
                          && !augment.IsPermanent;
        return NormalizeCatalogId(augmentId, fallbackIndex, GetCanonicalTemporaryAugmentIds(), _ => isTemporary);
    }

    private string NormalizeTraitId(string archetypeId, string traitId, int fallbackIndex, bool positive)
    {
        if (!TryGetTraitIds(archetypeId, out var positives, out var negatives))
        {
            return string.Empty;
        }

        var values = positive ? positives : negatives;
        if (!string.IsNullOrWhiteSpace(traitId) && values.Contains(traitId, StringComparer.Ordinal))
        {
            return traitId;
        }

        return values[Math.Abs(fallbackIndex) % values.Count];
    }

    private static string NormalizeCatalogId(
        string value,
        int fallbackIndex,
        IReadOnlyList<string> canonicalIds,
        Func<string, bool> contains)
    {
        if (!string.IsNullOrWhiteSpace(value) && contains(value))
        {
            return value;
        }

        return canonicalIds.Count == 0
            ? string.Empty
            : canonicalIds[Math.Abs(fallbackIndex) % canonicalIds.Count];
    }

    private static IReadOnlyList<string> ResolveOrder(
        IReadOnlyList<string>? primary,
        IReadOnlyList<string>? secondary,
        IEnumerable<string> fallback)
    {
        var values = primary is { Count: > 0 }
            ? primary
            : secondary is { Count: > 0 }
                ? secondary
                : fallback.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
