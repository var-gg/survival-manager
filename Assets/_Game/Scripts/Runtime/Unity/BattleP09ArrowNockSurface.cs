using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

/// <summary>
/// 활 유닛의 화살 nock 연출. P09 무기 팩은 Bow 그룹 자식으로 Arrow_001~003 메시를 출고하지만
/// 외형 카탈로그에 Arrow 옵션이 없어 어떤 코드 경로에서도 켜지지 않았다 — 궁수가 빈 시위만 당기는
/// 결함. 이 surface는 기본공격 commit cue(WindupStartTick 발화)에 화살을 켜고, 투사체가 출발하는
/// release tick에 숨겨 비행 VFX와 바통터치한다. 화살은 Bow 그룹의 ParentConstraint(손 거치 플립)를
/// 그대로 따라가므로 여기서는 GameObject active만 다룬다. presentation 전용 — sim truth 무접촉.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleP09ArrowNockSurface : MonoBehaviour
{
    private const float FixedStepSeconds = 0.1f;
    private const float FallbackNockSeconds = 0.3f;

    private readonly List<GameObject> _arrows = new();
    private GameObject? _nockArrow;
    private float _hideRemainingSeconds;

    /// <summary>표시 중인 nock 화살 검증용.</summary>
    internal bool IsArrowVisibleForTests => _nockArrow != null && _nockArrow.activeSelf;

    public void Configure(Transform? modelRoot, BattleUnitReadModel actor)
    {
        _arrows.Clear();
        _nockArrow = null;
        _hideRemainingSeconds = 0f;
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
    }

    public void ConsumeCue(BattlePresentationCue cue)
    {
        if (_nockArrow == null || cue.CueType != BattlePresentationCueType.ActionCommitBasic)
        {
            return;
        }

        // windup 시작(cue 발화)부터 release tick까지 시위에 화살을 보여준다. release 산식은
        // driver의 원거리 핀 / VFX 비행 지연과 같은 상수를 공유해야 출발 순간이 일치한다.
        if (cue.CommitSchedule is { } schedule)
        {
            var releaseTick = BattleContactPinScheduler.ResolvePresentationReleaseTick(
                schedule.WindupStartTick, schedule.ContactTick, BattleContactPinScheduler.DefaultProjectileTravelTicks);
            _hideRemainingSeconds = Mathf.Max(0.05f, (releaseTick - schedule.WindupStartTick) * FixedStepSeconds);
        }
        else
        {
            _hideRemainingSeconds = FallbackNockSeconds;
        }

        _nockArrow.SetActive(true);
    }

    public void Tick(float deltaTime, float playbackSpeed, bool paused)
    {
        if (paused || _nockArrow == null || _hideRemainingSeconds <= 0f)
        {
            return;
        }

        _hideRemainingSeconds -= Mathf.Max(0f, deltaTime) * Mathf.Max(0.05f, playbackSpeed);
        if (_hideRemainingSeconds <= 0f)
        {
            _nockArrow.SetActive(false);
        }
    }

    public void ClearTransientState()
    {
        _hideRemainingSeconds = 0f;
        if (_nockArrow != null)
        {
            _nockArrow.SetActive(false);
        }
    }
}
