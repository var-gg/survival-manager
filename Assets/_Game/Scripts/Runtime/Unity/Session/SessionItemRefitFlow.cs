using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Core.Results;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

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
                out var error))
        {
            return Result.Fail(error);
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
                out var error))
        {
            return Result.Fail(error);
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
                out var error))
        {
            return Result.Fail(error);
        }

        if (!TryCanonicalizeSealSelection(
                itemState.AffixIds,
                sealedAffixIds,
                out var canonicalSealedAffixIds,
                out error))
        {
            return Result.Fail(error);
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
                out var error))
        {
            return Result.Fail(error);
        }

        if (!TryCanonicalizeSealSelection(
                itemState.AffixIds,
                sealedAffixIds,
                out var canonicalSealedAffixIds,
                out error))
        {
            return Result.Fail(error);
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
            out var error)
            ? service.QuoteNextEffective(itemState, chapterEconomy)
            : RefitQuote.Unavailable(error);
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
            out var error)
            ? service.QuoteSealNextEffective(itemState, chapterEconomy, sealedAffixIds)
            : RefitQuote.Unavailable(error);
    }

    internal string GetRefitPurchaseBlockReason(string itemInstanceId)
    {
        var quote = GetRefitQuote(itemInstanceId);
        return ResolvePurchaseBlockReason(quote, CraftOperationKindValue.Reforge);
    }

    internal string GetSealPurchaseBlockReason(
        string itemInstanceId,
        IReadOnlyCollection<string> sealedAffixIds)
    {
        var quote = GetSealQuote(itemInstanceId, sealedAffixIds);
        return ResolvePurchaseBlockReason(quote, CraftOperationKindValue.Seal);
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
            out var error)
            ? service.RefitNextEffective(itemState, chapterEconomy, stableCommandSeed)
            : RefitExecutionResult.NoChange(
                RefitQuote.Unavailable(error),
                Array.Empty<string>(),
                error: error);
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
            out var error)
            ? service.SealNextEffective(
                itemState,
                chapterEconomy,
                sealedAffixIds,
                attemptIndex,
                stableCommandSeed)
            : RefitExecutionResult.NoChange(
                RefitQuote.Unavailable(error),
                Array.Empty<string>(),
                error: error);
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
        var purchaseBlockReason = ResolvePurchaseBlockReason(
            quote,
            CraftOperationKindValue.Reforge);
        if (!string.IsNullOrWhiteSpace(purchaseBlockReason))
        {
            return Result.Fail(purchaseBlockReason);
        }

        var execution = service.RefitNextEffective(
            itemState,
            chapterEconomy,
            stableCommandSeed);
        if (!execution.Applied)
        {
            return Result.Fail(string.IsNullOrWhiteSpace(execution.Error)
                ? "Refit invariant 검증에 실패했습니다."
                : execution.Error);
        }

        if (execution.Quote.EchoCost != quote.EchoCost
            || execution.Quote.TargetRefitLevel != quote.TargetRefitLevel)
        {
            return Result.Fail("Refit quote changed before commit; no currency or item state was changed.");
        }

        var committedAffixIds = execution.AffixIds.ToList();
        var committedMagnitudeRolls = new List<InventoryAffixMagnitudeRecord>(
            committedAffixIds.Count);
        foreach (var affixId in committedAffixIds)
        {
            if (!execution.AffixMagnitudes.TryGetValue(affixId, out var magnitude))
            {
                return Result.Fail(
                    "Refit magnitude output did not match affix identity; no currency or item state was changed.");
            }

            committedMagnitudeRolls.Add(new InventoryAffixMagnitudeRecord
            {
                AffixId = affixId,
                Magnitude = magnitude,
            });
        }

        if (committedMagnitudeRolls.Count != execution.AffixMagnitudes.Count)
        {
            return Result.Fail(
                "Refit magnitude output contained stale identities; no currency or item state was changed.");
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
        var purchaseBlockReason = ResolvePurchaseBlockReason(
            quote,
            CraftOperationKindValue.Seal);
        if (!string.IsNullOrWhiteSpace(purchaseBlockReason))
        {
            return Result.Fail(purchaseBlockReason);
        }

        var execution = service.SealNextEffective(
            itemState,
            chapterEconomy,
            canonicalSealedAffixIds,
            attemptIndex,
            stableCommandSeed);
        if (!execution.Applied)
        {
            return Result.Fail(string.IsNullOrWhiteSpace(execution.Error)
                ? "Seal invariant 검증에 실패했습니다."
                : execution.Error);
        }

        if (execution.Quote.EchoCost != quote.EchoCost
            || execution.Quote.TargetRefitLevel != quote.TargetRefitLevel)
        {
            return Result.Fail("Seal quote changed before commit; no currency or item state was changed.");
        }

        var committedAffixIds = execution.AffixIds.ToList();
        var committedMagnitudeRolls = new List<InventoryAffixMagnitudeRecord>(
            committedAffixIds.Count);
        foreach (var affixId in committedAffixIds)
        {
            if (!execution.AffixMagnitudes.TryGetValue(affixId, out var magnitude))
            {
                return Result.Fail(
                    "Seal magnitude output did not match affix identity; no state was changed.");
            }

            committedMagnitudeRolls.Add(new InventoryAffixMagnitudeRecord
            {
                AffixId = affixId,
                Magnitude = magnitude,
            });
        }

        if (committedMagnitudeRolls.Count != execution.AffixMagnitudes.Count)
        {
            return Result.Fail(
                "Seal magnitude output contained stale identities; no state was changed.");
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

    private string ResolvePurchaseBlockReason(
        RefitQuote quote,
        CraftOperationKindValue operation)
    {
        if (!string.Equals(_owner.CurrentSceneName, SceneNames.Town, StringComparison.Ordinal))
        {
            return operation == CraftOperationKindValue.Seal
                ? "Seal은 Town에서만 가능합니다."
                : "Refit은 Town에서만 가능합니다.";
        }

        if (!quote.CanPurchase)
        {
            if (!string.IsNullOrWhiteSpace(quote.Reason))
            {
                return quote.Reason;
            }

            return operation == CraftOperationKindValue.Seal
                ? "이 장비에는 유효한 Seal 단계가 없습니다."
                : "이 장비에는 유효한 Refit 단계가 없습니다.";
        }

        if (_owner.Profile.Currencies.Echo < quote.EchoCost)
        {
            return operation == CraftOperationKindValue.Seal
                ? $"잔향이 부족합니다. 봉인에는 {quote.EchoCost} 잔향이 필요합니다."
                : $"잔향이 부족합니다. 재정비에는 {quote.EchoCost} 잔향이 필요합니다.";
        }

        return string.Empty;
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
        out string error)
    {
        canonicalSealedAffixIds = Array.Empty<string>();
        if (sealedAffixIds == null)
        {
            error = "Seal affix selection is required.";
            return false;
        }

        var sealedSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var affixId in sealedAffixIds)
        {
            if (string.IsNullOrWhiteSpace(affixId) || !sealedSet.Add(affixId))
            {
                error = "Seal affix selection contains a blank or duplicate id.";
                return false;
            }
        }

        if (sealedSet.Count >= itemAffixIds.Count && itemAffixIds.Count > 0)
        {
            error = "Seal must leave at least one affix unlocked.";
            return false;
        }

        var canonical = itemAffixIds.Where(sealedSet.Contains).ToArray();
        if (canonical.Length != sealedSet.Count)
        {
            error = "Seal affix selection contains an id that is not on the item.";
            return false;
        }

        canonicalSealedAffixIds = canonical;
        error = string.Empty;
        return true;
    }

    private bool TryBuildRefitContext(
        string itemInstanceId,
        out InventoryItemRecord item,
        out RefitItemState itemState,
        out RefitChapterEconomy chapterEconomy,
        out RefitService service,
        out string error)
    {
        item = null!;
        itemState = null!;
        chapterEconomy = null!;
        service = null!;
        error = string.Empty;

        var inventoryIndex = _owner.Profile.Inventory.FindIndex(candidate =>
            string.Equals(candidate.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
        if (inventoryIndex < 0)
        {
            error = $"아이템 '{itemInstanceId}'을 찾을 수 없습니다.";
            return false;
        }

        item = _owner.Profile.Inventory[inventoryIndex];
        var snapshot = _contentLookup.Snapshot;
        if (snapshot.RefitBalance == null)
        {
            error = "Refit balance data가 content snapshot에 없습니다.";
            return false;
        }

        if (snapshot.ItemCatalog is not { } items
            || !items.TryGetValue(item.ItemBaseId, out var itemTemplate))
        {
            error = $"Refit item base '{item.ItemBaseId}'을 content snapshot에서 찾을 수 없습니다.";
            return false;
        }

        var grade = item.RolledRarityTier >= (int)ItemRarityTierValue.Common
                    && item.RolledRarityTier <= (int)ItemRarityTierValue.Legendary
            ? (ItemRarityTierValue)item.RolledRarityTier
            : itemTemplate.RarityTier;
        if (!TryBuildAffixMagnitudeLookup(item, out var affixMagnitudes, out error))
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
        out string error)
    {
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
                error = $"Affix '{roll.AffixId}' has duplicate persisted magnitude rolls.";
                return false;
            }
        }

        magnitudes = result;
        error = string.Empty;
        return true;
    }

    private static int BuildStableSeed(string value, int salt)
        => GameSessionState.BuildStableSeed(value, salt);
}
