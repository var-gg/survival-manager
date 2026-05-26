using System;

namespace SM.Meta.Services;

/// <summary>
/// wave-33-progression: prototype baseline hero level curve.
/// 진짜 progression scaling은 별도 balance pass — 본 turn은 UI surface가 다음 level까지 필요 exp를
/// 일관되게 가져갈 수 있는 단일 지점을 만든다.
/// 공식: Level N → Level N+1 까지 필요 누적 exp = 100 + N * 50 (Lv1→Lv2: 150, Lv2→Lv3: 200, ...).
/// god util은 만들지 않는다 — hero progression 한 가지 cuve만 다루는 작은 helper.
/// </summary>
public static class HeroProgressionCurve
{
    public const int BaseExperience = 100;
    public const int PerLevelExperienceStep = 50;

    public static int ExperienceToNextLevel(int currentLevel)
    {
        var level = Math.Max(1, currentLevel);
        return BaseExperience + (level * PerLevelExperienceStep);
    }
}
