using System;
using System.IO;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SaveRecoveryTests
{
    [Test]
    public void LoadOrCreateDetailed_CreatesMissingProfile()
    {
        var root = CreateTempRoot();
        try
        {
            var repo = new JsonSaveRepository(root);

            var result = repo.LoadOrCreateDetailed("default");

            Assert.That(result.Status, Is.EqualTo(SaveRepositoryLoadStatus.MissingCreated));
            Assert.That(result.Profile, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(root, "default.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "default.manifest.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "default.bak.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "default.bak.manifest.json")), Is.True);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void LoadOrCreateDetailed_RecoversFromBackup_WhenPrimaryIsCorrupted()
    {
        var root = CreateTempRoot();
        try
        {
            var repo = new JsonSaveRepository(root);
            var profile = new SaveProfile { ProfileId = "default" };
            profile.Currencies.Gold = 17;
            Assert.That(repo.SaveDetailed(profile).IsSuccessful, Is.True);

            File.WriteAllText(Path.Combine(root, "default.json"), "{ corrupted");

            var recovered = repo.LoadOrCreateDetailed("default");
            var roundTrip = repo.LoadOrCreateDetailed("default");

            Assert.That(recovered.Status, Is.EqualTo(SaveRepositoryLoadStatus.LoadedBackupRecovered));
            Assert.That(recovered.Profile?.Currencies.Gold, Is.EqualTo(17));
            Assert.That(recovered.QuarantinePath, Is.Not.Empty);
            Assert.That(Directory.Exists(recovered.QuarantinePath), Is.True);
            Assert.That(roundTrip.Status, Is.EqualTo(SaveRepositoryLoadStatus.LoadedPrimary));
            Assert.That(roundTrip.Profile?.Currencies.Gold, Is.EqualTo(17));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void LoadOrCreateDetailed_Fails_WhenPrimaryArtifactsAreCorruptedAndBackupIsMissing()
    {
        var root = CreateTempRoot();
        try
        {
            var repo = new JsonSaveRepository(root);
            var profile = new SaveProfile { ProfileId = "default" };
            profile.Currencies.Gold = 9;
            Assert.That(repo.SaveDetailed(profile).IsSuccessful, Is.True);

            File.Delete(Path.Combine(root, "default.bak.json"));
            File.Delete(Path.Combine(root, "default.bak.manifest.json"));
            File.WriteAllText(Path.Combine(root, "default.json"), "{ corrupted");

            var result = repo.LoadOrCreateDetailed("default");

            Assert.That(result.Status, Is.EqualTo(SaveRepositoryLoadStatus.FailedCorrupt));
            Assert.That(result.Profile, Is.Null);
            Assert.That(result.QuarantinePath, Is.Not.Empty);
            Assert.That(Directory.Exists(result.QuarantinePath), Is.True);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm_save_recovery_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
