using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleTimelineScrubberView : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private Image fillImage = null!;
    [SerializeField] private RectTransform trackRect = null!;

    private Action<float>? _onSeekRequested;
    private bool _interactable = true;
    private bool _isDragging;

    public bool IsDragging => _isDragging;

    public void Initialize(Image fill, RectTransform track, Action<float> onSeekRequested)
    {
        fillImage = fill;
        trackRect = track;
        _onSeekRequested = onSeekRequested;
    }

    public void SetInteractable(bool interactable)
    {
        _interactable = interactable;
    }

    public void SetFillAmount(float normalized)
    {
        if (fillImage != null && !_isDragging)
        {
            fillImage.fillAmount = Mathf.Clamp01(normalized);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        _isDragging = true;
        UpdateSeekFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_interactable || !_isDragging) return;
        UpdateSeekFromPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging = false;
    }

    private void UpdateSeekFromPointer(PointerEventData eventData)
    {
        if (trackRect == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                trackRect, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            return;
        }

        var rect = trackRect.rect;
        var normalized = Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width);

        if (fillImage != null)
        {
            fillImage.fillAmount = normalized;
        }

        _onSeekRequested?.Invoke(normalized);
    }
}
