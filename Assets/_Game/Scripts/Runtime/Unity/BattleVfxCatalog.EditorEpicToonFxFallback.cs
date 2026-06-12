#if UNITY_EDITOR
using SM.Combat.Model;
using UnityEditor;
using UnityEngine;

namespace SM.Unity;

public sealed partial class BattleVfxCatalog
{
    private static BattleVfxCatalog? _editorEpicToonFxFallbackCatalog;

    private static BattleVfxCatalog? TryCreateEditorEpicToonFxFallbackCatalog()
    {
        if (_editorEpicToonFxFallbackCatalog != null)
        {
            return _editorEpicToonFxFallbackCatalog;
        }

        var catalog = CreateInstance<BattleVfxCatalog>();
        catalog.hideFlags = HideFlags.DontSave;
        PopulateEpicToonFxBaseline(catalog);

        _editorEpicToonFxFallbackCatalog = catalog.HasAnyEntry() ? catalog : null;
        return _editorEpicToonFxFallbackCatalog;
    }

    /// <summary>
    /// Epic Toon FX baseline 엔트리 테이블 — 에디터 폴백과 프로덕션 카탈로그 asset 빌더
    /// (SM.Editor BattleVfxCatalogBuilder)가 같은 테이블을 공유한다(패리티 보장). 벤더 prefab은
    /// 무수정 참조, 튜닝(scale/lifetime)은 엔트리 값으로만.
    /// </summary>
    public static void PopulateEpicToonFxBaseline(BattleVfxCatalog catalog)
    {
        AddEntry(catalog, BattlePresentationCueType.WindupEnter, "Assets/Epic Toon FX/Prefabs/Combat/Magic/Charge/MagicChargeYellow.prefab", BattleActorSocketId.Telegraph, 1.1f, new Vector3(0f, 0.04f, 0f), Vector3.zero, Vector3.one * 0.58f, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitBasic, "Assets/Epic Toon FX/Prefabs/Combat/Sword/Slash/SwordSlashThick/SwordSlashThickWhite.prefab", BattleActorSocketId.ProjectileOrigin, 1.15f, new Vector3(0f, 0.10f, 0.05f), new Vector3(0f, 0f, 0f), Vector3.one * 0.72f, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitBasic, "Assets/Epic Toon FX/Prefabs/Combat/Missiles/Sharp/SharpMissileGreen.prefab", BattleActorSocketId.ProjectileOrigin, 1.25f, new Vector3(0f, 0.10f, 0.05f), Vector3.zero, Vector3.one * 0.52f, false, BattleAnimationSemantic.BowShot);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitBasic, "Assets/Epic Toon FX/Prefabs/Combat/Missiles/MagicSoft/MagicMissileSoftBlue.prefab", BattleActorSocketId.ProjectileOrigin, 1.35f, new Vector3(0f, 0.08f, 0.05f), Vector3.zero, Vector3.one * 0.52f, false, BattleAnimationSemantic.ProjectileCast);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitSkill, "Assets/Epic Toon FX/Prefabs/Combat/Missiles/MagicSoft/MagicMissileSoftBlue.prefab", BattleActorSocketId.ProjectileOrigin, 1.45f, new Vector3(0f, 0.08f, 0.05f), Vector3.zero, Vector3.one * 0.58f, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitHeal, "Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealOnceBurst.prefab", BattleActorSocketId.Cast, 0.95f, new Vector3(0f, 0.08f, 0f), Vector3.zero, Vector3.one * 0.42f, false);
        // 모션 스윕 확정 결함: 이 폭발 prefab의 흰 돔이 유닛 키 2~3배 + 14프레임 잔존으로 밀집 교전을
        // 백화시키고(가해자 스윙·막타 가림) 사망 순간까지 가려 "증발"처럼 보이게 했다. 유닛 1배 이하로 축소.
        AddEntry(catalog, BattlePresentationCueType.ImpactDamage, "Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/TargetHitExplosion.prefab", BattleActorSocketId.Hit, 0.6f, Vector3.zero, Vector3.zero, Vector3.one * 0.30f, false);
        AddEntry(catalog, BattlePresentationCueType.ImpactHeal, "Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealNova.prefab", BattleActorSocketId.Head, 0.95f, Vector3.zero, Vector3.zero, Vector3.one * 0.34f, false);
        // 가드 쉴드 돔도 같은 백화 결함 공범 — 유닛을 살짝 감싸는 정도로 축소.
        AddEntry(catalog, BattlePresentationCueType.GuardEnter, "Assets/Epic Toon FX/Prefabs/Combat/Shield/ShieldSoftBlue.prefab", BattleActorSocketId.Telegraph, 1.0f, new Vector3(0f, 0.08f, 0f), Vector3.zero, Vector3.one * 0.46f, false);
        AddEntry(catalog, BattlePresentationCueType.RepositionStart, "Assets/Epic Toon FX/Prefabs/Environment/Dust/DustDirtyPoofSoft.prefab", BattleActorSocketId.FeetRing, 1.0f, Vector3.zero, Vector3.zero, Vector3.one * 0.46f, false);
        AddEntry(catalog, BattlePresentationCueType.DeathStart, "Assets/Epic Toon FX/Prefabs/Combat/Death/Souls/SoulGenericDeath.prefab", BattleActorSocketId.Center, 1.7f, Vector3.zero, Vector3.zero, Vector3.one * 0.62f, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitSkill, "Assets/Epic Toon FX/Prefabs/Combat/Sword/Wave/SwordWaveBlue.prefab", BattleActorSocketId.ProjectileOrigin, 1.25f, new Vector3(0f, 0.08f, 0.05f), Vector3.zero, Vector3.one * 0.58f, false, presentationFamily: SkillPresentationFamily.Melee, presentationSkin: SkillPresentationSkin.GuardSteel);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitSkill, "Assets/Epic Toon FX/Prefabs/Combat/Missiles/FireballSoft/FireballSoftMissileFire.prefab", BattleActorSocketId.ProjectileOrigin, 1.45f, new Vector3(0f, 0.08f, 0.05f), Vector3.zero, Vector3.one * 0.54f, false, BattleAnimationSemantic.BowShot, SkillPresentationFamily.Projectile, SkillPresentationSkin.Fire);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitSkill, "Assets/Epic Toon FX/Prefabs/Combat/Missiles/MagicSoft/MagicMissileSoftBlue.prefab", BattleActorSocketId.ProjectileOrigin, 1.45f, new Vector3(0f, 0.08f, 0.05f), Vector3.zero, Vector3.one * 0.58f, false, BattleAnimationSemantic.ProjectileCast, SkillPresentationFamily.Debuff, SkillPresentationSkin.EchoArcane);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitHeal, "Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealOnceBurst.prefab", BattleActorSocketId.Cast, 0.95f, new Vector3(0f, 0.08f, 0f), Vector3.zero, Vector3.one * 0.42f, false, presentationFamily: SkillPresentationFamily.Heal, presentationSkin: SkillPresentationSkin.HealGold);
        AddEntry(catalog, BattlePresentationCueType.GuardEnter, "Assets/Epic Toon FX/Prefabs/Combat/Shield/ShieldSoftBlue.prefab", BattleActorSocketId.Telegraph, 1.1f, new Vector3(0f, 0.08f, 0f), Vector3.zero, Vector3.one * 0.50f, false, BattleAnimationSemantic.GuardPose, SkillPresentationFamily.Shield, SkillPresentationSkin.GuardSteel);
        AddEntry(catalog, BattlePresentationCueType.RepositionStart, "Assets/Epic Toon FX/Prefabs/Environment/Dust/DustDirtyPoofSoft.prefab", BattleActorSocketId.FeetRing, 1.0f, Vector3.zero, Vector3.zero, Vector3.one * 0.48f, false, BattleAnimationSemantic.DashEngage, SkillPresentationFamily.Reposition, SkillPresentationSkin.FrostGlass);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitSkill, "Assets/Epic Toon FX/Prefabs/Combat/Magic/Field/MagicFieldBlue.prefab", BattleActorSocketId.Telegraph, 1.7f, new Vector3(0f, 0.04f, 0f), Vector3.zero, Vector3.one * 0.62f, false, presentationFamily: SkillPresentationFamily.Zone, presentationSkin: SkillPresentationSkin.FrostGlass);
    }

    private static void AddEntry(
        BattleVfxCatalog catalog,
        BattlePresentationCueType cueType,
        string prefabPath,
        BattleActorSocketId socketId,
        float lifetimeSeconds,
        Vector3 localOffset,
        Vector3 localEulerAngles,
        Vector3 localScale,
        bool parentToSocket,
        BattleAnimationSemantic animationSemantic = BattleAnimationSemantic.None,
        SkillPresentationFamily presentationFamily = SkillPresentationFamily.Any,
        SkillPresentationSkin presentationSkin = SkillPresentationSkin.Any)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        catalog.SetEntry(cueType, prefab, socketId, lifetimeSeconds, localOffset, localEulerAngles, localScale, parentToSocket, animationSemantic, presentationFamily, presentationSkin);
    }
}
#endif
