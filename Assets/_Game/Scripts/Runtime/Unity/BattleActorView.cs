using System.Collections;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleActorView : MonoBehaviour
{
    private const float OverlayHeight = 2.3f;
    private const float WorldInfoHeight = 2.0f;
    private const float WorldHpWidth = 1.3f;

    private Camera _camera = null!;
    private BattlePresentationController _owner = null!;
    private RectTransform _overlayParent = null!;
    private RectTransform _overlayRoot = null!;
    private Image _overlayBackground = null!;
    private GameObject _overlayHpBarRoot = null!;
    private Image _hpFill = null!;
    private Text _nameText = null!;
    private Text _stateText = null!;
    private Text _floatingText = null!;
    private Transform _visualRoot = null!;
    private Transform _worldInfoRoot = null!;
    private Transform _worldHpFillRoot = null!;
    private TextMesh _worldNameText = null!;
    private TextMesh _worldNameShadowText = null!;
    private TextMesh _worldStateText = null!;
    private TextMesh _worldStateShadowText = null!;
    private Renderer _renderer = null!;
    private Renderer _shadowRenderer = null!;
    private Color _baseColor;
    private BattlePresentationOptions _options = BattlePresentationOptions.CreateDefault();
    private BattleUnitReadModel _currentState = null!;
    private Coroutine? _pulseRoutine;
    private Coroutine? _floatingRoutine;
    private Coroutine? _impactRoutine;
    private Coroutine? _accentRoutine;

    public void Initialize(
        BattleUnitReadModel actor,
        RectTransform overlayParent,
        Camera camera,
        BattlePresentationController owner)
    {
        _overlayParent = overlayParent;
        _camera = camera;
        _owner = owner;
        transform.position = ToWorldPosition(actor.Position);

        CreateVisualRoot(actor);
        CreateWorldInfo();
        CreateOverlay(actor);
        ApplyBlend(actor, actor, 1f);
    }

    public void ApplyOptions(BattlePresentationOptions options)
    {
        _options = options;
        RefreshVisibility();
    }

    public void ApplyBlend(BattleUnitReadModel from, BattleUnitReadModel to, float alpha)
    {
        _currentState = to;
        transform.position = Vector3.Lerp(ToWorldPosition(from.Position), ToWorldPosition(to.Position), Mathf.Clamp01(alpha));

        var displayedHealth = Mathf.Lerp(from.CurrentHealth, to.CurrentHealth, Mathf.Clamp01(alpha));
        ApplyDisplay(to, displayedHealth);
        RefreshOverlayPosition();
    }

    public void PlayAsSource(BattleEvent eventData, BattleActorView? targetView)
    {
        switch (eventData.ActionType)
        {
            case BattleActionType.BasicAttack:
                RestartCoroutine(ref _accentRoutine, AccentRoutine(new Color(1f, 0.84f, 0.24f, 1f), 0.20f));
                RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(1f, 0.84f, 0.24f, 1f), 0.18f, 1.04f));
                break;
            case BattleActionType.ActiveSkill when eventData.Note == "heal_skill":
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
        if (eventData.Note == "heal_skill")
        {
            RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(0.28f, 1f, 0.52f, 1f), 0.22f, 1.05f));
            RestartCoroutine(ref _floatingRoutine, FloatingTextRoutine($"+{Mathf.CeilToInt(eventData.Value)}", new Color(0.48f, 1f, 0.58f, 1f), 0.45f));
            return;
        }

        if (eventData.ActionType is BattleActionType.BasicAttack or BattleActionType.ActiveSkill)
        {
            RestartCoroutine(ref _impactRoutine, ImpactRoutine(0.22f));
            RestartCoroutine(ref _pulseRoutine, PulseRoutine(new Color(1f, 0.24f, 0.24f, 1f), 0.18f, 1.03f));
            RestartCoroutine(ref _floatingRoutine, FloatingTextRoutine($"-{Mathf.CeilToInt(eventData.Value)}", new Color(1f, 0.45f, 0.45f, 1f), 0.45f));
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

    private void ApplyDisplay(BattleUnitReadModel actor, float displayedHealth)
    {
        var restColor = ResolveRestColor(actor);
        var healthRatio = actor.MaxHealth <= 0f ? 0f : Mathf.Clamp01(displayedHealth / actor.MaxHealth);
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
            _visualRoot.localScale = actor.IsAlive ? Vector3.one : new Vector3(1f, 0.8f, 1f);
        }

        if (_hpFill != null)
        {
            _hpFill.fillAmount = healthRatio;
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
            _nameText.text = $"{actor.Name}\nHP {Mathf.CeilToInt(displayedHealth)}/{Mathf.CeilToInt(actor.MaxHealth)}";
            _nameText.color = actor.IsAlive ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        var statusLine = BuildStatusLine(actor);
        if (_stateText != null)
        {
            _stateText.text = statusLine;
            _stateText.color = actor.IsAlive ? new Color(0.88f, 0.92f, 1f, 1f) : new Color(0.68f, 0.68f, 0.68f, 1f);
        }

        if (_worldNameText != null && _worldNameShadowText != null)
        {
            _worldNameText.text = actor.Name;
            _worldNameShadowText.text = actor.Name;
        }

        if (_worldStateText != null && _worldStateShadowText != null)
        {
            _worldStateText.text = $"HP {Mathf.CeilToInt(displayedHealth)}/{Mathf.CeilToInt(actor.MaxHealth)}\n{statusLine}";
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

    private string BuildStatusLine(BattleUnitReadModel actor)
    {
        if (!actor.IsAlive)
        {
            return "Dead";
        }

        var target = string.IsNullOrWhiteSpace(actor.TargetName) ? "-" : actor.TargetName;
        return actor.ActionState switch
        {
            CombatActionState.Windup => $"Windup {Mathf.RoundToInt(actor.WindupProgress * 100f)}% -> {target}",
            CombatActionState.MoveToEngage => $"Advance -> {target}",
            CombatActionState.Retreat => $"Retreat <- {target}",
            CombatActionState.Recovery => actor.IsDefending ? "Guard" : $"Recover {actor.CooldownRemaining:0.0}s",
            CombatActionState.Reposition => actor.IsDefending ? "Hold Line" : "Reposition",
            CombatActionState.AdvanceToAnchor => "To Anchor",
            CombatActionState.SeekTarget => $"Seek -> {target}",
            _ => target == "-" ? actor.ActionState.ToString() : $"{actor.ActionState} -> {target}"
        };
    }

    private void CreateVisualRoot(BattleUnitReadModel actor)
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
        body.transform.localScale = actor.Anchor.IsFrontRow()
            ? new Vector3(0.96f, 1.18f, 0.96f)
            : new Vector3(0.90f, 1.02f, 0.90f);
        RemoveCollider(body);
        _renderer = body.GetComponent<Renderer>();
        _baseColor = ResolveBaseColor(actor);
        _renderer.material.color = _baseColor;
    }

    private void CreateWorldInfo()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var infoGo = new GameObject("WorldInfoRoot");
        infoGo.transform.SetParent(transform, false);
        _worldInfoRoot = infoGo.transform;

        _worldNameShadowText = CreateWorldText(_worldInfoRoot, "NameShadowText", font, new Vector3(0.02f, -0.02f, 0.03f), new Color(0.04f, 0.04f, 0.04f, 0.92f), 38, 0.06f);
        _worldNameText = CreateWorldText(_worldInfoRoot, "NameText", font, Vector3.zero, Color.white, 38, 0.06f);
        _worldStateShadowText = CreateWorldText(_worldInfoRoot, "StateShadowText", font, new Vector3(0.02f, -0.02f, 0.03f), new Color(0.04f, 0.04f, 0.04f, 0.92f), 26, 0.05f);
        _worldStateText = CreateWorldText(_worldInfoRoot, "StateText", font, Vector3.zero, Color.white, 26, 0.05f);
        _worldStateText.transform.localPosition = new Vector3(0f, -0.24f, 0f);
        _worldStateShadowText.transform.localPosition = new Vector3(0.02f, -0.26f, 0.03f);

        var barBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barBack.name = "WorldHpBack";
        barBack.transform.SetParent(_worldInfoRoot, false);
        barBack.transform.localPosition = new Vector3(0f, -0.52f, 0.02f);
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

    private void CreateOverlay(BattleUnitReadModel actor)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var overlayGo = new GameObject($"{actor.Name}_Overlay", typeof(RectTransform), typeof(Image));
        overlayGo.transform.SetParent(_overlayParent, false);

        _overlayRoot = overlayGo.GetComponent<RectTransform>();
        _overlayRoot.sizeDelta = new Vector2(154f, 78f);
        _overlayBackground = overlayGo.GetComponent<Image>();
        _overlayBackground.raycastTarget = false;

        _nameText = CreateOverlayText(_overlayRoot, "NameText", font, new Vector2(0f, -4f), new Vector2(146f, 34f), 14, TextAnchor.MiddleCenter);
        _stateText = CreateOverlayText(_overlayRoot, "StateText", font, new Vector2(0f, -34f), new Vector2(146f, 22f), 12, TextAnchor.MiddleCenter);

        var barBackGo = new GameObject("HpBarBack", typeof(RectTransform));
        barBackGo.transform.SetParent(_overlayRoot, false);
        _overlayHpBarRoot = barBackGo;
        var barBackRect = barBackGo.GetComponent<RectTransform>();
        barBackRect.anchorMin = new Vector2(0.5f, 0f);
        barBackRect.anchorMax = new Vector2(0.5f, 0f);
        barBackRect.pivot = new Vector2(0.5f, 0f);
        barBackRect.anchoredPosition = new Vector2(0f, 4f);
        barBackRect.sizeDelta = new Vector2(118f, 12f);

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
        floatingRect.anchoredPosition = new Vector2(0f, 8f);
        floatingRect.sizeDelta = new Vector2(156f, 34f);

        _floatingText = floatingGo.AddComponent<Text>();
        _floatingText.font = font;
        _floatingText.fontSize = 24;
        _floatingText.alignment = TextAnchor.MiddleCenter;
        _floatingText.color = Color.clear;
        AddOutline(_floatingText, new Color(0f, 0f, 0f, 0.92f));
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

        if (_stateText != null)
        {
            _stateText.enabled = _options.ShowOverlayActorHp;
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

    private static TextMesh CreateWorldText(Transform parent, string name, Font font, Vector3 localPosition, Color color, int fontSize, float characterSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var textMesh = go.AddComponent<TextMesh>();
        textMesh.font = font;
        textMesh.GetComponent<MeshRenderer>().material = font.material;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = characterSize;
        textMesh.color = color;
        return textMesh;
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
        return actor.IsAlive ? _baseColor : Color.Lerp(_baseColor, Color.gray, 0.82f);
    }
}
