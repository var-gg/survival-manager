using NUnit.Framework;
using System.IO;
using UnityEngine;

public class RepositorySmokeTests
{
    [Test]
    public void CriticalProjectFoldersExist()
    {
        Assert.That(Directory.Exists("Assets/_Game"), Is.True, "Assets/_Game folder is missing.");
        Assert.That(Directory.Exists("Assets/Tests/EditMode"), Is.True, "Assets/Tests/EditMode folder is missing.");
        Assert.That(Directory.Exists("Assets/ThirdParty"), Is.True, "Assets/ThirdParty folder is missing.");
    }

    [Test]
    public void SampleSceneExists()
    {
        Assert.That(File.Exists("Assets/Scenes/SampleScene.unity"), Is.True, "SampleScene.unity is missing.");
    }

    [Test]
    public void RepositoryReadmeAssetsExist()
    {
        Assert.That(File.Exists("Assets/Tests/README.md"), Is.True, "Assets/Tests/README.md is missing.");
        Assert.That(File.Exists("Assets/ThirdParty/README.md"), Is.True, "Assets/ThirdParty/README.md is missing.");
    }
}
