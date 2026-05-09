using System;

namespace SM.Combat.Model;

public sealed class TacticContext
{
    private TacticContext(
        TeamSide side,
        TeamTacticProfile profile,
        int stepIndex,
        float combatPace,
        float focusModeBias,
        float frontSpacingBias,
        float backSpacingBias,
        float protectCarryBias,
        float targetSwitchPenalty,
        float compactness,
        float width,
        float depth,
        float lineSpacing,
        float flankBias)
    {
        Side = side;
        Profile = profile;
        StepIndex = stepIndex;
        Posture = profile.Posture;
        CombatPace = combatPace;
        FocusModeBias = focusModeBias;
        FrontSpacingBias = frontSpacingBias;
        BackSpacingBias = backSpacingBias;
        ProtectCarryBias = protectCarryBias;
        TargetSwitchPenalty = targetSwitchPenalty;
        Compactness = compactness;
        Width = width;
        Depth = depth;
        LineSpacing = lineSpacing;
        FlankBias = flankBias;
    }

    public TeamSide Side { get; }
    public TeamTacticProfile Profile { get; }
    public int StepIndex { get; }
    public TeamPostureType Posture { get; }
    public float CombatPace { get; }
    public float FocusModeBias { get; }
    public float FrontSpacingBias { get; }
    public float BackSpacingBias { get; }
    public float ProtectCarryBias { get; }
    public float TargetSwitchPenalty { get; }
    public float Compactness { get; }
    public float Width { get; }
    public float Depth { get; }
    public float LineSpacing { get; }
    public float FlankBias { get; }

    public static TacticContext Create(TeamSide side, TeamTacticProfile profile, int stepIndex)
    {
        return new TacticContext(
            side,
            profile,
            stepIndex,
            Clamp(profile.CombatPace, 0.75f, 1.35f),
            Clamp(profile.FocusModeBias, -1f, 1f),
            Clamp01(profile.FrontSpacingBias),
            Clamp01(profile.BackSpacingBias),
            Clamp01(profile.ProtectCarryBias),
            Clamp(profile.TargetSwitchPenalty, 0f, 2f),
            Clamp01(profile.Compactness),
            Clamp(profile.Width, 0.35f, 1.5f),
            Clamp(profile.Depth, 0.35f, 1.5f),
            Clamp(profile.LineSpacing, 0.35f, 1.5f),
            Clamp(profile.FlankBias, -1f, 1f));
    }

    public static TeamTacticProfile DefaultProfile(TeamPostureType posture)
    {
        return new TeamTacticProfile($"posture:{posture}", posture.ToString(), posture);
    }

    public float ResolveZoneWidth(float baseLaneGap, float quirkSpread)
    {
        return baseLaneGap * Clamp(0.55f + (0.45f * Width) - (0.25f * Compactness) + quirkSpread, 0.45f, 1.35f);
    }

    public float ResolveZoneDepth(float baseRowGap, float postureDepth)
    {
        return baseRowGap * Clamp(0.35f + (0.45f * Depth) - (0.15f * Compactness) + postureDepth, 0.35f, 1.20f);
    }

    public static float Clamp01(float value)
    {
        return Clamp(value, 0f, 1f);
    }

    public static float Clamp(float value, float min, float max)
    {
        return MathF.Min(max, MathF.Max(min, value));
    }
}
