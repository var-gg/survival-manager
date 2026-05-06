using System;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public enum BattleHumanoidAnimationStance
{
    Default = 0,
    OneHand = 1,
    TwoHand = 2,
    Shield = 3,
    Bow = 4,
    Spell = 5,
    Thrown = 6,
    Rifle = 7,
    Gun = 8,
}

[CreateAssetMenu(menuName = "SM/Battle/Humanoid Animation Set", fileName = "BattleHumanoidAnimationSet")]
public sealed partial class BattleHumanoidAnimationSet : ScriptableObject
{
    public const string ResourcesPath = "_Game/Battle/BattleHumanoidAnimationSet";

    [SerializeField] private BattleHumanoidAnimationStance stance = BattleHumanoidAnimationStance.Default;
    [SerializeField] private AnimationClip idle = null!;
    [SerializeField] private AnimationClip move = null!;
    [SerializeField] private AnimationClip guardLoop = null!;
    [SerializeField] private AnimationClip guardEnter = null!;
    [SerializeField] private AnimationClip guardExit = null!;
    [SerializeField] private AnimationClip death = null!;
    [SerializeField] private AnimationClip[] windups = Array.Empty<AnimationClip>();
    [SerializeField] private AnimationClip[] basicAttacks = Array.Empty<AnimationClip>();
    [SerializeField] private AnimationClip[] damagingSkills = Array.Empty<AnimationClip>();
    [SerializeField] private AnimationClip[] healSkills = Array.Empty<AnimationClip>();
    [SerializeField] private AnimationClip[] hits = Array.Empty<AnimationClip>();

    public BattleHumanoidAnimationStance Stance => stance;

    public bool TryResolveLoopClip(BattleUnitReadModel state, out AnimationClip clip)
    {
        return TryResolveLoopClip(state, isLocomoting: false, out clip);
    }

    public bool TryResolveLoopClip(BattleUnitReadModel state, bool isLocomoting, out AnimationClip clip)
    {
        clip = ResolveLoopClip(state, isLocomoting)!;
        return clip != null;
    }

    public bool TryResolveCueClip(BattlePresentationCue cue, BattleUnitReadModel state, out AnimationClip clip)
    {
        clip = ResolveCueClip(cue, state)!;
        return clip != null;
    }

    public static BattleHumanoidAnimationSet? ResolveRuntimeSet(BattleHumanoidAnimationSet? configuredSet)
    {
        if (configuredSet != null)
        {
            return configuredSet;
        }

        var loaded = Resources.Load<BattleHumanoidAnimationSet>(ResourcesPath);
        if (loaded != null && loaded.HasAnyClip())
        {
            return loaded;
        }

#if UNITY_EDITOR
        return TryCreateEditorKevinFallbackSet();
#else
        return null;
#endif
    }

    private AnimationClip? ResolveLoopClip(BattleUnitReadModel state, bool isLocomoting)
    {
        if (!state.IsAlive || state.ActionState == CombatActionState.Dead)
        {
            return death != null ? death : idle;
        }

        if (isLocomoting)
        {
            return move != null ? move : idle;
        }

        if (state.IsDefending || state.PendingActionType == BattleActionType.WaitDefend)
        {
            return guardLoop != null ? guardLoop : FirstNonNull(guardEnter, idle);
        }

        if (state.ActionState is CombatActionState.AdvanceToAnchor
            or CombatActionState.Approach
            or CombatActionState.Reposition
            or CombatActionState.BreakContact)
        {
            return move != null ? move : idle;
        }

        return idle;
    }

    private AnimationClip? ResolveCueClip(BattlePresentationCue cue, BattleUnitReadModel state)
    {
        var seed = ResolveCueSeed(cue);
        return cue.CueType switch
        {
            BattlePresentationCueType.WindupEnter => PickVariant(windups, seed),
            BattlePresentationCueType.ActionCommitBasic => PickVariant(basicAttacks, seed),
            BattlePresentationCueType.ActionCommitSkill => ResolveSkillClip(cue, state, seed),
            BattlePresentationCueType.ActionCommitHeal => PickVariant(healSkills, seed),
            BattlePresentationCueType.ImpactDamage => PickVariant(hits, seed),
            BattlePresentationCueType.GuardEnter => FirstNonNull(guardEnter, guardLoop, idle),
            BattlePresentationCueType.GuardExit => FirstNonNull(guardExit, idle),
            BattlePresentationCueType.RepositionStart => FirstNonNull(move, idle),
            BattlePresentationCueType.DeathStart => FirstNonNull(death, idle),
            _ => null,
        };
    }

    private AnimationClip? ResolveSkillClip(BattlePresentationCue cue, BattleUnitReadModel state, int seed)
    {
        if (cue.CueType == BattlePresentationCueType.ActionCommitHeal
            || BattleReadabilityFormatter.ResolveSemantic(state) == BattleActionSemantic.HealSupport)
        {
            return PickVariant(healSkills, seed) ?? PickVariant(damagingSkills, seed);
        }

        return PickVariant(damagingSkills, seed) ?? PickVariant(basicAttacks, seed);
    }

    private static AnimationClip? PickVariant(AnimationClip[]? clips, int seed)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        var start = PositiveModulo(seed, clips.Length);
        for (var i = 0; i < clips.Length; i++)
        {
            var clip = clips[(start + i) % clips.Length];
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private static AnimationClip? FirstNonNull(params AnimationClip?[] clips)
    {
        foreach (var clip in clips)
        {
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private bool HasAnyClip()
    {
        return idle != null
               || move != null
               || guardLoop != null
               || guardEnter != null
               || guardExit != null
               || death != null
               || HasAnyClip(windups)
               || HasAnyClip(basicAttacks)
               || HasAnyClip(damagingSkills)
               || HasAnyClip(healSkills)
               || HasAnyClip(hits);
    }

    private static bool HasAnyClip(AnimationClip[]? clips)
    {
        if (clips == null)
        {
            return false;
        }

        foreach (var clip in clips)
        {
            if (clip != null)
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveCueSeed(BattlePresentationCue cue)
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + cue.StepIndex;
            hash = hash * 31 + (int)cue.CueType;
            hash = hash * 31 + StableHash(cue.SubjectActorId);
            hash = hash * 31 + StableHash(cue.RelatedActorId ?? string.Empty);
            return hash;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            for (var i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash;
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
