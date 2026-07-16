using System;
using System.Globalization;
using System.Text;
using SM.Combat.Model;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
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
            H100PolicyObservationBuilder.Build(session, lookup, decisionSeed),
            decisionLog);

    public static HeadlessDeploymentDecision ApplyPolicyDeployment(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        int decisionSeed,
        HeadlessPolicyObservation observation,
        Action<string>? decisionLog = null)
    {
        if (observation.DecisionSeed != decisionSeed)
        {
            throw new InvalidOperationException("Prepared deployment observation seed does not match the requested decision seed.");
        }

        var decision = policy.DecideDeployment(observation);
        HeadlessPolicyGuard.ValidateDeploymentDecision(observation, decision);
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
            H100PolicyObservationBuilder.Build(session, lookup, decisionSeed),
            decisionLog);

    public static HeadlessRewardDecision ApplyPolicyReward(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        int decisionSeed,
        HeadlessPolicyObservation observation,
        Action<string>? decisionLog = null)
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
}
