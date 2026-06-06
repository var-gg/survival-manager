using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Core.Stats;

public sealed class StatBlock
{
    private readonly Dictionary<StatKey, float> _baseValues;
    private readonly List<StatModifier> _modifiers;

    public StatBlock(Dictionary<StatKey, float> baseValues, IEnumerable<StatModifier>? modifiers = null)
    {
        _baseValues = baseValues ?? new Dictionary<StatKey, float>();
        _modifiers = modifiers?.ToList() ?? new List<StatModifier>();
    }

    public IReadOnlyDictionary<StatKey, float> BaseValues => _baseValues;
    public IReadOnlyList<StatModifier> Modifiers => _modifiers;

    public void AddModifier(StatModifier modifier) => _modifiers.Add(modifier);

    public float Get(StatKey key)
    {
        var canonicalKey = StatKey.Canonicalize(key);

        // base 합산: Dictionary 열거 순서(런타임/플랫폼 의존)에 기대지 않는다. canonical 매칭을 StatKey ordinal로
        // 정렬한 뒤 합산해, alias 충돌(canonical + legacy가 동시에 존재)에도 cross-platform 동일 결과를 보장한다.
        // 현 콘텐츠는 canonical당 base 엔트리 단일이라 bit-neutral. (Phase 2.1 — 결정적 순서)
        var baseValue = 0f;
        foreach (var pair in OrderedBaseMatches(_baseValues, canonicalKey))
        {
            baseValue += pair.Value;
        }

        // modifier는 삽입 순서 List(결정적)다. Op별 reduction을 그 순서 그대로 단일 패스로 수행한다.
        var flat = 0f;
        var additivePercent = 0f;
        var multiplicativePercent = 1f;
        var clampMin = float.MinValue;
        var clampMax = float.MaxValue;

        foreach (var modifier in _modifiers)
        {
            if (!StatKey.Canonicalize(modifier.Stat).Equals(canonicalKey))
            {
                continue;
            }

            switch (modifier.Op)
            {
                case ModifierOp.Flat:
                    flat += modifier.Value;
                    break;
                case ModifierOp.Increased:
                    additivePercent += modifier.Value;
                    break;
                case ModifierOp.More:
                    multiplicativePercent *= 1f + modifier.Value;
                    break;
                case ModifierOp.ClampMin:
                    clampMin = Math.Max(clampMin, modifier.Value);
                    break;
                case ModifierOp.ClampMax:
                    clampMax = Math.Min(clampMax, modifier.Value);
                    break;
            }
        }

        var result = baseValue;
        result += flat;
        result *= 1f + additivePercent;
        result *= multiplicativePercent;
        result = Math.Clamp(result, clampMin, clampMax);
        return result;
    }

    // canonical 매칭 base 엔트리를 StatKey ordinal 정렬로 모은다 — Dictionary 열거 순서 비결정성 제거(Phase 2.1).
    // 매칭이 0/1개면 정렬 비용 없이 그대로(현 콘텐츠), 2개 이상(alias 충돌)일 때만 안정 정렬.
    private static List<KeyValuePair<StatKey, float>> OrderedBaseMatches(
        IReadOnlyDictionary<StatKey, float> values, StatKey canonicalKey)
    {
        var matches = new List<KeyValuePair<StatKey, float>>();
        foreach (var pair in values)
        {
            if (StatKey.Canonicalize(pair.Key).Equals(canonicalKey))
            {
                matches.Add(pair);
            }
        }

        if (matches.Count > 1)
        {
            matches.Sort(static (a, b) => string.CompareOrdinal(a.Key.Value, b.Key.Value));
        }

        return matches;
    }
}
