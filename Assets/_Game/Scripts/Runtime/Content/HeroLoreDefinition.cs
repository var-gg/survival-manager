using SM.Core;
using UnityEngine;

namespace SM.Content
{

    [CreateAssetMenu(menuName = "SM/Definitions/Narrative/Hero Lore", fileName = "hero_lore_")]
    public sealed class HeroLoreDefinition : ScriptableObject
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private string _heroId = string.Empty;
        [SerializeField] private NarrativeTier _tier = NarrativeTier.Background;
        [SerializeField] private int _beatBudget;
        [SerializeField] private string _canonBio = string.Empty;
        [SerializeField] private string _unresolvedHook = string.Empty;

        public string Id => _id;
        public string HeroId => _heroId;
        public NarrativeTier Tier => _tier;
        public int BeatBudget => _beatBudget;
        public string CanonBio => _canonBio;
        public string UnresolvedHook => _unresolvedHook;
    }
}
