using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity;

/// <summary>
/// Story revisit reward scope adapter. The numeric rule stays in SM.Meta; this collaborator only
/// reserves persistent chapter budget and plans its live profile/run application.
/// </summary>
internal sealed class SessionCampaignRecoveryFlow
{
    private const string RevisitGoldRewardId = "campaign_revisit_gold";
    private const string RevisitEchoRewardId = "campaign_revisit_echo";
    private const string DefeatEchoRewardId = "campaign_defeat_consolation_echo";

    private readonly GameSessionState _session;

    internal SessionCampaignRecoveryFlow(GameSessionState session)
    {
        _session = session;
    }

    internal ActiveRunState InitializeRun(ActiveRunState run, int endlessCycleIndex)
    {
        if (endlessCycleIndex > 0
            || run.IsQuickBattle
            || string.IsNullOrWhiteSpace(_session.Profile.CampaignProgress.SelectedChapterId)
            || string.IsNullOrWhiteSpace(_session.Profile.CampaignProgress.SelectedSiteId)
            || !_session.Profile.CampaignProgress.ClearedSiteIds.Contains(
                _session.Profile.CampaignProgress.SelectedSiteId,
                StringComparer.Ordinal))
        {
            return run;
        }

        var chapterId = _session.Profile.CampaignProgress.SelectedChapterId;
        var counts = _session.Profile.CampaignProgress.RewardedRevisitCountsByChapter
                     ??= new Dictionary<string, int>(StringComparer.Ordinal);
        var previous = counts.TryGetValue(chapterId, out var value)
            ? Math.Max(0, value)
            : 0;
        var next = previous == int.MaxValue ? int.MaxValue : previous + 1;
        return run with
        {
            Overlay = run.Overlay with
            {
                RewardedRevisitIndex = next,
                RevisitItemRollsGranted = 0,
                RevisitCurrencyGranted = false,
            },
        };
    }

    internal bool IsRewardedRevisit
        => (_session.ActiveRun?.EndlessCycleIndex ?? 0) == 0
           && (_session.ActiveRun?.Overlay.RewardedRevisitIndex ?? 0) > 0;

    internal bool ShouldGrantVictoryExperience => !IsRewardedRevisit;

    internal bool ShouldSuppressRewardChoice
        => IsRewardedRevisit && _session.LastBattleVictory;

    internal bool HasPersistentDefeatConsolationContext
        => _session.ActiveRun != null
           && !_session.IsQuickBattleSmokeActive
           && _session.ActiveRun.EndlessCycleIndex == 0
           && !string.IsNullOrWhiteSpace(ResolveChapterId());

    internal int FilterRouteCurrency(int authoredAmount)
        => IsRewardedRevisit ? 0 : authoredAmount;

    internal RewardChoiceViewModel BuildDefeatConsolationChoice(CombatContentSnapshot? snapshot)
    {
        var amount = 0;
        if (HasPersistentDefeatConsolationContext
            && snapshot != null)
        {
            var chapterId = ResolveChapterId();
            amount = CampaignRecoveryRewardPolicy.GetDefeatConsolationEcho(
                CampaignRecoveryRewardPolicy.ResolveFirstFarmRunEcho(snapshot, chapterId),
                ResolvePendingDefeatIndex());
        }

        return new RewardChoiceViewModel(
            RewardChoiceKind.Echo,
            "ui.reward.choice.tactical_notes.title",
            "ui.reward.choice.tactical_notes.desc",
            0,
            amount,
            0,
            DefeatEchoRewardId);
    }

    internal void CommitDefeatConsolationClaim()
    {
        if (!HasPersistentDefeatConsolationContext)
        {
            return;
        }

        var chapterId = ResolveChapterId();
        var counts = _session.Profile.CampaignProgress.DefeatConsolationCountsByChapter
                     ??= new Dictionary<string, int>(StringComparer.Ordinal);
        counts[chapterId] = ResolvePendingDefeatIndex();
    }

    internal string BuildDefeatConsolationCommitId(string battleContextHash)
    {
        if (string.IsNullOrWhiteSpace(battleContextHash))
        {
            return string.Empty;
        }

        return RewardCommitIdService.Compute(
            battleContextHash,
            $"defeat-consolation-{ResolvePendingDefeatIndex()}");
    }

