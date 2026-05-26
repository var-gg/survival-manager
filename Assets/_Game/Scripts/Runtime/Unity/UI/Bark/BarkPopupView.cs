using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Bark;

/// <summary>
/// HeroFaceCard의 sm-face-card__bark-anchor 위에 잠깐 떠오르는 Label.
/// minimal viable — 시간이 지나면 USS class 추가로 fade-out (실제 timing은 caller 또는 IVisualElementScheduledItem).
/// </summary>
public static class BarkPopupView
{
    public const string PopupClassName = "sm-bark-popup";
    public const string EmotionClassPrefix = "sm-bark-popup--";

    /// <summary>
    /// face card에 bark popup을 추가하고, durationSeconds 이후 자동 제거되도록 schedule.
    /// faceCardOrAnchor: HeroFaceCard root 또는 bark-anchor element.
    /// schedule이 없으면(즉 panel 없는 detached element) timer 없이 1회 노출만.
    /// </summary>
    public static VisualElement Attach(VisualElement faceCardOrAnchor, BarkEvent evt)
    {
        var popup = new Label(evt.BarkText) { name = $"BarkPopup_{evt.SourceId}" };
        popup.AddToClassList(PopupClassName);
        if (!string.IsNullOrEmpty(evt.EmotionKey))
        {
            popup.AddToClassList($"{EmotionClassPrefix}{evt.EmotionKey}");
        }
        popup.pickingMode = PickingMode.Ignore;

        // 같은 face card에 이미 popup이 떠 있으면 교체 — 단발성 보장.
        var existing = faceCardOrAnchor.Query<Label>(className: PopupClassName).ToList();
        foreach (var prev in existing)
        {
            prev.RemoveFromHierarchy();
        }

        faceCardOrAnchor.Add(popup);

        if (faceCardOrAnchor.schedule != null)
        {
            var durationMs = (long)(evt.DurationSeconds * 1000f);
            faceCardOrAnchor.schedule
                .Execute(() => popup.RemoveFromHierarchy())
                .StartingIn(durationMs);
        }

        return popup;
    }
}
