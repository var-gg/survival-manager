using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Unity;

internal enum ExtraActorExposureTier
{
    ExtraActor = 0,
    BossActor = 1,
}

internal enum ExtraActorSpawnPolicy
{
    SiteLocked = 0,
    SiteLocalPool = 1,
    ChapterPool = 2,
    PostClearPool = 3,
    EventOnly = 4,
}

internal enum ExtraActorIllustrationTier
{
    ExtraCard = 0,
    BossCard = 1,
    PromotableCard = 2,
}

internal sealed record ExtraActorCharacterProfile(
    string ActorId,
    string DisplayName,
    ExtraActorExposureTier ExposureTier,
    string ChapterId,
    string SiteId,
    ExtraActorSpawnPolicy FirstClearSpawnPolicy,
    string StorySafety,
    string FactionId,
    string CombatArchetypeId,
    string P09BasePresetId,
    string ModelArchetype,
    ExtraActorIllustrationTier IllustrationTier,
    string BarkSetId,
    string DossierHook,
    bool GachaEligible);

internal static class ExtraActorCharacterRegistry
{
    private static readonly ExtraActorCharacterProfile[] Entries =
    {
        Boss("extra_kojin_gate_warden", "고진 관문 파수관 / Kojin Gate Warden", "chapter_ashen_gate", "site_ashen_gate", "actual_enemy_unit", "faction_solarum", "warden", "warden", "gate_guard", "절차형 관문 수문장"),
        Extra("extra_solarum_border_lancer", "솔라룸 국경 창병 / Solarium Border Lancer", "chapter_ashen_gate", "site_ashen_gate", "actual_enemy_unit", "faction_solarum", "hunter", "hunter", "field_lancer", "변방문 전열의 창끝"),
        Extra("extra_solarum_sigil_scribe", "솔라룸 각인 서기 / Solarium Sigil Scribe", "chapter_ashen_gate", "site_ashen_gate", "actual_enemy_unit", "faction_solarum", "hexer", "hexer", "record_clerk", "파편 표식을 전장 명령으로 옮기는 서기"),
        Extra("extra_border_reliquary_carry", "국경 성물 운반자 / Border Reliquary Carrier", "chapter_ashen_gate", "site_ashen_gate", "actual_enemy_unit", "faction_solarum", "raider", "raider", "reliquary_carrier", "후열 성물을 지키는 운반자"),
        Extra("extra_wolfpine_outrider", "늑대소나무 척후기수 / Wolfpine Outrider", "chapter_ashen_gate", "site_wolfpine_trail", "actual_enemy_unit", "faction_wolfpine", "rift_stalker", "rift_stalker", "field_lancer", "숲길 약측을 여는 척후기수"),
        Extra("extra_wolfpine_ember_runner_cell", "늑대소나무 불씨전령조 / Wolfpine Ember Runner Cell", "chapter_ashen_gate", "site_wolfpine_trail", "actual_enemy_cell", "faction_wolfpine", "hunter", "hunter", "field_lancer", "흩어진 추격대를 묶는 전령조"),
        Extra("extra_grey_fang_vanguard", "회조 선봉대 / Grey Fang Vanguard", "chapter_ashen_gate", "site_wolfpine_trail", "proxy_unit", "faction_wolfpine", "scout", "scout", "field_lancer", "회조의 선택을 먼저 흉내 내는 선봉대"),
        Extra("extra_bastion_line_guard", "보루 전열 경비 / Bastion Line Guard", "chapter_sunken_bastion", "site_sunken_bastion", "actual_enemy_unit", "faction_solarum", "guardian", "guardian", "gate_guard", "침몰 요새의 전열 방패"),
        Extra("extra_bastion_reliquary_guard", "보루 성물고 경비 / Bastion Reliquary Guard", "chapter_sunken_bastion", "site_sunken_bastion", "actual_enemy_unit", "faction_solarum", "bastion_penitent", "bastion_penitent", "reliquary_carrier", "단현 일지를 운반하는 성물고 경비"),
        Extra("extra_sunken_adjudicator_lieutenant", "침몰 심판 부관 / Sunken Adjudicator Lieutenant", "chapter_sunken_bastion", "site_sunken_bastion", "actual_enemy_unit", "faction_solarum", "priest", "priest", "adjudicator", "판결관을 예고하는 부관"),
        Boss("extra_sunken_bastion_adjudicator", "침몰 보루 심판관 / Sunken Bastion Adjudicator", "chapter_sunken_bastion", "site_sunken_bastion", "actual_enemy_unit", "faction_solarum", "marksman", "marksman", "adjudicator", "질서론을 판결 절차로 오용한 보루 심판관"),
        Extra("extra_tithe_mark_bearer", "십일조 표식 운반자 / Tithe Mark Bearer", "chapter_sunken_bastion", "site_tithe_road", "actual_enemy_unit", "faction_solarum", "scout", "scout", "record_clerk", "정화 재판의 표식을 옮기는 운반자"),
        Extra("extra_tithe_chain_cantor", "십일조 사슬 성창가 / Tithe Chain Cantor", "chapter_sunken_bastion", "site_tithe_road", "actual_enemy_unit", "faction_solarum", "priest", "priest", "ritual_cell", "결박과 정화를 노래하는 성창가"),
        Extra("extra_tithe_executioner_proxy", "십일조 집행 대리인 / Tithe Executioner Proxy", "chapter_sunken_bastion", "site_tithe_road", "proxy_unit", "faction_solarum", "pale_executor", "pale_executor", "adjudicator", "처형 절차를 대신 수행하는 대리인"),
        Boss("extra_tithe_inquisitor_pureflame", "십일조 순화 심문관 / Tithe Pureflame Inquisitor", "chapter_sunken_bastion", "site_tithe_road", "actual_enemy_unit", "faction_solarum", "hexer", "hexer", "adjudicator", "정화를 권력 언어로 바꾸는 대심문관"),
        Extra("extra_pale_memorial_keeper", "창백 추모지기 / Pale Memorial Keeper", "chapter_ruined_crypts", "site_ruined_crypts", "actual_enemy_unit", "faction_pale", "priest", "priest", "ritual_cell", "묘역의 기억을 소모전으로 지키는 추모지기"),
        Extra("extra_pale_tomb_sentinel", "창백 묘지 파수병 / Pale Tomb Sentinel", "chapter_ruined_crypts", "site_ruined_crypts", "actual_enemy_unit", "faction_pale", "guardian", "guardian", "gate_guard", "묘역 방어선을 닫는 파수병"),
        Extra("extra_black_roll_bailiff", "흑부 집달관 / Black Roll Bailiff", "chapter_ruined_crypts", "site_ruined_crypts", "actual_enemy_unit", "faction_pale", "mirror_cantor", "mirror_cantor", "adjudicator", "단죄 명단 사본을 회수하는 집달관"),
        Boss("extra_crypt_list_keeper", "묘실 명부지기 / Crypt List Keeper", "chapter_ruined_crypts", "site_ruined_crypts", "actual_enemy_unit", "faction_pale", "marksman", "marksman", "record_clerk", "명단을 방패처럼 쓰는 묘실 수호자"),
        Extra("extra_lattice_root_usher", "격자뿌리 인도자 / Lattice Root Usher", "chapter_ruined_crypts", "site_bone_orchard", "ritual_proxy", "faction_lattice", "shaman", "shaman", "survivor_keeper", "뿌리 의식으로 통과자를 가르는 인도자"),
        Extra("extra_lattice_echo_caretaker", "격자 메아리 관리인 / Lattice Echo Caretaker", "chapter_ruined_crypts", "site_bone_orchard", "ritual_proxy", "faction_lattice", "mirror_cantor", "mirror_cantor", "reliquary_carrier", "남은 메아리를 보존하는 관리인"),
        Boss("extra_bone_orchard_watcher", "뼈 과수원 감시자 / Bone Orchard Watcher", "chapter_ruined_crypts", "site_bone_orchard", "ritual_proxy", "faction_lattice", "reaver", "reaver", "survivor_keeper", "문을 통과할 자격을 묻는 관망자"),
        Extra("extra_glass_field_cleric", "유리 들판 성직자 / Glass Field Cleric", "chapter_glass_forest", "site_glass_forest", "actual_enemy_unit", "faction_solarum", "priest", "priest", "ritual_cell", "유리 숲 제어장을 유지하는 성직자"),
        Extra("extra_glass_shard_bailiff", "유리 파편 집달관 / Glass Shard Bailiff", "chapter_glass_forest", "site_glass_forest", "actual_enemy_unit", "faction_solarum", "scout", "scout", "adjudicator", "결정 숲 측면을 잠그는 집달관"),
        Boss("extra_glass_forest_recordkeeper", "유리숲 기록관 / Glass Forest Recordkeeper", "chapter_glass_forest", "site_glass_forest", "actual_enemy_unit", "faction_solarum", "mirror_cantor", "mirror_cantor", "record_clerk", "결정화 통제를 기록 권한으로 집행하는 기록관"),
        Extra("extra_menagerie_snare_runner", "우리 덫 주자 / Menagerie Snare Runner", "chapter_glass_forest", "site_starved_menagerie", "actual_enemy_unit", "faction_solarum", "scout", "scout", "field_lancer", "포획조의 속도를 만드는 덫 주자"),
        Extra("extra_sample_b17_survivor", "표본 B17 생존자 / Sample B17 Survivor", "chapter_glass_forest", "site_starved_menagerie", "personhood_hook", "faction_mixed", "reaver", "reaver", "survivor_keeper", "적군 proxy로 세워진 표본 생존자"),
        Boss("extra_menagerie_keeper", "우리 관리인 / Menagerie Keeper", "chapter_glass_forest", "site_starved_menagerie", "actual_enemy_unit", "faction_solarum", "priest", "priest", "survivor_keeper", "표본화된 생존 압박을 관리하는 관리자"),
        Extra("extra_heartforge_gate_guard", "심장단조 관문 경비 / Heartforge Gate Guard", "chapter_heartforge_descent", "site_heartforge_gate", "system_guard", "faction_mixed", "guardian", "guardian", "gate_guard", "첫 도시 문턱의 경비"),
        Extra("extra_record_rights_marker", "기록권 표식자 / Record Rights Marker", "chapter_heartforge_descent", "site_heartforge_gate", "system_guard", "faction_solarum", "pale_executor", "pale_executor", "record_clerk", "자격과 기록권을 표식으로 남기는 표식자"),
        Boss("extra_heartforge_gate_warden", "심장단조 관문 파수관 / Heartforge Gate Warden", "chapter_heartforge_descent", "site_heartforge_gate", "system_guard", "faction_mixed", "hexer", "hexer", "gate_guard", "기록권을 묻는 대문 수호체"),
        Extra("extra_worldscar_archive_cell", "세계상처 기록 감방 / Worldscar Archive Cell", "chapter_heartforge_descent", "site_worldscar_depths", "system_cell", "faction_mixed", "mirror_cantor", "mirror_cantor", "ritual_cell", "첫 도시 기록실의 전투 셀"),
        Extra("extra_worldscar_rite_echo", "세계상처 의례 메아리 / Worldscar Rite Echo", "chapter_heartforge_descent", "site_worldscar_depths", "ritual_echo", "faction_lattice", "shaman", "shaman", "ritual_cell", "네 갈래 의례 도구의 잔향"),
        Extra("extra_worldscar_record_bailiff", "세계상처 기록 집달관 / Worldscar Record Bailiff", "chapter_heartforge_descent", "site_worldscar_depths", "actual_enemy_unit", "faction_solarum", "guardian", "guardian", "record_clerk", "백규 전 기록 권한을 집행하는 집달관"),
    };

