using SM.Core;
using UnityEngine;

namespace SM.Content
{

    [CreateAssetMenu(menuName = "SM/Definitions/Narrative/Story Condition", fileName = "story_condition_")]
    public sealed class StoryConditionDefinition : ScriptableObject
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private StoryConditionKind _kind;
        [SerializeField] private string _operandA = string.Empty;
        [SerializeField] private string _operandB = string.Empty;

        public string Id => _id;
        public StoryConditionKind Kind => _kind;
        public string OperandA => _operandA;
        public string OperandB => _operandB;
    }
}
