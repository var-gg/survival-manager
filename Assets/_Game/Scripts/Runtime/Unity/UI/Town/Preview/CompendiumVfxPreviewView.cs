using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

internal sealed class CompendiumVfxPreviewView
{
    private const float DurationSeconds = 1.35f;

    private static readonly string[] StyleClasses =
    {
        "cmp-vfx-stage--melee",
        "cmp-vfx-stage--projectile",
        "cmp-vfx-stage--arcane",
        "cmp-vfx-stage--area",
        "cmp-vfx-stage--heal",
        "cmp-vfx-stage--guard",
        "cmp-vfx-stage--aura",
        "cmp-vfx-stage--control",
    };

    private readonly VisualElement _stage;
    private readonly Image _prefabPreviewImage;
    private readonly VisualElement _caster;
    private readonly VisualElement _target;
    private readonly VisualElement _trace;
    private readonly VisualElement _projectile;
    private readonly VisualElement _burst;
    private readonly VisualElement _pulseA;
    private readonly VisualElement _pulseB;
    private readonly Label _pattern;
    private readonly Label _route;
    private readonly Label _asset;
    private readonly Label _casterLabel;
    private readonly Label _targetLabel;
    private readonly Label _title;
    private readonly Label _caption;
    private readonly IVisualElementScheduledItem _animation;
    private readonly CompendiumPrefabVfxPreviewStage _prefabStage = new();

    private CompendiumVfxPreviewViewState? _state;
    private int _lastPlayToken = -1;
    private float _startedAt;

