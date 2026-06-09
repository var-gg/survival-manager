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
        foreach (var characterId in EnumerateCharacterIds(unit))
        {
            foreach (var assetStem in EnumeratePortraitAssetStems(unit))
            {
                var texture = LoadRaw(characterId, assetStem);
                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    public Texture2D? ResolveFullBody(BattleUnitReadModel unit)
    {
        foreach (var characterId in EnumerateCharacterIds(unit))
        {
            foreach (var assetStem in EnumerateFullBodyAssetStems(unit))
            {
                var texture = LoadRaw(characterId, assetStem);
                if (texture != null)
                {
                    return texture;
                }
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

    private static IEnumerable<string> EnumerateCharacterIds(BattleUnitReadModel unit)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in EnumerateRawCharacterIdCandidates(unit))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> EnumerateRawCharacterIdCandidates(BattleUnitReadModel unit)
    {
        if (!string.IsNullOrWhiteSpace(unit.CharacterId))
        {
            yield return unit.CharacterId.Trim();
        }

        var token = $"{unit.ArchetypeId} {unit.Id} {unit.Name}".ToLowerInvariant();
        if (token.Contains("aegis", StringComparison.Ordinal) || token.Contains("sentinel", StringComparison.Ordinal) || token.Contains("guardian", StringComparison.Ordinal) || token.Contains("vanguard", StringComparison.Ordinal))
        {
            yield return "hero_aegis_sentinel";
        }
        if (token.Contains("수호", StringComparison.Ordinal))
        {
            yield return "hero_aegis_sentinel";
        }
        if (token.Contains("pale", StringComparison.Ordinal) || token.Contains("executor", StringComparison.Ordinal) || token.Contains("slayer", StringComparison.Ordinal) || token.Contains("duelist", StringComparison.Ordinal))
        {
            yield return "hero_pale_executor";
        }
        if (token.Contains("학살", StringComparison.Ordinal))
        {
            yield return "hero_pale_executor";
        }
        if (token.Contains("longshot", StringComparison.Ordinal) || token.Contains("hunter", StringComparison.Ordinal) || token.Contains("ranger", StringComparison.Ordinal))
        {
            yield return "hero_longshot_hunter";
        }
        if (token.Contains("prism", StringComparison.Ordinal) || token.Contains("seer", StringComparison.Ordinal) || token.Contains("mystic", StringComparison.Ordinal))
        {
            yield return "hero_prism_seeker";
        }
        if (token.Contains("grave", StringComparison.Ordinal) || token.Contains("reaver", StringComparison.Ordinal) || token.Contains("raider", StringComparison.Ordinal))
        {
            yield return "hero_grave_reaver";
        }
        if (token.Contains("악탈", StringComparison.Ordinal))
        {
            yield return "hero_grave_reaver";
        }
        if (token.Contains("echo", StringComparison.Ordinal) || token.Contains("savant", StringComparison.Ordinal))
        {
            yield return "hero_echo_savant";
        }
        if (token.Contains("gate", StringComparison.Ordinal) || token.Contains("파수", StringComparison.Ordinal))
        {
            // 게이트 파수꾼 보스(적). 플레이어 'warden' 아키타입과 섞이지 않도록 gate/파수 토큰만 매칭.
            yield return "boss_gate_warden";
        }
        else if (token.Contains("warden", StringComparison.Ordinal) || token.Contains("감시", StringComparison.Ordinal))
        {
            // 플레이어 '감시자(warden)' 아키타입 → 실제 hero_iron_warden 페이스 아트.
            // (이전엔 boss_gate_warden으로 잘못 매핑돼 face 자산이 없어 빈 카드로 렌더됨)
            yield return "hero_iron_warden";
        }
        if (token.Contains("root", StringComparison.Ordinal) || token.Contains("watcher", StringComparison.Ordinal))
        {
            yield return "boss_root_watcher";
        }
        if (token.Contains("glass", StringComparison.Ordinal) || token.Contains("conduit", StringComparison.Ordinal))
        {
            yield return "boss_glass_conduit";
        }
        if (token.Contains("menagerie", StringComparison.Ordinal) || token.Contains("overseer", StringComparison.Ordinal))
        {
            yield return "boss_menagerie_overseer";
        }
        if (token.Contains("scribe", StringComparison.Ordinal) || token.Contains("sigil", StringComparison.Ordinal) || token.Contains("서기", StringComparison.Ordinal))
        {
            yield return "npc_archive_scribe";
        }
        if (token.Contains("border", StringComparison.Ordinal) || token.Contains("irregular", StringComparison.Ordinal) || token.Contains("carrier", StringComparison.Ordinal) || token.Contains("창병", StringComparison.Ordinal) || token.Contains("운반자", StringComparison.Ordinal))
        {
            yield return "npc_galma_mercenary_post";
        }
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
