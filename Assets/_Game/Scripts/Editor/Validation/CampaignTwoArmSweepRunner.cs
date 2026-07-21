using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessPolicies;
using SM.Meta;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>
/// Phase B 480-cell two-arm campaign measurement. 실 session/content composition을 사용하되
/// gameplay/content 자산은 수정하지 않고 측정 입력의 build/roster/enemy placement만 투영한다.
/// </summary>
internal static partial class CampaignTwoArmSweepRunner
{
    internal static CampaignTwoArmNodeReport RunBossLearningProbe(
        string encounterId,
        int maximumCells = 96)
    {
        var config = CampaignBalanceSweepConfig.Default;
        config.Validate();
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignTwoArmSweepRunner));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"campaign boss learning probe content unavailable: {contentError}");
        }

        var itemIndex = CampaignBalanceSweepRunner.BuildItemMetaIndex(content);
        var order = CampaignContentOrderIndex.Build(content);
        var grid = config.BuildGrid();
        var sampleCount = Math.Clamp(maximumCells, 1, grid.Count);
        var sampledCells = Enumerable.Range(0, sampleCount)
            .Select(index => grid[(int)Math.Floor(index * grid.Count / (double)sampleCount)])
            .ToArray();
        var accumulator = new CampaignTwoArmSweepAccumulator(config);
        foreach (var arm in config.Arms)
        {
            foreach (var cell in sampledCells)
            {
                RunCell(lookup, itemIndex, order, config, arm, cell, accumulator, encounterId);
            }
        }

        var aggregate = accumulator.BuildNodeAggregates()
            .Single(node => string.Equals(node.EncounterId, encounterId, StringComparison.Ordinal));
        return CampaignTwoArmBandEvaluator.EvaluateNode(config, aggregate);
    }

    internal static CampaignPrepMechanismSummary RunPrepMechanismWitness(int maximumCells = 12)
    {
        var config = CampaignBalanceSweepConfig.Default;
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"campaign prep witness content unavailable: {contentError}");
        }

        var itemIndex = CampaignBalanceSweepRunner.BuildItemMetaIndex(content);
        var order = CampaignContentOrderIndex.Build(content);
        var accumulator = new CampaignTwoArmSweepAccumulator(config);
        foreach (var cell in config.BuildGrid().Take(Math.Max(1, maximumCells)))
        {
            foreach (var arm in config.Arms)
            {
                RunCell(lookup, itemIndex, order, config, arm, cell, accumulator);
            }

            var witness = accumulator.BuildPrepMechanismSummary();
            if (witness.FormationDivergenceCount > 0)
            {
                return witness;
            }
        }

        return accumulator.BuildPrepMechanismSummary();
    }

    public static CampaignTwoArmSweepReport Run(CampaignBalanceSweepConfig config)
    {
        config.Validate();
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignTwoArmSweepRunner));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"campaign two-arm sweep content unavailable: {contentError}");
        }

        var itemIndex = CampaignBalanceSweepRunner.BuildItemMetaIndex(content);
        var order = CampaignContentOrderIndex.Build(content);
        var grid = config.BuildGrid();
        var accumulator = new CampaignTwoArmSweepAccumulator(config);
        var started = DateTime.UtcNow;

        foreach (var arm in config.Arms)
        {
            Debug.Log($"[CampaignTwoArmSweep] arm={arm.ArmId} policy={arm.PolicyId} cells={grid.Count} start");
            for (var cellIndex = 0; cellIndex < grid.Count; cellIndex++)
            {
                RunCell(lookup, itemIndex, order, config, arm, grid[cellIndex], accumulator);
                if ((cellIndex + 1) % 20 == 0 || cellIndex + 1 == grid.Count)
                {
                    Debug.Log(
                        $"[CampaignTwoArmSweep] arm={arm.ArmId} cells={cellIndex + 1}/{grid.Count} "
                        + $"elapsed={(DateTime.UtcNow - started).TotalMinutes:0.0}m");
                }
            }
        }

        return CampaignTwoArmSweepReportWriter.Write(CampaignTwoArmBandEvaluator.BuildReport(config, accumulator));
    }

    private static void RunCell(
        ICombatContentLookup lookup,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex,
        CampaignContentOrderIndex order,
        CampaignBalanceSweepConfig config,
        CampaignBalanceArmSpec arm,
        CampaignBalanceGridCell cell,
        CampaignTwoArmSweepAccumulator accumulator,
        string stopAfterEncounterId = "",
        Func<BattleState, ResolvedEncounterContext, BattleResult>? battleRunner = null,
        Action<GameSessionState, BattleLoadoutSnapshot, BattleState, ResolvedEncounterContext>? setupObserver = null)
    {
        // arm id를 hero/profile identity에 넣지 않는다. 두 팔의 unit-id tie break까지 paired 상태로 유지한다.
        var cellTag = CellTag(cell);
        var session = H100SessionDriver.CreateSession(lookup, $"campaign-two-arm-{cellTag}");
        CampaignBalanceSweepRunner.AuthorSquad(
            session,
            lookup,
            cellTag,
            cell.RosterArchetypeIds.ToArray(),
            itemIndex,
            redistributeStarterGear: false);
        ApplyBuildPower(session, lookup, itemIndex, cell.BuildPower);

        var policy = HeadlessPolicyFactory.Create(arm.PolicyId);
        var siteEntryPolicy = new GreedyPolicy();
        var siteCount = 0;
        while (!session.Profile.CampaignProgress.StoryCleared && siteCount < config.SiteSafety)
        {
            H100SessionDriver.AdvanceToNextUnclearedSite(session);
            var chapterId = session.SelectedCampaignChapterId;
            var siteId = session.SelectedCampaignSiteId;
            var chapterOrder = order.ChapterOrder(chapterId);
            var siteOrder = order.SiteOrder(siteId);

            var setupBefore = FormationHash(session);
            var decisionSeed = H100SessionDriver.DeriveSeed(
                $"{chapterId}|{siteId}|{cell.CellId}|site-entry",
                siteCount);
            var observation = H100PolicyObservationBuilder.Build(
                session,
                lookup,
                decisionSeed,
                includeTownRoster: true);

            // Phase B는 두 arm의 site-entry를 동일하게 고정하고 elite/boss prep만 인과 변수로 둔다.
            H100SessionDriver.ApplyPolicyDeployment(
                session,
                lookup,
                siteEntryPolicy,
                decisionSeed,
                observation);

            var setupAfter = FormationHash(session);
            accumulator.RecordSiteEntryDecision(
                arm,
                observation.EnemyPreview.IsAvailable,
                !string.Equals(setupBefore, setupAfter, StringComparison.Ordinal));

            session.BeginNewExpedition();
            var siteFirstVisitClear = true;
            while (true)
            {
                while (CampaignDefaultRouteNavigator.TryAdvanceIntermediateNonBattle(session))
                {
                }

                if (session.GetSelectedExpeditionNode()?.RequiresBattle != true)
                {
                    break;
                }

                var node = session.GetSelectedExpeditionNode()!;
                if (!session.TryBuildSelectedBattleState(
                        out _,
                        out var encounter,
                        out var allySnapshot,
                        out var buildError))
                {
                    throw new InvalidOperationException(
                        $"two-arm battle state build failed({cell.CellId}/{arm.ArmId}/{node.Id}): {buildError}");
                }

                var measuredEncounter = ProjectEncounter(encounter, cell.EnemyComposition);
                var prepOpportunity = encounter.Context.IsBoss || IsElite(encounter);
                var prepChanged = false;
                var prepEquipmentAssignmentCount = 0;
                if (prepOpportunity && arm.UsesEncounterPrep && policy is IHeadlessPrepPolicy prepPolicy)
                {
                    var prepBefore = FormationHash(session);
                    var prepSeed = H100SessionDriver.DeriveSeed(
                        $"{chapterId}|{siteId}|{node.Id}|{cell.CellId}|prep",
                        siteCount);
                    var prepObservation = H100PolicyObservationBuilder.Build(
                            session,
                            lookup,
                            prepSeed,
                            includeTownRoster: true)
                        .WithEnemyPreview(ProjectPreview(
                            H100PolicyObservationBuilder.Build(
                                session,
                                lookup,
                                prepSeed,
                                includeTownRoster: true).EnemyPreview,
                            measuredEncounter.Enemies));
                    var prepDecision = H100SessionDriver.ApplyPolicyPrep(
                        session,
                        policy,
                        prepPolicy,
                        prepSeed,
                        prepObservation);
                    prepEquipmentAssignmentCount = prepDecision.EquipmentAssignments.Count;
                    prepChanged = !string.Equals(prepBefore, FormationHash(session), StringComparison.Ordinal)
                                  || prepEquipmentAssignmentCount > 0;

                    if (!session.TryBuildSelectedBattleState(
                            out _,
                            out encounter,
                            out allySnapshot,
                            out buildError))
                    {
                        throw new InvalidOperationException(
                            $"two-arm post-prep battle state build failed({cell.CellId}/{arm.ArmId}/{node.Id}): {buildError}");
                    }

                    measuredEncounter = ProjectEncounter(encounter, cell.EnemyComposition);
                }

                accumulator.RecordPrepDecision(
                    arm,
                    prepOpportunity,
                    prepChanged,
                    prepEquipmentAssignmentCount);
                if (!session.TryComposeBattleState(allySnapshot, measuredEncounter, out var state, out var composeError))
                {
                    throw new InvalidOperationException(
                        $"two-arm battle compose failed({cell.CellId}/{arm.ArmId}/{node.Id}): {composeError}");
                }

                setupObserver?.Invoke(session, allySnapshot, state, measuredEncounter);
                var identity = new CampaignNodeIdentity(
                    chapterId,
                    chapterOrder,
                    siteId,
                    siteOrder,
                    node.Id,
                    encounter.Context.SiteNodeIndex + 1,
                    encounter.Context.EncounterId,
                    IsElite(encounter),
                    encounter.Context.IsBoss);
                var threatObserver = identity.IsBoss && battleRunner == null
                    ? new CampaignThreatLandingBattleObserver(state)
                    : null;
                var measured = battleRunner != null
                    ? battleRunner(state, measuredEncounter)
                    : threatObserver == null
                        ? BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps)
                        : BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps, threatObserver.ObserveStep);
                var won = measured.Winner == TeamSide.Ally;
                siteFirstVisitClear &= won;
                if (threatObserver != null)
                {
                    accumulator.RecordThreatLanding(arm, cell, identity, threatObserver.BuildObservation());
                }

                accumulator.RecordNode(
                    arm,
                    cell.CellId,
                    cell.Squad.SquadId,
                    identity,
                    won,
                    CampaignBossAnswerTagEvaluator.HasAnswer(
                        allySnapshot,
                        config.FindBossLearningSpec(identity.EncounterId)),
                    FormationHash(session),
                    prepEquipmentAssignmentCount > 0);
                if (string.Equals(identity.EncounterId, stopAfterEncounterId, StringComparison.Ordinal))
                {
                    return;
                }

                // 측정 outcome은 위 1회 deterministic cell 결과다. 캠페인 후속 노드 도달 상태만 기존
                // retry-until-win 하네스와 같이 동일 build/formation의 첫 winning seed로 정산한다.
                var progression = won
                    ? measured
                    : FindProgressionResult(session, allySnapshot, measuredEncounter, config.ProgressionRetrySeedCount) ?? measured;
                session.MarkBattleResolved(
                    progression.Winner == TeamSide.Ally,
                    progression.StepCount,
                    progression.Events.Count,
                    progression.FinalUnits);
                session.ResolveSelectedExpeditionNode();
            }

            accumulator.RecordSite(
                arm,
                cell.Squad.SquadId,
                new CampaignSiteIdentity(chapterId, chapterOrder, siteId, siteOrder),
                siteFirstVisitClear);

            session.ResolveSelectedNodeToRewardSettlement();
            if (session.PendingRewardChoices.Count > 0)
            {
                // 두 arm의 사후 성장 차이를 막기 위해 동일한 고정 reward policy를 공유한다.
                session.ApplyRewardChoice(0);
            }

            session.ReturnToTownAfterReward();
            ApplyBuildPower(session, lookup, itemIndex, cell.BuildPower);
            siteCount++;
        }

        if (!session.Profile.CampaignProgress.StoryCleared)
        {
            throw new InvalidOperationException(
                $"two-arm campaign did not clear within SiteSafety={config.SiteSafety}: arm={arm.ArmId} cell={cell.CellId}");
        }
    }

    private static ResolvedEncounterContext ProjectEncounter(
        ResolvedEncounterContext authored,
        CampaignEnemyCompositionVariantSpec variant)
    {
        var seed = H100SessionDriver.DeriveSeed(
            authored.Context.BattleContextHash,
            1000 + variant.VariantIndex);
        return authored with
        {
            Context = authored.Context with { BattleSeed = seed },
            Enemies = CampaignBalanceGridProjector.ProjectEnemyComposition(
                authored.Enemies,
                variant.VariantIndex),
        };
    }

    private static HeadlessEnemyPreview ProjectPreview(
        HeadlessEnemyPreview authored,
        IReadOnlyList<BattleUnitLoadout> enemies)
        => new(
            authored.IsAvailable,
            authored.EncounterId,
            authored.FactionId,
            authored.DifficultyBand,
            authored.ThreatSkulls,
            (enemies ?? Array.Empty<BattleUnitLoadout>())
                .Select(unit => new HeadlessEnemyUnitPreview(
                    unit.ArchetypeId,
                    unit.RaceId,
                    unit.ClassId,
                    unit.RoleTag,
                    unit.PreferredAnchor,
                    authored.Units
                        .FirstOrDefault(value => string.Equals(
                            value.ArchetypeId,
                            unit.ArchetypeId,
                            StringComparison.Ordinal))
                        ?.EquippedItems))
                .ToArray(),
            authored.BossAuraTag,
            authored.BossUtilityTag,
            authored.RewardDropTags);

    private static BattleResult FindProgressionResult(
        GameSessionState session,
        BattleLoadoutSnapshot allySnapshot,
        ResolvedEncounterContext measuredEncounter,
        int retrySeedCount)
    {
        for (var attempt = 0; attempt < retrySeedCount; attempt++)
        {
            var seed = H100SessionDriver.DeriveSeed(
                measuredEncounter.Context.BattleContextHash,
                2000 + attempt);
            var retry = measuredEncounter with
            {
                Context = measuredEncounter.Context with { BattleSeed = seed },
            };
            if (!session.TryComposeBattleState(allySnapshot, retry, out var state, out var error))
            {
                throw new InvalidOperationException($"two-arm progression compose failed: {error}");
            }

            var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
            if (result.Winner == TeamSide.Ally)
            {
                return result;
            }
        }

        return null;
    }

    private static void ApplyBuildPower(
        GameSessionState session,
        ISessionContentLookup lookup,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex,
        CampaignBuildPowerQuantileSpec quantile)
    {
        foreach (var hero in session.Profile.Heroes)
        {
            foreach (var instanceId in hero.EquippedItemIds.ToList())
            {
                var result = session.UnequipItem(hero.HeroId, instanceId);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"two-arm build projection could not unequip {instanceId} from {hero.HeroId}: {result.Error}");
                }
            }
        }

        if (quantile.EquipmentSlotsPerHero > 0)
        {
            var inventory = session.Profile.Inventory
                .Select((item, index) => (Item: item, Index: index))
                .Where(entry => string.IsNullOrWhiteSpace(entry.Item.EquippedHeroId))
                .OrderBy(entry => entry.Item.ItemBaseId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Item)
                .ToArray();
            foreach (var item in inventory)
            {
                if (!itemIndex.TryGetValue(item.ItemBaseId, out var meta))
                {
                    continue;
                }

                foreach (var hero in session.Profile.Heroes)
                {
                    if (hero.EquippedItemIds.Count >= quantile.EquipmentSlotsPerHero
                        || !CampaignBalanceSweepRunner.CanWear(session, itemIndex, hero, meta))
                    {
                        continue;
                    }

                    if (session.EquipItem(hero.HeroId, item.ItemInstanceId).IsSuccess)
                    {
                        break;
                    }
                }
            }
        }

        if (quantile.GrowAvailablePassives)
        {
            CampaignBalanceSweepRunner.GreedyGrowPassives(session, lookup);
        }
    }

    private static string FormationHash(GameSessionState session)
    {
        var assignments = session.EnumerateDeploymentAssignments()
            .OrderBy(value => value.Anchor)
            .Select(value => $"{value.Anchor}:{value.HeroId ?? "-"}");
        var expedition = session.ExpeditionSquadHeroIds.OrderBy(id => id, StringComparer.Ordinal);
        return $"{string.Join("|", assignments)}||{string.Join(",", expedition)}";
    }

    private static bool IsElite(ResolvedEncounterContext encounter)
        => encounter.Context.EncounterId.Contains("_elite_", StringComparison.Ordinal)
           || encounter.Context.RewardSourceId.Contains("elite", StringComparison.OrdinalIgnoreCase);

    private static string CellTag(CampaignBalanceGridCell cell)
        => $"{cell.Squad.SquadId}-{cell.BuildPower.QuantileId.ToLowerInvariant()}-"
           + $"e{cell.EnemyComposition.VariantIndex}-c{cell.RosterCoverage.BenchArchetypeCount}";

    private sealed class CampaignContentOrderIndex
    {
        private readonly IReadOnlyDictionary<string, int> _chapterOrders;
        private readonly IReadOnlyDictionary<string, int> _siteOrders;

        private CampaignContentOrderIndex(
            IReadOnlyDictionary<string, int> chapterOrders,
            IReadOnlyDictionary<string, int> siteOrders)
        {
            _chapterOrders = chapterOrders;
            _siteOrders = siteOrders;
        }

        public int ChapterOrder(string chapterId)
            => _chapterOrders.TryGetValue(chapterId, out var order)
                ? order
                : throw new InvalidOperationException($"Campaign chapter order missing: {chapterId}");

        public int SiteOrder(string siteId)
            => _siteOrders.TryGetValue(siteId, out var order)
                ? order
                : throw new InvalidOperationException($"Campaign site order missing: {siteId}");

        public static CampaignContentOrderIndex Build(CombatContentSnapshot content)
        {
            var chapterOrders = content.CampaignChapters.Values
                .OrderBy(chapter => chapter.StoryOrder)
                .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
                .Select((chapter, index) => (chapter.Id, Order: index + 1))
                .ToDictionary(value => value.Id, value => value.Order, StringComparer.Ordinal);
            var siteOrders = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var chapter in content.CampaignChapters.Values)
            {
                var orderedSites = chapter.SiteIds
                    .Where(content.ExpeditionSites.ContainsKey)
                    .OrderBy(id => content.ExpeditionSites[id].SiteOrder)
                    .ThenBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                for (var index = 0; index < orderedSites.Length; index++)
                {
                    siteOrders[orderedSites[index]] = index + 1;
                }
            }

            return new CampaignContentOrderIndex(chapterOrders, siteOrders);
        }
    }
}
