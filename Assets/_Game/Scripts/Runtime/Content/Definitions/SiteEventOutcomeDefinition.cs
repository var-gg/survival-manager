using System;
using SM.Core.Content;

namespace SM.Content.Definitions;

[Serializable]
public sealed class SiteEventOutcomeDefinition
{
    public OutcomeKind Kind;
    public string PayloadId = string.Empty;
    public string AuxiliaryId = string.Empty;
    public int Amount;
    public OutcomeTargetRule TargetRule;
}
