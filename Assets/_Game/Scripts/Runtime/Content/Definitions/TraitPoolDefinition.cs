using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Trait Pool Definition", fileName = "traitpool_")]
    public sealed class TraitPoolDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string ArchetypeId = string.Empty;
        public List<TraitEntry> PositiveTraits = new();
        public List<TraitEntry> NegativeTraits = new();

        [Tooltip("적 encounter 수치 노브 전용 trait — 아군 영입/데모 추첨 풀(Positive/Negative)과 분리된 컨테이너. 추첨 Normalize가 index % 풀크기라 노브를 NegativeTraits에 섞으면 아군 trait 배분이 이동·오염된다. EnemySquad member의 NegativeTraitId로만 참조하며, 전투 패키지 조립에는 똑같이 합류한다.")]
        public List<TraitEntry> EncounterTraits = new();
    }
}
