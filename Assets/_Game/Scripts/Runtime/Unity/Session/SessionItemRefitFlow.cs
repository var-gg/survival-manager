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

    private Result ApplyItemRefit(
        InventoryItemRecord item,
        RefitItemState itemState,
        RefitChapterEconomy chapterEconomy,
        RefitService service,
        ulong stableCommandSeed)
    {
        if (!string.Equals(_owner.CurrentSceneName, SceneNames.Town, StringComparison.Ordinal))
        {
            return Result.Fail("Refit은 Town에서만 가능합니다.");
        }

        // Atomic order: resolve target/cost -> affordability -> generate -> invariants
        // -> materialize both identity/value lists -> charge -> commit both lists.
        var quote = service.QuoteNextEffective(itemState, chapterEconomy);
        if (!quote.CanPurchase)
        {
            return Result.Fail(string.IsNullOrWhiteSpace(quote.Reason)
                ? "이 장비에는 유효한 Refit 단계가 없습니다."
                : quote.Reason);
        }

        if (_owner.Profile.Currencies.Echo < quote.EchoCost)
        {
            return Result.Fail($"잔향이 부족합니다. 재정비에는 {quote.EchoCost} 잔향이 필요합니다.");
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