    /// <summary>
    /// Returns true when the call was planned as a revisit, including an exhausted (zero reward) run.
    /// The owning expedition flow performs mutations so this collaborator does not require private
    /// GameSessionState access.
    /// </summary>
    internal bool TryBuildRevisitAutomaticLoot(
        CombatContentSnapshot? snapshot,
        IReadOnlyList<string> contextTags,
        out ActiveRunState updatedRun,
        out IReadOnlyList<LootEntry> plannedEntries)
    {
        updatedRun = null!;
        plannedEntries = System.Array.Empty<LootEntry>();
        if (!IsRewardedRevisit || _session.ActiveRun == null)
        {
            return false;
        }

        var run = _session.ActiveRun;
        var revisitIndex = run.Overlay.RewardedRevisitIndex;
        var entries = new List<LootEntry>();
        var currencyGranted = run.Overlay.RevisitCurrencyGranted;
        var itemRollsGranted = System.Math.Max(0, run.Overlay.RevisitItemRollsGranted);

        if (snapshot == null)
        {
            updatedRun = BuildUpdatedRun(run, currencyGranted: true, itemRollsGranted);
            plannedEntries = entries;
            return true;
        }

        if (!currencyGranted)
        {
            CommitRewardedRevisit(revisitIndex);
            var gold = CampaignRecoveryRewardPolicy.GetRevisitGold(revisitIndex);
            var firstFarmRunEcho = CampaignRecoveryRewardPolicy.ResolveFirstFarmRunEcho(
                snapshot,
                ResolveChapterId());
            var echo = CampaignRecoveryRewardPolicy.GetRevisitEcho(
                firstFarmRunEcho,
                revisitIndex);
            if (gold > 0)
            {
                entries.Add(new LootEntry(
                    RevisitGoldRewardId,
                    RewardType.Gold,
                    gold,
                    RarityBracketValue.Common));
            }

            if (echo > 0)
            {
                entries.Add(new LootEntry(
                    RevisitEchoRewardId,
                    RewardType.Echo,
                    echo,
                    RarityBracketValue.Common));
            }

            currencyGranted = true;
        }

        var itemRollLimit = CampaignRecoveryRewardPolicy.GetItemRollCount(revisitIndex);
        while (itemRollsGranted < itemRollLimit
               && !string.IsNullOrWhiteSpace(run.Overlay.RewardSourceId))
        {
            var lootService = new LootResolutionService(snapshot);
            var rollOrdinal = itemRollsGranted + 1;
            var rollSeed = CampaignEncounterSeed.Derive(
                run.Overlay.BattleSeed,
                $"revisit-item|{revisitIndex}|{rollOrdinal}");
            if (lootService.TryResolveItemRoll(
                    run.Overlay.RewardSourceId,
                    rollSeed,
                    contextTags,
                    out var item,
                    out _))
            {
                entries.Add(item);
            }

            // A roll is consumed even when an authored table has no eligible item entry.
            itemRollsGranted = rollOrdinal;
        }

        updatedRun = BuildUpdatedRun(run, currencyGranted, itemRollsGranted);
        plannedEntries = entries;
        return true;
    }

    private static ActiveRunState BuildUpdatedRun(
        ActiveRunState run,
        bool currencyGranted,
        int itemRollsGranted)
    {
        return run with
        {
            Overlay = run.Overlay with
            {
                RevisitCurrencyGranted = currencyGranted,
                RevisitItemRollsGranted = itemRollsGranted,
            },
        };
    }

    private string ResolveChapterId()
    {
        var activeChapterId = _session.ActiveRun?.Overlay.ChapterId;
        return string.IsNullOrWhiteSpace(activeChapterId)
            ? _session.Profile.CampaignProgress.SelectedChapterId
            : activeChapterId;
    }

    private void CommitRewardedRevisit(int revisitIndex)
    {
        var chapterId = ResolveChapterId();
        var counts = _session.Profile.CampaignProgress.RewardedRevisitCountsByChapter
                     ??= new Dictionary<string, int>(StringComparer.Ordinal);
        var previous = counts.TryGetValue(chapterId, out var value)
            ? Math.Max(0, value)
            : 0;
        counts[chapterId] = Math.Max(previous, revisitIndex);
    }

    private int ResolvePendingDefeatIndex()
    {
        var chapterId = ResolveChapterId();
        var counts = _session.Profile.CampaignProgress.DefeatConsolationCountsByChapter
                     ??= new Dictionary<string, int>(StringComparer.Ordinal);
        var previous = counts.TryGetValue(chapterId, out var value)
            ? System.Math.Max(0, value)
            : 0;
        var exhaustedIndex = CampaignRecoveryRewardPolicy.RewardedDefeatLimit + 1;
        return previous >= exhaustedIndex ? exhaustedIndex : previous + 1;
    }
}
