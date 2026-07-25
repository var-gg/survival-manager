using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SM.Core.Content;
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
    string Reason,
    ulong CurrentPercentileQ64,
    int CurrentRefitLevel,
    int TargetRefitLevel,
    ulong TargetFloorQ64,
    int EchoCost)
{
    public static RefitQuote Unavailable(string reason, int currentRefitLevel = 0)
        => new(
            CanPurchase: false,
            RefitMaxed: false,
            Reason: reason ?? string.Empty,
            CurrentPercentileQ64: 0UL,
            CurrentRefitLevel: Math.Max(0, currentRefitLevel),
            TargetRefitLevel: Math.Max(0, currentRefitLevel),
            TargetFloorQ64: 0UL,
            EchoCost: 0);
}

public sealed record RefitExecutionResult(
    bool Applied,
    bool InvariantFailure,
    string Error,
    RefitQuote Quote,
    ulong ResultPercentileQ64,
    IReadOnlyList<string> AffixIds,
    IReadOnlyDictionary<string, float> AffixMagnitudes)
{
    public static RefitExecutionResult NoChange(
        RefitQuote quote,
        IReadOnlyList<string> affixIds,
        IReadOnlyDictionary<string, float>? affixMagnitudes = null,
        string error = "")
        => new(
            Applied: false,
            InvariantFailure: false,
            Error: error ?? string.Empty,
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
    {
        if (!TryResolveItem(item, out var currentQualityQ64, out var error))
        {
            return RefitQuote.Unavailable(error, item?.RefitLevel ?? 0);
        }

        if (item.RefitLevel < 0)
        {
            return RefitQuote.Unavailable(
                "Refit level cannot be negative.",
                item.RefitLevel);
        }

        if (chapterEconomy == null
            || string.IsNullOrWhiteSpace(chapterEconomy.ChapterId)
            || chapterEconomy.FirstFarmRunEcho <= 0
            || !double.IsFinite(chapterEconomy.MeanGrade))
        {
            return RefitQuote.Unavailable(
                "The chapter first-farm Echo or mean grade could not be derived.",
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
                echoCost = RefitCostCurve.GetBundleCost(
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
                return RefitQuote.Unavailable(exception.Message, item.RefitLevel);
            }

            return new RefitQuote(
                CanPurchase: true,
                RefitMaxed: false,
                Reason: string.Empty,
                CurrentPercentileQ64: currentQualityQ64,
                CurrentRefitLevel: item.RefitLevel,
                TargetRefitLevel: targetLevel,
                TargetFloorQ64: floorQ64,
                EchoCost: echoCost);
        }

        return Maxed(
            "The item is already at or above every effective Refit floor.",
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
                item.AffixIds,
                item.AffixMagnitudes,
                quote.Reason);
        }

        if (_lookup.Snapshot.AffixCatalog is not { } catalog)
        {
            return InvariantFailure(
                item,
                quote,
                "Affix catalog is unavailable.");
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
                $"Magnitude Refit generation failed: {exception.Message}");
        }

        var generatedAffixIds = item.AffixIds.ToArray();
        if (!ValidatePostconditions(
                item,
                quote,
                generatedAffixIds,
                generatedMagnitudes,
                out var resultQualityQ64,
                out var invariantError))
        {
            return InvariantFailure(item, quote, invariantError);
        }

        return new RefitExecutionResult(
            Applied: true,
            InvariantFailure: false,
            Error: string.Empty,
            Quote: quote,
            ResultPercentileQ64: resultQualityQ64,
            AffixIds: generatedAffixIds,
            AffixMagnitudes: generatedMagnitudes);
    }

    private bool TryResolveItem(
        RefitItemState? item,
        out ulong currentQualityQ64,
        out string error)
    {
        currentQualityQ64 = 0UL;
        error = string.Empty;
        if (item == null
            || string.IsNullOrWhiteSpace(item.ItemBaseId)
            || string.IsNullOrWhiteSpace(item.StableItemKey)
            || item.AffixIds == null)
        {
            error = "Refit item state is incomplete.";
            return false;
        }

        if (!Enum.IsDefined(typeof(ItemRarityTierValue), item.Grade))
        {
            error = $"Refit item grade '{item.Grade}' is invalid.";
            return false;
        }

        if (!ValidateAffixLegality(item, item.AffixIds, out error))
        {
            return false;
        }

        if (_lookup.Snapshot.AffixCatalog is not { } catalog)
        {
            error = "Affix catalog is unavailable.";
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
            error = $"Roll quality resolution failed: {exception.Message}";
            return false;
        }
    }

    private bool ValidatePostconditions(
        RefitItemState item,
        RefitQuote quote,
        IReadOnlyList<string> generatedAffixIds,
        IReadOnlyDictionary<string, float> generatedMagnitudes,
        out ulong resultQualityQ64,
        out string error)
    {
        resultQualityQ64 = 0UL;
        if (!generatedAffixIds.SequenceEqual(item.AffixIds, StringComparer.Ordinal))
        {
            error = "Refit changed affix identity.";
            return false;
        }

        if (!ValidateAffixLegality(item, generatedAffixIds, out error))
        {
            return false;
        }

        if (generatedMagnitudes.Count != generatedAffixIds.Count
            || generatedAffixIds.Any(id => !generatedMagnitudes.ContainsKey(id)))
        {
            error = "Refit magnitude output does not match the affix identity set atomically.";
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
            error = $"Refit magnitude range validation failed: {exception.Message}";
            return false;
        }

        if (resultQualityQ64 < quote.CurrentPercentileQ64)
        {
            error = $"Refit roll quality regressed from "
                    + $"{RefitRollQuality.FromQ64(quote.CurrentPercentileQ64):R} to "
                    + $"{RefitRollQuality.FromQ64(resultQualityQ64):R}.";
            return false;
        }

        if (resultQualityQ64 < quote.TargetFloorQ64)
        {
            error = "Refit result roll quality is below the purchased floor.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateAffixLegality(
        RefitItemState item,
        IReadOnlyList<string> affixIds,
        out string error)
    {
        if (_lookup.Snapshot.ItemCatalog is not { } itemCatalog
            || !itemCatalog.TryGetValue(item.ItemBaseId, out var itemTemplate))
        {
            error = $"Unknown item base '{item.ItemBaseId}'.";
            return false;
        }

        if (_lookup.Snapshot.AffixCatalog is not { } catalog)
        {
            error = "Affix catalog is unavailable.";
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
                error = $"Refit found an unknown or duplicate affix '{affixId}'.";
                return false;
            }

            if (affix.AllowedSlotTypes is { Count: > 0 }
                && !affix.AllowedSlotTypes.Contains(
                    itemTemplate.SlotType,
                    StringComparer.Ordinal))
            {
                error = $"Affix '{affixId}' is illegal for slot '{itemTemplate.SlotType}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(affix.ExclusiveGroupId)
                && !seenExclusiveGroups.Add(affix.ExclusiveGroupId))
            {
                error = $"Affix '{affixId}' conflicts in exclusive group "
                        + $"'{affix.ExclusiveGroupId}'.";
                return false;
            }

            tiers.Add(affix.Tier);
        }

        if (!FollowsTierStepSequence(item.Grade, tiers))
        {
            error = "Refit item does not follow its grade's generated tier-step sequence.";
            return false;
        }

        error = string.Empty;
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
        string error)
        => new(
            Applied: false,
            InvariantFailure: true,
            Error: error,
            Quote: quote,
            ResultPercentileQ64: quote.CurrentPercentileQ64,
            AffixIds: item.AffixIds.ToArray(),
            AffixMagnitudes: (item.AffixMagnitudes
                              ?? new Dictionary<string, float>(StringComparer.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    private static RefitQuote Maxed(
        string reason,
        RefitItemState item,
        ulong currentQualityQ64)
        => new(
            CanPurchase: false,
            RefitMaxed: true,
            Reason: reason,
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
