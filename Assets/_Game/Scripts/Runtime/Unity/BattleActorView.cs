using System.Collections;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleActorView : MonoBehaviour
{
    private const float OverlayHeight = 2.2f;
    private const float WorldInfoHeight = 2.05f;
    private const float WorldHpWidth = 1.3f;

    private Camera _camera = null!;
    private BattlePresentationController _owner = null!;
    private RectTransform _overlayParent = null!;
    private RectTransform _overlayRoot = null!;
    private Image _overlayBackground = null!;
    private GameObject _overlayHpBarRoot = null!;
    private Image _hpFill = null!;
    private Text _nameText = null!;
    private Text _floatingText = null!;
    private Transform _visualRoot = null!;
    private Transform _worldInfoRoot = null!;
    private Transform _worldHpFillRoot = null!;
    private TextMesh _worldNameText = null!;
    private TextMesh _worldNameShadowText = null!;
    private Renderer _renderer = null!;
    private Renderer _shadowRenderer = null!;
    private Vector3 _homePosition;
    private Color _baseColor;
    private BattlePresentationOptions _options = BattlePresentationOptions.CreateDefault();
    private BattleReplayActorSnapshot _currentState = null!;
    private Coroutine? _movementRoutine;
    private Coroutine? _pulseRoutine;
    private Coroutine? _floatingRoutine;
    private Coroutine? _impactRoutine;
    private Coroutine? _accentRoutine;

    public string ActorId => _currentState.Id;
    public Vector3 HomePosition => _homePosition;

    public void Initialize(
        BattleReplayActorSnapshot actor,
        RectTransform overlayParent,
        Camera camera,
        Vector3 homePosition,
        BattlePresentationController owner)
    {
        _overlayParent = overlayParent;
        _camera = camera;
        _owner = owner;
        _homePosition = homePosition;
        transform.position = homePosition;

        CreateVisualRoot(actor);
        CreateWorldInfo(actor);
        CreateOverlay(actor);
        ApplyState(actor);
    }

    public void ApplyOptions(BattlePresentationOptions options)
    {
        _options = options;
        RefreshVisibility();
    }

    public void ApplyState(BattleReplayActorSnapshot actor)
    {
        _currentState = actor;
        var restColor = ResolveRestColor();
        var healthRatio = actor.MaxHealth <= 0f ? 0f : Mathf.Clamp01(actor.CurrentHealth / actor.MaxHealth);
        var healthColor = ResolveHealthColor(healthRatio, actor.IsAlive);

        if (_renderer != null)
        {
            _renderer.material.color = restColor;
        }

        if (_shadowRenderer != null)
        {
            _shadowRenderer.material.color = actor.IsAlive
                ? new Color(0f, 0f, 0f, 0.28f)
                : new Color(0.12f, 0.12f, 0.12f, 0.20f);
        }

        if (_visualRoot != null)
        {
            _visualRoot.localScale = actor.IsAlive ? Vector3.one : new Vector3(1f, 0.82f, 1f);
        }

        if (_hpFill != null)
        {
            _hpFill.fillAmount = healthRatio;
            _hpFill.color = healthColor;
        }

        if (_overlayBackground != null)
        {
            var overlayTint = actor.Side == TeamSide.Ally
                ? new Color(0.08f, 0.12f, 0.18f, 0.66f)
                : new Color(0.18f, 0.10f, 0.10f, 0.66f);
            _overlayBackground.color = actor.IsAlive ? overlayTint : new Color(0.10f, 0.10f, 0.10f, 0.50f);
        }

        if (_nameText != null)
        {
            _nameText.text = $"{actor.Name}\nHP {Mathf.CeilToInt(actor.CurrentHealth)}/{Mathf.CeilToInt(actor.MaxHealth)}";
            _nameText.color = actor.IsAlive ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        if (_worldNameText != null && _worldNameShadowText != null)
        {
            var label = $"{actor.Name}\n{Mathf.CeilToInt(actor.CurrentHealth)}/{Mathf.CeilToInt(actor.MaxHealth)}";
            _worldNameText.text = label;
            _worldNameShadowText.text = label;
            _worldNameText.color = actor.IsAlive ? Color.white : new Color(0.78f, 0.78f, 0.78f, 1f);
            _worldNameShadowText.color = new Color(0.04f, 0.04f, 0.04f, 0.92f);
        }

        if (_worldHpFillRoot != null)
        {
            var ratio = actor.MaxHealth <= 0f ? 0f : Mathf.Clamp01(actor.CurrentHealth / actor.MaxHealth);
            var width = Mathf.Max(0.05f, WorldHpWidth * ratio);
            _worldHpFillRoot.localScale = new Vector3(width, 0.08f, 0.06f);
            _worldHpFillRoot.localPosition = new Vector3((-WorldHpWidth * 0.5f) + (width * 0.5f), -0.42f, 0f);
            var fillRenderer = _worldHpFillRoot.GetComponent<Renderer>();
            if (fillRenderer != null)
            {
                fillRenderer.material.color = healthColor;
            }
        }

        RefreshVisibility();
        RefreshOverlayPosition();
    }

    public void PlayAsSource(BattleReplayFrame frame, BattleActorView? targetView)
    {
        if (frame.ActionType == null)
        {
            return;
        }

        RestartCoroutine(ref _accentRoutine, AccentRoutine(new Color(1f, 0.82f, 0.26f, 1f), frame.DurationSeconds * 0.9f));

        switch (frame.ActionType.Value)
        {
            case BattleActionType.BasicAttack:
                if (targetView != null)
                {
                    RestartCoroutine(ref _movementRoutine, LungeRoutine(targetView.HomePosition, frame.DurationSeconds));
                }
                break;
            case BattleActionType.ActiveSkill:
                if (frame.Note == "heal_skill")
                {
                    RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(0.22f, 1f, 0.52f, 1f), frame.DurationSeconds * 0.8f, 1.06f));
                }
                else if (targetView != null)
                {
                    RestartCoroutine(ref _movementRoutine, LungeRoutine(targetView.HomePosition, frame.DurationSeconds));
                }
                break;
            case BattleActionType.WaitDefend:
                RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(0.32f, 0.72f, 1f, 1f), frame.DurationSeconds * 0.8f, 1.05f));
                break;
        }
    }

    public void PlayAsTarget(BattleReplayFrame frame)
    {
        if (frame.ActionType == null || frame.BeforeTargetHealth == null || frame.AfterTargetHealth == null)
        {
            return;
        }

        var delta = frame.AfterTargetHealth.Value - frame.BeforeTargetHealth.Value;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        if (delta < 0f)
        {
            RestartCoroutine(ref _impactRoutine, ImpactRoutine(frame.DurationSeconds * 0.65f));
            RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(1f, 0.22f, 0.22f, 1f), frame.DurationSeconds * 0.7f, 1.03f));
            RestartCoroutine(ref _floatingRoutine, FloatingTextRoutine($"-{Mathf.CeilToInt(Mathf.Abs(delta))}", new Color(1f, 0.45f, 0.45f, 1f), frame.DurationSeconds * 0.72f));
            return;
        }

        RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(0.24f, 1f, 0.48f, 1f), frame.DurationSeconds * 0.72f, 1.05f));
        RestartCoroutine(ref _floatingRoutine, FloatingTextRoutine($"+{Mathf.CeilToInt(delta)}", new Color(0.48f, 1f, 0.58f, 1f), frame.DurationSeconds * 0.72f));
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
            var screenPosition = RectTransformUtility.WorldToScreenPoint(_camera, transform.position + Vector3.up * OverlayHeight);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayParent, screenPosition, null, out var anchored))
            {
                _overlayRoot.anchoredPosition = anchored;
            }
        }

        if (_worldInfoRoot != null)
        {
            _worldInfoRoot.position = transform.position + Vector3.up * WorldInfoHeight;
            var facing = _camera.transform.position - _worldInfoRoot.position;
            if (facing.sqrMagnitude > 0.001f)
            {
                _worldInfoRoot.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }
        }
    }

    private bool IsPaused => _owner != null && _owner.IsPaused;

    private void CreateVisualRoot(BattleReplayActorSnapshot actor)
    {
        var visualGo = new GameObject("VisualRoot");
        visualGo.transform.SetParent(transform, false);
        _visualRoot = visualGo.transform;

        var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "GroundShadow";
        shadow.transform.SetParent(_visualRoot, false);
        shadow.transform.localPosition = new Vector3(0f, -1.02f, 0f);
        shadow.transform.localScale = new Vector3(0.58f, 0.03f, 0.58f);
        RemoveCollider(shadow);
        _shadowRenderer = shadow.GetComponent<Renderer>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(_visualRoot, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = actor.Row == RowPosition.Front
            ? new Vector3(0.96f, 1.18f, 0.96f)
            : new Vector3(0.90f, 1.02f, 0.90f);
        RemoveCollider(body);
        _renderer = body.GetComponent<Renderer>();
        _baseColor = ResolveBaseColor(actor);
        _renderer.material.color = _baseColor;
    }

    private void CreateWorldInfo(BattleReplayActorSnapshot actor)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var infoGo = new GameObject("WorldInfoRoot");
        infoGo.transform.SetParent(transform, false);
        _worldInfoRoot = infoGo.transform;
        _worldInfoRoot.localPosition = new Vector3(0f, WorldInfoHeight, 0f);

        _worldNameShadowText = CreateWorldText(_worldInfoRoot, "NameShadowText", font, new Vector3(0.02f, -0.02f, 0.03f), new Color(0.04f, 0.04f, 0.04f, 0.92f));
        _worldNameText = CreateWorldText(_worldInfoRoot, "NameText", font, Vector3.zero, Color.white);

        var barBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barBack.name = "WorldHpBack";
        barBack.transform.SetParent(_worldInfoRoot, false);
        barBack.transform.localPosition = new Vector3(0f, -0.42f, 0.02f);
        barBack.transform.localScale = new Vector3(WorldHpWidth, 0.10f, 0.07f);
        RemoveCollider(barBack);
        var backRenderer = barBack.GetComponent<Renderer>();
        backRenderer.material.color = new Color(0.04f, 0.04f, 0.04f, 1f);

        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "WorldHpFill";
        fill.transform.SetParent(_worldInfoRoot, false);
        RemoveCollider(fill);
        _worldHpFillRoot = fill.transform;
    }

    private void CreateOverlay(BattleReplayActorSnapshot actor)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var overlayGo = new GameObject($"{actor.Name}_Overlay", typeof(RectTransform), typeof(Image));
        overlayGo.transform.SetParent(_overlayParent, false);

        _overlayRoot = overlayGo.GetComponent<RectTransform>();
        _overlayRoot.sizeDelta = new Vector2(148f, 60f);
        _overlayBackground = overlayGo.GetComponent<Image>();
        _overlayBackground.raycastTarget = false;

        var nameGo = new GameObject("NameText", typeof(RectTransform));
        nameGo.transform.SetParent(_overlayRoot, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -2f);
        nameRect.sizeDelta = new Vector2(140f, 34f);

        _nameText = nameGo.AddComponent<Text>();
        _nameText.font = font;
        _nameText.fontSize = 15;
        _nameText.alignment = TextAnchor.MiddleCenter;
        _nameText.color = Color.white;
        AddOutline(_nameText, new Color(0f, 0f, 0f, 0.86f));

        var barBackGo = new GameObject("HpBarBack", typeof(RectTransform));
        barBackGo.transform.SetParent(_overlayRoot, false);
        _overlayHpBarRoot = barBackGo;
        var barBackRect = barBackGo.GetComponent<RectTransform>();
        barBackRect.anchorMin = new Vector2(0.5f, 0f);
        barBackRect.anchorMax = new Vector2(0.5f, 0f);
        barBackRect.pivot = new Vector2(0.5f, 0f);
        barBackRect.anchoredPosition = new Vector2(0f, 4f);
        barBackRect.sizeDelta = new Vector2(116f, 12f);

        var barBackImage = barBackGo.AddComponent<Image>();
        barBackImage.color = new Color(0.04f, 0.04f, 0.04f, 0.92f);
        barBackImage.raycastTarget = false;

        var fillGo = new GameObject("HpBarFill", typeof(RectTransform));
        fillGo.transform.SetParent(barBackRect, false);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        _hpFill = fillGo.AddComponent<Image>();
        _hpFill.type = Image.Type.Filled;
        _hpFill.fillMethod = Image.FillMethod.Horizontal;
        _hpFill.fillOrigin = 0;
        _hpFill.raycastTarget = false;

        var floatingGo = new GameObject("FloatingText", typeof(RectTransform));
        floatingGo.transform.SetParent(_overlayRoot, false);
        var floatingRect = floatingGo.GetComponent<RectTransform>();
        floatingRect.anchorMin = new Vector2(0.5f, 0.5f);
        floatingRect.anchorMax = new Vector2(0.5f, 0.5f);
        floatingRect.pivot = new Vector2(0.5f, 0.5f);
        floatingRect.anchoredPosition = new Vector2(0f, 12f);
        floatingRect.sizeDelta = new Vector2(156f, 38f);

        _floatingText = floatingGo.AddComponent<Text>();
        _floatingText.font = font;
        _floatingText.fontSize = 26;
        _floatingText.alignment = TextAnchor.MiddleCenter;
        _floatingText.color = Color.clear;
        _floatingText.text = string.Empty;
        AddOutline(_floatingText, new Color(0f, 0f, 0f, 0.92f));
    }

    private IEnumerator LungeRoutine(Vector3 targetPosition, float duration)
    {
        var direction = (targetPosition - _homePosition).normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = _currentState.Side == TeamSide.Ally ? Vector3.right : Vector3.left;
        }

        var anticipation = _homePosition - (direction * 0.12f);
        var impact = _homePosition + (direction * 0.72f);
        var anticipationDuration = Mathf.Max(0.06f, duration * 0.18f);
        var burstDuration = Mathf.Max(0.08f, duration * 0.18f);
        var holdDuration = Mathf.Max(0.03f, duration * 0.08f);
        var returnDuration = Mathf.Max(0.12f, duration * 0.42f);

        yield return AnimateWorldPosition(_homePosition, anticipation, anticipationDuration, true);
        yield return AnimateWorldPosition(anticipation, impact, burstDuration, false);
        yield return Hold(holdDuration);
        yield return AnimateWorldPosition(impact, _homePosition, returnDuration, true);
        transform.position = _homePosition;
    }

    private IEnumerator ImpactRoutine(float duration)
    {
        if (_visualRoot == null)
        {
            yield break;
        }

        var direction = _currentState.Side == TeamSide.Ally ? Vector3.left : Vector3.right;
        var impactPosition = direction * 0.16f;
        var impactScale = new Vector3(1.08f, 0.92f, 1.08f);
        var forwardDuration = Mathf.Max(0.06f, duration * 0.35f);
        var returnDuration = Mathf.Max(0.08f, duration * 0.45f);

        yield return AnimateLocalVisual(Vector3.zero, impactPosition, Vector3.one, impactScale, forwardDuration);
        yield return AnimateLocalVisual(impactPosition, Vector3.zero, impactScale, Vector3.one, returnDuration);
        _visualRoot.localPosition = Vector3.zero;
        _visualRoot.localScale = _currentState.IsAlive ? Vector3.one : new Vector3(1f, 0.82f, 1f);
    }

    private IEnumerator PulseRoutine(Color flashColor, float duration, float punchScale)
    {
        if (_renderer == null || _visualRoot == null)
        {
            yield break;
        }

        var baseScale = _currentState.IsAlive ? Vector3.one : new Vector3(1f, 0.82f, 1f);
        var peakScale = baseScale * punchScale;
        var restColor = ResolveRestColor();
        var half = Mathf.Max(0.05f, duration * 0.5f);

        yield return AnimatePulse(restColor, flashColor, baseScale, peakScale, half);
        yield return AnimatePulse(flashColor, restColor, peakScale, baseScale, half);
        _renderer.material.color = restColor;
        _visualRoot.localScale = baseScale;
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
        _shadowRenderer.material.color = baseShadowColor;
        _overlayBackground.color = baseOverlayColor;
    }

    private IEnumerator FloatingTextRoutine(string message, Color color, float duration)
    {
        if (_floatingText == null)
        {
            yield break;
        }

        var rect = _floatingText.rectTransform;
        var start = new Vector2(0f, 10f);
        var end = new Vector2(0f, 52f);
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
            rect.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, progress));
            _floatingText.color = new Color(color.r, color.g, color.b, 1f - progress);
            yield return null;
        }

        rect.anchoredPosition = start;
        _floatingText.color = Color.clear;
        _floatingText.text = string.Empty;
    }

    private IEnumerator AnimateWorldPosition(Vector3 from, Vector3 to, float duration, bool smooth)
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
            t = smooth ? Mathf.SmoothStep(0f, 1f, t) : 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
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

    private IEnumerator Hold(float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsPaused)
            {
                elapsed += Time.deltaTime;
            }

            yield return null;
        }
    }

    private Color ResolveRestColor()
    {
        return _currentState.IsAlive ? _baseColor : Color.Lerp(_baseColor, Color.gray, 0.82f);
    }

    private void RefreshVisibility()
    {
        if (_worldInfoRoot != null)
        {
            _worldInfoRoot.gameObject.SetActive(_options.ShowWorldActorHp);
        }

        if (_overlayBackground != null)
        {
            _overlayBackground.enabled = _options.ShowOverlayActorHp;
        }

        if (_nameText != null)
        {
            _nameText.enabled = _options.ShowOverlayActorHp;
        }

        if (_overlayHpBarRoot != null)
        {
            _overlayHpBarRoot.SetActive(_options.ShowOverlayActorHp);
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

    private static TextMesh CreateWorldText(Transform parent, string name, Font font, Vector3 localPosition, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var textMesh = go.AddComponent<TextMesh>();
        textMesh.font = font;
        textMesh.GetComponent<MeshRenderer>().material = font.material;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 44;
        textMesh.characterSize = 0.06f;
        textMesh.color = color;
        return textMesh;
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
            Destroy(collider);
        }
    }

    private static Color ResolveHealthColor(float ratio, bool isAlive)
    {
        if (!isAlive)
        {
            return new Color(0.42f, 0.42f, 0.42f, 1f);
        }

        if (ratio <= 0.25f)
        {
            return new Color(0.96f, 0.28f, 0.24f, 1f);
        }

        if (ratio <= 0.55f)
        {
            return new Color(0.96f, 0.74f, 0.24f, 1f);
        }

        return new Color(0.34f, 0.90f, 0.42f, 1f);
    }

    private static Color ResolveBaseColor(BattleReplayActorSnapshot actor)
    {
        if (actor.RaceId == "human") return actor.Side == TeamSide.Ally ? new Color(0.25f, 0.55f, 1f) : new Color(0.34f, 0.46f, 0.96f);
        if (actor.RaceId == "beastkin") return actor.Side == TeamSide.Ally ? new Color(0.24f, 0.78f, 0.28f) : new Color(0.24f, 0.66f, 0.22f);
        if (actor.RaceId == "undead") return actor.Side == TeamSide.Ally ? new Color(0.62f, 0.62f, 0.72f) : new Color(0.48f, 0.48f, 0.56f);
        if (actor.ClassId == "mystic") return new Color(0.82f, 0.38f, 0.92f);
        return actor.Side == TeamSide.Ally ? new Color(0.25f, 0.88f, 0.88f) : new Color(0.94f, 0.35f, 0.35f);
    }
}
