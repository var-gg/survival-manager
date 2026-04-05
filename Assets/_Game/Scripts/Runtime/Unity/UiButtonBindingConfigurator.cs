using UnityEngine.Events;
using UnityEngine.UI;

namespace SM.Unity;

public static class UiButtonBindingConfigurator
{
    public static void BindExclusive(Button button, UnityAction action)
    {
        var clickEvent = new Button.ButtonClickedEvent();
        clickEvent.AddListener(action);
        button.onClick = clickEvent;

        UiGraphicRaycastPolicy.ApplyToHierarchy(button.transform);
    }
}
