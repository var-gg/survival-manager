using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SM.Core.Content;
using SM.Core.Results;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record RefitItemState(
    string ItemBaseId,
    string StableItemKey,
    ItemRarityTierValue Grade,
    IReadOnlyList<string> AffixIds,
    IReadOnlyDictionary<string, float>? AffixMagnitudes,
    int RefitLevel);

public sealed record RefitChapterEconomy(
    string ChapterId,
    int FirstFarmRunEcho,
    double MeanGrade);

public sealed record RefitQuote(
    bool CanPurchase,
    bool RefitMaxed,
    OperationFailure? Failure,
    ulong CurrentPercentileQ64,
    int CurrentRefitLevel,
    int TargetRefitLevel,
    ulong TargetFloorQ64,
    int EchoCost)
{
    public string Reason => Failure?.Diagnostic ?? string.Empty;

    public static RefitQuote Unavailable(OperationFailure failure, int currentRefitLevel = 0)
        => new(
            CanPurchase: false,
            RefitMaxed: false,
            Failure: failure,
            CurrentPercentileQ64: 0UL,
            CurrentRefitLevel: Math.Max(0, currentRefitLevel),
            TargetRefitLevel: Math.Max(0, currentRefitLevel),
            TargetFloorQ64: 0UL,
            EchoCost: 0);
}

