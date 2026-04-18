using System;
using SM.Core.Content;
using System.Collections.Generic;
using SM.Core.Contracts;
using UnityEngine;

namespace SM.Content.Definitions
{

    [Serializable]
    public sealed class BasicAttackDefinition
    {
        public string Id = "basic_attack";
        public string NameKey = string.Empty;
        public DamageTypeValue DamageType = DamageTypeValue.Physical;
        public ActionLane Lane = ActionLane.Primary;
        public ActionLockRule LockRule = ActionLockRule.SoftCommit;
        public TargetRule TargetRule = new();
        public List<EffectDescriptor> Effects = new()
        {
            new EffectDescriptor
            {
                Layer = AuthorityLayer.UnitKit,
                Scope = EffectScope.CurrentTarget,
                Capabilities = EffectCapability.DealDamage,
            }
        };
    }
}
