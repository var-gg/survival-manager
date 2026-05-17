using UnityEngine;

namespace SM.Content.Definitions
{
    [CreateAssetMenu(menuName = "SM/Definitions/Extra Actor Character Definition", fileName = "extra_actor_")]
    public sealed class ExtraActorCharacterDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public ExtraActorExposureTierValue ExposureTier = ExtraActorExposureTierValue.ExtraActor;
        public string ChapterId = string.Empty;
        public string SiteId = string.Empty;
        public ExtraActorSpawnPolicyValue FirstClearSpawnPolicy = ExtraActorSpawnPolicyValue.SiteLocalPool;
        public string StorySafety = string.Empty;
        public string FactionId = string.Empty;
        public string CombatArchetypeId = string.Empty;
        public string P09BasePresetId = string.Empty;
        public string ModelArchetype = string.Empty;
        public ExtraActorIllustrationTierValue IllustrationTier = ExtraActorIllustrationTierValue.ExtraCard;
        public string BarkSetId = string.Empty;
        public string DossierHook = string.Empty;
        public bool GachaEligible = true;
    }
}
