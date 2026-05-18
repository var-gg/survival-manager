using System;

namespace SM.Combat.Model;

public enum SkillPresentationFamily
{
    Any = 0,
    Melee = 1,
    Projectile = 2,
    Ranged = 3,
    Nova = 4,
    Zone = 5,
    Trap = 6,
    Aura = 7,
    Heal = 8,
    Shield = 9,
    Debuff = 10,
    Reposition = 11,
    PassiveProc = 12,
}

public enum SkillPresentationSkin
{
    Any = 0,
    Physical = 1,
    Arcane = 2,
    Fire = 3,
    Lightning = 4,
    FrostGlass = 5,
    ShadowSoul = 6,
    HealGold = 7,
    GuardSteel = 8,
    Blood = 9,
    EchoArcane = 10,
}

public enum SkillPresentationGesture
{
    None = 0,
    Melee = 1,
    HeavyMelee = 2,
    BowShot = 3,
    ProjectileCast = 4,
    SpellOmni = 5,
    GuardPose = 6,
    Reposition = 7,
    Throw = 8,
    Passive = 9,
}

public enum SkillPresentationCueSequence
{
    None = 0,
    StrikeImpact = 1,
    ProjectileImpact = 2,
    AuraPulse = 3,
    NovaPulse = 4,
    ZoneLinger = 5,
    TrapDeploy = 6,
    HealPulse = 7,
    ShieldGuard = 8,
    DebuffApply = 9,
    RepositionTrail = 10,
    PassiveProc = 11,
}

public sealed record BattleSkillPresentationProfile(
    SkillPresentationFamily Family,
    SkillPresentationSkin Skin,
    SkillPresentationGesture Gesture,
    SkillPresentationCueSequence CueSequence)
{
    public static readonly BattleSkillPresentationProfile Default = new(
        SkillPresentationFamily.Melee,
        SkillPresentationSkin.Physical,
        SkillPresentationGesture.Melee,
        SkillPresentationCueSequence.StrikeImpact);

    public bool IsResolved
        => Family != SkillPresentationFamily.Any
           && Skin != SkillPresentationSkin.Any
           && Gesture != SkillPresentationGesture.None
           && CueSequence != SkillPresentationCueSequence.None;
}

public static class SkillPresentationProfileResolver
{
    public static BattleSkillPresentationProfile Resolve(
        string skillId,
        SkillKind kind,
        DamageType damageType,
        SkillDelivery delivery,
        string slotKind,
        int appliedStatusCount = 0,
        string cleanseProfileId = "",
        BattleAreaEffectFamily areaEffectFamily = BattleAreaEffectFamily.SingleTarget)
    {
        var normalizedSlot = CompiledSkillSlots.Normalize(slotKind);
        var hasStatusPayload = appliedStatusCount > 0 || !string.IsNullOrWhiteSpace(cleanseProfileId);
        var family = ResolveFamily(skillId, kind, damageType, delivery, normalizedSlot, hasStatusPayload, areaEffectFamily);
        var skin = ResolveSkin(skillId, family, damageType);
        var gesture = ResolveGesture(skillId, family, damageType, delivery);
        var sequence = ResolveCueSequence(family);
        return new BattleSkillPresentationProfile(family, skin, gesture, sequence);
    }

    private static SkillPresentationFamily ResolveFamily(
        string skillId,
        SkillKind kind,
        DamageType damageType,
        SkillDelivery delivery,
        string slotKind,
        bool hasStatusPayload,
        BattleAreaEffectFamily areaEffectFamily)
    {
        if (damageType == DamageType.Healing || kind == SkillKind.Heal)
        {
            return SkillPresentationFamily.Heal;
        }

        if (kind == SkillKind.Shield)
        {
            return SkillPresentationFamily.Shield;
        }

        if (ContainsAny(skillId, "step", "dash", "blink", "roll", "reposition"))
        {
            return SkillPresentationFamily.Reposition;
        }

        if (delivery == SkillDelivery.Trap)
        {
            return SkillPresentationFamily.Trap;
        }

        if (delivery == SkillDelivery.Zone || areaEffectFamily != BattleAreaEffectFamily.SingleTarget)
        {
            return SkillPresentationFamily.Zone;
        }

        if (delivery == SkillDelivery.Nova)
        {
            return SkillPresentationFamily.Nova;
        }

        if (kind == SkillKind.Debuff || hasStatusPayload)
        {
            return SkillPresentationFamily.Debuff;
        }

        if (slotKind is CompiledSkillSlots.Passive or CompiledSkillSlots.Support)
        {
            return SkillPresentationFamily.PassiveProc;
        }

        if (kind == SkillKind.Buff || delivery == SkillDelivery.Aura)
        {
            return SkillPresentationFamily.Aura;
        }

        return delivery switch
        {
            SkillDelivery.Projectile => SkillPresentationFamily.Projectile,
            SkillDelivery.Ranged => SkillPresentationFamily.Ranged,
            _ => SkillPresentationFamily.Melee,
        };
    }

