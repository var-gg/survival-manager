using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class SkillInstanceRecord
{
    public string SkillInstanceId = string.Empty;
    public string SkillId = string.Empty;
    public string SlotKind = "active_core";
    public List<string> CompileTags = new();
}
