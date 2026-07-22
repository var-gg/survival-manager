using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Combat.Services;

/// <summary>
/// Phase 1 tactical brain. Chooses *what each unit is trying to do* (its <see cref="CombatIntent"/>) from its
/// role, posture, and battle context — a layer above the Phase 0 movement/attack executor. The executor stays
/// dumb and reliable; the intent biases which target and where the unit wants to be, so the fight reads as
/// role-driven drama (tank holds/peels, duelist dives, ranger anchors, mystic supports) rather than a lockstep
/// blob.
///
/// Anti-jitter (gpt-pro design): intents are chosen at a low per-role cadence, not every 0.1s tick. Role-critical
/// interrupts (vanguard Peel, duelist Dive entry) are checked every tick; otherwise an intent is held until its
/// commit window expires, a hard interrupt fires, or a clearly better candidate appears (25% hysteresis). The
/// brain is a pure function of battle truth (positions, roles, posture, target ids, integer step) — no RNG, no
/// wall-clock — so it reproduces exactly on re-sim from seed and needs no serialization.
/// </summary>
public static class RoleBrain
{
    // Per-role decision cadence in fixed steps (0.1s each).
    private const int VanguardCadenceSteps = 5;
    private const int DuelistCadenceSteps = 6;
    private const int RangerCadenceSteps = 7;
    private const int MysticCadenceSteps = 4;
    private const int DefaultCadenceSteps = 6;

    // Intent priorities — the anti-jitter hysteresis baseline (a new intent must beat the current by 25% to
    // interrupt an unexpired commit). Dive/Peel carry their computed score as priority (usually 70+/80+).
    private const int EngagePriority = 0;
    private const int AnchorPriority = 10;
    private const int SupportPriority = 10;
    private const int HoldLinePriority = 20;

    // === Duelist Dive knobs (conservative defaults — BALANCE is owner-controlled; tune these) ===
    private const float MeleeRangeThreshold = 1.8f;     // matches the existing melee-nearest threshold
    private const float DiveMinHealthRatio = 0.55f;     // do not enter a dive while already hurt
    private const float DiveAbortHealthRatio = 0.30f;   // hard-interrupt out of a dive below this
    private const float DiveMaxForwardDepth = 5.0f;     // committed-dive renewal geometry, not a movement clamp
    private const float DiveMaxPathDistance = 5.5f;     // committed-dive renewal geometry, diagonal distance
    private const int DiveGeometryHardVetoScore = -1000;
    private const float DiveStartDangerRadius = 1.5f;
    private const int DiveStartMaxNearbyEnemies = 1;    // allow one frontline contact, reject obvious 1v2
    private const float DiveProtectedRadius = 1.5f;     // reject targets bodyguarded by an enemy frontliner
    private const int DiveCommitSteps = 12;             // 1.2s commit
    private const int DiveScoreThreshold = 70;          // requires a real backline objective
    private const int DiveContinueScoreThreshold = 50;  // 진행 중 다이브 지속 문턱 — 진입보다 관대(히스테리시스)
    private const float DiveSupportRadius = 4.0f;       // "not alone": allied frontliner nearby
    private const float LaneEngagedBuffer = 0.8f;       // "own front engages enemy front" proxy
    private const float DiveLowHpTargetRatio = 0.45f;   // bonus for a low-HP backline target
    private const int HealthPercentScale = 100;
    private const int DuelistDiveCommitMinHealthPercent = 45;
    private const int DuelistDiveCommitScoreThreshold = 55;
    private const int DuelistExecuteTargetHealthPercent = 35;
    private const int DuelistExecuteTargetScoreBonus = 50;
    private const int DuelistAssassinCommitSteps = 25;
    private const int DuelistAssassinContinueScoreThreshold = 35;
    // === Phase 2 블랙보드 소비 (마스터 플랜 기본값 — V1 authority 튜닝 노브) ===
    private const int DiveFocusMarkScore = 25;          // "+25 target has FocusMark" — 팀 화력과 다이브가 같은 곳을 본다
    private const float HoldLineDiveLowHpRatio = 0.35f; // HoldLine에서 다이브가 열리는 빈사 문턱
    private const int PeelBreachScore = 25;             // breach로 식별된 위협(전선 뒤 침투자)이 요격 1순위
    private const int PeelCarryScore = 15;              // 위협받는 아군이 carry면 우선 보호

    // === Vanguard Peel knobs (conservative defaults — owner-tunable) ===
    private const float PeelThreatBuffer = 0.8f;        // an enemy is "threatening" within enemy.AttackRange + this
    private const float PeelMaxInterceptDistance = 3.5f; // vanguard must be near enough to plausibly intercept
    private const float DuelistPeelMaxInterceptDistance = 3.0f;
    private const int PeelCommitSteps = 10;             // 1.0s commit
    private const float PeelInterceptAttackRangeFraction = 0.75f; // intercept lies attack-range-close to the threat

