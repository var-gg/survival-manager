using System;
using System.Collections.Generic;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>authored archetype+anchor case를 실제 GameSessionState로 조립하는 Editor 전용 factory.</summary>
internal static class H100ScreeningSessionFactory
{
    public static GameSessionState Create(
        RuntimeCombatContentLookup lookup,
        string caseId,
        IReadOnlyList<H100BattleScreeningMember> members,
        string identityPrefix = "census")
    {
        var profile = new SaveProfile
        {
            ProfileId = $"h100-{identityPrefix}-{caseId}",
            Heroes = new List<HeroInstanceRecord>(),
        };
        var heroIds = new List<string>(members.Count);
        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            if (!lookup.TryGetArchetype(member.ArchetypeId, out var archetype))
            {
                throw new InvalidOperationException($"Screening archetype is unavailable: {member.ArchetypeId}");
            }

            var heroId = $"{identityPrefix}-{index:D2}-{member.ArchetypeId}";
            heroIds.Add(heroId);
            profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = heroId,
                Name = member.ArchetypeId,
                ArchetypeId = member.ArchetypeId,
                RaceId = archetype.Race.Id,
                ClassId = archetype.Class.Id,
                FlexActiveId = archetype.Loadout?.FlexActive?.Id ?? string.Empty,
                FlexPassiveId = archetype.Loadout?.FlexPassive?.Id ?? string.Empty,
                RecruitTier = archetype.RecruitTier,
            });
        }

        var session = H100SessionDriver.CreateSession(lookup, profile);
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        for (var index = 0; index < members.Count; index++)
        {
            if (!session.AssignHeroToAnchor(members[index].Anchor, heroIds[index]))
            {
                throw new InvalidOperationException(
                    $"Could not apply screening placement: {heroIds[index]}@{members[index].Anchor}");
            }
        }

        return session;
    }
}
