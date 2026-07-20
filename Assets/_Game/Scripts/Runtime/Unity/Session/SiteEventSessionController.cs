using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

/// <summary>사건 제시와 headless 선택을 소유하는 세션 외부 controller.</summary>
public sealed class SiteEventSessionController
{
    private readonly GameSessionState _session;
    private readonly List<SiteEventRecruitOffer> _pendingRecruitOffers = new();
    private readonly List<string> _consumableIds = new();
    private int _recruitOffersGrantedAtSite;
    private string _recruitOfferSiteId = string.Empty;
    private int _pendingExtractBonusEcho;

    internal SiteEventSessionController(GameSessionState session)
    {
        _session = session;
    }

    public SiteEventPresentationViewModel? PendingPresentation { get; private set; }
    public IReadOnlyList<SiteEventRecruitOffer> PendingRecruitOffers => _pendingRecruitOffers;
    public IReadOnlyList<string> ConsumableIds => _consumableIds;
    public int PendingExtractBonusEcho => _pendingExtractBonusEcho;

    internal void ResetRunState()
    {
        PendingPresentation = null;
        _pendingRecruitOffers.Clear();
        _consumableIds.Clear();
        _recruitOffersGrantedAtSite = 0;
        _recruitOfferSiteId = string.Empty;
        _pendingExtractBonusEcho = 0;
    }

    internal bool TryPresent(ExpeditionNodeViewModel node)
    {
        if (node.NodeKind != SiteNodeKindValue.Event
            || string.IsNullOrWhiteSpace(node.EventId)
            || !_session.TryGetSiteEventSnapshot(out var snapshot)
            || snapshot.SiteEvents == null
            || !snapshot.SiteEvents.TryGetValue(node.EventId, out var template)
            || (!string.Equals(template.SiteId, "*", StringComparison.Ordinal)
                && !string.Equals(template.SiteId, _session.Profile.CampaignProgress.SelectedSiteId, StringComparison.Ordinal)))
        {
            return false;
        }

        _session.PrepareSiteEventNode(node);
        if (!string.Equals(_recruitOfferSiteId, _session.Profile.CampaignProgress.SelectedSiteId, StringComparison.Ordinal))
        {
            _recruitOfferSiteId = _session.Profile.CampaignProgress.SelectedSiteId;
            _recruitOffersGrantedAtSite = 0;
        }

        var setupKey = (template.SetupKey ?? string.Empty)
            .Replace("{siteId}", _session.Profile.CampaignProgress.SelectedSiteId, StringComparison.Ordinal);
        PendingPresentation = new SiteEventPresentationViewModel(
            template.Id,
            setupKey,
            template.Choices
                .Select(choice => new SiteEventChoiceViewModel(choice.Id, choice.LabelKey))
                .ToArray());
        return true;
    }

    public IReadOnlyList<SiteEventLegalAction> GetLegalActions()
    {
        if (!TryGetPendingContext(out var template, out var node, out var snapshot))
        {
            return Array.Empty<SiteEventLegalAction>();
        }

        var state = CaptureResolutionState(node);
        return template.Choices
            .Where(choice => SiteEventOutcomeApplier.Apply(choice, state, snapshot.WarWound!).IsSuccess)
            .Select(choice => new SiteEventLegalAction(template.Id, choice.Id))
            .ToArray();
    }

