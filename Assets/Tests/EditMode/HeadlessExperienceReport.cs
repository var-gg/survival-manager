using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content;
using SM.Content.Definitions;
using SM.Core;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Meta;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Playthrough;
using SM.Unity;
using SM.Unity.Narrative;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SM.Tests.EditMode;

/// <summary>
/// 헤드리스 체감 리포트 — 도트린("껍데기 전에 AI가 코드레벨로 완주하며 재미·밸런스 체감") 엔진의 첫 슬라이스.
/// 씬·GUI 없이 실 콘텐츠(RuntimeCombatContentLookup + 실 StoryDirector)로 캠페인을 플레이하며
/// (1) 발화된 narrative를 localization으로 실제 한국어 텍스트까지 해상해 트랜스크립트로 남기고,
/// (2) 실 BattleSimulator 전투의 DurationSeconds로 관전시간을,
/// (3) 읽기(char/CPS)·숙고(결정 표면 × human dwell) 모델로 플레이타임을 추정한다.
/// 산출물 = Library/SM/Reports/headless-experience-report.md (사람/AI가 읽고 재미·밸런스·길이를 평가하는 1차 재료).
///
/// <para>cycle 1 범위: 세션이 내부 발화하는 moment(SiteEntered/BattleResolved/RewardCommitted/ExtractCommitted)만
/// drain한다(authored 카탈로그의 대부분). 소비자 전용 moment(BattleStarted/RewardOpened/TownEntered)의 능동 발화는
/// context 게이팅 배선과 함께 후속.</para>
///
/// <para>BatchOnly: RuntimeCombatContentLookup 실 Resources + Content_Story StringTable이 필요하다.</para>
/// </summary>
[Category("BatchOnly")]
public sealed class HeadlessExperienceReport
{
    private const string ReportPath = "Library/SM/Reports/headless-experience-report.md";

