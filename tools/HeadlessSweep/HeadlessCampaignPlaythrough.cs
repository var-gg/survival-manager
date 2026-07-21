using System.Globalization;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Editor.Validation;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Meta.Services;

internal static class HeadlessCampaignPlaythrough
{
    internal static HeadlessCampaignArmExecution Run(
        SnapshotSessionContentLookup lookup,
        CampaignBalanceSweepConfig config,
        CampaignBalanceArmSpec arm,
        CampaignBalanceGridCell cell,
        string stopAfterEncounterId)
    {
        var state = HeadlessCampaignState.Create(lookup, cell);
        state.ApplyBuildPower(cell.BuildPower);

        var policy = HeadlessPolicyFactory.Create(arm.PolicyId);
        var siteEntryPolicy = new GreedyPolicy();
        var nodes = new List<HeadlessCampaignNodeObservation>();
        var sites = new List<HeadlessCampaignSiteObservation>();
        var siteEntryDecisions = new List<HeadlessCampaignSiteEntryDecisionObservation>();
        var prepDecisions = new List<HeadlessCampaignPrepDecisionObservation>();
        var siteCount = 0;

        while (!state.StoryCleared && siteCount < config.SiteSafety)
        {
            state.AdvanceToNextUnclearedSite();
            var chapterId = state.SelectedChapterId;
            var siteId = state.SelectedSiteId;
            var chapterOrder = ChapterOrder(state.Snapshot, chapterId);
            var siteOrder = SiteOrder(state.Snapshot, chapterId, siteId);

            var setupBefore = state.FormationHash();
            var siteEntrySeed = DeriveSeed($"{chapterId}|{siteId}|{cell.CellId}|site-entry", siteCount);
            var siteEntryObservation = HeadlessCampaignPolicyObservationBuilder.Build(
                state,
                siteEntrySeed,
                includeTownRoster: true);
            var siteEntryDecision = siteEntryPolicy.DecideDeployment(siteEntryObservation);
            state.ApplyDeploymentDecision(siteEntryObservation, siteEntryDecision);
            siteEntryDecisions.Add(new HeadlessCampaignSiteEntryDecisionObservation(
                siteEntryObservation.EnemyPreview.IsAvailable,
                !string.Equals(setupBefore, state.FormationHash(), StringComparison.Ordinal)));

            state.BeginSite();
            var siteFirstVisitClear = true;
            while (state.SelectedNode is { RequiresBattle: true } selectedNode)
            {
                var setup = state.BuildBattleSetup();
                var authoredEncounter = setup.AuthoredEncounter;
                var measuredEncounter = ProjectEncounter(authoredEncounter, cell.EnemyComposition);
                var prepOpportunity = authoredEncounter.Context.IsBoss || IsElite(authoredEncounter);
                var prepChanged = false;
                var prepEquipmentAssignmentCount = 0;

                if (prepOpportunity && arm.UsesEncounterPrep && policy is IHeadlessPrepPolicy prepPolicy)
                {
                    var prepBefore = state.FormationHash();
                    var prepSeed = DeriveSeed(
                        $"{chapterId}|{siteId}|{selectedNode.EncounterId}|{cell.CellId}|prep",
                        siteCount);
                    var prepObservation = HeadlessCampaignPolicyObservationBuilder.Build(
                        state,
                        prepSeed,
                        includeTownRoster: true,
                        measuredEncounter);
                    var prepDecision = prepPolicy.DecidePrep(prepObservation);
                    state.ApplyPrepDecision(prepObservation, prepDecision);
                    prepEquipmentAssignmentCount = prepDecision.EquipmentAssignments.Count;
                    prepChanged = !string.Equals(prepBefore, state.FormationHash(), StringComparison.Ordinal)
                                  || prepEquipmentAssignmentCount > 0;

                    setup = state.BuildBattleSetup();
                    authoredEncounter = setup.AuthoredEncounter;
                    measuredEncounter = ProjectEncounter(authoredEncounter, cell.EnemyComposition);
                }

                prepDecisions.Add(new HeadlessCampaignPrepDecisionObservation(
                    prepOpportunity,
                    prepChanged,
                    prepEquipmentAssignmentCount));
                var measured = RunBattle(state, setup.AllySnapshot, measuredEncounter, "measured");
                var won = measured.Result.Winner == TeamSide.Ally;
                siteFirstVisitClear &= won;
                var identity = new CampaignNodeIdentity(
                    chapterId,
                    chapterOrder,
                    siteId,
                    siteOrder,
                    selectedNode.EncounterId,
                    authoredEncounter.Context.SiteNodeIndex + 1,
                    authoredEncounter.Context.EncounterId,
                    IsElite(authoredEncounter),
                    authoredEncounter.Context.IsBoss);
                var stopAtCurrentNode = string.Equals(identity.EncounterId, stopAfterEncounterId, StringComparison.Ordinal);
                var progression = won
                    ? measured.Result
                    : stopAtCurrentNode
                        ? null
                        : FindProgressionResult(
                        state,
                        setup.AllySnapshot,
                        measuredEncounter,
                        config.ProgressionRetrySeedCount) ?? measured.Result;
                var woundsApplied = progression == null ? 0 : state.ApplyBattleProgression(progression);
                nodes.Add(new HeadlessCampaignNodeObservation(
                    identity,
                    won,
                    CampaignBossAnswerTagEvaluator.HasAnswer(
                        setup.AllySnapshot,
                        config.FindBossLearningSpec(identity.EncounterId)),
                    state.FormationHash(),
                    prepEquipmentAssignmentCount > 0,
                    measured.FlankSurvival,
                    measured.AntiClusterAoeSurvival,
                    measured.ThreatLanding,
                    won ? woundsApplied : 0));

                if (stopAtCurrentNode)
                {
                    return new HeadlessCampaignArmExecution(
                        arm,
                        cell.CellId,
                        cell.Squad.SquadId,
                        nodes,
                        sites,
                        siteEntryDecisions,
                        prepDecisions,
                        StoppedAtTarget: true);
                }

                state.AdvanceBattleNode();
            }

            sites.Add(new HeadlessCampaignSiteObservation(
                new CampaignSiteIdentity(chapterId, chapterOrder, siteId, siteOrder),
                siteFirstVisitClear));
            state.CompleteSite();
            state.ApplyBuildPower(cell.BuildPower);
            siteCount++;
        }

        if (!state.StoryCleared)
        {
            throw new InvalidOperationException(
                $"Headless campaign did not clear within SiteSafety={config.SiteSafety}: arm={arm.ArmId} cell={cell.CellId}");
        }

        if (!string.IsNullOrWhiteSpace(stopAfterEncounterId))
        {
            throw new InvalidOperationException(
                $"Headless campaign never reached encounter '{stopAfterEncounterId}': arm={arm.ArmId} cell={cell.CellId}");
        }

        return new HeadlessCampaignArmExecution(
            arm,
            cell.CellId,
            cell.Squad.SquadId,
            nodes,
            sites,
            siteEntryDecisions,
            prepDecisions,
            StoppedAtTarget: false);
    }

