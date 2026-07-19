using System;
using System.Globalization;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.HeadlessCensus;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.SealedLlmBridge;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>player-visible 정보만 사용하는 deterministic campaign session 동작 집합.</summary>
internal static class H100SessionDriver
{
    public static GameSessionState CreateSession(RuntimeCombatContentLookup lookup, string profileId)
        => CreateSession(lookup, new SaveProfile { ProfileId = profileId });

    public static GameSessionState CreateSession(RuntimeCombatContentLookup lookup, SaveProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var session = new GameSessionState(lookup);
        session.BindProfile(profile);
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    public static HeadlessDeploymentDecision ApplyPolicyDeployment(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        int decisionSeed,
        Action<string>? decisionLog = null)
        => ApplyPolicyDeployment(
            session,
            lookup,
            policy,
            decisionSeed,
            H100PolicyObservationBuilder.Build(
                session,
                lookup,
                decisionSeed,
                includeTownRoster: policy is IHeadlessRosterPolicy),
            decisionLog);

    public static HeadlessDeploymentDecision ApplyPolicyDeployment(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        int decisionSeed,
        HeadlessPolicyObservation observation,
        Action<string>? decisionLog = null,
        int decisionIndex = -1,
        Action<H100DecisionAppliedContext>? decisionApplied = null)
    {
        if (observation.DecisionSeed != decisionSeed)
        {
            throw new InvalidOperationException("Prepared deployment observation seed does not match the requested decision seed.");
        }

        var decision = policy.DecideDeployment(observation);
        HeadlessPolicyGuard.ValidateDeploymentDecision(observation, decision);
        SynchronizeExpeditionSquad(session, decision);
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        foreach (var placement in decision.Placements)
        {
            if (!session.AssignHeroToAnchor(placement.Anchor, placement.HeroId))
            {
                throw new InvalidOperationException($"Validated H100 deployment could not be applied: {placement.HeroId}@{placement.Anchor}.");
            }
        }

        decisionLog?.Invoke(FormatDecisionLog(policy.Id, "deployment", observation, decision.Rationale, decision.EstimatedValue));
        if (decisionApplied != null)
        {
            RequireDecisionIndex(decisionIndex);
            decisionApplied(new H100DecisionAppliedContext(
                decisionIndex,
                IntentTrackLeverId.Deployment,
                0,
                session,
                SealedLlmActionCodec.EncodeDeployment(decision),
                "success"));
        }

        return decision;
    }

    public static HeadlessRecruitDecision ApplyPolicyRecruit(
        GameSessionState session,
        IHeadlessRosterPolicy policy,
        HeadlessRosterPolicyObservation observation,
        Action<string>? decisionLog = null)
    {
        var decision = policy.DecideRecruit(observation);
        HeadlessRosterPolicyGuard.ValidateRecruitDecision(observation, decision);
        if (!decision.IsNoOp)
        {
            var result = session.Recruit(decision.OfferIndex);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Validated H100 recruit could not be applied: {result.Error}");
            }
        }

        decisionLog?.Invoke(FormatRosterDecisionLog("recruit", observation, decision.Rationale, decision.EstimatedValue));
        return decision;
    }

    public static HeadlessPrepDecision ApplyPolicyPrep(
        GameSessionState session,
        IHeadlessPolicy policy,
        IHeadlessPrepPolicy prepPolicy,
        int decisionSeed,
        HeadlessPolicyObservation observation,
        Action<string>? decisionLog = null,
        int decisionIndex = -1,
        Action<H100DecisionAppliedContext>? decisionApplied = null)
    {
        if (observation.DecisionSeed != decisionSeed)
        {
            throw new InvalidOperationException("Prepared prep observation seed does not match the requested decision seed.");
        }

        session.EnsureBattleDeployReady();
        var decision = prepPolicy.DecidePrep(observation);
        HeadlessPrepPolicyGuard.ValidateDecision(observation, decision);
        SynchronizeExpeditionSquad(session, new HeadlessDeploymentDecision(
            decision.Placements,
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds));
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        foreach (var placement in decision.Placements)
        {
            if (!session.AssignHeroToAnchor(placement.Anchor, placement.HeroId))
            {
                throw new InvalidOperationException($"Validated H100 prep placement could not be applied: {placement.HeroId}@{placement.Anchor}.");
            }
        }

        foreach (var assignment in decision.EquipmentAssignments)
        {
            var result = session.ReequipOwnedItemForEncounter(assignment.ItemInstanceId, assignment.HeroId);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Validated H100 prep equipment move could not be applied: {result.Error}");
            }
        }

