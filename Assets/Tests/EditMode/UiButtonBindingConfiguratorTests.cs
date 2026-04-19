using NUnit.Framework;
using SM.Unity;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class UiButtonBindingConfiguratorTests
{
    [Test]
    public void BindExclusive_Replaces_Persistent_And_Runtime_Clicks_With_Single_Action()
    {
        var buttonGo = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var button = buttonGo.GetComponent<Button>();

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(buttonGo.transform, false);
        var label = labelGo.AddComponent<Text>();

        var receiver = buttonGo.AddComponent<ButtonBindingProbe>();
        UnityEventTools.AddPersistentListener(button.onClick, receiver.RecordPersistent);
        button.onClick.AddListener(receiver.RecordRuntime);

        UiButtonBindingConfigurator.BindExclusive(button, receiver.RecordExclusive);
        button.onClick.Invoke();

        Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(0), "Runtime binding should replace serialized persistent listeners.");
        Assert.That(receiver.PersistentCalls, Is.EqualTo(0), "Old persistent listener should no longer run.");
        Assert.That(receiver.RuntimeCalls, Is.EqualTo(0), "Old runtime listener should no longer run.");
        Assert.That(receiver.ExclusiveCalls, Is.EqualTo(1), "Exclusive listener should run exactly once.");
        Assert.That(button.GetComponent<Image>().raycastTarget, Is.True, "Button root should remain clickable.");
        Assert.That(label.raycastTarget, Is.False, "Button label should not intercept pointer events.");

        Object.DestroyImmediate(buttonGo);
    }

    private sealed class ButtonBindingProbe : MonoBehaviour
    {
        public int PersistentCalls { get; private set; }
        public int RuntimeCalls { get; private set; }
        public int ExclusiveCalls { get; private set; }

        public void RecordPersistent()
        {
            PersistentCalls++;
        }

        public void RecordRuntime()
        {
            RuntimeCalls++;
        }

        public void RecordExclusive()
        {
            ExclusiveCalls++;
        }
    }
}
