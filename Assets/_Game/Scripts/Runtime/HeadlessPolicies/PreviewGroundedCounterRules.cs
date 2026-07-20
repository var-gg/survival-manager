using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

public static class PreviewCounterCapability
{
    public const string AntiClusterSpacing = "anti_cluster_spacing";
    public const string PriorityAccess = "priority_access";
    public const string SustainDisruption = "sustain_disruption";
    public const string WallBreak = "wall_break";
    public const string Protection = "protection";
    public const string ControlResistance = "control_resistance";
    public const string AreaPressure = "area_pressure";
}

public sealed class PreviewCounterConnection
{
    public PreviewCounterConnection(
        string threatTag,
        string capabilityTag,
        string heroId,
        string threatEvidenceSignalKey,
        string heroEvidenceSignalKey)
    {
        ThreatTag = threatTag;
        CapabilityTag = capabilityTag;
        HeroId = heroId;
        ThreatEvidenceSignalKey = threatEvidenceSignalKey;
        HeroEvidenceSignalKey = heroEvidenceSignalKey;
    }

    public string ThreatTag { get; }
    public string CapabilityTag { get; }
    public string HeroId { get; }
    public string ThreatEvidenceSignalKey { get; }
    public string HeroEvidenceSignalKey { get; }
}

/// <summary>위협 태그와 roster skill card 사이의 설명 가능한 연결만 생성한다.</summary>
public static class PreviewGroundedCounterRules
{
    public static IReadOnlyList<PreviewCounterConnection> Connect(
        EnemyThreatProfile profile,
        IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var connections = new List<PreviewCounterConnection>();
        foreach (var finding in profile.Findings)
        {
            foreach (var hero in heroes.OrderBy(value => value.HeroId, StringComparer.Ordinal))
            {
                var match = FindCapability(finding.Tag, hero);
                if (match == null)
                {
                    continue;
                }

                connections.Add(new PreviewCounterConnection(
                    finding.Tag,
                    match.CapabilityTag,
                    hero.HeroId,
                    finding.EvidenceSignalKeys.OrderBy(value => value, StringComparer.Ordinal).First(),
                    HeadlessPolicyEvidence.HeroSkillSignal(hero.HeroId, match.Skill.SkillId)));
            }
        }

        return connections
            .OrderBy(value => value.ThreatTag, StringComparer.Ordinal)
            .ThenBy(value => value.CapabilityTag, StringComparer.Ordinal)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .ThenBy(value => value.HeroEvidenceSignalKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static CapabilityMatch FindCapability(string threatTag, HeadlessHeroObservation hero)
    {
        return threatTag switch
        {
            EnemyThreatTag.AntiClusterAoe => FindSkill(hero, PreviewCounterCapability.AntiClusterSpacing, skill =>
                string.Equals(hero.ClassId, "ranger", StringComparison.Ordinal)
                && skill.Kind == SkillKind.Strike
                && (skill.Range >= 2.5f
                    || skill.Delivery is SkillDelivery.Ranged or SkillDelivery.Projectile)),
            EnemyThreatTag.BacklineDive => FindSkill(hero, PreviewCounterCapability.Protection, skill =>
                skill.Kind is SkillKind.Heal or SkillKind.Shield
                || HasStatus(skill, "guarded", "barrier", "shield")),
            EnemyThreatTag.BacklineFirepower => FindSkill(hero, PreviewCounterCapability.PriorityAccess, skill =>
                skill.TargetRule is SkillTargetRule.LowestHpEnemy or SkillTargetRule.MostExposedEnemy
                && (skill.Range >= 2.5f
                    || skill.Delivery is SkillDelivery.Ranged or SkillDelivery.Projectile
                    || string.Equals(hero.ClassId, "duelist", StringComparison.Ordinal))),
            EnemyThreatTag.SustainEngine => FindSkill(hero, PreviewCounterCapability.SustainDisruption, skill =>
                HasStatus(skill, "silence", "bleed", "wound")
                || skill.Kind == SkillKind.Debuff && skill.TargetRule != SkillTargetRule.Self),
            EnemyThreatTag.FrontlineWall => FindSkill(hero, PreviewCounterCapability.WallBreak, skill =>
                HasStatus(skill, "sunder", "exposed", "armor_break")),
            EnemyThreatTag.HighPressure => FindSkill(hero, PreviewCounterCapability.Protection, skill =>
                skill.Kind is SkillKind.Heal or SkillKind.Shield
                || HasStatus(skill, "guarded", "barrier", "shield")),
            EnemyThreatTag.ControlPressure => FindSkill(hero, PreviewCounterCapability.ControlResistance, skill =>
                HasStatus(skill, "unstoppable", "cleanse", "immunity")),
            EnemyThreatTag.SwarmPressure => FindSkill(hero, PreviewCounterCapability.AreaPressure, skill =>
                skill.Delivery is SkillDelivery.Nova or SkillDelivery.Zone or SkillDelivery.Trap),
            _ => null,
        };
    }

    private static CapabilityMatch FindSkill(
        HeadlessHeroObservation hero,
        string capabilityTag,
        Func<HeadlessSkillObservation, bool> predicate)
    {
        var skill = hero.SkillCards.Where(predicate)
            .OrderBy(value => value.SkillId, StringComparer.Ordinal)
            .FirstOrDefault();
        return skill == null ? null : new CapabilityMatch(capabilityTag, skill);
    }

    private static bool HasStatus(HeadlessSkillObservation skill, params string[] tokens)
        => skill.AppliedStatuses.Any(status => tokens.Any(token =>
            status.StatusId.Contains(token, StringComparison.OrdinalIgnoreCase)));

    private sealed class CapabilityMatch
    {
        public CapabilityMatch(string capabilityTag, HeadlessSkillObservation skill)
        {
            CapabilityTag = capabilityTag;
            Skill = skill;
        }

        public string CapabilityTag { get; }
        public HeadlessSkillObservation Skill { get; }
    }
}

public static class PreviewGroundedEvidenceGuard
{
    public static void RequireSupported(
        string decisionReason,
        IReadOnlyList<PreviewCounterConnection> connections)
    {
        if (!string.Equals(decisionReason, IntentDecisionReason.CounterAdapt, StringComparison.Ordinal))
        {
            return;
        }

        if (connections == null
            || connections.Count == 0
            || connections.Any(value => string.IsNullOrWhiteSpace(value.ThreatEvidenceSignalKey)
                                        || string.IsNullOrWhiteSpace(value.HeroEvidenceSignalKey)))
        {
            throw new HeadlessPolicyEvidenceException(
                "Counter adaptation requires at least one visible threat-to-capability evidence connection.");
        }
    }
}
