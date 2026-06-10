using SM.Combat.Model;

namespace SM.Unity;

/// <summary>기본공격 presentation 분류. 활(BowShot)·투사체(ProjectileCast)·근접 연출을 고르는 단일 기준.</summary>
public enum BattleBasicAttackPresentationKind
{
    Melee = 0,
    Bow = 1,
    Projectile = 2,
}

/// <summary>
/// 기본공격 presentation 분류기 — 권위 actor 속성(class/archetype/preferred range)만 보고
/// 활/투사체/근접 연출을 결정한다 (GPT Pro J8: event Note 문자열 추론 금지). 이 분류는
/// commit 모션 semantic, 무기 손 거치(BattleP09WeaponStanceRig), ready idle이 함께 쓰는
/// presentation 전용 truth이며 sim의 ranged 판정(BasicAttackActionProfileResolver)과는 별개다.
/// 기존에 BattlePresentationCueBuilder 내부에 사유화돼 있던 판정을 옮겨 단일화했다.
/// </summary>
public static class BattleBasicAttackPresentationClassifier
{
    public static BattleBasicAttackPresentationKind Resolve(BattleUnitReadModel actor)
    {
        if (IsBowBasicAttacker(actor))
        {
            return BattleBasicAttackPresentationKind.Bow;
        }

        if (IsProjectileBasicAttacker(actor))
        {
            return BattleBasicAttackPresentationKind.Projectile;
        }

        return BattleBasicAttackPresentationKind.Melee;
    }

    public static bool IsBowBasicAttacker(BattleUnitReadModel actor)
    {
        return IsTag(actor.ClassId, "ranger")
               || IsTag(actor.ArchetypeId, "hunter")
               || IsTag(actor.ArchetypeId, "scout")
               || IsTag(actor.ArchetypeId, "marksman")
               || IsTag(actor.ArchetypeId, "rift_stalker");
    }

    public static bool IsProjectileBasicAttacker(BattleUnitReadModel actor)
    {
        return actor.PreferredRangeMin >= 1.8f
               || actor.PreferredRangeMax >= 2.4f;
    }

    private static bool IsTag(string value, string expected)
    {
        return string.Equals(value, expected, System.StringComparison.Ordinal);
    }
}
