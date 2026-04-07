using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class SaveManifestRecord
{
    public string ProfileId = string.Empty;
    public string SavedAtUtc = string.Empty;
    public string CheckpointKind = string.Empty;
    public string CompileHash = string.Empty;
    public string PayloadHash = string.Empty;
    public int PayloadBytes = 0;
}