    private static SkillPresentationSkin ResolveSkin(
        string skillId,
        SkillPresentationFamily family,
        DamageType damageType)
    {
        if (family == SkillPresentationFamily.Heal)
        {
            return SkillPresentationSkin.HealGold;
        }

        if (family == SkillPresentationFamily.Shield || ContainsAny(skillId, "aegis", "guardian", "sentinel", "bulwark"))
        {
            return SkillPresentationSkin.GuardSteel;
        }

        if (ContainsAny(skillId, "ember", "cinder", "ash", "flame", "fire"))
        {
            return SkillPresentationSkin.Fire;
        }

        if (ContainsAny(skillId, "storm", "spark", "volt", "lightning"))
        {
            return SkillPresentationSkin.Lightning;
        }

        if (ContainsAny(skillId, "fracture", "prism", "glass", "shard", "lattice"))
        {
            return SkillPresentationSkin.FrostGlass;
        }

        if (ContainsAny(skillId, "echo", "resonance", "memory", "rune"))
        {
            return SkillPresentationSkin.EchoArcane;
        }

        if (ContainsAny(skillId, "shadow", "soul", "hex", "curse", "void"))
        {
            return SkillPresentationSkin.ShadowSoul;
        }

        if (ContainsAny(skillId, "blood", "iron", "pelt", "maul"))
        {
            return SkillPresentationSkin.Blood;
        }

        return damageType == DamageType.Magical
            ? SkillPresentationSkin.Arcane
            : SkillPresentationSkin.Physical;
    }

    private static SkillPresentationGesture ResolveGesture(
        string skillId,
        SkillPresentationFamily family,
        DamageType damageType,
        SkillDelivery delivery)
    {
        return family switch
        {
            SkillPresentationFamily.Melee => ContainsAny(skillId, "linebreaker", "maul", "overrun", "bulwark")
                ? SkillPresentationGesture.HeavyMelee
                : SkillPresentationGesture.Melee,
            SkillPresentationFamily.Projectile or SkillPresentationFamily.Ranged => damageType == DamageType.Physical
                ? SkillPresentationGesture.BowShot
                : SkillPresentationGesture.ProjectileCast,
            SkillPresentationFamily.Trap => SkillPresentationGesture.Throw,
            SkillPresentationFamily.Shield => SkillPresentationGesture.GuardPose,
            SkillPresentationFamily.Reposition => SkillPresentationGesture.Reposition,
            SkillPresentationFamily.PassiveProc => SkillPresentationGesture.Passive,
            SkillPresentationFamily.Debuff => delivery is SkillDelivery.Projectile or SkillDelivery.Ranged
                ? SkillPresentationGesture.ProjectileCast
                : SkillPresentationGesture.SpellOmni,
            _ => SkillPresentationGesture.SpellOmni,
        };
    }

    private static SkillPresentationCueSequence ResolveCueSequence(SkillPresentationFamily family)
    {
        return family switch
        {
            SkillPresentationFamily.Projectile or SkillPresentationFamily.Ranged => SkillPresentationCueSequence.ProjectileImpact,
            SkillPresentationFamily.Aura => SkillPresentationCueSequence.AuraPulse,
            SkillPresentationFamily.Nova => SkillPresentationCueSequence.NovaPulse,
            SkillPresentationFamily.Zone => SkillPresentationCueSequence.ZoneLinger,
            SkillPresentationFamily.Trap => SkillPresentationCueSequence.TrapDeploy,
            SkillPresentationFamily.Heal => SkillPresentationCueSequence.HealPulse,
            SkillPresentationFamily.Shield => SkillPresentationCueSequence.ShieldGuard,
            SkillPresentationFamily.Debuff => SkillPresentationCueSequence.DebuffApply,
            SkillPresentationFamily.Reposition => SkillPresentationCueSequence.RepositionTrail,
            SkillPresentationFamily.PassiveProc => SkillPresentationCueSequence.PassiveProc,
            _ => SkillPresentationCueSequence.StrikeImpact,
        };
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (var i = 0; i < tokens.Length; i++)
        {
            if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
