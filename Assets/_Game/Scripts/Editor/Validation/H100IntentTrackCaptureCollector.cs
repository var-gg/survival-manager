using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.Meta.Model;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>실 campaign hook을 종료 후 oracle이 읽을 immutable 성격의 offer/battle capture로 투영한다.</summary>
internal sealed class H100IntentTrackCaptureCollector
{
    private readonly IReadOnlyList<ConceptContract> _contracts;
    private readonly CombatContentSnapshot _snapshot;
    private readonly RuntimeCombatContentLookup _lookup;
    private readonly IReadOnlyList<FormationPlacement> _formations;
    private readonly Dictionary<string, H100IntentTrackCampaignCapture> _captures = new(StringComparer.Ordinal);

    public H100IntentTrackCaptureCollector(
        IReadOnlyList<ConceptContract> contracts,
        CombatContentSnapshot snapshot,
        RuntimeCombatContentLookup lookup,
        IReadOnlyList<FormationPlacement> formations)
    {
        _contracts = contracts == null || contracts.Count == 0
            ? throw new ArgumentException("Intent-track capture contracts are required.", nameof(contracts))
            : contracts;
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _formations = formations ?? throw new ArgumentNullException(nameof(formations));
        Hooks = new H100CampaignObservationHooks(
            RewardOffered: OnRewardOffered,
            DeploymentOffered: OnDeploymentOffered,
            BattleCompleted: OnBattleCompleted,
            RosterDecisionOffered: OnRosterDecisionOffered);
    }

    public H100CampaignObservationHooks Hooks { get; }

    public H100IntentTrackCampaignCapture Require(string campaignId)
    {
        if (!_captures.TryGetValue(campaignId, out var capture) || capture.InitialState == null)
        {
            throw new InvalidOperationException($"Intent-track capture is incomplete for {campaignId}.");
        }

        return capture;
    }

    private void OnDeploymentOffered(H100DeploymentOfferedContext context)
    {
        var capture = GetOrCreate(context.CampaignId, context.CampaignIndex, context.CampaignSeed);
        capture.InitialState ??= H100IntentTrackInputProjector.ProjectInitialState(
            context.Observation,
            context.Session.Profile,
            _snapshot);
        capture.AddWindow(new IntentTrackAgencyWindow(
            context.DecisionIndex,
            IntentTrackLeverId.Deployment,
            $"{context.Session.SelectedCampaignChapterId}|{context.Session.SelectedCampaignSiteId}|deployment|{context.DecisionSeed}",
            context.BattleStartIndex,
            H100IntentTrackInputProjector.ProjectDeploymentChoices(
                context.Observation,
                _contracts,
                _formations,
                _snapshot)));
    }

    private void OnRewardOffered(H100RewardOfferedContext context)
    {
        var capture = GetOrCreate(context.CampaignId, context.CampaignIndex, context.CampaignSeed);
        capture.AddWindow(new IntentTrackAgencyWindow(
            context.DecisionIndex,
            IntentTrackLeverId.Reward,
            $"{context.Session.SelectedCampaignChapterId}|{context.Session.SelectedCampaignSiteId}|reward|{context.DecisionSeed}",
            context.BattleIndex,
            H100IntentTrackInputProjector.ProjectRewardChoices(context.Observation)));
    }

    private void OnBattleCompleted(H100BattleCompletedContext context)
    {
        var capture = GetOrCreate(context.CampaignId, context.CampaignIndex, campaignSeed: 0);
        capture.AddBattle(new H100IntentTrackBattleCapture(
            context.BattleIndex,
            H100IntentTrackPayoffProjector.Project(context.Result)));
    }

    private void OnRosterDecisionOffered(H100RosterDecisionOfferedContext context)
    {
        var capture = GetOrCreate(context.CampaignId, context.CampaignIndex, context.CampaignSeed);
        var choices = context.LeverId switch
        {
            IntentTrackLeverId.Recruit => H100RosterIntentTrackInputProjector.ProjectRecruitChoices(
                context.Observation,
                _contracts),
            IntentTrackLeverId.LevelNode => H100RosterIntentTrackInputProjector.ProjectPassiveChoices(
                context.Observation,
                _contracts,
                _snapshot),
            IntentTrackLeverId.Refit => H100RosterIntentTrackInputProjector.ProjectRefitChoices(
                context.Observation,
                context.Session,
                _lookup,
                _contracts),
            _ => throw new InvalidOperationException($"Unknown roster intent-track lever '{context.LeverId}'."),
        };
        capture.AddWindow(new IntentTrackAgencyWindow(
            context.DecisionIndex,
            context.LeverId,
            $"{context.Session.SelectedCampaignChapterId}|{context.Session.SelectedCampaignSiteId}|town|{context.LeverId}|{context.DecisionSeed}",
            context.BattleIndex,
            choices));
    }

    private H100IntentTrackCampaignCapture GetOrCreate(string campaignId, int campaignIndex, int campaignSeed)
    {
        if (_captures.TryGetValue(campaignId, out var existing))
        {
            return existing;
        }

        var capture = new H100IntentTrackCampaignCapture(campaignId, campaignIndex, campaignSeed);
        _captures.Add(campaignId, capture);
        return capture;
    }
}

internal sealed class H100IntentTrackCampaignCapture
{
    private readonly List<IntentTrackAgencyWindow> _windows = new();
    private readonly List<H100IntentTrackBattleCapture> _battles = new();

    public H100IntentTrackCampaignCapture(string campaignId, int campaignIndex, int campaignSeed)
    {
        CampaignId = campaignId;
        CampaignIndex = campaignIndex;
        CampaignSeed = campaignSeed;
    }

    public string CampaignId { get; }
    public int CampaignIndex { get; }
    public int CampaignSeed { get; }
    public IntentTrackState? InitialState { get; set; }
    public IReadOnlyList<IntentTrackAgencyWindow> Windows => _windows;
    public IReadOnlyList<H100IntentTrackBattleCapture> Battles => _battles;

    public void AddWindow(IntentTrackAgencyWindow window)
    {
        if (_windows.Any(value => value.WindowIndex == window.WindowIndex))
        {
            throw new InvalidOperationException($"Duplicate intent-track agency window: {CampaignId}/{window.WindowIndex}");
        }

        _windows.Add(window);
    }

    public void AddBattle(H100IntentTrackBattleCapture battle)
    {
        if (_battles.Any(value => value.BattleIndex == battle.BattleIndex))
        {
            throw new InvalidOperationException($"Duplicate intent-track battle: {CampaignId}/{battle.BattleIndex}");
        }

        _battles.Add(battle);
    }
}

internal sealed record H100IntentTrackBattleCapture(
    int BattleIndex,
    IReadOnlyList<string> PayoffWitnessIds);
