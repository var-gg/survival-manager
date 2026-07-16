using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Services;
using SM.HeadlessMetrics;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>Stage 4 case를 실제 session/sim 경로로 재실행하고 pure 진형 레코드로 투영한다.</summary>
internal static class H100FormationBattleRunner
{
    public static IReadOnlyList<FormationBattleRecord> Run(
        RuntimeCombatContentLookup lookup,
        string runId,
        IReadOnlyList<H100FormationBattleCase> cases,
        int maxBattleSteps,
        float targetBattleSeconds)
    {
        var records = new List<FormationBattleRecord>(cases.Count);
        foreach (var formationCase in cases.OrderBy(value => value.CaseId, StringComparer.Ordinal))
        {
            records.Add(RunCase(lookup, runId, formationCase, maxBattleSteps, targetBattleSeconds));
        }

        return records;
    }

    private static FormationBattleRecord RunCase(
        RuntimeCombatContentLookup lookup,
        string runId,
        H100FormationBattleCase formationCase,
        int maxBattleSteps,
        float targetBattleSeconds)
    {
        var tracker = new FormationEligibilityTracker();
        BattleMetricRecord battle;
        try
        {
            var session = H100ScreeningSessionFactory.Create(
                lookup,
                formationCase.CaseId,
                formationCase.Members,
                "formation");
            session.BeginNewExpedition();
            if (!session.TryBuildSelectedBattleState(out _, out var encounter, out var allySnapshot, out var buildError))
            {
                battle = Failure(runId, formationCase, "unavailable", $"build:{buildError}");
                return Project(battle, tracker, formationCase);
            }

            var scenarioId = H100SessionDriver.ScenarioId(encounter.Context);
            var seededEncounter = encounter with
            {
                Context = encounter.Context with { BattleSeed = formationCase.Seed }
            };
            if (!session.TryComposeBattleState(allySnapshot, seededEncounter, out var state, out var composeError))
            {
                battle = Failure(runId, formationCase, scenarioId, $"compose:{composeError}");
                return Project(battle, tracker, formationCase);
            }

            var openingEvents = H100FormationCoverageProbe.Run(
                state,
                formationCase.CoverageProbeChannelId,
                () => tracker.Observe(state));
            tracker.Observe(state);
            var result = BattleResolver.Run(state, maxBattleSteps, _ => tracker.Observe(state));
            if (openingEvents.Count > 0)
            {
                result = result with { Events = openingEvents.Concat(result.Events).ToArray() };
            }
            battle = BattleMetricProjector.Project(
                runId,
                string.Empty,
                formationCase.CaseId,
                formationCase.PairingId,
                0,
                scenarioId,
                formationCase.PolicyId,
                state,
                result,
                maxBattleSteps,
                targetBattleSeconds);
        }
        catch (Exception exception)
        {
            battle = Failure(
                runId,
                formationCase,
                "unavailable",
                FailureCode(exception));
        }

        return Project(battle, tracker, formationCase);
    }

    private static BattleMetricRecord Failure(
        string runId,
        H100FormationBattleCase formationCase,
        string scenarioId,
        string failureCode)
        => BattleMetricProjector.ProjectFailure(
            runId,
            string.Empty,
            formationCase.CaseId,
            formationCase.PairingId,
            0,
            scenarioId,
            formationCase.PolicyId,
            formationCase.Seed,
            failureCode);

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

    private static FormationBattleRecord Project(
        BattleMetricRecord battle,
        FormationEligibilityTracker tracker,
        H100FormationBattleCase formationCase)
        => FormationMetricProjector.Project(
            battle,
            tracker,
            formationCase.PairingId,
            formationCase.PlacementSetId,
            formationCase.PlacementVariantId,
            formationCase.IsDefaultPlacement,
            formationCase.IsPolicyChoice,
            formationCase.IsHealerComparison,
            formationCase.HealerComparisonId,
            formationCase.ContainsHealer,
            formationCase.CompetentSelectedHealer,
            formationCase.CoverageProbeChannelId);
}
