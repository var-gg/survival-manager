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
    [SerializeField] private bool disableRootMotion = true;
    [SerializeField] private bool forceAlwaysAnimate;
    [SerializeField, Min(0.02f)] private float minimumOneShotSeconds = 0.12f;

    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private AnimationClipPlayable _loopPlayable;
    private AnimationClipPlayable _oneShotPlayable;
    private AnimationClip? _loopClip;
    private AnimationClip? _oneShotClip;
    private BattleUnitReadModel? _lastState;
    private BattleHumanoidAnimationSet? _resolvedAnimationSet;
    private float _oneShotRemaining;
    private float _playbackSpeed = 1f;
    private bool _lastIsLocomoting;
    private bool _isHoldingTerminalPose;

    public BattleHumanoidAnimationSet? AnimationSet => animationSet;
    public BattleHumanoidAnimationSet? ActiveAnimationSet => ResolveAnimationSet();
    public AnimationClip? CurrentLoopClip => _loopClip;
    public AnimationClip? CurrentOneShotClip => _oneShotClip;
    public bool IsHoldingTerminalPose => _isHoldingTerminalPose;
    public int CuePlaybackCount { get; private set; }

    public void ConfigureAnimationSet(BattleHumanoidAnimationSet set)
    {
        animationSet = set;
        _resolvedAnimationSet = null;
        if (_lastState != null)
        {
            ApplyState(_lastState, _playbackSpeed, paused: false, _lastIsLocomoting);
        }
    }

    public void Initialize(BattleActorWrapper wrapper, BattleUnitReadModel actor)
    {
        _lastState = actor;
        ResolveAnimator(wrapper);
        ConfigureAnimator();
        ApplyState(actor, _playbackSpeed, paused: false);
    }

    public void ApplyState(BattleUnitReadModel state, float playbackSpeed, bool paused)
    {
        ApplyState(state, playbackSpeed, paused, isLocomoting: false);
    }

    public void ApplyState(BattleUnitReadModel state, float playbackSpeed, bool paused, bool isLocomoting)
    {
        _lastState = state;
        _lastIsLocomoting = isLocomoting;
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
            if (_oneShotRemaining <= 0f)
            {
                PlayTerminalPose(terminalClip);
            }

            ApplyPlayableSpeed(paused);
            return;
        }

        if (_oneShotRemaining <= 0f && activeSet.TryResolveLoopClip(state, isLocomoting, out var loopClip))
        {
            PlayLoop(loopClip);
        }

        ApplyPlayableSpeed(paused);
    }

    public void ConsumeCue(BattlePresentationCue cue, BattleUnitReadModel state, float playbackSpeed)
    {
        _lastState = state;
        _playbackSpeed = ResolvePlaybackSpeed(playbackSpeed);

        var activeSet = ResolveAnimationSet();
        if (activeSet == null || !EnsureGraph())
        {
            return;
        }

        if (activeSet.TryResolveCueClip(cue, state, out var clip))
        {
            PlayOneShot(clip);
        }
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

        WrapLoopPlayback();
        if (_oneShotRemaining <= 0f)
        {
            return;
        }

        _oneShotRemaining = Mathf.Max(0f, _oneShotRemaining - Mathf.Max(0f, deltaTime) * _playbackSpeed);
        if (_oneShotRemaining > 0f)
        {
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
            ApplyState(_lastState, _playbackSpeed, paused: false, _lastIsLocomoting);
        }
    }

    public void ClearTransientState(BattlePresentationCueType reason)
    {
        _oneShotRemaining = 0f;
        StopOneShot();
        if (_lastState != null)
        {
            ApplyState(_lastState, _playbackSpeed, paused: false, _lastIsLocomoting);
        }
    }

    private void OnDisable()
    {
        DestroyGraph();
    }

    private void OnDestroy()
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

        if (disableRootMotion)
        {
            animator.applyRootMotion = false;
        }

        animator.cullingMode = forceAlwaysAnimate
            ? AnimatorCullingMode.AlwaysAnimate
            : AnimatorCullingMode.CullUpdateTransforms;
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

    private void PlayOneShot(AnimationClip clip)
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
        _mixer.SetInputWeight(0, _loopPlayable.IsValid() ? 0f : 0f);
        _mixer.SetInputWeight(1, 1f);
        CuePlaybackCount++;
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

        if (!_mixer.IsValid())
        {
            return;
        }

        _mixer.SetInputWeight(0, _loopPlayable.IsValid() ? 1f : 0f);
        _mixer.SetInputWeight(1, 0f);
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
            _oneShotPlayable.SetSpeed(_isHoldingTerminalPose ? 0f : speed);
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
    }

    private static float ResolvePlaybackSpeed(float playbackSpeed)
    {
        return Mathf.Clamp(playbackSpeed, 0.05f, 4f);
    }

    private static bool IsTerminalState(BattleUnitReadModel state)
    {
        return !state.IsAlive || state.ActionState == CombatActionState.Dead;
    }
}
