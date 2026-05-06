#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SM.Unity;

public sealed partial class BattleHumanoidAnimationSet
{
    private static BattleHumanoidAnimationSet? _editorKevinFallbackSet;

    private static BattleHumanoidAnimationSet? TryCreateEditorKevinFallbackSet()
    {
        if (_editorKevinFallbackSet != null)
        {
            return _editorKevinFallbackSet;
        }

        var idleClip = LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatIdle01.fbx");
        var moveClip = LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx")
                       ?? LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx");

        if (idleClip == null && moveClip == null)
        {
            return null;
        }

        var set = CreateInstance<BattleHumanoidAnimationSet>();
        set.hideFlags = HideFlags.DontSave;
        set.stance = BattleHumanoidAnimationStance.Default;
        set.idle = idleClip!;
        set.move = moveClip!;
        set.guardLoop = LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Shield/HumanM@BlockShield01 - Loop.fbx") ?? idleClip!;
        set.guardEnter = set.guardLoop;
        set.guardExit = idleClip!;
        set.death = LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDeath01.fbx") ?? idleClip!;
        set.windups = NonNullClips(
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect1H01_R - Load.fbx"),
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatIdle01_Action01_R.fbx"));
        set.basicAttacks = NonNullClips(
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Attack1H01_R.fbx"),
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Shield/HumanM@AttackShield01.fbx"));
        set.damagingSkills = NonNullClips(
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect1H01_R - Cast.fbx"),
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Special/HumanM@SpecialMagicAttack01 - Cast.fbx"));
        set.healSkills = NonNullClips(
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Omnidirectional/HumanM@MagicAttackOmni01 - Cast.fbx"),
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/HumanM@CastingEnter01.fbx"));
        set.hits = NonNullClips(
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDamage01.fbx"),
            LoadEditorClip("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/HumanM@CastingDamage01.fbx"));

        _editorKevinFallbackSet = set;
        return _editorKevinFallbackSet;
    }

    private static AnimationClip? LoadEditorClip(string path)
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__", System.StringComparison.Ordinal));
    }

    private static AnimationClip[] NonNullClips(params AnimationClip?[] clips)
    {
        return clips.Where(clip => clip != null).Cast<AnimationClip>().ToArray();
    }
}
#endif
