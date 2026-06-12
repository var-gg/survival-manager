using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity;

public enum SpectacleShotKind
{
    /// <summary>중간 강조 — 콤보/구도 사건. 줌 상한이 유닛 포커스 수준.</summary>
    Medium = 0,

    /// <summary>임팩트 클로즈업 — 킬/클러치 전용(마스터 플랜: close-up only for kill/clutch/ultimate combo).</summary>
    Close = 1,
}

/// <summary>
/// 디렉터가 이번 순간 카메라에 제안하는 강조 샷. 유닛 id 가 우선이고(살아 움직이는 대상 추적),
/// 둘 다 해석 불가능하면 <see cref="FallbackPosition"/>(beat 발생 지점)으로 프레임한다.
/// presentation 전용 — sim/save truth 무접촉.
/// </summary>
public sealed record SpectacleCameraEmphasis(
    SpectacleShotKind ShotKind,
    int Importance,
    string? SubjectUnitId,
    string? RelatedUnitId,
    CombatVector2 FallbackPosition,
    double StartedAtSeconds,
    double ExpiresAtSeconds);

/// <summary>
/// Phase 4 spectacle director — sim truth(beat/kill 이벤트)를 읽어 "지금 무엇을 강조할지"를 결정하는
/// 순수 presentation 정책. 카메라 컷 예산(0.8s)·개전 보드 뷰(1.0s)·콜아웃 밀도(1초 1건)를 단일
/// 소유한다. 입력은 불변 read model 뿐이고 출력은 제안(suggested frame/콜아웃)뿐이라 sim 에 피드백할
/// 수 없다. 시계는 호출자가 주입한다(<c>nowSeconds</c>) — 벽시계 진행 + <b>일시정지 동안 동결</b>이
/// 계약이라 배속/step 버스트에서 시청자 기준 밀도가 유지되고 정지 화면에서 컷/만료가 흐르지 않는다.
/// 같은 (StepIndex, SequenceInStep) beat 은 한 재생 흐름 안에서 한 번만 반응한다(같은 step 의 중복
/// 전달 보험). 시크/되감기 후 재생은 <see cref="Reset"/>이 dedup 까지 청산하므로 의도적으로 다시
/// 발화한다 — 재시청은 하이라이트를 다시 보여준다. 상수는 V1 authority 튜닝 노브.
/// </summary>
public sealed class BattleSpectacleDirector
{
    /// <summary>카메라 컷(강조 대상 교체) 최소 간격 — 마스터 플랜 "한 번 컷하면 0.8s 안에 다시 컷하지 않는다".</summary>
    public const double CameraCutBudgetSeconds = 0.8;

    /// <summary>개전 직후 전술 보드 뷰 유지 구간(sim 시간) — 이 동안 카메라 강조 후보를 만들지 않는다.</summary>
    public const float OpeningBoardViewSimSeconds = 1.0f;

    /// <summary>강조 샷 유지 시간 — 만료되면 passive 보드 프레임으로 복귀.</summary>
    public const double EmphasisHoldSeconds = 1.3;

    /// <summary>카메라 강조 진입 최소 중요도 — 기본공격(10)/시너지(60)는 카메라를 훔치지 못한다.</summary>
    public const int CameraEmphasisMinImportance = 70;

    /// <summary>
    /// 승격되지 못한 카메라 후보가 뉴스 가치를 잃는 시한. 반드시 <see cref="EmphasisHoldSeconds"/> 보다
    /// 길어야 한다 — 짧으면 컷 직후 적재된 동급 중요도 후보(연쇄 킬)가 활성 샷에 막혀 있는 동안
    /// 시효로 폐기되어 어떤 프레임 타이밍에서도 보여질 수 없는 데드존이 생긴다(리뷰 확정 결함).
    /// </summary>
    public const double PendingShotStaleSeconds = 1.6;

    /// <summary>메이저 콜아웃 최소 간격 — 마스터 플랜 "1초당 메이저 UI 콜아웃 1개".</summary>
    public const double CalloutBudgetSeconds = 1.0;

