using System;
using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Content.Definitions;

[Serializable]
public sealed class MobilityDefinition
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public BudgetCard BudgetCard = new() { Domain = BudgetDomain.Mobility };
    public ActivationModel ActivationModel = ActivationModel.Trigger;
    public ActionLane Lane = ActionLane.Reaction;
    public ActionLockRule LockRule = ActionLockRule.HardCommit;
    public TargetRule TargetRule = new();
    public MobilityProfileDefinition Profile;
    public List<EffectDescriptor> Effects = new();
}
