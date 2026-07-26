using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>Town roster observation과 provenance fact index를 같은 visible snapshot에서 만든다.</summary>
internal static class H100RosterPlayerVisibleFactProjector
{
    public static HeadlessRosterPolicyObservation AttachEvidenceIndex(HeadlessRosterPolicyObservation observation)
        => Project(string.Empty, string.Empty, new PlayerVisibleTimelinePoint(0, 0, 0), observation).Observation;

    public static H100RosterPlayerVisibleFactProjection Project(
        string runId,
        string campaignId,
        PlayerVisibleTimelinePoint observedAt,
        HeadlessRosterPolicyObservation observation)
    {
        if (observation == null) throw new ArgumentNullException(nameof(observation));
        var drafts = BuildDrafts(observation);
        var projected = drafts.Select(draft => new
        {
            draft.EvidenceKey,
            Fact = PlayerVisibleFactRecord.Create(
                runId,
                campaignId,
                observedAt,
                draft.UiSource,
                draft.Subject,
                draft.Verb,
                draft.Target,
                draft.Condition,
                draft.StackOrThreshold,
                draft.AcquisitionHint,
                draft.SourceText),
        }).ToArray();
        var index = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in projected)
        {
            if (!string.IsNullOrWhiteSpace(value.EvidenceKey)) index[value.EvidenceKey] = value.Fact.FactId;
        }

