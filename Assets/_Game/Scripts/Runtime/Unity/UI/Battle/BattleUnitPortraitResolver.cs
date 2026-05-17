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

        foreach (var assetStem in EnumeratePortraitAssetStems(unit))
        {
            var texture = LoadRaw(unit.CharacterId, assetStem);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    public Texture2D? ResolveFullBody(BattleUnitReadModel unit)
    {
        if (string.IsNullOrWhiteSpace(unit.CharacterId))
        {
            return null;
        }

        foreach (var assetStem in EnumerateFullBodyAssetStems(unit))
        {
            var texture = LoadRaw(unit.CharacterId, assetStem);
            if (texture != null)
            {
                return texture;
            }
        }

        return Resolve(unit);
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

    private static IEnumerable<string> EnumeratePortraitAssetStems(BattleUnitReadModel unit)
    {
        foreach (var faceId in EnumerateFaceIds(unit))
        {
            yield return $"portrait_face_{faceId}";
        }

        yield return "portrait_stance_idle";
        yield return "portrait_full";
    }

    private static IEnumerable<string> EnumerateFullBodyAssetStems(BattleUnitReadModel unit)
    {
        if (!unit.IsAlive || unit.CurrentHealth <= 0f)
        {
            yield return "portrait_stance_downed";
        }

        if (unit.WindupProgress > 0.01f)
        {
            yield return unit.PendingActionType == BattleActionType.ActiveSkill
                ? "portrait_stance_cast"
                : "portrait_stance_attack";
        }

        if (unit.IsDefending || HasStatus(unit.StatusIds ?? Array.Empty<string>(), "guard"))
        {
            yield return "portrait_stance_guard";
        }

        yield return "portrait_full";
        yield return "portrait_full_body";
        yield return "portrait_stance_idle";
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