    // StoryPresentationRunner.DefaultCharactersPerSecond(=DialogueScenePresenter 타이핑 기본값)과 동일 — 읽기속도 기준.
    private const float ReadingCharsPerSecond = 40f;

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(HeadlessExperienceReport));
    }

    // 밸런스 우회 full-campaign walkthrough — AutoResolve로 5챕터를 엔딩까지 관통해 전체 스토리 아크를 읽고
    // full 플레이타임을 추정한다(전투 승패와 분리). 전투 관전시간은 Simulate 실측 평균(96 step)으로 추정.
    [Test]
    public void AiWalksFullCampaign_ReadsAllStory_AndEstimatesFullPlaytime()
    {
        const int estimatedStepsPerBattle = 96; // Simulate 실측 평균(86~126) — 밸런스 튜닝 후 실측으로 대체.

        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile { ProfileId = "headless_walkthrough" });
        session.SetCurrentScene(SceneNames.Town);

        var text = new NarrativeTextResolver();
        var report = new StringBuilder();
        void Line(string s = "") => report.AppendLine(s);

        var beats = new List<(string source, string key, string kind, int chars, bool resolved)>();
        var siteRecords = new List<(string chapter, string site, int battles, int rewardOptions)>();

        Line("# 헤드리스 full-campaign walkthrough — AI가 읽은 전체 스토리 아크");
        Line();
        Line("밸런스 우회(AutoResolve). 캠페인을 엔딩까지 관통하며 발화 narrative를 전부 읽고 full 플레이타임을 추정한다.");
        Line($"_전투 관전시간은 Simulate 실측 평균 {estimatedStepsPerBattle} step(≈{estimatedStepsPerBattle * 0.1f:0.0}s)으로 추정._");
        Line();

        var policy = new ScriptedPlaythroughPolicy(rewardIndex: 0);
        ApplyDeployment(session, policy);
        FireAndDrain(session, NarrativeMoment.TownEntered, text, beats, report); // 초기 타운 진입 = 프롤로그/오프닝(루프 전 1회)

        var clearedSites = new List<string>();
        var totalBattles = 0;
        const int safety = 32;

        while (!session.Profile.CampaignProgress.StoryCleared && clearedSites.Count < safety)
        {
            AdvanceToNextUnclearedSite(session);
            var chapterId = session.SelectedCampaignChapterId;
            var siteId = session.SelectedCampaignSiteId;
            Line($"## {chapterId} / {siteId}");

            session.BeginNewExpedition();
            Drain(session, "SiteEntered", text, beats, report);

            var battleNodes = 0;
            while (true)
            {
                while (CampaignDefaultRouteNavigator.TryAdvanceIntermediateNonBattle(session))
                {
                }

                if (session.GetSelectedExpeditionNode()?.RequiresBattle != true)
                {
                    break;
                }

                battleNodes++;
                // AutoResolve는 sim 없이 커서만 전진 → 전투 moment를 하네스가 발화(커서가 전투 노드에 있을 때 NodeIndex 정확).
                FireAndDrain(session, NarrativeMoment.BattleStarted, text, beats, report);
                FireAndDrain(session, NarrativeMoment.BattleResolved, text, beats, report);
                session.ResolveSelectedExpeditionNode();
            }

            session.ResolveSelectedNodeToRewardSettlement();
            Drain(session, "ExtractCommitted", text, beats, report);
            FireAndDrain(session, NarrativeMoment.RewardOpened, text, beats, report); // 보상 화면 서사

            var rewardView = BuildRewardView(session, chapterId, siteId);
            if (rewardView.Options.Count > 0)
            {
                session.ApplyRewardChoice(policy.DecideReward(rewardView));
                Drain(session, "RewardCommitted", text, beats, report);
            }

            session.ReturnToTownAfterReward();
            Drain(session, "Return", text, beats, report);
            FireAndDrain(session, NarrativeMoment.TownEntered, text, beats, report); // 타운 복귀 서사(챕터말 등)

            totalBattles += battleNodes;
            siteRecords.Add((chapterId, siteId, battleNodes, rewardView.Options.Count));
            clearedSites.Add(siteId);
            Line($"- 전투 노드 {battleNodes} · 보상 {rewardView.Options.Count}안");
            Line();
        }

        var battleSeconds = totalBattles * estimatedStepsPerBattle * 0.1f;
        var readChars = beats.Where(b => b.resolved).Sum(b => b.chars);
        var readSeconds = readChars / ReadingCharsPerSecond;
        var deployDwell = 60f + (10f * session.Profile.Heroes.Count);
        var rewardDwell = siteRecords.Sum(s => 12f * s.rewardOptions);
        var siteDwell = clearedSites.Count * 8f;
        var deliberation = deployDwell + rewardDwell + siteDwell;
        var humanEstimate = battleSeconds + readSeconds + deliberation;

        Line("## ⏱️ full-campaign 플레이타임 추정");
        Line($"- 사이트 {clearedSites.Count} · 전투 {totalBattles} · narrative {beats.Count(b => b.resolved)}건 해상 / {readChars:N0}자");
        Line($"- 관전(추정) {FormatTime(battleSeconds)} + 읽기 {FormatTime(readSeconds)} + 숙고 {FormatTime(deliberation)}");
        Line($"- **추정 human full-campaign ≈ {FormatTime(humanEstimate)}** (밸런스 무관 콘텐츠 길이)");
        Line();
        Line($"_생성: {(session.Profile.CampaignProgress.StoryCleared ? "엔딩 도달" : "미완주")} · 클리어 챕터 {session.Profile.CampaignProgress.ClearedChapterIds.Count}._");

        var fullPath = Path.GetFullPath("Library/SM/Reports/headless-walkthrough-report.md");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, report.ToString());
        TestContext.WriteLine(report.ToString());

        Assert.That(clearedSites, Is.Not.Empty, "최소 1사이트 관통.");
        Assert.That(session.Profile.CampaignProgress.StoryCleared, Is.True,
            "AutoResolve walkthrough가 캠페인을 엔딩까지 관통했다(전체 스토리 아크 도달).");
        // 회귀 게이트: 보스전 서사가 실제로 발화됐는지 — NodeIs off-by-one(4→3) 재발 시 이 단언이 깨진다.
        Assert.That(beats.Any(b => b.key.Contains("boss_bark", StringComparison.Ordinal)), Is.True,
            "보스전 진입(boss_bark)이 발화됐다 — NodeIs off-by-one 회귀 차단.");
        Assert.That(beats.Any(b => b.key.Contains("boss_defeat", StringComparison.Ordinal)), Is.True,
            "보스 처치(boss_defeat)가 발화됐다 — NodeIs off-by-one 회귀 차단.");
    }

    [Test]
    public void AiPlaysCampaign_SurfacesStory_AndEstimatesPlaytime()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup); // 단일 인자 = LoadFromResources 실 narrative director
        session.BindProfile(new SaveProfile { ProfileId = "headless_experience" }); // 기본 분대 시드
        session.SetCurrentScene(SceneNames.Town);

        var text = new NarrativeTextResolver();
        var report = new StringBuilder();
        void Line(string s = "") => report.AppendLine(s);

        var beats = new List<(string source, string key, string kind, int chars, bool resolved)>();
        var battles = new List<(string node, bool win, int steps, float seconds)>();
        var rewards = new List<(string site, int options, int pick, string kind)>();
        var payoffs = new List<BattleFormationPayoffSummary>(); // 전투마다 깊이 발현(시너지/콤보/진형) 요약

        Line("# 헤드리스 체감 리포트 — AI가 코드로 완주한 게임");
        Line();
        Line("씬·VisualElement·MonoBehaviour 0. 실 콘텐츠 + 실 StoryDirector + 실 BattleSimulator로 처음부터 종료까지 플레이하며 관찰한 기록.");
        Line();

        // ── 편성 (정책 결정) ──
        var policy = new ScriptedPlaythroughPolicy(rewardIndex: 0);
        ApplyDeployment(session, policy);
        FireAndDrain(session, NarrativeMoment.TownEntered, text, beats, report); // 초기 타운 진입 = 프롤로그/오프닝(루프 전 1회)
        var roster = session.Profile.Heroes;
        Line("## 편성");
        Line($"- 로스터 {roster.Count}인: {string.Join(", ", roster.Select(h => $"{h.ClassId}/{h.ArchetypeId}"))}");
        Line($"- 배치 앵커 {session.DeploymentAnchors.Count()}칸");
        Line();

        // ── 캠페인 순회 ──
        Line("## 원정 — 실 전투로 종료까지");
        Line();
        var clearedSites = new List<string>();
        string? defeatedSite = null;
        const int safety = 16;

        while (!session.Profile.CampaignProgress.StoryCleared && clearedSites.Count < safety)
        {
            AdvanceToNextUnclearedSite(session);
            var chapterId = session.SelectedCampaignChapterId;
            var siteId = session.SelectedCampaignSiteId;
            Line($"### {chapterId} / {siteId}");

            session.BeginNewExpedition(); // SiteEntered 내부 발화
            Drain(session, "SiteEntered", text, beats, report);

            var defeated = false;
            var battleIdx = 0;
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
                battleIdx++;
                FireAndDrain(session, NarrativeMoment.BattleStarted, text, beats, report); // 전투 진입 발화(boss-bark 등, 씬 controller 등가)
                if (!session.TryResolveSelectedBattleNodeViaSimulation(out var br, out var err))
                {
                    throw new InvalidOperationException($"전투 sim 실패({node.Id}): {err}");
                }

                var win = br.Winner == TeamSide.Ally;
                battles.Add((node.Id, win, br.StepCount, br.DurationSeconds));
                Line($"- ⚔️ 전투 {battleIdx} `{node.Id}` — {(win ? "승" : "패")} ({br.StepCount} step · {br.DurationSeconds:0.0}s)");
                // 깊이가 이 전투에서 실제로 발화했는지 표면화 — 데이터엔 있으나 리포트가 안 찍던 신호.
                var payoff = session.LastBattleFormationPayoff;
                payoffs.Add(payoff);
                if (payoff.HasData)
                {
                    Line($"  · MVP {payoff.MvpUnitName} (피해 {payoff.MvpDamage:0} · 처치 {payoff.MvpKills})"
                        + $" | 진형 측면{payoff.FlankStrikes}·후방{payoff.RearStrikes}·차단{payoff.ScreenAbsorbs}·구출{payoff.SaveMoments}·후열격파{payoff.BacklineDiveKills}"
                        + $" | 발현 시너지{payoff.SynergyActivations}·콤보{payoff.ComboConsumes}·증강{payoff.TriggeredEffects}");
                }

                Drain(session, "BattleResolved", text, beats, report); // BattleResolved 내부 발화

                if (!win)
                {
                    session.AbandonExpeditionRun();
                    defeated = true;
                    defeatedSite = siteId;
                    break;
                }

                session.ResolveSelectedExpeditionNode();
            }

            if (defeated)
            {
                Line();
                Line($"### 💀 {siteId} 에서 패배 — 캠페인 중단");
                Line();
                break;
            }

            session.ResolveSelectedNodeToRewardSettlement();
            Drain(session, "ExtractCommitted", text, beats, report); // ExtractCommitted 내부 발화
            FireAndDrain(session, NarrativeMoment.RewardOpened, text, beats, report); // 보상 화면 서사

            var rewardView = BuildRewardView(session, chapterId, siteId);
            var pick = policy.DecideReward(rewardView);
            var kind = rewardView.Options[pick].Kind.ToString();
            session.ApplyRewardChoice(pick);
            Drain(session, "RewardCommitted", text, beats, report); // RewardCommitted 내부 발화
            rewards.Add((siteId, rewardView.Options.Count, pick, kind));
            Line($"- 🎁 보상 {rewardView.Options.Count}안 중 {pick + 1}번 선택 ({kind})");

            session.ReturnToTownAfterReward();
            Drain(session, "Return", text, beats, report);
            FireAndDrain(session, NarrativeMoment.TownEntered, text, beats, report); // 타운 복귀 서사(챕터말 등)

            clearedSites.Add(siteId);
            Line();
        }

        // ── 플레이타임 추정 ──
        var battleSeconds = battles.Sum(b => b.seconds);
        var readChars = beats.Where(b => b.resolved).Sum(b => b.chars);
        var readSeconds = readChars / ReadingCharsPerSecond;
        // 숙고(human dwell 추정 — AI는 사실상 0이라 이 성분이 AI floor와 human을 가른다):
        var deployDwell = 60f + (10f * roster.Count);            // 편성 1회: 로스터 스터디 + 배치
        var rewardDwell = rewards.Sum(r => 12f * r.options);     // 보상: 선택지 폭 비례
        var siteDwell = clearedSites.Count * 8f;                 // 사이트/atlas 이동
        var deliberation = deployDwell + rewardDwell + siteDwell;
        var aiFloor = battleSeconds;                             // AI 실측: 실시간 성분은 전투 관전뿐
        var humanEstimate = battleSeconds + readSeconds + deliberation;

        Line("## ⏱️ 플레이타임 추정");
        Line($"- 관전(전투): **{battleSeconds:0.0}s** — 실측 sim, 1× 관람이라 AI≈사람");
        Line($"- 읽기(내러티브): {beats.Count(b => b.resolved)}건 해상 / {readChars:N0}자 → **{readSeconds:0.0}s** (@{ReadingCharsPerSecond:0} CPS)");
        Line($"- 숙고(결정): 편성 {deployDwell:0}s + 보상 {rewardDwell:0}s + 이동 {siteDwell:0}s = **{deliberation:0}s** (human dwell 가정, AI≈0)");
        Line($"- **AI 기계 최소치 ≈ {FormatTime(aiFloor)}** (전투 관전만; 콘텐츠 두께의 하한)");
        Line($"- **추정 human 플레이타임 ≈ {FormatTime(humanEstimate)}** (관전+읽기+숙고; 볼륨 척도)");
        Line();

        // ── 밸런스/재미 신호 ──
        var wins = battles.Count(b => b.win);
        var resolvedBeats = beats.Count(b => b.resolved);
        Line("## 📊 밸런스 / 재미 신호");
        Line($"- 전투: {wins}/{battles.Count} 승" + (defeatedSite != null ? $" · **첫 패배 {defeatedSite}**(밸런스 벽)" : " · 완주"));
        Line($"- narrative surface: {beats.Count}건 발화 / {resolvedBeats}건 텍스트 해상" + (beats.Count == 0 ? " ⚠️ 0건 — 배선/조건 게이트 확인 필요" : ""));
        Line($"- 결정 표면: 편성 1(로스터 {roster.Count}×앵커 {session.DeploymentAnchors.Count()}) · 보상 {rewards.Count}회(선택지 {string.Join("/", rewards.Select(r => r.options))})");
        if (battles.Count > 0)
        {
            Line($"- 전투 길이 분포: {battles.Min(b => b.steps)}~{battles.Max(b => b.steps)} step (평균 {battles.Average(b => b.steps):0}) — 너무 짧으면 얄팍/너무 길면 지루");
        }
        var payoffData = payoffs.Where(p => p.HasData).ToList();
        if (payoffData.Count > 0)
        {
            Line($"- 진형 전과 총계: 측면 {payoffData.Sum(p => p.FlankStrikes)}·후방 {payoffData.Sum(p => p.RearStrikes)}·차단 {payoffData.Sum(p => p.ScreenAbsorbs)}·구출 {payoffData.Sum(p => p.SaveMoments)}·후열격파 {payoffData.Sum(p => p.BacklineDiveKills)}");
            Line($"- **깊이 발현 총계: 시너지 발동 {payoffData.Sum(p => p.SynergyActivations)}·콤보 작렬 {payoffData.Sum(p => p.ComboConsumes)}·증강/규칙 {payoffData.Sum(p => p.TriggeredEffects)}** — 0에 가까우면 깊이가 실 콘텐츠에서 안 터지는 것(튜닝 포인트)");
        }
        Line();
        Line($"_생성: 실 콘텐츠 헤드리스, 사이트 {clearedSites.Count}클리어, {(session.Profile.CampaignProgress.StoryCleared ? "엔딩 도달" : "미완주")}._");

        var fullPath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, report.ToString());
        TestContext.WriteLine(report.ToString());

        // ── 게이트(비공허) ──
        Assert.That(battles, Is.Not.Empty, "실 전투가 최소 1회 정산됐다.");
        Assert.That(battles.All(b => b.steps > 0), Is.True, "모든 전투가 실 BattleSimulator tick을 돌렸다.");
        Assert.That(session.Profile.CampaignProgress.StoryCleared || defeatedSite != null, Is.True,
            "캠페인이 실 결과로 종료 — 완주 또는 실 패배.");
    }

    // 소비자 전용 moment(BattleStarted 등)를 하네스가 능동 발화 + drain — 씬 controller 발화의 헤드리스 등가.
    // BuildNodeContext가 현재 노드 인덱스를 실어 조건(NodeIs) 게이트가 맞물린다.
    private static void FireAndDrain(
        GameSessionState session,
        NarrativeMoment moment,
        NarrativeTextResolver text,
        List<(string source, string key, string kind, int chars, bool resolved)> beats,
        StringBuilder report)
    {
        session.AdvanceNarrative(moment, NarrativeMomentResolver.BuildNodeContext(session));
        Drain(session, moment.ToString(), text, beats, report);
    }

    // 세션이 내부 발화한 presentation을 전부 drain하며 텍스트 해상 → 리포트/집계에 적재.
    private static void Drain(
        GameSessionState session,
        string source,
        NarrativeTextResolver text,
        List<(string source, string key, string kind, int chars, bool resolved)> beats,
        StringBuilder report)
    {
        while (session.TryDequeueNarrativePresentation(out var request) && request != null)
        {
            var (lines, chars, resolved) = text.Resolve(request);
            beats.Add((source, request.PresentationKey, request.PresentationKind.ToString(), chars, resolved));
            report.AppendLine($"  - 📖 _{source}_ · `{request.PresentationKey}` ({request.PresentationKind})");
            foreach (var line in lines)
            {
                report.AppendLine($"    > {line}");
            }
        }
    }

    private static void ApplyDeployment(GameSessionState session, IPlaythroughDecisionPolicy policy)
    {
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        var view = new PlaythroughDeploymentView(
            session.Profile.Heroes
                .Select(hero => new PlaythroughHero(hero.HeroId, hero.ClassId, hero.ArchetypeId, 1))
                .ToList(),
            session.DeploymentAnchors.ToList());

        foreach (var placement in policy.DecideDeployment(view))
        {
            session.AssignHeroToAnchor(placement.Anchor, placement.HeroId);
        }
    }

    private static PlaythroughRewardView BuildRewardView(GameSessionState session, string chapterId, string siteId)
    {
        var options = session.PendingRewardChoices
            .Select((choice, index) => new PlaythroughRewardOption(
                index, choice.Kind, choice.PayloadId, choice.GoldAmount, choice.EchoAmount))
            .ToList();
        return new PlaythroughRewardView(chapterId, siteId, options);
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

    private static string FormatTime(float seconds)
    {
        if (seconds < 90f)
        {
            return $"{seconds:0}초";
        }

        var minutes = seconds / 60f;
        return $"{minutes:0.0}분";
    }

    /// <summary>
    /// StoryPresentationRequest(키만 든다)를 실제 표시 텍스트로 해상 — StoryPresentationRunner의 런타임 경로를
    /// 헤드리스(에디터 동기)로 복제한다: dialogue sequence Resources 인덱스 + Content_Story ko StringTable 직접 조회.
    /// batchmode 로컬라이제이션 불확실성 대비 best-effort(해상 실패 시 키 노출 + resolved=false).
    /// </summary>
    private sealed class NarrativeTextResolver
    {
        private const string DialogueSequencesResourcesPath = "_Game/Content/Definitions/DialogueSequences";

        private readonly Dictionary<string, DialogueSequenceDefinition> _sequencesById;
        private StringTable? _storyTable;
        private bool _tableResolved;

        public NarrativeTextResolver()
        {
            _sequencesById = new Dictionary<string, DialogueSequenceDefinition>(StringComparer.Ordinal);
            foreach (var sequence in Resources.LoadAll<DialogueSequenceDefinition>(DialogueSequencesResourcesPath))
            {
                if (sequence == null || string.IsNullOrWhiteSpace(sequence.Id) || _sequencesById.ContainsKey(sequence.Id))
                {
                    continue;
                }

                _sequencesById.Add(sequence.Id, sequence);
            }
        }

        public (List<string> lines, int chars, bool resolved) Resolve(StoryPresentationRequest request)
        {
            var lines = new List<string>();
            var resolved = false;

            if (request.PresentationKind is StoryPresentationKind.DialogueScene or StoryPresentationKind.DialogueOverlay)
            {
                var sequence = FindSequence(request.PresentationKey);
                if (sequence != null)
                {
                    foreach (var line in sequence.Lines ?? Array.Empty<DialogueLineDefinition>())
                    {
                        if (line == null || string.IsNullOrWhiteSpace(line.TextKey))
                        {
                            continue;
                        }

                        var body = Localize(line.TextKey);
                        if (string.IsNullOrEmpty(body))
                        {
                            continue;
                        }

                        resolved = true;
                        var speaker = string.IsNullOrWhiteSpace(line.SpeakerId) || IsNarrator(line.SpeakerId)
                            ? string.Empty
                            : $"{line.SpeakerId}: ";
                        lines.Add(speaker + body);
                    }
                }
            }
            else
            {
                var body = Localize($"loc.story.presentation.{request.PresentationKey}.body");
                if (!string.IsNullOrEmpty(body))
                {
                    resolved = true;
                    lines.Add(body);
                }
            }

            if (lines.Count == 0)
            {
                lines.Add($"[미해상: {request.PresentationKey}]");
            }

            return (lines, lines.Sum(l => l.Length), resolved);
        }

        private DialogueSequenceDefinition? FindSequence(string key)
        {
            if (_sequencesById.TryGetValue(key, out var direct))
            {
                return direct;
            }

            if (key.StartsWith("dialogue_scene_", StringComparison.Ordinal))
            {
                key = "dialogue_seq_" + key["dialogue_scene_".Length..];
            }
            else if (key.StartsWith("dialogue_overlay_", StringComparison.Ordinal))
            {
                key = "dialogue_seq_" + key["dialogue_overlay_".Length..];
            }

            return _sequencesById.TryGetValue(key, out var normalized) ? normalized : null;
        }

        private string Localize(string key)
        {
            var table = StoryTable();
            if (table == null)
            {
                return string.Empty;
            }

            var entry = table.GetEntry(key);
            return entry != null && !string.IsNullOrEmpty(entry.Value) ? entry.Value : string.Empty;
        }

        private StringTable? StoryTable()
        {
            if (_tableResolved)
            {
                return _storyTable;
            }

            _tableResolved = true;
            try
            {
                LocalizationSettings.InitializationOperation.WaitForCompletion();
                var collection = LocalizationEditorSettings.GetStringTableCollection(ContentLocalizationTables.Story);
                _storyTable = collection?.GetTable(new LocaleIdentifier("ko")) as StringTable;
            }
            catch (Exception)
            {
                _storyTable = null;
            }

            return _storyTable;
        }

        private static bool IsNarrator(string speakerId)
            => string.Equals(speakerId, "Narrator", StringComparison.OrdinalIgnoreCase);
    }
}
