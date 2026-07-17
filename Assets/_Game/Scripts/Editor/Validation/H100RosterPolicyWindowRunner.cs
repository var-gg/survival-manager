using System;
using SM.HeadlessCensus;
using SM.HeadlessPolicies;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>사이트 사이 Town phase의 recruit → node → refit opt-in 순서를 소유한다.</summary>
internal static class H100RosterPolicyWindowRunner
{
    public static int Run(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        IHeadlessPolicy policy,
        IHeadlessRosterPolicy rosterPolicy,
        string campaignId,
        int campaignIndex,
        int campaignSeed,
        int siteIndex,
        int battleIndex,
        int decisionIndex,
        H100PlayerVisibleFactLedgerCollector factLedger,
        H100IntentTraceCollector intentTrace,
        Action<string>? decisionLog,
        H100CampaignObservationHooks? observationHooks)
    {
        var recruitObservation = Observe(
            session,
            lookup,
            campaignId,
            campaignIndex,
            campaignSeed,
            siteIndex,
            battleIndex,
            decisionIndex,
            IntentTrackLeverId.Recruit,
            factLedger,
            observationHooks);
        var recruit = H100SessionDriver.ApplyPolicyRecruit(session, rosterPolicy, recruitObservation, decisionLog);
        factLedger.RecordRecruit(campaignId, campaignIndex, siteIndex, decisionIndex, policy.Id, recruit);
        intentTrace.Record(campaignId, campaignIndex, siteIndex, decisionIndex, policy);
        decisionIndex++;

        var passiveObservation = Observe(
            session,
            lookup,
            campaignId,
            campaignIndex,
            campaignSeed,
            siteIndex,
            battleIndex,
            decisionIndex,
            IntentTrackLeverId.LevelNode,
            factLedger,
            observationHooks);
        var passive = H100SessionDriver.ApplyPolicyPassive(session, rosterPolicy, passiveObservation, decisionLog);
        factLedger.RecordPassive(campaignId, campaignIndex, siteIndex, decisionIndex, policy.Id, passive);
        intentTrace.Record(campaignId, campaignIndex, siteIndex, decisionIndex, policy);
        decisionIndex++;

        var refitObservation = Observe(
            session,
            lookup,
            campaignId,
            campaignIndex,
            campaignSeed,
            siteIndex,
            battleIndex,
            decisionIndex,
            IntentTrackLeverId.Refit,
            factLedger,
            observationHooks);
        var refit = H100SessionDriver.ApplyPolicyRefit(session, rosterPolicy, refitObservation, decisionLog);
        factLedger.RecordRefit(campaignId, campaignIndex, siteIndex, decisionIndex, policy.Id, refit);
        intentTrace.Record(campaignId, campaignIndex, siteIndex, decisionIndex, policy);
        return decisionIndex + 1;
    }

    private static HeadlessRosterPolicyObservation Observe(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        string campaignId,
        int campaignIndex,
        int campaignSeed,
        int siteIndex,
        int battleIndex,
        int decisionIndex,
        string leverId,
        H100PlayerVisibleFactLedgerCollector factLedger,
        H100CampaignObservationHooks? observationHooks)
    {
        var decisionSeed = H100SessionDriver.DeriveSeed(
            $"{session.SelectedCampaignChapterId}|{session.SelectedCampaignSiteId}|town|{leverId}",
            campaignSeed + siteIndex);
        var observation = factLedger.ObserveRoster(
            campaignId,
            campaignIndex,
            siteIndex,
            decisionIndex,
            H100RosterPolicyObservationBuilder.Build(session, lookup, decisionSeed));
        observationHooks?.RosterDecisionOffered?.Invoke(new H100RosterDecisionOfferedContext(
            campaignId,
            campaignIndex,
            campaignSeed,
            siteIndex,
            battleIndex,
            decisionIndex,
            decisionSeed,
            leverId,
            session,
            observation));
        return observation;
    }
}
