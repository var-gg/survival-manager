using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Content.Definitions;
using SM.Core;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Results;
using SM.Editor.SeedData;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>
/// 게이트① 수치 sweep 러너 — 오너 비준 스펙(analysis-owner-gates-headless100-recommendations)의 측정 집행부.
/// 32시드 × 표준 분대 3종(전열/원거리/혼성)으로 (a) 캠페인 사이트별 승률 곡선과 (b) 아이템/유령패시브
/// 장착 vs 미장착 승률 델타를 측정해 Logs/balance-sweep-campaign/에 JSON+MD 리포트를 쓴다.
///
/// <para>측정 원칙:</para>
/// <para>- 전투 합성은 세션 단일 소스(<see cref="GameSessionState.TryComposeBattleState"/>)만 탄다 —
/// 시드 분산은 ResolvedEncounterContext record의 BattleSeed만 with-교체(2nd battle-truth 생성 금지).
/// 시드 0 = canonical BattleSeed(게임 실선), 시드 1..N-1 = FNV("sweep|contextHash|k") 파생(프로세스 불변).</para>
/// <para>- 곡선 진행은 retry-until-win 모델: canonical 패배 시 승리한 variant의 실전투 결과로 정산해
/// "재시도 끝에 통과한 플레이어"의 도달 상태를 sim-정직하게 재현한다. 전 시드 패배(0/N)면 forced-march로
/// 커서만 전진하고 사이트를 벽(wall)으로 기록한다.</para>
/// <para>- 분대 성장은 실게임 API만 사용: 승리 XP(MarkBattleResolved), 보상 선택(ApplyRewardChoice 0번),
/// 인벤토리 탐욕 장착(EquipItem), 패시브 탐욕 성장(TogglePassiveNode, 예산 계단은 세션 validator 소유).</para>
/// <para>- 델타 벤치마크는 "이전 전투 노드 전승 가정 XP"로 저작한 혼성 분대 프로필 — baseline 대비
/// 아이템 1개/유령 노드 closure의 승률 차이를 같은 시드 쌍으로 잰다(paired).</para>
///
/// <para>비준 밴드: 곡선 = 1챕 ≥90% → 종챕 50~70%, 연속 사이트 낙차 30%p 초과 금지.
/// 델타 = dead &lt; +1%p · 목표 +2~+10%p · dominant &gt; +15%p. 상태 배율 7채널은 노브로만(측정 arm 없음).</para>
///
/// <para>실행 레인: 배치 -executeMethod(RunFullFromCli) 또는 메뉴. Test Runner 게이트에는
/// HeadlessBalanceSweepSmokeTests(BatchOnly, 축소 scope)만 태운다 — Loop D와 동일한 분리.</para>
/// </summary>
public static class CampaignBalanceSweepRunner
{
    private const string ReportFolderName = "Logs/balance-sweep-campaign";
    private const string CurveReportFileName = "sweep_curve_report.json";
    private const string DeltaReportFileName = "sweep_delta_report.json";
    private const string HumanReportFileName = "sweep_report.md";

    // ── 재비준 밴드(2회전 — 오너 위임 GPT Pro 결정 2026-07-13, 리포트 §질문 4건의 답) ──
    // Q1: 1챕 온보딩은 사이트 AND가 아니라 노드별 첫판 승률(3분대 전부 ≥0.90).
    private const float CurveChapter1MinNodeWinRate = 0.90f;
    // Q1: 종챕 50~70% 밴드는 고정 기준 분대(frontline)의 사이트 AND율로만 판정 — 동적 최약이 아니라 불변 기준점.
    private const string CurveReferenceSquad = "frontline";
    private const float CurveFinalChapterMinClearRate = 0.50f;
    private const float CurveFinalChapterMaxClearRate = 0.70f;
    // Q1: 카운터 분대(ranged/mixed)는 종챕 바닥만 두고 상한 없음 — 정찰→카운터 편성 보상을 깎지 않는다.
    private const float CurveFinalCounterMinClearRate = 0.85f;
    // Q2: 절벽의 공식 판정 = 어느 분대든 개별 노드 첫판 승률 < 0.30 (사이트 AND율·연속 낙차는 진단 출력 전용).
    private const float CurveCliffNodeMinWinRate = 0.30f;
    private const float CurveCliffMaxDrop = 0.30f;
    // 관찰 지표(리스크 감시): 종반(마지막 2사이트 8노드)에서 카운터 분대의 상시 포화 신호.
    private const float LateSaturationNodeRate = 0.95f;

    private const float DeltaDeadBelow = 0.01f;
    private const float DeltaTargetMin = 0.02f;
    private const float DeltaTargetMax = 0.10f;
    private const float DeltaGreyHighMax = 0.15f;
    private const float DeltaDominantAbove = 0.15f;
    // Q4: 착용자 민감도 positive control — 착용자 파워 증폭(+3레벨 등가) paired Δ가 이 미만이면
    // 그 벤치마크는 해당 착용자에 비민감 → dead 판정 유보(insensitive)로 기록.
    private const float SensitivityControlMinDelta = 0.05f;
    private const int SensitivityControlLevelBoost = 3;

    private const int BattleXpGainPerVictory = 50; // SessionRewardSettlementFlow와 동일 상수(벤치마크 XP 저작용).

    // 표준 분대 3종 — 배치 캡 4에서 시너지(2/4·2/3 문턱)가 실제로 발화하는 조합만 표준으로 삼는다.
    // 1/1/1/1 혼성은 시너지 0발화 허수아비(스모크 실측: 1챕 첫 사이트 클리어율 0.00)라 표준에서 제외.
    // frontline = 디폴트 배치(첫 4 canonical archetype)와 동일 — 기존 walkthrough 실측과 접점.
    private static readonly (string Name, string[] Archetypes)[] SquadPresets =
    {
        ("frontline", new[] { "warden", "guardian", "slayer", "raider" }),
        ("ranged", new[] { "warden", "marksman", "hunter", "scout" }),
        ("mixed", new[] { "warden", "guardian", "marksman", "hunter" }),
    };

    // 델타 측정 분대 2종 — 42아이템의 클래스 게이트(4클래스)를 시너지-생존 조합으로 커버한다.
    // vanguard/duelist 아이템 → front, ranger/mystic 아이템 → back, 클래스 무관 → front.
    private static readonly (string Name, string[] Archetypes)[] DeltaSquads =
    {
        ("front", new[] { "warden", "guardian", "slayer", "raider" }),
        ("back", new[] { "marksman", "hunter", "priest", "shaman" }),
    };

    /// <summary>sweep 실행 범위 — full(비준 스펙)과 smoke(하네스 witness)가 같은 코드를 공유한다.</summary>
    public sealed record SweepScope(
        int SeedCount,
        int SiteSafety,
        IReadOnlyList<(string Name, string[] Archetypes)> Presets,
        int BenchmarkCandidateDepth,
        int? ItemLimit,
        int? GhostLimit);

    public static SweepScope FullScope { get; } = new(
        SeedCount: 32,
        SiteSafety: 16,
        Presets: SquadPresets,
        BenchmarkCandidateDepth: 4,
        ItemLimit: null,
        GhostLimit: null);

    public static SweepScope SmokeScope { get; } = new(
        SeedCount: 4,
        SiteSafety: 1,
        Presets: new[] { SquadPresets[2] },
        BenchmarkCandidateDepth: 2,
        ItemLimit: 2,
        GhostLimit: 1);

    // 벤치마크 baseline이 이 구간을 벗어나면(바닥/천장 효과) 델타가 전부 0으로 눌려 dead 허구를 만든다 —
    // 분대별로 민감 구간에 드는 첫 후보 사이트를 채택하고, 전 후보 불감이면 최근접 후보에 low-sensitivity 표식.
    private const float BenchmarkSensitivityMin = 0.15f;
    private const float BenchmarkSensitivityMax = 0.85f;

    [MenuItem("SM/Internal/Validation/Run Campaign Balance Sweep (Full)")]
    public static void RunFullMenu()
    {
        var result = Run(FullScope);
        Debug.Log($"[CampaignSweep] full run complete → {result.ReportDirectory}");
    }

    [MenuItem("SM/Internal/Validation/Run Campaign Balance Sweep (Smoke)")]
    public static void RunSmokeMenu()
    {
        var result = Run(SmokeScope);
        Debug.Log($"[CampaignSweep] smoke run complete → {result.ReportDirectory}");
    }

    [MenuItem("SM/Internal/Validation/Run Campaign Balance Sweep (Two Arm Phase B)")]
    public static void RunTwoArmBaselineMenu()
    {
        var result = RunTwoArm(CampaignBalanceSweepConfig.Default);
        Debug.Log($"[CampaignSweep] two-arm baseline complete → {result.JsonReportPath}");
    }

    /// <summary>Phase B 480-cell naive/informed measurement lane. 기존 curve/delta lane과 artifact를 분리한다.</summary>
    public static CampaignTwoArmSweepReport RunTwoArm(CampaignBalanceSweepConfig config)
        => CampaignTwoArmSweepRunner.Run(config);