    public static void ResolveIntent(BattleState state, UnitSnapshot actor)
    {
        if (!actor.IsAlive)
        {
            return;
        }

        var current = actor.CurrentCombatIntent;
        var step = state.StepIndex;
        var hardInterruptReason = ResolveHardInterruptReason(state, actor, current);
        var hardInterrupt = hardInterruptReason != HardInterruptReason.None;
        if (hardInterruptReason == HardInterruptReason.DiveHealthBelowAbortThreshold)
        {
            BattleDiagnosticRecorder.RecordDiveHardAbort(state, actor, current, DiveAbortHealthRatio);
        }

        // Role-critical interrupts (vanguard Peel, duelist Dive entry) are evaluated EVERY tick, before cadence —
        // they are tactical interrupts, not generic re-evaluation. (Wired in Stage 2.)
        if (!hardInterrupt
            && TryBuildRoleCriticalIntent(state, actor, out var critical)
            && ShouldReplaceIntent(current, critical, step, roleCritical: true))
        {
            ApplyIntent(state, actor, critical);
            return;
        }

        // Cadence: between decision steps, hold the committed intent (kills per-tick churn). The movement
        // executor still runs every tick and adapts to the held intent.
        if (!hardInterrupt && step < actor.NextCombatIntentDecisionStep)
        {
            return;
        }

        var candidate = BuildBestRoleIntent(state, actor);
        if (hardInterrupt || ShouldReplaceIntent(current, candidate, step, roleCritical: false))
        {
            ApplyIntent(state, actor, candidate);
        }
        else
        {
            actor.SetNextCombatIntentDecisionStep(step + GetCadenceSteps(actor));
        }
    }

    private static void ApplyIntent(BattleState state, UnitSnapshot actor, CombatIntent intent)
    {
        var current = actor.CurrentCombatIntent;
        if (!intent.DiveEntryTargetId.HasValue && current.DiveEntryTargetId.HasValue)
        {
            intent = intent with
            {
                DiveEntryStep = current.DiveEntryStep,
                DiveEntryTargetId = current.DiveEntryTargetId,
                DiveEntryPathDistance = current.DiveEntryPathDistance,
                DiveEntryActorPosition = current.DiveEntryActorPosition,
            };
        }

        actor.SetCombatIntent(intent);
        actor.SetNextCombatIntentDecisionStep(state.StepIndex + GetCadenceSteps(actor));
    }

    private static bool ShouldReplaceIntent(CombatIntent current, CombatIntent candidate, int step, bool roleCritical)
    {
        if (current.Type == CombatIntentType.None)
        {
            return true;
        }

        if (roleCritical)
        {
            return true;
        }

        if (step >= current.CommitUntilStep)
        {
            return true;
        }

        // Hysteresis: a new intent must beat the current by 25% to interrupt an unexpired commit. Integer-safe.
        return candidate.Priority * 100 >= current.Priority * 125;
    }

    private static int GetCadenceSteps(UnitSnapshot actor) => actor.Definition.ClassId switch
    {
        "vanguard" => VanguardCadenceSteps,
        "duelist" => DuelistCadenceSteps,
        "ranger" => RangerCadenceSteps,
        "mystic" => MysticCadenceSteps,
        _ => DefaultCadenceSteps,
    };

    // Baseline (cadence-gated) role intents. Dive/Peel are NOT built here — they are role-critical (every-tick).
    private static CombatIntent BuildBestRoleIntent(BattleState state, UnitSnapshot actor)
    {
        var step = state.StepIndex;

        // Ranger / backline ranged → AnchorFire: hold the backline anchor, fire at whatever enters range.
        if (IsAnchoredRanged(actor))
        {
            return new CombatIntent(CombatIntentType.AnchorFire, actor.CurrentTargetId, null,
                MovementResolver.ResolveHomePosition(state, actor), step, AnchorPriority);
        }

        // Mystic / backline support → SupportAnchor: hold the backline anchor, support/cast from there.
        if (IsBacklineSupport(actor))
        {
            return new CombatIntent(CombatIntentType.SupportAnchor, actor.CurrentTargetId, null,
                MovementResolver.ResolveHomePosition(state, actor), step, SupportPriority);
        }

        // Vanguard under a HoldLine posture → HoldLine: hold the frontline band, don't over-chase.
        if (IsHoldLineVanguard(state, actor))
        {
            return new CombatIntent(CombatIntentType.HoldLine, actor.CurrentTargetId, null,
                MovementResolver.ResolveHomePosition(state, actor), step, HoldLinePriority);
        }

        // Everyone else → Engage: clean Phase 0 pursuit.
        return new CombatIntent(CombatIntentType.Engage, actor.CurrentTargetId, null, default, step, EngagePriority);
    }