    public static IReadOnlyList<ExtraActorCharacterProfile> Profiles => Entries;

    public static IReadOnlyList<string> CharacterIds => Entries.Select(profile => profile.ActorId).ToArray();

    public static bool TryGetProfile(string actorId, out ExtraActorCharacterProfile profile)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.ActorId, actorId, StringComparison.Ordinal))
            {
                profile = entry;
                return true;
            }
        }

        profile = null!;
        return false;
    }

    private static ExtraActorCharacterProfile Boss(
        string actorId,
        string displayName,
        string chapterId,
        string siteId,
        string storySafety,
        string factionId,
        string combatArchetypeId,
        string p09BasePresetId,
        string modelArchetype,
        string dossierHook)
    {
        return new ExtraActorCharacterProfile(
            actorId,
            displayName,
            ExtraActorExposureTier.BossActor,
            chapterId,
            siteId,
            ExtraActorSpawnPolicy.SiteLocked,
            storySafety,
            factionId,
            combatArchetypeId,
            p09BasePresetId,
            modelArchetype,
            ExtraActorIllustrationTier.BossCard,
            $"bark_set_{actorId}",
            dossierHook,
            GachaEligible: true);
    }

    private static ExtraActorCharacterProfile Extra(
        string actorId,
        string displayName,
        string chapterId,
        string siteId,
        string storySafety,
        string factionId,
        string combatArchetypeId,
        string p09BasePresetId,
        string modelArchetype,
        string dossierHook)
    {
        return new ExtraActorCharacterProfile(
            actorId,
            displayName,
            ExtraActorExposureTier.ExtraActor,
            chapterId,
            siteId,
            ExtraActorSpawnPolicy.SiteLocalPool,
            storySafety,
            factionId,
            combatArchetypeId,
            p09BasePresetId,
            modelArchetype,
            ExtraActorIllustrationTier.ExtraCard,
            $"bark_set_{actorId}",
            dossierHook,
            GachaEligible: true);
    }
}