    /// <summary>콜아웃 진입 최소 중요도 — 콤보 포석(55)/처치 흡수(55)는 피드 스팸이 되지 않는다.</summary>
    public const int CalloutMinImportance = 60;

    /// <summary>대기 콜아웃이 뉴스 가치를 잃는 시한.</summary>
    public const double PendingCalloutStaleSeconds = 2.5;

    // 킬은 beat 채널이 아니라 BattleEvent(Kill) — 기존 P2 극적 점수와 같은 대역으로 합산 비교한다.
    private const int BacklineDiveKillImportance = 100;
    private const int KillImportance = 90;

    private readonly record struct PendingShot(
        SpectacleShotKind ShotKind,
        int Importance,
        string? SubjectUnitId,
        string? RelatedUnitId,
        CombatVector2 FallbackPosition,
        double QueuedAtSeconds);

    private readonly record struct PendingCallout(CombatBeat Beat, double QueuedAtSeconds);

    private readonly HashSet<(int StepIndex, int Sequence)> _seenBeats = new();
    private readonly List<PendingCallout> _pendingCallouts = new();
    private PendingShot? _pendingShot;
    private SpectacleCameraEmphasis? _activeEmphasis;
    private double _lastCutAtSeconds = double.NegativeInfinity;
    private double _lastCalloutAtSeconds = double.NegativeInfinity;

    /// <summary>전투 시작/재시작/되감기 스냅샷 적용 시 — 모든 강조 상태를 청산한다(ClearTransients 계약).</summary>
    public void Reset()
    {
        _seenBeats.Clear();
        _pendingCallouts.Clear();
        _pendingShot = null;
        _activeEmphasis = null;
        _lastCutAtSeconds = double.NegativeInfinity;
        _lastCalloutAtSeconds = double.NegativeInfinity;
    }

    /// <summary>
    /// step 전환마다 1회(ConsumeTimelineTransition). beat 과 킬 이벤트를 후보로 적재한다 —
    /// 한 프레임에 여러 step 이 버스트로 들어와도 컷/콜아웃 예산은 시계 기준이라 밀도가 보존된다.
    /// </summary>
    public void IngestStep(BattleSimulationStep step, double nowSeconds)
    {
        if (step == null)
        {
            return;
        }

        IngestBeats(step, nowSeconds);
        IngestKillEvents(step, nowSeconds);
    }

    /// <summary>
    /// 매 프레임 — 활성 강조 샷이 있으면 반환한다. 새 후보는 컷 예산(0.8s)을 통과해야 승격되고,
    /// 활성 샷보다 중요도가 낮으면 활성 샷을 끊지 못한다.
    /// </summary>
    public bool TryGetCameraEmphasis(double nowSeconds, out SpectacleCameraEmphasis emphasis)
    {
        PromotePendingShot(nowSeconds);

        if (_activeEmphasis != null && nowSeconds < _activeEmphasis.ExpiresAtSeconds)
        {
            emphasis = _activeEmphasis;
            return true;
        }

        _activeEmphasis = null;
        emphasis = null!;
        return false;
    }

    /// <summary>
    /// 매 프레임 — 콜아웃 예산(1초 1건)이 허락하면 가장 중요한 대기 beat 하나를 내보낸다.
    /// 시효(2.5s)가 지난 대기 항목은 조용히 버린다(지나간 일을 늦게 외치지 않는다).
    /// </summary>
    public bool TryDrainCallout(double nowSeconds, out CombatBeat callout)
    {
        _pendingCallouts.RemoveAll(pending => nowSeconds - pending.QueuedAtSeconds > PendingCalloutStaleSeconds);

        if (_pendingCallouts.Count == 0 || nowSeconds - _lastCalloutAtSeconds < CalloutBudgetSeconds)
        {
            callout = null!;
            return false;
        }

        var bestIndex = 0;
        for (var i = 1; i < _pendingCallouts.Count; i++)
        {
            if (IsHigherPriorityCallout(_pendingCallouts[i].Beat, _pendingCallouts[bestIndex].Beat))
            {
                bestIndex = i;
            }
        }

        callout = _pendingCallouts[bestIndex].Beat;
        _pendingCallouts.RemoveAt(bestIndex);
        _lastCalloutAtSeconds = nowSeconds;
        return true;
    }

