using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

/// <summary>
/// 활 유닛의 화살 nock 연출. P09 무기 팩은 Bow 그룹 자식으로 Arrow_001~003 메시를 출고하지만
/// 외형 카탈로그에 Arrow 옵션이 없어 어떤 코드 경로에서도 켜지지 않았다 — 궁수가 빈 시위만 당기는
/// 결함. 활 연출 문법(조준 Hold idle → Release → 재장전 Load 체인)에 맞춰 화살은 조준 중 기본
/// 표시되고, 기본공격의 release tick(투사체 출발)에 숨었다가 재장전 동작 타이밍에 다시 나타난다.
/// 화살은 Bow 그룹의 ParentConstraint(손 거치 플립)를 그대로 따라가므로 여기서는 GameObject
/// active만 다룬다. presentation 전용 — sim truth 무접촉.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleP09ArrowNockSurface : MonoBehaviour
{
    private const float FixedStepSeconds = 0.1f;
    private const float FallbackHideSeconds = 0.1f;
    // release(숨김) 후 화살이 다시 보일 때까지 — Release follow-through + 재장전 Load 체인이
    // 새 화살을 메기는 구간에 맞춘 고정 박자.
    private const float ReNockDelaySeconds = 1.0f;

    private readonly List<GameObject> _arrows = new();
    private GameObject? _nockArrow;
    private float _hideInSeconds;
    private float _renockInSeconds;

    /// <summary>표시 중인 nock 화살 검증용.</summary>
    internal bool IsArrowVisibleForTests => _nockArrow != null && _nockArrow.activeSelf;

    public void Configure(Transform? modelRoot, BattleUnitReadModel actor)
    {
        _arrows.Clear();
        _nockArrow = null;
        _hideInSeconds = 0f;
        _renockInSeconds = 0f;
        if (modelRoot == null
            || BattleBasicAttackPresentationClassifier.Resolve(actor) != BattleBasicAttackPresentationKind.Bow)
        {
            return;
        }

        foreach (var child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.StartsWith("Arrow_", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (child.GetComponentInChildren<Renderer>(true) == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            _arrows.Add(child.gameObject);
        }

        _nockArrow = _arrows.Count > 0 ? _arrows[0] : null;
        // 조준 대기(드로운 Hold idle)부터 화살이 메겨져 있어야 한다 — 표시가 기본 상태.
        if (_nockArrow != null)
        {
            _nockArrow.SetActive(true);
        }
    }

    public void ConsumeCue(BattlePresentationCue cue)
    {
        if (_nockArrow == null || cue.CueType != BattlePresentationCueType.ActionCommitBasic)
        {
            return;
        }

        // release tick(투사체 출발, driver의 원거리 핀·VFX 비행 지연과 같은 상수)에 숨겨
        // 비행 투사체와 바통터치한다. 스케줄이 없으면 짧은 폴백 후 숨김.
        if (cue.CommitSchedule is { } schedule)
        {
            var releaseTick = BattleContactPinScheduler.ResolvePresentationReleaseTick(
                schedule.WindupStartTick, schedule.ContactTick, BattleContactPinScheduler.DefaultProjectileTravelTicks);
            _hideInSeconds = Mathf.Max(0.05f, (releaseTick - schedule.WindupStartTick) * FixedStepSeconds);
        }
        else
        {
            _hideInSeconds = FallbackHideSeconds;
        }

        _renockInSeconds = 0f;
        _nockArrow.SetActive(true);
    }

    public void Tick(float deltaTime, float playbackSpeed, bool paused)
    {
        if (paused || _nockArrow == null)
        {
            return;
        }

        var advance = Mathf.Max(0f, deltaTime) * Mathf.Max(0.05f, playbackSpeed);
        if (_hideInSeconds > 0f)
        {
            _hideInSeconds -= advance;
            if (_hideInSeconds <= 0f)
            {
                _nockArrow.SetActive(false);
                _renockInSeconds = ReNockDelaySeconds;
            }

            return;
        }

        if (_renockInSeconds > 0f)
        {
            _renockInSeconds -= advance;
            if (_renockInSeconds <= 0f)
            {
                _nockArrow.SetActive(true);
            }
        }
    }

    public void ClearTransientState()
    {
        _hideInSeconds = 0f;
        _renockInSeconds = 0f;
        // seek/reset 후 기본 상태(조준 + 화살 메김)로 복귀.
        if (_nockArrow != null)
        {
            _nockArrow.SetActive(true);
        }
    }
}
