using System;
using SM.Core.Content;
using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Content.Definitions;

[Serializable]
public sealed class PassiveDefinition
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public string EffectFamilyId = string.Empty;
    public string MutuallyExclusiveGroupId = string.Empty;
    public BudgetCard BudgetCard = new() { Domain = BudgetDomain.Passive };
    public List<StableTagDefinition> RecruitNativeTags = new();
    public List<StableTagDefinition> RecruitPlanTags = new();
    public List<StableTagDefinition> RecruitScoutTags = new();
    public ActivationModel ActivationModel = ActivationModel.Passive;
    public ActionLane Lane = ActionLane.Primary;
    public ActionLockRule LockRule = ActionLockRule.None;
    public List<EffectDescriptor> Effects = new();
    public bool AllowMirroredOwnedSummonKill = false;
}
