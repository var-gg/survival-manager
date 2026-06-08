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
    [SerializeField] private bool usePresentationRootMotion = true;
    [SerializeField] private bool clearRuntimeAnimatorController = true;
    [SerializeField] private bool forceAlwaysAnimate;
    [SerializeField, Min(0.02f)] private float minimumOneShotSeconds = 0.12f;
    [SerializeField, Min(0.02f)] private float maxRootMotionVisualOffset = 0.45f;

    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private AnimationClipPlayable _loopPlayable;
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
    private BattleHitstopWindow _hitstopWindow = BattleHitstopWindow.None;
    private double _lastSampleElapsed;
    private float _playbackSpeed = 1f;
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
    public bool IsHoldingTerminalPose => _isHoldingTerminalPose;
    public Vector3 PresentationRootMotionOffsetWorld => _rootMotionVisualOffsetWorld;
    public int CuePlaybackCount { get; private set; }

    public void ConfigureAnimationSet(BattleHumanoidAnimationSet set)
    {
        animationSet = set;
        _resolvedAnimationSet = null;
        if (_lastState != null)
        {
            ApplyState(_lastState, _playbackSpeed, paused: false, _lastIsLocomoting, _lastPresentationPhase);
        }
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
        _playbackSpeed = ResolvePlaybackSpeed(playbackSpeed);

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

    public void ConsumeCue(BattlePresentationCue cue, BattleUnitReadModel state, float playbackSpeed)
    {
        // GPT Pro D2-C: a cancel interrupts the matching scheduled commit one-shot (keyed by
        // ActionInstanceId, not actor id) — no gameplay/reaction, no ghost contact pose.
        if (cue.CueType == BattlePresentationCueType.ActionCanceled)
        {
            if (_isPinnedCommit && cue.CommitSchedule is { } canceled && _pinActionInstanceId.Equals(canceled.ActionInstanceId))
            {
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
            var timing = BattleClipTimingCatalog.Resolve(cue.AnimationSemantic);
            if (cue.CommitSchedule is { } schedule)
            {
                PlayPinnedCommit(clip, timing, schedule);
            }
            else
            {
                PlayOneShot(clip, timing);
            }
        }
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
        _mixer.SetInputWeight(0, _loopPlayable.IsValid() ? 1f - weight : 0f);
    }

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

        var advance = Mathf.Max(0f, deltaTime) * _playbackSpeed;
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
        if (_lastState != null)
        {
            ApplyState(_lastState, _playbackSpeed, paused: false, _lastIsLocomoting, _lastPresentationPhase);
        }
    }

    public void ClearTransientState(BattlePresentationCueType reason)
    {
        _oneShotRemaining = 0f;
        StopOneShot();
        if (_lastState != null)
        {
            ApplyState(_lastState, _playbackSpeed, paused: false, _lastIsLocomoting, _lastPresentationPhase);
        }
    }

    internal void DisposePresentationGraphForTests()
    {
        DestroyGraph();
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

        DisconnectPlayable(ref _loopPlayable, inputIndex: 0);
        _loopPlayable = CreateClipPlayable(clip);
        _graph.Connect(_loopPlayable, 0, _mixer, 0);
        _loopClip = clip;
        _mixer.SetInputWeight(0, _oneShotPlayable.IsValid() ? 0f : 1f);
    }

    private void PlayOneShot(AnimationClip clip, BattleClipTiming timing)
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
        _oneShotRemaining = Mathf.Max(minimumOneShotSeconds, clip.length / _playbackSpeed);
        _oneShotElapsed = 0f;
        _hitstopWindow = BattleHitstopWindow.None;
        // Stage 4 blend driver (GPT Pro J5): ramp the one-shot layer in/out across the contact window
        // instead of the old instant 1<->0 weight swap that popped the layer (D1). Weight is full from
        // (contact - lead) through (contact + hold), then fades back to the loop at the tail.
        _blendEnvelope = BattleOneShotBlendResolver.Resolve(clip.length, _playbackSpeed, timing);
        ApplyBlendWeights(0f);
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
        _mixer.SetInputWeight(0, _loopPlayable.IsValid() ? 1f - weight : 0f);
    }

    private void PlayPinnedCommit(AnimationClip clip, BattleClipTiming timing, BattleCommitSchedule schedule)
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

        _isPinnedCommit = true;
        _pinWindupStartTick = schedule.WindupStartTick;
        _pinActionInstanceId = schedule.ActionInstanceId;
        _pinPlan = BattleContactPinPlanner.Resolve(timing.ContactNorm, clip.length, schedule.WindupStartTick, schedule.ContactTick, FixedStepSeconds);

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
        StopOneShot();
        if (_lastState != null)
        {
            ApplyState(_lastState, _playbackSpeed, paused: false, _lastIsLocomoting, _lastPresentationPhase);
        }
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
        _pinActionInstanceId = ActionInstanceId.None;
        _hitstopWindow = BattleHitstopWindow.None;
        _blendEnvelope = BattleBlendEnvelope.InstantFull;

        if (!_mixer.IsValid())
        {
            return;
        }

        _mixer.SetInputWeight(0, _loopPlayable.IsValid() ? 1f : 0f);
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

        DisconnectPlayable(ref _loopPlayable, inputIndex: 0);
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
        var speed = paused ? 0f : _playbackSpeed;
        if (_loopPlayable.IsValid())
        {
            _loopPlayable.SetSpeed(speed);
        }

        if (_oneShotPlayable.IsValid())
        {
            // A pinned commit, a held terminal pose, and an active hitstop are all sampled manually — never
            // auto-advanced.
            var manual = _isHoldingTerminalPose || _isPinnedCommit || _hitstopWindow.IsActiveAt(_oneShotElapsed);
            _oneShotPlayable.SetSpeed(manual ? 0f : speed);
        }
    }

    private void WrapLoopPlayback()
    {
        if (!_loopPlayable.IsValid() || _loopClip == null || _loopClip.length <= 0.001f)
        {
            return;
        }

        var time = _loopPlayable.GetTime();
        if (time >= _loopClip.length)
        {
            _loopPlayable.SetTime(time % _loopClip.length);
        }
    }

    private void DestroyGraph()
    {
        if (!_graph.IsValid())
        {
            _mixer = default;
            _loopPlayable = default;
            _oneShotPlayable = default;
            _loopClip = null;
            _oneShotClip = null;
            _isHoldingTerminalPose = false;
            _isPinnedCommit = false;
            return;
        }

        _graph.Destroy();
        _mixer = default;
        _loopPlayable = default;
        _oneShotPlayable = default;
        _loopClip = null;
        _oneShotClip = null;
        _oneShotRemaining = 0f;
        _isHoldingTerminalPose = false;
        _isPinnedCommit = false;
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