        decisionLog?.Invoke(FormatDecisionLog(policy.Id, "prep", observation, decision.Rationale, decision.EstimatedValue));
        if (decisionApplied != null)
        {
            RequireDecisionIndex(decisionIndex);
            decisionApplied(new H100DecisionAppliedContext(
                decisionIndex,
                SealedLlmSeamTypes.Prep,
                0,
                session,
                SealedLlmActionCodec.EncodePrep(decision),
                "success"));
        }

        return decision;
    }

    public static HeadlessPassiveDecision ApplyPolicyPassive(
        GameSessionState session,
        IHeadlessRosterPolicy policy,
        HeadlessRosterPolicyObservation observation,
        Action<string>? decisionLog = null)
    {
        var decision = policy.DecidePassiveAllocation(observation);
        HeadlessRosterPolicyGuard.ValidatePassiveDecision(observation, decision);
        if (!decision.IsNoOp)
        {
            var hero = observation.PassiveHeroes.Single(value => string.Equals(value.HeroId, decision.HeroId, StringComparison.Ordinal));
            if (!string.Equals(hero.SelectedBoardId, decision.BoardId, StringComparison.Ordinal))
            {
                var boardResult = session.SelectPassiveBoard(decision.HeroId, decision.BoardId);
                if (!boardResult.IsSuccess)
                {
                    throw new InvalidOperationException($"Validated H100 passive board could not be applied: {boardResult.Error}");
                }
            }

            var nodeResult = session.TogglePassiveNode(decision.HeroId, decision.NodeId);
            if (!nodeResult.IsSuccess)
            {
                throw new InvalidOperationException($"Validated H100 passive node could not be applied: {nodeResult.Error}");
            }
        }

        decisionLog?.Invoke(FormatRosterDecisionLog("level_node", observation, decision.Rationale, decision.EstimatedValue));
        return decision;
    }

    public static HeadlessRefitDecision ApplyPolicyRefit(
        GameSessionState session,
        IHeadlessRosterPolicy policy,
        HeadlessRosterPolicyObservation observation,
        Action<string>? decisionLog = null)
    {
        var decision = policy.DecideRefit(observation);
        HeadlessRosterPolicyGuard.ValidateRefitDecision(observation, decision);
        if (!decision.IsNoOp)
        {
            var result = session.RefitItem(decision.ItemInstanceId, decision.AffixSlotIndex);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Validated H100 refit could not be applied: {result.Error}");
            }
        }

        decisionLog?.Invoke(FormatRosterDecisionLog("refit", observation, decision.Rationale, decision.EstimatedValue));
        return decision;
    }

    public static HeadlessRewardDecision ApplyPolicyReward(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        int decisionSeed,
        Action<string>? decisionLog = null)
        => ApplyPolicyReward(
            session,
            lookup,
            policy,
            decisionSeed,
            H100PolicyObservationBuilder.Build(
                session,
                lookup,
                decisionSeed,
                includeTownRoster: policy is IHeadlessRosterPolicy),
            decisionLog);

    public static HeadlessRewardDecision ApplyPolicyReward(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        int decisionSeed,
        HeadlessPolicyObservation observation,
        Action<string>? decisionLog = null,
        int decisionIndex = -1,
        Action<H100DecisionAppliedContext>? decisionApplied = null)
    {
        if (observation.DecisionSeed != decisionSeed)
        {
            throw new InvalidOperationException("Prepared reward observation seed does not match the requested decision seed.");
        }

        var decision = policy.DecideReward(observation);
        HeadlessPolicyGuard.ValidateRewardDecision(observation, decision);
        if (decision.OptionIndex >= 0 && !session.ApplyRewardChoice(decision.OptionIndex))
        {
            throw new InvalidOperationException($"Validated H100 reward choice could not be applied: {decision.OptionIndex}.");
        }

        decisionLog?.Invoke(FormatDecisionLog(policy.Id, "reward", observation, decision.Rationale, decision.EstimatedValue));
        if (decisionApplied != null)
        {
            RequireDecisionIndex(decisionIndex);
            decisionApplied(new H100DecisionAppliedContext(
                decisionIndex,
                IntentTrackLeverId.Reward,
                0,
                session,
                SealedLlmActionCodec.EncodeReward(decision),
                "success"));
        }

        return decision;
    }

    public static void AdvanceToNextUnclearedSite(GameSessionState session)
    {
        var progress = session.Profile.CampaignProgress;
        if (!progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            return;
        }

        session.TryCycleCampaignSite(+1);
        if (progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            session.TryCycleCampaignChapter(+1);
        }
    }

    public static string ScenarioId(BattleContextState context)
        => $"{context.ChapterId}/{context.SiteId}/{context.SiteNodeIndex.ToString(CultureInfo.InvariantCulture)}/{context.EncounterId}";

    public static int DeriveSeed(string contextHash, int salt)
    {
        unchecked
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var hash = offset;
            var payload = Encoding.UTF8.GetBytes($"h100|{contextHash}|{salt.ToString(CultureInfo.InvariantCulture)}");
            foreach (var value in payload)
            {
                hash ^= value;
                hash *= prime;
            }

            var result = (int)(hash & 0x7fffffffu);
            return result == 0 ? 1 : result;
        }
    }

    private static void RequireDecisionIndex(int decisionIndex)
    {
        if (decisionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decisionIndex),
                decisionIndex,
                "DecisionApplied requires a non-negative decision index.");
        }
    }

    private static void SynchronizeExpeditionSquad(
        GameSessionState session,
        HeadlessDeploymentDecision decision)
    {
        var desired = decision.Placements.Select(value => value.HeroId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var heroId in desired.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (session.ExpeditionSquadHeroIds.Contains(heroId, StringComparer.Ordinal))
            {
                continue;
            }

            var replace = session.ExpeditionSquadHeroIds
                .Where(current => !desired.Contains(current))
                .OrderBy(current => current, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(replace) && !session.ToggleExpeditionHero(replace))
            {
                throw new InvalidOperationException($"H100 could not remove expedition hero '{replace}'.");
            }

            if (!session.ToggleExpeditionHero(heroId))
            {
                throw new InvalidOperationException($"H100 could not add recruited expedition hero '{heroId}'.");
            }
        }
    }

    private static string FormatDecisionLog(
        string policyId,
        string decisionKind,
        HeadlessPolicyObservation observation,
        string rationale,
        double estimatedValue)
    {
        var singleLineRationale = rationale.Replace('\r', ' ').Replace('\n', ' ');
        return $"[H100Policy] policy={policyId} kind={decisionKind} chapter={observation.ChapterId} "
               + $"site={observation.SiteId} seed={observation.DecisionSeed.ToString(CultureInfo.InvariantCulture)} "
               + $"value={estimatedValue.ToString("F3", CultureInfo.InvariantCulture)} reason={singleLineRationale}";
    }

    private static string FormatRosterDecisionLog(
        string decisionKind,
        HeadlessRosterPolicyObservation observation,
        string rationale,
        double estimatedValue)
    {
        var singleLineRationale = rationale.Replace('\r', ' ').Replace('\n', ' ');
        return $"[H100Policy] kind={decisionKind} chapter={observation.ChapterId} "
               + $"site={observation.SiteId} seed={observation.DecisionSeed.ToString(CultureInfo.InvariantCulture)} "
               + $"value={estimatedValue.ToString("F3", CultureInfo.InvariantCulture)} reason={singleLineRationale}";
    }
}
