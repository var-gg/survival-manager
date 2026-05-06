#if UNITY_EDITOR
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

        AddEntry(catalog, BattlePresentationCueType.WindupEnter, "Assets/Epic Toon FX/Prefabs/Interactive/Flares/Sharp/FlareSharpYellow.prefab", BattleActorSocketId.Telegraph, 1.2f, Vector3.zero, Vector3.zero, Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitBasic, "Assets/Epic Toon FX/Prefabs/Combat/Sword/Slash/SwordSlashThin/SwordSlashThinWhite.prefab", BattleActorSocketId.ProjectileOrigin, 1.4f, Vector3.zero, new Vector3(0f, 0f, 0f), Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitSkill, "Assets/Epic Toon FX/Prefabs/Combat/Missiles/Storm/StormMissile.prefab", BattleActorSocketId.ProjectileOrigin, 2.0f, Vector3.zero, Vector3.zero, Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitHeal, "Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealOnce.prefab", BattleActorSocketId.Cast, 2.0f, Vector3.zero, Vector3.zero, Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.ImpactDamage, "Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/HitDustExplosion.prefab", BattleActorSocketId.Hit, 1.5f, Vector3.zero, Vector3.zero, Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.ImpactHeal, "Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealNova.prefab", BattleActorSocketId.Head, 2.0f, Vector3.zero, Vector3.zero, Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.GuardEnter, "Assets/Epic Toon FX/Prefabs/Combat/Shield/ShieldSoftBlue.prefab", BattleActorSocketId.Telegraph, 2.0f, Vector3.zero, Vector3.zero, Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.RepositionStart, "Assets/Epic Toon FX/Prefabs/Environment/Dust/DustDirtyPoofSoft.prefab", BattleActorSocketId.FeetRing, 1.4f, Vector3.zero, Vector3.zero, Vector3.one, false);
        AddEntry(catalog, BattlePresentationCueType.DeathStart, "Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/FlashExplosionRadial.prefab", BattleActorSocketId.Center, 2.0f, Vector3.zero, Vector3.zero, Vector3.one, false);

        _editorEpicToonFxFallbackCatalog = catalog.HasAnyEntry() ? catalog : null;
        return _editorEpicToonFxFallbackCatalog;
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
        bool parentToSocket)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        catalog.SetEntry(cueType, prefab, socketId, lifetimeSeconds, localOffset, localEulerAngles, localScale, parentToSocket);
    }
}
#endif