        return new H100RosterPlayerVisibleFactProjection(
            observation.WithEvidenceFactIds(index),
            projected.Select(value => value.Fact)
                .GroupBy(value => value.FactId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.FactId, StringComparer.Ordinal)
                .ToArray());
    }

    private static IReadOnlyList<FactDraft> BuildDrafts(HeadlessRosterPolicyObservation observation)
    {
        var result = new List<FactDraft>();
        Add(result, HeadlessRosterPolicyEvidence.CampaignContextSignal, PlayerVisibleUiSource.CampaignMap,
            "town_roster_window", "shows", $"chapter={observation.ChapterId};site={observation.SiteId}",
            "current Town phase", string.Empty, "campaign map", $"{observation.ChapterId}/{observation.SiteId}");
        Add(result, HeadlessRosterPolicyEvidence.WalletSignal, PlayerVisibleUiSource.TownHudWallet,
            "wallet", "holds", $"gold={observation.Wallet.Gold};echo={observation.Wallet.Echo}",
            "current profile currencies", string.Empty, "town HUD", $"gold {observation.Wallet.Gold}, echo {observation.Wallet.Echo}");

        var recruitSummary = string.Join("|", observation.RecruitOffers.OrderBy(value => value.OfferIndex)
            .Select(offer => $"{offer.OfferIndex}:{offer.ArchetypeId}:{offer.RaceId}:{offer.ClassId}:{offer.FlexActiveSkillId}:{offer.FlexPassiveSkillId}:{offer.GoldCost}:{offer.Tier}:{offer.PlanFit}"));
        Add(result, HeadlessRosterPolicyEvidence.RecruitSurfaceSignal, PlayerVisibleUiSource.TownRoster,
            "recruit_pack", "offers", recruitSummary, "current four-offer pack",
            $"count={observation.RecruitOffers.Count};roster={observation.Roster.Count}/{observation.RosterCapacity}", "recruit panel", recruitSummary);
        foreach (var offer in observation.RecruitOffers.OrderBy(value => value.OfferIndex))
        {
            var text = $"{offer.ArchetypeId};race={offer.RaceId};class={offer.ClassId};role={offer.RoleTag};active={offer.FlexActiveSkillId};passive={offer.FlexPassiveSkillId};cost={offer.GoldCost};tier={offer.Tier};plan={offer.PlanFit};duplicate={offer.IsDuplicate}";
            Add(result, HeadlessRosterPolicyEvidence.RecruitOfferSignal(offer.OfferIndex), PlayerVisibleUiSource.TownRoster,
                $"recruit_offer_{offer.OfferIndex}", "describes", text, "currently purchasable offer", $"gold={offer.GoldCost}", "recruit panel", text);
        }

        Add(result, HeadlessRosterPolicyEvidence.PassiveSurfaceSignal, PlayerVisibleUiSource.RosterSheetPassive,
            "passive_boards", "shows", $"heroes={observation.PassiveHeroes.Count}", "current roster sheets", string.Empty, "passive board", "current passive boards and node graph");
        foreach (var hero in observation.PassiveHeroes.OrderBy(value => value.HeroId, StringComparer.Ordinal))
        {
            var selected = string.Join(",", hero.SelectedNodeIds.OrderBy(value => value, StringComparer.Ordinal));
            Add(result, HeadlessRosterPolicyEvidence.PassiveHeroSignal(hero.HeroId), PlayerVisibleUiSource.RosterSheetPassive,
                hero.HeroId, "shows_budget", $"board={hero.SelectedBoardId};selected={selected}", "current hero passive sheet",
                $"selected={hero.SelectedNodeIds.Count}/{hero.MaxActiveNodeCount};keystone_cap={hero.MaxKeystoneCount}", "passive board", selected);
            foreach (var board in hero.Boards.OrderBy(value => value.BoardId, StringComparer.Ordinal))
            foreach (var node in board.Nodes.OrderBy(value => value.BoardDepth).ThenBy(value => value.NodeId, StringComparer.Ordinal))
            {
                var text = $"board={board.BoardId};node={node.NodeId};depth={node.BoardDepth};kind={node.NodeKind};prereq={string.Join(",", node.PrerequisiteNodeIds)};exclusive={string.Join(",", node.MutualExclusionTagIds)};skill={node.GrantedSkillId};tags={string.Join(",", node.CompileTags)}";
                Add(result, HeadlessRosterPolicyEvidence.PassiveNodeSignal(hero.HeroId, node.NodeId), PlayerVisibleUiSource.RosterSheetPassive,
                    node.NodeId, "describes_node", text, $"hero={hero.HeroId};board={board.BoardId}", $"depth={node.BoardDepth}", "passive board", text);
            }
        }

        Add(result, HeadlessRosterPolicyEvidence.RefitSurfaceSignal, PlayerVisibleUiSource.RosterSheetItem,
            "equipment_refit", "shows", $"items={observation.RefitItems.Count}", "current inventory only", string.Empty, "equipment refit", "current items and affix slots");
        foreach (var item in observation.RefitItems.OrderBy(value => value.ItemId, StringComparer.Ordinal).ThenBy(value => value.ItemInstanceId, StringComparer.Ordinal))
        {
            var qualityText = string.Join(",", item.AffixSlots
                .OrderBy(value => value.SlotIndex)
                .Select(value =>
                    $"{value.CurrentAffix.AffixId}={value.RollQuality.ToString("R", CultureInfo.InvariantCulture)}"));
            var sealCostText = string.Join(",", item.SealCosts
                .OrderBy(value => value.LockedAffixCount)
                .Select(value => $"{value.LockedAffixCount}={value.EchoCost}"));
            var sealText = $"item={item.ItemId};instance={item.ItemInstanceId};allows_seal={item.AllowsSeal};"
                           + $"quality={qualityText};seal_cost_by_lock_count={sealCostText}";
            Add(result, HeadlessRosterPolicyEvidence.RefitSealSignal(item.ItemInstanceId), PlayerVisibleUiSource.RosterSheetItem,
                item.ItemInstanceId, "shows_seal_surface", sealText, "current affix rolls only; future roll hidden",
                sealCostText, "equipment refit", sealText);

        foreach (var slot in item.AffixSlots.OrderBy(value => value.SlotIndex))
        {
            var text = $"item={item.ItemId};instance={item.ItemInstanceId};slot={slot.SlotIndex};affix={slot.CurrentAffix.AffixId};can_refit={slot.CanRefit};cost={item.EchoCost}";
            Add(result, HeadlessRosterPolicyEvidence.RefitSlotSignal(item.ItemInstanceId, slot.SlotIndex), PlayerVisibleUiSource.RosterSheetItem,
                item.ItemInstanceId, "shows_refit_slot", text, "current affix only; future roll hidden", $"echo={item.EchoCost}", "equipment refit", text);
        }
        }

        return result;
    }

    private static void Add(
        ICollection<FactDraft> drafts,
        string evidenceKey,
        string uiSource,
        string subject,
        string verb,
        string target,
        string condition,
        string stackOrThreshold,
        string acquisitionHint,
        string sourceText)
        => drafts.Add(new FactDraft(evidenceKey, uiSource, subject, verb, target, condition, stackOrThreshold, acquisitionHint, sourceText));

    private sealed record FactDraft(
        string EvidenceKey,
        string UiSource,
        string Subject,
        string Verb,
        string Target,
        string Condition,
        string StackOrThreshold,
        string AcquisitionHint,
        string SourceText);
}

internal sealed record H100RosterPlayerVisibleFactProjection(
    HeadlessRosterPolicyObservation Observation,
    IReadOnlyList<PlayerVisibleFactRecord> Facts);
