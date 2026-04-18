using System;
using UnityEngine;

namespace SM.Content.Definitions
{

    [Serializable]
    public sealed class UnitLoadoutDefinition
    {
        public BasicAttackDefinition BasicAttack = new();
        public SkillDefinitionAsset SignatureActive;
        public SkillDefinitionAsset FlexActive;
        public PassiveDefinition SignaturePassive = new();
        public PassiveDefinition FlexPassive = new();
        public MobilityDefinition MobilityReaction = new();

        public bool IsComplete()
        {
            return SignatureActive != null && FlexActive != null;
        }
    }
}