    // Role-critical (every-tick) interrupts: vanguard Peel and duelist Dive entry.
    private static bool TryBuildRoleCriticalIntent(BattleState state, UnitSnapshot actor, out CombatIntent intent)
    {
        // Vanguard Peel: intercept a diver threatening a backline ally — checked every tick.
        if ((actor.Definition.ClassId == "vanguard"
             || (actor.Definition.ClassId == "duelist" && actor.HasBehaviorTag(CombatBehaviorTags.DuelistPeel)))
            && TryBuildPeelIntent(state, actor, out intent))
        {
            return true;
        }

        // Dive entry/continuation is checked every tick. Entry catches the window the moment it opens;
        // continuation(이미 다이빙 중)은 TryBuildDiveIntent의 지속 분기가 재점수만으로 커밋을 갱신한다 —
        // 종전처럼 다이빙 중을 제외하면 커밋(1.2s)이 후열 도달 전에 만료돼 다이브가 페인트로 끝난다.
        if (actor.Definition.ClassId == "duelist"
            && TryBuildDiveIntent(state, actor, out intent))
        {
            return true;
        }

        intent = CombatIntent.None;
        return false;
    }

    private static HardInterruptReason ResolveHardInterruptReason(
        BattleState state,
        UnitSnapshot actor,
        CombatIntent current)
    {
        if (!actor.IsAlive)
        {
            return HardInterruptReason.ActorDead;
        }

        // The intent's chosen target died/became invalid.
        if (current.TargetId.HasValue && LivingUnit(state, current.TargetId) == null)
        {
            return HardInterruptReason.TargetInvalid;
        }

        // A diver that drops below the abort HP bails immediately (regardless of commit).
        if (current.Type == CombatIntentType.Dive && actor.HealthRatio < DiveAbortHealthRatio)
        {
            return HardInterruptReason.DiveHealthBelowAbortThreshold;
        }

        // A peel is moot once the protected ally is gone.
        if (current.Type == CombatIntentType.Peel
            && current.ProtectAllyId.HasValue && LivingUnit(state, current.ProtectAllyId) == null)
        {
            return HardInterruptReason.ProtectedAllyInvalid;
        }

        return HardInterruptReason.None;
    }

    // ===== Duelist Dive ===== (target reshape applied by TacticEvaluator.TryApplyIntentTargetOverride)

    private static bool TryBuildDiveIntent(BattleState state, UnitSnapshot actor, out CombatIntent intent)
    {
        intent = CombatIntent.None;
        var geometryLimits = ResolveDiveGeometryLimits(state, actor);
        var trace = state.ShouldObserveDiagnostic(BattleDiagnosticKind.DiveIntentEvaluation, actor.Id.Value)
            ? new DiveIntentTrace(
                state,
                actor,
                actor.HasBehaviorTag(CombatBehaviorTags.DuelistDiveCommit)
                    ? DuelistDiveCommitMinHealthPercent / (float)HealthPercentScale
                    : DiveMinHealthRatio,
                MeleeRangeThreshold,
                geometryLimits.MaxForwardDepth,
                geometryLimits.MaxPathDistance,
                DiveStartMaxNearbyEnemies)
            : null;
        if (actor.HasBehaviorTag(CombatBehaviorTags.DuelistHoldBruiser))
        {
            trace?.Fail(DiveIntentGateReason.HoldBruiserTag);
            trace?.Emit(state);
            return false;
        }

        // 지속(continuation) 계약: 이미 다이빙 중이면 진입 게이트(support proxy/threat/posture)를 다시 묻지
        // 않는다 — 진입 게이트를 지속 조건으로 재적용하면 전선을 떠나는 순간 갱신이 끊겨 다이브가 1.2초
        // 페인트로 끝난다(T1 sweep: 의도 발동 24/24인데 후열 도달 0). 지속 조건은 재점수만: 타겟이 살아있고
        // ScoreDiveTarget ≥ 지속 문턱이면 커밋을 갱신한다. 적 전열이 후열을 보디가드하러 복귀하면 보호
        // 페널티(-45)로 점수가 문턱 아래로 떨어져 다이브가 풀린다 — 수비가 응답하면 다이브가 꺾이는 룰.
        // hard interrupt(HP<0.30, 타겟 사망)는 그대로 우선한다.
        if (actor.CurrentCombatIntent.Type == CombatIntentType.Dive
            && actor.CurrentCombatIntent.TargetId is { } diveTargetId
            && LivingUnit(state, diveTargetId) is { } diveTarget)
        {
            trace?.SetContinuation(diveTarget.Id.Value);
            var continueScore = ScoreDiveTarget(state, actor, diveTarget, trace);
            if (continueScore >= ResolveDiveContinueScoreThreshold(actor))
            {
                intent = new CombatIntent(CombatIntentType.Dive, diveTarget.Id, null,
                    MovementResolver.ResolveHomePosition(state, actor), state.StepIndex + ResolveDiveCommitSteps(actor), continueScore,
                    actor.CurrentCombatIntent.DiveEntryStep,
                    actor.CurrentCombatIntent.DiveEntryTargetId,
                    actor.CurrentCombatIntent.DiveEntryPathDistance,
                    actor.CurrentCombatIntent.DiveEntryActorPosition);
                trace?.Select(diveTarget.Id.Value, state.StepIndex + ResolveDiveCommitSteps(actor));
                trace?.Emit(state);
                return true;
            }

            trace?.Fail(DiveIntentGateReason.ContinueScoreBelowThreshold);
            trace?.Emit(state);
            return false;
        }

        if (!IsDiveEntryEligibleIgnoringSlot(state, actor, trace))
        {
            trace?.Emit(state);
            return false;
        }

        if (!CanEnterTeamDiveSlot(state, actor, trace))
        {
            trace?.Fail(DiveIntentGateReason.TeamDiveSlotUnavailable);
            trace?.Emit(state);
            return false;
        }

        var selection = ResolveDistinctDiveTarget(state, actor, trace);
        if (selection == null)
        {
            trace?.Fail(DiveIntentGateReason.DistinctTargetUnavailable);
            trace?.Emit(state);
            return false;
        }

        var selectedTarget = selection.Value.Target;
        intent = new CombatIntent(CombatIntentType.Dive, selectedTarget.Id, null,
            MovementResolver.ResolveHomePosition(state, actor), state.StepIndex + ResolveDiveCommitSteps(actor), selection.Value.Score,
            state.StepIndex,
            selectedTarget.Id,
            actor.Position.DistanceTo(selectedTarget.Position),
            actor.Position);
        trace?.Select(selectedTarget.Id.Value, state.StepIndex + ResolveDiveCommitSteps(actor));
        trace?.Emit(state);
        return true;
    }

