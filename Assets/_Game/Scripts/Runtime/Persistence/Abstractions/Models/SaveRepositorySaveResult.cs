namespace SM.Persistence.Abstractions.Models;

public enum SaveRepositorySaveStatus
{
    Success = 0,
    Failed = 1,
}

public sealed class SaveRepositorySaveResult
{
    public SaveRepositorySaveStatus Status { get; init; } = SaveRepositorySaveStatus.Failed;
    public SaveManifestRecord? Manifest { get; init; }
    public string Message { get; init; } = string.Empty;
    public string RecoveryPath { get; init; } = string.Empty;
    public string QuarantinePath { get; init; } = string.Empty;
    public int PayloadBytes { get; init; }

    public bool IsSuccessful => Status == SaveRepositorySaveStatus.Success;
}
