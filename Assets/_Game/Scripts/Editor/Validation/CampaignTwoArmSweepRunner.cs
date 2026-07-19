using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>
/// Phase A 480-cell two-arm campaign measurement. 실 session/content composition을 사용하되
/// gameplay/content 자산은 수정하지 않고 측정 입력의 build/roster/enemy placement만 투영한다.
/// </summary>
internal static class CampaignTwoArmSweepRunner
{
    public static CampaignTwoArmSweepReport Run(CampaignBalanceSweepConfig config)
    {
        config.Validate();
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignTwoArmSweepRunner));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"campaign two-arm sweep content unavailable: {contentError}");
        }

        var itemIndex = CampaignBalanceSweepRunner.LoadItemMetaIndex();
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
        RuntimeCombatContentLookup lookup,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex,
        CampaignContentOrderIndex order,
        CampaignBalanceSweepConfig config,
        CampaignBalanceArmSpec arm,
        CampaignBalanceGridCell cell,
        CampaignTwoArmSweepAccumulator accumulator)
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

            // Naive는 첫 출격에서 greedy/fixed binding을 적용한 뒤 이전 formation hash를 유지한다.
            // Informed만 매 site 무료 preview를 읽어 site-entry setup을 다시 고른다.
            if (arm.UsesForcedPreview || siteCount == 0)
            {
                H100SessionDriver.ApplyPolicyDeployment(
                    session,
                    lookup,
                    policy,
                    decisionSeed,
                    observation);
            }

            var setupAfter = FormationHash(session);
            accumulator.RecordSiteEntryDecision(
                arm,
                observation.EnemyPreview.IsAvailable,
                !string.Equals(setupBefore, setupAfter, StringComparison.Ordinal));

            session.BeginNewExpedition();
            var siteFirstVisitClear = true;
            while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
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
                if (!session.TryComposeBattleState(allySnapshot, measuredEncounter, out var state, out var composeError))
                {
                    throw new InvalidOperationException(
                        $"two-arm battle compose failed({cell.CellId}/{arm.ArmId}/{node.Id}): {composeError}");
                }

                var measured = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
                var won = measured.Winner == TeamSide.Ally;
                siteFirstVisitClear &= won;
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
                accumulator.RecordNode(
                    arm,
                    cell.Squad.SquadId,
                    identity,
                    won,
                    HasBossAnswerTag(allySnapshot));

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
        RuntimeCombatContentLookup lookup,
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

    private static bool HasBossAnswerTag(BattleLoadoutSnapshot snapshot)
    {
        var allies = snapshot.Allies ?? Array.Empty<BattleUnitLoadout>();
        var guardAnchor = allies.Any(unit =>
            unit.PreferredAnchor.IsFrontRow()
            && (string.Equals(unit.ClassId, "vanguard", StringComparison.Ordinal)
                || ContainsToken(unit.RoleTag, "anchor")
                || (unit.CompileTags ?? Array.Empty<string>()).Any(tag => ContainsToken(tag, "guard"))));
        var baitedGap = allies.Count(unit => unit.PreferredAnchor.IsFrontRow()) >= 2
                        && allies.All(unit => unit.PreferredAnchor != DeploymentAnchorId.FrontCenter);
        var hasMark = allies.Any(unit =>
            (unit.CompileTags ?? Array.Empty<string>()).Any(tag => ContainsToken(tag, "mark"))
            || (unit.Skills ?? Array.Empty<BattleSkillSpec>()).Any(skill => ContainsToken(skill.Id, "mark")));
        var hasBurst = allies.Any(unit =>
            ContainsToken(unit.RoleTag, "carry")
            || string.Equals(unit.ClassId, "duelist", StringComparison.Ordinal)
            || string.Equals(unit.ClassId, "ranger", StringComparison.Ordinal));
        return guardAnchor || baitedGap || (hasMark && hasBurst);
    }

    private static bool ContainsToken(string value, string token)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(token, StringComparison.OrdinalIgnoreCase);

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
