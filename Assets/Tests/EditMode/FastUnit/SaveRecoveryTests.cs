using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;
using UnityEngine;

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

    [Test]
    public void SaveDetailed_WritesPayloadWithoutUtf8Bom_AndManifestMatchesFileLength()
    {
        var root = CreateTempRoot();
        try
        {
            var repo = new JsonSaveRepository(root);
            var profile = new SaveProfile
            {
                ProfileId = "default",
                DisplayName = "Player",
            };

            var save = repo.SaveDetailed(profile);

            Assert.That(save.IsSuccessful, Is.True, save.Message);
            var payloadPath = Path.Combine(root, "default.json");
            var manifestPath = Path.Combine(root, "default.manifest.json");
            var payloadBytes = File.ReadAllBytes(payloadPath);
            var manifest = JsonUtility.FromJson<SaveManifestRecord>(File.ReadAllText(manifestPath));

            Assert.That(payloadBytes.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()), Is.False);
            Assert.That(manifest.PayloadBytes, Is.EqualTo(payloadBytes.Length));
            Assert.That(manifest.PayloadHash, Is.EqualTo(ComputeHash(payloadBytes)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void LoadOrCreateDetailed_AcceptsLegacyUtf8BomPayload_WhenManifestWasRecordedWithoutBom()
    {
        var root = CreateTempRoot();
        try
        {
            var repo = new JsonSaveRepository(root);
            var profile = new SaveProfile
            {
                ProfileId = "default",
                DisplayName = "Legacy",
            };
            profile.Currencies.Gold = 23;

            var json = JsonUtility.ToJson(profile, true);
            var bomlessPayloadBytes = Encoding.UTF8.GetBytes(json);
            var payloadPath = Path.Combine(root, "default.json");
            var manifestPath = Path.Combine(root, "default.manifest.json");
            File.WriteAllBytes(payloadPath, Encoding.UTF8.GetPreamble().Concat(bomlessPayloadBytes).ToArray());
            File.WriteAllText(
                manifestPath,
                "{\n"
                + "  \"ProfileId\": \"default\",\n"
                + "  \"SavedAtUtc\": \"2026-04-09T00:00:00.0000000Z\",\n"
                + "  \"CheckpointKind\": \"StartupLoad\",\n"
                + "  \"CompileHash\": \"\",\n"
                + $"  \"PayloadHash\": \"{ComputeHash(bomlessPayloadBytes)}\",\n"
                + $"  \"PayloadBytes\": {bomlessPayloadBytes.Length}\n"
                + "}",
                new UTF8Encoding(false));

            var result = repo.LoadOrCreateDetailed("default");

            Assert.That(result.Status, Is.EqualTo(SaveRepositoryLoadStatus.LoadedPrimary));
            Assert.That(result.Profile, Is.Not.Null);
            Assert.That(result.Profile!.Currencies.Gold, Is.EqualTo(23));
            Assert.That(result.PayloadBytes, Is.EqualTo(bomlessPayloadBytes.Length));
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

    private static string ComputeHash(byte[] bytes)
    {
        using var algorithm = SHA256.Create();
        var hash = algorithm.ComputeHash(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("X2"));
        }

        return builder.ToString();
    }
}