public sealed record RefitExecutionResult(
    bool Applied,
    OperationFailure? Failure,
    RefitQuote Quote,
    ulong ResultPercentileQ64,
    IReadOnlyList<string> AffixIds,
    IReadOnlyDictionary<string, float> AffixMagnitudes)
{
    public bool InvariantFailure => Failure?.IsInvariantViolation == true;
    public string Error => Failure?.Diagnostic ?? string.Empty;

    public static RefitExecutionResult NoChange(
        RefitQuote quote,
        IReadOnlyList<string> affixIds,
        IReadOnlyDictionary<string, float>? affixMagnitudes = null,
        OperationFailure? failure = null)
        => new(
            Applied: false,
            Failure: failure ?? quote.Failure,
            Quote: quote,
            ResultPercentileQ64: quote.CurrentPercentileQ64,
            AffixIds: affixIds.ToArray(),
            AffixMagnitudes: (affixMagnitudes
                              ?? new Dictionary<string, float>(StringComparer.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
}

/// <summary>
/// Item roll-quality Refit. It preserves affix identity and rerolls only instance
/// magnitudes, then validates the complete result before a caller may commit it.
/// This service never mutates persistence or currency and never falls back to the
/// retired affix-identity behavior.
/// </summary>
public sealed class RefitService
{
    private readonly ISessionContentLookup _lookup;
    private readonly RefitBalanceTemplate _balance;

    public RefitService(
        ISessionContentLookup lookup,
        RefitBalanceTemplate balance)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        if (_balance.FloorScheduleQ64 is not { Count: > 0 })
        {
            throw new ArgumentException(
                "Refit balance must contain a generated floor schedule.",
                nameof(balance));
        }
    }

    public RefitQuote QuoteNextEffective(
        RefitItemState item,
        RefitChapterEconomy chapterEconomy)
        => QuoteNextEffective(
            item,
            chapterEconomy,
            CraftOperationKindValue.Reforge,
            lockedAffixCount: 0);

    public RefitQuote QuoteSealNextEffective(
        RefitItemState item,
        RefitChapterEconomy chapterEconomy,
        IReadOnlyCollection<string> sealedAffixIds)
    {
        if (!TryNormalizeSealedAffixes(
                item,
                sealedAffixIds,
                out var canonicalSealedAffixIds,
                out var failure))
        {
            return RefitQuote.Unavailable(failure!, item?.RefitLevel ?? 0);
        }

        return QuoteNextEffective(
            item,
            chapterEconomy,
            CraftOperationKindValue.Seal,
            canonicalSealedAffixIds.Count);
    }

    private RefitQuote QuoteNextEffective(
        RefitItemState item,
        RefitChapterEconomy chapterEconomy,
        CraftOperationKindValue operation,
        int lockedAffixCount)
    {
        if (!TryResolveItem(item, out var currentQualityQ64, out var failure))
        {
            return RefitQuote.Unavailable(failure!, item?.RefitLevel ?? 0);
        }

        if (!TryValidateOperationAllowed(item, operation, out failure))
        {
            return RefitQuote.Unavailable(failure!, item.RefitLevel);
        }

        if (item.RefitLevel < 0)
        {
            return RefitQuote.Unavailable(
                OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitLevelInvalid,
                    $"Refit level '{item.RefitLevel}' cannot be negative for item '{item.StableItemKey}'."),
                item.RefitLevel);
        }

        if (chapterEconomy == null
            || string.IsNullOrWhiteSpace(chapterEconomy.ChapterId)
            || chapterEconomy.FirstFarmRunEcho <= 0
            || !double.IsFinite(chapterEconomy.MeanGrade))
        {
            return RefitQuote.Unavailable(
                OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitChapterEconomyUnavailable,
                    $"Chapter economy was unavailable or invalid for chapter '{chapterEconomy?.ChapterId ?? "<null>"}'."),
                item.RefitLevel);
        }

        for (var scheduleIndex = item.RefitLevel;
             scheduleIndex < _balance.FloorScheduleQ64.Count;
             scheduleIndex++)
        {
            var floorQ64 = _balance.FloorScheduleQ64[scheduleIndex];
            if (floorQ64 <= currentQualityQ64)
            {
                continue;
            }

            var targetLevel = scheduleIndex + 1;
            int echoCost;
            try
            {
                echoCost = operation == CraftOperationKindValue.Seal
                    ? RefitCostCurve.GetSealBundleCost(
                        _balance,
                        chapterEconomy.FirstFarmRunEcho,
                        item.RefitLevel,
                        targetLevel,
                        item.Grade,
                        chapterEconomy.MeanGrade,
                        lockedAffixCount)
                    : RefitCostCurve.GetBundleCost(
                        _balance,
                        chapterEconomy.FirstFarmRunEcho,
                        item.RefitLevel,
                        targetLevel,
                        item.Grade,
                        chapterEconomy.MeanGrade);
            }
            catch (Exception exception) when (
                exception is ArgumentOutOfRangeException or OverflowException)
            {
                return RefitQuote.Unavailable(
                    OperationFailure.Invariant(
                        MetaOperationFailureCodes.RefitCostResolutionFailed,
                        $"Refit cost resolution failed for item '{item.StableItemKey}', operation '{operation}', target level '{targetLevel}': {exception}"),
                    item.RefitLevel);
            }

            return new RefitQuote(
                CanPurchase: true,
                RefitMaxed: false,
                Failure: null,
                CurrentPercentileQ64: currentQualityQ64,
                CurrentRefitLevel: item.RefitLevel,
                TargetRefitLevel: targetLevel,
                TargetFloorQ64: floorQ64,
                EchoCost: echoCost);
        }

        return Maxed(
            item,
            currentQualityQ64);
    }

    public RefitExecutionResult RefitNextEffective(
        RefitItemState item,
        RefitChapterEconomy chapterEconomy,
        ulong stableCommandSeed)
    {
        var quote = QuoteNextEffective(item, chapterEconomy);
        if (!quote.CanPurchase)
        {
            return RefitExecutionResult.NoChange(
                quote,
                item?.AffixIds ?? Array.Empty<string>(),
                item?.AffixMagnitudes);
        }

        if (_lookup.Snapshot.AffixCatalog is not { } catalog)
        {
            return InvariantFailure(
                item,
                quote,
                OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitAffixCatalogUnavailable,
                    $"Affix catalog was unavailable while executing Refit for item '{item.StableItemKey}'."));
        }

        IReadOnlyDictionary<string, float> generatedMagnitudes;
        try
        {
            generatedMagnitudes = RefitRollQuality.RerollToFloor(
                _lookup.Snapshot,
                item.AffixIds,
                RefitSeedDerivation.Derive(
                    stableCommandSeed,
                    item.StableItemKey,
                    quote.TargetRefitLevel,
                    _balance.RulesVersion),
                quote.TargetFloorQ64);
        }
        catch (Exception exception)
        {
            return InvariantFailure(
                item,
                quote,
                OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitGenerationFailed,
                    $"Magnitude Refit generation failed for item '{item.StableItemKey}': {exception}"));
        }

        var generatedAffixIds = item.AffixIds.ToArray();
        if (!ValidatePostconditions(
                item,
                quote,
                generatedAffixIds,
                generatedMagnitudes,
                out var resultQualityQ64,
                out var invariantFailure))
        {
            return InvariantFailure(item, quote, invariantFailure!);
        }

        return new RefitExecutionResult(
            Applied: true,
            Failure: null,
            Quote: quote,
            ResultPercentileQ64: resultQualityQ64,
            AffixIds: generatedAffixIds,
            AffixMagnitudes: generatedMagnitudes);
    }

    public RefitExecutionResult SealNextEffective(
        RefitItemState item,
        RefitChapterEconomy chapterEconomy,
        IReadOnlyCollection<string> sealedAffixIds,
        int attemptIndex,
        ulong stableCommandSeed)
    {
        if (attemptIndex < 0)
        {
            return RefitExecutionResult.NoChange(
                RefitQuote.Unavailable(
                    OperationFailure.Invariant(
                        MetaOperationFailureCodes.RefitSealAttemptInvalid,
                        $"Seal attempt index '{attemptIndex}' cannot be negative for item '{item?.StableItemKey ?? "<null>"}'."),
                    item?.RefitLevel ?? 0),
                item?.AffixIds ?? Array.Empty<string>(),
                item?.AffixMagnitudes);
        }

        if (!TryNormalizeSealedAffixes(
                item,
                sealedAffixIds,
                out var canonicalSealedAffixIds,
                out var normalizeFailure))
        {
            return RefitExecutionResult.NoChange(
                RefitQuote.Unavailable(
                    normalizeFailure!,
                    item?.RefitLevel ?? 0),
                item?.AffixIds ?? Array.Empty<string>(),
                item?.AffixMagnitudes);
        }

        var quote = QuoteNextEffective(
            item,
            chapterEconomy,
            CraftOperationKindValue.Seal,
            canonicalSealedAffixIds.Count);
        if (!quote.CanPurchase)
        {
            return RefitExecutionResult.NoChange(
                quote,
                item.AffixIds,
                item.AffixMagnitudes);
        }

        IReadOnlyDictionary<string, float> generatedMagnitudes;
        try
        {
            generatedMagnitudes = canonicalSealedAffixIds.Count == 0
                ? RefitRollQuality.RerollToFloor(
                    _lookup.Snapshot,
                    item.AffixIds,
                    RefitSeedDerivation.Derive(
                        stableCommandSeed,
                        item.StableItemKey,
                        quote.TargetRefitLevel,
                        _balance.RulesVersion),
                    quote.TargetFloorQ64)
                : RefitRollQuality.RerollUnlockedToFloor(
                    _lookup.Snapshot,
                    item.AffixIds,
                    item.AffixMagnitudes,
                    canonicalSealedAffixIds,
                    RefitSeedDerivation.DeriveSeal(
                        stableCommandSeed,
                        item.StableItemKey,
                        quote.TargetRefitLevel,
                        attemptIndex,
                        _balance.RulesVersion,
                        canonicalSealedAffixIds),
                    quote.TargetFloorQ64);
        }
        catch (Exception exception)
        {
            return InvariantFailure(
                item,
                quote,
                OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitGenerationFailed,
                    $"Magnitude Seal generation failed for item '{item.StableItemKey}': {exception}"));
        }

        var generatedAffixIds = item.AffixIds.ToArray();
        if (!ValidatePostconditions(
                item,
                quote,
                generatedAffixIds,
                generatedMagnitudes,
                out var resultQualityQ64,
                out var invariantFailure))
        {
            return InvariantFailure(item, quote, invariantFailure!);
        }

        if (!ValidateSealedMagnitudes(
                item,
                canonicalSealedAffixIds,
                generatedMagnitudes,
                out invariantFailure))
        {
            return InvariantFailure(item, quote, invariantFailure!);
        }

        return new RefitExecutionResult(
            Applied: true,
            Failure: null,
            Quote: quote,
            ResultPercentileQ64: resultQualityQ64,
            AffixIds: generatedAffixIds,
            AffixMagnitudes: generatedMagnitudes);
    }

    private bool TryResolveItem(
        RefitItemState? item,
        out ulong currentQualityQ64,
        out OperationFailure? failure)
    {
        currentQualityQ64 = 0UL;
        failure = null;
        if (item == null
            || string.IsNullOrWhiteSpace(item.ItemBaseId)
            || string.IsNullOrWhiteSpace(item.StableItemKey)
            || item.AffixIds == null)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitItemStateIncomplete,
                "Refit item state is incomplete.");
            return false;
        }

        if (!Enum.IsDefined(typeof(ItemRarityTierValue), item.Grade))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitInvalidGrade,
                $"Refit item grade '{item.Grade}' is invalid for item '{item.StableItemKey}'.");
            return false;
        }

        if (!ValidateAffixLegality(item, item.AffixIds, out failure))
        {
            return false;
        }

        if (_lookup.Snapshot.AffixCatalog is not { } catalog)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitAffixCatalogUnavailable,
                $"Affix catalog was unavailable while measuring item '{item.StableItemKey}'.");
            return false;
        }

        try
        {
            currentQualityQ64 = RefitRollQuality.ToQ64(
                RefitRollQuality.Measure(
                    _lookup.Snapshot,
                    item.AffixIds,
                    item.AffixMagnitudes));
            return true;
        }
        catch (Exception exception)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitQualityResolutionFailed,
                $"Roll quality resolution failed for item '{item.StableItemKey}': {exception}");
            return false;
        }
    }

    private bool ValidatePostconditions(
        RefitItemState item,
        RefitQuote quote,
        IReadOnlyList<string> generatedAffixIds,
        IReadOnlyDictionary<string, float> generatedMagnitudes,
        out ulong resultQualityQ64,
        out OperationFailure? failure)
    {
        resultQualityQ64 = 0UL;
        failure = null;
        if (!generatedAffixIds.SequenceEqual(item.AffixIds, StringComparer.Ordinal))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitPostconditionFailed,
                $"Refit changed affix identity for item '{item.StableItemKey}'.");
            return false;
        }

        if (!ValidateAffixLegality(item, generatedAffixIds, out failure))
        {
            return false;
        }

        if (generatedMagnitudes.Count != generatedAffixIds.Count
            || generatedAffixIds.Any(id => !generatedMagnitudes.ContainsKey(id)))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitPostconditionFailed,
                $"Refit magnitude output did not match the affix identity set atomically for item '{item.StableItemKey}'.");
            return false;
        }

        try
        {
            var catalog = _lookup.Snapshot.AffixCatalog
                          ?? throw new InvalidOperationException(
                              "Affix catalog is unavailable.");
            resultQualityQ64 = RefitRollQuality.ToQ64(
                RefitRollQuality.Measure(
                    _lookup.Snapshot,
                    generatedAffixIds,
                    generatedMagnitudes));
        }
        catch (Exception exception)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitPostconditionFailed,
                $"Refit magnitude range validation failed for item '{item.StableItemKey}': {exception}");
            return false;
        }

        if (resultQualityQ64 < quote.CurrentPercentileQ64)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitPostconditionFailed,
                $"Refit roll quality regressed from "
                + $"{RefitRollQuality.FromQ64(quote.CurrentPercentileQ64):R} to "
                + $"{RefitRollQuality.FromQ64(resultQualityQ64):R} for item '{item.StableItemKey}'.");
            return false;
        }

        if (resultQualityQ64 < quote.TargetFloorQ64)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitPostconditionFailed,
                $"Refit result roll quality is below the purchased floor for item '{item.StableItemKey}'.");
            return false;
        }

        return true;
    }

    private bool ValidateAffixLegality(
        RefitItemState item,
        IReadOnlyList<string> affixIds,
        out OperationFailure? failure)
    {
        failure = null;
        if (_lookup.Snapshot.ItemCatalog is not { } itemCatalog
            || !itemCatalog.TryGetValue(item.ItemBaseId, out var itemTemplate))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitItemBaseUnknown,
                $"Unknown item base '{item.ItemBaseId}' for item '{item.StableItemKey}'.");
            return false;
        }

        if (_lookup.Snapshot.AffixCatalog is not { } catalog)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitAffixCatalogUnavailable,
                $"Affix catalog was unavailable while validating item '{item.StableItemKey}'.");
            return false;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenExclusiveGroups = new HashSet<string>(StringComparer.Ordinal);
        var tiers = new List<string>();
        foreach (var affixId in affixIds)
        {
            if (string.IsNullOrWhiteSpace(affixId)
                || !seenIds.Add(affixId)
                || !catalog.TryGetValue(affixId, out var affix))
            {
                failure = OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitAffixSetInvalid,
                    $"Refit found an unknown or duplicate affix '{affixId}' on item '{item.StableItemKey}'.");
                return false;
            }

            if (affix.AllowedSlotTypes is { Count: > 0 }
                && !affix.AllowedSlotTypes.Contains(
                    itemTemplate.SlotType,
                    StringComparer.Ordinal))
            {
                failure = OperationFailure.Refusal(
                    MetaOperationFailureCodes.RefitAffixIllegalForSlot,
                    $"Affix '{affixId}' is illegal for slot '{itemTemplate.SlotType}' on item '{item.StableItemKey}'.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(affix.ExclusiveGroupId)
                && !seenExclusiveGroups.Add(affix.ExclusiveGroupId))
            {
                failure = OperationFailure.Refusal(
                    MetaOperationFailureCodes.RefitAffixExclusiveConflict,
                    $"Affix '{affixId}' conflicts in exclusive group "
                    + $"'{affix.ExclusiveGroupId}' on item '{item.StableItemKey}'.");
                return false;
            }

            tiers.Add(affix.Tier);
        }

        if (!FollowsTierStepSequence(item.Grade, tiers))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitTierSequenceInvalid,
                $"Refit item '{item.StableItemKey}' does not follow grade '{item.Grade}' tier-step sequence.");
            return false;
        }

        return true;
    }

    private bool TryValidateOperationAllowed(
        RefitItemState item,
        CraftOperationKindValue operation,
        out OperationFailure? failure)
    {
        failure = null;
        if (_lookup.Snapshot.ItemCatalog is not { } itemCatalog
            || !itemCatalog.TryGetValue(item.ItemBaseId, out var itemTemplate))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitItemBaseUnknown,
                $"Unknown item base '{item.ItemBaseId}' for item '{item.StableItemKey}'.");
            return false;
        }

        if (itemTemplate.AllowedCraftOperations is not { Count: > 0 }
            || !itemTemplate.AllowedCraftOperations.Contains(operation))
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitOperationNotAllowed,
                $"Item base '{item.ItemBaseId}' does not allow operation '{operation}'.",
                operation.ToString());
            return false;
        }

        return true;
    }

    private static bool TryNormalizeSealedAffixes(
        RefitItemState? item,
        IReadOnlyCollection<string>? sealedAffixIds,
        out IReadOnlyList<string> canonicalSealedAffixIds,
        out OperationFailure? failure)
    {
        canonicalSealedAffixIds = Array.Empty<string>();
        failure = null;
        if (item == null || item.AffixIds == null)
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.RefitItemStateIncomplete,
                "Seal item state is incomplete.");
            return false;
        }

        if (sealedAffixIds == null)
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitSealSelectionRequired,
                $"Seal affix selection is required for item '{item.StableItemKey}'.");
            return false;
        }

        var sealedSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sealedAffixId in sealedAffixIds)
        {
            if (string.IsNullOrWhiteSpace(sealedAffixId)
                || !sealedSet.Add(sealedAffixId))
            {
                failure = OperationFailure.Refusal(
                    MetaOperationFailureCodes.RefitSealSelectionInvalid,
                    $"Seal affix selection contains a blank or duplicate id for item '{item.StableItemKey}'.");
                return false;
            }
        }

        if (sealedSet.Count >= item.AffixIds.Count && item.AffixIds.Count > 0)
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitSealAllAffixesLocked,
                $"Seal selection must leave at least one affix unlocked for item '{item.StableItemKey}'.");
            return false;
        }

        var canonical = item.AffixIds
            .Where(sealedSet.Contains)
            .ToArray();
        if (canonical.Length != sealedSet.Count)
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitSealSelectionInvalid,
                $"Seal affix selection contains an id that is not on item '{item.StableItemKey}'.");
            return false;
        }

        canonicalSealedAffixIds = canonical;
        return true;
    }

    private static bool ValidateSealedMagnitudes(
        RefitItemState item,
        IReadOnlyList<string> sealedAffixIds,
        IReadOnlyDictionary<string, float> generatedMagnitudes,
        out OperationFailure? failure)
    {
        failure = null;
        foreach (var sealedAffixId in sealedAffixIds)
        {
            if (item.AffixMagnitudes == null
                || !item.AffixMagnitudes.TryGetValue(sealedAffixId, out var original)
                || !generatedMagnitudes.TryGetValue(sealedAffixId, out var generated))
            {
                failure = OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitSealMagnitudeMissing,
                    $"Seal requires a persisted magnitude for '{sealedAffixId}' on item '{item.StableItemKey}'.");
                return false;
            }

            if (BitConverter.SingleToInt32Bits(original)
                != BitConverter.SingleToInt32Bits(generated))
            {
                failure = OperationFailure.Invariant(
                    MetaOperationFailureCodes.RefitSealMagnitudeChanged,
                    $"Seal changed locked affix magnitude '{sealedAffixId}' on item '{item.StableItemKey}'.");
                return false;
            }
        }

        return true;
    }

    private static bool FollowsTierStepSequence(
        ItemRarityTierValue grade,
        IReadOnlyList<string> selectedTiers)
    {
        var expected = new[] { GeneratedItemAffixStateGraph.ImplicitTier }
            .Concat(GeneratedItemAffixStateGraph.GetGradeStepTiers(grade))
            .ToArray();
        var expectedIndex = 0;
        string? priorTier = null;
        foreach (var tier in selectedTiers)
        {
            if (string.Equals(tier, priorTier, StringComparison.Ordinal))
            {
                continue;
            }

            while (expectedIndex < expected.Length
                   && !string.Equals(expected[expectedIndex], tier, StringComparison.Ordinal))
            {
                expectedIndex++;
            }

            if (expectedIndex >= expected.Length)
            {
                return false;
            }

            priorTier = tier;
            expectedIndex++;
        }

        return true;
    }

    private static RefitExecutionResult InvariantFailure(
        RefitItemState item,
        RefitQuote quote,
        OperationFailure failure)
        => new(
            Applied: false,
            Failure: failure,
            Quote: quote,
            ResultPercentileQ64: quote.CurrentPercentileQ64,
            AffixIds: item.AffixIds.ToArray(),
            AffixMagnitudes: (item.AffixMagnitudes
                              ?? new Dictionary<string, float>(StringComparer.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    private static RefitQuote Maxed(
        RefitItemState item,
        ulong currentQualityQ64)
        => new(
            CanPurchase: false,
            RefitMaxed: true,
            Failure: OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitQualityMaxed,
                $"Item '{item.StableItemKey}' is already at or above every effective Refit floor."),
            CurrentPercentileQ64: currentQualityQ64,
            CurrentRefitLevel: item.RefitLevel,
            TargetRefitLevel: item.RefitLevel,
            TargetFloorQ64: currentQualityQ64,
            EchoCost: 0);
}

public static class RefitSeedDerivation
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static int Derive(
        ulong stableCommandSeed,
        string stableItemKey,
        int targetRefitLevel,
        int rulesVersion)
    {
        var hash = OffsetBasis;
        AppendPart(ref hash, stableCommandSeed.ToString(CultureInfo.InvariantCulture));
        AppendPart(ref hash, stableItemKey ?? string.Empty);
        AppendPart(ref hash, targetRefitLevel.ToString(CultureInfo.InvariantCulture));
        AppendPart(ref hash, rulesVersion.ToString(CultureInfo.InvariantCulture));
        AppendPart(ref hash, "REFIT");
        return (int)(hash & int.MaxValue);
    }

    public static int DeriveSeal(
        ulong stableCommandSeed,
        string stableItemKey,
        int targetRefitLevel,
        int attemptIndex,
        int rulesVersion,
        IReadOnlyList<string> canonicalSealedAffixIds)
    {
        var hash = OffsetBasis;
        AppendPart(ref hash, stableCommandSeed.ToString(CultureInfo.InvariantCulture));
        AppendPart(ref hash, stableItemKey ?? string.Empty);
        AppendPart(ref hash, targetRefitLevel.ToString(CultureInfo.InvariantCulture));
        AppendPart(ref hash, attemptIndex.ToString(CultureInfo.InvariantCulture));
        AppendPart(ref hash, rulesVersion.ToString(CultureInfo.InvariantCulture));
        foreach (var sealedAffixId in canonicalSealedAffixIds)
        {
            AppendPart(ref hash, sealedAffixId ?? string.Empty);
        }

        AppendPart(ref hash, "SEAL");
        return (int)(hash & int.MaxValue);
    }

    private static void AppendPart(ref ulong hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendByte(ref hash, (byte)(bytes.Length & 0xff));
        AppendByte(ref hash, (byte)((bytes.Length >> 8) & 0xff));
        AppendByte(ref hash, (byte)((bytes.Length >> 16) & 0xff));
        AppendByte(ref hash, (byte)((bytes.Length >> 24) & 0xff));
        foreach (var valueByte in bytes)
        {
            AppendByte(ref hash, valueByte);
        }
    }

    private static void AppendByte(ref ulong hash, byte value)
    {
        unchecked
        {
            hash ^= value;
            hash *= Prime;
        }
    }
}
