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

        AddEntry(catalog, BattlePresentationCueType.WindupEnter, "Assets/Epic Toon FX/Prefabs/Combat/Magic/Charge/MagicChargeYellow.prefab", BattleActorSocketId.Telegraph, 1.1f, new Vector3(0f, 0.04f, 0f), Vector3.zero, Vector3.one * 0.58f, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitBasic, "Assets/Epic Toon FX/Prefabs/Combat/Sword/Slash/SwordSlashThick/SwordSlashThickWhite.prefab", BattleActorSocketId.ProjectileOrigin, 1.15f, new Vector3(0f, 0.10f, 0.05f), new Vector3(0f, 0f, 0f), Vector3.one * 0.72f, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitSkill, "Assets/Epic Toon FX/Prefabs/Combat/Missiles/MagicSoft/MagicMissileSoftBlue.prefab", BattleActorSocketId.ProjectileOrigin, 1.45f, new Vector3(0f, 0.08f, 0.05f), Vector3.zero, Vector3.one * 0.58f, false);
        AddEntry(catalog, BattlePresentationCueType.ActionCommitHeal, "Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealOnceBurst.prefab", BattleActorSocketId.Cast, 1.55f, new Vector3(0f, 0.08f, 0f), Vector3.zero, Vector3.one * 0.70f, false);
        AddEntry(catalog, BattlePresentationCueType.ImpactDamage, "Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/TargetHitExplosion.prefab", BattleActorSocketId.Hit, 1.1f, Vector3.zero, Vector3.zero, Vector3.one * 0.58f, false);
        AddEntry(catalog, BattlePresentationCueType.ImpactHeal, "Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealNova.prefab", BattleActorSocketId.Head, 1.65f, Vector3.zero, Vector3.zero, Vector3.one * 0.62f, false);
        AddEntry(catalog, BattlePresentationCueType.GuardEnter, "Assets/Epic Toon FX/Prefabs/Combat/Shield/ShieldSoftBlue.prefab", BattleActorSocketId.Telegraph, 1.7f, new Vector3(0f, 0.08f, 0f), Vector3.zero, Vector3.one * 0.78f, false);
        AddEntry(catalog, BattlePresentationCueType.RepositionStart, "Assets/Epic Toon FX/Prefabs/Environment/Dust/DustDirtyPoofSoft.prefab", BattleActorSocketId.FeetRing, 1.0f, Vector3.zero, Vector3.zero, Vector3.one * 0.46f, false);
        AddEntry(catalog, BattlePresentationCueType.DeathStart, "Assets/Epic Toon FX/Prefabs/Combat/Death/Souls/SoulGenericDeath.prefab", BattleActorSocketId.Center, 1.7f, Vector3.zero, Vector3.zero, Vector3.one * 0.62f, false);

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
