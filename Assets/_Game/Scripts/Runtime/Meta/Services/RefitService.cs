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
    int RefitLevel);

public sealed record RefitChapterEconomy(
    string ChapterId,
    int FirstFarmRunEcho,
    double MeanGrade);

public sealed record RefitQuote(
    bool CanPurchase,
    bool RefitMaxed,
    string Reason,
    int CurrentScoreQ,
    ulong CurrentPercentileQ64,
    int CurrentRefitLevel,
    int TargetRefitLevel,
    ulong TargetFloorQ64,
    int TargetScoreQ,
    int EchoCost)
{
    public static RefitQuote Unavailable(string reason, int currentRefitLevel = 0)
        => new(
            CanPurchase: false,
            RefitMaxed: false,
            Reason: reason ?? string.Empty,
            CurrentScoreQ: 0,
            CurrentPercentileQ64: 0UL,
            CurrentRefitLevel: Math.Max(0, currentRefitLevel),
            TargetRefitLevel: Math.Max(0, currentRefitLevel),
            TargetFloorQ64: 0UL,
            TargetScoreQ: 0,
            EchoCost: 0);
}

public sealed record RefitExecutionResult(
    bool Applied,
    bool InvariantFailure,
    string Error,
    RefitQuote Quote,
    IReadOnlyList<string> AffixIds)
{
    public static RefitExecutionResult NoChange(
        RefitQuote quote,
        IReadOnlyList<string> affixIds,
        string error = "")
        => new(false, false, error ?? string.Empty, quote, affixIds.ToArray());
}

/// <summary>
/// Item-total power-quality Refit. The natural generator is conditioned on one exact attainable score.
/// This service never mutates persistence or currency and never falls back to the retired slot reroll.
/// </summary>
public sealed class RefitService
{
    private readonly ISessionContentLookup _lookup;
    private readonly RefitBalanceTemplate _balance;
    private readonly float _gradeStepBudgetScore;
    private readonly AffixQualityProfileCompiler _compiler = new();
    private readonly AffixQualityConditionedSelector _selector = new();
    private readonly Dictionary<(string ItemBaseId, ItemRarityTierValue Grade), AffixQualityProfile> _profiles = new();