    // Dive entry gates that do NOT depend on the team dive-slot (used to gate entry AND to pick the deterministic
    // primary diver). Posture-gated to aggressive postures so default StandardAdvance behavior is unchanged.
    private static bool IsDiveEntryEligibleIgnoringSlot(
        BattleState state,
        UnitSnapshot actor,
        DiveIntentTrace? trace = null)
    {
        if (actor.HasBehaviorTag(CombatBehaviorTags.DuelistHoldBruiser))
        {
            trace?.Fail(DiveIntentGateReason.HoldBruiserTag);
            return false;
        }

        if (actor.AttackRange > MeleeRangeThreshold)
        {
            trace?.Fail(DiveIntentGateReason.AttackRangeAboveMeleeThreshold);
            return false;
        }

        // 공격 posture에서는 자유 다이브, HoldLine에서는 절제된 다이브(빈사 표적 또는 팀 FocusMark에만 —
        // 마스터 플랜 "HoldLine: duelist does not dive unless target HP ≤ 35% or FocusMark"). StandardAdvance/
        // ProtectCarry 기본 동작은 불변.
        var posture = state.GetPosture(actor.Side);
        if (posture != TeamPostureType.AllInBackline
            && posture != TeamPostureType.CollapseWeakSide
            && posture != TeamPostureType.HoldLine
            && !(posture == TeamPostureType.StandardAdvance
                 && actor.HasBehaviorTag(CombatBehaviorTags.DuelistDiveCommit)))
        {
            trace?.Fail(DiveIntentGateReason.PostureDisallowsDive);
            return false;
        }

        if (actor.HasBehaviorTag(CombatBehaviorTags.DuelistDiveCommit)
            ? !actor.IsHealthRatioAtOrAbove(DuelistDiveCommitMinHealthPercent, HealthPercentScale)
            : actor.HealthRatio < DiveMinHealthRatio)
        {
            trace?.Fail(DiveIntentGateReason.HealthBelowEntryThreshold);
            return false;
        }

        var hasSupportProxy = HasDiveSupportProxy(state, actor);
        trace?.SetSupportProxy(hasSupportProxy);
        if (!hasSupportProxy)
        {
            trace?.Fail(DiveIntentGateReason.SupportProxyMissing);
            return false;
        }

        var nearbyEnemyCount = CountLivingEnemiesWithin(state, actor, DiveStartDangerRadius);
        trace?.SetNearbyEnemyCount(nearbyEnemyCount);
        if (nearbyEnemyCount > DiveStartMaxNearbyEnemies)
        {
            trace?.Fail(DiveIntentGateReason.TooManyNearbyEnemies);
            return false;
        }

        if (trace == null)
        {
            return state.GetOpponents(actor.Side)
                .Any(e => e.IsAlive
                          && IsDiveCandidateUnderPosture(state, actor, e)
                          && ScoreDiveTarget(state, actor, e) >= ResolveDiveEntryScoreThreshold(actor));
        }

        var runtimeBacklineCandidates = 0;
        var postureEligibleCandidates = 0;
        var scoreEligibleCandidates = 0;
        var requiredScore = ResolveDiveEntryScoreThreshold(actor);
        foreach (var enemy in state.GetOpponents(actor.Side).Where(enemy => enemy.IsAlive))
        {
            var isRuntimeBackline = IsDiveCandidate(enemy);
            var postureEligible = isRuntimeBackline && IsDiveCandidateUnderPosture(state, actor, enemy);
            trace.RecordCandidateShape(enemy, postureEligible, requiredScore);
            if (!isRuntimeBackline)
            {
                continue;
            }

            runtimeBacklineCandidates++;
            if (!postureEligible)
            {
                continue;
            }

            postureEligibleCandidates++;
            if (ScoreDiveTarget(state, actor, enemy, trace) >= requiredScore)
            {
                scoreEligibleCandidates++;
            }
        }

        if (runtimeBacklineCandidates == 0)
        {
            trace.Fail(DiveIntentGateReason.NoRuntimeBacklineCandidate);
            return false;
        }

        if (postureEligibleCandidates == 0)
        {
            trace.Fail(DiveIntentGateReason.PostureFilteredAllCandidates);
            return false;
        }

        if (scoreEligibleCandidates == 0)
        {
            trace.Fail(DiveIntentGateReason.CandidateScoreBelowEntryThreshold);
            return false;
        }

        return true;
    }

