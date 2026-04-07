namespace SM.Unity;

public enum SessionCheckpointStatus
{
    Success = 0,
    RecoveredFromBackup = 1,
    Blocked = 2,
    Failed = 3,
}

public sealed class SessionCheckpointResult
{
    public SessionCheckpointKind Kind { get; init; } = SessionCheckpointKind.ManualSave;
    public SessionCheckpointStatus Status { get; init; } = SessionCheckpointStatus.Success;
    public string Message { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public string RecoveryPath { get; init; } = string.Empty;
    public string QuarantinePath { get; init; } = string.Empty;
    public bool UsedBackup { get; init; }
    public bool CreatedNewProfile { get; init; }
    public bool ManifestVerified { get; init; }
    public int PayloadBytes { get; init; }

    public bool IsSuccessful =>
        Status is SessionCheckpointStatus.Success or SessionCheckpointStatus.RecoveredFromBackup;

    public static SessionCheckpointResult Success(
        SessionCheckpointKind kind,
        string profileId,
        string message = "",
        string recoveryPath = "",
        bool createdNewProfile = false,
        bool manifestVerified = false,
        int payloadBytes = 0)
    {
        return new SessionCheckpointResult
        {
            Kind = kind,
            Status = SessionCheckpointStatus.Success,
            Message = message,
            ProfileId = profileId,
            RecoveryPath = recoveryPath,
            CreatedNewProfile = createdNewProfile,
            ManifestVerified = manifestVerified,
            PayloadBytes = payloadBytes,
        };
    }

    public static SessionCheckpointResult Recovered(
        SessionCheckpointKind kind,
        string profileId,
        string message,
        string recoveryPath,
        string quarantinePath = "",
        int payloadBytes = 0)
    {
        return new SessionCheckpointResult
        {
            Kind = kind,
            Status = SessionCheckpointStatus.RecoveredFromBackup,
            Message = message,
            ProfileId = profileId,
            RecoveryPath = recoveryPath,
            QuarantinePath = quarantinePath,
            UsedBackup = true,
            ManifestVerified = true,
            PayloadBytes = payloadBytes,
        };
    }

    public static SessionCheckpointResult Blocked(
        SessionCheckpointKind kind,
        string profileId,
        string message)
    {
        return new SessionCheckpointResult
        {
            Kind = kind,
            Status = SessionCheckpointStatus.Blocked,
            Message = message,
            ProfileId = profileId,
        };
    }

    public static SessionCheckpointResult Failed(
        SessionCheckpointKind kind,
        string profileId,
        string message,
        string recoveryPath = "",
        string quarantinePath = "")
    {
        return new SessionCheckpointResult
        {
            Kind = kind,
            Status = SessionCheckpointStatus.Failed,
            Message = message,
            ProfileId = profileId,
            RecoveryPath = recoveryPath,
            QuarantinePath = quarantinePath,
        };
    }
}
