using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class ArenaDefenseSnapshotRecord
{
    public string SnapshotId = string.Empty;
    public string BlueprintId = string.Empty;
    public string SnapshotHash = string.Empty;
    public string CompileVersion = string.Empty;
    public string CompileHash = string.Empty;
    public string ContentVersion = string.Empty;
    public int Rating;
    public string CreatedAtUtc = string.Empty;
}
