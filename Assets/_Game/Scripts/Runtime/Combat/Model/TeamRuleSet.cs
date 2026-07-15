using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Combat.Model;

/// <summary>
/// 전투 시작에 시너지 패키지에서 한 번 컴파일되는 팀 규칙 집합.
/// ordinal 정렬 배열만 소유하며 전투 중에는 변하지 않는다. seed + loadout으로 재구성되는 파생 상수라
/// replay/canonical hash payload에는 별도로 직렬화하지 않는다.
/// </summary>
public sealed class TeamRuleSet
{
    public const string PhalanxRuleId = "rule.phalanx";
    public const string BloodrushRuleId = "rule.bloodrush";
    public const string DeathTollRuleId = "rule.deathtoll";

    internal const string BloodrushStatusId = "team-rule.bloodrush";
    internal const string DeathTollStatusId = "team-rule.deathtoll";
    internal const float BloodrushDurationSeconds = 2.5f;
    internal const float BloodrushTempoPerStack = 0.05f;
    internal const float DeathTollPhysPowerPerStack = 0.25f;
    internal const float DeathTollMaxHealthPerStack = 1f;
    internal const int MaxRuleStacks = 99;

    private static readonly string[] EmptyRules = Array.Empty<string>();
    private readonly string[] _allyRules;
    private readonly string[] _enemyRules;

    public TeamRuleSet(IEnumerable<string>? allyRules = null, IEnumerable<string>? enemyRules = null)
    {
        _allyRules = Normalize(allyRules);
        _enemyRules = Normalize(enemyRules);
    }

    public static TeamRuleSet Empty { get; } = new();

    public bool Has(TeamSide side, string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            return false;
        }

        var rules = side == TeamSide.Ally ? _allyRules : _enemyRules;
        return Array.BinarySearch(rules, ruleId, StringComparer.Ordinal) >= 0;
    }

    private static string[] Normalize(IEnumerable<string>? rules)
    {
        if (rules == null)
        {
            return EmptyRules;
        }

        return rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Select(rule => rule.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(rule => rule, StringComparer.Ordinal)
            .ToArray();
    }
}
