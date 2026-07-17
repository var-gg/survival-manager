using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Services;
using SM.HeadlessMetrics;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>BT1-E09 case를 production session/sim 경로로 재생하고 관측 전용 trace로 투영한다.</summary>
internal static class H100TacticalAttributionBattleRunner
{
    private const float TargetBattleSeconds = 35f;

    public static IReadOnlyList<PlacementAttributionBattleRecord> Run(
        RuntimeCombatContentLookup lookup,
        string runId,
        IReadOnlyList<H100TacticalAttributionCase> cases,
        int maxBattleSteps)
    {
        var records = new List<PlacementAttributionBattleRecord>(cases.Count);
        foreach (var tacticalCase in cases.OrderBy(value => value.CaseId, StringComparer.Ordinal))
        {
            records.Add(RunCase(lookup, runId, tacticalCase, maxBattleSteps));
        }

        return records;
    }

    private static PlacementAttributionBattleRecord RunCase(
        RuntimeCombatContentLookup lookup,
        string runId,
        H100TacticalAttributionCase tacticalCase,
        int maxBattleSteps)
    {
        try
        {
            var session = H100ScreeningSessionFactory.Create(
                lookup,
                tacticalCase.CaseId,
                tacticalCase.Members,
                "tactical-attribution");
            session.Profile.CampaignProgress.SelectedChapterId = tacticalCase.ChapterId;
            session.Profile.CampaignProgress.SelectedSiteId = tacticalCase.SiteId;
            session.BeginNewExpedition();
            if (!session.TryBuildSelectedBattleState(out _, out var encounter, out var allySnapshot, out var buildError))
            {
                return Failure(runId, tacticalCase, $"build:{buildError}");
            }

            var scenarioId = H100SessionDriver.ScenarioId(encounter.Context);
            var seededEncounter = encounter with
            {
                Context = encounter.Context with { BattleSeed = tacticalCase.BattleSeed },
            };
            if (!session.TryComposeBattleState(allySnapshot, seededEncounter, out var state, out var composeError))
            {
                return Failure(runId, tacticalCase, $"compose:{composeError}", scenarioId);
            }

            var eligibility = new FormationEligibilityTracker();
            var trace = new PlacementAttributionTraceCollector();
            eligibility.Observe(state);
            var result = BattleResolver.Run(state, maxBattleSteps, step =>
            {
                eligibility.Observe(state);
                trace.Observe(step);
            });
            var battle = BattleMetricProjector.Project(
                runId,
                string.Empty,
                tacticalCase.CaseId,
                tacticalCase.PairingId,
                0,
                scenarioId,
                "bt1-e09-fixed-placement",
                state,
                result,
                maxBattleSteps,
                TargetBattleSeconds);
            var formation = FormationMetricProjector.Project(
                battle,
                eligibility,
                tacticalCase.PairingId,
                tacticalCase.CompositionId,
                tacticalCase.PlacementVariantId,
                tacticalCase.IsBaseline,
                false,
                false,
                string.Empty,
                false,
                false);
            return Project(tacticalCase, battle, formation, trace.Complete(result));
        }
        catch (Exception exception)
        {
            return Failure(runId, tacticalCase, FailureCode(exception));
        }
    }

    private static PlacementAttributionBattleRecord Project(
        H100TacticalAttributionCase tacticalCase,
        BattleMetricRecord battle,
        FormationBattleRecord formation,
        PlacementTraceSummary trace)
        => new()
        {
            RunId = battle.RunId,
            BattleId = battle.BattleId,
            PairingId = tacticalCase.PairingId,
            ComparisonKind = tacticalCase.ComparisonKind,
            CompositionId = tacticalCase.CompositionId,
            ConceptVariantId = tacticalCase.ConceptVariantId,
            EncounterFamilyId = tacticalCase.SiteId,
            ScenarioId = battle.ScenarioId,
            Seed = tacticalCase.Seed,
            BattleSeed = tacticalCase.BattleSeed,
            PlacementVariantId = tacticalCase.PlacementVariantId,
            IsBaseline = tacticalCase.IsBaseline,
            SemanticPreservationExpected = tacticalCase.SemanticPreservationExpected,
            FormationProfileId = tacticalCase.FormationProfileId,
            FormationFeatures = tacticalCase.FormationFeatures,
            AnchorIdsByMemberIndex = tacticalCase.AnchorIdsByMemberIndex,
            WinnerSide = battle.WinnerSide,
            NormalizedFinalPowerDifference = battle.NormalizedFinalPowerDifference,
            FixedStepSeconds = battle.FixedStepSeconds,
            Channels = formation.Channels.Select(value =>
                new PlacementAttributionBattleRecord.ChannelTrace(
                    value.ChannelId,
                    value.Eligible,
                    value.EventCount)).ToArray(),
            Trace = trace,
            FailureCode = battle.FailureCode,
        };

    private static PlacementAttributionBattleRecord Failure(
        string runId,
        H100TacticalAttributionCase tacticalCase,
        string failureCode,
        string scenarioId = "unavailable")
        => new()
        {
            RunId = runId,
            BattleId = tacticalCase.CaseId,
            PairingId = tacticalCase.PairingId,
            ComparisonKind = tacticalCase.ComparisonKind,
            CompositionId = tacticalCase.CompositionId,
            ConceptVariantId = tacticalCase.ConceptVariantId,
            EncounterFamilyId = tacticalCase.SiteId,
            ScenarioId = scenarioId,
            Seed = tacticalCase.Seed,
            BattleSeed = tacticalCase.BattleSeed,
            PlacementVariantId = tacticalCase.PlacementVariantId,
            IsBaseline = tacticalCase.IsBaseline,
            SemanticPreservationExpected = tacticalCase.SemanticPreservationExpected,
            FormationProfileId = tacticalCase.FormationProfileId,
            FormationFeatures = tacticalCase.FormationFeatures,
            AnchorIdsByMemberIndex = tacticalCase.AnchorIdsByMemberIndex,
            FailureCode = failureCode,
        };

    private static string FailureCode(Exception exception)
    {
        var message = exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (message.Length > 160)
        {
            message = message.Substring(0, 160);
        }

        return string.IsNullOrWhiteSpace(message)
            ? $"exception:{exception.GetType().Name}"
            : $"exception:{exception.GetType().Name}:{message}";
    }
}
