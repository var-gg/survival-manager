using System.Collections.Generic;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>파일 출력 projection과 oracle 재실행용 profile payload를 함께 보관하는 Editor-only capture.</summary>
internal sealed record H100SunkenCapturedArrival(
    SunkenArrivalSnapshotRecord Snapshot,
    string ProfileSnapshot,
    HeadlessDeploymentDecision ChosenDecision,
    int CampaignSeed,
    int BattleStartIndex,
    H100SunkenLookbackCheckpoint? Lookback);

internal sealed record H100SunkenLookbackCheckpoint(
    string CampaignId,
    string SiteId,
    string ProfileSnapshot,
    IReadOnlyList<SunkenArrivalSnapshotRecord.RewardOption> RewardOptions,
    IReadOnlyList<SunkenArrivalSnapshotRecord.RecruitOffer> RecruitOffers,
    int ChosenOptionIndex,
    string ChosenPayloadId);