    private static HeadlessCampaignBattleOutcome RunBattle(
        HeadlessCampaignState state,
        BattleLoadoutSnapshot allySnapshot,
        ResolvedEncounterContext encounter,
        string phase)
    {
        if (!SessionBattleStateComposer.TryCompose(
                state.Lookup,
                allySnapshot,
                encounter,
                out var battleState,
                out var error))
        {
            throw new InvalidOperationException(
                $"Headless campaign {phase} compose failed ({state.Cell.CellId}/{encounter.Context.EncounterId}): {error}");
        }

        var survivalObserver = new PackPursuitSurvivalObserver();
        var antiClusterAoeObserver = new AntiClusterAoeSurvivalObserver();
        var threatLandingObserver = encounter.Context.IsBoss
            ? new CampaignThreatLandingBattleObserver(battleState)
            : null;
        var result = BattleResolver.Run(
            battleState,
            BattleSimulator.DefaultMaxSteps,
            step =>
            {
                survivalObserver.Observe(battleState, step);
                antiClusterAoeObserver.Observe(battleState, step);
                threatLandingObserver?.ObserveStep(step);
            });
        return new HeadlessCampaignBattleOutcome(
            result,
            survivalObserver.Complete(battleState),
            antiClusterAoeObserver.Complete(battleState),
            threatLandingObserver?.BuildObservation());
    }

