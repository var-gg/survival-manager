using System;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Combat.Model;

public enum BodySizeCategory
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

public enum MobilityStyle
{
    None = 0,
    Dash = 1,
    Roll = 2,
    Blink = 3,
}

public enum MobilityPurpose
{
    None = 0,
    Engage = 1,
    Disengage = 2,
    Evade = 3,
    Chase = 4,
    MaintainRange = 5,
}

public enum ReevaluationReason
{
    None = 0,
    Cadence = 1,
    TargetLost = 2,
    SlotLost = 3,
    TookHit = 4,
    SkillReady = 5,
    MobilityReady = 6,
    RangeBreak = 7,
    TargetMoved = 8,
}

public enum PositioningIntentKind
{
    None = 0,
    Frontline = 1,
    FlankLeft = 2,
    FlankRight = 3,
    BacklineDive = 4,
    MaintainRange = 5,
}

public readonly record struct FloatRange(float Min, float Max)
{
    public float ClampedMin => MathF.Max(0f, MathF.Min(Min, Max));
    public float ClampedMax => MathF.Max(ClampedMin, Max);
    public float Midpoint => (ClampedMin + ClampedMax) * 0.5f;

    public bool Contains(float value, float hysteresis = 0f)
    {
        var margin = MathF.Max(0f, hysteresis);
        return value >= ClampedMin - margin && value <= ClampedMax + margin;
    }
}

public sealed record FootprintProfile(
    float NavigationRadius,
    float SeparationRadius,
    float CombatReach,
    FloatRange PreferredRangeBand,
    int EngagementSlotCount,
    float EngagementSlotRadius,
    BodySizeCategory BodySizeCategory,
    float HeadAnchorHeight)
{
    public float PreferredRangeMin => PreferredRangeBand.ClampedMin;
    public float PreferredRangeMax => PreferredRangeBand.ClampedMax;
}

public sealed record BehaviorProfile(
    float ReevaluationInterval,
    float RangeHysteresis,
    float RetreatBias,
    float MaintainRangeBias,
    float Opportunism,
    float Discipline,
    float DodgeChance,
    float BlockChance,
    float BlockMitigation,
    float Stability,
    float BlockCooldownSeconds = 1.2f,
    FormationLine FormationLine = FormationLine.Frontline,
    RangeDiscipline RangeDiscipline = RangeDiscipline.HoldBand,
    float PreferredRangeMin = 0f,
    float PreferredRangeMax = 0f,
    float ApproachBuffer = 0.4f,
    float RetreatBuffer = 0.25f,
    float ChaseLeashMeters = 5f,
    float RetreatAtHpPercent = 0f,
    float FrontlineGuardRadius = 2.5f);

public sealed record MobilityActionProfile(
    MobilityStyle Style,
    MobilityPurpose Purpose,
    float Distance,
    float Cooldown,
    float CastTime,
    float Recovery,
    float TriggerMinDistance,
    float TriggerMaxDistance,
    float LateralBias)
{
    public bool IsEnabled => Style != MobilityStyle.None && Distance > 0.01f;
}

public sealed record MobilityDecision(
    MobilityActionProfile Profile,
    CombatVector2 Destination);

public sealed record EngagementSlotAssignment(
    EntityId TargetId,
    int SlotIndex,
    CombatVector2 Position,
    bool IsOverflow);

