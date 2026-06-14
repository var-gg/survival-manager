using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Meta.Services;

/// <summary>
/// 마을 캐릭터 시트 표시용 — 컴파일된 유닛의 base 스탯 + numeric package를 전투와 동일한 방식으로 합쳐
/// effective 스탯을 산출한다.
///
/// 전투 spawn(<see cref="UnitSnapshot"/>)이 쓰는 것과 같은 SoT: base 딕셔너리 + Packages.SelectMany(modifiers)를
/// <see cref="StatBlock"/>에 넘기고 <see cref="StatBlock.Get"/>로 해결한다. 따라서 시트의 effective 값은
/// 전장에서 실제 스폰될 유닛의 스탯과 일치한다.
///
/// 레벨은 전투 스탯에 영향을 주지 않는다(스킬/패시브 해금만 좌우) — delta는 순수 장비/특성/패시브/augment 효과다.
/// 시너지(TeamPackages)는 분대 구성에 따라 변하므로 개별 유닛 스탯에서 제외한다(UnitSnapshot 동일).
///
/// 순수 — Unity 의존 없이 loadout 입력만 받는다(FastUnit 대상).
/// </summary>
public static class HeroEffectiveStatPreview
{
    public sealed record StatEntry(StatKey Key, float BaseValue, float EffectiveValue)
    {
        public float Delta => EffectiveValue - BaseValue;
    }

    /// <summary>컴파일된 유닛의 base/package에서 지정 키들의 base·effective 값을 해결한다.</summary>
    public static IReadOnlyList<StatEntry> Resolve(BattleUnitLoadout unit, IReadOnlyList<StatKey> keys)
    {
        if (unit == null)
        {
            return Array.Empty<StatEntry>();
        }

        return Resolve(unit.BaseStats, unit.Packages, keys);
    }

    /// <summary>base 딕셔너리 + numeric package에서 지정 키들의 base·effective 값을 해결한다(테스트 진입점).</summary>
    public static IReadOnlyList<StatEntry> Resolve(
        IReadOnlyDictionary<StatKey, float> baseStats,
        IReadOnlyList<CombatModifierPackage> packages,
        IReadOnlyList<StatKey> keys)
    {
        if (baseStats == null || keys == null || keys.Count == 0)
        {
            return Array.Empty<StatEntry>();
        }

        // 전투 UnitSnapshot과 동일: Packages 의 modifier 만 합친다(TeamPackages 제외).
        var modifiers = packages?.SelectMany(package => package.Modifiers).ToList() ?? new List<StatModifier>();
        var block = new StatBlock(new Dictionary<StatKey, float>(baseStats), modifiers);

        var entries = new List<StatEntry>(keys.Count);
        foreach (var key in keys)
        {
            baseStats.TryGetValue(key, out var baseValue);
            entries.Add(new StatEntry(key, baseValue, block.Get(key)));
        }

        return entries;
    }
}