    public CompendiumVfxPreviewView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _stage = Require<VisualElement>(root, "CompendiumVfxPreviewStage");
        _prefabPreviewImage = Require<Image>(root, "CompendiumVfxPrefabPreviewImage");
        _caster = Require<VisualElement>(root, "CompendiumVfxCaster");
        _target = Require<VisualElement>(root, "CompendiumVfxTarget");
        _trace = Require<VisualElement>(root, "CompendiumVfxTrace");
        _projectile = Require<VisualElement>(root, "CompendiumVfxProjectile");
        _burst = Require<VisualElement>(root, "CompendiumVfxBurst");
        _pulseA = Require<VisualElement>(root, "CompendiumVfxPulseA");
        _pulseB = Require<VisualElement>(root, "CompendiumVfxPulseB");
        _pattern = Require<Label>(root, "CompendiumVfxPatternLabel");
        _route = Require<Label>(root, "CompendiumVfxRouteLabel");
        _asset = Require<Label>(root, "CompendiumVfxAssetLabel");
        _casterLabel = Require<Label>(root, "CompendiumVfxCasterLabel");
        _targetLabel = Require<Label>(root, "CompendiumVfxTargetLabel");
        _title = Require<Label>(root, "CompendiumVfxPreviewTitle");
        _caption = Require<Label>(root, "CompendiumVfxPreviewCaption");
        _animation = _stage.schedule.Execute(UpdateFrame).Every(16);
        _animation.Pause();
    }

    public void Render(CompendiumVfxPreviewViewState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _stage.style.display = state.CanPreview ? DisplayStyle.Flex : DisplayStyle.None;
        if (!state.CanPreview)
        {
            _animation.Pause();
            _prefabStage.Clear(_prefabPreviewImage);
            return;
        }

        _title.text = state.Title;
        _caption.text = string.IsNullOrWhiteSpace(state.HookId)
            ? state.Caption
            : $"{state.Caption} / {state.HookId}";
        _pattern.text = state.PatternLabel;
        _route.text = state.RouteLabel;
        _asset.text = state.AssetLabel;
        _casterLabel.text = state.CasterLabel;
        _targetLabel.text = state.TargetLabel;
        ApplyStyleClass(state.StyleKey);
        _prefabStage.Render(_prefabPreviewImage, state.Prefab, state.PlayToken);

        if (state.PlayToken != _lastPlayToken)
        {
            _lastPlayToken = state.PlayToken;
            Play();
            return;
        }

        ApplyFrame(1f);
    }

    private void Play()
    {
        _startedAt = Time.realtimeSinceStartup;
        ApplyFrame(0f);
        _animation.Resume();
    }

    private void UpdateFrame()
    {
        if (_state == null || !_state.CanPreview)
        {
            _animation.Pause();
            return;
        }

        var elapsed = Time.realtimeSinceStartup - _startedAt;
        var t = Mathf.Clamp01(elapsed / DurationSeconds);
        ApplyFrame(t);
        if (t >= 1f)
        {
            _animation.Pause();
        }
    }

    private void ApplyFrame(float t)
    {
        var style = _state?.StyleKey ?? string.Empty;
        if (style is "area" or "heal" or "guard" or "aura" or "control")
        {
            ApplyCenteredFrame(t, style);
            return;
        }

        ApplyTravelFrame(t, style);
    }

    private void ApplyTravelFrame(float t, string style)
    {
        var travel = EaseOut(Mathf.Clamp01(t / 0.72f));
        var impact = Mathf.Clamp01((t - 0.48f) / 0.42f);
        var fade = 1f - Mathf.Clamp01((t - 0.78f) / 0.22f);
        var arc = style == "arcane" ? Mathf.Sin(travel * Mathf.PI) * 9f : 0f;

        SetCircle(_caster, 13f, 50f, 34f + Mathf.Sin(t * Mathf.PI * 2f) * 2f, 0.92f);
        SetCircle(_target, 78f, 50f, 38f + impact * 8f, 0.78f);
        SetBar(_trace, 20f, 53f - arc * 0.08f, 52f * travel, 5f, 0.82f * fade);
        SetCircle(_projectile, 22f + 54f * travel, 48f - arc, 18f + impact * 10f, fade);
        SetCircle(_burst, 76f, 46f, 18f + impact * 76f, (1f - impact) * 0.92f);
        SetCircle(_pulseA, 75f, 45f, 32f + impact * 92f, (1f - impact) * 0.36f);
        SetCircle(_pulseB, 73f, 43f, 24f + impact * 52f, (1f - impact) * 0.48f);
        SetLabel(_casterLabel, 8f, 70f, 0.78f);
        SetLabel(_targetLabel, 73f, 70f, 0.72f + impact * 0.22f);
    }

    private void ApplyCenteredFrame(float t, string style)
    {
        var charge = Mathf.Clamp01(t / 0.34f);
        var bloom = Mathf.Clamp01((t - 0.24f) / 0.58f);
        var fade = 1f - Mathf.Clamp01((t - 0.82f) / 0.18f);
        var centerX = style is "heal" or "guard" or "aura" ? 45f : 52f;

        SetCircle(_caster, 20f, 50f, 34f + charge * 8f, 0.86f);
        SetCircle(_target, 72f, 50f, 34f + bloom * 10f, style == "control" ? 0.58f : 0.74f);
        SetBar(_trace, 26f, 53f, style == "control" ? 42f * bloom : 34f * charge, 4f, 0.36f * fade);
        SetCircle(_projectile, centerX, 48f, 20f + charge * 18f, 0.62f * fade);
        SetCircle(_burst, centerX - 3f, 42f, 30f + bloom * 104f, (1f - bloom) * 0.88f);
        SetCircle(_pulseA, centerX - 5f, 40f, 44f + bloom * 132f, (1f - bloom) * 0.34f);
        SetCircle(_pulseB, centerX + 2f, 45f, 24f + bloom * 72f, (1f - bloom) * 0.46f);
        SetLabel(_casterLabel, 15f, 70f, 0.78f);
        SetLabel(_targetLabel, 68f, 70f, 0.72f + bloom * 0.22f);
    }

    private void ApplyStyleClass(string styleKey)
    {
        foreach (var className in StyleClasses)
        {
            _stage.RemoveFromClassList(className);
        }

        var safeStyle = string.IsNullOrWhiteSpace(styleKey) ? "melee" : styleKey;
        _stage.AddToClassList($"cmp-vfx-stage--{safeStyle}");
    }

    private static void SetCircle(VisualElement element, float leftPercent, float topPercent, float size, float opacity)
    {
        element.style.left = Length.Percent(leftPercent);
        element.style.top = Length.Percent(topPercent);
        element.style.width = size;
        element.style.height = size;
        element.style.opacity = Mathf.Clamp01(opacity);
    }

    private static void SetBar(VisualElement element, float leftPercent, float topPercent, float widthPercent, float height, float opacity)
    {
        element.style.left = Length.Percent(leftPercent);
        element.style.top = Length.Percent(topPercent);
        element.style.width = Length.Percent(Mathf.Max(0f, widthPercent));
        element.style.height = height;
        element.style.opacity = Mathf.Clamp01(opacity);
    }

    private static void SetLabel(VisualElement element, float leftPercent, float topPercent, float opacity)
    {
        element.style.left = Length.Percent(leftPercent);
        element.style.top = Length.Percent(topPercent);
        element.style.opacity = Mathf.Clamp01(opacity);
    }

    private static float EaseOut(float value)
    {
        return 1f - Mathf.Pow(1f - Mathf.Clamp01(value), 3f);
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
