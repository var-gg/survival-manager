using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>트랙 탐색기에 전달되는 authored-object-free roster member snapshot.</summary>
public sealed record IntentTrackRosterMember(
    string MemberId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> ComponentIds,
    IReadOnlyList<string> EffectIds);

public sealed record IntentTrackTagCount(string TagId, int Count);

/// <summary>
/// evaluator-only 탐색 상태. 현재 v1은 roster/deployment/reward만 바꾸지만 passive/refit 자원과
/// inventory/skill/passive 축을 명시해 후속 lever가 같은 탐색기를 확장할 수 있게 한다.
/// </summary>
public sealed record IntentTrackState(
    IReadOnlyList<IntentTrackRosterMember> Roster,
    int RosterCapacity,
    IReadOnlyList<string> InventoryComponentIds,
    IReadOnlyList<string> SkillIds,
    IReadOnlyList<string> PassiveIds,
    IReadOnlyList<string> OwnedComponentIds,
    int RecruitResource,
    int PassiveBudget,
    int RefitResource,
    IReadOnlyList<string> DeployedMemberIds,
    IReadOnlyList<IntentTrackTagCount> DeployedTagCounts,
    IReadOnlyList<string> ActiveComponentIds,
    IReadOnlyList<string> ActiveEffectIds,
    IReadOnlyList<string> ActiveTeamRuleIds,
    FormationFeatures? Formation,
    IReadOnlyList<string> CompletedMilestones)
{
    public static IntentTrackState Empty { get; } = new(
        Array.Empty<IntentTrackRosterMember>(),
        0,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        0,
        0,
        0,
        Array.Empty<string>(),
        Array.Empty<IntentTrackTagCount>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        null,
        Array.Empty<string>());
}
