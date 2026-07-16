using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Services;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>첫 authored campaign encounter를 paired seed/replay copy로 실행하는 전투 corpus runner.</summary>
internal static class H100BattleCorpusRunner
{
    public static IReadOnlyList<BattleMetricRecord> Run(
        RuntimeCombatContentLookup lookup,
        H100MetricsRunSettings settings,
        float targetBattleSeconds,
        Action<string>? decisionLog = null)
    {
        var records = new List<BattleMetricRecord>(settings.BattleCount * settings.ReplayCopies);
        GameSessionState session;
        try
        {
            var policy = HeadlessPolicyFactory.Create(settings.PolicyId);
            session = H100SessionDriver.CreateSession(lookup, settings.PairingProfileId("battle-corpus"));
            H100SessionDriver.ApplyPolicyDeployment(
                session,
                lookup,
                policy,
                H100SessionDriver.DeriveSeed("battle-corpus-deployment", settings.SeedBase),
                decisionLog);
            session.BeginNewExpedition();
        }
        catch (Exception exception)
        {
            AppendBuildFailures(records, settings, $"setup-exception:{exception.GetType().Name}");
            return records;
        }

        if (!session.TryBuildSelectedBattleState(out _, out var encounter, out var allySnapshot, out var buildError))
        {
            AppendBuildFailures(records, settings, buildError);
            return records;
        }

        var scenarioId = H100SessionDriver.ScenarioId(encounter.Context);
        for (var groupIndex = 0; groupIndex < settings.BattleCount; groupIndex++)
        {
            var replayGroupId = $"battle-corpus-{groupIndex:D6}";
            var seed = H100SessionDriver.DeriveSeed(encounter.Context.BattleContextHash, settings.SeedBase + groupIndex);
            var seededEncounter = encounter with { Context = encounter.Context with { BattleSeed = seed } };
            for (var copy = 0; copy < settings.ReplayCopies; copy++)
            {
                var battleId = $"{replayGroupId}-copy-{copy:D2}";
                try
                {
                    if (!session.TryComposeBattleState(allySnapshot, seededEncounter, out var state, out var composeError))
                    {
                        records.Add(BattleMetricProjector.ProjectFailure(
                            settings.RunId, string.Empty, battleId, replayGroupId, copy, scenarioId,
                            settings.PolicyId, seed, $"compose:{composeError}"));
                        continue;
                    }

                    var result = BattleResolver.Run(state, settings.MaxBattleSteps);
                    records.Add(BattleMetricProjector.Project(
                        settings.RunId, string.Empty, battleId, replayGroupId, copy, scenarioId,
                        settings.PolicyId, state, result, settings.MaxBattleSteps, targetBattleSeconds));
                }
                catch (Exception exception)
                {
                    records.Add(BattleMetricProjector.ProjectFailure(
                        settings.RunId, string.Empty, battleId, replayGroupId, copy, scenarioId,
                        settings.PolicyId, seed, $"exception:{exception.GetType().Name}"));
                }
            }
        }

        return records;
    }

