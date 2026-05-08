using System;
using System.Collections.Generic;

namespace SM.Content.Definitions
{

    public enum AffixFamilyValue
    {
        LegacyDerived = 0,
        CoreScalar = 1,
        ConditionalTagged = 2,
        BuildShaping = 3,
    }

    public enum AffixEffectTypeValue
    {
        LegacyDerived = 0,
        StatModifier = 1,
        RuleModifier = 2,
        ConditionalTagged = 3,
        BuildShaping = 4,
        Proc = 5,
    }

    public enum SkillTemplateTypeValue
    {
        LegacyDerived = 0,
        SingleTargetStrike = 1,
        SweepCleave = 2,
        DashStrikeLunge = 3,
        ProjectileShot = 4,
        MultiShotVolley = 5,
        BeamRay = 6,
        ConeCast = 7,
        GroundArea = 8,
        NovaPulse = 9,
        SelfBuffStance = 10,
        AllyBuffAuraPulse = 11,
        ShieldBarrierHeal = 12,
        SummonDeployable = 13,
        BlinkRepositionUtility = 14,
        TriggerPassive = 15,
    }

    public enum SkillAiIntentValue
    {
        Burst = 0,
        Engage = 1,
        Evade = 2,
        MaintainRange = 3,
        Execute = 4,
        Protect = 5,
        Control = 6,
        Sustain = 7,
    }

    public enum SkillLearnSourceValue
    {
        LegacyDerived = 0,
        LockedCore = 1,
        RecruitFlex = 2,
        RetrainFlex = 3,
        ItemGranted = 4,
        AugmentGranted = 5,
        EnemyOnly = 6,
    }

    [Serializable]
    public sealed class SkillAiScoreHints
    {
        public float BurstBias = 0f;
        public float ProtectBias = 0f;
        public float MaintainRangeBias = 0f;
        public float ExecuteBias = 0f;
        public float ControlBias = 0f;
        public float MinimumTargetHealthRatio = 0f;
        public float MaximumTargetHealthRatio = 1f;
        public float MinimumDistance = 0f;
        public float MaximumDistance = 0f;
    }

    public enum AugmentOfferBucketValue
    {
        LegacyDerived = 0,
        HeroRewrite = 1,
        TacticalRewrite = 2,
        ScalingEngine = 3,
        EconomyAndLoot = 4,
        SynergyPact = 5,
    }

    public enum AugmentRiskRewardClassValue
    {
        LegacyDerived = 0,
        Neutral = 1,
        Rescue = 2,
        RiskReward = 3,
        Alignment = 4,
    }

    public enum StatusStackPolicyValue
    {
        LegacyDerived = 0,
        SingleInstance = 1,
        AddStack = 2,
        SeparateInstance = 3,
        ReplaceHighest = 4,
    }

    public enum StatusRefreshPolicyValue
    {
        LegacyDerived = 0,
        RefreshDuration = 1,
        ExtendDuration = 2,
        KeepExisting = 3,
        ReplaceDuration = 4,
    }

    public enum StatusProcAttributionPolicyValue
    {
        LegacyDerived = 0,
        None = 1,
        OnAttack = 2,
        OnHit = 3,
        OnDamageTick = 4,
        OnStatusApply = 5,
    }

    public enum StatusOwnershipPolicyValue
    {
        LegacyDerived = 0,
        SourceOwns = 1,
        SummonOwnerOwns = 2,
        SecondaryHitOwnerOwns = 3,
        TargetOwns = 4,
    }

    [Serializable]
    public sealed class StringIdPool
    {
        public List<string> Entries = new();
    }
}
