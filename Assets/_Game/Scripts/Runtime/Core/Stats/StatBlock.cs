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
        var baseValue = _baseValues
            .Where(pair => StatKey.Canonicalize(pair.Key).Equals(canonicalKey))
            .Sum(pair => pair.Value);
        var relevant = _modifiers
            .Where(x => StatKey.Canonicalize(x.Stat).Equals(canonicalKey))
            .ToList();

        var flat = relevant.Where(x => x.Op == ModifierOp.Flat).Sum(x => x.Value);
        var additivePercent = relevant.Where(x => x.Op == ModifierOp.Increased).Sum(x => x.Value);
        var multiplicativePercent = relevant.Where(x => x.Op == ModifierOp.More).Aggregate(1f, (acc, x) => acc * (1f + x.Value));
        var clampMin = relevant.Where(x => x.Op == ModifierOp.ClampMin).Select(x => x.Value).DefaultIfEmpty(float.MinValue).Max();
        var clampMax = relevant.Where(x => x.Op == ModifierOp.ClampMax).Select(x => x.Value).DefaultIfEmpty(float.MaxValue).Min();

        var result = baseValue;
        result += flat;
        result *= (1f + additivePercent);
        result *= multiplicativePercent;
        result = Math.Clamp(result, clampMin, clampMax);
        return result;
    }
}
