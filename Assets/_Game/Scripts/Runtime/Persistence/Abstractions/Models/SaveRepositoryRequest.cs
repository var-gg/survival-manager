namespace SM.Persistence.Abstractions.Models;

public sealed class SaveRepositoryRequest
{
    public string CheckpointKind { get; init; } = string.Empty;
    public string CompileHash { get; init; } = string.Empty;
}