public static class CombatProfileDefaults
{
    public static FootprintProfile ResolveFootprint(FootprintProfile? authored, string classId, float attackRange, float collisionRadius, float maxHealth)
    {
        if (authored != null)
        {
            return authored with
            {
                NavigationRadius = MathF.Max(0.15f, authored.NavigationRadius),
                SeparationRadius = MathF.Max(0.2f, authored.SeparationRadius),
                CombatReach = MathF.Max(0.4f, authored.CombatReach),
                PreferredRangeBand = new FloatRange(
                    MathF.Max(0f, authored.PreferredRangeBand.ClampedMin),
                    MathF.Max(authored.PreferredRangeBand.ClampedMin, authored.PreferredRangeBand.ClampedMax)),
                EngagementSlotCount = Math.Max(1, authored.EngagementSlotCount),
                EngagementSlotRadius = MathF.Max(0.4f, authored.EngagementSlotRadius),
                HeadAnchorHeight = MathF.Max(1.1f, authored.HeadAnchorHeight),
            };
        }

        var safeRange = MathF.Max(0.8f, attackRange);
        var safeCollision = MathF.Max(0.15f, collisionRadius);
        return classId switch
        {
            "vanguard" => new FootprintProfile(
                MathF.Max(0.42f, safeCollision - 0.04f),
                MathF.Max(0.7f, safeCollision + 0.2f),
                MathF.Min(0.68f, safeRange),
                new FloatRange(0.6f, 1.15f),
                5,
                0.95f,
                maxHealth >= 25f || safeCollision >= 0.55f ? BodySizeCategory.Large : BodySizeCategory.Medium,
                2.15f),
            "duelist" => new FootprintProfile(
                MathF.Max(0.38f, safeCollision - 0.1f),
                MathF.Max(0.62f, safeCollision + 0.12f),
                MathF.Min(0.58f, safeRange),
                new FloatRange(0.55f, 1.25f),
                6,
                0.9f,
                BodySizeCategory.Medium,
                2f),
            "ranger" => new FootprintProfile(
                MathF.Max(0.32f, safeCollision * 0.8f),
                MathF.Max(0.72f, safeCollision + 0.24f),
                safeRange,
                new FloatRange(MathF.Max(5f, safeRange - 0.6f), MathF.Max(5.8f, safeRange + 0.2f)),
                3,
                MathF.Max(1.5f, safeRange * 0.7f),
                BodySizeCategory.Small,
                1.92f),
            "mystic" => new FootprintProfile(
                MathF.Max(0.32f, safeCollision * 0.8f),
                MathF.Max(0.76f, safeCollision + 0.28f),
                safeRange,
                new FloatRange(MathF.Max(2.1f, safeRange - 0.55f), MathF.Max(2.7f, safeRange + 0.2f)),
                3,
                MathF.Max(1.45f, safeRange * 0.68f),
                BodySizeCategory.Small,
                1.96f),
            _ => new FootprintProfile(
                MathF.Max(0.4f, safeCollision - 0.05f),
                MathF.Max(0.65f, safeCollision + 0.15f),
                MathF.Min(0.65f, safeRange),
                new FloatRange(MathF.Min(0.65f, MathF.Max(0.55f, safeRange - 0.65f)), safeRange),
                4,
                0.95f,
                BodySizeCategory.Medium,
                2f),
        };
    }

