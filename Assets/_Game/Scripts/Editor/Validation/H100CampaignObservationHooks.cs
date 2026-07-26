using System;
using SM.Combat.Model;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>기존 campaign runner의 truth 경로를 바꾸지 않고 진단 snapshot만 관찰하는 optional hook 묶음.</summary>
internal sealed record H100CampaignObservationHooks(
    Action<H100SiteArrivalContext>? SiteArrived = null,
    Action<H100RewardOfferedContext>? RewardOffered = null,
    Action<H100RewardChosenContext>? RewardChosen = null,
    Func<bool>? StopRequested = null,
    Action<H100DeploymentOfferedContext>? DeploymentOffered = null,
    Action<H100BattleCompletedContext>? BattleCompleted = null,
    Action<H100RosterDecisionOfferedContext>? RosterDecisionOffered = null,
    Action<H100DecisionAppliedContext>? DecisionApplied = null,
    Action<H100PrepOfferedContext>? PrepOffered = null,
    Func<HeadlessRosterPolicyObservation, HeadlessRosterPolicyObservation>? RosterObservationTransform = null);

internal sealed record H100DecisionAppliedContext(
    int DecisionIndex,
    string SeamType,
    int Ordinal,
    GameSessionState Session,
    string AppliedActionDescriptor,
    string ResultMarker);

internal sealed record H100DeploymentOfferedContext(
    string CampaignId,
    int CampaignIndex,
    int CampaignSeed,
    int SiteIndex,
    int BattleStartIndex,
    int DecisionIndex,
    int DecisionSeed,
    GameSessionState Session,
    HeadlessPolicyObservation Observation);

internal sealed record H100SiteArrivalContext(
    string CampaignId,
    int CampaignIndex,
    int CampaignSeed,
    int SiteIndex,
    int BattleStartIndex,
    int DecisionSeed,
    GameSessionState Session,
    HeadlessDeploymentDecision Decision);

internal sealed record H100RewardOfferedContext(
    string CampaignId,
    int CampaignIndex,
    int CampaignSeed,
    int SiteIndex,
    int BattleIndex,
    int DecisionIndex,
    int DecisionSeed,
    GameSessionState Session,
    HeadlessPolicyObservation Observation);

internal sealed record H100PrepOfferedContext(
    string CampaignId,
    int CampaignIndex,
    int CampaignSeed,
    int SiteIndex,
    int BattleIndex,
    int DecisionIndex,
    int DecisionSeed,
    GameSessionState Session,
    HeadlessPolicyObservation Observation);

internal sealed record H100RewardChosenContext(
    string CampaignId,
    int CampaignIndex,
    int CampaignSeed,
    int SiteIndex,
    int BattleIndex,
    GameSessionState Session,
    HeadlessRewardDecision Decision);

internal sealed record H100BattleCompletedContext(
    string CampaignId,
    int CampaignIndex,
    int SiteIndex,
    int BattleIndex,
    BattleResult Result,
    BattleMetricRecord Metric);

internal sealed record H100RosterDecisionOfferedContext(
    string CampaignId,
    int CampaignIndex,
    int CampaignSeed,
    int SiteIndex,
    int BattleIndex,
    int DecisionIndex,
    int DecisionSeed,
    string LeverId,
    GameSessionState Session,
    HeadlessRosterPolicyObservation Observation);