    /// <summary>배치 CLI 진입점: Phase B canonical 480-cell two-arm baseline.</summary>
    public static void RunTwoArmBaselineFromCli()
    {
        try
        {
            var result = RunTwoArm(CampaignBalanceSweepConfig.Default);
            Debug.Log($"[CampaignSweep] CLI two-arm baseline complete → {result.JsonReportPath}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CampaignSweep] CLI two-arm baseline failed: {exception}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>배치 CLI 진입점: Unity -batchmode -executeMethod SM.Editor.Validation.CampaignBalanceSweepRunner.RunFullFromCli -quit</summary>
    public static void RunFullFromCli()
    {
        try
        {
            var result = Run(FullScope);
            Debug.Log($"[CampaignSweep] CLI full run complete → {result.ReportDirectory}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CampaignSweep] CLI run failed: {exception}");
            EditorApplication.Exit(1);
        }
    }

    public static SweepRunResult Run(SweepScope scope)
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignBalanceSweepRunner));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var itemIndex = LoadItemMetaIndex();

        var curve = new List<SquadCurveReport>();
        foreach (var preset in scope.Presets)
        {
            Debug.Log($"[CampaignSweep] curve preset={preset.Name} start");
            curve.Add(RunCurveForPreset(lookup, itemIndex, preset.Name, preset.Archetypes, scope));
            Debug.Log($"[CampaignSweep] curve preset={preset.Name} complete");
        }

        var curveFindings = EvaluateCurveBands(content, curve);

        var deltas = new List<SquadDeltaReport>();
        foreach (var squad in DeltaSquads)
        {
            Debug.Log($"[CampaignSweep] delta squad={squad.Name} start");
            deltas.Add(RunDeltaLaneForSquad(lookup, itemIndex, content, squad, scope));
            Debug.Log($"[CampaignSweep] delta squad={squad.Name} complete");
        }

        var outliers = EvaluateDeltaBands(deltas);
        var reportDirectory = WriteReports(scope, curve, curveFindings, deltas, outliers);
        return new SweepRunResult(curve, curveFindings, deltas, outliers, reportDirectory);
    }

    // ─────────────────────────────────────────────
    // 곡선 레인 — 표준 분대로 캠페인을 완주하며 노드마다 N시드 분포를 잰다.
    // ─────────────────────────────────────────────

    private static SquadCurveReport RunCurveForPreset(
        RuntimeCombatContentLookup lookup,
        IReadOnlyDictionary<string, ItemMeta> itemIndex,
        string presetName,
        string[] archetypes,
        SweepScope scope)
    {
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile { ProfileId = $"sweep_curve_{presetName}" });
        session.SetCurrentScene(SceneNames.Town);
        AuthorSquad(session, lookup, presetName, archetypes, itemIndex);

        var sites = new List<SiteSweepMeasurement>();
        while (!session.Profile.CampaignProgress.StoryCleared && sites.Count < scope.SiteSafety)
        {
            AdvanceToNextUnclearedSite(session);
            var chapterId = session.SelectedCampaignChapterId;
            var siteId = session.SelectedCampaignSiteId;
            var arrivalLevels = ResolveSquadLevels(session);

            session.BeginNewExpedition();
            var nodes = new List<NodeSweepMeasurement>();
            var perSeedSiteClear = Enumerable.Repeat(true, scope.SeedCount).ToArray();
            var forcedMarch = false;

            while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
                var node = session.GetSelectedExpeditionNode()!;
                var measurement = MeasureSelectedBattleNode(session, node, scope.SeedCount, out var progression, out var progressionKind);
                nodes.Add(measurement);
                for (var k = 0; k < scope.SeedCount; k++)
                {
                    perSeedSiteClear[k] &= measurement.PerSeedWin[k];
                }

                forcedMarch |= progressionKind == "forced-march";
                session.MarkBattleResolved(
                    progression.Winner == TeamSide.Ally,
                    progression.StepCount,
                    progression.Events.Count,
                    progression.FinalUnits);
                session.ResolveSelectedExpeditionNode();
            }

            session.ResolveSelectedNodeToRewardSettlement();
            if (session.PendingRewardChoices.Count > 0)
            {
                session.ApplyRewardChoice(0);
            }

            session.ReturnToTownAfterReward();
            GreedyEquipInventory(session, itemIndex);
            GreedyGrowPassives(session, lookup);

