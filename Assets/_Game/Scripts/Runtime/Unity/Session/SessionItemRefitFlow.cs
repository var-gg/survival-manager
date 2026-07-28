using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Core.Results;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using UnityEngine;

namespace SM.Unity;

internal sealed class SessionItemRefitFlow
{
    private readonly GameSessionState _owner;
    private readonly ISessionContentLookup _contentLookup;
    private RefitService? _itemRefitService;

    internal SessionItemRefitFlow(GameSessionState owner, ISessionContentLookup contentLookup)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _contentLookup = contentLookup ?? throw new ArgumentNullException(nameof(contentLookup));
    }

    /// <summary>Player-facing item-level action: purchase the next effective roll-quality floor.</summary>
    internal Result RefitItem(string itemInstanceId)
    {
        if (!TryBuildRefitContext(
                itemInstanceId,
                out var item,
                out var itemState,
                out var chapterEconomy,
                out var service,
                out var failure))
        {
            return FailAtPlayerBoundary(failure!, "refit", itemInstanceId);
        }

        var commandSeed = unchecked((ulong)(uint)BuildStableSeed(
            $"REFIT_COMMAND|{_owner.Profile.ProfileId}|{itemState.StableItemKey}",
            item.RefitLevel + 1));
        return ApplyItemRefit(item, itemState, chapterEconomy, service, commandSeed);
    }

    /// <summary>Deterministic harness seam. The player-facing overload derives this seed from stable save state.</summary>
    internal Result RefitItem(string itemInstanceId, ulong stableCommandSeed)
    {
        if (!TryBuildRefitContext(
                itemInstanceId,
                out var item,
                out var itemState,
                out var chapterEconomy,
                out var service,
                out var failure))
        {
            return FailAtPlayerBoundary(failure!, "refit", itemInstanceId);
        }

        return ApplyItemRefit(item, itemState, chapterEconomy, service, stableCommandSeed);
    }

    /// <summary>
    /// Player-facing Seal action. Selection UI is intentionally separate from this
    /// service-reachable session seam.
    /// </summary>
    internal Result SealItem(
        string itemInstanceId,
        IReadOnlyCollection<string> sealedAffixIds)
    {
        if (!TryBuildRefitContext(
                itemInstanceId,
                out var item,
                out var itemState,
                out var chapterEconomy,
                out var service,
                out var failure))
        {
            return FailAtPlayerBoundary(failure!, "seal", itemInstanceId);
        }

        if (!TryCanonicalizeSealSelection(
                itemState.AffixIds,
                sealedAffixIds,
                out var canonicalSealedAffixIds,
                out failure))
        {
            return FailAtPlayerBoundary(failure!, "seal", itemInstanceId);
        }

        var attemptIndex = ResolveNextSealAttemptIndex(item.ItemInstanceId);
        var commandSeed = unchecked((ulong)(uint)BuildStableSeed(
            $"SEAL_COMMAND|{_owner.Profile.ProfileId}|{itemState.StableItemKey}|"
            + string.Join("|", canonicalSealedAffixIds),
            attemptIndex));
        return ApplyItemSeal(
            item,
            itemState,
            chapterEconomy,
            service,
            canonicalSealedAffixIds,
            attemptIndex,
            commandSeed);
    }

    /// <summary>Deterministic Seal harness seam with explicit persisted command input.</summary>
    internal Result SealItem(
        string itemInstanceId,
        IReadOnlyCollection<string> sealedAffixIds,
        int attemptIndex,
        ulong stableCommandSeed)
    {
        if (!TryBuildRefitContext(
                itemInstanceId,
                out var item,
                out var itemState,
                out var chapterEconomy,
                out var service,
                out var failure))
        {
            return FailAtPlayerBoundary(failure!, "seal", itemInstanceId);
        }

        if (!TryCanonicalizeSealSelection(
                itemState.AffixIds,
                sealedAffixIds,
                out var canonicalSealedAffixIds,
                out failure))
        {
            return FailAtPlayerBoundary(failure!, "seal", itemInstanceId);
        }

        return ApplyItemSeal(
            item,
            itemState,
            chapterEconomy,
            service,
            canonicalSealedAffixIds,
            attemptIndex,
            stableCommandSeed);
    }

    internal RefitQuote GetRefitQuote(string itemInstanceId)
    {
        return TryBuildRefitContext(
            itemInstanceId,
            out _,
            out var itemState,
            out var chapterEconomy,
            out var service,
            out var failure)
            ? PrepareQuoteForPlayer(
                service.QuoteNextEffective(itemState, chapterEconomy),
                "refit",
                itemInstanceId)
            : PrepareQuoteForPlayer(
                RefitQuote.Unavailable(failure!),
                "refit",
                itemInstanceId);
    }

    internal RefitQuote GetSealQuote(
        string itemInstanceId,
        IReadOnlyCollection<string> sealedAffixIds)
    {
        return TryBuildRefitContext(
            itemInstanceId,
            out _,
            out var itemState,
            out var chapterEconomy,
            out var service,
            out var failure)
            ? PrepareQuoteForPlayer(
                service.QuoteSealNextEffective(itemState, chapterEconomy, sealedAffixIds),
                "seal",
                itemInstanceId)
            : PrepareQuoteForPlayer(
                RefitQuote.Unavailable(failure!),
                "seal",
                itemInstanceId);
    }

    internal OperationFailure? GetRefitPurchaseBlockFailure(string itemInstanceId)
    {
        var quote = GetRefitQuote(itemInstanceId);
        var failure = ResolvePurchaseBlockFailure(quote, CraftOperationKindValue.Reforge);
        return failure == null ? null : PlayerSafe(failure);
    }

    internal OperationFailure? GetSealPurchaseBlockFailure(
        string itemInstanceId,
        IReadOnlyCollection<string> sealedAffixIds)
    {
        var quote = GetSealQuote(itemInstanceId, sealedAffixIds);
        var failure = ResolvePurchaseBlockFailure(quote, CraftOperationKindValue.Seal);
        return failure == null ? null : PlayerSafe(failure);
    }

    internal RefitExecutionResult PreviewRefitItem(
        string itemInstanceId,
        ulong stableCommandSeed)
    {
        return TryBuildRefitContext(
            itemInstanceId,
            out _,
            out var itemState,
            out var chapterEconomy,
            out var service,
            out var failure)
            ? service.RefitNextEffective(itemState, chapterEconomy, stableCommandSeed)
            : RefitExecutionResult.NoChange(
                RefitQuote.Unavailable(failure!),
                Array.Empty<string>());
    }

    internal RefitExecutionResult PreviewSealItem(
        string itemInstanceId,
        IReadOnlyCollection<string> sealedAffixIds,
        int attemptIndex,
        ulong stableCommandSeed)
    {
        return TryBuildRefitContext(
            itemInstanceId,
            out _,
            out var itemState,
            out var chapterEconomy,
            out var service,
            out var failure)
            ? service.SealNextEffective(
                itemState,
                chapterEconomy,
                sealedAffixIds,
                attemptIndex,
                stableCommandSeed)
            : RefitExecutionResult.NoChange(
                RefitQuote.Unavailable(failure!),
                Array.Empty<string>());
    }

    private Result ApplyItemRefit(
        InventoryItemRecord item,
        RefitItemState itemState,
        RefitChapterEconomy chapterEconomy,
        RefitService service,
        ulong stableCommandSeed)
    {
        // Atomic order: resolve target/cost -> affordability -> generate -> invariants
        // -> materialize both identity/value lists -> charge -> commit both lists.
        var quote = service.QuoteNextEffective(itemState, chapterEconomy);
        var purchaseBlockFailure = ResolvePurchaseBlockFailure(
            quote,
            CraftOperationKindValue.Reforge);
        if (purchaseBlockFailure != null)
        {
            return FailAtPlayerBoundary(
                purchaseBlockFailure,
                "refit",
                item.ItemInstanceId);
        }

        var execution = service.RefitNextEffective(
            itemState,
            chapterEconomy,
            stableCommandSeed);
        if (!execution.Applied)
        {
            var failure = execution.Failure
                          ?? OperationFailure.Invariant(
                              SessionOperationFailureCodes.GenericOperationFailed,
                              $"Refit execution returned no result and no failure for item '{item.ItemInstanceId}'.");
            return FailAtPlayerBoundary(failure, "refit", item.ItemInstanceId);
        }

        if (execution.Quote.EchoCost != quote.EchoCost
            || execution.Quote.TargetRefitLevel != quote.TargetRefitLevel)
        {
            return FailAtPlayerBoundary(
                OperationFailure.Invariant(
                    SessionOperationFailureCodes.RefitQuoteChanged,
                    $"Refit quote changed before commit for item '{item.ItemInstanceId}': "
                    + $"preview cost/level={quote.EchoCost}/{quote.TargetRefitLevel}, "
                    + $"execution cost/level={execution.Quote.EchoCost}/{execution.Quote.TargetRefitLevel}."),
                "refit",
                item.ItemInstanceId);
        }

        var committedAffixIds = execution.AffixIds.ToList();
        var committedMagnitudeRolls = new List<InventoryAffixMagnitudeRecord>(
            committedAffixIds.Count);
        foreach (var affixId in committedAffixIds)
        {
            if (!execution.AffixMagnitudes.TryGetValue(affixId, out var magnitude))
            {
                return FailAtPlayerBoundary(
                    OperationFailure.Invariant(
                        SessionOperationFailureCodes.RefitCommitMismatch,
                        $"Refit magnitude output was missing affix '{affixId}' for item '{item.ItemInstanceId}'."),
                    "refit",
                    item.ItemInstanceId);
            }

            committedMagnitudeRolls.Add(new InventoryAffixMagnitudeRecord
            {
                AffixId = affixId,
                Magnitude = magnitude,
            });
        }

        if (committedMagnitudeRolls.Count != execution.AffixMagnitudes.Count)
        {
            return FailAtPlayerBoundary(
                OperationFailure.Invariant(
                    SessionOperationFailureCodes.RefitCommitMismatch,
                    $"Refit magnitude output contained stale identities for item '{item.ItemInstanceId}': "
                    + $"affixes={committedMagnitudeRolls.Count}, magnitudes={execution.AffixMagnitudes.Count}."),
                "refit",
                item.ItemInstanceId);
        }

        // These reference assignments are the single-threaded session transaction. Both
        // collections were fully materialized and validated before currency or item mutation.
        _owner.Profile.Currencies.Echo -= quote.EchoCost;
        item.AffixIds = committedAffixIds;
        item.AffixMagnitudeRolls = committedMagnitudeRolls;
        item.RefitLevel = quote.TargetRefitLevel;

        _owner.SynchronizeRefitEquippedHero(item.EquippedHeroId);

        return Result.Success();
    }

    private Result ApplyItemSeal(
        InventoryItemRecord item,
        RefitItemState itemState,
        RefitChapterEconomy chapterEconomy,
        RefitService service,
        IReadOnlyList<string> canonicalSealedAffixIds,
        int attemptIndex,
        ulong stableCommandSeed)
    {
        var quote = service.QuoteSealNextEffective(
            itemState,
            chapterEconomy,
            canonicalSealedAffixIds);
        var purchaseBlockFailure = ResolvePurchaseBlockFailure(
            quote,
            CraftOperationKindValue.Seal);
        if (purchaseBlockFailure != null)
        {
            return FailAtPlayerBoundary(
                purchaseBlockFailure,
                "seal",
                item.ItemInstanceId);
        }

        var execution = service.SealNextEffective(
            itemState,
            chapterEconomy,
            canonicalSealedAffixIds,
            attemptIndex,
            stableCommandSeed);
        if (!execution.Applied)
        {
            var failure = execution.Failure
                          ?? OperationFailure.Invariant(
                              SessionOperationFailureCodes.GenericOperationFailed,
                              $"Seal execution returned no result and no failure for item '{item.ItemInstanceId}'.");
            return FailAtPlayerBoundary(failure, "seal", item.ItemInstanceId);
        }

        if (execution.Quote.EchoCost != quote.EchoCost
            || execution.Quote.TargetRefitLevel != quote.TargetRefitLevel)
        {
            return FailAtPlayerBoundary(
                OperationFailure.Invariant(
                    SessionOperationFailureCodes.RefitQuoteChanged,
                    $"Seal quote changed before commit for item '{item.ItemInstanceId}': "
                    + $"preview cost/level={quote.EchoCost}/{quote.TargetRefitLevel}, "
                    + $"execution cost/level={execution.Quote.EchoCost}/{execution.Quote.TargetRefitLevel}."),
                "seal",
                item.ItemInstanceId);
        }

        var committedAffixIds = execution.AffixIds.ToList();
        var committedMagnitudeRolls = new List<InventoryAffixMagnitudeRecord>(
            committedAffixIds.Count);
        foreach (var affixId in committedAffixIds)
        {
            if (!execution.AffixMagnitudes.TryGetValue(affixId, out var magnitude))
            {
                return FailAtPlayerBoundary(
                    OperationFailure.Invariant(
                        SessionOperationFailureCodes.RefitCommitMismatch,
                        $"Seal magnitude output was missing affix '{affixId}' for item '{item.ItemInstanceId}'."),
                    "seal",
                    item.ItemInstanceId);
            }

            committedMagnitudeRolls.Add(new InventoryAffixMagnitudeRecord
            {
                AffixId = affixId,
                Magnitude = magnitude,
            });
        }

        if (committedMagnitudeRolls.Count != execution.AffixMagnitudes.Count)
        {
            return FailAtPlayerBoundary(
                OperationFailure.Invariant(
                    SessionOperationFailureCodes.RefitCommitMismatch,
                    $"Seal magnitude output contained stale identities for item '{item.ItemInstanceId}': "
                    + $"affixes={committedMagnitudeRolls.Count}, magnitudes={execution.AffixMagnitudes.Count}."),
                "seal",
                item.ItemInstanceId);
        }

        var operation = new ItemCraftOperationRecord
        {
            OperationId = $"{item.ItemInstanceId}:Seal:{attemptIndex}",
            ItemInstanceId = item.ItemInstanceId,
            ItemBaseId = item.ItemBaseId,
            OperationKind = CraftOperationKindValue.Seal,
            SealedAffixIds = canonicalSealedAffixIds.ToList(),
            AttemptIndex = attemptIndex,
            StableCommandSeed = stableCommandSeed,
            TargetRefitLevel = quote.TargetRefitLevel,
            RulesVersion = _contentLookup.Snapshot.RefitBalance!.RulesVersion,
            EchoCost = quote.EchoCost,
        };

        _owner.Profile.ItemCraftOperations ??= new List<ItemCraftOperationRecord>();
        _owner.Profile.Currencies.Echo -= quote.EchoCost;
        item.AffixIds = committedAffixIds;
        item.AffixMagnitudeRolls = committedMagnitudeRolls;
        item.RefitLevel = quote.TargetRefitLevel;
        _owner.Profile.ItemCraftOperations.Add(operation);

        _owner.SynchronizeRefitEquippedHero(item.EquippedHeroId);
        return Result.Success();
    }

    private OperationFailure? ResolvePurchaseBlockFailure(
        RefitQuote quote,
        CraftOperationKindValue operation)
    {
        if (!string.Equals(_owner.CurrentSceneName, SceneNames.Town, StringComparison.Ordinal))
        {
            return OperationFailure.Refusal(
                SessionOperationFailureCodes.RefitTownOnly,
                $"Operation '{operation}' is available only in Town.",
                operation.ToString());
        }

        if (!quote.CanPurchase)
        {
            if (quote.Failure != null)
            {
                return quote.Failure;
            }

            return OperationFailure.Invariant(
                SessionOperationFailureCodes.RefitNoEffectiveStep,
                $"Operation '{operation}' has no purchasable step and returned no failure.");
        }

        if (_owner.Profile.Currencies.Echo < quote.EchoCost)
        {
            return OperationFailure.Refusal(
                SessionOperationFailureCodes.RefitUnaffordable,
                $"Operation '{operation}' costs {quote.EchoCost} Echo but wallet has {_owner.Profile.Currencies.Echo}.",
                operation.ToString(),
                quote.EchoCost.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return null;
    }

    private int ResolveNextSealAttemptIndex(string itemInstanceId)
    {
        var operations = _owner.Profile.ItemCraftOperations
                         ?? new List<ItemCraftOperationRecord>();
        var priorAttempts = operations
            .Where(operation =>
                operation != null
                && operation.OperationKind == CraftOperationKindValue.Seal
                && string.Equals(
                    operation.ItemInstanceId,
                    itemInstanceId,
                    StringComparison.Ordinal))
            .Select(operation => operation.AttemptIndex)
            .DefaultIfEmpty(0)
            .Max();
        return checked(priorAttempts + 1);
    }

    private static bool TryCanonicalizeSealSelection(
        IReadOnlyList<string> itemAffixIds,
        IReadOnlyCollection<string>? sealedAffixIds,
        out IReadOnlyList<string> canonicalSealedAffixIds,
        out OperationFailure? failure)
    {
        canonicalSealedAffixIds = Array.Empty<string>();
        failure = null;
        if (sealedAffixIds == null)
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitSealSelectionRequired,
                "Seal affix selection is required.");
            return false;
        }

        var sealedSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var affixId in sealedAffixIds)
        {
            if (string.IsNullOrWhiteSpace(affixId) || !sealedSet.Add(affixId))
            {
                failure = OperationFailure.Refusal(
                    MetaOperationFailureCodes.RefitSealSelectionInvalid,
                    "Seal affix selection contains a blank or duplicate id.");
                return false;
            }
        }

        if (sealedSet.Count >= itemAffixIds.Count && itemAffixIds.Count > 0)
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitSealAllAffixesLocked,
                "Seal must leave at least one affix unlocked.");
            return false;
        }

        var canonical = itemAffixIds.Where(sealedSet.Contains).ToArray();
        if (canonical.Length != sealedSet.Count)
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.RefitSealSelectionInvalid,
                "Seal affix selection contains an id that is not on the item.");
            return false;
        }

        canonicalSealedAffixIds = canonical;
        return true;
    }

    private bool TryBuildRefitContext(
        string itemInstanceId,
        out InventoryItemRecord item,
        out RefitItemState itemState,
        out RefitChapterEconomy chapterEconomy,
        out RefitService service,
        out OperationFailure? failure)
    {
        item = null!;
        itemState = null!;
        chapterEconomy = null!;
        service = null!;
        failure = null;

        var inventoryIndex = _owner.Profile.Inventory.FindIndex(candidate =>
            string.Equals(candidate.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
        if (inventoryIndex < 0)
        {
            failure = OperationFailure.Refusal(
                SessionOperationFailureCodes.ItemNotFound,
                $"Inventory item '{itemInstanceId}' was not found.");
            return false;
        }

        item = _owner.Profile.Inventory[inventoryIndex];
        var snapshot = _contentLookup.Snapshot;
        if (snapshot.RefitBalance == null)
        {
            failure = OperationFailure.Invariant(
                SessionOperationFailureCodes.RefitBalanceMissing,
                "Refit balance data is absent from the content snapshot.");
            return false;
        }

        if (snapshot.ItemCatalog is not { } items
            || !items.TryGetValue(item.ItemBaseId, out var itemTemplate))
        {
            failure = OperationFailure.Invariant(
                SessionOperationFailureCodes.RefitItemBaseMissing,
                $"Refit item base '{item.ItemBaseId}' was not found in the content snapshot for item '{itemInstanceId}'.");
            return false;
        }

        var grade = item.RolledRarityTier >= (int)ItemRarityTierValue.Common
                    && item.RolledRarityTier <= (int)ItemRarityTierValue.Legendary
            ? (ItemRarityTierValue)item.RolledRarityTier
            : itemTemplate.RarityTier;
        if (!TryBuildAffixMagnitudeLookup(item, out var affixMagnitudes, out failure))
        {
            return false;
        }

        itemState = new RefitItemState(
            item.ItemBaseId,
            $"{item.ItemBaseId}|{inventoryIndex}",
            grade,
            (IReadOnlyList<string>?)item.AffixIds ?? Array.Empty<string>(),
            affixMagnitudes,
            item.RefitLevel);

        var chapterId = !string.IsNullOrWhiteSpace(_owner.ActiveRun?.Overlay.ChapterId)
            ? _owner.ActiveRun.Overlay.ChapterId
            : _owner.Profile.CampaignProgress.SelectedChapterId;
        chapterEconomy = new RefitChapterEconomy(
            chapterId,
            CampaignRecoveryRewardPolicy.ResolveFirstFarmRunEcho(snapshot, chapterId),
            CampaignRecoveryRewardPolicy.ResolveFirstFarmRunMeanGrade(snapshot, chapterId));

        _itemRefitService ??= new RefitService(
            _contentLookup,
            snapshot.RefitBalance);
        service = _itemRefitService;
        return true;
    }

    private static bool TryBuildAffixMagnitudeLookup(
        InventoryItemRecord item,
        out IReadOnlyDictionary<string, float> magnitudes,
        out OperationFailure? failure)
    {
        failure = null;
        var currentAffixIds = new HashSet<string>(
            item.AffixIds ?? new List<string>(),
            StringComparer.Ordinal);
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var roll in item.AffixMagnitudeRolls
                             ?? new List<InventoryAffixMagnitudeRecord>())
        {
            // Old identity-reroll saves can contain rolls for the affixes they replaced.
            // They are not effective values for the current identity set and are ignored.
            if (roll == null
                || !currentAffixIds.Contains(roll.AffixId))
            {
                continue;
            }

            if (!result.TryAdd(roll.AffixId, roll.Magnitude))
            {
                magnitudes = new Dictionary<string, float>(StringComparer.Ordinal);
                failure = OperationFailure.Invariant(
                    SessionOperationFailureCodes.RefitMagnitudeStateInvalid,
                    $"Affix '{roll.AffixId}' has duplicate persisted magnitude rolls on item '{item.ItemInstanceId}'.");
                return false;
            }
        }

        magnitudes = result;
        return true;
    }

    private Result FailAtPlayerBoundary(
        OperationFailure failure,
        string operation,
        string itemInstanceId)
    {
        if (failure.IsInvariantViolation)
        {
            LogInvariant(failure, operation, itemInstanceId);
        }

        return Result.Fail(PlayerSafe(failure));
    }

    private RefitQuote PrepareQuoteForPlayer(
        RefitQuote quote,
        string operation,
        string itemInstanceId)
    {
        if (quote.Failure == null)
        {
            return quote;
        }

        if (quote.Failure.IsInvariantViolation)
        {
            LogInvariant(quote.Failure, operation, itemInstanceId);
        }

        return quote with { Failure = PlayerSafe(quote.Failure) };
    }

    private static OperationFailure PlayerSafe(OperationFailure failure)
        => new(
            failure.Code,
            failure.Kind,
            string.Empty,
            failure.Arguments);

    private static void LogInvariant(
        OperationFailure failure,
        string operation,
        string itemInstanceId)
    {
        Debug.LogWarning(
            $"[SessionItemRefitFlow] operation='{operation}' item='{itemInstanceId}' "
            + $"cause='{failure.Code}' diagnostic='{failure.Diagnostic}'");
    }

    private static int BuildStableSeed(string value, int salt)
        => GameSessionState.BuildStableSeed(value, salt);
}
