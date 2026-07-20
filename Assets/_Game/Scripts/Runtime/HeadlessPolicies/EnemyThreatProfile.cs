using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

public static class EnemyThreatTag
{
    public const string AntiClusterAoe = "anti_cluster_aoe";
    public const string BacklineDive = "backline_dive";
    public const string BacklineFirepower = "backline_firepower";
    public const string SustainEngine = "sustain_engine";
    public const string FrontlineWall = "frontline_wall";
    public const string HighPressure = "high_pressure";
    public const string ControlPressure = "control_pressure";
    public const string SwarmPressure = "swarm_pressure";
}

/// <summary>
/// 적 preview에서 정책에 허용된 필드만 복사한 site-identity-free 입력 DTO.
/// </summary>
public sealed class EnemyThreatObservation
{
    public EnemyThreatObservation(
        bool isAvailable,
        string difficultyBand,
        int threatSkulls,
        IReadOnlyList<EnemyThreatUnitObservation> units,
        string bossAuraTag,
        string bossUtilityTag)
    {
        IsAvailable = isAvailable;
        DifficultyBand = difficultyBand ?? string.Empty;
        ThreatSkulls = threatSkulls;
        Units = units ?? Array.Empty<EnemyThreatUnitObservation>();
        BossAuraTag = bossAuraTag ?? string.Empty;
        BossUtilityTag = bossUtilityTag ?? string.Empty;
    }

    public bool IsAvailable { get; }
    public string DifficultyBand { get; }
    public int ThreatSkulls { get; }
    public IReadOnlyList<EnemyThreatUnitObservation> Units { get; }
    public string BossAuraTag { get; }
    public string BossUtilityTag { get; }

    public static EnemyThreatObservation FromVisiblePreview(HeadlessEnemyPreview preview)
    {
        if (preview == null)
        {
            throw new ArgumentNullException(nameof(preview));
        }

        return new EnemyThreatObservation(
            preview.IsAvailable,
            preview.DifficultyBand,
            preview.ThreatSkulls,
            preview.Units.Select((unit, index) => new EnemyThreatUnitObservation(
                index,
                unit.ArchetypeId,
                unit.RaceId,
                unit.ClassId,
                unit.RoleTag,
                unit.PreferredAnchor)).ToArray(),
            preview.BossAuraTag,
            preview.BossUtilityTag);
    }
}

public sealed class EnemyThreatUnitObservation
{
    public EnemyThreatUnitObservation(
        int visibleIndex,
        string archetypeTag,
        string raceTag,
        string classTag,
        string roleTag,
        DeploymentAnchorId preferredAnchor)
    {
        VisibleIndex = visibleIndex;
        ArchetypeTag = archetypeTag ?? string.Empty;
        RaceTag = raceTag ?? string.Empty;
        ClassTag = classTag ?? string.Empty;
        RoleTag = roleTag ?? string.Empty;
        PreferredAnchor = preferredAnchor;
    }

    public int VisibleIndex { get; }
    public string ArchetypeTag { get; }
    public string RaceTag { get; }
    public string ClassTag { get; }
    public string RoleTag { get; }
    public DeploymentAnchorId PreferredAnchor { get; }
}

public sealed class EnemyThreatFinding
{
    public EnemyThreatFinding(string tag, IReadOnlyList<string> evidenceSignalKeys)
    {
        Tag = tag;
        EvidenceSignalKeys = evidenceSignalKeys ?? Array.Empty<string>();
    }

    public string Tag { get; }
    public IReadOnlyList<string> EvidenceSignalKeys { get; }
}

public sealed class EnemyThreatProfile
{
    public EnemyThreatProfile(IReadOnlyList<EnemyThreatFinding> findings)
    {
        Findings = findings ?? Array.Empty<EnemyThreatFinding>();
    }

    public IReadOnlyList<EnemyThreatFinding> Findings { get; }
    public IReadOnlyList<string> Tags => Findings.Select(value => value.Tag).ToArray();
}

