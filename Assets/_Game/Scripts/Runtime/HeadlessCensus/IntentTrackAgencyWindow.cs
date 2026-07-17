using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

public static class IntentTrackLeverId
{
    public const string Deployment = "deployment";
    public const string Reward = "reward";
    public const string Recruit = "recruit";
    public const string LevelNode = "level_node";
    public const string Refit = "refit";
}

/// <summary>확정된 실제 offer window 안의 합법 선택 하나를 pure state delta로 낮춘 DTO.</summary>
public sealed record IntentTrackChoice(
    string ChoiceId,
    IReadOnlyList<string> RequiredRosterMemberIds,
    IReadOnlyList<string> RequiredOwnedComponentIds,
    IReadOnlyList<IntentTrackRosterMember> AddedRosterMembers,
    IReadOnlyList<string> AddedInventoryComponentIds,
    IReadOnlyList<string> AddedSkillIds,
    IReadOnlyList<string> AddedPassiveIds,
    IReadOnlyList<string> AddedOwnedComponentIds,
    int RecruitResourceDelta,
    int RecruitResourceCost,
    int PassiveBudgetDelta,
    int PassiveBudgetCost,
    int RefitResourceDelta,
    int RefitResourceCost,
    IReadOnlyList<string> DeployedMemberIds,
    IReadOnlyList<IntentTrackTagCount> DeployedTagCounts,
    IReadOnlyList<string> ActiveComponentIds,
    IReadOnlyList<string> ActiveEffectIds,
    IReadOnlyList<string> ActiveTeamRuleIds,
    FormationFeatures? Formation,
    IReadOnlyList<string> OfferedSemanticIds,
    bool Irreversible)
{
    public static IntentTrackChoice NoOp(string choiceId) => new(
        choiceId,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<IntentTrackRosterMember>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        0,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<string>(),
        Array.Empty<IntentTrackTagCount>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        null,
        Array.Empty<string>(),
        false);
}

/// <summary>플레이어 선택이 실제로 발생한 한 지점. opt-in roster policy는 Town phase의 세 subwindow도 기록한다.</summary>
public sealed record IntentTrackAgencyWindow(
    int WindowIndex,
    string LeverId,
    string SourceId,
    int BattleOpportunityStartIndex,
    IReadOnlyList<IntentTrackChoice> Choices);

public sealed record IntentTrackSearchInput(
    ConceptContract Contract,
    IntentTrackState InitialState,
    IReadOnlyList<IntentTrackAgencyWindow> Windows,
    IReadOnlyList<string> EnabledLeverIds,
    int CommitWindowIndex,
    int HorizonWindowCount);
