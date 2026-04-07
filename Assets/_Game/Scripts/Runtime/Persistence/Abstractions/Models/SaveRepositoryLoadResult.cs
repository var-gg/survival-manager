namespace SM.Persistence.Abstractions.Models;

public enum SaveRepositoryLoadStatus
{
    MissingCreated = 0,
    LoadedPrimary = 1,
    LoadedBackupRecovered = 2,
    FailedCorrupt = 3,
}

public sealed class SaveRepositoryLoadResult
{
    public SaveRepositoryLoadStatus Status { get; init; } = SaveRepositoryLoadStatus.FailedCorrupt;
    public SaveProfile? Profile { get; init; }
    public SaveManifestRecord? Manifest { get; init; }
    public string Message { get; init; } = string.Empty;
    public string RecoveryPath { get; init; } = string.Empty;
    public string QuarantinePath { get; init; } = string.Empty;
    public bool UsedBackup { get; init; }
    public bool CreatedNewProfile { get; init; }
    public bool ManifestVerified { get; init; }
    public int PayloadBytes { get; init; }

    public bool IsSuccessful =>
        Status is SaveRepositoryLoadStatus.MissingCreated
            or SaveRepositoryLoadStatus.LoadedPrimary
            or SaveRepositoryLoadStatus.LoadedBackupRecovered;
}
