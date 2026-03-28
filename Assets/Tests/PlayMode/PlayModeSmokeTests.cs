using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SM.Tests.PlayMode;

public class PlayModeSmokeTests
{
    [UnityTest]
    public System.Collections.IEnumerator Can_Create_Runtime_GameObject_In_PlayMode()
    {
        var go = new GameObject("PlayModeSmoke");
        Assert.That(go, Is.Not.Null);
        yield return null;
        Object.Destroy(go);
    }
}
