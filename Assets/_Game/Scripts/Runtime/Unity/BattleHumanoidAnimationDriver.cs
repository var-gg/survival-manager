using SM.Combat.Model;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SM.Unity;

[DisallowMultipleComponent]
public sealed class BattleHumanoidAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator = null!;
    [SerializeField] private BattleHumanoidAnimationSet animationSet = null!;
    // 회귀(2026-06): 미검증 presentation root motion(메시 슬롯에 최대 0.45u 오프셋 누적)이 몸을 권위 위치에서
    // 벗어나게 해 "사거리 밖처럼 보이는데 데미지" + 멈출 때 1프레임 snap pop을 유발했다. 권위 보간 위치에 메시를
    // 정확히 앉히도록 기능을 끈다(commit 7594c75c 통째 revert 대신 외과적 비활성화). foot-slide가 거슬리면
    // cadence(authoredLocomotionSpeed)를 PlayMode로 정합한 뒤 재활성화한다.
    [SerializeField] private bool usePresentationRootMotion = false;
    [SerializeField] private bool clearRuntimeAnimatorController = true;
    [SerializeField] private bool forceAlwaysAnimate;
    [SerializeField, Min(0.02f)] private float minimumOneShotSeconds = 0.12f;
    [SerializeField, Min(0.02f)] private float maxRootMotionVisualOffset = 0.45f;
    // 피격 리액션 one-shot의 시작 지점(초). Kevin Damage 클립들의 리코일 apex가 0.15~0.3s 부근이라
    // 이만큼 건너뛰고 시작해야 hitstop 고정·빠른 교체 속에서도 "맞은 포즈"가 화면에 보인다.
    [SerializeField, Min(0f)] private float reactionPoseLeadSeconds = 0.18f;

    // 게이트 해제/걷기↔달리기 전환의 1프레임 포즈 스냅 수술: 루프 레이어는 2슬롯 서브믹서로
    // 크로스페이드한다. one-shot 엔벨로프는 바깥 _mixer(input 0=루프층, 1=one-shot)의 계약 그대로.
    private const float LoopCrossfadeSeconds = 0.18f;
    // Knockdown(눕는 클립)만 꼬리 블렌드를 길게 — 0.10s 스왑이 prone→upright 1프레임 복귀를 만들었다.
    private const float KnockdownBlendOutSeconds = 0.45f;

    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private AnimationMixerPlayable _loopMixer;
    private readonly AnimationClipPlayable[] _loopSlots = new AnimationClipPlayable[2];
    private int _activeLoopSlot;
    private float _loopFadeElapsed;
    private float _loopFadeDuration;
    private AnimationClipPlayable _oneShotPlayable;
    private AnimationClip? _loopClip;
    private AnimationClip? _oneShotClip;
    private BattleUnitReadModel? _lastState;
    private BattleHumanoidAnimationSet? _resolvedAnimationSet;
    private float _oneShotRemaining;
    private float _oneShotElapsed;
    private BattleBlendEnvelope _blendEnvelope = BattleBlendEnvelope.InstantFull;
    private bool _isPinnedCommit;
    private BattleContactPinPlan _pinPlan = BattleContactPinPlan.None;
    private int _pinWindupStartTick;
    private float _pinTotalSeconds;
    private ActionInstanceId _pinActionInstanceId = ActionInstanceId.None;
    // 활 commit(Release 클립)이 자연 종료되면 이어 붙일 재장전 draw(Load) 클립. windup ~0.22s에는
    // 1초짜리 당김이 물리적으로 안 들어가므로 당김 모션은 발사 후 재장전 자리에서 재생한다
    // (조준 Hold idle → Release → Load 체인 → Hold 복귀). 취소된 commit은 체인하지 않는다.
    private AnimationClip? _pinFollowUpClip;
    private BattleHitstopWindow _hitstopWindow = BattleHitstopWindow.None;
    private double _lastSampleElapsed;
    // 속도 분리 계약(러닝머신 수술): _playbackSpeed는 타임라인 배속(Tick/ConsumeCue가 기록),
    // _locomotionCadence는 발-변위 정합 배율(ApplyState가 locomoting일 때만 기록). 단일 필드를
    // SetBlend→TickTransients 두 호출자가 last-write-wins로 다투면서 cadence가 매 프레임
    // 무효화되던 회귀의 직접 원인 제거 — 루프 재생속도는 두 값의 곱이다.
    private float _playbackSpeed = 1f;
    private float _locomotionCadence = 1f;
    private float _lastWorldSpeed;
    private bool _oneShotIsMobility;
    private bool _isDeathOneShotActive;
    private Transform? _rootMotionTarget;
    private Vector3 _rootMotionTargetBaseLocalPosition;
    private Vector3 _rootMotionVisualOffsetWorld;
    private Vector3 _authoritativeRootMotionFrameDeltaWorld;
    private bool _hasRootMotionTargetBase;
    private bool _acceptRootMotionFrame;
    private BattlePresentationRootMotionRelay? _rootMotionRelay;

    // Mirrors BattleSimulator.DefaultFixedStepSeconds (the sim's fixed tick). The contact-pin lives in the
    // scaled clock where one tick advances this many seconds in BOTH step dispatch and clip playback.
    private const float FixedStepSeconds = 0.1f;
    private bool _lastIsLocomoting;
    private bool _isHoldingTerminalPose;
    private BattleActorPresentationPhase _lastPresentationPhase = BattleActorPresentationPhase.CombatReady;

    public BattleHumanoidAnimationSet? AnimationSet => animationSet;
    public BattleHumanoidAnimationSet? ActiveAnimationSet => ResolveAnimationSet();
    public AnimationClip? CurrentLoopClip => _loopClip;
    public AnimationClip? CurrentOneShotClip => _oneShotClip;

    /// <summary>리액션 시작 오프셋 검증용 — 현재 one-shot 플레이어블의 클립 로컬 시간(초).</summary>
    internal double CurrentOneShotClipTimeForTests => _oneShotPlayable.IsValid() ? _oneShotPlayable.GetTime() : -1d;

    /// <summary>속도 분리 검증용 — 활성 루프 슬롯의 실제 재생속도(타임라인 × cadence).</summary>
    internal float LoopPlaybackSpeedForTests
        => _loopSlots[_activeLoopSlot].IsValid() ? (float)_loopSlots[_activeLoopSlot].GetSpeed() : -1f;

    /// <summary>루프 크로스페이드 검증용 — 페이드 중인 outgoing 슬롯이 살아 있는가.</summary>
    internal bool HasOutgoingLoopForTests => _loopSlots[1 - _activeLoopSlot].IsValid();

    /// <summary>원거리 release 핀 검증용 — 핀 커밋의 contact 정렬 budget(초).</summary>
    internal double PinBudgetSecondsForTests => _isPinnedCommit ? _pinPlan.BudgetSeconds : -1d;
    public bool IsHoldingTerminalPose => _isHoldingTerminalPose;
    public Vector3 PresentationRootMotionOffsetWorld => _rootMotionVisualOffsetWorld;
    public int CuePlaybackCount { get; private set; }

    public void ConfigureAnimationSet(BattleHumanoidAnimationSet set)
    {
        animationSet = set;
        _resolvedAnimationSet = null;
        ReapplyLastState();
    }

    public void Initialize(BattleActorWrapper wrapper, BattleUnitReadModel actor)
    {
        _lastState = actor;
        ResolveAnimator(wrapper);
        ConfigurePresentationRootMotion(wrapper);
        ConfigureAnimator();
        ApplyState(actor, _playbackSpeed, paused: false);
    }

    public void ApplyState(BattleUnitReadModel state, float playbackSpeed, bool paused)
    {
        ApplyState(state, playbackSpeed, paused, isLocomoting: false);
    }

    public void ApplyState(BattleUnitReadModel state, float playbackSpeed, bool paused, bool isLocomoting)
    {
        ApplyState(state, playbackSpeed, paused, isLocomoting, BattleActorPresentationPhase.CombatReady);
    }

    public void ApplyState(
        BattleUnitReadModel state,
        float playbackSpeed,
        bool paused,
        bool isLocomoting,
        BattleActorPresentationPhase presentationPhase)
    {
        ApplyState(
            state,
            playbackSpeed,
            paused,
            isLocomoting,
            presentationPhase,
            worldSpeed: 0f,
            authoritativeFrameDeltaWorld: Vector3.zero);
    }

    public void ApplyState(
        BattleUnitReadModel state,
        float playbackSpeed,
        bool paused,
        bool isLocomoting,
        BattleActorPresentationPhase presentationPhase,
        float worldSpeed,
        Vector3 authoritativeFrameDeltaWorld)
    {
        _lastState = state;
        _lastIsLocomoting = isLocomoting;
        _lastPresentationPhase = presentationPhase;
        _lastWorldSpeed = worldSpeed;
        // 이 인자는 cadence다(타임라인 배속 아님) — 정지 유닛에 0.35x 하한이 적용되어 idle이
        // 근정지로 재생되던 결함까지 같이 차단(비로코모션은 1.0 고정).
        _locomotionCadence = isLocomoting ? ResolvePlaybackSpeed(playbackSpeed) : 1f;
        ApplyStateCore(state, paused, isLocomoting, presentationPhase, worldSpeed, authoritativeFrameDeltaWorld);
    }

    private void ApplyStateCore(
        BattleUnitReadModel state,
        bool paused,
        bool isLocomoting,
        BattleActorPresentationPhase presentationPhase,
        float worldSpeed,
        Vector3 authoritativeFrameDeltaWorld)
    {
        var activeSet = ResolveAnimationSet();
        if (activeSet == null || !EnsureGraph())
        {
            return;
        }

        if (!IsTerminalState(state) && _isHoldingTerminalPose)
        {
            _isHoldingTerminalPose = false;
            StopOneShot();
        }

        if (IsTerminalState(state) && activeSet.TryResolveLoopClip(state, isLocomoting: false, out var terminalClip))
        {
            BeginPresentationRootMotionFrame(isLocomoting: false, Vector3.zero);
            if (_oneShotRemaining <= 0f)
            {
                PlayTerminalPose(terminalClip);
            }

            ApplyPlayableSpeed(paused);
            return;
        }

        BeginPresentationRootMotionFrame(isLocomoting, authoritativeFrameDeltaWorld);

        if (_oneShotRemaining <= 0f && activeSet.TryResolveLoopClip(state, isLocomoting, presentationPhase, worldSpeed, out var loopClip))
        {
            PlayLoop(loopClip);
        }

        ApplyPlayableSpeed(paused);
    }

    // one-shot 종료/핀 해제/transient 정리 후 마지막 권위 상태로 복귀. cadence는 마지막
    // ApplyState가 기록한 값을 보존한다(타임라인 배속을 cadence로 오기록하지 않는다).
    private void ReapplyLastState()
    {
        if (_lastState != null)
        {
            ApplyStateCore(_lastState, paused: false, _lastIsLocomoting, _lastPresentationPhase, _lastWorldSpeed, Vector3.zero);
        }
    }

    public void ConsumeCue(BattlePresentationCue cue, BattleUnitReadModel state, float playbackSpeed)
    {
        // GPT Pro D2-C: a cancel interrupts the matching scheduled commit one-shot (keyed by
        // ActionInstanceId, not actor id) — no gameplay/reaction, no ghost contact pose.
        if (cue.CueType == BattlePresentationCueType.ActionCanceled)
        {
            if (_isPinnedCommit && cue.CommitSchedule is { } canceled && _pinActionInstanceId.Equals(canceled.ActionInstanceId))
            {
                // 취소된 발사는 재장전 follow-through를 보여주지 않는다.
                _pinFollowUpClip = null;
                EndPinnedCommit();
            }

            return;
        }

        _lastState = state;
        _playbackSpeed = ResolvePlaybackSpeed(playbackSpeed);

        var activeSet = ResolveAnimationSet();
        if (activeSet == null || !EnsureGraph())
        {
            return;
        }

        if (activeSet.TryResolveCueClip(cue, state, out var clip))
        {
            // 사망 one-shot의 최종 발언권: 같은 step의 살해 ImpactDamage 리액션이 방금 시작한
            // death 낙하를 교체하면 '리코일 재생→직립 마지막 프레임 고정→눕기 1프레임 스냅'으로
            // 끝나고 쓰러지는 모션은 영영 안 나온다. cue 소비 시점 state는 직전 step의 alive라
            // 상태 게이트는 불가능 — 반드시 이 플래그로 막는다.
            if (cue.CueType == BattlePresentationCueType.ImpactDamage && _isDeathOneShotActive)
            {
                return;
            }

            var timing = BattleClipTimingCatalog.Resolve(cue.AnimationSemantic);
            if (cue.CommitSchedule is { } schedule)
            {
                PlayPinnedCommit(clip, timing, schedule, cue.AnimationSemantic);
                // 활 commit이 자연 종료되면 재장전 draw(Load)를 이어 붙인다 — 시위 당김 모션이
                // 재생되는 유일한 자리. 클립은 commit 시점에 미리 해석해 둔다(종료 시점 해석 금지).
                if (cue.AnimationSemantic == BattleAnimationSemantic.BowShot)
                {
                    var drawCue = cue with
                    {
                        CueType = BattlePresentationCueType.WindupEnter,
                        AnimationSemantic = BattleAnimationSemantic.BowDraw,
                        CommitSchedule = null,
                    };
                    _pinFollowUpClip = activeSet.TryResolveCueClip(drawCue, state, out var drawClip) ? drawClip : null;
                }
            }
            else
            {
                // 피격 리액션 중 리코일 계열만 리코일 전개 지점에서 시작. Dodge/Knockdown 같은
                // 전신 변위 클립까지 0.18s를 건너뛰면 살아있는 유닛에 첫 프레임부터 수평 포즈가
                // 번쩍인다(lying flash). 사망/가드/이동은 0초부터.
                var startOffset = cue.CueType == BattlePresentationCueType.ImpactDamage && IsRecoilReaction(cue.AnimationSemantic)
                    ? Mathf.Min(reactionPoseLeadSeconds, clip.length * 0.4f)
                    : 0f;
                var blendOutSeconds = cue.AnimationSemantic == BattleAnimationSemantic.Knockdown
                    ? KnockdownBlendOutSeconds
                    : BattleOneShotBlendResolver.DefaultBlendOutSeconds;
                // 이동성 one-shot(접근 대시 등)은 RepositionStart 유래만 — DashEngage semantic은
                // 공격 lunge 커밋과 겹쳐 쓰이므로 cue type + 스케줄 부재로 판별해야 안전하다.
                var isMobility = cue.CueType == BattlePresentationCueType.RepositionStart
                                 && cue.AnimationSemantic is BattleAnimationSemantic.DashEngage
                                     or BattleAnimationSemantic.BackstepDisengage
                                     or BattleAnimationSemantic.LateralStrafe;
                PlayOneShot(clip, timing, startOffset, isMobility, blendOutSeconds);
                _isDeathOneShotActive = cue.CueType == BattlePresentationCueType.DeathStart;
            }
        }
    }

    private static bool IsRecoilReaction(BattleAnimationSemantic semantic)
    {
        return semantic is BattleAnimationSemantic.None
            or BattleAnimationSemantic.HitLight
            or BattleAnimationSemantic.HitHeavy
            or BattleAnimationSemantic.CriticalImpact
            or BattleAnimationSemantic.BlockImpact;
    }

    /// <summary>
    /// Drive a contact-pinned commit one-shot from the fixed-step clock (GPT Pro D2 guard B). Called per
    /// render frame with the current step index and intra-step alpha, it samples the clip at
    /// <c>clipLocal(elapsed)</c> where <c>elapsed = ((step − windupStart) + alpha) · dt</c> — an absolute
    /// anchor, never an accumulated delta — so the contact frame lands on the damage tick at any framerate,
    /// catch-up batching, or pause/resume.
    /// </summary>
    public void EvaluateContactPin(int currentStepIndex, float alpha)
    {
        if (!_isPinnedCommit || !_oneShotPlayable.IsValid() || !_mixer.IsValid())
        {
            return;
        }

        var elapsed = BattleContactPinPlanner.ElapsedAtStep(_pinWindupStartTick, currentStepIndex, alpha, FixedStepSeconds);
        _lastSampleElapsed = elapsed;
        // Lifetime is driven by the LIVE choreography time: the commit ends at the same schedule time
        // whether or not a hitstop held its pose in between (the hold is absorbed by the catch-up).
        if (elapsed >= _pinTotalSeconds)
        {
            EndPinnedCommit();
            return;
        }

        // Hitstop (Stage 5) holds only the OUTPUT pose — sample the clip at the remapped time, never freeze
        // the clock (J25). Outside a window ResolveOutputTime returns `elapsed` unchanged.
        var sampled = BattleHitstop.ResolveOutputTime(elapsed, _hitstopWindow);
        _oneShotPlayable.SetTime(_pinPlan.ClipLocalTimeAt(sampled));
        var weight = _blendEnvelope.WeightAt((float)sampled);
        _mixer.SetInputWeight(1, weight);
        _mixer.SetInputWeight(0, HasLoopSource ? 1f - weight : 0f);
    }

    private bool HasLoopSource => _loopSlots[_activeLoopSlot].IsValid();

    /// <summary>
    /// Begin a contact hitstop (Stage 5): hold the current one-shot's output pose for a few frames of
    /// "punch", then catch-up blend back to live. Output-only — never touches the sim, the schedule, or
    /// the global timescale (J6/J15/J25). The attacker's strike pins its hold at the contact frame; the
    /// target's hit-react holds from its impact frame.
    /// </summary>
    public void StartHitstop(BattleAnimationIntensity intensity)
    {
        if (!_oneShotPlayable.IsValid())
        {
            return;
        }

        var contactTime = _isPinnedCommit ? _pinPlan.BudgetSeconds : _oneShotElapsed;
        _hitstopWindow = BattleHitstop.Merge(_hitstopWindow, BattleHitstopCatalog.ResolveWindow(contactTime, intensity));
    }

    public void Tick(float deltaTime, float playbackSpeed, bool paused)
    {
        _playbackSpeed = ResolvePlaybackSpeed(playbackSpeed);

        if (!IsGraphUsable())
        {
            return;
        }

        ApplyPlayableSpeed(paused);
        if (paused)
        {
            return;
        }

        // 루프 크로스페이드는 핀 커밋 중에도 진행한다(커밋 뒤편의 루프층이 페이드 중일 수 있다).
        if (_loopFadeDuration > 0f && _loopFadeElapsed < _loopFadeDuration)
        {
            _loopFadeElapsed += Mathf.Max(0f, deltaTime) * _playbackSpeed;
            ApplyLoopMixWeights();
        }

        // A contact-pinned commit is driven by EvaluateContactPin from the fixed-step anchor, never by this
        // accumulating clock (GPT Pro guard B). Tick only keeps its playable speed pinned at 0.
        if (_isPinnedCommit)
        {
            return;
        }

        WrapLoopPlayback();
        if (_oneShotRemaining <= 0f)
        {
            return;
        }

        var advance = Mathf.Max(0f, deltaTime) * _playbackSpeed * (_oneShotIsMobility ? _locomotionCadence : 1f);
        _oneShotElapsed += advance;
        _oneShotRemaining = Mathf.Max(0f, _oneShotRemaining - advance);
        if (_oneShotRemaining > 0f)
        {
            if (!_isHoldingTerminalPose)
            {
                if (_hitstopWindow.IsActiveAt(_oneShotElapsed))
                {
                    // Hold the recoil pose for the punch. ApplyPlayableSpeed pins the speed to 0 during the
                    // window, so sampling the remapped time is authoritative (Stage 5, J25).
                    var sampled = BattleHitstop.ResolveOutputTime(_oneShotElapsed, _hitstopWindow);
                    if (_oneShotPlayable.IsValid())
                    {
                        _oneShotPlayable.SetTime(sampled);
                    }

                    ApplyBlendWeights((float)sampled);
                }
                else
                {
                    ApplyBlendWeights(_oneShotElapsed);
                }
            }

            return;
        }

        if (_lastState != null && IsTerminalState(_lastState) && _oneShotPlayable.IsValid())
        {
            HoldOneShotFinalFrame();
            return;
        }

        StopOneShot();
        ReapplyLastState();
    }

    public void ClearTransientState(BattlePresentationCueType reason)
    {
        _oneShotRemaining = 0f;
        StopOneShot();
        ReapplyLastState();
    }

    internal void DisposePresentationGraphForTests()
    {
        DestroyGraph();
    }

    // presentation root motion은 기본 비활성(회귀 방지). 메커니즘 검증 테스트는 이 훅으로 opt-in한다.
    // Initialize/ConfigureAnimator보다 먼저 호출해야 한다.
    internal void SetPresentationRootMotionEnabledForTests(bool enabled)
    {
        usePresentationRootMotion = enabled;
    }

    private void OnDisable()
    {
        DestroyGraph();
    }

    private void OnDestroy()
    {
        DestroyGraph();
    }

    private void OnApplicationQuit()
    {
        DestroyGraph();
    }

    private void ResolveAnimator(BattleActorWrapper wrapper)
    {
        if (animator != null)
        {
            return;
        }

        animator = wrapper.VendorVisualSlot.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = wrapper.VisualRoot.GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = usePresentationRootMotion && _rootMotionTarget != null;

        if (clearRuntimeAnimatorController && animator.runtimeAnimatorController != null)
        {
            animator.runtimeAnimatorController = null;
        }

        animator.cullingMode = forceAlwaysAnimate
            ? AnimatorCullingMode.AlwaysAnimate
            : AnimatorCullingMode.CullUpdateTransforms;
    }

    private void ConfigurePresentationRootMotion(BattleActorWrapper wrapper)
    {
        _rootMotionTarget = wrapper.VendorVisualSlot != wrapper.transform
            ? wrapper.VendorVisualSlot
            : null;
        CaptureRootMotionTargetBase();

        if (!usePresentationRootMotion || animator == null)
        {
            return;
        }

        _rootMotionRelay = animator.GetComponent<BattlePresentationRootMotionRelay>();
        if (_rootMotionRelay == null)
        {
            _rootMotionRelay = animator.gameObject.AddComponent<BattlePresentationRootMotionRelay>();
        }

        _rootMotionRelay.Configure(animator, this);
    }

    private BattleHumanoidAnimationSet? ResolveAnimationSet()
    {
        if (animationSet != null)
        {
            return animationSet;
        }

        _resolvedAnimationSet ??= BattleHumanoidAnimationSet.ResolveRuntimeSet(null);
        return _resolvedAnimationSet;
    }

    private bool EnsureGraph()
    {
        if (animator == null)
        {
            return false;
        }

        if (_graph.IsValid() && _mixer.IsValid())
        {
            return true;
        }

        _graph = PlayableGraph.Create($"{nameof(BattleHumanoidAnimationDriver)}:{gameObject.name}");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        _mixer = AnimationMixerPlayable.Create(_graph, 2, normalizeWeights: true);
        // 루프층 서브믹서: 슬롯 2개를 핑퐁하며 클립 교체를 크로스페이드한다. 바깥 _mixer의
        // input 0(루프층)↔input 1(one-shot) 가중치 계약(J5 엔벨로프)은 그대로 격리된다.
        _loopMixer = AnimationMixerPlayable.Create(_graph, 2, normalizeWeights: true);
        _graph.Connect(_loopMixer, 0, _mixer, 0);
        var output = AnimationPlayableOutput.Create(_graph, "BattleHumanoidAnimation", animator);
        output.SetSourcePlayable(_mixer);
        _graph.Play();
        return true;
    }

    private bool IsGraphUsable()
    {
        return _graph.IsValid() && _mixer.IsValid();
    }

    private void PlayLoop(AnimationClip clip)
    {
        if (clip == null || _loopClip == clip)
        {
            return;
        }

        // 루프↔루프 크로스페이드: 기존 하드 스왑(Disconnect→weight 1 즉시)이 게이트 해제·
        // relaxedIdle→idle phase 전환·걷기↔달리기 스왑의 1프레임 포즈 스냅을 만들었다.
        var hadPrevious = _loopSlots[_activeLoopSlot].IsValid();
        var incomingSlot = hadPrevious ? 1 - _activeLoopSlot : _activeLoopSlot;
        DisconnectLoopSlot(incomingSlot);
        var playable = CreateClipPlayable(clip);
        _loopSlots[incomingSlot] = playable;
        _graph.Connect(playable, 0, _loopMixer, incomingSlot);
        _activeLoopSlot = incomingSlot;
        _loopClip = clip;
        _loopFadeDuration = hadPrevious ? LoopCrossfadeSeconds : 0f;
        _loopFadeElapsed = 0f;
        ApplyLoopMixWeights();
        _mixer.SetInputWeight(0, _oneShotPlayable.IsValid() ? 0f : 1f);
    }

    private void ApplyLoopMixWeights()
    {
        if (!_loopMixer.IsValid())
        {
            return;
        }

        var progress = _loopFadeDuration <= 0f ? 1f : Mathf.Clamp01(_loopFadeElapsed / _loopFadeDuration);
        var outgoingSlot = 1 - _activeLoopSlot;
        var hasOutgoing = _loopSlots[outgoingSlot].IsValid();
        _loopMixer.SetInputWeight(_activeLoopSlot, hasOutgoing ? progress : 1f);
        if (!hasOutgoing)
        {
            return;
        }

        _loopMixer.SetInputWeight(outgoingSlot, 1f - progress);
        if (progress >= 1f)
        {
            DisconnectLoopSlot(outgoingSlot);
            _loopMixer.SetInputWeight(_activeLoopSlot, 1f);
        }
    }

    private void DisconnectLoopSlot(int slot)
    {
        if (!_loopSlots[slot].IsValid())
        {
            return;
        }

        if (_loopMixer.IsValid())
        {
            _loopMixer.DisconnectInput(slot);
            _loopMixer.SetInputWeight(slot, 0f);
        }

        _loopSlots[slot].Destroy();
        _loopSlots[slot] = default;
    }

    private void PlayOneShot(
        AnimationClip clip,
        BattleClipTiming timing,
        float startOffsetSeconds = 0f,
        bool isMobility = false,
        float blendOutSeconds = BattleOneShotBlendResolver.DefaultBlendOutSeconds)
    {
        if (clip == null)
        {
            return;
        }

        DisconnectPlayable(ref _oneShotPlayable, inputIndex: 1);
        _oneShotPlayable = CreateClipPlayable(clip);
        _graph.Connect(_oneShotPlayable, 0, _mixer, 1);
        _oneShotClip = clip;
        _isHoldingTerminalPose = false;
        _oneShotIsMobility = isMobility;
        _isDeathOneShotActive = false;
        // 핀 상태 청산: 자기 커밋 핀이 살아있는 채로 이 one-shot(특히 death)이 레이어를 차지하면
        // stale 핀이 하이재킹한다 — Tick은 핀 가드로 전진을 멈추고(서서 정지), EvaluateContactPin은
        // 핀 만료 순간 StopOneShot으로 플레이어블을 파괴해 마지막 프레임으로 점프시킨다
        // ("가만히 있다가 갑자기 누움"). 레이어 소유권이 넘어올 때 핀 소유권도 함께 청산한다.
        _isPinnedCommit = false;
        _pinActionInstanceId = ActionInstanceId.None;
        _pinFollowUpClip = null;
        // 피격 리액션 가시화: 리액션 one-shot은 클립 0초(중립 포즈)가 아니라 리코일이 전개된
        // 지점에서 시작할 수 있다. 타겟 hitstop이 시작 시점 포즈를 고정하고(StartHitstop의
        // contactTime = _oneShotElapsed) 다음 액션이 리액션을 수십 ms 안에 교체하는 전장에서,
        // 0초 시작은 "중립 포즈 고정 → 교체"로 끝나 리코일이 화면에 한 번도 안 나온다.
        var offset = Mathf.Clamp(startOffsetSeconds, 0f, Mathf.Max(0f, clip.length - 0.05f));
        // 수명/엔벨로프는 전부 클립 로컬 초 도메인 — Tick이 dt×재생속도로 클립 로컬을 전진시키므로
        // 어떤 배속/cadence에서도 일관된다(기존 /speed 나눗셈은 배속≠1에서 수명 단위 불일치였다).
        _oneShotRemaining = Mathf.Max(minimumOneShotSeconds, clip.length - offset);
        _oneShotElapsed = offset;
        if (offset > 0f)
        {
            _oneShotPlayable.SetTime(offset);
        }

        _hitstopWindow = BattleHitstopWindow.None;
        // Stage 4 blend driver (GPT Pro J5): ramp the one-shot layer in/out across the contact window
        // instead of the old instant 1<->0 weight swap that popped the layer (D1). Weight is full from
        // (contact - lead) through (contact + hold), then fades back to the loop at the tail.
        _blendEnvelope = BattleOneShotBlendResolver.Resolve(
            clip.length,
            playbackSpeed: 1f,
            timing,
            BattleOneShotBlendResolver.DefaultBlendInSeconds,
            blendOutSeconds);
        ApplyBlendWeights(offset);
        ApplyPlayableSpeed(paused: false);
        CuePlaybackCount++;
    }

    private void ApplyBlendWeights(float elapsed)
    {
        if (!_mixer.IsValid() || !_oneShotPlayable.IsValid())
        {
            return;
        }

        var weight = _blendEnvelope.WeightAt(elapsed);

        // A terminal (death) one-shot holds its pose into the freeze rather than fading back to the loop.
        if (_lastState != null && IsTerminalState(_lastState) && elapsed >= _blendEnvelope.BlendInEndSeconds)
        {
            weight = 1f;
        }

        _mixer.SetInputWeight(1, weight);
        _mixer.SetInputWeight(0, HasLoopSource ? 1f - weight : 0f);
    }

    private void PlayPinnedCommit(AnimationClip clip, BattleClipTiming timing, BattleCommitSchedule schedule, BattleAnimationSemantic semantic)
    {
        if (clip == null)
        {
            return;
        }

        DisconnectPlayable(ref _oneShotPlayable, inputIndex: 1);
        _oneShotPlayable = CreateClipPlayable(clip);
        // GPT Pro D2-R4 / guard B: the clip time is driven manually from the step anchor each frame, never
        // auto-advanced and never speed-warped for the pin.
        _oneShotPlayable.SetSpeed(0d);
        _graph.Connect(_oneShotPlayable, 0, _mixer, 1);
        _oneShotClip = clip;
        _isHoldingTerminalPose = false;
        _oneShotIsMobility = false;
        _isDeathOneShotActive = false;
        _pinFollowUpClip = null;

        _isPinnedCommit = true;
        _pinWindupStartTick = schedule.WindupStartTick;
        _pinActionInstanceId = schedule.ActionInstanceId;
        // J12 인과 복원: 원거리는 몸의 release 프레임을 ContactTick이 아니라 release tick(투사체가
        // 그 시점에 출발해 ContactTick에 도착하도록 앞당긴 틱)에 정렬한다. 기존엔 시위를 놓는 포즈가
        // '화살이 명중하는 순간'에 나와 투사체(=windup 시작에 출발)와 인과가 역전됐다. 근접은 그대로.
        var pinTargetTick = semantic is BattleAnimationSemantic.BowShot or BattleAnimationSemantic.ProjectileCast
            ? BattleContactPinScheduler.ResolvePresentationReleaseTick(
                schedule.WindupStartTick, schedule.ContactTick, BattleContactPinScheduler.DefaultProjectileTravelTicks)
            : schedule.ContactTick;
        _pinPlan = BattleContactPinPlanner.Resolve(timing.ContactNorm, clip.length, schedule.WindupStartTick, pinTargetTick, FixedStepSeconds);

        // The pin and blend share the scaled clock (1 tick = FixedStepSeconds in both step dispatch and clip
        // playback), so every span is in scaled seconds: the clip advances at slope 1 from its hold/offset
        // and ends after Hold + clipLength − Offset, while the strike blends to full weight by the contact
        // budget (D2-R5: blend contact = budget, not the clip-local contact time).
        _pinTotalSeconds = (float)(_pinPlan.HoldSeconds + clip.length - _pinPlan.OffsetSeconds);
        var instantOn = timing.CanContactPin && timing.ContactNorm <= 0.0001f;
        _blendEnvelope = BattleOneShotBlendResolver.Resolve(
            (float)_pinPlan.BudgetSeconds,
            _pinTotalSeconds,
            timing.RequiredFullWeightLeadSeconds,
            timing.RequiredFullWeightHoldSeconds,
            instantOn);

        _oneShotElapsed = 0f;
        _oneShotRemaining = 0f; // pinned lifetime is driven by EvaluateContactPin, not the Tick accumulator.
        _hitstopWindow = BattleHitstopWindow.None;
        _oneShotPlayable.SetTime(_pinPlan.ClipLocalTimeAt(0d));
        ApplyBlendWeights(0f);
        CuePlaybackCount++;
    }

    private void EndPinnedCommit()
    {
        _isPinnedCommit = false;
        _pinActionInstanceId = ActionInstanceId.None;
        var followUp = _pinFollowUpClip;
        _pinFollowUpClip = null;
        StopOneShot();
        // 재장전 체인(비핀·대칭 크로스페이드): 당김이 끝나면 드로운 조준 idle(Hold)로 복귀하므로
        // 포즈가 이어져 꼬리 스왑이 보이지 않는다. 죽은 유닛에는 체인하지 않는다(시체 포즈 보호).
        if (followUp != null && _lastState != null && !IsTerminalState(_lastState))
        {
            PlayOneShot(followUp, BattleClipTiming.NonPinnable);
            return;
        }

        ReapplyLastState();
    }

    private AnimationClipPlayable CreateClipPlayable(AnimationClip clip)
    {
        var playable = AnimationClipPlayable.Create(_graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetTime(0d);
        playable.SetSpeed(_playbackSpeed);
        return playable;
    }

    private void StopOneShot()
    {
        DisconnectPlayable(ref _oneShotPlayable, inputIndex: 1);
        _oneShotClip = null;
        _isHoldingTerminalPose = false;
        _oneShotElapsed = 0f;
        _isPinnedCommit = false;
        _oneShotIsMobility = false;
        _isDeathOneShotActive = false;
        _pinFollowUpClip = null;
        _pinActionInstanceId = ActionInstanceId.None;
        _hitstopWindow = BattleHitstopWindow.None;
        _blendEnvelope = BattleBlendEnvelope.InstantFull;

        if (!_mixer.IsValid())
        {
            return;
        }

        _mixer.SetInputWeight(0, HasLoopSource ? 1f : 0f);
        _mixer.SetInputWeight(1, 0f);
    }

    internal void BeginPresentationRootMotionFrame(bool isLocomoting, Vector3 authoritativeFrameDeltaWorld)
    {
        _authoritativeRootMotionFrameDeltaWorld = Flatten(authoritativeFrameDeltaWorld);
        _acceptRootMotionFrame = usePresentationRootMotion
                                 && _rootMotionTarget != null
                                 && isLocomoting
                                 && _oneShotRemaining <= 0f
                                 && !_isPinnedCommit
                                 && !_isHoldingTerminalPose
                                 && _authoritativeRootMotionFrameDeltaWorld.sqrMagnitude <= 1.0f;

        if (!_acceptRootMotionFrame)
        {
            ResetPresentationRootMotionOffset();
        }
    }

    internal void ConsumePresentationRootMotion(Vector3 animatorDeltaPositionWorld)
    {
        if (!_acceptRootMotionFrame || _rootMotionTarget == null)
        {
            return;
        }

        var visualRootDeltaWorld = Flatten(animatorDeltaPositionWorld);
        _rootMotionVisualOffsetWorld += visualRootDeltaWorld - _authoritativeRootMotionFrameDeltaWorld;
        _rootMotionVisualOffsetWorld = ClampHorizontal(_rootMotionVisualOffsetWorld, Mathf.Max(0.02f, maxRootMotionVisualOffset));
        ApplyPresentationRootMotionOffset();
    }

    private void ResetPresentationRootMotionOffset()
    {
        if (_rootMotionVisualOffsetWorld.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        _rootMotionVisualOffsetWorld = Vector3.zero;
        ApplyPresentationRootMotionOffset();
    }

    private void ApplyPresentationRootMotionOffset()
    {
        if (_rootMotionTarget == null)
        {
            return;
        }

        CaptureRootMotionTargetBase();
        var parent = _rootMotionTarget.parent;
        var localOffset = parent != null
            ? parent.InverseTransformVector(_rootMotionVisualOffsetWorld)
            : _rootMotionVisualOffsetWorld;
        _rootMotionTarget.localPosition = _rootMotionTargetBaseLocalPosition + localOffset;
    }

    private void CaptureRootMotionTargetBase()
    {
        if (_rootMotionTarget == null || _hasRootMotionTargetBase)
        {
            return;
        }

        _rootMotionTargetBaseLocalPosition = _rootMotionTarget.localPosition;
        _hasRootMotionTargetBase = true;
    }

    private void PlayTerminalPose(AnimationClip clip)
    {
        if (clip == null)
        {
            StopOneShot();
            return;
        }

        if (_oneShotPlayable.IsValid() && _oneShotClip == clip)
        {
            HoldOneShotFinalFrame();
            return;
        }

        DisconnectLoopSlot(0);
        DisconnectLoopSlot(1);
        // 루프 슬롯 파괴 후 _loopClip을 비워야 seek-부활 시 PlayLoop의 same-clip early-out이
        // 전-가중치-0 믹서(바인드 포즈)를 남기지 않는다.
        _loopClip = null;
        DisconnectPlayable(ref _oneShotPlayable, inputIndex: 1);
        _oneShotPlayable = CreateClipPlayable(clip);
        _graph.Connect(_oneShotPlayable, 0, _mixer, 1);
        _oneShotClip = clip;
        HoldOneShotFinalFrame();
    }

    private void HoldOneShotFinalFrame()
    {
        if (!_oneShotPlayable.IsValid() || _oneShotClip == null)
        {
            return;
        }

        var holdTime = Mathf.Max(0f, _oneShotClip.length - 0.01f);
        _oneShotPlayable.SetTime(holdTime);
        _oneShotPlayable.SetSpeed(0d);
        _oneShotRemaining = 0f;
        _isHoldingTerminalPose = true;

        if (_mixer.IsValid())
        {
            _mixer.SetInputWeight(0, 0f);
            _mixer.SetInputWeight(1, 1f);
        }
    }

    private void DisconnectPlayable(ref AnimationClipPlayable playable, int inputIndex)
    {
        if (!playable.IsValid())
        {
            return;
        }

        if (_mixer.IsValid())
        {
            _mixer.DisconnectInput(inputIndex);
        }

        playable.Destroy();
        playable = default;
    }

    private void ApplyPlayableSpeed(bool paused)
    {
        // 루프 = 타임라인 × cadence. outgoing 슬롯도 페이드 동안 같은 속도로 계속 재생한다.
        var loopSpeed = paused ? 0f : _playbackSpeed * _locomotionCadence;
        for (var slot = 0; slot < _loopSlots.Length; slot++)
        {
            if (_loopSlots[slot].IsValid())
            {
                _loopSlots[slot].SetSpeed(loopSpeed);
            }
        }

        if (_oneShotPlayable.IsValid())
        {
            // A pinned commit, a held terminal pose, and an active hitstop are all sampled manually — never
            // auto-advanced.
            var manual = _isHoldingTerminalPose || _isPinnedCommit || _hitstopWindow.IsActiveAt(_oneShotElapsed);
            // 이동성 one-shot(접근 대시 등)은 루프처럼 cadence를 곱해 발 구름이 실제 변위를 따라간다.
            // 핀 커밋은 ContactTick 정렬 계약이라 위 manual 분기로 항상 제외된다.
            var oneShotSpeed = paused ? 0f : _playbackSpeed * (_oneShotIsMobility ? _locomotionCadence : 1f);
            _oneShotPlayable.SetSpeed(manual ? 0f : oneShotSpeed);
        }
    }

    private void WrapLoopPlayback()
    {
        for (var slot = 0; slot < _loopSlots.Length; slot++)
        {
            if (!_loopSlots[slot].IsValid())
            {
                continue;
            }

            var clip = _loopSlots[slot].GetAnimationClip();
            if (clip == null || clip.length <= 0.001f)
            {
                continue;
            }

            var time = _loopSlots[slot].GetTime();
            if (time >= clip.length)
            {
                _loopSlots[slot].SetTime(time % clip.length);
            }
        }
    }

    private void DestroyGraph()
    {
        if (_graph.IsValid())
        {
            _graph.Destroy();
            _oneShotRemaining = 0f;
        }

        _mixer = default;
        _loopMixer = default;
        _loopSlots[0] = default;
        _loopSlots[1] = default;
        _activeLoopSlot = 0;
        _loopFadeElapsed = 0f;
        _loopFadeDuration = 0f;
        _oneShotPlayable = default;
        _loopClip = null;
        _oneShotClip = null;
        _isHoldingTerminalPose = false;
        _isPinnedCommit = false;
        _oneShotIsMobility = false;
        _isDeathOneShotActive = false;
        _pinFollowUpClip = null;
    }

    private static float ResolvePlaybackSpeed(float playbackSpeed)
    {
        return Mathf.Clamp(playbackSpeed, 0.05f, 4f);
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static Vector3 ClampHorizontal(Vector3 value, float maxMagnitude)
    {
        var flat = Flatten(value);
        if (flat.sqrMagnitude <= maxMagnitude * maxMagnitude)
        {
            return flat;
        }

        return flat.normalized * maxMagnitude;
    }

    private static bool IsTerminalState(BattleUnitReadModel state)
    {
        return !state.IsAlive || state.ActionState == CombatActionState.Dead;
    }
}
