using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Numerics;

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

    /// <summary>
    /// 결정적 stat 값(Hp64 권위, Phase 4). <see cref="Get"/>의 float 경로는 Phase 2.1 결정적 순서 + 분리된
    /// 단일 +/* 연산이라 이미 cross-platform 안전하다 — 이 accessor는 그 값을 <b>fixed로 직접</b> 제공해 downstream
    /// fixed 수식(damage·scoring·slot)이 use-site에서 재양자화하지 않게 하는 권위다. 규모부(base+flat)는 Hp64,
    /// 배수부(1+increased, more 곱)는 Fixed32로 계산해 Hp64×Fixed32로 결합한다(ceiling 1e6까지 안전). base 매칭은
    /// StatKey ordinal 정렬, modifier는 삽입 순서(<see cref="Get"/>와 동일). stat 값은 아직 저작 float이라 각 입력을
    /// 사용 직전 한 번 양자화한다. 곱셈/clamp 순서는 <see cref="Get"/>와 일치(= projection 정합).
    /// </summary>
    public Hp64 GetWide(StatKey key)
    {
        var canonicalKey = StatKey.Canonicalize(key);

        var magnitude = Hp64.Zero;
        foreach (var pair in OrderedBaseMatches(_baseValues, canonicalKey))
        {
            magnitude += Hp64.FromFloatQuantized(pair.Value);
        }

        var additivePercent = Fixed32.Zero;
        var multiplicativePercent = Fixed32.One;
        var clampMin = Hp64.FromRaw(long.MinValue);
        var clampMax = Hp64.FromRaw(long.MaxValue);

        foreach (var modifier in _modifiers)
        {
            if (!StatKey.Canonicalize(modifier.Stat).Equals(canonicalKey))
            {
                continue;
            }

            switch (modifier.Op)
            {
                case ModifierOp.Flat:
                    magnitude += Hp64.FromFloatQuantized(modifier.Value);
                    break;
                case ModifierOp.Increased:
                    additivePercent += Fixed32.FromFloatQuantized(modifier.Value);
                    break;
                case ModifierOp.More:
                    multiplicativePercent *= Fixed32.One + Fixed32.FromFloatQuantized(modifier.Value);
                    break;
                case ModifierOp.ClampMin:
                    clampMin = Hp64.Max(clampMin, Hp64.FromFloatQuantized(modifier.Value));
                    break;
                case ModifierOp.ClampMax:
                    clampMax = Hp64.Min(clampMax, Hp64.FromFloatQuantized(modifier.Value));
                    break;
            }
        }

        var result = magnitude * (Fixed32.One + additivePercent);
        result *= multiplicativePercent;
        return Hp64.Clamp(result, clampMin, clampMax);
    }

    /// <summary>작은 stat(배수·퍼센트·속도·확률 등 Int32 범위)용 Fixed32 narrowing. 규모값(MaxHealth 등 Int32
    /// 초과 가능)은 <see cref="GetWide"/>를 쓴다 — 여기 넘기면 raw가 wrap된다.</summary>
    public Fixed32 GetFixed(StatKey key) => Fixed32.FromRaw((int)GetWide(key).Raw);

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
