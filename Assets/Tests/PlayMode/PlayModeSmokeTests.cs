using System.Collections;
using NUnit.Framework;
using SM.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SM.Tests.PlayMode;

public class PlayModeSmokeTests
{
    [UnityTest]
    public IEnumerator BootScene_Creates_GameSessionRoot()
    {
        yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
        yield return null;
        yield return null;

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Boot").Or.EqualTo("Town"));
        Assert.That(GameSessionRoot.Instance, Is.Not.Null);
        Assert.That(GameSessionRoot.Instance!.SessionState, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator BootScene_Can_Advance_To_Town()
    {
        yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);

        var timeout = 120;
        while (timeout-- > 0 && SceneManager.GetActiveScene().name != "Town")
        {
            yield return null;
        }

        Assert.That(GameSessionRoot.Instance, Is.Not.Null);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Town"), "Boot scene should advance to Town when bootstrap succeeds.");
    }
}
