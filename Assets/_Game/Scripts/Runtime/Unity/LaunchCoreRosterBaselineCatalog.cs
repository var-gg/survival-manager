using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Unity;

public sealed record LaunchCoreUnitBaseline(
    string ArchetypeId,
    string RaceId,
    string ClassId,
    string SignatureActiveId,
    string FlexActiveId,
    string SignaturePassiveId,
    string FlexPassiveId,
    string DefaultPassiveBoardId,
    TeamPostureType PreferredPosture,
    string RecommendedTeamTacticId,
    IReadOnlyList<string> ExpectedSynergyIds);

public sealed class LaunchCoreRosterBaselineCatalog
{
    private readonly ICombatContentLookup _lookup;
    private readonly FirstPlayableSliceDefinition? _slice;
    private readonly Dictionary<string, LaunchCoreUnitBaseline> _unitBaselines = new(StringComparer.Ordinal);

    public LaunchCoreRosterBaselineCatalog(ICombatContentLookup lookup)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _slice = lookup.GetFirstPlayableSlice();
    }

    public bool TryGetUnitBaseline(string archetypeId, out LaunchCoreUnitBaseline baseline)
    {
        if (_unitBaselines.TryGetValue(archetypeId, out baseline!))
        {
            return true;
        }

        if (!_lookup.TryGetArchetype(archetypeId, out var archetype))
        {
            baseline = null!;
            return false;
        }

        var raceId = archetype.Race?.Id ?? string.Empty;
        var classId = archetype.Class?.Id ?? string.Empty;
        var preferredPosture = ToRuntimePosture(archetype.PreferredTeamPosture);
        baseline = new LaunchCoreUnitBaseline(
            archetype.Id,
            raceId,
            classId,
            ResolveSignatureActiveId(archetype),
            ResolveFlexActiveId(archetype),
            ResolveSignaturePassiveId(archetype),
            ResolveFlexPassiveId(archetype),
            ResolveDefaultPassiveBoardId(classId),
            preferredPosture,
            ResolveRecommendedTeamTacticId(preferredPosture),
            BuildExpectedSynergyIds(raceId, classId));
        _unitBaselines[archetypeId] = baseline;
        return true;
    }

    public bool IsInFirstPlayableSlice(string archetypeId)
    {
        return _slice?.UnitBlueprintIds.Contains(archetypeId, StringComparer.Ordinal) == true;
    }

    public IReadOnlyList<string> BuildExpectedSynergyIds(string raceId, string classId)
    {
        return new[]
            {
                BuildSynergyId(raceId),
                BuildSynergyId(classId),
            }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public (int Minor, int Major) ResolveSynergyThresholds(string synergyId, bool isClassFamily)
    {
        var entry = _slice?.SynergyGrammar?
            .FirstOrDefault(candidate => string.Equals(candidate.FamilyId, synergyId, StringComparison.Ordinal));
        if (entry != null)
        {
            return (Math.Max(1, entry.MinorThreshold), Math.Max(entry.MinorThreshold, entry.MajorThreshold));
        }

        return isClassFamily ? (2, 3) : (2, 4);
    }

    public static string ResolveRecommendedTeamTacticId(TeamPostureType posture)
    {
        return posture switch
        {
            TeamPostureType.HoldLine => "team_tactic_hold_line",
            TeamPostureType.ProtectCarry => "team_tactic_protect_carry",
            TeamPostureType.CollapseWeakSide => "team_tactic_collapse_weak_side",
            TeamPostureType.AllInBackline => "team_tactic_all_in_backline",
            _ => "team_tactic_standard_advance",
        };
    }

    private string ResolveDefaultPassiveBoardId(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId))
        {
            return string.Empty;
        }

        var boardId = $"board_{classId}";
        return _lookup.TryGetPassiveBoardDefinition(boardId, out _)
            ? boardId
            : string.Empty;
    }

    private static string ResolveSignatureActiveId(UnitArchetypeDefinition archetype)
    {
        return FirstNonEmpty(
            archetype.Loadout?.SignatureActive?.Id,
            archetype.LockedSignatureActiveSkill?.Id);
    }

    private static string ResolveFlexActiveId(UnitArchetypeDefinition archetype)
    {
        return FirstNonEmpty(
            archetype.Loadout?.FlexActive?.Id,
            archetype.RecruitFlexActivePool?.FirstOrDefault()?.Id,
            archetype.FlexUtilitySkillPool?.FirstOrDefault()?.Id);
    }

    private static string ResolveSignaturePassiveId(UnitArchetypeDefinition archetype)
    {
        return FirstNonEmpty(
            archetype.Loadout?.SignaturePassive?.Id,
            archetype.LockedSignaturePassiveSkill?.Id);
    }

    private static string ResolveFlexPassiveId(UnitArchetypeDefinition archetype)
    {
        return FirstNonEmpty(
            archetype.Loadout?.FlexPassive?.Id,
            archetype.RecruitFlexPassivePool?.FirstOrDefault()?.Id,
            archetype.FlexSupportSkillPool?.FirstOrDefault()?.Id);
    }

    private static string BuildSynergyId(string familyId)
    {
        return string.IsNullOrWhiteSpace(familyId)
            ? string.Empty
            : $"synergy_{familyId}";
    }

    private static TeamPostureType ToRuntimePosture(TeamPostureTypeValue posture)
    {
        return (TeamPostureType)(int)posture;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
