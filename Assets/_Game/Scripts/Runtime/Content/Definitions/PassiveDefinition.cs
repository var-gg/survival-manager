using System;
using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Content.Definitions;

[Serializable]
public sealed class PassiveDefinition
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public ActivationModel ActivationModel = ActivationModel.Passive;
    public ActionLane Lane = ActionLane.Primary;
    public ActionLockRule LockRule = ActionLockRule.None;
    public List<EffectDescriptor> Effects = new();
    public bool AllowMirroredOwnedSummonKill = false;
}