            var clearRate = perSeedSiteClear.Count(win => win) / (float)scope.SeedCount;
            sites.Add(new SiteSweepMeasurement(
                chapterId,
                siteId,
                arrivalLevels.Min,
                arrivalLevels.Max,
                clearRate,
                forcedMarch,
                nodes));
            Debug.Log($"[CampaignSweep] {presetName} {chapterId}/{siteId} clearRate={clearRate:0.00} forced={forcedMarch}");
        }

        return new SquadCurveReport(
            presetName,
            string.Join(",", archetypes),
            session.Profile.CampaignProgress.StoryCleared,
            sites);
    }

    /// <summary>
    /// 현재 선택 전투 노드를 seed 0(canonical)+파생 시드로 N회 sim해 분포를 재고, retry-until-win
    /// 진행 결과(canonical 승 → canonical, 패 → 첫 승리 variant, 전패 → canonical 패배)를 돌려준다.
    /// </summary>
    private static NodeSweepMeasurement MeasureSelectedBattleNode(
        GameSessionState session,
        ExpeditionNodeViewModel node,
        int seedCount,
        out BattleResult progression,
        out string progressionKind)
    {
        if (!session.TryBuildSelectedBattleState(out var canonicalState, out var encounter, out var allySnapshot, out var error))
        {
            throw new InvalidOperationException($"battle state build failed({node.Id}): {error}");
        }

        var perSeedWin = new bool[seedCount];
        var steps = new List<float>(seedCount);
        var timeouts = 0;
        var enemyHealingTotals = new List<float>(seedCount);
        var firstEnemyDeathSeconds = new List<float>(seedCount);
        BattleResult? firstVariantWin = null;

        var canonical = BattleResolver.Run(canonicalState, BattleSimulator.DefaultMaxSteps);
        perSeedWin[0] = canonical.Winner == TeamSide.Ally;
        steps.Add(canonical.StepCount);
        timeouts += canonical.StepCount >= BattleSimulator.DefaultMaxSteps ? 1 : 0;
        AccumulateNodeObservability(canonicalState, enemyHealingTotals, firstEnemyDeathSeconds);

        for (var k = 1; k < seedCount; k++)
        {
            var sweepSeed = DeriveSweepSeed(encounter.Context.BattleContextHash, k);
            var variantEncounter = encounter with { Context = encounter.Context with { BattleSeed = sweepSeed } };
            if (!session.TryComposeBattleState(allySnapshot, variantEncounter, out var variantState, out var composeError))
            {
                throw new InvalidOperationException($"variant compose failed({node.Id} k={k}): {composeError}");
            }

            var result = BattleResolver.Run(variantState, BattleSimulator.DefaultMaxSteps);
            perSeedWin[k] = result.Winner == TeamSide.Ally;
            steps.Add(result.StepCount);
            timeouts += result.StepCount >= BattleSimulator.DefaultMaxSteps ? 1 : 0;
            AccumulateNodeObservability(variantState, enemyHealingTotals, firstEnemyDeathSeconds);
            if (perSeedWin[k])
            {
                firstVariantWin ??= result;
            }
        }

        if (canonical.Winner == TeamSide.Ally)
        {
            progression = canonical;
            progressionKind = "canonical-win";
        }
        else if (firstVariantWin != null)
        {
            progression = firstVariantWin;
            progressionKind = "retry-win";
        }
        else
        {
            progression = canonical;
            progressionKind = "forced-march";
        }

        return new NodeSweepMeasurement(
            node.Id,
            encounter.Context.EncounterId,
            perSeedWin[0],
            perSeedWin.Count(win => win) / (float)seedCount,
            Percentile(steps, 0.5f),
            timeouts,
            progressionKind,
            perSeedWin,
            enemyHealingTotals.Count == 0 ? 0f : enemyHealingTotals.Average(),
            firstEnemyDeathSeconds.Count == 0 ? -1f : firstEnemyDeathSeconds.Average());
    }

    /// <summary>노드 관찰 지표(재비준 리스크 감시) — 적측 총 회복량(힐 수렁 검증)과 첫 적 처치 시간.
    /// 판정에 쓰지 않는 순수 관찰값이라 sim/판정 코드 무접촉. id 매칭은 실 인스턴스 id set(접두사 허구 금지).</summary>
    private static void AccumulateNodeObservability(BattleState state, List<float> enemyHealingTotals, List<float> firstEnemyDeathSeconds)
    {
        var enemyIds = new HashSet<string>(state.Enemies.Select(unit => unit.Id.Value), StringComparer.Ordinal);
        var healing = 0f;
        var firstDeath = -1f;
        foreach (var record in state.TelemetryEvents)
        {
            if (record.EventKind == TelemetryEventKind.HealingApplied
                && record.Actor != null
                && enemyIds.Contains(record.Actor.UnitInstanceId))
            {
                healing += Math.Max(0f, record.ValueA);
            }
            else if (record.EventKind == TelemetryEventKind.UnitDied
                && firstDeath < 0f
                && record.Actor != null
                && enemyIds.Contains(record.Actor.UnitInstanceId))
            {
                firstDeath = record.TimeSeconds;
            }
        }

        enemyHealingTotals.Add(healing);
        if (firstDeath >= 0f)
        {
            firstEnemyDeathSeconds.Add(firstDeath);
        }
    }

    // ─────────────────────────────────────────────
    // 델타 레인 — 벤치마크 사이트에서 아이템/유령 패시브의 paired 승률 델타.
    // ─────────────────────────────────────────────

    private sealed record BenchmarkSpec(string ChapterId, string SiteId, int PriorBattleNodeCount, int ChapterOrder);

    /// <summary>
    /// 벤치마크 후보: 어려운 지점부터(종반 벽 → 중반 → 초중반 → 1챕 말) — 분대별 적응 선택이
    /// baseline 민감 구간에 드는 첫 후보를 채택한다. 콘텐츠 순서는 StoryOrder/SiteOrder에서 도출(하드코딩 없음).
    /// </summary>
    private static IReadOnlyList<BenchmarkSpec> ResolveBenchmarkCandidates(CombatContentSnapshot content)
    {
        var chapters = (content.CampaignChapters ?? new Dictionary<string, CampaignChapterTemplate>())
            .Values
            .OrderBy(chapter => chapter.StoryOrder)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            .ToList();
        var sites = content.ExpeditionSites ?? new Dictionary<string, ExpeditionSiteTemplate>();

        var walkOrder = new List<(string ChapterId, string SiteId, int BattleNodes, int ChapterOrder)>();
        for (var chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            var chapter = chapters[chapterIndex];
            var orderedSiteIds = chapter.SiteIds
                .Where(sites.ContainsKey)
                .OrderBy(id => sites[id].SiteOrder)
                .ThenBy(id => id, StringComparer.Ordinal);
            foreach (var siteId in orderedSiteIds)
            {
                walkOrder.Add((chapter.Id, siteId, sites[siteId].EncounterIds.Count, chapterIndex + 1));
            }
        }

        var specs = new List<BenchmarkSpec>();
        void AddBenchmark(int chapterOrder, bool firstSite)
        {
            var candidates = walkOrder.Where(entry => entry.ChapterOrder == chapterOrder).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var chosen = firstSite ? candidates.First() : candidates.Last();
            var prior = walkOrder.TakeWhile(entry => entry.SiteId != chosen.SiteId).Sum(entry => entry.BattleNodes);
            specs.Add(new BenchmarkSpec(chosen.ChapterId, chosen.SiteId, prior, chosen.ChapterOrder));
        }

        AddBenchmark(Math.Min(4, Math.Max(1, chapters.Count - 1)), firstSite: true);
        AddBenchmark(Math.Min(3, chapters.Count), firstSite: true);
        AddBenchmark(Math.Min(2, chapters.Count), firstSite: true);
        AddBenchmark(1, firstSite: false);
        return specs
            .GroupBy(spec => spec.SiteId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>
    /// 분대 1종의 델타 레인: 후보 사이트를 어려운 순으로 스캔해 bare baseline이 민감 구간에 드는
    /// 첫 (사이트, 노드)를 잠근 뒤, 그 분대로 라우팅된 아이템/유령 arm 전부를 같은 지점에서 paired 측정한다.
    /// </summary>
    private static SquadDeltaReport RunDeltaLaneForSquad(
        RuntimeCombatContentLookup lookup,
        IReadOnlyDictionary<string, ItemMeta> itemIndex,
        CombatContentSnapshot content,
        (string Name, string[] Archetypes) squad,
        SweepScope scope)
    {
        var candidates = ResolveBenchmarkCandidates(content).Take(scope.BenchmarkCandidateDepth).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("no benchmark candidates resolved from campaign content.");
        }

        BenchmarkSpec? lockedSpec = null;
        BenchmarkNodeRate? lockedNode = null;
        IReadOnlyList<BenchmarkNodeRate> lockedNodes = Array.Empty<BenchmarkNodeRate>();
        (BenchmarkSpec Spec, BenchmarkNodeRate Node, IReadOnlyList<BenchmarkNodeRate> Nodes, float Distance)? fallback = null;

        foreach (var candidate in candidates)
        {
            var session = CreateBenchmarkSession(lookup, itemIndex, content, candidate, $"baseline_{squad.Name}", squad.Archetypes);
            var nodes = MeasureBenchmarkSiteNodes(session, scope.SeedCount);
            if (nodes.Count == 0)
            {
                continue;
            }

            var best = nodes
                .OrderBy(nodeRate => Math.Abs(nodeRate.WinRate - 0.5f))
                .ThenBy(nodeRate => nodeRate.BattleNodeIndex)
                .First();
            var distance = Math.Abs(best.WinRate - 0.5f);
            Debug.Log($"[CampaignSweep] delta squad={squad.Name} candidate={candidate.SiteId} bestNode={best.NodeId} baseline={best.WinRate:0.00}");
            if (fallback == null || distance < fallback.Value.Distance)
            {
                fallback = (candidate, best, nodes, distance);
            }

            if (best.WinRate >= BenchmarkSensitivityMin && best.WinRate <= BenchmarkSensitivityMax)
            {
                lockedSpec = candidate;
                lockedNode = best;
                lockedNodes = nodes;
                break;
            }
        }

        var lowSensitivity = lockedSpec == null;
        if (lockedSpec == null)
        {
            if (fallback == null)
            {
                throw new InvalidOperationException($"delta squad {squad.Name}: no benchmark candidate had battle nodes.");
            }

            lockedSpec = fallback.Value.Spec;
            lockedNode = fallback.Value.Node;
            lockedNodes = fallback.Value.Nodes;
            Debug.LogWarning($"[CampaignSweep] delta squad={squad.Name} no sensitive benchmark — fallback {lockedSpec.SiteId} baseline={lockedNode.WinRate:0.00}");
        }

        var benchmark = lockedSpec;
        var baselineNode = lockedNode!;

        // 아이템 arm — 이 분대로 라우팅된 것만.
        var itemIds = lookup.GetCanonicalItemIds()
            .OrderBy(id => id, StringComparer.Ordinal)
            .Where(id => !itemIndex.TryGetValue(id, out var meta) || ResolveDeltaSquadForClasses(meta.AllowedClassIds).Name == squad.Name)
            .ToList();
        if (scope.ItemLimit is { } itemLimit)
        {
            itemIds = itemIds.Take(itemLimit).ToList();
        }

        // Q4(재비준): 4-arm paired — A0=bare(baseline), A1=base only, A2=base+대표 어픽스(완제품),
        // C=착용자 민감도 positive control(+3레벨 등가). dead 판정의 주 기준은 A2-A0이고,
        // A1-A0은 harmful/dominant만 판정하는 정보값. 착용자 민감도 미달이면 verdict=insensitive(판정 유보).
        var affixIndex = LoadAffixMetaIndex();
        var sensitivityByHero = new Dictionary<string, float>(StringComparer.Ordinal);

        float ResolveWearerSensitivity(string heroId)
        {
            if (sensitivityByHero.TryGetValue(heroId, out var cached))
            {
                return cached;
            }

            var controlSession = CreateBenchmarkSession(lookup, itemIndex, content, benchmark, $"control_{heroId}", squad.Archetypes);
            // C arm = 예산 확보(+3레벨) 후 착용자만 패시브 보드 탐욕 성장 — 파워 전달이 검증된 채널.
            BoostHeroLevels(controlSession, heroId, SensitivityControlLevelBoost);
            var controlHero = controlSession.Profile.Heroes.FirstOrDefault(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
            if (controlHero != null)
            {
                GrowHeroPassives(controlSession, content, controlHero);
            }

            var controlRate = MeasureBenchmarkNodeWinRate(controlSession, baselineNode.BattleNodeIndex, scope.SeedCount);
            var sensitivity = controlRate - baselineNode.WinRate;
            sensitivityByHero[heroId] = sensitivity;
            Debug.Log($"[CampaignSweep] delta squad={squad.Name} sensitivity hero={heroId} Δ={sensitivity:+0.00;-0.00}");
            return sensitivity;
        }

        var itemArms = new List<DeltaArmMeasurement>();
        foreach (var itemId in itemIds)
        {
            if (!itemIndex.TryGetValue(itemId, out var itemMeta))
            {
                itemArms.Add(new DeltaArmMeasurement(itemId, "item", squad.Name, string.Empty, 0f, 0f, "no-item-meta"));
                continue;
            }

            var session = CreateBenchmarkSession(lookup, itemIndex, content, benchmark, $"item_{itemId}", squad.Archetypes);
            var instanceId = $"sweep_{itemId}";
            session.Profile.Inventory.Add(new InventoryItemRecord
            {
                ItemInstanceId = instanceId,
                ItemBaseId = itemId,
            });
            var equippedHeroId = TryEquipOnEligibleSquadHero(session, itemIndex, instanceId, itemId);
            if (equippedHeroId == null)
            {
                itemArms.Add(new DeltaArmMeasurement(itemId, "item", squad.Name, string.Empty, 0f, 0f, "equip-blocked"));
                continue;
            }

            var rate = MeasureBenchmarkNodeWinRate(session, baselineNode.BattleNodeIndex, scope.SeedCount);
            var baseOnlyDelta = rate - baselineNode.WinRate;

            // A2 — base-only가 dead 미만 구간이면 대표 어픽스 완제품으로 재측정(dead 판정의 주 기준).
            string representativeAffixId = string.Empty;
            float? completeRate = null;
            float? completeDelta = null;
            if (baseOnlyDelta < DeltaDeadBelow)
            {
                representativeAffixId = ResolveRepresentativeAffix(affixIndex, itemMeta.SlotType);
                if (!string.IsNullOrEmpty(representativeAffixId))
                {
                    var completeSession = CreateBenchmarkSession(lookup, itemIndex, content, benchmark, $"item_{itemId}_affix", squad.Archetypes);
                    var completeInstanceId = $"sweep_{itemId}_complete";
                    completeSession.Profile.Inventory.Add(new InventoryItemRecord
                    {
                        ItemInstanceId = completeInstanceId,
                        ItemBaseId = itemId,
                        AffixIds = new List<string> { representativeAffixId },
                    });
                    var completeHeroId = TryEquipOnEligibleSquadHero(completeSession, itemIndex, completeInstanceId, itemId);
                    if (completeHeroId != null)
                    {
                        completeRate = MeasureBenchmarkNodeWinRate(completeSession, baselineNode.BattleNodeIndex, scope.SeedCount);
                        completeDelta = completeRate - baselineNode.WinRate;
                    }
                }
            }

            var wearerSensitivity = ResolveWearerSensitivity(equippedHeroId);
            var verdict = ResolveArmVerdict(baseOnlyDelta, completeDelta, wearerSensitivity);
            itemArms.Add(new DeltaArmMeasurement(
                itemId,
                "item",
                squad.Name,
                equippedHeroId,
                rate,
                baseOnlyDelta,
                "measured",
                representativeAffixId,
                completeRate,
                completeDelta,
                wearerSensitivity,
                verdict));
        }

        // 유령 패시브 arm — 이 분대의 클래스 보드에 속한 host만. prereq-only 대조 arm으로
        // "closure 스탯"과 "유령 스킬 자체"를 분리 관측한다.
        var ghostHosts = content.PassiveNodes.Values
            .Where(nodeTemplate => !string.IsNullOrWhiteSpace(nodeTemplate.GrantedSkillId))
            .Where(nodeTemplate => ResolveDeltaSquadForClasses(new[] { ResolveBoardClass(nodeTemplate.BoardId) }).Name == squad.Name)
            .OrderBy(nodeTemplate => nodeTemplate.Id, StringComparer.Ordinal)
            .ToList();
        if (scope.GhostLimit is { } ghostLimit)
        {
            ghostHosts = ghostHosts.Take(ghostLimit).ToList();
        }

        var ghostArms = new List<GhostArmMeasurement>();
        foreach (var host in ghostHosts)
        {
            var closure = ResolvePrerequisiteClosure(content, host);
            var prereqOnly = closure.Where(id => !string.Equals(id, host.Id, StringComparison.Ordinal)).ToList();

            var fullRate = MeasureGhostArm(lookup, itemIndex, content, benchmark, squad, scope.SeedCount, baselineNode.BattleNodeIndex, host, closure, out var fullStatus, out var heroId);
            var controlRate = prereqOnly.Count == 0
                ? baselineNode.WinRate
                : MeasureGhostArm(lookup, itemIndex, content, benchmark, squad, scope.SeedCount, baselineNode.BattleNodeIndex, host, prereqOnly, out _, out _);

            // Q4: 유령 패시브도 착용자 민감도 게이트를 통과해야 dead 판정이 성립(비민감=insensitive).
            float? ghostSensitivity = fullStatus == "measured" && !string.IsNullOrEmpty(heroId)
                ? ResolveWearerSensitivity(heroId)
                : null;
            var ghostVerdict = fullStatus == "measured"
                ? ResolveArmVerdict(fullRate - baselineNode.WinRate, null, ghostSensitivity)
                : fullStatus;

            ghostArms.Add(new GhostArmMeasurement(
                host.Id,
                host.GrantedSkillId,
                squad.Name,
                heroId,
                closure.Count,
                fullRate,
                fullRate - baselineNode.WinRate,
                fullRate - controlRate,
                fullStatus,
                ghostSensitivity,
                ghostVerdict));
        }

        return new SquadDeltaReport(
            squad.Name,
            string.Join(",", squad.Archetypes),
            benchmark.ChapterId,
            benchmark.SiteId,
            ResolveBenchmarkLevel(benchmark),
            baselineNode.NodeId,
            baselineNode.BattleNodeIndex,
            baselineNode.WinRate,
            lowSensitivity,
            lockedNodes,
            itemArms,
            ghostArms);
    }

    private static string ResolveBoardClass(string boardId)
        => boardId.StartsWith("board_", StringComparison.Ordinal) ? boardId["board_".Length..] : boardId;

    /// <summary>클래스 게이트 → 델타 분대 라우팅: vanguard/duelist(또는 무게이트) → front, ranger/mystic → back.</summary>
    private static (string Name, string[] Archetypes) ResolveDeltaSquadForClasses(IReadOnlyList<string> classIds)
    {
        if (classIds.Count == 0)
        {
            return DeltaSquads[0];
        }

        return classIds.Any(id => id is "vanguard" or "duelist") ? DeltaSquads[0] : DeltaSquads[1];
    }

    private static float MeasureGhostArm(
        RuntimeCombatContentLookup lookup,
        IReadOnlyDictionary<string, ItemMeta> itemIndex,
        CombatContentSnapshot content,
        BenchmarkSpec benchmark,
        (string Name, string[] Archetypes) squad,
        int seedCount,
        int battleNodeIndex,
        PassiveNodeTemplate host,
        IReadOnlyList<string> nodeIdsToSelect,
        out string status,
        out string heroId)
    {
        var session = CreateBenchmarkSession(lookup, itemIndex, content, benchmark, $"ghost_{host.Id}_{nodeIdsToSelect.Count}", squad.Archetypes);
        var boardClass = host.BoardId.StartsWith("board_", StringComparison.Ordinal)
            ? host.BoardId["board_".Length..]
            : host.BoardId;
        var hero = session.Profile.Heroes.FirstOrDefault(candidate => string.Equals(candidate.ClassId, boardClass, StringComparison.Ordinal));
        if (hero == null)
        {
            status = $"no-{boardClass}-hero";
            heroId = string.Empty;
            return float.NaN;
        }

        heroId = hero.HeroId;
        var boardSelected = EnsurePassiveBoardSelected(session, hero);
        if (!boardSelected.IsSuccess)
        {
            status = $"board-select-failed:{boardSelected.Error}";
            return float.NaN;
        }

        var ordered = OrderNodesForToggle(content, nodeIdsToSelect);
        foreach (var nodeId in ordered)
        {
            var toggled = session.TogglePassiveNode(hero.HeroId, nodeId);
            if (!toggled.IsSuccess)
            {
                status = $"toggle-blocked:{nodeId}:{toggled.Error}";
                return float.NaN;
            }
        }

        status = "measured";
        return MeasureBenchmarkNodeWinRate(session, battleNodeIndex, seedCount);
    }

    /// <summary>패시브 토글은 보드 선택이 선행 조건(세션 계약) — 미선택이면 클래스 보드를 선택한다.
    /// 이미 선택된 보드는 건드리지 않는다(SelectPassiveBoard는 기존 선택을 리셋하므로).</summary>
    private static Result EnsurePassiveBoardSelected(GameSessionState session, HeroInstanceRecord hero)
    {
        var loadout = session.Profile.HeroLoadouts
            .FirstOrDefault(record => string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(loadout?.PassiveBoardId))
        {
            return Result.Success();
        }

        return session.SelectPassiveBoard(hero.HeroId, $"board_{hero.ClassId}");
    }

    public sealed record BenchmarkNodeRate(int BattleNodeIndex, string NodeId, float WinRate);

    /// <summary>벤치마크 사이트의 모든 전투 노드를 순서대로 재고 run을 정리한다(진행 정산 없음 — 순수 측정).</summary>
    private static List<BenchmarkNodeRate> MeasureBenchmarkSiteNodes(GameSessionState session, int seedCount)
    {
        session.BeginNewExpedition();
        var rates = new List<BenchmarkNodeRate>();
        var battleNodeIndex = 0;
        while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
        {
            var node = session.GetSelectedExpeditionNode()!;
            var measurement = MeasureSelectedBattleNode(session, node, seedCount, out _, out _);
            rates.Add(new BenchmarkNodeRate(battleNodeIndex, node.Id, measurement.WinRate));
            session.ResolveSelectedExpeditionNode();
            battleNodeIndex++;
        }

        session.AbandonExpeditionRun();
        return rates;
    }

    /// <summary>벤치마크 사이트의 battleNodeIndex번째 전투 노드만 재고 run을 정리한다.</summary>
    private static float MeasureBenchmarkNodeWinRate(GameSessionState session, int battleNodeIndex, int seedCount)
    {
        session.BeginNewExpedition();
        var currentIndex = 0;
        while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
        {
            var node = session.GetSelectedExpeditionNode()!;
            if (currentIndex == battleNodeIndex)
            {
                var measurement = MeasureSelectedBattleNode(session, node, seedCount, out _, out _);
                session.AbandonExpeditionRun();
                return measurement.WinRate;
            }

            session.ResolveSelectedExpeditionNode();
            currentIndex++;
        }

        session.AbandonExpeditionRun();
        throw new InvalidOperationException($"battle node index {battleNodeIndex} not found in benchmark site.");
    }

    /// <summary>
    /// 벤치마크 도달 상태 프로필: 이전 사이트 전부 클리어 + 혼성 분대 + "이전 전투 전승" XP.
    /// 저작은 전부 실게임 API/모델 — 세이브 파일에 닿지 않는 throwaway 세션.
    /// </summary>
    private static GameSessionState CreateBenchmarkSession(
        RuntimeCombatContentLookup lookup,
        IReadOnlyDictionary<string, ItemMeta> itemIndex,
        CombatContentSnapshot content,
        BenchmarkSpec benchmark,
        string armId,
        string[] squadArchetypes)
    {
        var profile = new SaveProfile { ProfileId = $"sweep_delta_{benchmark.SiteId}_{armId}" };
        profile.CampaignProgress.SelectedChapterId = benchmark.ChapterId;
        profile.CampaignProgress.SelectedSiteId = benchmark.SiteId;
        foreach (var (chapterId, siteId) in PriorSitesInWalkOrder(content, benchmark.SiteId))
        {
            profile.CampaignProgress.ClearedSiteIds.Add(siteId);
            if (!profile.CampaignProgress.ClearedChapterIds.Contains(chapterId)
                && ChapterFullyCleared(content, chapterId, profile.CampaignProgress.ClearedSiteIds))
            {
                profile.CampaignProgress.ClearedChapterIds.Add(chapterId);
            }
        }

        var session = new GameSessionState(lookup);
        session.BindProfile(profile);
        session.SetCurrentScene(SceneNames.Town);
        // 벤치마크 분대는 bare — 스타터 기어 재배분 없음(모든 슬롯이 비어 있어야 어떤 아이템 arm도
        // 슬롯 선점에 막히지 않고, baseline이 '아이템 0개'로 순수해진다). squadTag를 arm과 무관하게
        // 분대 구성으로만 파생시켜 baseline과 모든 arm의 hero id 세트가 동일(진짜 paired)하도록 한다.
        AuthorSquad(session, lookup, string.Join("+", squadArchetypes), squadArchetypes, itemIndex, redistributeStarterGear: false);

        // 선택 좌표가 프로필에서 복원되지 않는 lane 대비 — 이전 전부 클리어라 '첫 미클리어 = 벤치마크'.
        // 그래도 어긋나면 조용히 다른 사이트를 재는 측정 허구가 되므로 하드 단언으로 fail-loud.
        AdvanceToNextUnclearedSite(session);
        if (!string.Equals(session.SelectedCampaignSiteId, benchmark.SiteId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"benchmark site mismatch: selected={session.SelectedCampaignSiteId}, expected={benchmark.SiteId}");
        }

        var experience = benchmark.PriorBattleNodeCount * BattleXpGainPerVictory;
        foreach (var hero in session.Profile.Heroes)
        {
            ApplyExperience(session.Profile, hero.HeroId, experience);
        }

        return session;
    }

    private static IEnumerable<(string ChapterId, string SiteId)> PriorSitesInWalkOrder(CombatContentSnapshot content, string targetSiteId)
    {
        var sites = content.ExpeditionSites ?? new Dictionary<string, ExpeditionSiteTemplate>();
        var chapters = (content.CampaignChapters ?? new Dictionary<string, CampaignChapterTemplate>())
            .Values
            .OrderBy(chapter => chapter.StoryOrder)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal);
        foreach (var chapter in chapters)
        {
            var orderedSiteIds = chapter.SiteIds
                .Where(sites.ContainsKey)
                .OrderBy(id => sites[id].SiteOrder)
                .ThenBy(id => id, StringComparer.Ordinal);
            foreach (var siteId in orderedSiteIds)
            {
                if (string.Equals(siteId, targetSiteId, StringComparison.Ordinal))
                {
                    yield break;
                }

                yield return (chapter.Id, siteId);
            }
        }
    }

    private static bool ChapterFullyCleared(CombatContentSnapshot content, string chapterId, IReadOnlyList<string> clearedSiteIds)
    {
        if (content.CampaignChapters == null || !content.CampaignChapters.TryGetValue(chapterId, out var chapter))
        {
            return false;
        }

        return chapter.SiteIds.All(clearedSiteIds.Contains);
    }

    private static int ResolveBenchmarkLevel(BenchmarkSpec benchmark)
    {
        var experience = benchmark.PriorBattleNodeCount * BattleXpGainPerVictory;
        var level = 1;
        while (experience >= HeroProgressionCurve.ExperienceToNextLevel(level))
        {
            experience -= HeroProgressionCurve.ExperienceToNextLevel(level);
            level++;
        }

        return level;
    }

    private static void ApplyExperience(SaveProfile profile, string heroId, int experience)
    {
        var progression = profile.HeroProgressions.FirstOrDefault(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        if (progression == null)
        {
            progression = new HeroProgressionRecord { HeroId = heroId, Level = 1 };
            profile.HeroProgressions.Add(progression);
        }

        progression.Experience += experience;
        while (progression.Experience >= HeroProgressionCurve.ExperienceToNextLevel(progression.Level))
        {
            progression.Experience -= HeroProgressionCurve.ExperienceToNextLevel(progression.Level);
            progression.Level++;
        }
    }

    /// <summary>Q4 착용자 민감도 컨트롤(C arm)의 파워 부스트 1단계 — 레벨은 이 게임에서 전투 스탯에
    /// 직결되지 않고(2회전 실측: 레벨만 부스트한 C arm 전부 Δ0) 패시브 예산 계단(5→8)만 연다.
    /// 실제 파워 전달은 2단계(GrowHeroPassives — 검증된 스탯 노드 채널)가 담당한다.</summary>
    private static void BoostHeroLevels(GameSessionState session, string heroId, int levels)
    {
        var progression = session.Profile.HeroProgressions.FirstOrDefault(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        var level = progression?.Level ?? 1;
        var experience = 0;
        for (var i = 0; i < levels; i++)
        {
            experience += HeroProgressionCurve.ExperienceToNextLevel(level + i);
        }

        ApplyExperience(session.Profile, heroId, experience);
    }

    private sealed record AffixMeta(string AffixId, IReadOnlyList<ItemSlotType> Slots, bool Unconditional, bool Spawnable, float BudgetScore);

    private static IReadOnlyList<AffixMeta> LoadAffixMetaIndex()
    {
        var list = new List<AffixMeta>();
        foreach (var definition in Resources.LoadAll<AffixDefinition>("_Game/Content/Definitions/Affixes"))
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            list.Add(new AffixMeta(
                definition.Id,
                (definition.AllowedSlotTypes ?? new List<ItemSlotType>()).ToList(),
                (definition.RequiredTags ?? new List<StableTagDefinition>()).All(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)),
                definition.SpawnWeight > 0f,
                definition.BudgetScore));
        }

        return list.OrderBy(meta => meta.AffixId, StringComparer.Ordinal).ToList();
    }

    /// <summary>Q4 대표 어픽스 선정(재비준 규칙): legal pool(슬롯 허용 + 무조건 발동 + spawnable) 중
    /// 파워 예산이 pool 중앙값에 가장 가까운 것, 동률은 affix id 사전순 최초.</summary>
    private static string ResolveRepresentativeAffix(IReadOnlyList<AffixMeta> affixes, ItemSlotType slot)
    {
        var pool = affixes.Where(meta => meta.Spawnable && meta.Unconditional && meta.Slots.Contains(slot)).ToList();
        if (pool.Count == 0)
        {
            return string.Empty;
        }

        var scores = pool.Select(meta => meta.BudgetScore).OrderBy(score => score).ToList();
        var median = scores[scores.Count / 2];
        return pool
            .OrderBy(meta => Math.Abs(meta.BudgetScore - median))
            .ThenBy(meta => meta.AffixId, StringComparer.Ordinal)
            .First().AffixId;
    }

    /// <summary>Q4 verdict: 민감도 미달=insensitive(판정 유보), 이후 유효 델타(완제품 우선)를
    /// harmful/dead/grey-low/target/grey-high/dominant로 등급화.</summary>
    private static string ResolveArmVerdict(float baseOnlyDelta, float? completeDelta, float? wearerSensitivity)
    {
        if (wearerSensitivity.HasValue && wearerSensitivity.Value < SensitivityControlMinDelta)
        {
            return "insensitive";
        }

        var effective = completeDelta ?? baseOnlyDelta;
        if (effective < 0f)
        {
            return "harmful";
        }

        if (effective < DeltaDeadBelow)
        {
            return "dead";
        }

        if (effective < DeltaTargetMin)
        {
            return "grey-low";
        }

        if (effective <= DeltaTargetMax)
        {
            return "target";
        }

        return effective <= DeltaGreyHighMax ? "grey-high" : "dominant";
    }

    // ─────────────────────────────────────────────
    // 분대 저작/성장 헬퍼 — 전부 실게임 세션 API 경유.
    // ─────────────────────────────────────────────

    /// <summary>
    /// 아이템 저작 메타(클래스 게이트 + 슬롯) — EquipItemCore는 클래스/슬롯을 검증하지 않으므로(UI 소유),
    /// 측정이 정직하려면 러너가 저작 게이트를 직접 지켜야 한다. 아니면 원거리 활이 vanguard에 붙어
    /// "델타 0 = dead" 허구를 생산한다.
    /// </summary>
    internal sealed record ItemMeta(string ItemId, ItemSlotType SlotType, IReadOnlyList<string> AllowedClassIds);

    internal static IReadOnlyDictionary<string, ItemMeta> LoadItemMetaIndex()
    {
        var index = new Dictionary<string, ItemMeta>(StringComparer.Ordinal);
        foreach (var definition in Resources.LoadAll<ItemBaseDefinition>("_Game/Content/Definitions/Items"))
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || index.ContainsKey(definition.Id))
            {
                continue;
            }

            index[definition.Id] = new ItemMeta(
                definition.Id,
                definition.SlotType,
                (definition.AllowedClassTags ?? new List<StableTagDefinition>())
                    .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
                    .Select(tag => tag.Id)
                    .ToList());
        }

        return index;
    }

    internal static bool CanWear(
        GameSessionState session,
        IReadOnlyDictionary<string, ItemMeta> itemIndex,
        HeroInstanceRecord hero,
        ItemMeta meta)
    {
        if (meta.AllowedClassIds.Count > 0 && !meta.AllowedClassIds.Contains(hero.ClassId))
        {
            return false;
        }

        foreach (var equippedInstanceId in hero.EquippedItemIds)
        {
            var equippedBaseId = session.Profile.Inventory
                .FirstOrDefault(item => string.Equals(item.ItemInstanceId, equippedInstanceId, StringComparison.Ordinal))
                ?.ItemBaseId;
            if (equippedBaseId != null
                && itemIndex.TryGetValue(equippedBaseId, out var equippedMeta)
                && equippedMeta.SlotType == meta.SlotType)
            {
                return false;
            }
        }

        return true;
    }

    internal static void AuthorSquad(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        string squadTag,
        string[] archetypes,
        IReadOnlyDictionary<string, ItemMeta> itemIndex,
        bool redistributeStarterGear = true)
    {
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"sweep squad authoring: combat snapshot unavailable — {contentError}");
        }

        // 데모 8인 중 1명만 남기고 정리 → 프리셋 4인 저작 → 마지막 데모 해고.
        // (전량 선해고는 '빈 로스터' 코너, 전량 선저작은 로스터 캡 코너 — 이 순서가 둘 다 피한다.)
        var demoHeroIds = session.Profile.Heroes
            .Where(hero => hero.HeroId.StartsWith("hero-", StringComparison.Ordinal))
            .Select(hero => hero.HeroId)
            .ToList();
        foreach (var demoHeroId in demoHeroIds.Skip(1))
        {
            RequireSuccess(session.DismissHero(demoHeroId), $"dismiss {demoHeroId}");
        }

        for (var index = 0; index < archetypes.Length; index++)
        {
            GrantDeterministicHero(session, lookup, content, squadTag, archetypes[index], index);
        }

        if (demoHeroIds.Count > 0)
        {
            RequireSuccess(session.DismissHero(demoHeroIds[0]), $"dismiss {demoHeroIds[0]}");
        }

        ApplyClassBasedDeployment(session);
        if (redistributeStarterGear)
        {
            GreedyEquipInventory(session, itemIndex); // 데모 스타터 기어를 프리셋 분대에 재배분 — 실게임 시작 전력 등가.
        }
    }

    /// <summary>
    /// 측정 분대원 직저작 — GrantHeroDirect는 hero id를 GUID로 뽑는데(세션 계약), 전투 타깃 동률 해소가
    /// unit id ordinal을 쓰므로 id 재추첨은 arm 간 paired 비교와 곡선 재현성을 통째로 깨뜨린다
    /// (1차 full 실측: 아이템 42종 중 33종 음수 쏠림 = baseline id-추첨 운의 허구). 레코드 구성은
    /// SeedDemoProfile과 동일 문법(기본 flex/트레잇 normalize/결정적 dominant hand), id만 결정화한다.
    /// </summary>
    private static void GrantDeterministicHero(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        CombatContentSnapshot content,
        string squadTag,
        string archetypeId,
        int index)
    {
        if (!content.Archetypes.TryGetValue(archetypeId, out var archetype))
        {
            throw new InvalidOperationException($"sweep squad archetype missing: {archetypeId}");
        }

        var heroId = $"sweep-{squadTag}-{index + 1}-{archetypeId}";
        session.Profile.Heroes.Add(new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = archetype.DisplayName,
            ArchetypeId = archetypeId,
            RaceId = archetype.RaceId,
            ClassId = archetype.ClassId,
            PositiveTraitId = lookup.NormalizePositiveTraitId(archetypeId, string.Empty, index),
            NegativeTraitId = lookup.NormalizeNegativeTraitId(archetypeId, string.Empty, index + 1),
            FlexActiveId = archetype.FlexActive?.Id ?? string.Empty,
            FlexPassiveId = archetype.FlexPassive?.Id ?? string.Empty,
            RecruitTier = archetype.RecruitTier,
            RecruitSource = RecruitOfferSource.DirectGrant,
            DominantHand = DominantHandDistributionService.ResolveGenerated(heroId, archetype.ClassId),
            RetrainState = new UnitRetrainState(),
            EconomyFootprint = new UnitEconomyFootprint(),
            EquippedItemIds = new List<string>(),
        });
    }

    private static void ApplyClassBasedDeployment(GameSessionState session)
    {
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        var frontAnchors = new Queue<DeploymentAnchorId>(new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontBottom,
        });
        var backAnchors = new Queue<DeploymentAnchorId>(new[]
        {
            DeploymentAnchorId.BackCenter,
            DeploymentAnchorId.BackTop,
            DeploymentAnchorId.BackBottom,
        });

        foreach (var hero in session.Profile.Heroes)
        {
            var prefersFront = hero.ClassId is "vanguard" or "duelist";
            var lane = prefersFront
                ? (frontAnchors.Count > 0 ? frontAnchors : backAnchors)
                : (backAnchors.Count > 0 ? backAnchors : frontAnchors);
            if (lane.Count == 0)
            {
                break;
            }

            session.AssignHeroToAnchor(lane.Dequeue(), hero.HeroId);
        }
    }

    /// <summary>
    /// 미장착 인벤토리를 결정적 순서로 순회하며 클래스 게이트+슬롯(종류당 1개)을 지키는 첫 분대원에게
    /// 장착 — "드랍을 쓰는 일반 플레이어"의 근사. 게이트 판정은 러너 소유(CanWear 주석 참조).
    /// </summary>
    private static void GreedyEquipInventory(GameSessionState session, IReadOnlyDictionary<string, ItemMeta> itemIndex)
    {
        // 정렬키에 인스턴스 id를 쓰지 않는다 — 드랍 인스턴스 id는 GUID(SessionInventoryItemBuilder)라
        // 런마다 달라져 장착 배분이 비재현이 된다. base id + 획득 순서(리스트 인덱스)는 시드 결정적.
        var unequipped = session.Profile.Inventory
            .Select((item, acquisitionIndex) => (Item: item, AcquisitionIndex: acquisitionIndex))
            .Where(entry => string.IsNullOrEmpty(entry.Item.EquippedHeroId))
            .OrderBy(entry => entry.Item.ItemBaseId, StringComparer.Ordinal)
            .ThenBy(entry => entry.AcquisitionIndex)
            .Select(entry => entry.Item)
            .ToList();
        foreach (var item in unequipped)
        {
            if (!itemIndex.TryGetValue(item.ItemBaseId, out var meta))
            {
                continue;
            }

            foreach (var hero in session.Profile.Heroes)
            {
                if (CanWear(session, itemIndex, hero, meta)
                    && session.EquipItem(hero.HeroId, item.ItemInstanceId).IsSuccess)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 얕은 노드부터(보드 depth → id ordinal) 예산이 허용하는 만큼 토글 — "일반 플레이어"의 성장 근사.
    /// 예산 계단(레벨 5→8)은 세션 validator가 소유하므로 여기서는 Result만 소비한다.
    /// </summary>
    internal static void GreedyGrowPassives(GameSessionState session, RuntimeCombatContentLookup lookup)
    {
        if (!lookup.TryGetCombatSnapshot(out var content, out _))
        {
            return;
        }

        foreach (var hero in session.Profile.Heroes.ToList())
        {
            GrowHeroPassives(session, content, hero);
        }
    }

    /// <summary>단일 hero의 보드를 얕은 노드부터 예산까지 탐욕 토글 — 곡선 레인 성장과
    /// Q4 민감도 컨트롤(C arm)이 같은 코드를 공유한다(레인 간 성장 의미 동일성).</summary>
    private static void GrowHeroPassives(GameSessionState session, CombatContentSnapshot content, HeroInstanceRecord hero)
    {
        if (!EnsurePassiveBoardSelected(session, hero).IsSuccess)
        {
            return;
        }

        var boardId = $"board_{hero.ClassId}";
        var selected = new HashSet<string>(
            session.Profile.PassiveSelections
                .FirstOrDefault(record => string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal))
                ?.SelectedNodeIds ?? new List<string>(),
            StringComparer.Ordinal);
        var boardNodes = content.PassiveNodes.Values
            .Where(nodeTemplate => string.Equals(nodeTemplate.BoardId, boardId, StringComparison.Ordinal))
            .OrderBy(nodeTemplate => nodeTemplate.BoardDepth)
            .ThenBy(nodeTemplate => nodeTemplate.Id, StringComparer.Ordinal)
            .ToList();

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var nodeTemplate in boardNodes)
            {
                if (selected.Contains(nodeTemplate.Id))
                {
                    continue;
                }

                if (session.TogglePassiveNode(hero.HeroId, nodeTemplate.Id).IsSuccess)
                {
                    selected.Add(nodeTemplate.Id);
                    changed = true;
                }
            }
        }
    }

    private static string? TryEquipOnEligibleSquadHero(
        GameSessionState session,
        IReadOnlyDictionary<string, ItemMeta> itemIndex,
        string itemInstanceId,
        string itemBaseId)
    {
        if (!itemIndex.TryGetValue(itemBaseId, out var meta))
        {
            return null;
        }

        foreach (var hero in session.Profile.Heroes)
        {
            if (CanWear(session, itemIndex, hero, meta)
                && session.EquipItem(hero.HeroId, itemInstanceId).IsSuccess)
            {
                return hero.HeroId;
            }
        }

        return null;
    }

    private static (int Min, int Max) ResolveSquadLevels(GameSessionState session)
    {
        var levels = session.Profile.Heroes
            .Select(hero => session.Profile.HeroProgressions
                .FirstOrDefault(record => string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal))?.Level ?? 1)
            .ToList();
        return levels.Count == 0 ? (1, 1) : (levels.Min(), levels.Max());
    }

    private static void AdvanceToNextUnclearedSite(GameSessionState session)
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

    private static IReadOnlyList<string> ResolvePrerequisiteClosure(CombatContentSnapshot content, PassiveNodeTemplate host)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(host.Id);
        while (stack.Count > 0)
        {
            var currentId = stack.Pop();
            if (!closure.Add(currentId) || !content.PassiveNodes.TryGetValue(currentId, out var currentNode))
            {
                continue;
            }

            foreach (var prerequisiteId in currentNode.PrerequisiteNodeIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(prerequisiteId))
                {
                    stack.Push(prerequisiteId);
                }
            }
        }

        return OrderNodesForToggle(content, closure.ToList());
    }

    private static IReadOnlyList<string> OrderNodesForToggle(CombatContentSnapshot content, IReadOnlyList<string> nodeIds)
    {
        return nodeIds
            .OrderBy(id => content.PassiveNodes.TryGetValue(id, out var nodeTemplate) ? nodeTemplate.BoardDepth : int.MaxValue)
            .ThenBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    private static void RequireSuccess(Result result, string operation)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"sweep squad authoring failed({operation}): {result.Error}");
        }
    }

    /// <summary>프로세스 불변 FNV-1a 파생 시드 — HashCode.Combine 금지 계약(BuildStableSeed 지뢰) 준수.</summary>
    private static int DeriveSweepSeed(string battleContextHash, int index)
    {
        unchecked
        {
            var hash = 2166136261u;
            var input = $"sweep|{battleContextHash}|{index.ToString(CultureInfo.InvariantCulture)}";
            foreach (var character in input)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return (int)hash;
        }
    }

    private static float Percentile(IReadOnlyList<float> values, float percentile)
    {
        if (values.Count == 0)
        {
            return 0f;
        }

        var ordered = values.OrderBy(value => value).ToList();
        var index = Mathf.Clamp(Mathf.CeilToInt((ordered.Count - 1) * percentile), 0, ordered.Count - 1);
        return ordered[index];
    }

    // ─────────────────────────────────────────────
    // 밴드 판정 + 리포트.
    // ─────────────────────────────────────────────

    private static CurveFindings EvaluateCurveBands(CombatContentSnapshot content, IReadOnlyList<SquadCurveReport> curve)
    {
        var chapterOrderById = (content.CampaignChapters ?? new Dictionary<string, CampaignChapterTemplate>())
            .Values
            .OrderBy(chapter => chapter.StoryOrder)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            .Select((chapter, index) => (chapter.Id, Order: index + 1))
            .ToDictionary(entry => entry.Id, entry => entry.Order, StringComparer.Ordinal);
        var finalChapterOrder = chapterOrderById.Count == 0 ? 1 : chapterOrderById.Values.Max();

        var cliffs = new List<string>();
        var chapter1Violations = new List<string>();
        var finalBandNotes = new List<string>();
        var diagnostics = new List<string>();
        var lateSaturation = new List<string>();

        foreach (var preset in curve)
        {
            var lateNodesAtSaturation = 0;
            var lateNodesTotal = 0;
            for (var index = 0; index < preset.Sites.Count; index++)
            {
                var site = preset.Sites[index];
                var chapterOrder = chapterOrderById.TryGetValue(site.ChapterId, out var order) ? order : 0;
                foreach (var node in site.Nodes)
                {
                    // Q2: 절벽의 공식 판정 단위는 개별 노드 첫판 승률.
                    if (node.WinRate < CurveCliffNodeMinWinRate)
                    {
                        cliffs.Add($"{preset.Preset}:{site.SiteId}/{node.NodeId}={node.WinRate:0.00}");
                    }

                    // Q1: 1챕 온보딩은 노드별로, 3분대 전부.
                    if (chapterOrder == 1 && node.WinRate < CurveChapter1MinNodeWinRate)
                    {
                        chapter1Violations.Add($"{preset.Preset}:{site.SiteId}/{node.NodeId}={node.WinRate:0.00}");
                    }
                }

                // 진단 전용(자동 판정 아님) — 사이트 AND율 기반 연속 낙차는 관찰 신호로만 남긴다.
                if (index > 0)
                {
                    var drop = preset.Sites[index - 1].ClearRate - site.ClearRate;
                    if (drop > CurveCliffMaxDrop)
                    {
                        diagnostics.Add($"낙차(진단) {preset.Preset}:{preset.Sites[index - 1].SiteId}({preset.Sites[index - 1].ClearRate:0.00})→{site.SiteId}({site.ClearRate:0.00}) drop={drop:0.00}");
                    }
                }

                if (chapterOrder == finalChapterOrder)
                {
                    // Q1: 종챕 50~70%는 고정 기준 분대(frontline)만, 카운터 분대는 바닥 0.85만.
                    if (string.Equals(preset.Preset, CurveReferenceSquad, StringComparison.Ordinal)
                        && (site.ClearRate < CurveFinalChapterMinClearRate || site.ClearRate > CurveFinalChapterMaxClearRate))
                    {
                        finalBandNotes.Add($"{preset.Preset}:{site.SiteId}={site.ClearRate:0.00} (기준 분대 밴드 {CurveFinalChapterMinClearRate:0.00}~{CurveFinalChapterMaxClearRate:0.00})");
                    }
                    else if (!string.Equals(preset.Preset, CurveReferenceSquad, StringComparison.Ordinal)
                        && site.ClearRate < CurveFinalCounterMinClearRate)
                    {
                        finalBandNotes.Add($"{preset.Preset}:{site.SiteId}={site.ClearRate:0.00} (카운터 분대 바닥 {CurveFinalCounterMinClearRate:0.00})");
                    }
                }

                // 관찰: 종반(마지막 2사이트) 상시 포화 — 카운터 분대의 무긴장화 리스크 감시.
                if (index >= preset.Sites.Count - 2)
                {
                    foreach (var node in site.Nodes)
                    {
                        lateNodesTotal++;
                        if (node.WinRate >= LateSaturationNodeRate)
                        {
                            lateNodesAtSaturation++;
                        }
                    }
                }
            }

            lateSaturation.Add($"{preset.Preset}: 종반 {lateNodesTotal}노드 중 {lateNodesAtSaturation}노드 ≥{LateSaturationNodeRate:0.00}");
        }

        return new CurveFindings(cliffs, chapter1Violations, finalBandNotes, diagnostics, lateSaturation);
    }

    private static DeltaOutliers EvaluateDeltaBands(IReadOnlyList<SquadDeltaReport> deltas)
    {
        // Q4(재비준): 거버넌스 대상은 verdict — dead는 완제품(A2) 기준이고 착용자 민감도 게이트를
        // 통과한 arm에만 성립한다. base-only(A1)는 harmful/dominant만 판정(dead 하한 미적용, 정보값).
        var dead = new List<string>();
        var dominant = new List<string>();
        var harmful = new List<string>();
        var insensitive = new List<string>();

        void Classify(string squad, string siteId, string contentId, string verdict, float effectiveDelta)
        {
            var label = $"{squad}@{siteId}:{contentId} Δ{effectiveDelta:+0.00;-0.00}";
            switch (verdict)
            {
                case "dead":
                    dead.Add(label);
                    break;
                case "dominant":
                    dominant.Add(label);
                    break;
                case "harmful":
                    harmful.Add(label);
                    break;
                case "insensitive":
                    insensitive.Add(label);
                    break;
            }
        }

        foreach (var delta in deltas)
        {
            foreach (var arm in delta.ItemArms.Where(arm => arm.Status == "measured"))
            {
                Classify(delta.Squad, delta.SiteId, arm.ContentId, arm.Verdict, arm.CompleteDelta ?? arm.Delta);
            }

            foreach (var ghost in delta.GhostArms.Where(arm => arm.Status == "measured"))
            {
                Classify(delta.Squad, delta.SiteId, ghost.HostNodeId, ghost.Verdict, ghost.DeltaVsBaseline);
            }
        }

        return new DeltaOutliers(dead, dominant, harmful, insensitive);
    }

    private static string WriteReports(
        SweepScope scope,
        IReadOnlyList<SquadCurveReport> curve,
        CurveFindings curveFindings,
        IReadOnlyList<SquadDeltaReport> deltas,
        DeltaOutliers outliers)
    {
        var reportDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFolderName));
        Directory.CreateDirectory(reportDirectory);

        File.WriteAllText(
            Path.Combine(reportDirectory, CurveReportFileName),
            JsonConvert.SerializeObject(new { scope.SeedCount, curve, curveFindings }, Formatting.Indented));
        File.WriteAllText(
            Path.Combine(reportDirectory, DeltaReportFileName),
            JsonConvert.SerializeObject(new { scope.SeedCount, deltas, outliers }, Formatting.Indented));
        File.WriteAllText(Path.Combine(reportDirectory, HumanReportFileName), BuildHumanReport(scope, curve, curveFindings, deltas, outliers));
        return reportDirectory;
    }

    private static string BuildHumanReport(
        SweepScope scope,
        IReadOnlyList<SquadCurveReport> curve,
        CurveFindings curveFindings,
        IReadOnlyList<SquadDeltaReport> deltas,
        DeltaOutliers outliers)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 캠페인 밸런스 sweep 리포트 (게이트①)");
        builder.AppendLine();
        builder.AppendLine($"- 시드 {scope.SeedCount} (0=canonical) · 곡선 분대 {curve.Count}종 · 델타 분대 {deltas.Count}종(적응 벤치마크)");
        builder.AppendLine($"- 곡선 밴드(2회전 재비준): 1챕 노드별 ≥{CurveChapter1MinNodeWinRate:0.00}(전 분대) · 종챕 {CurveReferenceSquad} {CurveFinalChapterMinClearRate:0.00}~{CurveFinalChapterMaxClearRate:0.00} / 카운터 분대 ≥{CurveFinalCounterMinClearRate:0.00} · 절벽 = 노드 승률 <{CurveCliffNodeMinWinRate:0.00}");
        builder.AppendLine($"- 델타 밴드(2회전 재비준): dead 판정은 완제품(A2=base+대표어픽스) + 착용자 민감도(C, Δ≥{SensitivityControlMinDelta:0.00}) 통과 시에만 · 목표 {DeltaTargetMin:0.00}~{DeltaTargetMax:0.00} · dominant >{DeltaDominantAbove:0.00}");
        builder.AppendLine($"- 민감도 컨트롤 구현: 착용자 +{SensitivityControlLevelBoost}레벨(예산 계단) + 해당 hero 패시브 보드 탐욕 성장 — 슬롯별 스탯 ×1.25의 세션 합법 대체(검증된 스탯 노드 채널, 편차 명시)");
        builder.AppendLine();

        builder.AppendLine("## 캠페인 곡선 (사이트 클리어율 = 시드별 전 노드 첫판 전승률)");
        builder.AppendLine();
        foreach (var preset in curve)
        {
            builder.AppendLine($"### {preset.Preset} ({preset.Archetypes}) — {(preset.StoryCleared ? "완주" : "미완주")}");
            builder.AppendLine();
            builder.AppendLine("| 챕터 | 사이트 | 도달Lv | 클리어율 | 노드별 승률 | 비고 |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var site in preset.Sites)
            {
                var nodeRates = string.Join(" ", site.Nodes.Select(node => $"{node.WinRate:0.00}{(node.CanonicalWin ? "" : "*")}"));
                var note = site.ForcedMarch ? "⚠️ 벽(forced-march)" : string.Empty;
                builder.AppendLine($"| {site.ChapterId} | {site.SiteId} | {site.ArrivalLevelMin}~{site.ArrivalLevelMax} | {site.ClearRate:0.00} | {nodeRates} | {note} |");
            }

            builder.AppendLine();
        }

        builder.AppendLine("_노드별 승률의 `*` = canonical 시드 패배._");
        builder.AppendLine();
        builder.AppendLine("### 곡선 밴드 판정 (2회전 재비준 기준)");
        builder.AppendLine();
        AppendFindingList(builder, $"절벽(노드 승률 <{CurveCliffNodeMinWinRate:0.00})", curveFindings.Cliffs);
        AppendFindingList(builder, $"1챕 노드 {CurveChapter1MinNodeWinRate:0.00} 미달(전 분대)", curveFindings.Chapter1Violations);
        AppendFindingList(builder, "종챕 밴드 이탈", curveFindings.FinalChapterBandNotes);
        AppendFindingList(builder, "진단(판정 비관여 — 사이트 AND율 낙차)", curveFindings.Diagnostics);
        builder.AppendLine();
        builder.AppendLine("### 관찰 지표 (리스크 감시)");
        builder.AppendLine();
        foreach (var entry in curveFindings.LateSaturation)
        {
            builder.AppendLine($"- 종반 포화: {entry}");
        }

        foreach (var preset in curve)
        {
            foreach (var site in preset.Sites)
            {
                foreach (var node in site.Nodes.Where(node => node.WinRate < 0.60f))
                {
                    var firstKill = node.MeanFirstEnemyDeathSeconds < 0f ? "관측없음" : $"{node.MeanFirstEnemyDeathSeconds:0.0}s";
                    builder.AppendLine($"- 저승률 노드 심층: {preset.Preset}:{site.SiteId}/{node.NodeId} 승률 {node.WinRate:0.00} · 적측 평균 회복 {node.MeanEnemyHealing:0.0} · 첫 적 처치 {firstKill}");
                }
            }
        }

        builder.AppendLine();

        builder.AppendLine("## 아이템/유령 패시브 델타 (paired 시드, 분대별 bare baseline 대비 %p)");
        builder.AppendLine();
        foreach (var delta in deltas)
        {
            var sensitivityNote = delta.LowSensitivity ? " ⚠️ low-sensitivity(전 후보 불감 — 최근접 fallback)" : string.Empty;
            builder.AppendLine($"### {delta.Squad} 분대 ({delta.Archetypes}) — 벤치마크 {delta.ChapterId}/{delta.SiteId} Lv{delta.SquadLevel}, 노드 {delta.ChosenNodeId}, baseline {delta.BaselineWinRate:0.00}{sensitivityNote}");
            builder.AppendLine();
            builder.AppendLine("| 콘텐츠 | 종류 | 장착 대상 | base Δ(A1) | 완제품 Δ(A2) | 민감도(C) | verdict |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var arm in delta.ItemArms)
            {
                var complete = arm.CompleteDelta.HasValue
                    ? $"{arm.CompleteDelta.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)} ({arm.RepresentativeAffixId})"
                    : "—";
                var sensitivity = arm.WearerSensitivityDelta.HasValue
                    ? arm.WearerSensitivityDelta.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)
                    : "—";
                builder.AppendLine($"| {arm.ContentId} | {arm.Kind} | {arm.EquippedHeroId} | {FormatDelta(arm.Delta, arm.Status)} | {complete} | {sensitivity} | {ResolveVerdictLabel(arm.Verdict, arm.Status)} |");
            }

            foreach (var ghost in delta.GhostArms)
            {
                var sensitivity = ghost.WearerSensitivityDelta.HasValue
                    ? ghost.WearerSensitivityDelta.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)
                    : "—";
                builder.AppendLine($"| {ghost.HostNodeId} ({ghost.GrantedSkillId}) | ghost | {ghost.HeroId} | {FormatDelta(ghost.DeltaVsBaseline, ghost.Status)} (스킬 단독 {FormatDelta(ghost.DeltaVsPrereqOnly, ghost.Status)}) | — | {sensitivity} | {ResolveVerdictLabel(ghost.Verdict, ghost.Status)} |");
            }

            builder.AppendLine();
        }

        builder.AppendLine("### 델타 outlier (verdict 기준 — 완제품 Δ + 민감도 게이트)");
        builder.AppendLine();
        AppendFindingList(builder, "harmful (Δ<0)", outliers.Harmful);
        AppendFindingList(builder, $"dead (<{DeltaDeadBelow:P0}, 완제품 기준)", outliers.Dead);
        AppendFindingList(builder, $"dominant (>{DeltaDominantAbove:P0})", outliers.Dominant);
        AppendFindingList(builder, "insensitive (벤치마크 비민감 — 판정 유보)", outliers.Insensitive);
        builder.AppendLine();
        builder.AppendLine("_상태 배율 7채널은 비준대로 측정 arm 없음 — 상태 부여 콘텐츠의 델타로 관측하고 배율은 outlier 조정 노브로만 쓴다._");
        return builder.ToString();
    }

    private static void AppendFindingList(StringBuilder builder, string title, IReadOnlyList<string> findings)
    {
        builder.AppendLine(findings.Count == 0
            ? $"- {title}: 없음 ✅"
            : $"- {title}: {string.Join(" · ", findings)}");
    }

    private static string FormatRate(float rate, string status)
        => status == "measured" ? rate.ToString("0.00", CultureInfo.InvariantCulture) : status;

    private static string FormatDelta(float delta, string status)
        => status == "measured" ? delta.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) : "—";

    private static string ResolveVerdictLabel(string verdict, string status)
        => status != "measured" ? status : string.IsNullOrEmpty(verdict) ? "—" : verdict;

    // ─────────────────────────────────────────────
    // 관찰값 모델(리포트 직렬화 대상).
    // ─────────────────────────────────────────────

    public sealed record SweepRunResult(
        IReadOnlyList<SquadCurveReport> Curve,
        CurveFindings CurveFindings,
        IReadOnlyList<SquadDeltaReport> Deltas,
        DeltaOutliers Outliers,
        string ReportDirectory);

    public sealed record SquadCurveReport(
        string Preset,
        string Archetypes,
        bool StoryCleared,
        IReadOnlyList<SiteSweepMeasurement> Sites);

    public sealed record SiteSweepMeasurement(
        string ChapterId,
        string SiteId,
        int ArrivalLevelMin,
        int ArrivalLevelMax,
        float ClearRate,
        bool ForcedMarch,
        IReadOnlyList<NodeSweepMeasurement> Nodes);

    public sealed record NodeSweepMeasurement(
        string NodeId,
        string EncounterId,
        bool CanonicalWin,
        float WinRate,
        float StepP50,
        int Timeouts,
        string ProgressionKind,
        [property: JsonIgnore] IReadOnlyList<bool> PerSeedWin,
        // 관찰 지표(판정 비관여): 적측 평균 총 회복량(힐 수렁 검증) · 첫 적 처치까지 평균 초(-1=관측 없음).
        float MeanEnemyHealing = 0f,
        float MeanFirstEnemyDeathSeconds = -1f);

    public sealed record CurveFindings(
        IReadOnlyList<string> Cliffs,
        IReadOnlyList<string> Chapter1Violations,
        IReadOnlyList<string> FinalChapterBandNotes,
        IReadOnlyList<string> Diagnostics,
        IReadOnlyList<string> LateSaturation);

    public sealed record SquadDeltaReport(
        string Squad,
        string Archetypes,
        string ChapterId,
        string SiteId,
        int SquadLevel,
        string ChosenNodeId,
        int ChosenBattleNodeIndex,
        float BaselineWinRate,
        bool LowSensitivity,
        IReadOnlyList<BenchmarkNodeRate> BaselineNodes,
        IReadOnlyList<DeltaArmMeasurement> ItemArms,
        IReadOnlyList<GhostArmMeasurement> GhostArms);

    public sealed record DeltaArmMeasurement(
        string ContentId,
        string Kind,
        string Squad,
        string EquippedHeroId,
        float WinRate,
        float Delta,
        string Status,
        // Q4(재비준) 4-arm 확장 — base-only(A1)는 위 WinRate/Delta, 아래는 완제품(A2)·민감도(C)·최종 verdict.
        string RepresentativeAffixId = "",
        float? CompleteWinRate = null,
        float? CompleteDelta = null,
        float? WearerSensitivityDelta = null,
        string Verdict = "");

    public sealed record GhostArmMeasurement(
        string HostNodeId,
        string GrantedSkillId,
        string Squad,
        string HeroId,
        int ClosureSize,
        float WinRate,
        float DeltaVsBaseline,
        float DeltaVsPrereqOnly,
        string Status,
        // Q4(재비준): 착용자 민감도 컨트롤 Δ(+3레벨 등가) — 미달이면 verdict=insensitive(판정 유보).
        float? WearerSensitivityDelta = null,
        string Verdict = "");

    public sealed record DeltaOutliers(
        IReadOnlyList<string> Dead,
        IReadOnlyList<string> Dominant,
        IReadOnlyList<string> Harmful,
        IReadOnlyList<string> Insensitive);
}
