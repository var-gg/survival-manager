using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Unity
{

internal static class BattleP09AppearanceRoster
{
    // Runtime fallback keeps archetype aliases because authored content can still
    // resolve visuals by ArchetypeId. Studio authoring uses AuthoringCharacterIds.
    private static readonly string[] CoreCharacterIds =
    {
        "warden",
        "guardian",
        "bulwark",
        "slayer",
        "raider",
        "reaver",
        "hunter",
        "scout",
        "marksman",
        "priest",
        "hexer",
        "shaman",
        "rift_stalker",
        "bastion_penitent",
        "pale_executor",
        "mirror_cantor",
        "hero_dawn_priest",
        "hero_pack_raider",
        "hero_grave_hexer",
        "hero_echo_savant",
        "npc_lyra_sternfeld",
        "npc_grey_fang",
        "npc_black_vellum",
        "npc_silent_moon",
        "npc_baekgyu_sternheim",
        "hero_aegis_sentinel",
        "hero_shardblade",
        "hero_prism_seeker",
        "hero_ember_runner",
        "hero_iron_pelt",
        "npc_aldric",
    };

    // Extra actors are encounter-visible P09 variants. They are not main story
    // speakers by default, but they still need names and visual slots.
    private static readonly string[] ExtraActorCharacterIds =
    {
        "extra_kojin_gate_warden",
        "extra_solarum_border_lancer",
        "extra_solarum_sigil_scribe",
        "extra_border_reliquary_carry",
        "extra_wolfpine_outrider",
        "extra_wolfpine_ember_runner_cell",
        "extra_grey_fang_vanguard",
        "extra_bastion_line_guard",
        "extra_bastion_reliquary_guard",
        "extra_sunken_adjudicator_lieutenant",
        "extra_sunken_bastion_adjudicator",
        "extra_tithe_mark_bearer",
        "extra_tithe_chain_cantor",
        "extra_tithe_executioner_proxy",
        "extra_tithe_inquisitor_pureflame",
        "extra_pale_memorial_keeper",
        "extra_pale_tomb_sentinel",
        "extra_black_roll_bailiff",
        "extra_crypt_list_keeper",
        "extra_lattice_root_usher",
        "extra_lattice_echo_caretaker",
        "extra_bone_orchard_watcher",
        "extra_glass_field_cleric",
        "extra_glass_shard_bailiff",
        "extra_glass_forest_recordkeeper",
        "extra_menagerie_snare_runner",
        "extra_sample_b17_survivor",
        "extra_menagerie_keeper",
        "extra_heartforge_gate_guard",
        "extra_record_rights_marker",
        "extra_heartforge_gate_warden",
        "extra_worldscar_archive_cell",
        "extra_worldscar_rite_echo",
        "extra_worldscar_record_bailiff",
    };

    private static readonly string[] CharacterIds = CoreCharacterIds
        .Concat(ExtraActorCharacterIds)
        .ToArray();

    private static readonly string[] CoreAuthoringCharacterIds =
    {
        "hero_dawn_priest",
        "hero_pack_raider",
        "hero_grave_hexer",
        "hero_echo_savant",
        "npc_lyra_sternfeld",
        "npc_grey_fang",
        "npc_black_vellum",
        "npc_silent_moon",
        "npc_baekgyu_sternheim",
        "warden",
        "guardian",
        "bulwark",
        "slayer",
        "reaver",
        "hunter",
        "scout",
        "marksman",
        "shaman",
        "rift_stalker",
        "bastion_penitent",
        "pale_executor",
        "mirror_cantor",
        "hero_aegis_sentinel",
        "hero_shardblade",
        "hero_prism_seeker",
        "hero_ember_runner",
        "hero_iron_pelt",
        "npc_aldric",
    };

    private static readonly string[] AuthoringCharacterIds = CoreAuthoringCharacterIds
        .Concat(ExtraActorCharacterIds)
        .ToArray();

    private static readonly HashSet<string> AuthoringHiddenAliases = new(StringComparer.Ordinal)
    {
        "priest",
        "raider",
        "hexer",
    };

    private static readonly Dictionary<string, int> CharacterOrder = BuildCharacterOrder(CharacterIds);
    private static readonly Dictionary<string, int> AuthoringCharacterOrder = BuildCharacterOrder(AuthoringCharacterIds);
    private static readonly Dictionary<string, string> ConfirmedDisplayNames = new(StringComparer.Ordinal)
    {
        ["warden"] = "철위 (鐵衛) / Iron Warden",
        ["guardian"] = "묘직 (墓直) / Crypt Guardian",
        ["bulwark"] = "송곳벽 / Fang Bulwark",
        ["slayer"] = "서검 (誓劍) / Oath Slayer",
        ["raider"] = "이빨바람 / Pack Raider",
        ["reaver"] = "묵괴 (墨壞) / Grave Reaver",
        ["hunter"] = "원시 (遠矢) / Longshot Hunter",
        ["scout"] = "숲살이 / Trail Scout",
        ["marksman"] = "냉시 (冷矢) / Dread Marksman",
        ["priest"] = "단린 (丹麟) / Dawn Priest",
        ["hexer"] = "묵향 (墨香) / Grave Hexer",
        ["shaman"] = "풍의 (風儀) / Storm Shaman",
        ["rift_stalker"] = "틈사냥꾼 / Rift Stalker",
        ["bastion_penitent"] = "참회벽 / Bastion Penitent",
        ["pale_executor"] = "백집행 (白執行) / Pale Executor",
        ["mirror_cantor"] = "명음 (明音) / Mirror Cantor",
        ["hero_dawn_priest"] = "단린 (丹麟) / Dawn Priest",
        ["hero_pack_raider"] = "이빨바람 / Pack Raider",
        ["hero_grave_hexer"] = "묵향 (墨香) / Grave Hexer",
        ["hero_echo_savant"] = "공한 (空閑) / Echo Savant",
        ["npc_lyra_sternfeld"] = "선영 (宣英) / Lyra Sternfeld",
        ["npc_grey_fang"] = "회조 (灰爪) / Grey Fang",
        ["npc_black_vellum"] = "흑지 (黑紙) / Black Vellum",
        ["npc_silent_moon"] = "침월 (沉月) / Silent Moon",
        ["npc_baekgyu_sternheim"] = "백규 (白圭) / Baekgyu Sternheim",
        ["hero_aegis_sentinel"] = "방진 (方陣) / Aegis Sentinel",
        ["hero_shardblade"] = "편검 (片劍) / Shardblade",
        ["hero_prism_seeker"] = "광로 (光路) / Prism Seeker",
        ["hero_ember_runner"] = "연주 (燕走) / Ember Runner",
        ["hero_iron_pelt"] = "철피 (鐵皮) / Iron Pelt",
        ["npc_aldric"] = "단현 스턴홀트 (丹玄) / Aldric Sternfeld",
        ["extra_kojin_gate_warden"] = "고진 관문 파수관 / Kojin Gate Warden",
        ["extra_solarum_border_lancer"] = "솔라룸 국경 창병 / Solarium Border Lancer",
        ["extra_solarum_sigil_scribe"] = "솔라룸 각인 서기 / Solarium Sigil Scribe",
        ["extra_border_reliquary_carry"] = "국경 성물 운반자 / Border Reliquary Carrier",
        ["extra_wolfpine_outrider"] = "늑대소나무 척후기수 / Wolfpine Outrider",
        ["extra_wolfpine_ember_runner_cell"] = "늑대소나무 불씨전령조 / Wolfpine Ember Runner Cell",
        ["extra_grey_fang_vanguard"] = "회조 선봉대 / Grey Fang Vanguard",
        ["extra_bastion_line_guard"] = "보루 전열 경비 / Bastion Line Guard",
        ["extra_bastion_reliquary_guard"] = "보루 성물고 경비 / Bastion Reliquary Guard",
        ["extra_sunken_adjudicator_lieutenant"] = "침몰 심판 부관 / Sunken Adjudicator Lieutenant",
        ["extra_sunken_bastion_adjudicator"] = "침몰 보루 심판관 / Sunken Bastion Adjudicator",
        ["extra_tithe_mark_bearer"] = "십일조 표식 운반자 / Tithe Mark Bearer",
        ["extra_tithe_chain_cantor"] = "십일조 사슬 성창가 / Tithe Chain Cantor",
        ["extra_tithe_executioner_proxy"] = "십일조 집행 대리인 / Tithe Executioner Proxy",
        ["extra_tithe_inquisitor_pureflame"] = "십일조 순화 심문관 / Tithe Pureflame Inquisitor",
        ["extra_pale_memorial_keeper"] = "창백 추모지기 / Pale Memorial Keeper",
        ["extra_pale_tomb_sentinel"] = "창백 묘지 파수병 / Pale Tomb Sentinel",
        ["extra_black_roll_bailiff"] = "흑부 집달관 / Black Roll Bailiff",
        ["extra_crypt_list_keeper"] = "묘실 명부지기 / Crypt List Keeper",
        ["extra_lattice_root_usher"] = "격자뿌리 인도자 / Lattice Root Usher",
        ["extra_lattice_echo_caretaker"] = "격자 메아리 관리인 / Lattice Echo Caretaker",
        ["extra_bone_orchard_watcher"] = "뼈 과수원 감시자 / Bone Orchard Watcher",
        ["extra_glass_field_cleric"] = "유리 들판 성직자 / Glass Field Cleric",
        ["extra_glass_shard_bailiff"] = "유리 파편 집달관 / Glass Shard Bailiff",
        ["extra_glass_forest_recordkeeper"] = "유리숲 기록관 / Glass Forest Recordkeeper",
        ["extra_menagerie_snare_runner"] = "우리 덫 주자 / Menagerie Snare Runner",
        ["extra_sample_b17_survivor"] = "표본 B17 생존자 / Sample B17 Survivor",
        ["extra_menagerie_keeper"] = "우리 관리인 / Menagerie Keeper",
        ["extra_heartforge_gate_guard"] = "심장단조 관문 경비 / Heartforge Gate Guard",
        ["extra_record_rights_marker"] = "기록권 표식자 / Record Rights Marker",
        ["extra_heartforge_gate_warden"] = "심장단조 관문 파수관 / Heartforge Gate Warden",
        ["extra_worldscar_archive_cell"] = "세계상처 기록 감방 / Worldscar Archive Cell",
        ["extra_worldscar_rite_echo"] = "세계상처 의례 메아리 / Worldscar Rite Echo",
        ["extra_worldscar_record_bailiff"] = "세계상처 기록 집달관 / Worldscar Record Bailiff",
    };

    public static IReadOnlyList<string> CanonicalCharacterIds => CharacterIds;

    public static int GetSortOrder(string characterId)
    {
        return CharacterOrder.TryGetValue(characterId ?? string.Empty, out var order)
            ? order
            : int.MaxValue;
    }

    public static int GetAuthoringSortOrder(string characterId)
    {
        return AuthoringCharacterOrder.TryGetValue(characterId ?? string.Empty, out var order)
            ? order
            : GetSortOrder(characterId);
    }

    public static bool TryGetSlotId(string characterId, out string slotId)
    {
        if (CharacterOrder.TryGetValue(characterId ?? string.Empty, out var order))
        {
            slotId = FormatSlotId(order);
            return true;
        }

        slotId = string.Empty;
        return false;
    }

    public static bool HasDefinedLabel(string characterId)
    {
        return TryGetDefinedDisplayName(characterId, out _);
    }

    public static bool ShouldShowInAuthoring(string characterId)
    {
        return !AuthoringHiddenAliases.Contains(characterId ?? string.Empty)
            && TryGetDefinedDisplayName(characterId, out _);
    }

    public static string BuildAuthoringLabel(string characterId, string displayName)
    {
        var resolvedDisplayName = TryGetDefinedDisplayName(characterId, out var definedDisplayName)
            ? definedDisplayName
            : string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;

        if (TryGetAuthoringSlotId(characterId, out var slotId)
            || TryGetSlotId(characterId, out slotId))
        {
            return $"{slotId} · {resolvedDisplayName}";
        }

        return $"{resolvedDisplayName} · {characterId}";
    }

    public static string BuildPresetDisplayName(string characterId, string displayName)
    {
        if (TryGetDefinedDisplayName(characterId, out var confirmedDisplayName))
        {
            return confirmedDisplayName;
        }

        return string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;
    }

    public static bool TryGetDefinedDisplayName(string characterId, out string displayName)
    {
        return ConfirmedDisplayNames.TryGetValue(characterId ?? string.Empty, out displayName);
    }

    private static string FormatSlotId(int zeroBasedOrder)
    {
        return $"chr_{zeroBasedOrder + 1:0000}";
    }

    private static bool TryGetAuthoringSlotId(string characterId, out string slotId)
    {
        if (AuthoringCharacterOrder.TryGetValue(characterId ?? string.Empty, out var order))
        {
            slotId = FormatSlotId(order);
            return true;
        }

        slotId = string.Empty;
        return false;
    }

    private static Dictionary<string, int> BuildCharacterOrder(IReadOnlyList<string> characterIds)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < characterIds.Count; i++)
        {
            result[characterIds[i]] = i;
        }

        return result;
    }
}

}
