using System.Collections;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Unity.UI.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleActorView : MonoBehaviour
{
    private const float OverlayHeight = 2.3f;
    private const float WorldHpWidth = 1.3f;
    private const float OverlayScreenWidth = 148f;
    private const float OverlayScreenHeight = 44f;
    private const float OverlayHpWidth = 118f;
    private const float OverlayHpHeight = 8f;
    private const float GroundPlaneY = -0.98f;
    private const float TelegraphDiscThickness = 0.008f;

    private Camera _camera = null!;
    private BattlePresentationController _owner = null!;
    private BattleActorWrapper _wrapper = null!;
    private BattleActorVisualAdapter _visualAdapter = null!;
    private BattleAnimationEventBridge? _animationEventBridge;
    private BattleHumanoidAnimationDriver? _animationDriver;
    private BattleActorVfxSurface? _vfxSurface;
    private BattleActorAudioSurface? _audioSurface;
    private RectTransform _overlayParent = null!;
    private RectTransform _overlayRoot = null!;
    private Image _overlayBackground = null!;
    private GameObject _overlayHpBarRoot = null!;
    private Image _hpFill = null!;
    private Text _nameText = null!;
    private Text _stateText = null!;
    private Text _floatingText = null!;
    private Transform _visualRoot = null!;
    private Transform _headAnchor = null!;
    private Transform _centerAnchor = null!;
    private Transform _castAnchor = null!;
    private Transform _worldInfoRoot = null!;
    private Transform _worldHpFillRoot = null!;
    private TextMesh _worldNameText = null!;
    private TextMesh _worldNameShadowText = null!;
    private TextMesh _worldStateText = null!;
    private TextMesh _worldStateShadowText = null!;
    private Renderer _renderer = null!;
    private Renderer _shadowRenderer = null!;
    private Renderer _feetRingRenderer = null!;
    private Renderer _targetReticleRenderer = null!;
    private Renderer _windupRingRenderer = null!;
    private Renderer _homeAnchorRenderer = null!;
    private Renderer _rangeOuterRenderer = null!;
    private Renderer _rangeInnerRenderer = null!;
    private Renderer _guardRadiusRenderer = null!;
    private Renderer _clusterRadiusRenderer = null!;
    private LineRenderer _focusLineRenderer = null!;
    private LineRenderer _homeTetherRenderer = null!;
    private readonly List<Renderer> _slotMarkerRenderers = new();
    private Color _baseColor;
    private BattlePresentationOptions _options = BattlePresentationOptions.CreateDefault();
    private BattleUnitReadModel _currentState = null!;
    private Coroutine? _pulseRoutine;
    private Coroutine? _floatingRoutine;
    private Coroutine? _impactRoutine;
    private Coroutine? _accentRoutine;
    private BattleUnitMetadataFormatter? _metadataFormatter;
    private bool _isSelected;
    private bool _isCurrentActor;
    private bool _isCurrentTarget;
    private Vector3? _focusTargetWorld;
    private float _readabilityBoost;
    private BattleActionSemantic _activeSemantic = BattleActionSemantic.None;
    private float _actionCueTimer;
    private float _actionCueDuration;
    private float _impactCueTimer;
    private float _impactCueDuration;
    private float _accentTimer;
    private float _accentDuration;
    private float _guardCueTimer;
    private float _repositionCueTimer;
    private float _repositionCueDuration;
    private float _floatingTimer;
    private float _floatingDuration;
    private string _floatingMessage = string.Empty;
    private Color _accentColor = Color.clear;
    private Color _impactColor = Color.clear;
    private Color _floatingColor = Color.clear;
    private float _floatingScale = 1f;
    private BattleAnimationSemantic _activeAnimationSemantic = BattleAnimationSemantic.None;
    private BattleAnimationDirection _activeAnimationDirection = BattleAnimationDirection.Any;
    private BattleAnimationIntensity _activeAnimationIntensity = BattleAnimationIntensity.Any;
    private Quaternion _lastAliveRotation = Quaternion.identity;

    public void Initialize(
        BattleUnitReadModel actor,
        RectTransform overlayParent,
        Camera camera,
        BattlePresentationController owner,
        BattleUnitMetadataFormatter? metadataFormatter)
    {
        _overlayParent = overlayParent;
        _camera = camera;
        _owner = owner;
        _metadataFormatter = metadataFormatter;
        transform.position = ToWorldPosition(actor.Position);

        ConfigurePresentationWrapper(actor);
        CreateTelegraphRoot();
        CreateOverlay(actor);
        ApplyBlend(actor, actor, 1f);
    }

    public void SetMetadataFormatter(BattleUnitMetadataFormatter formatter)
    {
        _metadataFormatter = formatter;
        if (_currentState != null)
        {
            ApplyDisplay(_currentState, _currentState.CurrentHealth);
            RefreshVisualState();
        }
    }

    public void ApplyOptions(BattlePresentationOptions options)
    {
        _options = options;
        RefreshVisibility();
    }

    public void ApplyBlend(BattleUnitReadModel from, BattleUnitReadModel to, float alpha)
    {
        _currentState = to;
        var fromWorld = ToWorldPosition(from.Position);
        var toWorld = ToWorldPosition(to.Position);
        var clampedAlpha = Mathf.Clamp01(alpha);
        transform.position = ResolvePresentationPosition(from, to, fromWorld, toWorld, clampedAlpha);

        var displayedHealth = Mathf.Lerp(from.CurrentHealth, to.CurrentHealth, clampedAlpha);
        ApplyDisplay(to, displayedHealth);
        _animationDriver?.ApplyState(to, 1f, paused: false, isLocomoting: Vector3.Distance(fromWorld, toWorld) > 0.015f && clampedAlpha < 0.995f);
        RefreshVisualState();
        RefreshOverlayPosition();
    }

    public void ApplyContext(bool isSelected, bool isCurrentActor, bool isCurrentTarget, Vector3? focusTargetWorld, float readabilityBoost)
    {
        _isSelected = isSelected;
        _isCurrentActor = isCurrentActor;
        _isCurrentTarget = isCurrentTarget;
        _focusTargetWorld = focusTargetWorld;
        _readabilityBoost = readabilityBoost;
        RefreshVisualState();
    }

    public void ConsumeCue(BattlePresentationCue cue, Vector3? relatedWorld)
    {
        if (cue.CueType is BattlePresentationCueType.PlaybackReset or BattlePresentationCueType.SeekSnapshotApplied)
        {
            ClearTransients(cue.CueType);
            return;
        }

        _animationEventBridge?.OpenCueWindow(cue);
        _animationDriver?.ConsumeCue(cue, _currentState, 1f);
        _vfxSurface?.ConsumeCue(cue, _wrapper, relatedWorld);
        _audioSurface?.ConsumeCue(cue, _wrapper);

        var animationSemantic = ResolveAnimationSemantic(cue);
        switch (cue.CueType)
        {
            case BattlePresentationCueType.WindupEnter:
                _actionCueTimer = 0.28f;
                _actionCueDuration = 0.28f;
                _activeSemantic = ResolveCueSemantic(cue);
                break;
            case BattlePresentationCueType.TargetChanged:
                _accentColor = new Color(0.45f, 0.88f, 1f, 1f);
                _accentTimer = 0.18f;
                _accentDuration = 0.18f;
                break;
            case BattlePresentationCueType.ActionCommitBasic:
                StartActionCue(BattleActionSemantic.BasicAttack, 0.20f, ResolveSemanticColor(BattleActionSemantic.BasicAttack), relatedWorld);
                break;
            case BattlePresentationCueType.ActionCommitSkill:
                StartActionCue(BattleActionSemantic.DamagingSkill, 0.24f, ResolveSemanticColor(BattleActionSemantic.DamagingSkill), relatedWorld);
                break;
            case BattlePresentationCueType.ActionCommitHeal:
                StartActionCue(BattleActionSemantic.HealSupport, 0.22f, ResolveSemanticColor(BattleActionSemantic.HealSupport), relatedWorld);
                break;
            case BattlePresentationCueType.ImpactDamage:
            {
                _activeAnimationSemantic = animationSemantic;
                _activeAnimationDirection = cue.AnimationDirection;
                _activeAnimationIntensity = cue.AnimationIntensity;
                StartImpactCue(
                    ResolveImpactDuration(cue, animationSemantic),
                    ResolveImpactColor(cue, animationSemantic),
                    ResolveImpactLabel(cue, animationSemantic),
                    ResolveImpactScale(animationSemantic));
                break;
            }
            case BattlePresentationCueType.ImpactHeal:
                StartImpactCue(0.24f, new Color(0.38f, 1f, 0.54f, 1f), $"+{Mathf.CeilToInt(cue.Magnitude)}");
                break;
            case BattlePresentationCueType.GuardEnter:
                _guardCueTimer = 0.32f;
                _accentColor = ResolveSemanticColor(BattleActionSemantic.DefendHold);
                _accentTimer = 0.22f;
                _accentDuration = 0.22f;
                break;
            case BattlePresentationCueType.GuardExit:
                _guardCueTimer = 0.10f;
                break;
            case BattlePresentationCueType.RepositionStart:
                _activeAnimationSemantic = animationSemantic;
                _activeAnimationDirection = cue.AnimationDirection;
                _activeAnimationIntensity = cue.AnimationIntensity;
                _repositionCueTimer = ResolveRepositionCueDuration(animationSemantic);
                _repositionCueDuration = _repositionCueTimer;
                _accentColor = ResolveSemanticColor(BattleActionSemantic.Reposition);
                _accentTimer = 0.18f;
                _accentDuration = 0.18f;
                break;
            case BattlePresentationCueType.RepositionStop:
                _repositionCueTimer = 0.10f;
                _repositionCueDuration = 0.10f;
                break;
            case BattlePresentationCueType.DeathStart:
                _activeAnimationSemantic = BattleAnimationSemantic.Death;
                _activeAnimationDirection = BattleAnimationDirection.Any;
                _activeAnimationIntensity = BattleAnimationIntensity.Heavy;
                _impactCueTimer = 0.42f;
                _impactCueDuration = 0.42f;
                _impactColor = new Color(0.58f, 0.58f, 0.58f, 1f);
                break;
        }

        if (relatedWorld.HasValue)
        {
            _focusTargetWorld = relatedWorld;
        }

        RefreshVisualState();
    }

    public void ClearTransients(BattlePresentationCueType reason)
    {
        _actionCueTimer = 0f;
        _actionCueDuration = 0f;
        _impactCueTimer = 0f;
        _impactCueDuration = 0f;
        _accentTimer = 0f;
        _accentDuration = 0f;
        _guardCueTimer = 0f;
        _repositionCueTimer = 0f;
        _repositionCueDuration = 0f;
        _floatingTimer = 0f;
        _floatingDuration = 0f;
        _floatingMessage = string.Empty;
        _activeSemantic = BattleActionSemantic.None;
        _activeAnimationSemantic = BattleAnimationSemantic.None;
        _activeAnimationDirection = BattleAnimationDirection.Any;
        _activeAnimationIntensity = BattleAnimationIntensity.Any;
        _accentColor = Color.clear;
        _impactColor = Color.clear;
        _floatingColor = Color.clear;
        _animationEventBridge?.ClearTransientState(reason);
        _animationDriver?.ClearTransientState(reason);
        _vfxSurface?.ClearTransientState(reason);
        _audioSurface?.ClearTransientState(reason);

        if (_floatingText != null)
        {
            _floatingText.text = string.Empty;
            _floatingText.color = Color.clear;
        }

        RefreshVisualState();
    }

    public void TickTransients(float deltaTime, float playbackSpeed, bool paused)
    {
        _animationDriver?.Tick(deltaTime, playbackSpeed, paused);

        if (paused)
        {
            return;
        }

        var scaledDelta = deltaTime * playbackSpeed;
        _actionCueTimer = Mathf.Max(0f, _actionCueTimer - scaledDelta);
        _impactCueTimer = Mathf.Max(0f, _impactCueTimer - scaledDelta);
        _accentTimer = Mathf.Max(0f, _accentTimer - scaledDelta);
        _guardCueTimer = Mathf.Max(0f, _guardCueTimer - scaledDelta);
        _repositionCueTimer = Mathf.Max(0f, _repositionCueTimer - scaledDelta);
        _floatingTimer = Mathf.Max(0f, _floatingTimer - scaledDelta);
        if (_impactCueTimer <= 0f && _repositionCueTimer <= 0f)
        {
            _activeAnimationSemantic = BattleAnimationSemantic.None;
            _activeAnimationDirection = BattleAnimationDirection.Any;
            _activeAnimationIntensity = BattleAnimationIntensity.Any;
        }

        RefreshVisualState();
    }

    public void PlayAsSource(BattleEvent eventData, BattleActorView? targetView)
    {
        switch (eventData.ActionType)
        {
            case BattleActionType.BasicAttack:
                RestartCoroutine(ref _accentRoutine, AccentRoutine(new Color(1f, 0.84f, 0.24f, 1f), 0.20f));
                RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(1f, 0.84f, 0.24f, 1f), 0.18f, 1.04f));
                break;
            case BattleActionType.ActiveSkill when eventData.LogCode == BattleLogCode.ActiveSkillHeal:
                RestartCoroutine(ref _accentRoutine, AccentRoutine(new Color(0.28f, 1f, 0.52f, 1f), 0.24f));
                RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(0.28f, 1f, 0.52f, 1f), 0.22f, 1.05f));
                break;
            case BattleActionType.ActiveSkill:
                RestartCoroutine(ref _accentRoutine, AccentRoutine(new Color(1f, 0.58f, 0.18f, 1f), 0.22f));
                RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(1f, 0.58f, 0.18f, 1f), 0.20f, 1.05f));
                break;
            case BattleActionType.WaitDefend:
                RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(0.32f, 0.74f, 1f, 1f), 0.20f, 1.03f));
                break;
        }
    }

    public void PlayAsTarget(BattleEvent eventData)
    {
        if (eventData.LogCode == BattleLogCode.ActiveSkillHeal)
        {
            RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(0.28f, 1f, 0.52f, 1f), 0.22f, 1.05f));
            if (_options.ShowDamageText)
            {
                RestartCoroutine(ref _floatingRoutine, FloatingTextRoutine($"+{Mathf.CeilToInt(eventData.Value)}", new Color(0.48f, 1f, 0.58f, 1f), 0.45f));
            }
            return;
        }

        if (eventData.ActionType is BattleActionType.BasicAttack or BattleActionType.ActiveSkill)
        {
            RestartCoroutine(ref _impactRoutine, ImpactRoutine(0.22f));
            RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(1f, 0.24f, 0.24f, 1f), 0.18f, 1.03f));
            if (_options.ShowDamageText)
            {
                RestartCoroutine(ref _floatingRoutine, FloatingTextRoutine($"-{Mathf.CeilToInt(eventData.Value)}", new Color(1f, 0.45f, 0.45f, 1f), 0.45f));
            }
        }
    }

    public void RefreshOverlayPosition()
    {
        if (_camera == null)
        {
            _camera = Camera.main!;
        }

        if (_camera == null)
        {
            return;
        }

        if (_overlayRoot != null && _overlayParent != null)
        {
            var anchorWorldPosition = ResolveOverlayAnchorWorld();
            var viewport = _camera.WorldToViewportPoint(anchorWorldPosition);
            var isVisible = viewport.z > 0f
                            && viewport.x >= 0f && viewport.x <= 1f
                            && viewport.y >= 0f && viewport.y <= 1f;
            _overlayRoot.gameObject.SetActive(_options.ShowOverheadUi && isVisible);
            if (isVisible)
            {
                var screenPosition = RectTransformUtility.WorldToScreenPoint(_camera, anchorWorldPosition);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayParent, screenPosition, null, out var anchored))
                {
                    _overlayRoot.anchoredPosition = anchored;
                }
            }
        }
    }

    public Vector3 GetAnchorWorld(BattlePresentationAnchorId anchorId)
    {
        return _wrapper != null
            ? _wrapper.GetAnchorWorld(anchorId)
            : anchorId switch
            {
                BattlePresentationAnchorId.Root => transform.position,
                BattlePresentationAnchorId.Feet => new Vector3(transform.position.x, GroundPlaneY, transform.position.z),
                BattlePresentationAnchorId.Center => transform.position + Vector3.up * 0.1f,
                BattlePresentationAnchorId.Head => transform.position + Vector3.up * OverlayHeight,
                BattlePresentationAnchorId.Cast => transform.position + transform.forward * 0.6f + Vector3.up * 0.2f,
                _ => transform.position
            };
    }

    public float GetScreenDistanceSquared(Vector2 screenPosition)
    {
        if (_camera == null)
        {
            _camera = Camera.main!;
        }

        if (_camera == null)
        {
            return -1f;
        }

        var head = _camera.WorldToScreenPoint(GetAnchorWorld(BattlePresentationAnchorId.Head));
        if (head.z <= 0f)
        {
            return -1f;
        }

        return (new Vector2(head.x, head.y) - screenPosition).sqrMagnitude;
    }

    private bool IsPaused => _owner != null && _owner.IsPaused;

    private void ApplyDisplay(BattleUnitReadModel actor, float displayedHealth)
    {
        var restColor = ResolveRestColor(actor);
        var healthRatio = actor.MaxHealth <= 0f ? 0f : Mathf.Clamp01(displayedHealth / actor.MaxHealth);
        var healthColor = ResolveHealthColor(healthRatio, actor.IsAlive, actor.Side);
        var metadata = _metadataFormatter?.BuildOverhead(actor)
                      ?? new BattleUnitOverheadText(actor.Name, BattleReadabilityFormatter.BuildPlayerFacingState(actor));

        if (_hpFill != null)
        {
            var fillRect = _hpFill.GetComponent<RectTransform>();
            fillRect.anchorMax = new Vector2(healthRatio, 1f);
            _hpFill.color = healthColor;
        }

        if (_overlayBackground != null)
        {
            _overlayBackground.color = actor.Side == TeamSide.Ally
                ? new Color(0.08f, 0.12f, 0.18f, 0.66f)
                : new Color(0.18f, 0.10f, 0.10f, 0.66f);
        }

        if (_nameText != null)
        {
            _nameText.text = metadata.Header;
            _nameText.color = actor.IsAlive ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        if (_stateText != null)
        {
            _stateText.text = BuildOverlayStatusLine(actor);
            _stateText.color = actor.IsAlive ? new Color(0.92f, 0.94f, 0.86f, 1f) : new Color(0.68f, 0.68f, 0.68f, 1f);
        }

        if (_worldNameText != null && _worldNameShadowText != null)
        {
            _worldNameText.text = metadata.Header;
            _worldNameShadowText.text = metadata.Header;
        }

        if (_worldStateText != null && _worldStateShadowText != null)
        {
            _worldStateText.text = metadata.Subtitle;
            _worldStateShadowText.text = _worldStateText.text;
        }

        if (_worldHpFillRoot != null)
        {
            var width = Mathf.Max(0.05f, WorldHpWidth * healthRatio);
            _worldHpFillRoot.localScale = new Vector3(width, 0.08f, 0.06f);
            _worldHpFillRoot.localPosition = new Vector3((-WorldHpWidth * 0.5f) + (width * 0.5f), -0.46f, 0f);
            var fillRenderer = _worldHpFillRoot.GetComponent<Renderer>();
            if (fillRenderer != null)
            {
                fillRenderer.material.color = healthColor;
            }
        }

        RefreshVisibility();
    }

    private void RefreshVisualState()
    {
        if (_currentState == null || _visualAdapter == null)
        {
            return;
        }

        var restColor = ResolveRestColor(_currentState);
        if (_accentTimer > 0f && _accentDuration > 0f)
        {
            restColor = Color.Lerp(restColor, _accentColor, EvaluatePulse(_accentTimer, _accentDuration));
        }

        if (_impactCueTimer > 0f && _impactCueDuration > 0f)
        {
            restColor = Color.Lerp(restColor, _impactColor, EvaluatePulse(_impactCueTimer, _impactCueDuration));
        }

        var pose = ResolveBodyPose();
        var shadowColor = _currentState.IsAlive
            ? new Color(0f, 0f, 0f, 0.28f)
            : new Color(0.12f, 0.12f, 0.12f, 0.20f);
        _visualAdapter.ApplyState(new BattleActorVisualState(
            pose.position,
            pose.scale,
            pose.rotation,
            restColor,
            shadowColor));

        UpdateFocusTelegraphs();
        UpdateSelectedTelegraphs();
        UpdateFloatingText();
    }

    private (Vector3 position, Vector3 scale, Quaternion rotation) ResolveBodyPose()
    {
        var hasAuthoredDeathPose = !_currentState.IsAlive && _animationDriver?.ActiveAnimationSet != null;
        var scale = _currentState.IsAlive || hasAuthoredDeathPose ? Vector3.one : new Vector3(1.08f, 0.46f, 1.18f);
        var position = Vector3.zero;

        if (_currentState.IsAlive)
        {
            if (_currentState.IsDefending)
            {
                scale = new Vector3(1.08f, 0.92f, 1.18f);
                position += new Vector3(0f, -0.08f, 0f);
            }
            else if (_currentState.ActionState is CombatActionState.Reposition or CombatActionState.AdvanceToAnchor or CombatActionState.BreakContact)
            {
                ApplyMovementPose(ref position, ref scale, ResolveActiveMovementSemantic(), 0.55f);
            }
        }

        if (_currentState.ActionState == CombatActionState.ExecuteAction)
        {
            var windup = Mathf.Clamp01(_currentState.WindupProgress);
            position += new Vector3(0f, 0f, Mathf.Lerp(-0.20f, -0.06f, windup));
            scale = Vector3.Lerp(scale, new Vector3(1.06f, 0.94f, 1.12f), 0.45f);
        }

        if (_actionCueTimer > 0f && _actionCueDuration > 0f)
        {
            var cueProgress = 1f - (_actionCueTimer / _actionCueDuration);
            var lunge = Mathf.Sin(cueProgress * Mathf.PI) * ResolveLungeDistance();
            var lift = _activeSemantic == BattleActionSemantic.HealSupport ? Mathf.Sin(cueProgress * Mathf.PI) * 0.12f : 0f;
            position += new Vector3(0f, lift, lunge);
        }

        if (_impactCueTimer > 0f && _impactCueDuration > 0f)
        {
            var impact = Mathf.Sin((1f - (_impactCueTimer / _impactCueDuration)) * Mathf.PI);
            ApplyImpactPose(ref position, ref scale, impact);
        }

        if (_repositionCueTimer > 0f && _repositionCueDuration > 0f)
        {
            var movementPulse = Mathf.Sin((1f - (_repositionCueTimer / _repositionCueDuration)) * Mathf.PI);
            ApplyMovementPose(ref position, ref scale, ResolveActiveMovementSemantic(), movementPulse);
        }

        var rotation = ResolveFacingRotation();
        if (!_currentState.IsAlive)
        {
            if (hasAuthoredDeathPose)
            {
                position += new Vector3(0f, 0.02f, 0f);
            }
            else
            {
                rotation *= Quaternion.Euler(0f, 0f, 78f);
                position += new Vector3(0f, -0.08f, 0f);
            }
        }

        return (position, scale, rotation);
    }

    private BattleAnimationSemantic ResolveActiveMovementSemantic()
    {
        return _activeAnimationSemantic switch
        {
            BattleAnimationSemantic.DashEngage
                or BattleAnimationSemantic.BackstepDisengage
                or BattleAnimationSemantic.LateralStrafe => _activeAnimationSemantic,
            _ when _currentState.ActionState == CombatActionState.BreakContact => BattleAnimationSemantic.BackstepDisengage,
            _ when _currentState.ActionState == CombatActionState.AdvanceToAnchor => BattleAnimationSemantic.DashEngage,
            _ => BattleAnimationSemantic.LateralStrafe,
        };
    }

    private void ApplyMovementPose(ref Vector3 position, ref Vector3 scale, BattleAnimationSemantic semantic, float weight)
    {
        var t = Mathf.Clamp01(weight);
        var intensity = ResolveAnimationIntensityMultiplier();
        switch (semantic)
        {
            case BattleAnimationSemantic.DashEngage:
                position += new Vector3(0f, -0.04f * t, -0.16f * t * intensity);
                scale = Vector3.Lerp(scale, new Vector3(0.94f, 1.04f, 1.16f), Mathf.Clamp01(0.45f * t * intensity));
                break;
            case BattleAnimationSemantic.BackstepDisengage:
                position += new Vector3(0f, -0.04f * t, 0.14f * t * intensity);
                scale = Vector3.Lerp(scale, new Vector3(1.08f, 0.96f, 0.96f), Mathf.Clamp01(0.38f * t * intensity));
                break;
            case BattleAnimationSemantic.LateralStrafe:
                position += new Vector3(ResolveLateralSign() * 0.14f * t * intensity, -0.03f * t, -0.04f * t);
                scale = Vector3.Lerp(scale, new Vector3(1.06f, 0.98f, 1.08f), Mathf.Clamp01(0.36f * t * intensity));
                break;
            default:
                position += new Vector3(0f, -0.04f * t, -0.08f * t);
                scale = Vector3.Lerp(scale, new Vector3(0.96f, 1.02f, 1.10f), 0.40f * t);
                break;
        }
    }

    private void ApplyImpactPose(ref Vector3 position, ref Vector3 scale, float pulse)
    {
        var t = Mathf.Clamp01(pulse);
        var intensity = ResolveAnimationIntensityMultiplier();
        switch (_activeAnimationSemantic)
        {
            case BattleAnimationSemantic.Dodge:
                position += new Vector3(ResolveLateralSign() * 0.18f * t * intensity, 0.04f * t, 0f);
                scale = Vector3.Lerp(scale, new Vector3(0.96f, 1.04f, 1.02f), 0.22f * t);
                break;
            case BattleAnimationSemantic.BlockImpact:
                position += new Vector3(0f, -0.02f * t, -0.07f * t * intensity);
                scale = Vector3.Lerp(scale, new Vector3(1.12f, 0.94f, 1.08f), Mathf.Clamp01(0.28f * t * intensity));
                break;
            case BattleAnimationSemantic.CriticalImpact:
            case BattleAnimationSemantic.HitHeavy:
            case BattleAnimationSemantic.Knockdown:
                position += new Vector3(0f, -0.03f * t, -0.24f * t * intensity);
                scale = Vector3.Lerp(scale, new Vector3(1.12f, 0.86f, 1.16f), Mathf.Clamp01(0.34f * t * intensity));
                break;
            default:
                position += new Vector3(0f, 0f, -0.14f * t);
                scale = Vector3.Lerp(scale, new Vector3(1.05f, 0.92f, 1.06f), 0.24f * t);
                break;
        }
    }

    private float ResolveLateralSign()
    {
        return _activeAnimationDirection == BattleAnimationDirection.Left ? -1f : 1f;
    }

    private float ResolveAnimationIntensityMultiplier()
    {
        return _activeAnimationIntensity switch
        {
            BattleAnimationIntensity.Light => 0.72f,
            BattleAnimationIntensity.Heavy => 1.22f,
            _ => 1f,
        };
    }

    private Quaternion ResolveFacingRotation()
    {
        if (!_currentState.IsAlive)
        {
            var idHash = _currentState.Id != null ? _currentState.Id.GetHashCode() : 0;
            var deathYaw = ((idHash & 0xff) / 255f * 60f) - 30f;
            return _lastAliveRotation * Quaternion.Euler(0f, deathYaw, 0f);
        }

        var fallbackDirection = _currentState.Side == TeamSide.Ally ? Vector3.right : Vector3.left;
        var targetWorld = _focusTargetWorld ?? (Vector3?)null;
        var direction = targetWorld.HasValue
            ? targetWorld.Value - transform.position
            : fallbackDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = fallbackDirection;
        }

        var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _lastAliveRotation = rotation;
        return rotation;
    }

    private void UpdateFocusTelegraphs()
    {
        var feetWorld = _wrapper != null
            ? _wrapper.GetSocketWorld(BattleActorSocketId.FeetRing)
            : new Vector3(transform.position.x, GroundPlaneY, transform.position.z);
        var telegraphWorld = _wrapper != null
            ? _wrapper.GetSocketWorld(BattleActorSocketId.Telegraph)
            : feetWorld;

        UpdateDisc(_feetRingRenderer, _isCurrentActor || _isSelected, feetWorld, feetWorld.y, _isCurrentActor
            ? WithAlpha(ResolveSemanticColor(ResolveCurrentSemantic()), 0.52f)
            : new Color(0.42f, 0.76f, 1f, 0.42f), _isCurrentActor ? 1.18f : 1.04f);

        var windupVisible = _isCurrentActor && _currentState.ActionState == CombatActionState.ExecuteAction;
        var windupScale = Mathf.Lerp(0.45f, 1.18f, Mathf.Clamp01(_currentState.WindupProgress));
        UpdateDisc(_windupRingRenderer, windupVisible, telegraphWorld, telegraphWorld.y + 0.01f, WithAlpha(ResolveSemanticColor(ResolveCurrentSemantic()), 0.62f), windupScale);

        UpdateDisc(_targetReticleRenderer, _isCurrentTarget && _options.ShowDebugOverlay, telegraphWorld, telegraphWorld.y + 0.01f, new Color(1f, 0.56f, 0.28f, 0.46f), 1.18f);

        var showFocusLine = _options.ShowDebugOverlay && (_isCurrentActor || _isSelected) && _focusTargetWorld.HasValue;
        _focusLineRenderer.enabled = showFocusLine;
        if (showFocusLine)
        {
            _focusLineRenderer.positionCount = 2;
            _focusLineRenderer.SetPosition(0, GetAnchorWorld(BattlePresentationAnchorId.Cast));
            _focusLineRenderer.SetPosition(1, _focusTargetWorld!.Value + Vector3.up * 0.22f);
            var color = _isCurrentActor
                ? ResolveSemanticColor(ResolveCurrentSemantic())
                : new Color(0.55f, 0.78f, 1f, 0.88f);
            _focusLineRenderer.startWidth = _isCurrentActor ? 0.085f : 0.055f;
            _focusLineRenderer.endWidth = _isCurrentActor ? 0.045f : 0.03f;
            _focusLineRenderer.startColor = color;
            _focusLineRenderer.endColor = new Color(color.r, color.g, color.b, 0.36f);
        }
    }

    private void UpdateSelectedTelegraphs()
    {
        var homeWorld = ToWorldPosition(BattleFactory.ResolveAnchorPosition(_currentState.Side, _currentState.Anchor));
        var feetWorld = _wrapper != null
            ? _wrapper.GetSocketWorld(BattleActorSocketId.FeetRing)
            : new Vector3(transform.position.x, GroundPlaneY, transform.position.z);
        var showDebugTelegraphs = _isSelected && _options.ShowDebugOverlay;
        UpdateDisc(_homeAnchorRenderer, showDebugTelegraphs, homeWorld, GroundPlaneY + 0.005f, new Color(0.86f, 0.94f, 1f, 0.28f), 1.06f);

        var shouldShowTether = showDebugTelegraphs
                               && (_currentState.ActionState is CombatActionState.Reposition or CombatActionState.AdvanceToAnchor or CombatActionState.BreakContact
                                   || Vector3.Distance(homeWorld, transform.position) > 0.42f);
        _homeTetherRenderer.enabled = shouldShowTether;
        if (shouldShowTether)
        {
            _homeTetherRenderer.positionCount = 2;
            _homeTetherRenderer.SetPosition(0, GetAnchorWorld(BattlePresentationAnchorId.Feet));
            _homeTetherRenderer.SetPosition(1, homeWorld + Vector3.up * 0.02f);
        }

        var showRange = showDebugTelegraphs && _currentState.PreferredRangeMax > 0.05f;
        var rangeDiameter = Mathf.Max(0.5f, _currentState.PreferredRangeMax * 2f);
        var innerDiameter = Mathf.Max(0.2f, _currentState.PreferredRangeMin * 2f);
        UpdateDisc(_rangeOuterRenderer, showRange, feetWorld, feetWorld.y, new Color(0.40f, 0.66f, 0.92f, 0.18f), rangeDiameter);
        UpdateDisc(_rangeInnerRenderer, showRange && innerDiameter > 0.21f, feetWorld, feetWorld.y + 0.002f, new Color(0.08f, 0.12f, 0.18f, 0.16f), innerDiameter);

        var showGuard = showDebugTelegraphs && _currentState.FrontlineGuardRadius > 0.05f && (_currentState.IsDefending || _currentState.ActionState == CombatActionState.Recover);
        UpdateDisc(_guardRadiusRenderer, showGuard, feetWorld, feetWorld.y - 0.002f, WithAlpha(ResolveSemanticColor(BattleActionSemantic.DefendHold), 0.22f), _currentState.FrontlineGuardRadius * 2f);

        var showCluster = showDebugTelegraphs && _currentState.ClusterRadius > 0.05f && _focusTargetWorld.HasValue;
        UpdateDisc(_clusterRadiusRenderer, showCluster, _focusTargetWorld ?? transform.position, GroundPlaneY - 0.004f, new Color(0.90f, 0.42f, 0.28f, 0.20f), _currentState.ClusterRadius * 2f);

        var showSlots = showDebugTelegraphs && _focusTargetWorld.HasValue && _currentState.EngagementSlotCount > 0 && _currentState.EngagementSlotRadius > 0.05f;
        for (var i = 0; i < _slotMarkerRenderers.Count; i++)
        {
            var renderer = _slotMarkerRenderers[i];
            var active = showSlots && i < _currentState.EngagementSlotCount;
            renderer.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            var angle = (_currentState.EngagementSlotCount == 1 ? 0f : (160f / (_currentState.EngagementSlotCount - 1)) * i) - 80f;
            var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _currentState.EngagementSlotRadius;
            renderer.transform.position = _focusTargetWorld!.Value + new Vector3(offset.x, GroundPlaneY + 0.012f, offset.z);
        }
    }

    private void UpdateFloatingText()
    {
        if (_floatingText == null)
        {
            return;
        }

        if (_floatingTimer <= 0f || _floatingDuration <= 0f || !_options.ShowDamageText)
        {
            _floatingText.text = string.Empty;
            _floatingText.color = Color.clear;
            _floatingText.rectTransform.localScale = Vector3.one;
            return;
        }

        var progress = 1f - (_floatingTimer / _floatingDuration);
        _floatingText.text = _floatingMessage;
        _floatingText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(0f, 8f), new Vector2(0f, 44f), progress);
        _floatingText.color = new Color(_floatingColor.r, _floatingColor.g, _floatingColor.b, 1f - progress);
        _floatingText.rectTransform.localScale = Vector3.one * _floatingScale;
    }

    private void StartActionCue(BattleActionSemantic semantic, float duration, Color color, Vector3? relatedWorld)
    {
        _activeSemantic = semantic;
        _actionCueTimer = duration;
        _actionCueDuration = duration;
        _accentColor = color;
        _accentTimer = duration;
        _accentDuration = duration;
        if (relatedWorld.HasValue)
        {
            _focusTargetWorld = relatedWorld;
        }
    }

    private void StartImpactCue(float duration, Color color, string floatingText, float scale = 1f)
    {
        _impactCueTimer = duration;
        _impactCueDuration = duration;
        _impactColor = color;

        if (_options.ShowDamageText)
        {
            _floatingTimer = duration * 1.45f;
            _floatingDuration = _floatingTimer;
            _floatingMessage = floatingText;
            _floatingColor = color;
            _floatingScale = scale;
        }
    }

    private static float ResolveImpactDuration(BattlePresentationCue cue, BattleAnimationSemantic semantic)
    {
        return semantic switch
        {
            BattleAnimationSemantic.CriticalImpact or BattleAnimationSemantic.HitHeavy or BattleAnimationSemantic.Knockdown => 0.34f,
            BattleAnimationSemantic.Dodge => 0.22f,
            BattleAnimationSemantic.BlockImpact => 0.28f,
            _ when cue.AnimationIntensity == BattleAnimationIntensity.Heavy => 0.30f,
            _ => 0.24f,
        };
    }

    private static Color ResolveImpactColor(BattlePresentationCue cue, BattleAnimationSemantic semantic)
    {
        return semantic switch
        {
            BattleAnimationSemantic.Dodge => new Color(0.48f, 0.88f, 1f, 1f),
            BattleAnimationSemantic.BlockImpact => new Color(0.42f, 0.72f, 1f, 1f),
            BattleAnimationSemantic.CriticalImpact or BattleAnimationSemantic.Knockdown => new Color(1f, 0.85f, 0.25f, 1f),
            BattleAnimationSemantic.HitHeavy => new Color(1f, 0.46f, 0.24f, 1f),
            _ when cue.AnimationIntensity == BattleAnimationIntensity.Heavy => new Color(1f, 0.52f, 0.22f, 1f),
            _ => new Color(1f, 0.32f, 0.28f, 1f),
        };
    }

    private static string ResolveImpactLabel(BattlePresentationCue cue, BattleAnimationSemantic semantic)
    {
        var amount = Mathf.Max(0, Mathf.CeilToInt(cue.Magnitude));
        return semantic switch
        {
            BattleAnimationSemantic.Dodge => "MISS",
            BattleAnimationSemantic.BlockImpact when amount > 0 => $"BLOCK -{amount}",
            BattleAnimationSemantic.BlockImpact => "BLOCK",
            BattleAnimationSemantic.CriticalImpact => $"CRIT! -{amount}",
            BattleAnimationSemantic.Knockdown => $"BREAK! -{amount}",
            _ => $"-{amount}",
        };
    }

    private static float ResolveImpactScale(BattleAnimationSemantic semantic)
    {
        return semantic switch
        {
            BattleAnimationSemantic.CriticalImpact or BattleAnimationSemantic.Knockdown => 1.45f,
            BattleAnimationSemantic.HitHeavy => 1.25f,
            BattleAnimationSemantic.Dodge or BattleAnimationSemantic.BlockImpact => 0.95f,
            _ => 1f,
        };
    }

    private static float ResolveRepositionCueDuration(BattleAnimationSemantic semantic)
    {
        return semantic switch
        {
            BattleAnimationSemantic.DashEngage => 0.42f,
            BattleAnimationSemantic.BackstepDisengage => 0.34f,
            BattleAnimationSemantic.LateralStrafe => 0.36f,
            _ => 0.36f,
        };
    }

    private string BuildStatusLine(BattleUnitReadModel actor)
    {
        return BattleReadabilityFormatter.BuildPlayerFacingState(actor);
    }

    private string BuildOverlayStatusLine(BattleUnitReadModel actor)
    {
        var status = BuildStatusLine(actor);
        var targetSeparator = status.IndexOf(" -> ");
        if (targetSeparator >= 0)
        {
            status = status[..targetSeparator];
        }

        const int maxLength = 18;
        return status.Length <= maxLength
            ? status
            : status[..(maxLength - 3)] + "...";
    }

    private Vector3 ResolveOverlayAnchorWorld()
    {
        if (_wrapper == null)
        {
            return transform.position + Vector3.up * OverlayHeight;
        }

        var hud = _wrapper.GetSocketWorld(BattleActorSocketId.Hud);
        var head = _wrapper.GetSocketWorld(BattleActorSocketId.Head);
        var center = _wrapper.GetSocketWorld(BattleActorSocketId.Center);
        var clampedY = Mathf.Min(hud.y, Mathf.Lerp(center.y, head.y, 0.96f) + 0.08f);
        return new Vector3(hud.x, clampedY, hud.z);
    }

    private void ConfigurePresentationWrapper(BattleUnitReadModel actor)
    {
        _wrapper = GetComponent<BattleActorWrapper>();
        if (_wrapper == null)
        {
            _wrapper = gameObject.AddComponent<BattleActorWrapper>();
        }

        _wrapper.Configure(actor);
        _headAnchor = _wrapper.GetSocketTransform(BattleActorSocketId.Head)!;
        _centerAnchor = _wrapper.GetSocketTransform(BattleActorSocketId.Center)!;
        _castAnchor = _wrapper.GetSocketTransform(BattleActorSocketId.Cast)!;

        _visualAdapter = GetComponent<BattleActorVisualAdapter>();
        if (_visualAdapter == null)
        {
            _visualAdapter = gameObject.AddComponent<BattlePrimitiveActorVisualAdapter>();
        }

        _visualAdapter.Initialize(_wrapper, actor);
        _visualRoot = _visualAdapter.VisualRoot!;
        _renderer = _visualAdapter.PrimaryRenderer!;
        _shadowRenderer = _visualAdapter.ShadowRenderer!;
        _animationEventBridge = GetComponent<BattleAnimationEventBridge>();
        _animationDriver = GetComponent<BattleHumanoidAnimationDriver>();
        if (_animationDriver == null && _visualRoot != null)
        {
            _animationDriver = _visualRoot.GetComponentInChildren<BattleHumanoidAnimationDriver>(true);
        }

        _animationDriver?.Initialize(_wrapper, actor);
        _vfxSurface = GetComponent<BattleActorVfxSurface>();
        _audioSurface = GetComponent<BattleActorAudioSurface>();
        _baseColor = ResolveBaseColor(actor);
    }

    private void CreateTelegraphRoot()
    {
        _feetRingRenderer = CreateDisc("FeetRing", new Color(0.48f, 0.80f, 1f, 1f), 1.3f);
        _targetReticleRenderer = CreateDisc("TargetReticle", new Color(1f, 0.52f, 0.26f, 1f), 1.35f);
        _windupRingRenderer = CreateDisc("WindupRing", new Color(1f, 0.82f, 0.30f, 1f), 0.85f);
        _homeAnchorRenderer = CreateDisc("HomeAnchor", new Color(0.86f, 0.94f, 1f, 1f), 1.0f);
        _rangeOuterRenderer = CreateDisc("RangeOuter", new Color(0.40f, 0.66f, 0.92f, 1f), 2.2f);
        _rangeInnerRenderer = CreateDisc("RangeInner", new Color(0.08f, 0.12f, 0.18f, 1f), 1.0f);
        _guardRadiusRenderer = CreateDisc("GuardRadius", ResolveSemanticColor(BattleActionSemantic.DefendHold), 1.8f);
        _clusterRadiusRenderer = CreateDisc("ClusterRadius", new Color(0.90f, 0.42f, 0.28f, 1f), 1.6f);

        _focusLineRenderer = CreateLine("FocusLine", 0.06f);
        _homeTetherRenderer = CreateLine("HomeTether", 0.045f);
        _homeTetherRenderer.startColor = new Color(0.78f, 0.90f, 1f, 0.75f);
        _homeTetherRenderer.endColor = new Color(0.78f, 0.90f, 1f, 0.25f);

        var slotRoot = new GameObject("SlotMarkers");
        slotRoot.transform.SetParent(transform, false);
        for (var i = 0; i < 6; i++)
        {
            _slotMarkerRenderers.Add(CreateDisc($"SlotMarker_{i}", new Color(1f, 0.84f, 0.44f, 1f), 0.22f, slotRoot.transform));
        }
    }

    private void CreateWorldInfo()
    {
        var font = GameFontCatalog.LoadSharedUiFont();
        var infoGo = new GameObject("WorldInfoRoot");
        infoGo.transform.SetParent(transform, false);
        _worldInfoRoot = infoGo.transform;

        _worldNameShadowText = CreateWorldText(_worldInfoRoot, "NameShadowText", font, new Vector3(0.02f, -0.02f, 0.03f), new Color(0.04f, 0.04f, 0.04f, 0.92f), 57, 0.09f);
        _worldNameText = CreateWorldText(_worldInfoRoot, "NameText", font, Vector3.zero, Color.white, 57, 0.09f);
        _worldStateShadowText = CreateWorldText(_worldInfoRoot, "StateShadowText", font, new Vector3(0.02f, -0.02f, 0.03f), new Color(0.04f, 0.04f, 0.04f, 0.92f), 39, 0.075f);
        _worldStateText = CreateWorldText(_worldInfoRoot, "StateText", font, Vector3.zero, Color.white, 39, 0.075f);
        _worldStateText.transform.localPosition = new Vector3(0f, -0.24f, 0f);
        _worldStateShadowText.transform.localPosition = new Vector3(0.02f, -0.26f, 0.03f);

        var barBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barBack.name = "WorldHpBack";
        barBack.transform.SetParent(_worldInfoRoot, false);
        barBack.transform.localPosition = new Vector3(0f, -0.52f, 0.02f);
        barBack.transform.localScale = new Vector3(WorldHpWidth, 0.10f, 0.07f);
        RemoveCollider(barBack);
        var backRenderer = barBack.GetComponent<Renderer>();
        ConfigurePresentationRenderer(backRenderer);
        backRenderer.material.color = new Color(0.04f, 0.04f, 0.04f, 1f);

        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "WorldHpFill";
        fill.transform.SetParent(_worldInfoRoot, false);
        RemoveCollider(fill);
        ConfigurePresentationRenderer(fill.GetComponent<Renderer>());
        _worldHpFillRoot = fill.transform;
    }

    private void CreateOverlay(BattleUnitReadModel actor)
    {
        var font = GameFontCatalog.LoadSharedUiFont();
        var overlayGo = new GameObject($"{actor.Name}_Overlay", typeof(RectTransform), typeof(Image));
        overlayGo.transform.SetParent(_overlayParent, false);

        _overlayRoot = overlayGo.GetComponent<RectTransform>();
        _overlayRoot.sizeDelta = new Vector2(OverlayScreenWidth, OverlayScreenHeight);
        _overlayBackground = overlayGo.GetComponent<Image>();
        _overlayBackground.color = Color.clear;
        _overlayBackground.enabled = false;
        _overlayBackground.raycastTarget = false;

        _nameText = CreateOverlayText(_overlayRoot, "NameText", font, new Vector2(0f, -3f), new Vector2(OverlayScreenWidth, 16f), 12, TextAnchor.MiddleCenter);
        _stateText = CreateOverlayText(_overlayRoot, "StateText", font, new Vector2(0f, -3f), new Vector2(OverlayScreenWidth, 16f), 12, TextAnchor.MiddleCenter);

        var barBackGo = new GameObject("HpBarBack", typeof(RectTransform));
        barBackGo.transform.SetParent(_overlayRoot, false);
        _overlayHpBarRoot = barBackGo;
        var barBackRect = barBackGo.GetComponent<RectTransform>();
        barBackRect.anchorMin = new Vector2(0.5f, 0f);
        barBackRect.anchorMax = new Vector2(0.5f, 0f);
        barBackRect.pivot = new Vector2(0.5f, 0f);
        barBackRect.anchoredPosition = new Vector2(0f, 7f);
        barBackRect.sizeDelta = new Vector2(OverlayHpWidth, OverlayHpHeight);

        var barBackImage = barBackGo.AddComponent<Image>();
        barBackImage.color = new Color(0.04f, 0.04f, 0.04f, 0.86f);
        barBackImage.raycastTarget = false;

        var fillGo = new GameObject("HpBarFill", typeof(RectTransform));
        fillGo.transform.SetParent(barBackRect, false);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        _hpFill = fillGo.AddComponent<Image>();
        _hpFill.raycastTarget = false;

        var floatingGo = new GameObject("FloatingText", typeof(RectTransform));
        floatingGo.transform.SetParent(_overlayRoot, false);
        var floatingRect = floatingGo.GetComponent<RectTransform>();
        floatingRect.anchorMin = new Vector2(0.5f, 0.5f);
        floatingRect.anchorMax = new Vector2(0.5f, 0.5f);
        floatingRect.pivot = new Vector2(0.5f, 0.5f);
        floatingRect.anchoredPosition = new Vector2(0f, 20f);
        floatingRect.sizeDelta = new Vector2(OverlayScreenWidth, 42f);

        _floatingText = floatingGo.AddComponent<Text>();
        _floatingText.font = font;
        _floatingText.fontSize = 34;
        _floatingText.alignment = TextAnchor.MiddleCenter;
        _floatingText.color = Color.clear;
        AddOutline(_floatingText, new Color(0f, 0f, 0f, 0.92f));

        UiGraphicRaycastPolicy.ApplyToHierarchy(_overlayRoot);
    }

    private IEnumerator ImpactRoutine(float duration)
    {
        if (_visualRoot == null)
        {
            yield break;
        }

        var forwardDuration = Mathf.Max(0.05f, duration * 0.4f);
        var returnDuration = Mathf.Max(0.07f, duration * 0.5f);
        yield return AnimateLocalVisual(Vector3.zero, new Vector3(0f, 0f, -0.12f), Vector3.one, new Vector3(1.05f, 0.94f, 1.05f), forwardDuration);
        yield return AnimateLocalVisual(new Vector3(0f, 0f, -0.12f), Vector3.zero, new Vector3(1.05f, 0.94f, 1.05f), Vector3.one, returnDuration);
    }

    private IEnumerator PulseRoutine(Color flashColor, float duration, float punchScale)
    {
        if (_renderer == null || _visualRoot == null)
        {
            yield break;
        }

        var baseScale = _currentState.IsAlive ? Vector3.one : new Vector3(1f, 0.8f, 1f);
        var peakScale = baseScale * punchScale;
        var restColor = ResolveRestColor(_currentState);
        var half = Mathf.Max(0.04f, duration * 0.5f);

        yield return AnimatePulse(restColor, flashColor, baseScale, peakScale, half);
        yield return AnimatePulse(flashColor, restColor, peakScale, baseScale, half);
    }

    private IEnumerator AccentRoutine(Color accentColor, float duration)
    {
        if (_shadowRenderer == null || _overlayBackground == null)
        {
            yield break;
        }

        var baseShadowColor = _currentState.IsAlive ? new Color(0f, 0f, 0f, 0.28f) : new Color(0.12f, 0.12f, 0.12f, 0.20f);
        var baseOverlayColor = _overlayBackground.color;
        var half = Mathf.Max(0.04f, duration * 0.5f);

        yield return AnimateAccent(baseShadowColor, accentColor, baseOverlayColor, half);
        yield return AnimateAccent(accentColor, baseShadowColor, baseOverlayColor, half);
    }

    private IEnumerator FloatingTextRoutine(string message, Color color, float duration)
    {
        if (_floatingText == null)
        {
            yield break;
        }

        var rect = _floatingText.rectTransform;
        var start = new Vector2(0f, 8f);
        var end = new Vector2(0f, 44f);
        _floatingText.text = message;
        _floatingText.color = color;

        var elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(start, end, progress);
            _floatingText.color = new Color(color.r, color.g, color.b, 1f - progress);
            yield return null;
        }

        rect.anchoredPosition = start;
        _floatingText.color = Color.clear;
        _floatingText.text = string.Empty;
    }

    private IEnumerator AnimateLocalVisual(Vector3 fromPosition, Vector3 toPosition, Vector3 fromScale, Vector3 toScale, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            _visualRoot.localPosition = Vector3.Lerp(fromPosition, toPosition, t);
            _visualRoot.localScale = Vector3.Lerp(fromScale, toScale, t);
            yield return null;
        }

        _visualRoot.localPosition = Vector3.zero;
    }

    private IEnumerator AnimatePulse(Color fromColor, Color toColor, Vector3 fromScale, Vector3 toScale, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            _renderer.material.color = Color.Lerp(fromColor, toColor, t);
            _visualRoot.localScale = Vector3.Lerp(fromScale, toScale, t);
            yield return null;
        }

        _visualRoot.localScale = _currentState.IsAlive ? Vector3.one : new Vector3(1f, 0.8f, 1f);
    }

    private IEnumerator AnimateAccent(Color fromShadow, Color toShadow, Color overlayBaseColor, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            _shadowRenderer.material.color = Color.Lerp(fromShadow, toShadow, t);
            _overlayBackground.color = Color.Lerp(overlayBaseColor, new Color(toShadow.r, toShadow.g, toShadow.b, overlayBaseColor.a), t * 0.55f);
            yield return null;
        }
    }

    private void RefreshVisibility()
    {
        if (_overlayRoot != null)
        {
            _overlayRoot.gameObject.SetActive(_options.ShowOverheadUi);
        }

        if (_overlayBackground != null)
        {
            _overlayBackground.enabled = false;
        }

        if (_nameText != null)
        {
            _nameText.enabled = false;
        }

        if (_stateText != null)
        {
            _stateText.enabled = _options.ShowOverheadUi;
        }

        if (_overlayHpBarRoot != null)
        {
            _overlayHpBarRoot.SetActive(_options.ShowOverheadUi);
        }

        if (_floatingText != null)
        {
            _floatingText.enabled = _options.ShowDamageText;
        }
    }

    private void RestartCoroutine(ref Coroutine? routine, IEnumerator next)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(next);
    }

    private Renderer CreateDisc(string name, Color color, float diameter)
    {
        return CreateDisc(name, color, diameter, transform);
    }

    private Renderer CreateDisc(string name, Color color, float diameter, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, GroundPlaneY, 0f);
        go.transform.localScale = new Vector3(diameter, TelegraphDiscThickness, diameter);
        RemoveCollider(go);

        var renderer = go.GetComponent<Renderer>();
        ConfigurePresentationRenderer(renderer);
        renderer.sharedMaterial = BattlePresentationMaterialFactory.Create(color);
        go.SetActive(false);
        return renderer;
    }

    private LineRenderer CreateLine(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.material = BattlePresentationMaterialFactory.Create(Color.white);
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.startWidth = width;
        line.endWidth = width * 0.55f;
        ConfigurePresentationRenderer(line);
        line.enabled = false;
        return line;
    }

    private void UpdateDisc(Renderer renderer, bool isVisible, Vector3 worldPosition, float worldY, Color color, float diameter)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.gameObject.SetActive(isVisible);
        if (!isVisible)
        {
            return;
        }

        var boost = 0.12f * _readabilityBoost;
        BattlePresentationMaterialFactory.ApplyColor(renderer.sharedMaterial, color + new Color(boost, boost, boost, 0f));
        renderer.transform.position = new Vector3(worldPosition.x, worldY, worldPosition.z);
        renderer.transform.localScale = new Vector3(Mathf.Max(0.08f, diameter), TelegraphDiscThickness, Mathf.Max(0.08f, diameter));
    }

    private static TextMesh CreateWorldText(Transform parent, string name, Font font, Vector3 localPosition, Color color, int fontSize, float characterSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var textMesh = go.AddComponent<TextMesh>();
        textMesh.font = font;
        var renderer = textMesh.GetComponent<MeshRenderer>();
        ConfigurePresentationRenderer(renderer);
        renderer.material = font.material;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = characterSize;
        textMesh.color = color;
        return textMesh;
    }

    private static void ConfigurePresentationRenderer(Renderer renderer)
    {
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
    }

    private static Text CreateOverlayText(RectTransform parent, string name, Font font, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        AddOutline(text, new Color(0f, 0f, 0f, 0.86f));
        return text;
    }

    private static Vector3 ToWorldPosition(CombatVector2 position)
    {
        return new Vector3(position.X, 0f, position.Y);
    }

    private static Vector3 ResolvePresentationPosition(
        BattleUnitReadModel from,
        BattleUnitReadModel to,
        Vector3 fromWorld,
        Vector3 toWorld,
        float clampedAlpha)
    {
        if (clampedAlpha <= 0f)
        {
            return fromWorld;
        }

        if (clampedAlpha >= 0.995f)
        {
            return toWorld;
        }

        var distance = Vector3.Distance(fromWorld, toWorld);
        if (distance <= 0.015f)
        {
            return toWorld;
        }

        var alpha = ShouldEasePresentationMove(from, to, distance)
            ? Mathf.SmoothStep(0f, 1f, clampedAlpha)
            : clampedAlpha;
        return Vector3.LerpUnclamped(fromWorld, toWorld, alpha);
    }

    private static bool ShouldEasePresentationMove(BattleUnitReadModel from, BattleUnitReadModel to, float distance)
    {
        return distance >= 0.35f || IsPresentationRepositionState(from.ActionState) || IsPresentationRepositionState(to.ActionState);
    }

    private static bool IsPresentationRepositionState(CombatActionState state)
    {
        return state is CombatActionState.Reposition or CombatActionState.AdvanceToAnchor or CombatActionState.BreakContact;
    }

    private static void AddOutline(Graphic graphic, Color color)
    {
        var outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }

    private static void RemoveCollider(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyPresentationObject(collider);
        }
    }

    private static void DestroyPresentationObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private float ResolveLungeDistance()
    {
        return ResolveCurrentSemantic() switch
        {
            BattleActionSemantic.BasicAttack => 0.34f,
            BattleActionSemantic.DamagingSkill => 0.24f,
            BattleActionSemantic.HealSupport => 0.16f,
            _ => 0.18f,
        };
    }

    private BattleActionSemantic ResolveCurrentSemantic()
    {
        return _activeSemantic != BattleActionSemantic.None
            ? _activeSemantic
            : BattleReadabilityFormatter.ResolveSemantic(_currentState);
    }

    private static BattleActionSemantic ResolveCueSemantic(BattlePresentationCue cue)
    {
        return cue.CueType switch
        {
            BattlePresentationCueType.ActionCommitBasic => BattleActionSemantic.BasicAttack,
            BattlePresentationCueType.ActionCommitSkill => BattleActionSemantic.DamagingSkill,
            BattlePresentationCueType.ActionCommitHeal => BattleActionSemantic.HealSupport,
            BattlePresentationCueType.GuardEnter => BattleActionSemantic.DefendHold,
            BattlePresentationCueType.RepositionStart => BattleActionSemantic.Reposition,
            _ when cue.ActionType == BattleActionType.ActiveSkill => BattleActionSemantic.DamagingSkill,
            _ when cue.ActionType == BattleActionType.BasicAttack => BattleActionSemantic.BasicAttack,
            _ when cue.ActionType == BattleActionType.WaitDefend => BattleActionSemantic.DefendHold,
            _ => BattleActionSemantic.None
        };
    }

    private static BattleAnimationSemantic ResolveAnimationSemantic(BattlePresentationCue cue)
    {
        if (cue.AnimationSemantic != BattleAnimationSemantic.None)
        {
            return cue.AnimationSemantic;
        }

        if (HasCueNote(cue, "dodge"))
        {
            return BattleAnimationSemantic.Dodge;
        }

        if (HasCueNote(cue, "block"))
        {
            return BattleAnimationSemantic.BlockImpact;
        }

        if (HasCueNote(cue, "knockdown"))
        {
            return BattleAnimationSemantic.Knockdown;
        }

        if (HasCueNote(cue, "crit"))
        {
            return BattleAnimationSemantic.CriticalImpact;
        }

        return cue.CueType switch
        {
            BattlePresentationCueType.ImpactDamage => BattleAnimationSemantic.HitLight,
            BattlePresentationCueType.RepositionStart => BattleAnimationSemantic.LateralStrafe,
            BattlePresentationCueType.DeathStart => BattleAnimationSemantic.Death,
            _ => BattleAnimationSemantic.None,
        };
    }

    private static bool HasCueNote(BattlePresentationCue cue, string token)
    {
        return !string.IsNullOrEmpty(cue.Note)
               && cue.Note.Contains(token, System.StringComparison.OrdinalIgnoreCase);
    }

    private static float EvaluatePulse(float timer, float duration)
    {
        if (duration <= 0f)
        {
            return 0f;
        }

        var progress = 1f - Mathf.Clamp01(timer / duration);
        return Mathf.Sin(progress * Mathf.PI);
    }

    private static Color ResolveHealthColor(float ratio, bool isAlive, TeamSide side)
    {
        if (!isAlive)
        {
            return new Color(0.42f, 0.42f, 0.42f, 1f);
        }

        return side == TeamSide.Ally
            ? new Color(0.30f, 0.82f, 0.38f, 1f)
            : new Color(0.82f, 0.22f, 0.22f, 1f);
    }

    private static Color ResolveSemanticColor(BattleActionSemantic semantic)
    {
        return semantic switch
        {
            BattleActionSemantic.BasicAttack => new Color(1f, 0.82f, 0.28f, 1f),
            BattleActionSemantic.DamagingSkill => new Color(1f, 0.58f, 0.22f, 1f),
            BattleActionSemantic.HealSupport => new Color(0.42f, 1f, 0.60f, 1f),
            BattleActionSemantic.DefendHold => new Color(0.34f, 0.76f, 1f, 1f),
            BattleActionSemantic.Reposition => new Color(0.84f, 0.86f, 1f, 1f),
            BattleActionSemantic.Down => new Color(0.62f, 0.62f, 0.62f, 1f),
            _ => Color.white,
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    private static Color ResolveBaseColor(BattleUnitReadModel actor)
    {
        if (actor.RaceId == "human") return actor.Side == TeamSide.Ally ? new Color(0.25f, 0.55f, 1f) : new Color(0.34f, 0.46f, 0.96f);
        if (actor.RaceId == "beastkin") return actor.Side == TeamSide.Ally ? new Color(0.24f, 0.78f, 0.28f) : new Color(0.24f, 0.66f, 0.22f);
        if (actor.RaceId == "undead") return actor.Side == TeamSide.Ally ? new Color(0.62f, 0.62f, 0.72f) : new Color(0.48f, 0.48f, 0.56f);
        if (actor.ClassId == "mystic") return new Color(0.82f, 0.38f, 0.92f);
        return actor.Side == TeamSide.Ally ? new Color(0.25f, 0.88f, 0.88f) : new Color(0.94f, 0.35f, 0.35f);
    }

    private Color ResolveRestColor(BattleUnitReadModel actor)
    {
        if (!actor.IsAlive)
        {
            return Color.Lerp(_baseColor, Color.gray, 0.82f);
        }

        var color = _baseColor;
        if (_isSelected)
        {
            color = Color.Lerp(color, new Color(0.78f, 0.90f, 1f, 1f), 0.16f);
        }

        if (_isCurrentActor)
        {
            color = Color.Lerp(color, ResolveSemanticColor(ResolveCurrentSemantic()), 0.26f);
        }

        if (actor.IsDefending)
        {
            color = Color.Lerp(color, ResolveSemanticColor(BattleActionSemantic.DefendHold), 0.18f);
        }

        return color;
    }
}
