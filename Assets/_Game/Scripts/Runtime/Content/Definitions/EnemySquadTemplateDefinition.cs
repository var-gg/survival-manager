using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [System.Serializable]
    public sealed class EnemySquadMemberDefinition
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string ArchetypeId = string.Empty;
        public string CharacterId = string.Empty;
        public DeploymentAnchorValue Anchor = DeploymentAnchorValue.FrontCenter;
        public EnemySquadMemberRoleValue Role = EnemySquadMemberRoleValue.Unit;
        public string PositiveTraitId = string.Empty;
        public string NegativeTraitId = string.Empty;
        public List<string> RuleModifierTags = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
    }

    [CreateAssetMenu(menuName = "SM/Definitions/Enemy Squad Template Definition", fileName = "enemy_squad_")]
    public sealed class EnemySquadTemplateDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public string FactionId = string.Empty;
        public TeamPostureTypeValue EnemyPosture = TeamPostureTypeValue.StandardAdvance;
        public ThreatTierValue ThreatTier = ThreatTierValue.Tier1;
        public int ThreatCost = 1;
        public List<string> RewardDropTags = new();
        public List<EnemySquadMemberDefinition> Members = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
