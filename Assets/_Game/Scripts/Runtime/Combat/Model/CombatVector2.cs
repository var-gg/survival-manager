using System;

namespace SM.Combat.Model;

public readonly record struct CombatVector2(float X, float Y)
{
    public static CombatVector2 Zero => new(0f, 0f);

    public float Length => MathF.Sqrt((X * X) + (Y * Y));
    public float SqrLength => (X * X) + (Y * Y);

    public CombatVector2 Normalized
    {
        get
        {
            if (SqrLength <= 0.0001f)
            {
                return Zero;
            }

            var inverse = 1f / Length;
            return new CombatVector2(X * inverse, Y * inverse);
        }
    }

    public float DistanceTo(CombatVector2 other) => (this - other).Length;

    public static CombatVector2 Lerp(CombatVector2 from, CombatVector2 to, float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        return new CombatVector2(
            from.X + ((to.X - from.X) * clamped),
            from.Y + ((to.Y - from.Y) * clamped));
    }

    public static CombatVector2 MoveTowards(CombatVector2 current, CombatVector2 target, float maxDistanceDelta)
    {
        var delta = target - current;
        var distance = delta.Length;
        if (distance <= maxDistanceDelta || distance <= 0.0001f)
        {
            return target;
        }

        return current + (delta.Normalized * maxDistanceDelta);
    }

    public static float Dot(CombatVector2 left, CombatVector2 right) => (left.X * right.X) + (left.Y * right.Y);

    public static CombatVector2 operator +(CombatVector2 left, CombatVector2 right) => new(left.X + right.X, left.Y + right.Y);
    public static CombatVector2 operator -(CombatVector2 left, CombatVector2 right) => new(left.X - right.X, left.Y - right.Y);
    public static CombatVector2 operator *(CombatVector2 value, float scalar) => new(value.X * scalar, value.Y * scalar);
    public static CombatVector2 operator /(CombatVector2 value, float scalar) => scalar == 0f ? Zero : new(value.X / scalar, value.Y / scalar);
}
