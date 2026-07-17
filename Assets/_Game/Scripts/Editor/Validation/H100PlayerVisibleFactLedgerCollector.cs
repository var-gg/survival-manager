using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>campaign runner의 관측→결정 순서를 fact ledger로 조립하고 매 decision을 즉시 검증한다.</summary>
internal sealed class H100PlayerVisibleFactLedgerCollector
{
    private readonly string _runId;
    private readonly List<PlayerVisibleFactRecord> _facts = new();
    private readonly List<PlayerVisibleDecisionRecord> _decisions = new();

    public H100PlayerVisibleFactLedgerCollector(string runId)
    {
        _runId = runId ?? string.Empty;
    }

    public IReadOnlyList<PlayerVisibleFactRecord> Facts => _facts;

    public IReadOnlyList<PlayerVisibleDecisionRecord> Decisions => _decisions;

    public HeadlessPolicyObservation Observe(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        HeadlessPolicyObservation observation)
    {
        var projection = H100PlayerVisibleFactProjector.Project(
            _runId,
            campaignId,
            new PlayerVisibleTimelinePoint(campaignIndex, siteIndex, decisionIndex),
            observation);
        _facts.AddRange(projection.Facts);
        return projection.Observation;
    }

    public HeadlessRosterPolicyObservation ObserveRoster(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        HeadlessRosterPolicyObservation observation)
    {
        var projection = H100RosterPlayerVisibleFactProjector.Project(
            _runId,
            campaignId,
            new PlayerVisibleTimelinePoint(campaignIndex, siteIndex, decisionIndex),
            observation);
        _facts.AddRange(projection.Facts);
        return projection.Observation;
    }

    public void RecordDeployment(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        string policyId,
        HeadlessDeploymentDecision decision)
    {
        var action = string.Join("|", decision.Placements
            .OrderBy(value => value.Anchor)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .Select(value => $"{((int)value.Anchor).ToString(CultureInfo.InvariantCulture)}:{value.HeroId}"));
        Record(PlayerVisibleDecisionRecord.Create(
            _runId,
            campaignId,
            new PlayerVisibleTimelinePoint(campaignIndex, siteIndex, decisionIndex),
            policyId,
            "deployment",
            action,
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds));
    }

    public void RecordReward(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        string policyId,
        HeadlessRewardDecision decision)
    {
        Record(PlayerVisibleDecisionRecord.Create(
            _runId,
            campaignId,
            new PlayerVisibleTimelinePoint(campaignIndex, siteIndex, decisionIndex),
            policyId,
            "reward",
            $"option:{decision.OptionIndex.ToString(CultureInfo.InvariantCulture)}",
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds));
    }

    public void RecordRecruit(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        string policyId,
        HeadlessRecruitDecision decision)
        => RecordRosterDecision(
            campaignId,
            campaignIndex,
            siteIndex,
            decisionIndex,
            policyId,
            "recruit",
            $"offer:{decision.OfferIndex.ToString(CultureInfo.InvariantCulture)}",
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds);

    public void RecordPassive(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        string policyId,
        HeadlessPassiveDecision decision)
        => RecordRosterDecision(
            campaignId,
            campaignIndex,
            siteIndex,
            decisionIndex,
            policyId,
            "level_node",
            decision.IsNoOp ? "node:none" : $"node:{decision.HeroId}:{decision.NodeId}",
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds);

    public void RecordRefit(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        string policyId,
        HeadlessRefitDecision decision)
        => RecordRosterDecision(
            campaignId,
            campaignIndex,
            siteIndex,
            decisionIndex,
            policyId,
            "refit",
            decision.IsNoOp ? "refit:none" : $"refit:{decision.ItemInstanceId}:{decision.AffixSlotIndex.ToString(CultureInfo.InvariantCulture)}",
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds);

    private void RecordRosterDecision(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        string policyId,
        string decisionKind,
        string action,
        string rationale,
        double estimatedValue,
        IReadOnlyList<string> evidenceFactIds)
        => Record(PlayerVisibleDecisionRecord.Create(
            _runId,
            campaignId,
            new PlayerVisibleTimelinePoint(campaignIndex, siteIndex, decisionIndex),
            policyId,
            decisionKind,
            action,
            rationale,
            estimatedValue,
            evidenceFactIds));

    private void Record(PlayerVisibleDecisionRecord decision)
    {
        PlayerVisibleFactLedgerAuditor.ValidateDecision(_facts, decision);
        _decisions.Add(decision);
    }
}