    // The concurrency ceiling is the number of living duelists that are either continuing a Dive or currently pass
    // the entry gates. Admission is derived deterministically from battle state, so authored duelist count is the
    // pressure lever instead of a hard-coded team cap.
    private static bool CanEnterTeamDiveSlot(BattleState state, UnitSnapshot actor, DiveIntentTrace? trace = null)
    {
        var activeDivers = state.GetTeam(actor.Side)
            .Where(a => a.IsAlive && a.CurrentCombatIntent.Type == CombatIntentType.Dive)
            .ToList();
        if (activeDivers.Any(a => a.Id == actor.Id))
        {
            return true;
        }

        var eligibleDivers = GetOrderedDiveParticipants(state, actor.Side);
        var maxConcurrentDives = eligibleDivers.Count;
        trace?.SetDiveSlotCounts(activeDivers.Count, eligibleDivers.Count);
        if (activeDivers.Count >= maxConcurrentDives)
        {
            return false;
        }

        return eligibleDivers
            .Take(maxConcurrentDives)
            .Any(eligible => eligible.Id == actor.Id);
    }

    // Rebuild the whole diver->target matching on every entry decision. Divers are traversed by stable id; each
    // diver ranks candidates by integer score, HP ratio, distance, then stable target id and takes the first target
    // not already claimed. Existing committed Dive targets are reserved before new assignments. The HashSet is used
    // only for membership, never iteration: matching is a pure deterministic function of battle state. In the common
    // symmetric case it is also independent of act order; when divers outnumber reachable targets, candidates[0]
    // resolves through deterministic act order and remains reproducible without RNG.
    private static (UnitSnapshot Target, int Score)? ResolveDistinctDiveTarget(
        BattleState state,
        UnitSnapshot actor,
        DiveIntentTrace? trace = null)
    {
        var eligibleDivers = GetOrderedDiveParticipants(state, actor.Side);
        var claimedTargetIds = new HashSet<EntityId>();
        foreach (var diver in eligibleDivers)
        {
            if (diver.CurrentCombatIntent.Type == CombatIntentType.Dive
                && diver.CurrentCombatIntent.TargetId is { } committedTargetId
                && LivingUnit(state, committedTargetId) is { } committedTarget
                && committedTarget.Side != diver.Side)
            {
                claimedTargetIds.Add(committedTarget.Id);
            }
        }

        foreach (var diver in eligibleDivers)
        {
            if (diver.CurrentCombatIntent.Type == CombatIntentType.Dive)
            {
                continue;
            }

            var candidates = state.GetOpponents(diver.Side)
                .Where(enemy => enemy.IsAlive && IsDiveCandidateUnderPosture(state, diver, enemy))
                .Select(enemy => new
                {
                    Target = enemy,
                    Score = ScoreDiveTarget(state, diver, enemy, diver.Id == actor.Id ? trace : null),
                })
                .Where(candidate => candidate.Score >= ResolveDiveEntryScoreThreshold(diver))
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Target.HealthRatio)
                .ThenBy(candidate => diver.Position.DistanceTo(candidate.Target.Position))
                .ThenBy(candidate => candidate.Target.Id.Value, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            var selection = candidates.FirstOrDefault(candidate => !claimedTargetIds.Contains(candidate.Target.Id))
                            ?? candidates[0];
            claimedTargetIds.Add(selection.Target.Id);
            if (diver.Id == actor.Id)
            {
                return (selection.Target, selection.Score);
            }
        }

        return null;
    }

    private static List<UnitSnapshot> GetOrderedDiveParticipants(BattleState state, TeamSide side)
    {
        return state.GetTeam(side)
            .Where(unit => unit.IsAlive
                           && unit.Definition.ClassId == "duelist"
                           && !unit.HasBehaviorTag(CombatBehaviorTags.DuelistHoldBruiser)
                           && (unit.CurrentCombatIntent.Type == CombatIntentType.Dive
                               || IsDiveEntryEligibleIgnoringSlot(state, unit)))
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToList();
    }

