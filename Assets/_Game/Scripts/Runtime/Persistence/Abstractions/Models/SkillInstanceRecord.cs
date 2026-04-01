using System;
using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class SkillInstanceRecord
{
    public string SkillInstanceId = string.Empty;
    public string SkillId = string.Empty;
    public string SlotKind = "core_active";
    public ActionSlotKind? ResolvedSlotKind = null;
    public List<string> CompileTags = new();
}
