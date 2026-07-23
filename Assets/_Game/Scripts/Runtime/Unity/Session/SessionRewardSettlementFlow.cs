using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core;
using SM.Core.Contracts;
using SM.Core.Results;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity.Sandbox;
using Unity.Profiling;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    internal sealed class SessionRewardSettlementFlow
    {
        private readonly GameSessionState _session;

        internal SessionRewardSettlementFlow(GameSessionState session)
        {
            _session = session;
        }

        internal void RecordBattleAudit(BattleReplayBundle replay)
        {
            _session.Profile.MatchHeaders.Add(new MatchRecordHeader
            {
                MatchId = replay.Header.MatchId,
                RunId = _session.ActiveRun?.RunId ?? string.Empty,
                ContentVersion = replay.Header.ContentVersion,
                SimVersion = replay.Header.SimVersion,
                Seed = replay.Header.Seed,
                PlayerSnapshotHash = replay.Header.PlayerSnapshotHash,
                EnemySnapshotHash = replay.Header.EnemySnapshotHash,
                StartedAtUtc = replay.Header.StartedAtUtc,
                CompletedAtUtc = replay.Header.CompletedAtUtc,
                Winner = replay.Header.Winner.ToString(),
                FinalStateHash = replay.Header.FinalStateHash,
            });
            _session.Profile.MatchBlobs.Add(new MatchRecordBlob
            {
                MatchId = replay.Header.MatchId,
                CompileVersion = replay.Input.CompileVersion,
                CompileHash = replay.Input.CompileHash,
                InputDigest = $"{replay.Input.TeamPosture}|{replay.Input.Allies.Count}|{replay.Input.Enemies.Count}",
                BattleSummaryDigest = replay.BattleSummary == null
                    ? string.Empty
                    : $"{replay.BattleSummary.WinnerSideIndex}|{replay.BattleSummary.BattleDurationSeconds:0.###}|{replay.BattleSummary.UnexplainedDamageRatio:0.###}|{replay.BattleSummary.MajorEventCollisionRate:0.###}",
                ReadabilityDigest = replay.Readability == null
                    ? string.Empty
                    : $"{replay.Readability.UnexplainedDamageRatio:0.###}|{replay.Readability.UnexplainedHealingRatio:0.###}|{string.Join(",", replay.Readability.Violations.Select(violation => violation.ToString()))}",
                EventStream = replay.EventStream.Select(@event =>
                    $"{@event.StepIndex}|{@event.ActorId.Value}|{@event.ActionType}|{@event.LogCode}|{@event.TargetId?.Value}|{@event.Value:0.###}|{@event.EventKind}|{@event.PayloadId}|{@event.SecondaryValue:0.###}|{@event.Note}").ToList(),
                KeyframeDigests = replay.Keyframes.Select(frame =>
                    $"{frame.StepIndex}|{frame.TimeSeconds:0.###}|{frame.StateHash}").ToList(),
                TelemetryEvents = replay.TelemetryEvents?
                    .Select(record => $"{record.Domain}|{record.EventKind}|{record.TimeSeconds:0.###}|{record.Explain?.SourceContentId}|{record.Explain?.SourceDisplayName}|{record.StringValueA}|{record.ValueA:0.###}|{record.IntValueA}")
                    .ToList()
                    ?? new List<string>(),
                ArtifactPaths = new List<string>
                {
                    replay.BattleSummary != null ? "Logs/loop-d-balance" : string.Empty,
                    replay.Readability != null ? "Logs/loop-d-balance/readability_watchlist.json" : string.Empty,
                }.Where(path => !string.IsNullOrWhiteSpace(path)).ToList(),
                // ADR-0028 #provenance: 이 전투의 정치 조건 출처(세력·채널·사유)를 audit에 영속 — hash 밖, replay 재검증 입력.
                PoliticalConditions = PoliticalConditionAudit.EncodeAll(_session.ActiveBattlePoliticalConditions).ToList(),
            });

            if (_session.ActiveRun != null)
            {
                if (!string.IsNullOrWhiteSpace(_session.ActiveRun.Overlay.LastCompileHash)
                    && !string.Equals(_session.ActiveRun.Overlay.LastCompileHash, replay.Input.CompileHash, StringComparison.Ordinal))
                {
                    _session.Profile.SuspicionFlags.Add(new SuspicionFlagRecord
                    {
                        FlagId = Guid.NewGuid().ToString("N"),
                        RunId = _session.ActiveRun.RunId,
                        MatchId = replay.Header.MatchId,
                        Reason = "compile_hash_mismatch",
                        ExpectedHash = _session.ActiveRun.Overlay.LastCompileHash,
                        ObservedHash = replay.Input.CompileHash,
                        CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                    });
                }

                _session.ActiveRun = RunStateService.CompleteBattle(_session.ActiveRun, replay.Header.MatchId);
                _session.SyncActiveRunRecord();
            }
        }

        internal void SetLastBattleResult(bool victory, string summary)
        {
            _session.LastBattleVictory = victory;
            _session.LastBattleSummary = SessionTextToken.Plain(summary);
            _session.LastRewardApplicationSummary = SessionTextToken.Empty;
            _session.LastPermanentUnlockSummary = SessionTextToken.Empty;
            _session._hasPendingRewardSettlement = true;
            if (_session.ActiveRun != null)
            {
                _session.ActiveRun = _session.ActiveRun with { LastSettlementWasVictory = victory };
            }

            _session.EnsureRewardChoices(reset: true);
            _session.SyncActiveRunIfPresent();
        }

        internal void MarkBattleResolved(bool victory, int stepCount, int eventCount, IReadOnlyList<BattleUnitReadModel>? finalUnits = null)
        {
            var resolvedNode = _session.GetSelectedExpeditionNode() ?? _session.GetCurrentExpeditionNode();
            var shouldCreateRewardSettlement = !_session.IsDirectCombatSandboxLane;
            _session.LastBattleVictory = victory;
            _session.LastRewardApplicationSummary = SessionTextToken.Empty;
            _session.LastPermanentUnlockSummary = SessionTextToken.Empty;
            _session._lastAutomaticLootBundle = null;
            _session._hasPendingRewardSettlement = shouldCreateRewardSettlement;

            if (finalUnits != null
                && !_session.IsDirectCombatSandboxLane
                && !_session.IsQuickBattleSmokeActive)
            {
                ApplyWarWoundAftermath(finalUnits, victory);
            }

            // wave-33-progression: combat result → HeroInstanceRecord HP + HeroProgressionRecord Exp 갱신.
            // finalUnits == null이면 (sandbox/balance runner처럼 raw 결과를 안 전달하는 path) 건너뜀 — 기존 호환.
            if (finalUnits != null && !_session.IsDirectCombatSandboxLane)
            {
                ApplyHeroBattleAftermath(
                    finalUnits,
                    victory,
                    _session._campaignRecoveryFlow.ShouldGrantVictoryExperience);
            }

            // ludonarrative 루프 P1a: 전투 결과 → 캠페인 영구 dossier 기록.
            // sandbox/quick-battle smoke lane은 캠페인 기록이 아니므로 제외.
            if (finalUnits != null && !_session.IsDirectCombatSandboxLane && !_session.IsQuickBattleSmokeActive)
            {
                WriteDossierEntry(finalUnits, victory, stepCount, resolvedNode?.Id ?? string.Empty);
            }

            if (resolvedNode != null && !_session.IsQuickBattleSmokeActive)
            {
                _session.CurrentExpeditionNodeIndex = resolvedNode.Index;
                _session.SelectedExpeditionNodeIndex = resolvedNode.Index;
                _session.EnsureCampaignSelection();
                _session.EnsureActiveRunNodeState(resolvedNode);
            }

            if (victory && _session.ActiveRun != null && !string.IsNullOrWhiteSpace(_session.ActiveRun.Overlay.RewardSourceId))
            {
                _session.TryApplyAutomaticLoot();
            }

            if (victory && resolvedNode != null && !_session.IsQuickBattleSmokeActive)
            {
                _session.MarkNodeResolved(resolvedNode);
                _session.LastExpeditionEffectMessage = _session.ApplyExpeditionNodeEffect(resolvedNode);
                _session.UpdateCampaignProgressForResolvedNode(resolvedNode);
                _session.LastBattleSummary = _session.LastExpeditionEffectMessage.HasValue
                    ? new SessionTextToken(
                        GameLocalizationTables.UIReward,
                        "ui.reward.battle_summary.route_effect",
                        "{0} / {1} steps / {2} events\nRoute: {3}\nNode Effect: {4}",
                        SessionTextArg.Localized(
                            GameLocalizationTables.UIReward,
                            victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                            victory ? "Victory" : "Defeat"),
                        SessionTextArg.Number(stepCount),
                        SessionTextArg.Number(eventCount),
                        SessionTextArg.Localized(GameLocalizationTables.UIExpedition, resolvedNode.LabelKey, resolvedNode.Id),
                        SessionTextArg.Token(_session.LastExpeditionEffectMessage))
                    : new SessionTextToken(
                        GameLocalizationTables.UIReward,
                        "ui.reward.battle_summary.route",
                        "{0} / {1} steps / {2} events\nRoute: {3}",
                        SessionTextArg.Localized(
                            GameLocalizationTables.UIReward,
                            victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                            victory ? "Victory" : "Defeat"),
                        SessionTextArg.Number(stepCount),
                        SessionTextArg.Number(eventCount),
                        SessionTextArg.Localized(GameLocalizationTables.UIExpedition, resolvedNode.LabelKey, resolvedNode.Id));
                _session.RestoreResolvedProgressMarkers(includeCurrentNode: true);
            }
            else
            {
                _session.LastBattleSummary = BuildBattleSummaryToken(victory, stepCount, eventCount);
                if (!_session.IsQuickBattleSmokeActive)
                {
                    _session.HasActiveExpeditionRun = false;
                }

                if (resolvedNode != null && !_session.IsQuickBattleSmokeActive)
                {
                    _session.RestoreResolvedProgressMarkers(includeCurrentNode: false);
                }
            }

            if (_session.ActiveRun != null)
            {
                // task-reward-settlement-commit-v1 acceptance #1: pending reward에 deterministic
                // RewardCommitId를 stamp한다. battleContextHash + outcome으로 dedup key 생성.
                // shouldCreateRewardSettlement=false인 sandbox/quick-battle lane은 commit id를 비운다.
                var battleContextHash = _session.ActiveRun.Overlay.BattleContextHash;
                var rewardCommitId = shouldCreateRewardSettlement && !string.IsNullOrWhiteSpace(battleContextHash)
                    ? !victory && _session._campaignRecoveryFlow.HasPersistentDefeatConsolationContext
                        ? _session._campaignRecoveryFlow.BuildDefeatConsolationCommitId(battleContextHash)
                        : RewardCommitIdService.Compute(battleContextHash, victory ? "victory" : "defeat")
                    : string.Empty;

                _session.ActiveRun = _session.ActiveRun with
                {
                    LastSettlementWasVictory = victory,
                    Overlay = _session.ActiveRun.Overlay with
                    {
                        PendingRewardIds = shouldCreateRewardSettlement ? _session.ActiveRun.Overlay.PendingRewardIds : Array.Empty<string>(),
                        RewardSourceId = shouldCreateRewardSettlement ? _session.ActiveRun.Overlay.RewardSourceId : string.Empty,
                        RewardCommitId = rewardCommitId,
                    }
                };
            }

            if (shouldCreateRewardSettlement)
            {
                _session.EnsureRewardChoices(reset: true);
            }
            else
            {
                _session._pendingRewardChoices.Clear();
            }

            _session.SyncActiveRunIfPresent();
        }

        // wave-33-progression: combat result 직후 ally hero 4명의 HP/EXP를 HeroInstanceRecord +
        // HeroProgressionRecord에 반영. summon/소환물(EntityKind != RosterUnit)은 제외.
        // - HP: MaxHealth/CurrentHealth → int 반올림. 사망 unit은 CurrentHp = 0.
        // - EXP: victory 시 squad hero 각 +50 base XP, curve 임계치 넘으면 lv-up + 잔여 exp carry.
        // 진짜 XP 차등(보스/일반, K/D 가중)은 별도 balance sprint — 본 turn은 baseline.
        private const int BattleXpGainPerVictory = 50;

        // BattleFactory가 ally unit EntityId를 "ally_{index}_{heroId}"로 만든다(BattleFactory.cs:31 — 접두사는
        // telemetry/replay에서 ally/enemy 구분 + 인덱스 유일성용). settlement은 roster hero id로 매핑해야 하므로
        // 그 접두사를 벗겨 원본 hero id를 복원한다. 이게 빠져 있어 finalUnits→hero 매핑이 항상 실패,
        // 전투 후 HP/EXP가 한 번도 반영되지 않았다(씬·헤드리스 공통). 접두사가 없으면 id를 그대로 반환.
        private static string ResolveRosterHeroId(string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || !unitId.StartsWith("ally_", StringComparison.Ordinal))
            {
                return unitId;
            }

            var parts = unitId.Split('_', 3);
            return parts.Length == 3 ? parts[2] : unitId;
        }

        private void ApplyWarWoundAftermath(IReadOnlyList<BattleUnitReadModel> finalUnits, bool victory)
        {
            if (_session.ActiveRun == null
                || !_session._sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
                || snapshot.WarWound == null)
            {
                return;
            }

            var candidates = finalUnits
                .Where(unit => unit.Side == TeamSide.Ally && unit.EntityKind == CombatEntityKind.RosterUnit)
                .Select(unit => new WarWoundCandidate(
                    ResolveRosterHeroId(unit.Id),
                    unit.CurrentHealth,
                    unit.MaxHealth))
                .ToArray();
            var resolution = WarWoundResolutionService.Resolve(
                _session.ActiveRun,
                victory,
                candidates,
                snapshot.WarWound);
            _session.ActiveRun = resolution.UpdatedRun;
        }

        private void ApplyHeroBattleAftermath(
            IReadOnlyList<BattleUnitReadModel> finalUnits,
            bool victory,
            bool grantVictoryExperience)
        {
            var heroById = _session.Profile.Heroes
                .GroupBy(h => h.HeroId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var progressionById = _session.Profile.HeroProgressions
                .GroupBy(p => p.HeroId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            foreach (var unit in finalUnits)
            {
                if (unit.Side != TeamSide.Ally) continue;
                if (unit.EntityKind != CombatEntityKind.RosterUnit) continue;
                if (!heroById.TryGetValue(ResolveRosterHeroId(unit.Id), out var hero)) continue;

                hero.MaxHp = (int)Math.Max(1, Math.Round(unit.MaxHealth));
                hero.CurrentHp = (int)Math.Max(0, Math.Round(unit.CurrentHealth));

                if (!victory || !grantVictoryExperience) continue;

                if (!progressionById.TryGetValue(hero.HeroId, out var progression))
                {
                    progression = new HeroProgressionRecord { HeroId = hero.HeroId, Level = 1 };
                    _session.Profile.HeroProgressions.Add(progression);
                    progressionById[hero.HeroId] = progression;
                }

                progression.Experience += BattleXpGainPerVictory;
                while (progression.Experience >= HeroProgressionCurve.ExperienceToNextLevel(progression.Level))
                {
                    progression.Experience -= HeroProgressionCurve.ExperienceToNextLevel(progression.Level);
                    progression.Level += 1;
                }
            }
        }

        // ludonarrative 루프 P1a: finalUnits(이미 전달됨)에서 ally squad 생존을 집계해
        // DossierOutcome으로 분류하고 SaveProfile.Dossier에 영구 기록한다.
        // 판정 로직은 SM.Meta.DossierOutcomeClassifier(순수)가 소유 — 여기서는 집계 + 영속화만.
        // P2a: 같은 hook에서 출격 전 서약(Warrant)도 판정한다 — overlay의 PledgedWarrantId를
        // WarrantSpec으로 해석해 WarrantJudge(순수)로 kept/broken을 가린다. combat은 불변. ADR-0027.
        private void WriteDossierEntry(IReadOnlyList<BattleUnitReadModel> finalUnits, bool victory, int stepCount, string nodeId)
        {
            var totalAllyCount = 0;
            var survivorAllyCount = 0;
            var fallenAllyIds = new List<string>();
            foreach (var unit in finalUnits)
            {
                if (unit.Side != TeamSide.Ally) continue;
                if (unit.EntityKind != CombatEntityKind.RosterUnit) continue;

                totalAllyCount += 1;
                if (unit.CurrentHealth > 0)
                {
                    survivorAllyCount += 1;
                }
                else
                {
                    fallenAllyIds.Add(unit.Id);
                }
            }

            var outcome = DossierOutcomeClassifier.Classify(victory, survivorAllyCount, totalAllyCount);
            var chapterId = _session.ActiveRun?.Overlay.ChapterId ?? _session.Profile.CampaignProgress.SelectedChapterId;

            // P2a: 출격 전 서약(overlay에 실려 운반)을 전투 사실로 판정. 미서약이면 spec=null → NotApplicable.
            // GPT Pro 검수(§5.1): scalar 나열 대신 BattleFactSet으로 조립해 judge에 넘긴다 — P3(민간인/증거/비살상)에서
            // 새 전투 사실이 붙어도 judge signature가 보존된다. combat은 BattleFactSet을 모른다(여기서 BattleResult 파생).
            var pledgedWarrantId = _session.ActiveRun?.Overlay.PledgedWarrantId ?? string.Empty;
            var warrantSpec = WarrantCatalog.TryResolve(pledgedWarrantId, out var resolvedWarrant) ? resolvedWarrant : null;
            var battleFacts = new BattleFactSet(victory, stepCount, survivorAllyCount, totalAllyCount);
            // encounter-relative turn 임계(encounterParTurn - pressureDelta)는 P2b/content가 채운다. 슬라이스 1은 spec placeholder(0 → fallback).
            var warrantJudgment = WarrantJudge.Judge(warrantSpec, battleFacts, new EncounterContext(ResolvedSwiftTurnLimit: 0));

            // ADR-0028 #1 가독성 + #5 OfferSet + #b site offer: 정치 정산을 player-readable report로 한 번에 조립.
            // 적용·Dossier 기록·화면 노출이 모두 이 한 묶음을 쓴다. offer 소스 = 이 site의 정치 맥락(SiteWarrantOfferResolver,
            // 미등록 site는 전역 정치 카탈로그 fallback) — 거절 면이 "이 장소에서 누구를 거절했나"가 된다.
            var settlementSiteId = _session.ActiveRun?.Overlay.SiteId ?? _session.Profile.CampaignProgress.SelectedSiteId;
            var settlementReport = warrantSpec != null
                ? PoliticalSettlementReporter.Build(
                    warrantJudgment.Outcome,
                    warrantSpec.IssuerFactionId,
                    warrantSpec.OpposedFactionId,
                    SiteWarrantOfferResolver.Resolve(settlementSiteId).OfferedWarrantIds,
                    pledgedWarrantId)
                : PoliticalSettlementReport.Empty;

            _session.Profile.Dossier.Add(new DossierEntryRecord
            {
                EntryId = Guid.NewGuid().ToString("N"),
                RunId = _session.ActiveRun?.RunId ?? string.Empty,
                ChapterId = chapterId,
                SiteId = settlementSiteId,
                NodeId = nodeId,
                Result = victory ? "victory" : "defeat",
                Outcome = DossierOutcomeClassifier.ToToken(outcome),
                SurvivorAllyCount = survivorAllyCount,
                TotalAllyCount = totalAllyCount,
                FallenAllyIds = fallenAllyIds,
                WarrantId = warrantSpec?.Id ?? string.Empty,
                WarrantOutcome = WarrantJudgment.OutcomeToken(warrantJudgment.Outcome),
                WarrantFailureReason = WarrantJudgment.FailureReasonToken(warrantJudgment.FailureReason),
                WarrantSeverity = WarrantJudgment.SeverityToken(warrantJudgment.Severity),
                WarrantObservedTurnCount = warrantJudgment.ObservedTurnCount,
                WarrantResolvedTurnLimit = warrantJudgment.ResolvedTurnLimit,
                IssuerFactionId = warrantSpec?.IssuerFactionId ?? string.Empty,
                OpposedFactionId = warrantSpec?.OpposedFactionId ?? string.Empty,
                RejectedFactionIds = settlementReport.Lines
                    .Where(line => line.Reason == PoliticalSettlementReason.RejectedOffer)
                    .Select(line => line.FactionId).ToList(),
                // ADR-0028 incident-centric: 세력별 구조적 효과(세력·신뢰 delta·사유)를 incident에 박는다 — id projection의 source.
                PoliticalEffects = settlementReport.Lines
                    .Select(line => new DossierPoliticalEffectRecord
                    {
                        FactionId = line.FactionId,
                        Delta = line.Delta,
                        Reason = PoliticalSettlementReasonTokens.ToToken(line.Reason),
                    }).ToList(),
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
            });

            // ADR-0028: warrant 정치 결과를 세력 trust(profile truth)에 반영. delta 계산은 SM.Meta 순수(reporter가
            // FactionTrustService/WarrantOfferService에 위임), 적용·캡처만 여기서. issuer 없으면 Empty라 무영향.
            if (warrantSpec != null)
            {
                ApplyFactionTrustDeltas(settlementReport.Lines
                    .Select(line => new FactionTrustDelta(line.FactionId, line.Delta)).ToList());
            }

            // #1 가독성: 정치 정산을 화면 노출용으로 보관(미서약이면 Empty → reward 화면에서 정치 섹션 숨김).
            _session.LastPoliticalSettlement = settlementReport;

            // P1b: squad 손실(costly victory)을 chapter-scoped story flag로 stamp(through-director, 우회 아님).
            // Town-return 이벤트가 FlagSet로 이 flag를 읽어 손실 acknowledgement를 분기한다(P1b-content).
            if (outcome == DossierOutcome.CostlyVictory && !string.IsNullOrWhiteSpace(chapterId))
            {
                _session.StoryDirector.SetFlag($"story_flag_{chapterId}_squad_costly");
                _session.SyncNarrativeProgress();
            }

            // P2a: 서약 결과를 chapter-scoped story flag로 stamp(through-director, P1b와 동일 패턴).
            // GPT Pro 검수(§5.4): Dossier가 source of truth(전체 snapshot 보유), 이 flag는 authored dialogue projection일 뿐이다.
            // 세력 평판·해금·가격 같은 systemic 효과는 P2b에서 Dossier/FactionState로 물린다 — flag로 게이트하지 않는다.
            // 미서약(NotApplicable)은 stamp 안 함. Kept→kept, Broken/FailedMission→broken(coarse projection).
            if (warrantSpec != null && warrantJudgment.Outcome != WarrantOutcome.NotApplicable
                && !string.IsNullOrWhiteSpace(chapterId))
            {
                var warrantToken = warrantJudgment.Outcome == WarrantOutcome.Kept ? "kept" : "broken";
                _session.StoryDirector.SetFlag($"story_flag_{chapterId}_warrant_{warrantToken}");
                _session.SyncNarrativeProgress();
            }
        }

        // ADR-0028: SM.Meta가 계산한 trust delta를 SaveProfile.FactionStanding(profile truth)에 적용.
        // 해당 faction record가 없으면 생성. 정치 평판은 run 간 지속이므로 profile-level(run overlay 아님).
        private void ApplyFactionTrustDeltas(IReadOnlyList<FactionTrustDelta> deltas)
        {
            foreach (var delta in deltas)
            {
                if (string.IsNullOrWhiteSpace(delta.FactionId))
                {
                    continue;
                }

                var record = _session.Profile.FactionStanding
                    .FirstOrDefault(f => string.Equals(f.FactionId, delta.FactionId, StringComparison.Ordinal));
                if (record == null)
                {
                    record = new FactionStandingRecord { FactionId = delta.FactionId };
                    _session.Profile.FactionStanding.Add(record);
                }

                record.Trust += delta.Delta;
            }
        }

        internal bool ApplyRewardChoice(int index)
        {
            if (index < 0 || index >= _session._pendingRewardChoices.Count)
            {
                return false;
            }

            var choice = _session._pendingRewardChoices[index];
            var rewardSourceId = _session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty;
            var rewardCommitId = _session.ActiveRun?.Overlay.RewardCommitId ?? string.Empty;
            // task-reward-settlement-commit-v1 acceptance #3: rewardSourceId 기반 기존 dedup에
            // RewardCommitId 기반 검사를 추가해 reload-after-commit non-regression을 강화한다.
            // 같은 site에서 다른 outcome으로 두 번 도달하는 경우(예: defeat 후 retry)에 대해서도
            // commitId가 다르면 정상 처리되고, 같은 commitId면 두 번째 호출은 mutation 없이 통과.
            // 무한 순환(EndlessCycleIndex>0)에서는 SourceId 절을 건너뛴다 — RewardSourceId는 콘텐츠 고정값이라
            // 회차 재방문을 전부 '기지급'으로 오판한다. 사이클 dedup은 cycle-salt가 들어간 CommitId가 담당.
            var isEndlessCycleRun = (_session.ActiveRun?.EndlessCycleIndex ?? 0) > 0;
            var isPersistentDefeatConsolation = !_session.LastBattleVictory
                                                && _session._campaignRecoveryFlow.HasPersistentDefeatConsolationContext;
            if (_session._campaignRecoveryFlow.ShouldSuppressRewardChoice
                || (!isEndlessCycleRun
                    && !isPersistentDefeatConsolation
                    && _session.HasRecordedRewardSettlement(rewardSourceId))
                || _session.HasRecordedRewardSettlementByCommitId(rewardCommitId))
            {
                _session._pendingRewardChoices.Clear();
                _session.LastRewardApplicationSummary = new SessionTextToken(
                    GameLocalizationTables.UIReward,
                    "ui.reward.status.recovered_choice",
                    "Recovered previous reward settlement.");
                _session.AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardSettlementDuplicatePrevented(
                    _session.ResolveTelemetryRunId(),
                    rewardSourceId));
                _session.LastCommittedRewardSummary = BuildRewardSummary(
                    index, choice, rewardSourceId, goldDelta: 0, echoDelta: 0, wasRecovered: true);
                _session.SyncActiveRunIfPresent();
                return true;
            }

            var goldBeforeChoice = _session.Profile.Currencies.Gold;
            var echoBeforeChoice = _session.Profile.Currencies.Echo;
            var timestamp = DateTime.UtcNow.ToString("O");
            if (!_session.LastBattleVictory
                && _session._campaignRecoveryFlow.HasPersistentDefeatConsolationContext)
            {
                _session._campaignRecoveryFlow.CommitDefeatConsolationClaim();
            }

            switch (choice.Kind)
            {
                case RewardChoiceKind.Gold:
                    _session.ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.Gold, choice.GoldAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                    break;
                case RewardChoiceKind.Item:
                    var itemRecord = _session.CreateGeneratedInventoryItem(choice.PayloadId);
                    _session.Profile.Inventory.Add(itemRecord);
                    _session.Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
                    {
                        EntryId = Guid.NewGuid().ToString("N"),
                        RunId = _session.ActiveRun?.RunId ?? string.Empty,
                        ItemInstanceId = itemRecord.ItemInstanceId,
                        ItemBaseId = choice.PayloadId,
                        ChangeKind = "reward_item",
                        Amount = 1,
                        CreatedAtUtc = timestamp,
                        Summary = BuildRewardChoiceSummaryKey(choice),
                        SourceId = _session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                        SourceKind = _session.ResolveRewardSourceKind(_session.ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
                    });
                    _session.Profile.RewardLedger.Add(new RewardLedgerEntryRecord
                    {
                        EntryId = Guid.NewGuid().ToString("N"),
                        RunId = _session.ActiveRun?.RunId ?? string.Empty,
                        RewardId = choice.PayloadId,
                        RewardType = SM.Core.Content.RewardType.Item.ToString(),
                        Amount = 1,
                        CreatedAtUtc = timestamp,
                        Summary = BuildRewardChoiceSummaryKey(choice),
                        SourceId = _session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                        SourceKind = _session.ResolveRewardSourceKind(_session.ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
                        CommitId = rewardCommitId,
                    });
                    _session.LastRewardApplicationSummary = BuildRewardChoiceSummaryToken(choice);
                    break;
                case RewardChoiceKind.TemporaryAugment:
                    _session.ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.TemporaryAugment, 1, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                    break;
                case RewardChoiceKind.Echo:
                    _session.ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.Echo, choice.EchoAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                    break;
                case RewardChoiceKind.PermanentAugmentSlot:
                    return false;
            }

            _session.Profile.RunSummaries.Add(new RunSummaryRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                ExpeditionId = _session.IsQuickBattleSmokeActive ? "quick-battle" : $"node-{_session.CurrentExpeditionNodeIndex}",
                Result = _session.LastBattleVictory ? "victory" : "defeat",
                GoldEarned = choice.Kind == RewardChoiceKind.Gold ? choice.GoldAmount : 0,
                NodesCleared = _session.CurrentExpeditionNodeIndex + 1,
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
                ChapterId = _session.ActiveRun?.Overlay.ChapterId ?? _session.Profile.CampaignProgress.SelectedChapterId,
                SiteId = _session.ActiveRun?.Overlay.SiteId ?? _session.Profile.CampaignProgress.SelectedSiteId,
            });

            _session.AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardOptionChosen(
                _session.ResolveTelemetryRunId(),
                rewardSourceId,
                choice.PayloadId,
                _session.Profile.Currencies.Gold,
                _session.Profile.Currencies.Echo));
            _session.AppendRuntimeTelemetry(_session.BuildEconomySnapshot("reward_choice_applied"));
            _session.LastCommittedRewardSummary = BuildRewardSummary(
                index,
                choice,
                rewardSourceId,
                goldDelta: _session.Profile.Currencies.Gold - goldBeforeChoice,
                echoDelta: _session.Profile.Currencies.Echo - echoBeforeChoice,
                wasRecovered: false);
            _session._pendingRewardChoices.Clear();
            _session.SyncActiveRunIfPresent();
            return true;
        }

        internal string PreviewPermanentUnlockFromTemporaryAugment(string augmentId)
        {
            if (string.IsNullOrWhiteSpace(augmentId)
                || _session.ActiveRun == null
                || !string.IsNullOrWhiteSpace(_session.ActiveRun.Overlay.FirstSelectedTemporaryAugmentId))
            {
                return string.Empty;
            }

            return _session.ResolvePendingPermanentUnlockId(augmentId);
        }

        private RewardSummaryRecord BuildRewardSummary(
            int index,
            RewardChoiceViewModel choice,
            string rewardSourceId,
            int goldDelta,
            int echoDelta,
            bool wasRecovered)
        {
            return new RewardSummaryRecord(
                ChapterId: _session.ActiveRun?.Overlay.ChapterId ?? _session.Profile.CampaignProgress.SelectedChapterId,
                SiteId: _session.ActiveRun?.Overlay.SiteId ?? _session.Profile.CampaignProgress.SelectedSiteId,
                RewardSourceId: rewardSourceId,
                ChoiceIndex: index,
                ChoiceKind: choice.Kind.ToString(),
                PayloadId: choice.PayloadId,
                GoldDelta: goldDelta,
                EchoDelta: echoDelta,
                WasRecoveredSettlement: wasRecovered);
        }
    }

    private void ApplyLedgerBackedReward(RewardOption option, SessionTextToken summaryToken)
    {
        if (ActiveRun == null)
        {
            ActiveRun = RunStateService.StartRun(IsQuickBattleSmokeActive ? "quick-battle" : GetExpeditionRunId(), CaptureBlueprintState(), IsQuickBattleSmokeActive);
        }

        var currencyState = new CurrencyState(
            Profile.Currencies.Gold,
            Profile.Currencies.Echo,
            Profile.Currencies.TraitRerollCurrency,
            Profile.Currencies.TraitLockToken,
            Profile.Currencies.TraitPurgeToken,
            Profile.Currencies.EmberDust,
            Profile.Currencies.EchoCrystal,
            Profile.Currencies.BossSigil);
        var result = RewardLedgerService.ApplyReward(currencyState, ActiveRun!, option);
        Profile.Currencies.Gold = currencyState.Gold;
        Profile.Currencies.Echo = currencyState.Echo;
        Profile.Currencies.TraitRerollCurrency = currencyState.TraitRerollCurrency;
        Profile.Currencies.TraitLockToken = currencyState.TraitLockToken;
        Profile.Currencies.TraitPurgeToken = currencyState.TraitPurgeToken;
        Profile.Currencies.EmberDust = currencyState.EmberDust;
        Profile.Currencies.EchoCrystal = currencyState.EchoCrystal;
        Profile.Currencies.BossSigil = currencyState.BossSigil;
        Profile.RewardLedger.Add(new RewardLedgerEntryRecord
        {
            EntryId = result.RewardEntry.EntryId,
            RunId = result.RewardEntry.RunId,
            RewardId = result.RewardEntry.RewardId,
            RewardType = result.RewardEntry.RewardType,
            Amount = result.RewardEntry.Amount,
            CreatedAtUtc = result.RewardEntry.CreatedAtUtc,
            Summary = result.RewardEntry.Summary,
            SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
            SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
            CommitId = ActiveRun?.Overlay.RewardCommitId ?? string.Empty,
        });
        if (result.InventoryEntry != null)
        {
            Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
            {
                EntryId = result.InventoryEntry.EntryId,
                RunId = result.InventoryEntry.RunId,
                ItemInstanceId = result.InventoryEntry.ItemInstanceId,
                ItemBaseId = result.InventoryEntry.ItemBaseId,
                ChangeKind = result.InventoryEntry.ChangeKind,
                Amount = result.InventoryEntry.Amount,
                CreatedAtUtc = result.InventoryEntry.CreatedAtUtc,
                Summary = result.InventoryEntry.Summary,
                SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
            });
        }

        ActiveRun = result.UpdatedRun;
        if (option.Type == SM.Core.Content.RewardType.TemporaryAugment)
        {
            TrackPermanentAugmentProgression(option.Id);
        }

        LastRewardApplicationSummary = summaryToken;
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    private static string ResolveRoleTag(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private string NormalizeCharacterId(string characterId, string archetypeId)
    {
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            return characterId;
        }

        return string.IsNullOrWhiteSpace(archetypeId) ? string.Empty : archetypeId;
    }

    private string ResolveBlueprintRoleInstructionId(string heroId, string classId, DeploymentAnchorId anchor)
    {
        if (IsQuickBattleSmokeActive && QuickBattleConfig != null && !QuickBattleConfig.UseScenarioAuthoring)
        {
            var overrideId = QuickBattleConfig.AllySlots
                .Where(slot => slot != null && string.Equals(slot.HeroId, heroId, StringComparison.Ordinal))
                .Select(slot => slot.RoleInstructionIdOverride)
                .FirstOrDefault(roleId => !string.IsNullOrWhiteSpace(roleId));
            if (!string.IsNullOrWhiteSpace(overrideId))
            {
                return overrideId;
            }
        }

        return ResolveDefaultRoleInstructionId(classId, anchor);
    }

    private string ResolveSandboxCharacterId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.CharacterId))
        {
            return slot.CharacterId;
        }

        if (!string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride))
        {
            return slot.ArchetypeIdOverride;
        }

        return string.Empty;
    }

    private string ResolveSandboxArchetypeId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride))
        {
            return slot.ArchetypeIdOverride;
        }

        var characterId = ResolveSandboxCharacterId(slot);
        if (_combatContentLookup.TryGetCharacterDefinition(characterId, out var character)
            && character.DefaultArchetype != null)
        {
            return character.DefaultArchetype.Id;
        }

        return characterId;
    }

    private string ResolveSandboxRoleInstructionId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleInstructionId))
        {
            return slot.RoleInstructionId;
        }

        var characterId = ResolveSandboxCharacterId(slot);
        if (_combatContentLookup.TryGetCharacterDefinition(characterId, out var character)
            && character.DefaultRoleInstruction != null)
        {
            return character.DefaultRoleInstruction.Id;
        }

        var resolvedArchetypeId = ResolveSandboxArchetypeId(slot);
        if (_combatContentLookup.TryGetArchetype(resolvedArchetypeId, out var archetype))
        {
            return ResolveDefaultRoleInstructionId(archetype.Class.Id, slot.Anchor);
        }

        return string.Empty;
    }

    private string ResolveSandboxRoleTag(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleTag) && !string.Equals(slot.RoleTag, "auto", StringComparison.Ordinal))
        {
            return slot.RoleTag;
        }

        var roleInstructionId = ResolveSandboxRoleInstructionId(slot);
        if (_combatContentLookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction))
        {
            return roleInstruction.RoleTag;
        }

        var resolvedArchetypeId = ResolveSandboxArchetypeId(slot);
        if (_combatContentLookup.TryGetArchetype(resolvedArchetypeId, out var archetype))
        {
            return ResolveRoleTag(archetype.Class.Id, slot.Anchor);
        }

        return slot.Anchor.IsFrontRow() ? "frontline" : "backline";
    }

    private static string ResolveDefaultRoleInstructionId(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private static SessionTextToken BuildBattleSummaryToken(bool victory, int stepCount, int eventCount)
    {
        return new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.battle_summary.base",
            "{0} / {1} steps / {2} events",
            SessionTextArg.Localized(
                GameLocalizationTables.UIReward,
                victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                victory ? "Victory" : "Defeat"),
            SessionTextArg.Number(stepCount),
            SessionTextArg.Number(eventCount));
    }

    private static string BuildRewardChoiceSummaryKey(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => "ui.reward.kind.gold",
            RewardChoiceKind.Item => "ui.reward.kind.item",
            RewardChoiceKind.TemporaryAugment => "ui.reward.kind.temp_augment",
            RewardChoiceKind.Echo => "ui.reward.kind.echo",
            RewardChoiceKind.PermanentAugmentSlot => "ui.reward.kind.permanent_slot",
            _ => "ui.common.none"
        };
    }

    private static SessionTextToken BuildRewardChoiceSummaryToken(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.gold",
                "Gold +{0}",
                SessionTextArg.Number(choice.GoldAmount)),
            RewardChoiceKind.Item => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.item",
                "Item / {0}",
                SessionTextArg.ItemName(choice.PayloadId)),
            RewardChoiceKind.TemporaryAugment => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.temp_augment",
                "Temp / {0}",
                SessionTextArg.AugmentName(choice.PayloadId)),
            RewardChoiceKind.Echo => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.echo",
                "Echo +{0}",
                SessionTextArg.Number(choice.EchoAmount)),
            RewardChoiceKind.PermanentAugmentSlot => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.permanent_slot",
                "Permanent Slot +{0}",
                SessionTextArg.Number(choice.PermanentSlotAmount)),
            _ => SessionTextToken.Plain(choice.PayloadId)
        };
    }
}