    private static int ScoreDiveTarget(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        DiveIntentTrace? trace = null)
    {
        var formationLineScore = 0;
        if (target.Behavior.FormationLine == FormationLine.Backline)
        {
            formationLineScore = 35;
        }

        var classScore = 0;
        if (target.Definition.ClassId == "mystic")
        {
            classScore = 50;
        }
        else if (target.Definition.ClassId == "ranger")
        {
            classScore = 40;
        }

        var lowHealthScore = 0;
        if (actor.HasBehaviorTag(CombatBehaviorTags.ExecuteLowHp))
        {
            if (target.IsHealthRatioAtOrBelow(DuelistExecuteTargetHealthPercent, HealthPercentScale))
            {
                lowHealthScore = DuelistExecuteTargetScoreBonus;
            }
        }
        else if (target.HealthRatio <= DiveLowHpTargetRatio)
        {
            lowHealthScore = 30;
        }

        var hasFrontlineProtector = HasEnemyFrontlineProtectorNear(state, actor, target);
        var protectorScore = hasFrontlineProtector ? -45 : 0;

        // Phase 2 FocusMark: 팀 블랙보드가 지목한 표적은 다이브 가치가 오른다 — 팀 화력과 다이버가
        // 같은 곳을 보면서 집중 사격 + 다이브 마무리가 한 그림으로 읽힌다.
        var hasFocusMark = state.GetTeamBlackboard(actor.Side).FocusMarkId == target.Id;
        var focusMarkScore = hasFocusMark ? DiveFocusMarkScore : 0;

        var geometryLimits = ResolveDiveGeometryLimits(state, actor, target);
        var forwardDepth = ForwardDepth(actor, target);
        var forwardDepthScore = forwardDepth > geometryLimits.MaxForwardDepth ? DiveGeometryHardVetoScore : 0;

        var pathDistance = actor.Position.DistanceTo(target.Position);
        var pathDistanceScore = pathDistance > geometryLimits.MaxPathDistance ? DiveGeometryHardVetoScore : 0;

        var score = formationLineScore
                    + classScore
                    + lowHealthScore
                    + protectorScore
                    + focusMarkScore
                    + forwardDepthScore
                    + pathDistanceScore;
        trace?.RecordCandidateScore(
            target,
            IsDiveCandidateUnderPosture(state, actor, target),
            hasFocusMark,
            hasFrontlineProtector,
            forwardDepth,
            pathDistance,
            formationLineScore,
            classScore,
            lowHealthScore,
            protectorScore,
            focusMarkScore,
            forwardDepthScore,
            pathDistanceScore,
            score,
            actor.CurrentCombatIntent.Type == CombatIntentType.Dive
                ? ResolveDiveContinueScoreThreshold(actor)
                : ResolveDiveEntryScoreThreshold(actor));
        return score;
    }

    private static bool IsDiveCandidate(UnitSnapshot enemy)
    {
        return enemy.Behavior.FormationLine == FormationLine.Backline
               && (enemy.Definition.ClassId == "ranger" || enemy.Definition.ClassId == "mystic");
    }

    // HoldLine posture는 다이브 표적을 제한한다: 빈사(≤35%) 또는 팀 FocusMark만. 그 외 posture는 기본 후보 그대로.
    private static bool IsDiveCandidateUnderPosture(BattleState state, UnitSnapshot actor, UnitSnapshot enemy)
    {
        if (!IsDiveCandidate(enemy))
        {
            return false;
        }

        if (state.GetPosture(actor.Side) != TeamPostureType.HoldLine)
        {
            return true;
        }

        return enemy.HealthRatio <= HoldLineDiveLowHpRatio
               || state.GetTeamBlackboard(actor.Side).FocusMarkId == enemy.Id;
    }

    private static bool IsFrontlineBody(UnitSnapshot unit)
    {
        return unit.Behavior.FormationLine == FormationLine.Frontline
               && (unit.Definition.ClassId == "vanguard" || unit.Definition.ClassId == "duelist");
    }

