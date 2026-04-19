using NUnit.Framework;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleAnimationEventBridgeTests
{
    [Test]
    public void RawEvent_OutsideCueWindow_IsIgnored()
    {
        var go = new GameObject("Bridge");

        try
        {
            var bridge = go.AddComponent<BattleAnimationEventBridge>();
            bridge.SetRawEventMapping("Impact", BattleAnimationHookType.Impact);

            Assert.That(bridge.TryHandleRawEvent("Impact"), Is.False);

            bridge.OpenCueWindow(new BattlePresentationCue(BattlePresentationCueType.ActionCommitSkill, 1, "ally"));

            Assert.That(bridge.TryHandleRawEvent("Impact"), Is.True);
            Assert.That(bridge.DispatchCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ClearTransientState_RemovesPendingHookWindow()
    {
        var go = new GameObject("Bridge");

        try
        {
            var bridge = go.AddComponent<BattleAnimationEventBridge>();
            bridge.OpenCueWindow(new BattlePresentationCue(BattlePresentationCueType.ActionCommitBasic, 1, "ally"));
            Assert.That(bridge.TryHandleHook(BattleAnimationHookType.Commit), Is.True);

            bridge.ClearTransientState(BattlePresentationCueType.PlaybackReset);

            Assert.That(bridge.TryHandleHook(BattleAnimationHookType.Impact), Is.False);
            Assert.That(bridge.DispatchCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