    public bool ApplyChoice(string choiceId)
    {
        if (string.IsNullOrWhiteSpace(choiceId)
            || !TryGetPendingContext(out var template, out var node, out var snapshot))
        {
            return false;
        }

        var choice = template.Choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, choiceId, StringComparison.Ordinal));
        if (choice == null) return false;

        var application = SiteEventOutcomeApplier.Apply(choice, CaptureResolutionState(node), snapshot.WarWound!);
        if (!application.IsSuccess) return false;

        ApplyResolvedState(application.State);
        PendingPresentation = null;
        _session.CompleteSiteEventNode(node, application.State.Run);
        return true;
    }

    internal int ConsumeExtractBonusIfNeeded(ExpeditionNodeViewModel node)
    {
        if (node.NodeKind != SiteNodeKindValue.Extract || _pendingExtractBonusEcho == 0)
        {
            return 0;
        }

        var bonus = _pendingExtractBonusEcho;
        _session.Profile.Currencies.Echo = checked(_session.Profile.Currencies.Echo + bonus);
        _pendingExtractBonusEcho = 0;
        return bonus;
    }

    private bool TryGetPendingContext(
        out SiteEventTemplate template,
        out ExpeditionNodeViewModel node,
        out CombatContentSnapshot snapshot)
    {
        template = null!;
        node = null!;
        snapshot = null!;
        if (PendingPresentation == null
            || !_session.TryGetSiteEventSnapshot(out snapshot)
            || snapshot.WarWound == null
            || snapshot.SiteEvents == null
            || !snapshot.SiteEvents.TryGetValue(PendingPresentation.EventId, out template))
        {
            return false;
        }

        node = _session.GetSelectedExpeditionNode() ?? _session.GetCurrentExpeditionNode()!;
        return node != null
               && node.NodeKind == SiteNodeKindValue.Event
               && string.Equals(node.EventId, template.Id, StringComparison.Ordinal);
    }

    private SiteEventResolutionState CaptureResolutionState(ExpeditionNodeViewModel node)
    {
        var experience = _session.Profile.HeroProgressions
            .Where(record => !string.IsNullOrWhiteSpace(record.HeroId))
            .GroupBy(record => record.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Experience, StringComparer.Ordinal);
        foreach (var hero in _session.Profile.Heroes.Where(hero => !string.IsNullOrWhiteSpace(hero.HeroId)))
        {
            experience.TryAdd(hero.HeroId, 0);
        }

        var legalRouteIds = (node.NextNodeIndices ?? Array.Empty<int>())
            .Where(index => index >= 0 && index < _session.ExpeditionNodes.Count)
            .Select(index => _session.ExpeditionNodes[index].GraphNodeId)
            .ToArray();
        return new SiteEventResolutionState(
            _session.ActiveRun!,
            _session.Profile.Currencies.Echo,
            experience,
            _recruitOffersGrantedAtSite,
            1,
            _pendingExtractBonusEcho,
            Array.Empty<SiteEventItemGrant>(),
            Array.Empty<string>(),
            Array.Empty<SiteEventRecruitOffer>(),
            string.Empty,
            legalRouteIds);
    }

    private void ApplyResolvedState(SiteEventResolutionState resolved)
    {
        _session.Profile.Currencies.Echo = resolved.Echo;
        _recruitOffersGrantedAtSite = resolved.RecruitOffersGrantedAtSite;
        _pendingExtractBonusEcho = resolved.ExtractBonusEcho;

        foreach (var pair in resolved.HeroExperienceById)
        {
            var progression = _session.Profile.HeroProgressions.FirstOrDefault(record =>
                string.Equals(record.HeroId, pair.Key, StringComparison.Ordinal));
            if (progression == null)
            {
                progression = new HeroProgressionRecord { HeroId = pair.Key, Level = 1 };
                _session.Profile.HeroProgressions.Add(progression);
            }

            progression.Experience = pair.Value;
        }

        foreach (var grant in resolved.GrantedItems)
        {
            _session.Profile.ItemInstanceCounter = checked(_session.Profile.ItemInstanceCounter + 1L);
            _session.Profile.Inventory.Add(new InventoryItemRecord
            {
                ItemInstanceId = $"{grant.ItemBaseId}-event-i{_session.Profile.ItemInstanceCounter.ToString(CultureInfo.InvariantCulture)}",
                ItemBaseId = grant.ItemBaseId,
                AffixIds = string.IsNullOrWhiteSpace(grant.AffixId)
                    ? new List<string>()
                    : new List<string> { grant.AffixId },
            });
        }

        _consumableIds.AddRange(resolved.GrantedConsumableIds);
        _pendingRecruitOffers.AddRange(resolved.GrantedRecruitOffers);
        if (!string.IsNullOrWhiteSpace(resolved.SelectedRouteNodeId))
        {
            var route = _session.ExpeditionNodes.FirstOrDefault(candidate =>
                string.Equals(candidate.GraphNodeId, resolved.SelectedRouteNodeId, StringComparison.Ordinal));
            if (route != null)
            {
                _session.SelectNextExpeditionNode(route.Index);
            }
        }
    }
}
