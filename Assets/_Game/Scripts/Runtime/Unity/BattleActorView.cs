using System.Collections;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleActorView : MonoBehaviour
{
    private const float OverlayHeight = 2.15f;

    private Camera _camera = null!;
    private RectTransform _overlayParent = null!;
    private RectTransform _overlayRoot = null!;
    private Image _hpFill = null!;
    private Text _nameText = null!;
    private Text _floatingText = null!;
    private Renderer _renderer = null!;
    private Vector3 _homePosition;
    private Color _baseColor;
    private BattleReplayActorSnapshot _currentState = null!;

    public string ActorId => _currentState.Id;
    public Vector3 HomePosition => _homePosition;

    public void Initialize(BattleReplayActorSnapshot actor, RectTransform overlayParent, Camera camera, Vector3 homePosition)
    {
        _overlayParent = overlayParent;
        _camera = camera;
        _homePosition = homePosition;
        transform.position = homePosition;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = actor.Row == RowPosition.Front
            ? new Vector3(0.95f, 1.15f, 0.95f)
            : new Vector3(0.90f, 1.0f, 0.90f);

        var collider = body.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        _renderer = body.GetComponent<Renderer>();
        _baseColor = ResolveBaseColor(actor);
        _renderer.material.color = _baseColor;

        CreateOverlay(actor);
        ApplyState(actor);
    }

    public void ApplyState(BattleReplayActorSnapshot actor)
    {
        _currentState = actor;
        if (_renderer != null)
        {
            _renderer.material.color = actor.IsAlive ? _baseColor : Color.Lerp(_baseColor, Color.gray, 0.8f);
        }

        transform.localScale = actor.IsAlive ? Vector3.one : new Vector3(1f, 0.82f, 1f);

        if (_hpFill != null)
        {
            _hpFill.fillAmount = actor.MaxHealth <= 0f ? 0f : actor.CurrentHealth / actor.MaxHealth;
            _hpFill.color = actor.IsAlive ? new Color(0.28f, 0.82f, 0.36f, 1f) : new Color(0.45f, 0.45f, 0.45f, 1f);
        }

        if (_nameText != null)
        {
            _nameText.text = $"{actor.Name}\n{Mathf.CeilToInt(actor.CurrentHealth)}/{Mathf.CeilToInt(actor.MaxHealth)}";
            _nameText.color = actor.IsAlive ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        RefreshOverlayPosition();
    }

    public void PlayAsSource(BattleReplayFrame frame, BattleActorView? targetView)
    {
        if (frame.ActionType == null)
        {
            return;
        }

        switch (frame.ActionType.Value)
        {
            case BattleActionType.BasicAttack:
                if (targetView != null)
                {
                    StartCoroutine(LungeRoutine(targetView.HomePosition, frame.DurationSeconds));
                }
                break;
            case BattleActionType.ActiveSkill:
                if (frame.Note == "heal_skill")
                {
                    StartCoroutine(PulseRoutine(new Color(0.2f, 1f, 0.4f, 1f), frame.DurationSeconds * 0.7f));
                }
                else if (targetView != null)
                {
                    StartCoroutine(LungeRoutine(targetView.HomePosition, frame.DurationSeconds));
                }
                break;
            case BattleActionType.WaitDefend:
                StartCoroutine(PulseRoutine(new Color(0.3f, 0.7f, 1f, 1f), frame.DurationSeconds * 0.7f));
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
            StartCoroutine(PulseRoutine(new Color(1f, 0.2f, 0.2f, 1f), frame.DurationSeconds * 0.6f));
            StartCoroutine(FloatingTextRoutine($"-{Mathf.CeilToInt(Mathf.Abs(delta))}", new Color(1f, 0.45f, 0.45f, 1f), frame.DurationSeconds * 0.65f));
            return;
        }

        StartCoroutine(PulseRoutine(new Color(0.2f, 1f, 0.4f, 1f), frame.DurationSeconds * 0.6f));
        StartCoroutine(FloatingTextRoutine($"+{Mathf.CeilToInt(delta)}", new Color(0.45f, 1f, 0.55f, 1f), frame.DurationSeconds * 0.65f));
    }

    public void RefreshOverlayPosition()
    {
        if (_camera == null || _overlayRoot == null || _overlayParent == null)
        {
            return;
        }

        var screenPosition = RectTransformUtility.WorldToScreenPoint(_camera, transform.position + Vector3.up * OverlayHeight);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayParent, screenPosition, null, out var anchored))
        {
            _overlayRoot.anchoredPosition = anchored;
        }
    }

    private void CreateOverlay(BattleReplayActorSnapshot actor)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var overlayGo = new GameObject($"{actor.Name}_Overlay", typeof(RectTransform));
        overlayGo.transform.SetParent(_overlayParent, false);

        _overlayRoot = overlayGo.GetComponent<RectTransform>();
        _overlayRoot.sizeDelta = new Vector2(140f, 52f);

        var nameGo = new GameObject("NameText", typeof(RectTransform));
        nameGo.transform.SetParent(_overlayRoot, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(140f, 32f);

        _nameText = nameGo.AddComponent<Text>();
        _nameText.font = font;
        _nameText.fontSize = 14;
        _nameText.alignment = TextAnchor.MiddleCenter;
        _nameText.color = Color.white;

        var barBackGo = new GameObject("HpBarBack", typeof(RectTransform));
        barBackGo.transform.SetParent(_overlayRoot, false);
        var barBackRect = barBackGo.GetComponent<RectTransform>();
        barBackRect.anchorMin = new Vector2(0.5f, 0f);
        barBackRect.anchorMax = new Vector2(0.5f, 0f);
        barBackRect.pivot = new Vector2(0.5f, 0f);
        barBackRect.anchoredPosition = new Vector2(0f, 4f);
        barBackRect.sizeDelta = new Vector2(112f, 12f);

        var barBackImage = barBackGo.AddComponent<Image>();
        barBackImage.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);

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

        var floatingGo = new GameObject("FloatingText", typeof(RectTransform));
        floatingGo.transform.SetParent(_overlayRoot, false);
        var floatingRect = floatingGo.GetComponent<RectTransform>();
        floatingRect.anchorMin = new Vector2(0.5f, 0.5f);
        floatingRect.anchorMax = new Vector2(0.5f, 0.5f);
        floatingRect.pivot = new Vector2(0.5f, 0.5f);
        floatingRect.anchoredPosition = new Vector2(0f, 18f);
        floatingRect.sizeDelta = new Vector2(96f, 24f);

        _floatingText = floatingGo.AddComponent<Text>();
        _floatingText.font = font;
        _floatingText.fontSize = 16;
        _floatingText.alignment = TextAnchor.MiddleCenter;
        _floatingText.color = Color.clear;
        _floatingText.text = string.Empty;
    }

    private IEnumerator LungeRoutine(Vector3 targetPosition, float duration)
    {
        var elapsed = 0f;
        var outboundDuration = Mathf.Max(0.08f, duration * 0.35f);
        var returnDuration = Mathf.Max(0.08f, duration * 0.45f);
        var direction = (targetPosition - _homePosition).normalized;
        var attackPosition = _homePosition + direction * 0.6f;

        while (elapsed < outboundDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(_homePosition, attackPosition, Mathf.Clamp01(elapsed / outboundDuration));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(attackPosition, _homePosition, Mathf.Clamp01(elapsed / returnDuration));
            yield return null;
        }

        transform.position = _homePosition;
    }

    private IEnumerator PulseRoutine(Color flashColor, float duration)
    {
        if (_renderer == null)
        {
            yield break;
        }

        var elapsed = 0f;
        var half = Mathf.Max(0.05f, duration * 0.5f);
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            _renderer.material.color = Color.Lerp(_baseColor, flashColor, Mathf.Clamp01(elapsed / half));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            _renderer.material.color = Color.Lerp(flashColor, _currentState.IsAlive ? _baseColor : Color.Lerp(_baseColor, Color.gray, 0.8f), Mathf.Clamp01(elapsed / half));
            yield return null;
        }

        _renderer.material.color = _currentState.IsAlive ? _baseColor : Color.Lerp(_baseColor, Color.gray, 0.8f);
    }

    private IEnumerator FloatingTextRoutine(string message, Color color, float duration)
    {
        if (_floatingText == null)
        {
            yield break;
        }

        var rect = _floatingText.rectTransform;
        var start = new Vector2(0f, 18f);
        var end = new Vector2(0f, 42f);
        _floatingText.text = message;
        _floatingText.color = color;

        var elapsed = 0f;
        while (elapsed < duration)
        {
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

    private static Color ResolveBaseColor(BattleReplayActorSnapshot actor)
    {
        if (actor.RaceId == "human") return actor.Side == TeamSide.Ally ? new Color(0.25f, 0.55f, 1f) : new Color(0.34f, 0.46f, 0.96f);
        if (actor.RaceId == "beastkin") return actor.Side == TeamSide.Ally ? new Color(0.24f, 0.78f, 0.28f) : new Color(0.24f, 0.66f, 0.22f);
        if (actor.RaceId == "undead") return actor.Side == TeamSide.Ally ? new Color(0.62f, 0.62f, 0.72f) : new Color(0.48f, 0.48f, 0.56f);
        if (actor.ClassId == "mystic") return new Color(0.82f, 0.38f, 0.92f);
        return actor.Side == TeamSide.Ally ? new Color(0.25f, 0.88f, 0.88f) : new Color(0.94f, 0.35f, 0.35f);
    }
}