    /// <summary>
    /// Stage 3 census가 선정한 build/medoid/seed 한정 corpus. 기존 real-content session composition과
    /// BattleMetricProjector를 그대로 사용하며 full screening 규모나 pruning 판단은 수행하지 않는다.
    /// </summary>
    public static IReadOnlyList<BattleMetricRecord> RunScreening(
        RuntimeCombatContentLookup lookup,
        string runId,
        string policyId,
        IReadOnlyList<H100BattleScreeningCase> cases,
        int maxBattleSteps,
        float targetBattleSeconds)
    {
        var records = new List<BattleMetricRecord>(cases.Count);
        foreach (var screeningCase in cases.OrderBy(value => value.CaseId, StringComparer.Ordinal))
        {
            try
            {
                var session = CreateScreeningSession(lookup, screeningCase);
                session.BeginNewExpedition();
                if (!session.TryBuildSelectedBattleState(out _, out var encounter, out var allySnapshot, out var buildError))
                {
                    records.Add(BattleMetricProjector.ProjectFailure(
                        runId,
                        string.Empty,
                        screeningCase.CaseId,
                        screeningCase.CaseId,
                        0,
                        "unavailable",
                        policyId,
                        screeningCase.Seed,
                        $"build:{buildError}"));
                    continue;
                }

                var scenarioId = H100SessionDriver.ScenarioId(encounter.Context);
                var seededEncounter = encounter with
                {
                    Context = encounter.Context with { BattleSeed = screeningCase.Seed }
                };
                if (!session.TryComposeBattleState(allySnapshot, seededEncounter, out var state, out var composeError))
                {
                    records.Add(BattleMetricProjector.ProjectFailure(
                        runId,
                        string.Empty,
                        screeningCase.CaseId,
                        screeningCase.CaseId,
                        0,
                        scenarioId,
                        policyId,
                        screeningCase.Seed,
                        $"compose:{composeError}"));
                    continue;
                }

                var result = BattleResolver.Run(state, maxBattleSteps);
                records.Add(BattleMetricProjector.Project(
                    runId,
                    string.Empty,
                    screeningCase.CaseId,
                    screeningCase.CaseId,
                    0,
                    scenarioId,
                    policyId,
                    state,
                    result,
                    maxBattleSteps,
                    targetBattleSeconds));
            }
            catch (Exception exception)
            {
                records.Add(BattleMetricProjector.ProjectFailure(
                    runId,
                    string.Empty,
                    screeningCase.CaseId,
                    screeningCase.CaseId,
                    0,
                    "unavailable",
                    policyId,
                    screeningCase.Seed,
                    $"exception:{exception.GetType().Name}"));
            }
        }

        return records;
    }

    private static GameSessionState CreateScreeningSession(
        RuntimeCombatContentLookup lookup,
        H100BattleScreeningCase screeningCase)
    {
        var profile = new SaveProfile
        {
            ProfileId = $"h100-census-{screeningCase.CaseId}",
            Heroes = new List<HeroInstanceRecord>(),
        };
        var heroIds = new List<string>(screeningCase.Members.Count);
        for (var index = 0; index < screeningCase.Members.Count; index++)
        {
            var member = screeningCase.Members[index];
            if (!lookup.TryGetArchetype(member.ArchetypeId, out var archetype))
            {
                throw new InvalidOperationException($"Screening archetype is unavailable: {member.ArchetypeId}");
            }

            var heroId = $"census-{index:D2}-{member.ArchetypeId}";
            heroIds.Add(heroId);
            profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = heroId,
                Name = member.ArchetypeId,
                ArchetypeId = member.ArchetypeId,
                RaceId = archetype.Race.Id,
                ClassId = archetype.Class.Id,
                FlexActiveId = archetype.Loadout?.FlexActive?.Id ?? string.Empty,
                FlexPassiveId = archetype.Loadout?.FlexPassive?.Id ?? string.Empty,
                RecruitTier = archetype.RecruitTier,
            });
        }

        var session = H100SessionDriver.CreateSession(lookup, profile);
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        for (var index = 0; index < screeningCase.Members.Count; index++)
        {
            if (!session.AssignHeroToAnchor(screeningCase.Members[index].Anchor, heroIds[index]))
            {
                throw new InvalidOperationException(
                    $"Could not apply census placement: {heroIds[index]}@{screeningCase.Members[index].Anchor}");
            }
        }

        return session;
    }

    private static void AppendBuildFailures(
        ICollection<BattleMetricRecord> records,
        H100MetricsRunSettings settings,
        string error)
    {
        for (var groupIndex = 0; groupIndex < settings.BattleCount; groupIndex++)
        {
            var replayGroupId = $"battle-corpus-{groupIndex:D6}";
            for (var copy = 0; copy < settings.ReplayCopies; copy++)
            {
                records.Add(BattleMetricProjector.ProjectFailure(
                    settings.RunId, string.Empty, $"{replayGroupId}-copy-{copy:D2}", replayGroupId, copy,
                    "unavailable", settings.PolicyId, 0, $"build:{error}"));
            }
        }
    }
}