    public RefitService(
        ISessionContentLookup lookup,
        RefitBalanceTemplate balance,
        float gradeStepBudgetScore)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        if (gradeStepBudgetScore <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(gradeStepBudgetScore));
        }

        if (_balance.FloorScheduleQ64 is not { Count: > 0 })
        {
            throw new ArgumentException("Refit balance must contain a generated floor schedule.", nameof(balance));
        }

        _gradeStepBudgetScore = gradeStepBudgetScore;
    }

    public RefitQuote QuoteNextEffective(
        RefitItemState item,
        RefitChapterEconomy chapterEconomy)
    {
        if (!TryResolveItem(item, out var profile, out var oldScoreQ, out var error))
        {
            return RefitQuote.Unavailable(error, item?.RefitLevel ?? 0);
        }

        var currentPercentileQ64 = profile.GetInclusivePercentileQ64(oldScoreQ);
        if (item.RefitLevel < 0)
        {
            return RefitQuote.Unavailable("Refit level cannot be negative.", item.RefitLevel);
        }

        // A1 measured no useful power-percentile separation below Epic. Do not sell a
        // nominal level to a Common/Magic/Rare item that cannot provide a meaningful floor.
        if (item.Grade < ItemRarityTierValue.Epic)
        {
            return Maxed(
                "This grade has no effective power-quality Refit floor.",
                item,
                oldScoreQ,
                currentPercentileQ64);
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
            var targetLevel = scheduleIndex + 1;
            var floorQ64 = _balance.FloorScheduleQ64[scheduleIndex];
            var floorScoreQ = profile.GetQuantileScoreQ(floorQ64);
            if (floorScoreQ <= oldScoreQ)
            {
                continue;
            }

            var targetScoreQ = FindFirstSupportAtLeast(
                profile.SupportScoreQ,
                Math.Max(oldScoreQ, floorScoreQ));
            if (targetScoreQ < 0)
            {
                return Maxed(
                    "No attainable profile score can improve this item.",
                    item,
                    oldScoreQ,
                    currentPercentileQ64);
            }

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
                CurrentScoreQ: oldScoreQ,
                CurrentPercentileQ64: currentPercentileQ64,
                CurrentRefitLevel: item.RefitLevel,
                TargetRefitLevel: targetLevel,
                TargetFloorQ64: floorQ64,
                TargetScoreQ: targetScoreQ,
                EchoCost: echoCost);
        }

        return Maxed(
            "The item is already at or above every effective Refit floor.",
            item,
            oldScoreQ,
            currentPercentileQ64);
    }

    public RefitExecutionResult RefitNextEffective(
        RefitItemState item,
        RefitChapterEconomy chapterEconomy,
        ulong stableCommandSeed)
    {
        var quote = QuoteNextEffective(item, chapterEconomy);
        if (!quote.CanPurchase)
        {
            return RefitExecutionResult.NoChange(quote, item.AffixIds, quote.Reason);
        }

        var profile = GetProfile(item.ItemBaseId, item.Grade);
        IReadOnlyList<string> generated;
        try
        {
            generated = _selector.SelectBudgetWeightedConditioned(
                profile,
                quote.TargetScoreQ,
                RefitSeedDerivation.Derive(
                    stableCommandSeed,
                    item.StableItemKey,
                    quote.TargetRefitLevel,
                    _balance.RulesVersion));
        }
        catch (Exception exception)
        {
            return new RefitExecutionResult(
                Applied: false,
                InvariantFailure: true,
                Error: $"Conditioned Refit generation failed: {exception.Message}",
                Quote: quote,
                AffixIds: item.AffixIds.ToArray());
        }

        if (!ValidatePostconditions(item, profile, quote, generated, out var invariantError))
        {
            return new RefitExecutionResult(
                Applied: false,
                InvariantFailure: true,
                Error: invariantError,
                Quote: quote,
                AffixIds: item.AffixIds.ToArray());
        }

        return new RefitExecutionResult(
            Applied: true,
            InvariantFailure: false,
            Error: string.Empty,
            Quote: quote,
            AffixIds: generated.ToArray());
    }

    private bool TryResolveItem(
        RefitItemState? item,
        out AffixQualityProfile profile,
        out int oldScoreQ,
        out string error)
    {
        profile = null!;
        oldScoreQ = 0;
        error = string.Empty;
        if (item == null
            || string.IsNullOrWhiteSpace(item.ItemBaseId)
            || string.IsNullOrWhiteSpace(item.StableItemKey)
            || item.AffixIds == null)
        {
            error = "Refit item state is incomplete.";
            return false;
        }

        if (_lookup.Snapshot.ItemCatalog is not { } itemCatalog
            || !itemCatalog.ContainsKey(item.ItemBaseId))
        {
            error = $"Unknown item base '{item.ItemBaseId}'.";
            return false;
        }

        if (!TryScore(item.AffixIds, out oldScoreQ, out error))
        {
            return false;
        }

        try
        {
            profile = GetProfile(item.ItemBaseId, item.Grade);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Quality profile resolution failed: {exception.Message}";
            return false;
        }
    }

    private AffixQualityProfile GetProfile(string itemBaseId, ItemRarityTierValue grade)
    {
        var key = (itemBaseId, grade);
        if (_profiles.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var compiled = _compiler.Compile(
            _lookup,
            itemBaseId,
            grade,
            _gradeStepBudgetScore,
            _balance.AffixCatalogVersion,
            out _);
        _profiles.Add(key, compiled);
        return compiled;
    }

    private bool ValidatePostconditions(
        RefitItemState item,
        AffixQualityProfile profile,
        RefitQuote quote,
        IReadOnlyList<string> generated,
        out string error)
    {
        if (!string.Equals(profile.Key.ItemBaseId, item.ItemBaseId, StringComparison.Ordinal)
            || profile.Key.Grade != item.Grade)
        {
            error = "Refit changed the item base or grade profile.";
            return false;
        }

        if (!ValidateAffixLegality(profile, item.Grade, generated, out error))
        {
            return false;
        }

        if (!TryScore(generated, out var resultScoreQ, out error))
        {
            return false;
        }

        if (resultScoreQ < quote.CurrentScoreQ)
        {
            error = $"Refit score regressed from {quote.CurrentScoreQ} to {resultScoreQ}.";
            return false;
        }

        if (resultScoreQ != quote.TargetScoreQ)
        {
            error = $"Refit ended at {resultScoreQ}, expected exact target {quote.TargetScoreQ}.";
            return false;
        }

        if (profile.GetInclusivePercentileQ64(resultScoreQ) < quote.TargetFloorQ64)
        {
            error = "Refit result CDF is below the purchased floor.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateAffixLegality(
        AffixQualityProfile profile,
        ItemRarityTierValue grade,
        IReadOnlyList<string> affixIds,
        out string error)
    {
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
                error = $"Refit produced an unknown or duplicate affix '{affixId}'.";
                return false;
            }

            if (affix.AllowedSlotTypes is { Count: > 0 }
                && !affix.AllowedSlotTypes.Contains(profile.Key.SlotType, StringComparer.Ordinal))
            {
                error = $"Affix '{affixId}' is illegal for slot '{profile.Key.SlotType}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(affix.ExclusiveGroupId)
                && !seenExclusiveGroups.Add(affix.ExclusiveGroupId))
            {
                error = $"Affix '{affixId}' conflicts in exclusive group '{affix.ExclusiveGroupId}'.";
                return false;
            }

            tiers.Add(affix.Tier);
        }

        if (!FollowsTierStepSequence(grade, tiers))
        {
            error = "Refit result does not follow the grade's generated tier-step sequence.";
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

    private bool TryScore(
        IReadOnlyList<string> affixIds,
        out int totalScoreQ,
        out string error)
    {
        totalScoreQ = 0;
        error = string.Empty;
        if (_lookup.Snapshot.AffixCatalog is not { } catalog)
        {
            error = "Affix catalog is unavailable.";
            return false;
        }

        try
        {
            foreach (var affixId in affixIds)
            {
                if (!catalog.TryGetValue(affixId, out var affix))
                {
                    error = $"Unknown legacy affix '{affixId}'.";
                    return false;
                }

                totalScoreQ = checked(
                    totalScoreQ
                    + AffixQualityProfileCompiler.ToBudgetScoreQ(affix.BudgetScore));
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or OverflowException)
        {
            error = exception.Message;
            return false;
        }

        return true;
    }

    private static int FindFirstSupportAtLeast(
        IReadOnlyList<int> support,
        int minimumScoreQ)
    {
        var low = 0;
        var high = support.Count;
        while (low < high)
        {
            var midpoint = low + ((high - low) / 2);
            if (support[midpoint] < minimumScoreQ)
            {
                low = midpoint + 1;
            }
            else
            {
                high = midpoint;
            }
        }

        return low < support.Count ? support[low] : -1;
    }

    private static RefitQuote Maxed(
        string reason,
        RefitItemState item,
        int oldScoreQ,
        ulong currentPercentileQ64)
        => new(
            CanPurchase: false,
            RefitMaxed: true,
            Reason: reason,
            CurrentScoreQ: oldScoreQ,
            CurrentPercentileQ64: currentPercentileQ64,
            CurrentRefitLevel: item.RefitLevel,
            TargetRefitLevel: item.RefitLevel,
            TargetFloorQ64: currentPercentileQ64,
            TargetScoreQ: oldScoreQ,
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
