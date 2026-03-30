using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class MatchRecordBlob
{
    public string MatchId = string.Empty;
    public string CompileVersion = string.Empty;
    public string CompileHash = string.Empty;
    public string InputDigest = string.Empty;
    public List<string> EventStream = new();
    public List<string> KeyframeDigests = new();
}
