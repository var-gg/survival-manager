using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>옵션별 legal host build와 census placement를 같은 seed pair case로 고정한다.</summary>
internal static class H100OptionTrapCaseFactory
{
    public static IReadOnlyList<H100BattleScreeningCase> Build(
        OptionWitnessContract contract,
        BuildGrammarTruthSource source,
        CombatContentSnapshot snapshot,
        BuildSpaceCensus census,
        H100OptionTrapRunSettings settings,
        bool fullCensus)
    {
        var build = SelectHostBuild(contract, snapshot, census.Combinations);
        var placements = fullCensus
            ? census.Formations.OrderBy(value => value.Signature, StringComparer.Ordinal).ToArray()
            : census.Medoids.OrderBy(value => value.Placement.Signature, StringComparer.Ordinal)
                .Take(settings.MedoidCount)
                .Select(value => value.Placement)
                .ToArray();
        var cases = new List<H100BattleScreeningCase>(placements.Length * settings.SeedCount);
        for (var placementIndex = 0; placementIndex < placements.Length; placementIndex++)
        {
            var placement = placements[placementIndex];
            for (var seedIndex = 0; seedIndex < settings.SeedCount; seedIndex++)
            {
                var seed = H100SessionDriver.DeriveSeed(
                    $"option-trap|{contract.OptionId}|{build.BuildId}|{placement.Signature}",
                    settings.SeedBase + seedIndex);
                var members = build.FormationMembers.Select((member, memberIndex) => new H100BattleScreeningMember(
                    member.ArchetypeId,
                    placement.AnchorsByMemberIndex[memberIndex])).ToArray();
                cases.Add(new H100BattleScreeningCase(
                    $"{(fullCensus ? "full" : "screen")}|{contract.OptionId}|p{placementIndex:D3}|s{seedIndex:D2}",
                    build.BuildId,
                    placement.Signature,
                    seed,
                    members));
            }
        }

        return cases;
    }

    private static BuildCombination SelectHostBuild(
        OptionWitnessContract contract,
        CombatContentSnapshot snapshot,
        IReadOnlyList<BuildCombination> builds)
    {
        var skillHosts = contract.SubjectKind == BuildGrammarSubjectKind.Skill
            ? snapshot.Archetypes.Values
                .Where(archetype => EnumerateSkills(archetype).Any(skill => skill.Id == contract.SubjectId))
                .Select(archetype => archetype.Id)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var candidates = builds.Where(build => skillHosts.Count == 0
                                               || build.Members.Any(member => skillHosts.Contains(member.ArchetypeId)))
            .ToArray();
        return candidates
            .OrderByDescending(build => build.Roles.IsRoleComplete)
            .ThenByDescending(build => build.Synergy.ClassTier3Count + build.Synergy.RaceTier4Count)
            .ThenBy(build => build.BuildId, StringComparer.Ordinal)
            .First();
    }

    private static IEnumerable<SM.Combat.Model.BattleSkillSpec> EnumerateSkills(CombatArchetypeTemplate archetype)
    {
        var skills = (archetype.Skills ?? Array.Empty<SM.Combat.Model.BattleSkillSpec>())
            .Concat(archetype.RecruitFlexActivePool ?? Array.Empty<SM.Combat.Model.BattleSkillSpec>())
            .Concat(archetype.RecruitFlexPassivePool ?? Array.Empty<SM.Combat.Model.BattleSkillSpec>());
        if (archetype.SignatureActive != null) skills = skills.Append(archetype.SignatureActive);
        if (archetype.FlexActive != null) skills = skills.Append(archetype.FlexActive);
        return skills;
    }
}
