using System.Collections.Generic;
using SM.Core.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Skill Definition", fileName = "skill_")]
public sealed class SkillDefinitionAsset : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public SkillTemplateTypeValue TemplateType = SkillTemplateTypeValue.LegacyDerived;
    public SkillKindValue Kind = SkillKindValue.Strike;
    public SkillSlotKindValue SlotKind = SkillSlotKindValue.CoreActive;
    public DamageTypeValue DamageType = DamageTypeValue.Physical;
    public SkillDeliveryValue Delivery = SkillDeliveryValue.Melee;
    public SkillTargetRuleValue TargetRule = SkillTargetRuleValue.NearestEnemy;
    public float Power = 0f;
    public float Range = 1f;
    public float RangeMin = 0f;
    public float RangeMax = -1f;
    public float Radius = 0f;
    public float Width = 0f;
    public float ArcDegrees = 0f;
    public float PowerFlat = 0f;
    public float PhysCoeff = 1f;
    public float MagCoeff = 0f;
    public float HealCoeff = 0f;
    public float HealthCoeff = 0f;
    public bool CanCrit;
    public ActivationModel ActivationModel = ActivationModel.Cooldown;
    public ActionLane Lane = ActionLane.Primary;
    public ActionLockRule LockRule = ActionLockRule.HardCommit;
    public AuthorityLayer AuthorityLayer = AuthorityLayer.Skill;
    public float ManaCost = 0f;
    public float ResourceCost = -1f;
    public float BaseCooldownSeconds = 0f;
    public float CooldownSeconds = -1f;
    public float CastWindupSeconds = 0f;
    public float RecoverySeconds = -1f;
    public float PowerBudget = 0f;
    public float InterruptRefundScalar = 0.5f;
    public List<SkillAiIntentValue> AiIntents = new();
    public SkillAiScoreHints AiScoreHints = new();
    public string AnimationHookId = string.Empty;
    public string VfxHookId = string.Empty;
    public string SfxHookId = string.Empty;
    public SkillLearnSourceValue LearnSource = SkillLearnSourceValue.LegacyDerived;
    public TargetRule TargetRuleData = new();
    public SummonProfile SummonProfile;
    public List<EffectDescriptor> Effects = new();
    public List<StableTagDefinition> CompileTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public List<StableTagDefinition> SupportAllowedTags = new();
    public List<StableTagDefinition> SupportBlockedTags = new();
    public List<StableTagDefinition> RequiredWeaponTags = new();
    public List<StableTagDefinition> RequiredClassTags = new();
    public List<StatusApplicationRule> AppliedStatuses = new();
    public string CleanseProfileId = string.Empty;

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
}