    private static bool IsHigherPriorityCallout(CombatBeat candidate, CombatBeat incumbent)
    {
        if (candidate.Importance != incumbent.Importance)
        {
            return candidate.Importance > incumbent.Importance;
        }

        // 같은 중요도면 먼저 일어난 사건이 먼저 — step, step 내 순서.
        if (candidate.StepIndex != incumbent.StepIndex)
        {
            return candidate.StepIndex < incumbent.StepIndex;
        }

        return candidate.SequenceInStep < incumbent.SequenceInStep;
    }

    private void IngestBeats(BattleSimulationStep step, double nowSeconds)
    {
        if (step.Beats == null || step.Beats.Count == 0)
        {
            return;
        }

        foreach (var beat in step.Beats)
        {
            if (!_seenBeats.Add((beat.StepIndex, beat.SequenceInStep)))
            {
                continue; // 같은 재생 흐름 안의 중복 전달 보험 — 시크/되감기 재발화는 Reset 소관.
            }

            if (beat.Importance >= CalloutMinImportance)
            {
                _pendingCallouts.Add(new PendingCallout(beat, nowSeconds));
            }

            if (beat.Importance >= CameraEmphasisMinImportance
                && step.TimeSeconds >= OpeningBoardViewSimSeconds)
            {
                // close-up 은 킬/클러치 전용(마스터 플랜) — 빈사 임계 발동(clutch)만 close, 콤보는 medium.
                var shotKind = beat.Type == CombatBeatType.HpThresholdEffect
                    ? SpectacleShotKind.Close
                    : SpectacleShotKind.Medium;
                OfferShot(new PendingShot(
                    shotKind,
                    beat.Importance,
                    beat.TargetId?.Value ?? beat.SourceId?.Value,
                    beat.SourceId?.Value,
                    beat.Position,
                    nowSeconds));
            }
        }
    }

    private void IngestKillEvents(BattleSimulationStep step, double nowSeconds)
    {
        if (step.TimeSeconds < OpeningBoardViewSimSeconds)
        {
            return;
        }

        foreach (var eventData in step.Events)
        {
            if (eventData.EventKind != BattleEventKind.Kill)
            {
                continue;
            }

            var importance = eventData.KillPayload is { IsBacklineDiveKill: true }
                ? BacklineDiveKillImportance
                : KillImportance;
            OfferShot(new PendingShot(
                SpectacleShotKind.Close,
                importance,
                eventData.TargetId?.Value,
                eventData.ActorId.Value,
                CombatVector2.Zero,
                nowSeconds));
        }
    }

    private void OfferShot(PendingShot candidate)
    {
        if (_pendingShot is { } incumbent && incumbent.Importance >= candidate.Importance)
        {
            return;
        }

        _pendingShot = candidate;
    }

    private void PromotePendingShot(double nowSeconds)
    {
        if (_pendingShot is not { } pending)
        {
            return;
        }

        if (nowSeconds - pending.QueuedAtSeconds > PendingShotStaleSeconds)
        {
            _pendingShot = null;
            return;
        }

        if (nowSeconds - _lastCutAtSeconds < CameraCutBudgetSeconds)
        {
            return;
        }

        if (_activeEmphasis != null
            && nowSeconds < _activeEmphasis.ExpiresAtSeconds
            && _activeEmphasis.Importance >= pending.Importance)
        {
            return; // 진행 중인 더 큰 사건을 작은 사건이 끊지 못한다.
        }

        _activeEmphasis = new SpectacleCameraEmphasis(
            pending.ShotKind,
            pending.Importance,
            pending.SubjectUnitId,
            pending.RelatedUnitId,
            pending.FallbackPosition,
            nowSeconds,
            nowSeconds + EmphasisHoldSeconds);
        _lastCutAtSeconds = nowSeconds;
        _pendingShot = null;
    }
}