/// <summary>공개 role/anchor/난이도/aura 문자열을 안정적인 위협 어휘로만 투영한다.</summary>
public static class EnemyThreatProfileParser
{
    public static EnemyThreatProfile Parse(EnemyThreatObservation observation)
    {
        if (observation == null)
        {
            throw new ArgumentNullException(nameof(observation));
        }

        if (!observation.IsAvailable)
        {
            return new EnemyThreatProfile(Array.Empty<EnemyThreatFinding>());
        }

        var findings = new List<EnemyThreatFinding>();
        var backline = observation.Units.Where(IsBacklineFirepower).ToArray();
        if (backline.Length >= 2)
        {
            findings.Add(Finding(EnemyThreatTag.BacklineFirepower, backline));
        }

        var divers = observation.Units.Where(unit => ContainsAny(unit.ClassTag, "duelist")).ToArray();
        if (divers.Length > 0
            && string.Equals(
                observation.BossUtilityTag,
                "boss_utility_backline_dive",
                StringComparison.Ordinal))
        {
            findings.Add(Finding(EnemyThreatTag.BacklineDive, divers));
        }

        if (string.Equals(
                observation.BossUtilityTag,
                "boss_utility_anticluster_bombardment",
                StringComparison.Ordinal))
        {
            findings.Add(AggregateFinding(EnemyThreatTag.AntiClusterAoe));
        }

        var sustain = observation.Units.Where(IsSustainSource).ToArray();
        if (sustain.Length > 0 || ContainsAny(observation.BossAuraTag, "heal", "regen", "sustain", "recovery")
            || ContainsAny(observation.BossUtilityTag, "heal", "regen", "sustain", "recovery"))
        {
            findings.Add(Finding(EnemyThreatTag.SustainEngine, sustain));
        }

        var wall = observation.Units.Where(IsFrontlineWall).ToArray();
        if (wall.Length >= 2 || ContainsAny(observation.BossAuraTag, "guard", "shield", "barrier", "bulwark", "fortress")
            || ContainsAny(observation.BossUtilityTag, "guard", "shield", "barrier", "bulwark", "fortress"))
        {
            findings.Add(Finding(EnemyThreatTag.FrontlineWall, wall));
        }

        if (observation.ThreatSkulls >= 4
            || ContainsAny(observation.DifficultyBand, "lethal", "deadly", "boss", "extreme"))
        {
            findings.Add(AggregateFinding(EnemyThreatTag.HighPressure));
        }

        var controllers = observation.Units.Where(value => ContainsAny(
            value.RoleTag,
            "controller",
            "disruptor",
            "hexer")).ToArray();
        if (controllers.Length > 0
            || ContainsAny(observation.BossAuraTag, "control", "stun", "root", "snare", "silence")
            || ContainsAny(observation.BossUtilityTag, "control", "stun", "root", "snare", "silence"))
        {
            findings.Add(Finding(EnemyThreatTag.ControlPressure, controllers));
        }

        if (observation.Units.Count >= 5)
        {
            findings.Add(new EnemyThreatFinding(
                EnemyThreatTag.SwarmPressure,
                observation.Units.Select(value => HeadlessPolicyEvidence.EnemyUnitSignal(value.VisibleIndex)).ToArray()));
        }

        return new EnemyThreatProfile(findings
            .OrderBy(value => value.Tag, StringComparer.Ordinal)
            .ToArray());
    }

    private static EnemyThreatFinding Finding(
        string tag,
        IReadOnlyList<EnemyThreatUnitObservation> units)
    {
        var signals = units.Select(value => HeadlessPolicyEvidence.EnemyUnitSignal(value.VisibleIndex))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        if (signals.Count == 0)
        {
            signals.Add(HeadlessPolicyEvidence.EnemyPreviewSignal);
        }

        return new EnemyThreatFinding(tag, signals);
    }

    private static EnemyThreatFinding AggregateFinding(string tag)
        => new(tag, new[] { HeadlessPolicyEvidence.EnemyPreviewSignal });

    private static bool IsBacklineFirepower(EnemyThreatUnitObservation unit)
        => unit.PreferredAnchor.IsBackRow()
           && (ContainsAny(unit.ClassTag, "ranger", "mystic")
               || ContainsAny(unit.RoleTag, "carry", "marksman", "support", "healer", "artillery"));

    private static bool IsSustainSource(EnemyThreatUnitObservation unit)
        => ContainsAny(unit.ClassTag, "mystic")
           || ContainsAny(unit.RoleTag, "support", "healer", "medic", "priest");

    private static bool IsFrontlineWall(EnemyThreatUnitObservation unit)
        => unit.PreferredAnchor.IsFrontRow()
           && (ContainsAny(unit.ClassTag, "vanguard", "duelist")
               || ContainsAny(unit.RoleTag, "anchor", "bruiser", "tank", "guard"));

    private static bool ContainsAny(string value, params string[] tokens)
        => !string.IsNullOrWhiteSpace(value)
           && tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
