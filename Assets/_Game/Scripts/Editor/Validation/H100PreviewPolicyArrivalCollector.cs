using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

internal sealed record H100PreviewPolicyArrival(
    string SampleId,
    string BaselinePolicyId,
    string SiteId,
    int SiteIndex,
    int CampaignSeed,
    int BattleStartIndex,
    int DecisionSeed,
    string ProfileSnapshot,
    HeadlessPolicyObservation Observation,
    HeadlessDeploymentDecision BaselineDecision);

/// <summary>배치 전 profile/observation과 직후 baseline 결정을 target site별로 결합한다.</summary>
internal sealed class H100PreviewPolicyArrivalCollector
{
    private readonly string _baselinePolicyId;
    private readonly IReadOnlyList<string> _targetSiteIds;
    private readonly int _targetCount;
    private readonly Dictionary<string, PendingArrival> _pending = new(StringComparer.Ordinal);
    private readonly List<H100PreviewPolicyArrival> _arrivals = new();

    public H100PreviewPolicyArrivalCollector(
        string baselinePolicyId,
        IReadOnlyList<string> targetSiteIds,
        int targetCount)
    {
        _baselinePolicyId = baselinePolicyId;
        _targetSiteIds = targetSiteIds;
        _targetCount = Math.Max(1, targetCount);
        Hooks = new H100CampaignObservationHooks(
            SiteArrived: OnSiteArrived,
            StopRequested: HasCompleteMatrix,
            DeploymentOffered: OnDeploymentOffered);
    }

    public H100CampaignObservationHooks Hooks { get; }
    public IReadOnlyList<H100PreviewPolicyArrival> Arrivals => _arrivals;

    private void OnDeploymentOffered(H100DeploymentOfferedContext context)
    {
        var siteId = context.Session.SelectedCampaignSiteId;
        if (!_targetSiteIds.Contains(siteId, StringComparer.Ordinal)
            || Count(siteId) >= _targetCount)
        {
            return;
        }

        _pending[Key(context.CampaignId, siteId)] = new PendingArrival(
            context.CampaignId,
            siteId,
            context.SiteIndex,
            context.CampaignSeed,
            context.BattleStartIndex,
            context.DecisionSeed,
            H100ProfileSnapshotCodec.Capture(context.Session.Profile),
            context.Observation);
    }

    private void OnSiteArrived(H100SiteArrivalContext context)
    {
        var siteId = context.Session.SelectedCampaignSiteId;
        var key = Key(context.CampaignId, siteId);
        if (!_pending.Remove(key, out var pending)
            || Count(siteId) >= _targetCount)
        {
            return;
        }

        _arrivals.Add(new H100PreviewPolicyArrival(
            $"{_baselinePolicyId}-{context.CampaignId}-{context.CampaignSeed:D10}-{siteId}",
            _baselinePolicyId,
            siteId,
            pending.SiteIndex,
            pending.CampaignSeed,
            pending.BattleStartIndex,
            pending.DecisionSeed,
            pending.ProfileSnapshot,
            pending.Observation,
            context.Decision));
    }

    private bool HasCompleteMatrix()
        => _targetSiteIds.All(siteId => Count(siteId) >= _targetCount);

    private int Count(string siteId)
        => _arrivals.Count(value => string.Equals(value.SiteId, siteId, StringComparison.Ordinal));

    private static string Key(string campaignId, string siteId) => $"{campaignId}|{siteId}";

    private sealed record PendingArrival(
        string CampaignId,
        string SiteId,
        int SiteIndex,
        int CampaignSeed,
        int BattleStartIndex,
        int DecisionSeed,
        string ProfileSnapshot,
        HeadlessPolicyObservation Observation);
}
