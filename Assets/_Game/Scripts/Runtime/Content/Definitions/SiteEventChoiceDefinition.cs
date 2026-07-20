using System;
using System.Collections.Generic;

namespace SM.Content.Definitions;

[Serializable]
public sealed class SiteEventChoiceDefinition
{
    public string Id = string.Empty;
    public string LabelKey = string.Empty;
    public List<SiteEventOutcomeDefinition> Outcomes = new();
}
