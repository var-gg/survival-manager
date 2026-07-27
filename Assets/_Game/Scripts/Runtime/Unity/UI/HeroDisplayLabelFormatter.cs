using System;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI;

/// <summary>
/// Hero instance ids are persistence identifiers, never player-facing labels.
/// This formatter keeps person and job resolution consistent across presenters.
/// </summary>
internal static class HeroDisplayLabelFormatter
{
    private const string MissingLabel = "—";

    internal static string ResolvePersonName(
        HeroInstanceRecord? hero,
        Func<string, string, string>? characterName)
    {
        if (hero == null)
        {
            return MissingLabel;
        }

        if (characterName != null
            && (!string.IsNullOrWhiteSpace(hero.CharacterId) || !string.IsNullOrWhiteSpace(hero.ArchetypeId)))
        {
            var resolved = characterName(hero.CharacterId, hero.ArchetypeId);
            if (IsPlayerFacing(resolved, hero))
            {
                return resolved.Trim();
            }
        }

        return IsPlayerFacing(hero.Name, hero)
            ? hero.Name.Trim()
            : MissingLabel;
    }

    /// <summary>
    /// Thumbnail-sized cards cannot hold person plus job, and an ellipsis on every card reads as
    /// missing data rather than as a deliberate abbreviation. A compact card therefore shows the
    /// bare given name: no job, and no hanja parenthetical. Both survive on the surfaces that have
    /// room for them (character sheet, settlement rows, sortie confirmation).
    /// </summary>
    internal static string ResolvePersonNameCompact(
        HeroInstanceRecord? hero,
        Func<string, string, string>? characterName)
    {
        var person = ResolvePersonName(hero, characterName);
        var parenthetical = person.IndexOf('(');
        if (parenthetical <= 0)
        {
            return person;
        }

        var stripped = person[..parenthetical].TrimEnd();
        return stripped.Length == 0 ? person : stripped;
    }

    internal static string ResolvePersonAndJob(
        HeroInstanceRecord? hero,
        Func<string, string, string>? characterName,
        Func<string, string>? archetypeName)
    {
        var person = ResolvePersonName(hero, characterName);
        if (hero == null || archetypeName == null || string.IsNullOrWhiteSpace(hero.ArchetypeId))
        {
            return person;
        }

        var job = archetypeName(hero.ArchetypeId);
        if (!IsPlayerFacing(job, hero))
        {
            return person;
        }

        var trimmedJob = job.Trim();
        return string.Equals(person, trimmedJob, StringComparison.Ordinal)
            ? person
            : $"{person} · {trimmedJob}";
    }

    private static bool IsPlayerFacing(string? value, HeroInstanceRecord hero)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return !trimmed.StartsWith("content.", StringComparison.Ordinal)
               && !trimmed.StartsWith("ui.", StringComparison.Ordinal)
               && !trimmed.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(trimmed, hero.HeroId, StringComparison.Ordinal)
               && !string.Equals(trimmed, hero.CharacterId, StringComparison.Ordinal)
               && !string.Equals(trimmed, hero.ArchetypeId, StringComparison.Ordinal);
    }
}
