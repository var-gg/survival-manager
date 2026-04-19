using NUnit.Framework;
using SM.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class UiGraphicRaycastPolicyTests
{
    [Test]
    public void ApplyToHierarchy_LeavesOnlyInteractiveGraphicsRaycastable()
    {
        var root = new GameObject("Root", typeof(RectTransform));

        var buttonGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(root.transform, false);
        var buttonImage = buttonGo.GetComponent<Image>();

        var buttonLabelGo = new GameObject("Label", typeof(RectTransform));
        buttonLabelGo.transform.SetParent(buttonGo.transform, false);
        var buttonLabel = buttonLabelGo.AddComponent<Text>();

        var passiveTextGo = new GameObject("PassiveText", typeof(RectTransform));
        passiveTextGo.transform.SetParent(root.transform, false);
        var passiveText = passiveTextGo.AddComponent<Text>();

        var scrubberGo = new GameObject("Scrubber", typeof(RectTransform), typeof(Image), typeof(PointerDownProbe));
        scrubberGo.transform.SetParent(root.transform, false);
        var scrubberImage = scrubberGo.GetComponent<Image>();

        UiGraphicRaycastPolicy.ApplyToHierarchy(root.transform);

        Assert.That(buttonImage.raycastTarget, Is.True, "Button root should remain clickable.");
        Assert.That(buttonLabel.raycastTarget, Is.False, "Button label should not intercept pointer events.");
        Assert.That(passiveText.raycastTarget, Is.False, "Passive status text should not intercept pointer events.");
        Assert.That(scrubberImage.raycastTarget, Is.True, "Pointer-handler surfaces should remain raycastable.");

        Object.DestroyImmediate(root);
    }

    private sealed class PointerDownProbe : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
        }
    }
}
