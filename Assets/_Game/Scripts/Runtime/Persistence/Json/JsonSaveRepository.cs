using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;

namespace SM.Persistence.Json;

public sealed class JsonSaveRepository : ISaveRepository, ISaveRepositoryDiagnostics
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly string _rootDirectory;
    private readonly string _quarantineDirectory;

    public JsonSaveRepository(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _quarantineDirectory = Path.Combine(_rootDirectory, "quarantine");
        Directory.CreateDirectory(_rootDirectory);
    }

    public SaveProfile LoadOrCreate(string profileId)
    {
        var result = LoadOrCreateDetailed(profileId);
        if (result.IsSuccessful && result.Profile != null)
        {
            return result.Profile;
        }

        throw new InvalidDataException(string.IsNullOrWhiteSpace(result.Message)
            ? $"Profile '{profileId}' could not be recovered."
            : result.Message);
    }

    public void Save(SaveProfile profile)
    {
        var result = SaveDetailed(profile);
        if (!result.IsSuccessful)
        {
            throw new IOException(string.IsNullOrWhiteSpace(result.Message)
                ? $"Profile '{profile.ProfileId}' could not be saved."
                : result.Message);
        }
    }

    public SaveRepositoryLoadResult LoadOrCreateDetailed(string profileId, SaveRepositoryRequest? request = null)
    {
        request ??= new SaveRepositoryRequest();
        var artifacts = GetArtifacts(profileId);

        var primaryAttempt = TryReadProfileArtifacts(artifacts.PrimaryPayloadPath, artifacts.PrimaryManifestPath, profileId);
        if (primaryAttempt.Status == ProfileReadStatus.Valid)
        {
            CleanupStaleTemporaryArtifacts(artifacts);
            return new SaveRepositoryLoadResult
            {
                Status = SaveRepositoryLoadStatus.LoadedPrimary,
                Profile = primaryAttempt.Profile,
                Manifest = primaryAttempt.Manifest,
                Message = "Primary save loaded.",
                RecoveryPath = artifacts.PrimaryPayloadPath,
                ManifestVerified = true,
                PayloadBytes = primaryAttempt.PayloadBytes,
            };
        }

        var backupAttempt = TryReadProfileArtifacts(artifacts.BackupPayloadPath, artifacts.BackupManifestPath, profileId);
        if (backupAttempt.Status == ProfileReadStatus.Valid)
        {
            var quarantinePath = QuarantineInvalidPrimaryArtifacts(artifacts, primaryAttempt);
            PromoteBackupToPrimary(artifacts);
            CleanupStaleTemporaryArtifacts(artifacts);

            return new SaveRepositoryLoadResult
            {
                Status = SaveRepositoryLoadStatus.LoadedBackupRecovered,
                Profile = backupAttempt.Profile,
                Manifest = backupAttempt.Manifest,
                Message = string.IsNullOrWhiteSpace(primaryAttempt.Message)
                    ? "Backup save recovered."
                    : $"Primary save failed verification. Backup recovered. {primaryAttempt.Message}",
                RecoveryPath = artifacts.BackupPayloadPath,
                QuarantinePath = quarantinePath,
                UsedBackup = true,
                ManifestVerified = true,
                PayloadBytes = backupAttempt.PayloadBytes,
            };
        }

        var hasAnyExistingArtifacts =
            primaryAttempt.Status != ProfileReadStatus.Missing
            || backupAttempt.Status != ProfileReadStatus.Missing
            || artifacts.HasAnyTemporaryArtifacts;

        if (!hasAnyExistingArtifacts)
        {
            var profile = new SaveProfile { ProfileId = profileId };
            var saveResult = SaveDetailed(profile, request);
            return saveResult.IsSuccessful
                ? new SaveRepositoryLoadResult
                {
                    Status = SaveRepositoryLoadStatus.MissingCreated,
                    Profile = profile,
                    Manifest = saveResult.Manifest,
                    Message = "No save found. New profile created.",
                    RecoveryPath = artifacts.PrimaryPayloadPath,
                    CreatedNewProfile = true,
                    ManifestVerified = true,
                    PayloadBytes = saveResult.PayloadBytes,
                }
                : new SaveRepositoryLoadResult
                {
                    Status = SaveRepositoryLoadStatus.FailedCorrupt,
                    Message = string.IsNullOrWhiteSpace(saveResult.Message)
                        ? "Missing profile could not be initialized."
                        : saveResult.Message,
                    RecoveryPath = artifacts.PrimaryPayloadPath,
                    QuarantinePath = saveResult.QuarantinePath,
                };
        }

        var quarantineResult = QuarantineArtifacts(artifacts, primaryAttempt, backupAttempt);
        var error = BuildCorruptionMessage(primaryAttempt, backupAttempt);
        return new SaveRepositoryLoadResult
        {
            Status = SaveRepositoryLoadStatus.FailedCorrupt,
            Message = error,
            RecoveryPath = artifacts.PrimaryPayloadPath,
            QuarantinePath = quarantineResult,
        };
    }

    public SaveRepositorySaveResult SaveDetailed(SaveProfile profile, SaveRepositoryRequest? request = null)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        request ??= new SaveRepositoryRequest();
        var artifacts = GetArtifacts(profile.ProfileId);
        var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
        var payloadBytes = Encoding.UTF8.GetBytes(json);
        var manifest = new SaveManifestRecord
        {
            ProfileId = profile.ProfileId,
            SavedAtUtc = DateTime.UtcNow.ToString("O"),
            CheckpointKind = request.CheckpointKind ?? string.Empty,
            CompileHash = string.IsNullOrWhiteSpace(request.CompileHash) ? profile.ActiveRun?.CompileHash ?? string.Empty : request.CompileHash,
            PayloadHash = ComputeHash(payloadBytes),
            PayloadBytes = payloadBytes.Length,
        };

        try
        {
            Directory.CreateDirectory(_rootDirectory);

            File.WriteAllText(artifacts.TempPayloadPath, json, Utf8NoBom);
            File.WriteAllText(
                artifacts.TempManifestPath,
                JsonConvert.SerializeObject(manifest, Formatting.Indented),
                Utf8NoBom);

            var tempAttempt = TryReadProfileArtifacts(artifacts.TempPayloadPath, artifacts.TempManifestPath, profile.ProfileId);
            if (tempAttempt.Status != ProfileReadStatus.Valid)
            {
                var quarantinePath = QuarantineTemporaryArtifacts(artifacts, tempAttempt.Message);
                return new SaveRepositorySaveResult
                {
                    Status = SaveRepositorySaveStatus.Failed,
                    Message = string.IsNullOrWhiteSpace(tempAttempt.Message)
                        ? "Temporary save verification failed."
                        : tempAttempt.Message,
                    RecoveryPath = artifacts.PrimaryPayloadPath,
                    QuarantinePath = quarantinePath,
                    PayloadBytes = payloadBytes.Length,
                };
            }

            PrepareBackupArtifacts(artifacts);
            CommitPrimaryArtifacts(artifacts);

            var primaryAttempt = TryReadProfileArtifacts(artifacts.PrimaryPayloadPath, artifacts.PrimaryManifestPath, profile.ProfileId);
            if (primaryAttempt.Status != ProfileReadStatus.Valid)
            {
                var quarantinePath = QuarantinePrimaryArtifacts(artifacts, primaryAttempt.Message);
                PromoteBackupToPrimary(artifacts);
                return new SaveRepositorySaveResult
                {
                    Status = SaveRepositorySaveStatus.Failed,
                    Manifest = primaryAttempt.Manifest ?? manifest,
                    Message = string.IsNullOrWhiteSpace(primaryAttempt.Message)
                        ? "Primary save verification failed after replace."
                        : primaryAttempt.Message,
                    RecoveryPath = artifacts.BackupPayloadPath,
                    QuarantinePath = quarantinePath,
                    PayloadBytes = payloadBytes.Length,
                };
            }

            CleanupStaleTemporaryArtifacts(artifacts);
            return new SaveRepositorySaveResult
            {
                Status = SaveRepositorySaveStatus.Success,
                Manifest = manifest,
                Message = "Save completed.",
                RecoveryPath = artifacts.PrimaryPayloadPath,
                PayloadBytes = payloadBytes.Length,
            };
        }
        catch (Exception ex)
        {
            var quarantinePath = QuarantineTemporaryArtifacts(artifacts, ex.Message);
            var primaryAttempt = TryReadProfileArtifacts(artifacts.PrimaryPayloadPath, artifacts.PrimaryManifestPath, profile.ProfileId);
            if (primaryAttempt.Status != ProfileReadStatus.Valid)
            {
                var primaryQuarantinePath = QuarantinePrimaryArtifacts(artifacts, primaryAttempt.Message);
                PromoteBackupToPrimary(artifacts);
                if (!string.IsNullOrWhiteSpace(primaryQuarantinePath))
                {
                    quarantinePath = primaryQuarantinePath;
                }
            }

            return new SaveRepositorySaveResult
            {
                Status = SaveRepositorySaveStatus.Failed,
                Manifest = manifest,
                Message = ex.Message,
                RecoveryPath = artifacts.PrimaryPayloadPath,
                QuarantinePath = quarantinePath,
                PayloadBytes = payloadBytes.Length,
            };
        }
    }

    private static string BuildCorruptionMessage(ProfileReadAttempt primaryAttempt, ProfileReadAttempt backupAttempt)
    {
        var primaryReason = primaryAttempt.Status == ProfileReadStatus.Missing
            ? "missing"
            : string.IsNullOrWhiteSpace(primaryAttempt.Message)
                ? "invalid"
                : primaryAttempt.Message;
        var backupReason = backupAttempt.Status == ProfileReadStatus.Missing
            ? "missing"
            : string.IsNullOrWhiteSpace(backupAttempt.Message)
                ? "invalid"
                : backupAttempt.Message;
        return $"Save recovery failed. primary={primaryReason}; backup={backupReason}";
    }

    private void PrepareBackupArtifacts(SaveArtifacts artifacts)
    {
        if (File.Exists(artifacts.PrimaryPayloadPath) && File.Exists(artifacts.PrimaryManifestPath))
        {
            File.Copy(artifacts.PrimaryPayloadPath, artifacts.BackupPayloadPath, true);
            File.Copy(artifacts.PrimaryManifestPath, artifacts.BackupManifestPath, true);
            return;
        }

        File.Copy(artifacts.TempPayloadPath, artifacts.BackupPayloadPath, true);
        File.Copy(artifacts.TempManifestPath, artifacts.BackupManifestPath, true);
    }

    private void CommitPrimaryArtifacts(SaveArtifacts artifacts)
    {
        ReplaceOrMove(artifacts.TempPayloadPath, artifacts.PrimaryPayloadPath);
        ReplaceOrMove(artifacts.TempManifestPath, artifacts.PrimaryManifestPath);
    }

    private static void ReplaceOrMove(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(sourcePath, destinationPath, null, true);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }

    private void PromoteBackupToPrimary(SaveArtifacts artifacts)
    {
        if (File.Exists(artifacts.BackupPayloadPath))
        {
            File.Copy(artifacts.BackupPayloadPath, artifacts.PrimaryPayloadPath, true);
        }

        if (File.Exists(artifacts.BackupManifestPath))
        {
            File.Copy(artifacts.BackupManifestPath, artifacts.PrimaryManifestPath, true);
        }
    }

    private void CleanupStaleTemporaryArtifacts(SaveArtifacts artifacts)
    {
        DeleteIfExists(artifacts.TempPayloadPath);
        DeleteIfExists(artifacts.TempManifestPath);
    }

    private string QuarantineArtifacts(SaveArtifacts artifacts, ProfileReadAttempt primaryAttempt, ProfileReadAttempt backupAttempt)
    {
        var reason = string.Join(
            "__",
            new[]
            {
                SanitizeReason(primaryAttempt.Message),
                SanitizeReason(backupAttempt.Message),
                artifacts.HasAnyTemporaryArtifacts ? "temp_artifacts_present" : string.Empty,
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var quarantinePath = CreateQuarantineDirectory(artifacts.ProfileId, reason);
        MoveIfExists(artifacts.PrimaryPayloadPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.PrimaryPayloadPath)));
        MoveIfExists(artifacts.PrimaryManifestPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.PrimaryManifestPath)));
        MoveIfExists(artifacts.BackupPayloadPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.BackupPayloadPath)));
        MoveIfExists(artifacts.BackupManifestPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.BackupManifestPath)));
        MoveIfExists(artifacts.TempPayloadPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.TempPayloadPath)));
        MoveIfExists(artifacts.TempManifestPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.TempManifestPath)));
        return quarantinePath;
    }

    private string QuarantineInvalidPrimaryArtifacts(SaveArtifacts artifacts, ProfileReadAttempt primaryAttempt)
    {
        if (primaryAttempt.Status == ProfileReadStatus.Missing && !artifacts.HasAnyTemporaryArtifacts)
        {
            return string.Empty;
        }

        var quarantinePath = CreateQuarantineDirectory(artifacts.ProfileId, SanitizeReason(primaryAttempt.Message));
        MoveIfExists(artifacts.PrimaryPayloadPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.PrimaryPayloadPath)));
        MoveIfExists(artifacts.PrimaryManifestPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.PrimaryManifestPath)));
        MoveIfExists(artifacts.TempPayloadPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.TempPayloadPath)));
        MoveIfExists(artifacts.TempManifestPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.TempManifestPath)));
        return quarantinePath;
    }

    private string QuarantinePrimaryArtifacts(SaveArtifacts artifacts, string? reason)
    {
        var quarantinePath = CreateQuarantineDirectory(artifacts.ProfileId, SanitizeReason(reason));
        MoveIfExists(artifacts.PrimaryPayloadPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.PrimaryPayloadPath)));
        MoveIfExists(artifacts.PrimaryManifestPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.PrimaryManifestPath)));
        return quarantinePath;
    }

    private string QuarantineTemporaryArtifacts(SaveArtifacts artifacts, string? reason)
    {
        var quarantinePath = CreateQuarantineDirectory(artifacts.ProfileId, SanitizeReason(reason));
        MoveIfExists(artifacts.TempPayloadPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.TempPayloadPath)));
        MoveIfExists(artifacts.TempManifestPath, Path.Combine(quarantinePath, Path.GetFileName(artifacts.TempManifestPath)));
        return quarantinePath;
    }

    private string CreateQuarantineDirectory(string profileId, string? reason)
    {
        Directory.CreateDirectory(_quarantineDirectory);
        var suffix = string.IsNullOrWhiteSpace(reason) ? "invalid_save" : reason;
        var directory = Path.Combine(
            _quarantineDirectory,
            $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{profileId}_{suffix}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private ProfileReadAttempt TryReadProfileArtifacts(string payloadPath, string manifestPath, string expectedProfileId)
    {
        var hasPayload = File.Exists(payloadPath);
        var hasManifest = File.Exists(manifestPath);

        if (!hasPayload && !hasManifest)
        {
            return ProfileReadAttempt.Missing();
        }

        if (!hasPayload || !hasManifest)
        {
            return ProfileReadAttempt.Invalid("payload_manifest_pair_missing");
        }

        try
        {
            var manifestJson = File.ReadAllText(manifestPath, Encoding.UTF8);
            var manifest = JsonConvert.DeserializeObject<SaveManifestRecord>(manifestJson);
            if (manifest == null)
            {
                return ProfileReadAttempt.Invalid("manifest_deserialize_failed");
            }

            if (!string.IsNullOrWhiteSpace(manifest.ProfileId)
                && !string.Equals(manifest.ProfileId, expectedProfileId, StringComparison.Ordinal))
            {
                return ProfileReadAttempt.Invalid("manifest_profile_id_mismatch");
            }

            var payloadBytes = File.ReadAllBytes(payloadPath);
            if (!TryResolveVerifiedPayloadBytes(payloadBytes, manifest, out var verifiedPayloadBytes, out var verificationFailure))
            {
                return ProfileReadAttempt.Invalid(verificationFailure);
            }

            var profile = JsonConvert.DeserializeObject<SaveProfile>(Encoding.UTF8.GetString(verifiedPayloadBytes));
            if (profile == null)
            {
                return ProfileReadAttempt.Invalid("profile_deserialize_failed");
            }

            if (string.IsNullOrWhiteSpace(profile.ProfileId))
            {
                profile.ProfileId = expectedProfileId;
            }

            if (!string.Equals(profile.ProfileId, expectedProfileId, StringComparison.Ordinal))
            {
                return ProfileReadAttempt.Invalid("profile_id_mismatch");
            }

            return ProfileReadAttempt.Valid(profile, manifest, verifiedPayloadBytes.Length);
        }
        catch (Exception ex)
        {
            return ProfileReadAttempt.Invalid(ex.Message);
        }
    }

    private static bool TryResolveVerifiedPayloadBytes(
        byte[] payloadBytes,
        SaveManifestRecord manifest,
        out byte[] verifiedPayloadBytes,
        out string failureReason)
    {
        verifiedPayloadBytes = payloadBytes;
        failureReason = string.Empty;

        if (MatchesManifest(payloadBytes, manifest))
        {
            return true;
        }

        if (TryTrimUtf8Bom(payloadBytes, out var bomlessPayloadBytes)
            && MatchesManifest(bomlessPayloadBytes, manifest))
        {
            verifiedPayloadBytes = bomlessPayloadBytes;
            return true;
        }

        failureReason = manifest.PayloadBytes > 0 && manifest.PayloadBytes != payloadBytes.Length
            ? "payload_size_mismatch"
            : "payload_hash_mismatch";
        return false;
    }

    private static bool MatchesManifest(byte[] payloadBytes, SaveManifestRecord manifest)
    {
        if (manifest.PayloadBytes > 0 && manifest.PayloadBytes != payloadBytes.Length)
        {
            return false;
        }

        var hash = ComputeHash(payloadBytes);
        return string.Equals(hash, manifest.PayloadHash, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryTrimUtf8Bom(byte[] payloadBytes, out byte[] trimmedBytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        if (payloadBytes.Length >= preamble.Length
            && preamble.Length > 0
            && payloadBytes.Take(preamble.Length).SequenceEqual(preamble))
        {
            trimmedBytes = payloadBytes.Skip(preamble.Length).ToArray();
            return true;
        }

        trimmedBytes = Array.Empty<byte>();
        return false;
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

    private static void MoveIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        DeleteIfExists(destinationPath);
        File.Move(sourcePath, destinationPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string SanitizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return string.Empty;
        }

        var sanitized = new string(reason
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray());
        return sanitized.Length > 48 ? sanitized.Substring(0, 48) : sanitized;
    }

    private SaveArtifacts GetArtifacts(string profileId)
    {
        var payloadName = $"{profileId}.json";
        return new SaveArtifacts(
            profileId,
            Path.Combine(_rootDirectory, payloadName),
            Path.Combine(_rootDirectory, $"{profileId}.manifest.json"),
            Path.Combine(_rootDirectory, $"{profileId}.bak.json"),
            Path.Combine(_rootDirectory, $"{profileId}.bak.manifest.json"),
            Path.Combine(_rootDirectory, $"{profileId}.tmp.json"),
            Path.Combine(_rootDirectory, $"{profileId}.tmp.manifest.json"));
    }

    private enum ProfileReadStatus
    {
        Missing = 0,
        Valid = 1,
        Invalid = 2,
    }

    private sealed class ProfileReadAttempt
    {
        public ProfileReadStatus Status { get; private init; }
        public SaveProfile? Profile { get; private init; }
        public SaveManifestRecord? Manifest { get; private init; }
        public string Message { get; private init; } = string.Empty;
        public int PayloadBytes { get; private init; }

        public static ProfileReadAttempt Missing() => new() { Status = ProfileReadStatus.Missing };

        public static ProfileReadAttempt Valid(SaveProfile profile, SaveManifestRecord manifest, int payloadBytes) => new()
        {
            Status = ProfileReadStatus.Valid,
            Profile = profile,
            Manifest = manifest,
            PayloadBytes = payloadBytes,
        };

        public static ProfileReadAttempt Invalid(string? message) => new()
        {
            Status = ProfileReadStatus.Invalid,
            Message = message ?? string.Empty,
        };
    }

    private sealed class SaveArtifacts
    {
        public SaveArtifacts(
            string profileId,
            string primaryPayloadPath,
            string primaryManifestPath,
            string backupPayloadPath,
            string backupManifestPath,
            string tempPayloadPath,
            string tempManifestPath)
        {
            ProfileId = profileId;
            PrimaryPayloadPath = primaryPayloadPath;
            PrimaryManifestPath = primaryManifestPath;
            BackupPayloadPath = backupPayloadPath;
            BackupManifestPath = backupManifestPath;
            TempPayloadPath = tempPayloadPath;
            TempManifestPath = tempManifestPath;
        }

        public string ProfileId { get; }
        public string PrimaryPayloadPath { get; }
        public string PrimaryManifestPath { get; }
        public string BackupPayloadPath { get; }
        public string BackupManifestPath { get; }
        public string TempPayloadPath { get; }
        public string TempManifestPath { get; }
        public bool HasAnyTemporaryArtifacts => File.Exists(TempPayloadPath) || File.Exists(TempManifestPath);
    }
}