    private static BattleResult? FindProgressionResult(
        HeadlessCampaignState state,
        BattleLoadoutSnapshot allySnapshot,
        ResolvedEncounterContext measuredEncounter,
        int retrySeedCount)
    {
        for (var attempt = 0; attempt < retrySeedCount; attempt++)
        {
            var retry = measuredEncounter with
            {
                Context = measuredEncounter.Context with
                {
                    BattleSeed = DeriveSeed(measuredEncounter.Context.BattleContextHash, 2000 + attempt),
                },
            };
            var result = RunBattle(state, allySnapshot, retry, $"progression-{attempt}").Result;
            if (result.Winner == TeamSide.Ally)
            {
                return result;
            }
        }

        return null;
    }

    private static ResolvedEncounterContext ProjectEncounter(
        ResolvedEncounterContext authored,
        CampaignEnemyCompositionVariantSpec variant)
        => authored with
        {
            Context = authored.Context with
            {
                BattleSeed = DeriveSeed(authored.Context.BattleContextHash, 1000 + variant.VariantIndex),
            },
            Enemies = CampaignBalanceGridProjector.ProjectEnemyComposition(
                authored.Enemies,
                variant.VariantIndex),
        };

    private static bool IsElite(ResolvedEncounterContext encounter)
        => encounter.Context.EncounterId.Contains("_elite_", StringComparison.Ordinal)
           || encounter.Context.RewardSourceId.Contains("elite", StringComparison.OrdinalIgnoreCase);

    private static int ChapterOrder(CombatContentSnapshot snapshot, string chapterId)
        => (snapshot.CampaignChapters
            ?? throw new InvalidDataException("Campaign chapters are missing from the content snapshot."))
            .Values
            .OrderBy(chapter => chapter.StoryOrder)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            .Select((chapter, index) => (chapter.Id, Order: index + 1))
            .Single(value => string.Equals(value.Id, chapterId, StringComparison.Ordinal))
            .Order;

    private static int SiteOrder(CombatContentSnapshot snapshot, string chapterId, string siteId)
    {
        var chapters = snapshot.CampaignChapters
                       ?? throw new InvalidDataException("Campaign chapters are missing from the content snapshot.");
        var sites = snapshot.ExpeditionSites
                    ?? throw new InvalidDataException("Expedition sites are missing from the content snapshot.");
        var chapter = chapters[chapterId];
        return chapter.SiteIds
            .Where(sites.ContainsKey)
            .OrderBy(id => sites[id].SiteOrder)
            .ThenBy(id => id, StringComparer.Ordinal)
            .Select((id, index) => (Id: id, Order: index + 1))
            .Single(value => string.Equals(value.Id, siteId, StringComparison.Ordinal))
            .Order;
    }

    internal static int DeriveSeed(string contextHash, int salt)
    {
        unchecked
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var hash = offset;
            var payload = Encoding.UTF8.GetBytes(
                $"h100|{contextHash}|{salt.ToString(CultureInfo.InvariantCulture)}");
            foreach (var value in payload)
            {
                hash ^= value;
                hash *= prime;
            }

            var result = (int)(hash & 0x7fffffffu);
            return result == 0 ? 1 : result;
        }
    }
}