    private static bool HasDiveSupportProxy(BattleState state, UnitSnapshot actor)
    {
        // "The duelist is not alone": an allied frontliner is nearby...
        if (state.GetTeam(actor.Side).Any(a =>
                a.IsAlive && a.Id != actor.Id && IsFrontlineBody(a)
                && a.Position.DistanceTo(actor.Position) <= DiveSupportRadius))
        {
            return true;
        }

        // ...or an allied frontliner is lane-engaged with an enemy frontliner (own front occupies enemy front).
        // 자기 자신도 인정한다(T1 다이브 봉인 해제): 전열이 듀얼리스트 하나뿐인 조합에서는 "다른 아군 전열"
        // 요구가 Dive를 영구 봉인했다. 듀얼리스트 본인이 적 전열과 교전 중이면 적 전열이 전선에 묶여 있다는
        // 증명이고, 다이브가 시작되면 그 적 전열이 추격(수비측 Peel)으로 응수하는 게 의도된 드라마 루프다.
        foreach (var enemyFront in state.GetOpponents(actor.Side).Where(e => e.IsAlive && IsFrontlineBody(e)))
        {
            if (state.GetTeam(actor.Side).Any(ally =>
                    ally.IsAlive && IsFrontlineBody(ally)
                    && MovementResolver.ComputeEdgeDistance(ally, enemyFront)
                        <= Math.Max(ally.AttackRange, enemyFront.AttackRange) + LaneEngagedBuffer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEnemyFrontlineProtectorNear(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        return state.GetOpponents(actor.Side).Any(e =>
            e.IsAlive && e.Id != target.Id && IsFrontlineBody(e)
            && e.Position.DistanceTo(target.Position) <= DiveProtectedRadius);
    }

    private static int CountLivingEnemiesWithin(BattleState state, UnitSnapshot actor, float radius)
    {
        return state.GetOpponents(actor.Side).Count(e => e.IsAlive && e.Position.DistanceTo(actor.Position) <= radius);
    }

    private static float ForwardDepth(UnitSnapshot actor, UnitSnapshot target)
    {
        return actor.Side == TeamSide.Ally
            ? target.Position.X - actor.Position.X
            : actor.Position.X - target.Position.X;
    }

    private static (float MaxForwardDepth, float MaxPathDistance) ResolveDiveGeometryLimits(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target = null)
    {
        if (actor.CurrentCombatIntent.Type == CombatIntentType.Dive
            || actor.HasBehaviorTag(CombatBehaviorTags.PackPursuit))
        {
            return (DiveMaxForwardDepth, DiveMaxPathDistance);
        }

        var priorEntry = actor.CurrentCombatIntent;
        if (target == null
            && priorEntry.DiveEntryTargetId is { } priorTargetId
            && LivingUnit(state, priorTargetId) is { } priorTarget)
        {
            target = priorTarget;
        }

        // A target gets another widened entry window only after the actor has strictly reduced its path distance
        // since the preceding window began. The attempt metadata is carried through the baseline-intent lapse, so
        // a rooted or displaced-away diver remains on the original 5.0/5.5 gate instead of re-arming forever.
        if (target != null
            && priorEntry.DiveEntryStep >= 0
            && priorEntry.DiveEntryTargetId == target.Id
            && (actor.IsRooted
                || actor.Position.DistanceTo(target.Position)
                    >= priorEntry.DiveEntryActorPosition.DistanceTo(target.Position)))
        {
            return (DiveMaxForwardDepth, DiveMaxPathDistance);
        }

        // Entry asks whether this actor can enter the established safe geometry during one initial commit,
        // using its content-owned movement speed and tag-owned commit duration. This breaks the old circular
        // gate (too far to select, therefore never approaches) without making the limits aspirational. Pack-pursuit
        // units retain the original gate so their authored wing approach remains the way they earn Dive geometry.
        var initialCommitApproachDistance = actor.MoveSpeed * state.FixedStepSeconds * ResolveDiveCommitSteps(actor);
        return (
            DiveMaxForwardDepth + initialCommitApproachDistance,
            DiveMaxPathDistance + initialCommitApproachDistance);
    }

    // ===== Vanguard Peel ===== (target reshape applied by TacticEvaluator.TryApplyIntentTargetOverride)

    private static bool TryBuildPeelIntent(BattleState state, UnitSnapshot actor, out CombatIntent intent)
    {
        intent = CombatIntent.None;
        if (!IsPeelAllowedPosture(state.GetPosture(actor.Side)))
        {
            return false;
        }

        // The protected ally is whatever backline ally is under threat; pick the most urgent threat the vanguard
        // can plausibly reach. Deterministic ordering (score, then ally HP, then proximity, then ids ordinal).
        var best = state.GetTeam(actor.Side)
            .Where(a => a.IsAlive && a.Id != actor.Id && a.Behavior.FormationLine == FormationLine.Backline)
            .SelectMany(ally => state.GetOpponents(actor.Side)
                .Where(e => e.IsAlive && IsThreateningBacklineAlly(e, ally)
                            && actor.Position.DistanceTo(e.Position) <= ResolvePeelMaxInterceptDistance(actor))
                .Select(enemy => new { Ally = ally, Enemy = enemy, Score = ScorePeelThreat(state, actor, ally, enemy) }))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Ally.HealthRatio)
            .ThenBy(c => MovementResolver.ComputeEdgeDistance(c.Enemy, c.Ally))
            .ThenBy(c => actor.Position.DistanceTo(c.Enemy.Position))
            .ThenBy(c => c.Ally.Id.Value, StringComparer.Ordinal)
            .ThenBy(c => c.Enemy.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (best == null)
        {
            return false;
        }

        var anchor = ComputePeelInterceptPoint(actor, best.Enemy, best.Ally);
        intent = new CombatIntent(CombatIntentType.Peel, best.Enemy.Id, best.Ally.Id, anchor,
            state.StepIndex + PeelCommitSteps, best.Score);
        return true;
    }

    private static int ResolveDiveEntryScoreThreshold(UnitSnapshot actor)
        => actor.HasBehaviorTag(CombatBehaviorTags.DuelistDiveCommit)
            ? DuelistDiveCommitScoreThreshold
            : DiveScoreThreshold;

    private static int ResolveDiveContinueScoreThreshold(UnitSnapshot actor)
        => actor.HasBehaviorTag(CombatBehaviorTags.DiveAssassinKeystone)
            ? DuelistAssassinContinueScoreThreshold
            : DiveContinueScoreThreshold;

    private static int ResolveDiveCommitSteps(UnitSnapshot actor)
        => actor.HasBehaviorTag(CombatBehaviorTags.DiveAssassinKeystone)
            ? DuelistAssassinCommitSteps
            : DiveCommitSteps;

    private enum HardInterruptReason
    {
        None = 0,
        ActorDead = 1,
        TargetInvalid = 2,
        DiveHealthBelowAbortThreshold = 3,
        ProtectedAllyInvalid = 4,
    }

    private static float ResolvePeelMaxInterceptDistance(UnitSnapshot actor)
        => actor.HasBehaviorTag(CombatBehaviorTags.DuelistPeel)
            ? DuelistPeelMaxInterceptDistance
            : PeelMaxInterceptDistance;

    private static bool IsPeelAllowedPosture(TeamPostureType posture)
    {
        // Allowed in every posture except AllInBackline (which means "commit forward, don't peel").
        return posture != TeamPostureType.AllInBackline;
    }

    private static bool IsThreateningBacklineAlly(UnitSnapshot enemy, UnitSnapshot ally)
    {
        return MovementResolver.ComputeEdgeDistance(enemy, ally) <= enemy.AttackRange + PeelThreatBuffer;
    }

    private static int ScorePeelThreat(BattleState state, UnitSnapshot vanguard, UnitSnapshot ally, UnitSnapshot enemy)
    {
        var score = 80;
        var threatMargin = enemy.AttackRange + PeelThreatBuffer - MovementResolver.ComputeEdgeDistance(enemy, ally);
        if (threatMargin > 0f)
        {
            score += 20;
        }

        if (ally.HealthRatio <= 0.50f)
        {
            score += 15;
        }

        if (enemy.AttackRange <= 1.8f)
        {
            score += 10; // a melee diver on the backline reads clearly
        }

        // Phase 2 블랙보드: 전선 뒤로 들어온 침투자(breach)가 요격 1순위, 위협받는 아군이 carry면 우선 보호 —
        // Phase 1의 "즉시 위협" proxy 스캔은 즉각성 때문에 유지하고, 블랙보드 진실이 우선순위를 만든다.
        var blackboard = state.GetTeamBlackboard(vanguard.Side);
        if (blackboard.IsBreacher(enemy.Id))
        {
            score += PeelBreachScore;
        }

        if (blackboard.CarryId == ally.Id)
        {
            score += PeelCarryScore;
        }

        score -= (int)(vanguard.Position.DistanceTo(enemy.Position) * 5.0f);
        return score;
    }

    // The intercept point sits between the threat and the protected ally, attack-range-close to the threat so the
    // vanguard can strike once it arrives (not a retreat-to-ally point).
    private static CombatVector2 ComputePeelInterceptPoint(UnitSnapshot vanguard, UnitSnapshot threat, UnitSnapshot ally)
    {
        var threatToAlly = ally.Position - threat.Position;
        var distance = threatToAlly.Length;
        if (distance <= 0.001f)
        {
            return threat.Position;
        }

        var t = Math.Min(0.5f, (vanguard.AttackRange * PeelInterceptAttackRangeFraction) / distance);
        return threat.Position + (threatToAlly * t);
    }

    private static UnitSnapshot? LivingUnit(BattleState state, EntityId? id)
    {
        var unit = state.FindUnit(id);
        return unit is { IsAlive: true } ? unit : null;
    }

    private static bool IsAnchoredRanged(UnitSnapshot actor)
    {
        return actor.Behavior.FormationLine == FormationLine.Backline
               && actor.Definition.ClassId == "ranger";
    }

    private static bool IsBacklineSupport(UnitSnapshot actor)
    {
        return actor.Behavior.FormationLine == FormationLine.Backline
               && actor.Definition.ClassId == "mystic";
    }

    private static bool IsHoldLineVanguard(BattleState state, UnitSnapshot actor)
    {
        return actor.Behavior.FormationLine == FormationLine.Frontline
               && actor.Definition.ClassId == "vanguard"
               && state.GetPosture(actor.Side) == TeamPostureType.HoldLine;
    }
}
