using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Unity.UI.Expedition;

internal sealed record ExpeditionEncounterPreview(
    string EncounterId,
    string EncounterName,
    EncounterKindValue Kind,
    int ThreatSkulls,
    string DifficultyBand,
    string FactionId,
    string BossOverlayId,
    string BossOverlayName,
    string BossAuraTag,
    string BossUtilityTag,
    IReadOnlyList<string> EnemyNames,
    IReadOnlyList<string> RewardDropTags);

internal static class ExpeditionEncounterPreviewBuilder
{
    public static bool TryBuild(
        CombatContentSnapshot snapshot,
        ExpeditionNodeViewModel node,
        Func<string, string, string> resolveCharacterName,
        Func<string, string> resolveArchetypeName,
        out ExpeditionEncounterPreview preview,
        Func<string, string>? resolveEncounterName = null)
    {
        preview = null!;
        if (!node.RequiresBattle
            || snapshot.Encounters is not { } encounters
            || !encounters.TryGetValue(node.Id, out var encounter))
        {
            return false;
        }

        IEnumerable<EnemySquadMemberTemplate> squadMembers = Array.Empty<EnemySquadMemberTemplate>();
        IEnumerable<string> squadRewardTags = Array.Empty<string>();
        if (snapshot.EnemySquads is { } squads
            && squads.TryGetValue(encounter.EnemySquadTemplateId, out var squad))
        {
            squadMembers = squad.Members ?? Array.Empty<EnemySquadMemberTemplate>();
            squadRewardTags = squad.RewardDropTags ?? Array.Empty<string>();
        }

        var hasOverlay = TryResolveBossOverlay(snapshot, encounter, out var overlay);
        var overlayRewardTags = hasOverlay
            ? overlay.RewardDropTags ?? Array.Empty<string>()
            : Array.Empty<string>();
        var enemyNames = squadMembers
            .Select(member => FormatMemberName(member, resolveCharacterName, resolveArchetypeName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();

        preview = new ExpeditionEncounterPreview(
            encounter.Id,
            ResolveDisplayName(encounter.Id, encounter.Name, resolveEncounterName),
            encounter.Kind,
            Math.Max(1, encounter.ThreatSkulls),
            encounter.DifficultyBand,
            encounter.FactionId,
            encounter.BossOverlayId,
            hasOverlay ? ResolveDisplayName(overlay.Id, overlay.Name, null) : string.Empty,
            hasOverlay ? overlay.SignatureAuraTag : string.Empty,
            hasOverlay ? overlay.SignatureUtilityTag : string.Empty,
            enemyNames,
            MergeRewardTags(encounter, squadRewardTags, overlayRewardTags));
        return true;
    }

    private static bool TryResolveBossOverlay(
        CombatContentSnapshot snapshot,
        EncounterTemplate encounter,
        out BossOverlayTemplate overlay)
    {
        overlay = null!;
        if (string.IsNullOrWhiteSpace(encounter.BossOverlayId)
            || snapshot.BossOverlays is not { } overlays
            || !overlays.TryGetValue(encounter.BossOverlayId, out overlay))
        {
            return false;
        }

        return true;
    }

    private static string FormatMemberName(
        EnemySquadMemberTemplate member,
        Func<string, string, string> resolveCharacterName,
        Func<string, string> resolveArchetypeName)
    {
        var archetypeName = resolveArchetypeName(member.ArchetypeId);
        var characterName = !string.IsNullOrWhiteSpace(member.CharacterId)
            ? resolveCharacterName(member.CharacterId, member.ArchetypeId)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(characterName) || string.Equals(characterName, member.CharacterId, StringComparison.Ordinal))
        {
            characterName = !string.IsNullOrWhiteSpace(member.Name)
                ? member.Name
                : archetypeName;
        }

        var displayName = string.IsNullOrWhiteSpace(archetypeName)
                          || string.Equals(characterName, archetypeName, StringComparison.Ordinal)
            ? characterName
            : $"{characterName} ({archetypeName})";
        return member.Role == EnemySquadMemberRoleValue.Unit
            ? displayName
            : $"{BattleReadabilityFormatter.HumanizeToken(member.Role.ToString(), member.Role.ToString())}: {displayName}";
    }

    private static string ResolveDisplayName(string id, string rawName, Func<string, string>? resolver)
    {
        var resolved = resolver?.Invoke(id);
        if (!LooksPlayerFacing(resolved))
        {
            resolved = rawName;
        }

        return LooksPlayerFacing(resolved)
            ? resolved!
            : BattleReadabilityFormatter.HumanizeToken(id, id);
    }

    private static bool LooksPlayerFacing(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return !trimmed.StartsWith("content.", StringComparison.Ordinal)
               && !trimmed.StartsWith("ui.", StringComparison.Ordinal)
               && !trimmed.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> MergeRewardTags(
        EncounterTemplate encounter,
        IEnumerable<string> squadRewardTags,
        IEnumerable<string> overlayRewardTags)
    {
        return (encounter.RewardDropTags ?? Array.Empty<string>())
            .Concat(squadRewardTags)
            .Concat(overlayRewardTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();
    }
}
