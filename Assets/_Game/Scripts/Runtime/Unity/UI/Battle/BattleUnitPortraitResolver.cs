using System;
using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.UI.Battle;

internal sealed class BattleUnitPortraitResolver
{
    private const string BasePath = "_Game/Art/Characters";
    private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.Ordinal);

    public Texture2D? Resolve(BattleUnitReadModel unit)
    {
        if (string.IsNullOrWhiteSpace(unit.CharacterId))
        {
            return null;
        }

        foreach (var faceId in EnumerateFaceIds(unit))
        {
            var texture = Load(unit.CharacterId, faceId);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    public Texture2D? ResolveSkillIcon(string characterId, string skillId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(skillId))
        {
            return null;
        }

        foreach (var iconId in EnumerateSkillIconIds(characterId, skillId))
        {
            var texture = LoadRaw(characterId, $"skill_icon_{iconId}");
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    private Texture2D? Load(string characterId, string faceId)
    {
        var key = $"{characterId}:{faceId}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = Resources.Load<Texture2D>($"{BasePath}/{characterId}/portrait_face_{faceId}");
        _cache[key] = texture;
        return texture;
    }

    private Texture2D? LoadRaw(string characterId, string assetStem)
    {
        var key = $"{characterId}:{assetStem}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = Resources.Load<Texture2D>($"{BasePath}/{characterId}/{assetStem}");
        _cache[key] = texture;
        return texture;
    }

    private static IEnumerable<string> EnumerateFaceIds(BattleUnitReadModel unit)
    {
        if (!unit.IsAlive || unit.CurrentHealth <= 0f)
        {
            yield return "downed";
        }

        var statusIds = unit.StatusIds ?? Array.Empty<string>();
        if (HasStatus(statusIds, "stun"))
        {
            yield return "stunned";
        }

        if (HasStatus(statusIds, "fear"))
        {
            yield return "feared";
        }

        if (HasStatus(statusIds, "charm"))
        {
            yield return "charmed";
        }

        if (HasStatus(statusIds, "burn") || HasStatus(statusIds, "poison") || HasStatus(statusIds, "bleed"))
        {
            yield return "pained";
        }

        if (HasStatus(statusIds, "silence") || HasStatus(statusIds, "quiet"))
        {
            yield return "quiet";
        }

        if (unit.MaxHealth > 0f && unit.CurrentHealth / unit.MaxHealth <= 0.3f)
        {
            yield return "wounded";
        }

        yield return "serious";
        yield return "default";
    }

    private static IEnumerable<string> EnumerateSkillIconIds(string characterId, string skillId)
    {
        if (string.Equals(characterId, "hero_dawn_priest", StringComparison.Ordinal))
        {
            if (string.Equals(skillId, "skill_priest_core", StringComparison.Ordinal))
            {
                yield return "sigil_shield";
            }
            else if (string.Equals(skillId, "skill_minor_heal", StringComparison.Ordinal))
            {
                yield return "platinum_aegis";
            }
            else if (string.Equals(skillId, "support_purifying", StringComparison.Ordinal))
            {
                yield return "ash_purification";
            }
        }

        yield return skillId;
        if (skillId.StartsWith("skill_", StringComparison.Ordinal))
        {
            yield return skillId["skill_".Length..];
        }
    }

    private static bool HasStatus(IEnumerable<string> statusIds, string token)
    {
        foreach (var statusId in statusIds)
        {
            if (statusId?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
