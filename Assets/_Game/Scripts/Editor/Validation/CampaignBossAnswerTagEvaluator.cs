using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Editor.Validation;

/// <summary>BossLearningSpec이 참조하는 formation/build 답 어휘.</summary>
public static class CampaignBossAnswerTag
{
    public const string BacklineGuardAnchor = "backline_guard_anchor";
    public const string DurableBackCornerBait = "durable_back_corner_bait";
    public const string MarkFocusBurst = "mark_focus_burst";
}

/// <summary>
/// 전투 결과가 아니라 실제 ally loadout/formation만 보고 보스 답의 존재를 판정한다.
/// 학습 사양이 없는 기존 보스는 Phase B의 legacy 판정을 보존한다.
/// </summary>
public static class CampaignBossAnswerTagEvaluator
{
    public static IReadOnlyList<string> Evaluate(
        BattleLoadoutSnapshot snapshot,
        CampaignBossLearningSpec? learningSpec)
    {
        if (learningSpec == null)
        {
            return HasLegacyAnswer(snapshot)
                ? new[] { "legacy_boss_answer" }
                : Array.Empty<string>();
        }

        var allies = snapshot.Allies ?? Array.Empty<BattleUnitLoadout>();
        return learningSpec.AnswerTags
            .Where(tag => tag switch
            {
                CampaignBossAnswerTag.BacklineGuardAnchor => HasBacklineGuardAnchor(allies),
                CampaignBossAnswerTag.DurableBackCornerBait => HasDurableBackCornerBait(allies),
                CampaignBossAnswerTag.MarkFocusBurst => HasMarkFocusBurst(allies),
                _ => false,
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool HasAnswer(BattleLoadoutSnapshot snapshot, CampaignBossLearningSpec? learningSpec)
        => Evaluate(snapshot, learningSpec).Count > 0;

    private static bool HasBacklineGuardAnchor(IReadOnlyList<BattleUnitLoadout> allies)
    {
        var protectedBackliners = allies.Where(unit =>
                unit.PreferredAnchor.IsBackRow()
                && unit.ClassId is "ranger" or "mystic")
            .ToArray();
        return allies.Any(guard =>
            IsGuardBody(guard)
            && protectedBackliners.Any(target =>
                target.Id != guard.Id
                && (guard.PreferredAnchor.IsBackRow()
                    ? Math.Abs(target.PreferredAnchor.LaneIndex() - guard.PreferredAnchor.LaneIndex()) <= 1
                    : target.PreferredAnchor.LaneIndex() == guard.PreferredAnchor.LaneIndex())));
    }

    private static bool HasDurableBackCornerBait(IReadOnlyList<BattleUnitLoadout> allies)
        => allies.Any(unit =>
            unit.PreferredAnchor is DeploymentAnchorId.BackTop or DeploymentAnchorId.BackBottom
            && unit.ClassId is "ranger" or "mystic"
            && (Stat(unit, StatKey.MaxHealth) >= 22f
                || Stat(unit, StatKey.Armor) >= 3f));

    private static bool HasMarkFocusBurst(IReadOnlyList<BattleUnitLoadout> allies)
    {
        var hasAppliedMark = allies.Any(unit =>
            (unit.Skills ?? Array.Empty<BattleSkillSpec>()).Any(skill =>
                (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>()).Any(status =>
                    string.Equals(status.StatusId, "marked", StringComparison.Ordinal))));
        var focusBurstCount = allies.Count(unit =>
            unit.ClassId is "duelist" or "ranger"
            && (unit.Skills ?? Array.Empty<BattleSkillSpec>()).Any(skill => skill.Kind == SkillKind.Strike));
        return hasAppliedMark && focusBurstCount >= 2;
    }

    private static bool HasLegacyAnswer(BattleLoadoutSnapshot snapshot)
    {
        var allies = snapshot.Allies ?? Array.Empty<BattleUnitLoadout>();
        var guardAnchor = allies.Any(unit => unit.PreferredAnchor.IsFrontRow() && IsGuardBody(unit));
        var baitedGap = allies.Count(unit => unit.PreferredAnchor.IsFrontRow()) >= 2
                        && allies.All(unit => unit.PreferredAnchor != DeploymentAnchorId.FrontCenter);
        var hasMark = allies.Any(unit =>
            HasAnyToken(unit.CompileTags, "mark")
            || (unit.Skills ?? Array.Empty<BattleSkillSpec>()).Any(skill => ContainsToken(skill.Id, "mark")));
        var hasBurst = allies.Any(unit =>
            ContainsToken(unit.RoleTag, "carry")
            || unit.ClassId is "duelist" or "ranger");
        return guardAnchor || baitedGap || hasMark && hasBurst;
    }

    private static bool IsGuardBody(BattleUnitLoadout unit)
        => string.Equals(unit.ClassId, "vanguard", StringComparison.Ordinal)
           || ContainsToken(unit.RoleTag, "anchor")
           || HasAnyToken(unit.CompileTags, "guard");

    private static float Stat(BattleUnitLoadout unit, StatKey key)
    {
        var modifiers = unit.NumericPackages
            .Concat(unit.TeamPackages ?? Array.Empty<CombatModifierPackage>())
            .SelectMany(package => package.Modifiers ?? Array.Empty<StatModifier>());
        return new StatBlock(unit.BaseStats, modifiers).Get(key);
    }

    private static bool HasAnyToken(IEnumerable<string>? values, params string[] tokens)
        => (values ?? Array.Empty<string>()).Any(value => tokens.Any(token => ContainsToken(value, token)));

    private static bool ContainsToken(string? value, string token)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
