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
        set.variants = NonNullVariants(
            MakeVariant(BattleAnimationSemantic.BowShot, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/HumanM@BowShot01 - Release.fbx"),
            MakeVariant(BattleAnimationSemantic.BowShot, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/HumanM@BowShot01_Up - Release.fbx"),
            MakeVariant(BattleAnimationSemantic.BowShot, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/HumanM@BowShot01_Down - Release.fbx"),
            MakeVariant(BattleAnimationSemantic.ProjectileCast, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect1H01_R - Cast.fbx"),
            MakeVariant(BattleAnimationSemantic.ProjectileCast, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Special/HumanM@SpecialMagicAttack01 - Cast.fbx"),
            MakeVariant(BattleAnimationSemantic.Dodge, BattleAnimationDirection.Any, BattleAnimationIntensity.Light, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Dodge01.fbx"),
            MakeVariant(BattleAnimationSemantic.Dodge, BattleAnimationDirection.Any, BattleAnimationIntensity.Light, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/HumanM@Roll01.fbx"),
            MakeVariant(BattleAnimationSemantic.Dodge, BattleAnimationDirection.Any, BattleAnimationIntensity.Light, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/HumanM@CastingDodge01.fbx"),
            MakeVariant(BattleAnimationSemantic.DashEngage, BattleAnimationDirection.Forward, BattleAnimationIntensity.Heavy, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_Forward.fbx"),
            MakeVariant(BattleAnimationSemantic.DashEngage, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx"),
            MakeVariant(BattleAnimationSemantic.DashEngage, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/HumanM@RunSlide01.fbx"),
            MakeVariant(BattleAnimationSemantic.BackstepDisengage, BattleAnimationDirection.Backward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Backward.fbx"),
            MakeVariant(BattleAnimationSemantic.BackstepDisengage, BattleAnimationDirection.Backward, BattleAnimationIntensity.Light, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Backward.fbx"),
            MakeVariant(BattleAnimationSemantic.BackstepDisengage, BattleAnimationDirection.Left, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/HumanM@StrafeRun01_BackwardLeft.fbx"),
            MakeVariant(BattleAnimationSemantic.BackstepDisengage, BattleAnimationDirection.Right, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/HumanM@StrafeRun01_BackwardRight.fbx"),
            MakeVariant(BattleAnimationSemantic.LateralStrafe, BattleAnimationDirection.Left, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/HumanM@StrafeRun01_Left.fbx"),
            MakeVariant(BattleAnimationSemantic.LateralStrafe, BattleAnimationDirection.Right, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/HumanM@StrafeRun01_Right.fbx"),
            MakeVariant(BattleAnimationSemantic.BlockImpact, BattleAnimationDirection.Any, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Shield/HumanM@BlockShield01 - Hit.fbx"),
            MakeVariant(BattleAnimationSemantic.BlockImpact, BattleAnimationDirection.Any, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/HumanM@CastingBlock01_R - Hit.fbx"),
            MakeVariant(BattleAnimationSemantic.BlockImpact, BattleAnimationDirection.Any, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Parry1H01_R - Hit.fbx"),
            MakeVariant(BattleAnimationSemantic.CriticalImpact, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Damage05.fbx"),
            MakeVariant(BattleAnimationSemantic.CriticalImpact, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDamage02.fbx"),
            MakeVariant(BattleAnimationSemantic.Knockdown, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Knockdown01 - Fall.fbx"),
            MakeVariant(BattleAnimationSemantic.HitHeavy, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Damage04.fbx"),
            MakeVariant(BattleAnimationSemantic.HitHeavy, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Damage05.fbx"),
            MakeVariant(BattleAnimationSemantic.HitLight, BattleAnimationDirection.Backward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDamage01.fbx"),
            MakeVariant(BattleAnimationSemantic.HitLight, BattleAnimationDirection.Backward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Damage01.fbx"),
            MakeVariant(BattleAnimationSemantic.HitLight, BattleAnimationDirection.Backward, BattleAnimationIntensity.Medium, "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/HumanM@CastingDamage01.fbx"));

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

    private static BattleHumanoidAnimationVariant? MakeVariant(
        BattleAnimationSemantic semantic,
        BattleAnimationDirection direction,
        BattleAnimationIntensity intensity,
        string path,
        float weight = 1f)
    {
        var clip = LoadEditorClip(path);
        return clip == null
            ? null
            : new BattleHumanoidAnimationVariant(semantic, clip, direction, intensity, BattleHumanoidAnimationStance.Default, weight);
    }

    private static BattleHumanoidAnimationVariant[] NonNullVariants(params BattleHumanoidAnimationVariant?[] variants)
    {
        return variants.Where(variant => variant != null).Cast<BattleHumanoidAnimationVariant>().ToArray();
    }
}
#endif