    public static BehaviorProfile ResolveBehavior(BehaviorProfile? authored, string classId)
    {
        if (authored != null)
        {
            return authored with
            {
                ReevaluationInterval = MathF.Max(0.1f, authored.ReevaluationInterval),
                RangeHysteresis = MathF.Max(0f, authored.RangeHysteresis),
                RetreatBias = Math.Clamp(authored.RetreatBias, 0f, 1f),
                MaintainRangeBias = Math.Clamp(authored.MaintainRangeBias, 0f, 1f),
                Opportunism = Math.Clamp(authored.Opportunism, 0f, 1f),
                Discipline = Math.Clamp(authored.Discipline, 0f, 1f),
                DodgeChance = Math.Clamp(authored.DodgeChance, 0f, 1f),
                BlockChance = Math.Clamp(authored.BlockChance, 0f, 1f),
                BlockMitigation = Math.Clamp(authored.BlockMitigation, 0f, 0.9f),
                Stability = Math.Clamp(authored.Stability, 0f, 1f),
                BlockCooldownSeconds = MathF.Max(0f, authored.BlockCooldownSeconds),
                PreferredRangeMin = MathF.Max(0f, authored.PreferredRangeMin),
                PreferredRangeMax = MathF.Max(authored.PreferredRangeMin, authored.PreferredRangeMax),
                ApproachBuffer = MathF.Max(0f, authored.ApproachBuffer),
                RetreatBuffer = MathF.Max(0f, authored.RetreatBuffer),
                ChaseLeashMeters = MathF.Max(0.5f, authored.ChaseLeashMeters),
                RetreatAtHpPercent = Math.Clamp(authored.RetreatAtHpPercent, 0f, 1f),
                FrontlineGuardRadius = MathF.Max(0.5f, authored.FrontlineGuardRadius),
            };
        }

        return classId switch
        {
            "vanguard" => new BehaviorProfile(0.25f, 0.16f, 0.04f, 0.05f, 0.34f, 0.82f, 0.02f, 0.28f, 0.38f, 0.88f, 1f, FormationLine.Frontline, RangeDiscipline.Collapse, 0.6f, 1.15f, 0.25f, 0.2f, 5f, 0f, 3f),
            "duelist" => new BehaviorProfile(0.25f, 0.22f, 0.22f, 0.24f, 0.72f, 0.58f, 0.08f, 0.12f, 0.18f, 0.62f, 1.15f, FormationLine.Frontline, RangeDiscipline.HoldBand, 0.55f, 1.25f, 0.28f, 0.25f, 5.5f, 0.2f, 2.2f),
            "ranger" => new BehaviorProfile(0.25f, 0.28f, 0.72f, 0.84f, 0.58f, 0.74f, 0.12f, 0.04f, 0.12f, 0.34f, 1.5f, FormationLine.Backline, RangeDiscipline.KiteBackward, 5f, 5.8f, 0.55f, 0.35f, 10.5f, 0.35f, 1.5f),
            "mystic" => new BehaviorProfile(0.25f, 0.3f, 0.68f, 0.78f, 0.5f, 0.84f, 0.06f, 0.06f, 0.18f, 0.45f, 1.35f, FormationLine.Backline, RangeDiscipline.AnchorNearFrontline, 2.25f, 2.8f, 0.4f, 0.25f, 6f, 0.3f, 1.8f),
            _ => new BehaviorProfile(0.25f, 0.2f, 0.15f, 0.15f, 0.5f, 0.5f, 0.04f, 0.08f, 0.2f, 0.5f, 1.2f, FormationLine.Midline, RangeDiscipline.HoldBand, 1f, 2f, 0.4f, 0.25f, 5f, 0.25f, 2.5f),
        };
    }

    public static MobilityActionProfile? ResolveMobility(MobilityActionProfile? authored, string classId)
    {
        if (authored != null)
        {
            return authored with
            {
                Distance = MathF.Max(0f, authored.Distance),
                Cooldown = MathF.Max(0f, authored.Cooldown),
                CastTime = MathF.Max(0f, authored.CastTime),
                Recovery = MathF.Max(0f, authored.Recovery),
                TriggerMinDistance = MathF.Max(0f, authored.TriggerMinDistance),
                TriggerMaxDistance = MathF.Max(0f, authored.TriggerMaxDistance),
                LateralBias = Math.Clamp(authored.LateralBias, -1f, 1f),
            };
        }

        return classId switch
        {
            "vanguard" => new MobilityActionProfile(MobilityStyle.Dash, MobilityPurpose.Engage, 0.9f, 5f, 0f, 0.18f, 1.4f, 2.8f, 0f),
            "duelist" => new MobilityActionProfile(MobilityStyle.Dash, MobilityPurpose.Engage, 1.15f, 4.2f, 0f, 0.16f, 1.3f, 3f, 0.2f),
            "ranger" => new MobilityActionProfile(MobilityStyle.Roll, MobilityPurpose.MaintainRange, 1.45f, 3.4f, 0f, 0.22f, 0f, 1.45f, 0.68f),
            "mystic" => new MobilityActionProfile(MobilityStyle.Blink, MobilityPurpose.Disengage, 1.85f, 4.4f, 0f, 0.3f, 0f, 1.35f, 0.35f),
            _ => null,
        };
    }
}
