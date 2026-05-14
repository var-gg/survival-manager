#!/usr/bin/env python3
"""Apply the 2026-05-13 P09 costume coordination pass.

The source design note uses long wiki-facing IDs for some battle core
characters. This script writes the runtime preset IDs used by Unity, updates
the editor regeneration tool, and keeps alias presets byte-for-byte aligned
with their canonical character.
"""
from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[2]
TOOL_PATH = ROOT / "Assets/_Game/Scripts/Editor/Authoring/P09Appearance/P09DetailPreservingPaletteTool.cs"
PRESET_DIR = ROOT / "Assets/Resources/_Game/Battle/Appearances/P09"
MANIFEST_PATH = ROOT / "art-pipeline/config/p09_visual_identity_manifest.yaml"

PARTS = {
    "weapon": 1,
    "shield": 2,
    "head": 3,
    "chest": 4,
    "arm": 5,
    "waist": 6,
    "leg": 7,
    "hair": 10,
}

PART_LABELS = {
    "hair": "머리",
    "head": "머리 장비",
    "chest": "상의",
    "arm": "팔 장비",
    "waist": "허리 장비",
    "leg": "하의",
    "weapon": "무기",
    "shield": "방패",
}

PART_CONTAINS = {
    "hair": "Hair",
    "head": "Armor",
    "chest": "Armor",
    "arm": "Armor",
    "waist": "Armor",
    "leg": "Armor",
    "weapon": "Weapon",
    "shield": "Shield",
}

PART_ENUM = {
    "hair": "BattleP09AppearancePartType.HairColor",
    "head": "BattleP09AppearancePartType.Head",
    "chest": "BattleP09AppearancePartType.Chest",
    "arm": "BattleP09AppearancePartType.Arm",
    "waist": "BattleP09AppearancePartType.Waist",
    "leg": "BattleP09AppearancePartType.Leg",
    "weapon": "BattleP09AppearancePartType.Weapon",
    "shield": "BattleP09AppearancePartType.Shield",
}

COLOR_ORDER = ["hair", "head", "chest", "arm", "waist", "leg", "weapon", "shield"]


@dataclass(frozen=True)
class ColorSpec:
    main: str
    second: str
    third: str
    label: str | None = None
    emission: str = ""


@dataclass(frozen=True)
class CharacterSpec:
    id: str
    display: str
    label: str
    faction: str
    role: str
    sex: int
    hair_style: int
    hair_color: int
    skin: int
    eye: int
    facial_hair: int
    bust: int
    head: int
    chest: int
    arm: int
    waist: int
    leg: int
    weapon: int
    shield: int
    background: str
    colors: dict[str, ColorSpec]
    intent: str


def c(main: str, second: str, third: str, label: str | None = None, emission: str = "") -> ColorSpec:
    return ColorSpec(main, second, third, label, emission)


SPECS: list[CharacterSpec] = [
    CharacterSpec(
        "hero_dawn_priest", "단린 (丹麟) / Dawn Priest", "단린", "solarum", "priest_paladin",
        2, 4, 6, 1, 1, 0, 2, 7, 7, 5, 7, 4, 3, 4, "#6F7884",
        {
            "hair": c("#9B643F", "#6D3F24", "#C58A5C"),
            "head": c("#D8C8A8", "#C9A24E", "#D9856F"),
            "chest": c("#D8C8A8", "#C9A24E", "#7A6688"),
            "arm": c("#CFC7B8", "#8E7F72", "#C9A24E"),
            "waist": c("#8E6A45", "#5B4430", "#C9A24E"),
            "leg": c("#7A5635", "#4D3828", "#D8C8A8"),
            "weapon": c("#C9A24E", "#E8D49A", "#8E6A45"),
            "shield": c("#D8C8A8", "#C9A24E", "#7A6688"),
        },
        "Armor_007 사제복을 중심으로 arm/leg를 전투형으로 섞은 warm ivory/gold/rose 사제 호위자.",
    ),
    CharacterSpec(
        "hero_pack_raider", "이빨바람 / Pack Raider", "이빨바람", "irisol", "pack_raider",
        1, 2, 1, 1, 1, 0, 2, 0, 4, 10, 10, 4, 1, 0, "#687078",
        {
            "hair": c("#15120F", "#050403", "#3A2B22"),
            "chest": c("#A96F36", "#D08A2E", "#C9B58A"),
            "arm": c("#8B5A2E", "#2F4A2F", "#D08A2E"),
            "waist": c("#5A3A25", "#D08A2E", "#2F4A2F"),
            "leg": c("#6B472D", "#3F2A1E", "#A96F36"),
            "weapon": c("#73543A", "#C9B58A", "#2D2016", "사냥칼"),
        },
        "Head를 비워 wild hair를 살리고 Armor_004 leather에 Armor_010 forest accent를 섞은 raider.",
    ),
    CharacterSpec(
        "hero_grave_hexer", "묵향 (墨香) / Grave Hexer", "묵향", "pale_conclave", "grave_hexer",
        2, 8, 5, 1, 1, 0, 1, 9, 9, 9, 8, 9, 7, 0, "#66635F",
        {
            "hair": c("#C8C5BB", "#A89F8B", "#E2DED6"),
            "head": c("#B8B0A3", "#6DBE7C", "#C4A458"),
            "chest": c("#B8B0A3", "#8E8980", "#6DBE7C"),
            "arm": c("#B8B0A3", "#7FA88C", "#C4A458"),
            "waist": c("#6F647A", "#4C405E", "#6DBE7C"),
            "leg": c("#5F5A54", "#3F3B36", "#B8B0A3"),
            "weapon": c("#4F5856", "#6DBE7C", "#B8B0A3", "지팡이", "#234A31"),
        },
        "Armor_009 robe에 Armor_008 waist를 더한 bone/jade/amber 기억 관리관.",
    ),
    CharacterSpec(
        "hero_echo_savant", "공한 (空閑) / Echo Savant", "공한", "lattice_order", "echo_savant",
        1, 13, 8, 1, 4, 0, 2, 11, 11, 11, 8, 11, 13, 0, "#7A706A",
        {
            "hair": c("#6F647A", "#2A2430", "#C8C1D8"),
            "head": c("#8C7FA0", "#4C405E", "#8FD4E8"),
            "chest": c("#8C7FA0", "#C8C1D8", "#8FD4E8"),
            "arm": c("#7B6E90", "#4C405E", "#C8C1D8"),
            "waist": c("#D6D2E3", "#8C7FA0", "#8FD4E8"),
            "leg": c("#4C405E", "#2E2638", "#8C7FA0"),
            "weapon": c("#4D435A", "#8FD4E8", "#C8C1D8", "의례 활", "#2A5360"),
        },
        "Armor_011 cowl에 Armor_008 pale sash와 Bow_004 cyan prism을 둔 관측 사수.",
    ),
    CharacterSpec(
        "npc_lyra_sternfeld", "선영 (宣英) / Lyra Sternfeld", "선영", "solarum", "fanatic_priest",
        2, 7, 3, 1, 3, 0, 2, 9, 7, 8, 7, 7, 9, 0, "#77716A",
        {
            "hair": c("#D6CBAE", "#BFAE82", "#F0E6C8"),
            "head": c("#D8D2C2", "#8D74C9", "#B8B9C2"),
            "chest": c("#D8D2C2", "#8D74C9", "#D6CBAE"),
            "arm": c("#C8C0D2", "#6E6480", "#8D74C9"),
            "waist": c("#6E6480", "#3C2D48", "#D6CBAE"),
            "leg": c("#5C5248", "#3B322D", "#8D74C9"),
            "weapon": c("#D8D2C2", "#8D74C9", "#D4AF37", "의식 지팡이", "#32205A"),
        },
        "단린과 같은 priest baseline을 cold ivory/violet ceremonial authority로 뒤튼 광신 사제.",
    ),
    CharacterSpec(
        "npc_grey_fang", "회조 (灰爪) / Grey Fang", "회조", "irisol", "grey_fang_duelist",
        1, 2, 5, 1, 1, 5, 2, 0, 4, 11, 4, 3, 1, 0, "#5E6970",
        {
            "hair": c("#B0B3B0", "#817A74", "#D4D0CA"),
            "chest": c("#8B7A68", "#5C5248", "#C1B59C"),
            "arm": c("#6A625F", "#3E3A36", "#8F9698"),
            "waist": c("#5C5248", "#3A2A1F", "#8B7A68"),
            "leg": c("#6F665C", "#4B3A2B", "#8F9698"),
            "weapon": c("#5B5A58", "#6A4A35", "#C1B59C", "낡은 사냥칼"),
        },
        "피형제 leather baseline에 frost hair와 rogue arm/travel boots를 더한 분리파 결투가.",
    ),
    CharacterSpec(
        "npc_black_vellum", "흑지 (黑紙) / Black Vellum", "흑지", "pale_conclave", "black_vellum_judicator",
        2, 9, 1, 1, 1, 0, 2, 9, 9, 11, 9, 8, 7, 0, "#6A6864",
        {
            "hair": c("#151517", "#050508", "#34343A"),
            "head": c("#AFA79A", "#202022", "#2E5A4F"),
            "chest": c("#AFA79A", "#202022", "#5A5148"),
            "arm": c("#4A4648", "#202022", "#2E5A4F"),
            "waist": c("#3E3A36", "#151517", "#AFA79A"),
            "leg": c("#56505C", "#2A2434", "#2E5A4F"),
            "weapon": c("#3B3745", "#2E5A4F", "#AFA79A", "단죄 지팡이", "#123028"),
        },
        "Armor_009 robe에 black sleeve와 dark caster boots를 붙인 parchment/ink/emerald 단죄자.",
    ),
    CharacterSpec(
        "npc_silent_moon", "침월 (沉月) / Silent Moon", "침월", "lattice_order", "silent_moon_hardliner",
        2, 9, 8, 1, 3, 0, 1, 8, 8, 8, 9, 8, 7, 0, "#746A64",
        {
            "hair": c("#C7C0D8", "#6F5D88", "#F0EAF8"),
            "head": c("#AFA7B8", "#3F304D", "#8EA0FF"),
            "chest": c("#AFA7B8", "#3F304D", "#D6D2E3"),
            "arm": c("#AFA7B8", "#4A3A5C", "#8EA0FF"),
            "waist": c("#D6D2E3", "#8A8198", "#3F304D"),
            "leg": c("#3F304D", "#241B2D", "#AFA7B8"),
            "weapon": c("#4D435A", "#8EA0FF", "#D6D2E3", "침묵 지팡이", "#26365A"),
        },
        "Armor_008 moon mage에 Armor_009 sash를 더한 lavender/eggplant/blue-violet 강경파.",
    ),
    CharacterSpec(
        "npc_baekgyu_sternheim", "백규 (白圭) / Baekgyu Sternheim", "백규", "solarum", "scholar_boss",
        1, 3, 5, 1, 1, 6, 2, 12, 12, 5, 12, 12, 5, 0, "#5B6470",
        {
            "hair": c("#D8D4C8", "#AFA89A", "#F2EEE0"),
            "head": c("#7E8490", "#D8CFB8", "#7D5CC7"),
            "chest": c("#7E8490", "#D8CFB8", "#7D5CC7"),
            "arm": c("#6F737A", "#3E4652", "#D8CFB8"),
            "waist": c("#3E4652", "#242A31", "#7D5CC7"),
            "leg": c("#5C6370", "#353B45", "#D8CFB8"),
            "weapon": c("#424047", "#D8CFB8", "#7D5CC7", "귀족 검", "#30204A"),
        },
        "Armor_012 officer-scholar에 Armor_005 arm을 섞은 grey/ivory/violet 정치 권위.",
    ),
    CharacterSpec(
        "warden", "철위 (鐵衛) / Iron Warden", "철위", "solarum", "iron_warden",
        1, 5, 5, 1, 2, 4, 2, 6, 6, 6, 5, 6, 3, 5, "#5E6670",
        {
            "hair": c("#C0C0B8", "#8E8A82", "#E5E0D6"),
            "head": c("#9AA0A6", "#3F5F8F", "#D8CFB8"),
            "chest": c("#9AA0A6", "#3F5F8F", "#B7B0A0"),
            "arm": c("#8D949C", "#5B626A", "#D8CFB8"),
            "waist": c("#4A5160", "#2E3440", "#8A5A38"),
            "leg": c("#7A828C", "#4A5160", "#D8CFB8"),
            "weapon": c("#B0B4B8", "#6D6258", "#D8CFB8", "정규군 검"),
            "shield": c("#4A6FA8", "#D8CFB8", "#8A5A38", "왕청 방패"),
        },
        "Armor_006 full plate에 field waist와 royal blue shield를 둔 veteran warden.",
    ),
    CharacterSpec(
        "guardian", "묘직 (墓直) / Crypt Guardian", "묘직", "pale_conclave", "crypt_guardian",
        2, 6, 5, 1, 2, 0, 2, 9, 5, 6, 9, 5, 2, 3, "#59635F",
        {
            "hair": c("#D2CEC4", "#A9A092", "#EEE9DE"),
            "head": c("#B8B0A3", "#7A8FA8", "#C4A458"),
            "chest": c("#8D969A", "#5E6F80", "#B8B0A3"),
            "arm": c("#8A9096", "#5B636B", "#C4A458"),
            "waist": c("#B8B0A3", "#7A8FA8", "#C4A458"),
            "leg": c("#6B7075", "#3F454A", "#B8B0A3"),
            "weapon": c("#7A7770", "#B8B0A3", "#C4A458", "묘역 검"),
            "shield": c("#8D969A", "#5E6F80", "#C4A458", "석관 방패"),
        },
        "Armor_005 chain에 robe head/waist와 heavy arm을 얹은 blue-grey/bone/amber 수호자.",
    ),
    CharacterSpec(
        "bulwark", "송곳벽 / Fang Bulwark", "송곳벽", "irisol", "fang_bulwark",
        1, 2, 6, 1, 1, 2, 2, 4, 4, 6, 4, 4, 1, 2, "#686158",
        {
            "hair": c("#A65A2E", "#5A2E1D", "#D08A2E"),
            "head": c("#B06A3C", "#D08A2E", "#C9B58A"),
            "chest": c("#9A5B34", "#D08A2E", "#2F4A2F"),
            "arm": c("#7E7770", "#5A3A25", "#D08A2E"),
            "waist": c("#5A3A25", "#3A2618", "#D08A2E"),
            "leg": c("#6B472D", "#3F2A1E", "#9A5B34"),
            "weapon": c("#73543A", "#C9B58A", "#2D2016", "부족 검"),
            "shield": c("#D08A2E", "#5A3A25", "#2F4A2F", "나무 방패"),
        },
        "Armor_004 leather에 heavy guard arm과 orange shield를 더한 이리솔 방패병.",
    ),
    CharacterSpec(
        "slayer", "서검 (誓劍) / Oath Slayer", "서검", "solarum", "oath_slayer",
        2, 6, 1, 1, 2, 0, 1, 0, 5, 12, 5, 5, 4, 0, "#6D6E74",
        {
            "hair": c("#1A1718", "#050508", "#3A3336"),
            "chest": c("#8A8E92", "#2A2A2E", "#D8CFB8"),
            "arm": c("#5F6570", "#D8CFB8", "#8D74C9"),
            "waist": c("#3E4652", "#202022", "#D8CFB8"),
            "leg": c("#5C6370", "#2A2A2E", "#8A8E92"),
            "weapon": c("#B8B8BC", "#D8CFB8", "#2A2A2E", "맹세검"),
        },
        "Head를 비워 black hair를 보이고 Armor_005 chain에 Armor_012 arm을 붙인 executor duelist.",
    ),
    CharacterSpec(
        "reaver", "묵괴 (墨壞) / Grave Reaver", "묵괴", "pale_conclave", "grave_reaver",
        2, 8, 5, 1, 2, 0, 1, 11, 11, 4, 11, 5, 5, 0, "#5F5B57",
        {
            "hair": c("#CFCAC0", "#A89F8B", "#E7E2D8"),
            "head": c("#4A4648", "#AFA79A", "#8E4F36"),
            "chest": c("#4A4648", "#AFA79A", "#8E4F36"),
            "arm": c("#6B5544", "#3A2A1F", "#8E4F36"),
            "waist": c("#3E3A36", "#202022", "#AFA79A"),
            "leg": c("#5C5A58", "#2F3035", "#AFA79A"),
            "weapon": c("#3B3745", "#8E4F36", "#AFA79A", "회수검"),
        },
        "Armor_011 rogue에 leather arm과 chain leg를 섞은 ash/black/rust 묘역 순찰병.",
    ),
    CharacterSpec(
        "hunter", "원시 (遠矢) / Longshot Hunter", "원시", "solarum", "longshot_hunter",
        1, 12, 1, 1, 2, 1, 2, 3, 3, 5, 3, 3, 10, 0, "#657078",
        {
            "hair": c("#5A3D2B", "#2A1F1A", "#8A5A38"),
            "head": c("#C0A078", "#3F5F8F", "#D8CFB8"),
            "chest": c("#C0A078", "#3F5F8F", "#D8CFB8"),
            "arm": c("#8A8E92", "#4A5160", "#D8CFB8"),
            "waist": c("#3F5F8F", "#2E3440", "#C0A078"),
            "leg": c("#6A5A45", "#3E342A", "#3F5F8F"),
            "weapon": c("#8A6A3F", "#3F5F8F", "#D8CFB8", "장궁"),
        },
        "Armor_003 travel hunter에 Armor_005 bracer와 tan/blue cloth를 둔 원정 정찰병.",
    ),
    CharacterSpec(
        "scout", "숲살이 / Trail Scout", "숲살이", "irisol", "trail_scout",
        2, 12, 4, 1, 1, 0, 2, 10, 10, 3, 10, 4, 11, 0, "#5F6C5B",
        {
            "hair": c("#4F6B35", "#2E3F24", "#8A9A5B"),
            "head": c("#6F8A3A", "#D08A2E", "#C9B58A"),
            "chest": c("#6F8A3A", "#D08A2E", "#3F5F32"),
            "arm": c("#A88A5F", "#5A3A25", "#D08A2E"),
            "waist": c("#5A3A25", "#2F4A2F", "#D08A2E"),
            "leg": c("#6B472D", "#3F2A1E", "#6F8A3A"),
            "weapon": c("#73543A", "#D08A2E", "#3F5F32", "숲 활"),
        },
        "Armor_010 forest scout에 travel sleeve/leather leg를 섞은 green/orange 혼혈 정찰병.",
    ),
    CharacterSpec(
        "marksman", "냉시 (冷矢) / Dread Marksman", "냉시", "pale_conclave", "dread_marksman",
        1, 5, 5, 1, 4, 0, 2, 5, 5, 5, 9, 5, 12, 0, "#596066",
        {
            "hair": c("#D0D6D8", "#9EA8AA", "#EEF4F4"),
            "head": c("#7F8E94", "#4F6A76", "#76D7E0"),
            "chest": c("#7F8E94", "#4F6A76", "#76D7E0"),
            "arm": c("#6F7E84", "#3F5058", "#B8B0A3"),
            "waist": c("#B8B0A3", "#4F6A76", "#76D7E0"),
            "leg": c("#5C666A", "#2F3A40", "#7F8E94"),
            "weapon": c("#3E4652", "#76D7E0", "#B8B0A3", "냉궁", "#1B4A50"),
        },
        "Armor_005 chain에 Armor_009 sash와 cyan bow glow를 둔 cold precision archer.",
    ),
    CharacterSpec(
        "shaman", "풍의 (風儀) / Storm Shaman", "풍의", "irisol", "storm_shaman",
        2, 10, 1, 1, 1, 0, 3, 10, 10, 10, 7, 10, 8, 0, "#6A6458",
        {
            "hair": c("#2E2119", "#1A120E", "#8A7F70"),
            "head": c("#8A4F2E", "#C98D47", "#2F4A2F"),
            "chest": c("#8A4F2E", "#C98D47", "#2F4A2F"),
            "arm": c("#7A4A32", "#C98D47", "#8A7F70"),
            "waist": c("#6B4A3A", "#D4A458", "#2F4A2F"),
            "leg": c("#4F3928", "#2E2119", "#8A4F2E"),
            "weapon": c("#6A4A35", "#D4A458", "#2F4A2F", "토템 지팡이"),
        },
        "Armor_010 shaman에 robe sash를 더한 red-brown/amber/deep-green 공동체 치유사.",
    ),
    CharacterSpec(
        "rift_stalker", "틈사냥꾼 / Rift Stalker", "틈사냥꾼", "irisol", "rift_stalker",
        2, 1, 1, 1, 4, 0, 1, 0, 11, 4, 10, 11, 1, 0, "#615F69",
        {
            "hair": c("#2A1F1A", "#0F0B08", "#5A3A28"),
            "chest": c("#5A4A62", "#3F304D", "#8EA0FF"),
            "arm": c("#7A4A32", "#D08A2E", "#5A3A25"),
            "waist": c("#5A3A25", "#2F4A2F", "#D08A2E"),
            "leg": c("#3F304D", "#241B2D", "#5A4A62"),
            "weapon": c("#5B5A58", "#8EA0FF", "#D08A2E", "틈 단검"),
        },
        "Head를 비운 Armor_011 rogue에 leather/forest clan mark와 cyan rift accent를 더한 stalker.",
    ),
    CharacterSpec(
        "bastion_penitent", "참회벽 / Bastion Penitent", "참회벽", "solarum", "bastion_penitent",
        2, 5, 9, 1, 2, 0, 2, 0, 6, 5, 6, 5, 3, 3, "#626A72",
        {
            "hair": c("#A33A2E", "#5A1F1A", "#D66A4F"),
            "chest": c("#8A8E92", "#4A5160", "#C9A24E"),
            "arm": c("#7A828C", "#3E4652", "#D8CFB8"),
            "waist": c("#5C6370", "#2E3440", "#8A3A2E"),
            "leg": c("#5C6370", "#2A2A2E", "#C9A24E"),
            "weapon": c("#A8A8AC", "#6D6258", "#C9A24E", "옛 정규군 검"),
            "shield": c("#8A8E92", "#C9A24E", "#3F5F8F", "가족 방패"),
        },
        "Head를 비운 repaired plate/chain mix와 red hair/gold-blue shield의 참회 방패병.",
    ),
    CharacterSpec(
        "pale_executor", "백집행 (白執行) / Pale Executor", "백집행", "pale_conclave", "pale_executor",
        1, 13, 5, 1, 4, 3, 2, 12, 12, 9, 12, 11, 12, 0, "#6A6870",
        {
            "hair": c("#D8D4C8", "#AFA89A", "#F2EEE0"),
            "head": c("#B8B0C8", "#2F3035", "#2E8A7A"),
            "chest": c("#B8B0C8", "#2F3035", "#2E8A7A"),
            "arm": c("#AFA79A", "#3E3A36", "#2E8A7A"),
            "waist": c("#4A405A", "#2F3035", "#D8CFB8"),
            "leg": c("#3B3745", "#202022", "#B8B0C8"),
            "weapon": c("#3B3745", "#2E8A7A", "#B8B0C8", "망명자의 활", "#124038"),
        },
        "Armor_012 formal executor에 robe arm/exile leg와 teal bow glow를 더한 망명 처형자.",
    ),
    CharacterSpec(
        "mirror_cantor", "명음 (明音) / Mirror Cantor", "명음", "lattice_order", "mirror_cantor",
        2, 9, 8, 1, 4, 0, 2, 9, 9, 8, 9, 8, 9, 5, "#686370",
        {
            "hair": c("#2D1F3F", "#120A20", "#C7C0D8"),
            "head": c("#D8D2E3", "#C05BE0", "#8FD4E8"),
            "chest": c("#D8D2E3", "#C05BE0", "#8FD4E8"),
            "arm": c("#AFA7B8", "#4A3A5C", "#8FD4E8"),
            "waist": c("#C7C0D8", "#6F5D88", "#C05BE0"),
            "leg": c("#4A3A5C", "#2D2438", "#D8D2E3"),
            "weapon": c("#D8D2E3", "#C05BE0", "#8FD4E8", "거울 지팡이", "#2A5360"),
            "shield": c("#C7C0D8", "#8FD4E8", "#C05BE0", "의례 거울", "#2A5360"),
        },
        "Armor_009 cantor robe에 Armor_008 arm/leg와 mirror shield를 둔 crystal white/magenta/cyan 봉합자.",
    ),
    CharacterSpec(
        "hero_aegis_sentinel", "방진 (方陣) / Aegis Sentinel", "방진", "lattice_order", "aegis_sentinel",
        1, 5, 5, 1, 2, 1, 2, 0, 6, 6, 9, 6, 5, 5, "#646B76",
        {
            "hair": c("#C9C6BC", "#8F8A82", "#E8E2D6"),
            "chest": c("#9EA5B0", "#5A4B78", "#D8D2E3"),
            "arm": c("#8D96A0", "#4C405E", "#B8B0C8"),
            "waist": c("#4B3A32", "#2D2438", "#8FD4E8"),
            "leg": c("#7E8792", "#3E4652", "#D8D2E3"),
            "weapon": c("#B8B0C8", "#8FD4E8", "#4C405E", "결사 의례검", "#26365A"),
            "shield": c("#D8D2E3", "#8FD4E8", "#5A4B78", "격자 방패", "#26365A"),
        },
        "Head를 비워 회백 노년 hair/moustache를 보이고 plate+ritual sash+heraldic shield로 침묵의 결사 보초를 만든다.",
    ),
    CharacterSpec(
        "hero_shardblade", "편검 (片劍) / Shardblade", "편검", "lattice_order", "shardblade",
        2, 3, 8, 1, 4, 0, 2, 0, 4, 5, 4, 3, 4, 0, "#6B6862",
        {
            "hair": c("#6F4B88", "#2A1838", "#C8B5E8"),
            "chest": c("#7B614A", "#2E3440", "#8FD4E8"),
            "arm": c("#6F737A", "#4C405E", "#D8D2E3"),
            "waist": c("#2E3440", "#1A1D24", "#8FD4E8"),
            "leg": c("#5C4A38", "#2D241C", "#6F4B88"),
            "weapon": c("#D8D2E3", "#8FD4E8", "#6F4B88", "재조립 검", "#2A5360"),
        },
        "외부 일상복 같은 Armor_004에 실전 bracer와 격자검을 더한 earth/indigo/prism 직공 결투가.",
    ),
    CharacterSpec(
        "hero_prism_seeker", "광로 (光路) / Prism Seeker", "광로", "lattice_order", "prism_seeker",
        1, 12, 1, 1, 4, 0, 2, 0, 3, 11, 8, 3, 13, 0, "#686C76",
        {
            "hair": c("#2B211D", "#0D0908", "#6B4A38"),
            "chest": c("#D6D2E3", "#5A4B78", "#8FD4E8"),
            "arm": c("#6F647A", "#3F304D", "#8FD4E8"),
            "waist": c("#4C405E", "#2A2430", "#D6D2E3"),
            "leg": c("#5C6370", "#2E3440", "#8FD4E8"),
            "weapon": c("#4D435A", "#8FD4E8", "#D6D2E3", "프리즘 활", "#2A5360"),
        },
        "Pindoc wiki의 젊은 남성 Ranger 기준. travel scout gear에 lattice sash와 Bow_004 prism signal을 더한 외부 호기심 정찰병.",
    ),
    CharacterSpec(
        "hero_ember_runner", "연주 (燕走) / Ember Runner", "연주", "irisol", "ember_runner",
        2, 1, 6, 1, 1, 0, 1, 0, 3, 4, 4, 3, 10, 0, "#66706A",
        {
            "hair": c("#B66A34", "#5A2E1D", "#E08A42"),
            "chest": c("#8A6B3E", "#D08A2E", "#3F5F32"),
            "arm": c("#8A4F2E", "#5A3A25", "#D08A2E"),
            "waist": c("#5A3A25", "#2F4A2F", "#D08A2E"),
            "leg": c("#7A4A32", "#3F2A1E", "#8A6B3E"),
            "weapon": c("#8A6A3F", "#D08A2E", "#3F5F32", "견습 활"),
        },
        "가벼운 travel/leather mix와 bright ember hair로 이리솔 견습 사냥꾼의 빠른 에너지를 만든다.",
    ),
    CharacterSpec(
        "hero_iron_pelt", "철피 (鐵皮) / Iron Pelt", "철피", "irisol", "iron_pelt_hardliner",
        1, 2, 1, 1, 3, 5, 2, 0, 6, 6, 4, 4, 5, 3, "#5E5B54",
        {
            "hair": c("#1F1713", "#080504", "#5A3A25"),
            "chest": c("#5E6266", "#2F3438", "#9A4A2E"),
            "arm": c("#5A3A25", "#2F4A2F", "#8A4A2E"),
            "waist": c("#4A2E22", "#1E1712", "#9A4A2E"),
            "leg": c("#5A3A25", "#2F241C", "#5E6266"),
            "weapon": c("#4A4642", "#9A4A2E", "#C1B59C", "강경파 검"),
            "shield": c("#5E6266", "#9A4A2E", "#2F4A2F", "탈취 방패"),
        },
        "솔라룸 plate 파편을 이리솔 leather에 강제로 섞은 dark iron/rust/moss 강경 분리파 vanguard.",
    ),
    CharacterSpec(
        "npc_aldric", "단현 스턴홀트 (丹玄) / Aldric Sternfeld", "단현", "solarum", "historical_scholar",
        1, 8, 5, 1, 2, 6, 2, 0, 3, 3, 3, 3, 0, 0, "#706A64",
        {
            "hair": c("#D8D4C8", "#AFA89A", "#F2EEE0"),
            "chest": c("#5C5A58", "#202022", "#B8A078"),
            "arm": c("#4A4648", "#202022", "#B8A078"),
            "waist": c("#2A2A2E", "#151517", "#8A3A2E"),
            "leg": c("#3F3B36", "#202022", "#B8A078"),
        },
        "Male_Armor_003 scholar robe, ivory tied hair, and dignified facial hair for the historical dead scholar with no combat weapon.",
    ),
]

ALIASES = {
    "priest": ("단린 (丹麟) / Dawn Priest", "hero_dawn_priest"),
    "raider": ("이빨바람 / Pack Raider", "hero_pack_raider"),
    "hexer": ("묵향 (墨香) / Grave Hexer", "hero_grave_hexer"),
}


def json_string(value: str, *, ascii_only: bool = False) -> str:
    return json.dumps(value, ensure_ascii=ascii_only)


def parse_existing_face_types() -> dict[str, int]:
    result: dict[str, int] = {}
    for path in PRESET_DIR.glob("p09_appearance_*.asset"):
        text = path.read_text(encoding="utf-8")
        character_match = re.search(r"^\s*characterId:\s*(.+)$", text, re.MULTILINE)
        face_match = re.search(r"^\s*faceTypeId:\s*(\d+)$", text, re.MULTILINE)
        if character_match and face_match:
            result[character_match.group(1).strip()] = int(face_match.group(1))
    return result


def hex_to_float_tuple(hex_color: str) -> tuple[float, float, float]:
    if not re.fullmatch(r"#[0-9A-Fa-f]{6}", hex_color):
        raise ValueError(f"Invalid color: {hex_color}")
    return (
        int(hex_color[1:3], 16) / 255.0,
        int(hex_color[3:5], 16) / 255.0,
        int(hex_color[5:7], 16) / 255.0,
    )


def unity_color(hex_color: str) -> str:
    r, g, b = hex_to_float_tuple(hex_color)
    return f"{{r: {r:.8g}, g: {g:.8g}, b: {b:.8g}, a: 1}}"


def replace_scalar(text: str, key: str, value: int | str) -> str:
    if isinstance(value, str):
        rendered = json_string(value, ascii_only=True)
    else:
        rendered = str(value)
    return re.sub(
        rf"^(\s*{re.escape(key)}:\s*).*$",
        lambda match: f"{match.group(1)}{rendered}",
        text,
        flags=re.MULTILINE,
    )


def color_for_yaml(key: str, spec: ColorSpec) -> list[str]:
    label = spec.label or PART_LABELS[key]
    emission = spec.emission or "#000000"
    return [
        f"  - Label: {json_string(label, ascii_only=True)}",
        "    UsePartTarget: 1",
        f"    TargetPart: {PARTS[key]}",
        f"    TargetContains: {PART_CONTAINS[key]}",
        "    Enabled: 1",
        f"    MainColor: {unity_color(spec.main)}",
        f"    SecondColor: {unity_color(spec.second)}",
        f"    ThirdColor: {unity_color(spec.third)}",
        f"    UseEmissionColor: {1 if spec.emission else 0}",
        f"    EmissionColor: {unity_color(emission)}",
    ]


def write_preset_asset(spec: CharacterSpec, face_type: int, *, character_id: str | None = None, display: str | None = None) -> None:
    output_id = character_id or spec.id
    output_display = display or spec.display
    path = PRESET_DIR / f"p09_appearance_{output_id}.asset"
    if not path.exists():
        raise FileNotFoundError(path)
    text = path.read_text(encoding="utf-8")

    values: dict[str, int | str] = {
        "characterId": output_id,
        "displayName": output_display,
        "weaponId": spec.weapon,
        "shieldId": spec.shield,
        "headId": spec.head,
        "chestId": spec.chest,
        "armId": spec.arm,
        "waistId": spec.waist,
        "legId": spec.leg,
        "sexId": spec.sex,
        "faceTypeId": face_type,
        "hairStyleId": spec.hair_style,
        "hairColorId": spec.hair_color,
        "skinId": spec.skin,
        "eyeColorId": spec.eye,
        "facialHairId": spec.facial_hair,
        "bustSizeId": spec.bust,
    }
    for key, value in values.items():
        text = replace_scalar(text, key, value)

    marker = "  materialColorOverrides:"
    marker_index = text.find(marker)
    if marker_index < 0:
        raise ValueError(f"{path}: materialColorOverrides block not found")
    prefix = text[: marker_index + len(marker)].rstrip() + "\n"
    color_lines: list[str] = []
    for key in COLOR_ORDER:
        color = spec.colors.get(key)
        if color is not None:
            color_lines.extend(color_for_yaml(key, color))
    path.write_text(prefix + "\n".join(color_lines) + "\n", encoding="utf-8")


def cs_color(key: str, spec: ColorSpec) -> str:
    label = spec.label or PART_LABELS[key]
    args = [
        json_string(label),
        PART_ENUM[key],
        json_string(PART_CONTAINS[key]),
        json_string(spec.main),
        json_string(spec.second),
        json_string(spec.third),
    ]
    if spec.emission:
        args.append(json_string(spec.emission))
    return "                    O(" + ", ".join(args) + "),"


def generate_csharp_specs(face_types: dict[str, int]) -> str:
    lines: list[str] = [
        "    private static IReadOnlyList<PaletteSpec> BuildCanonicalSpecs()",
        "    {",
        "        return new[]",
        "        {",
    ]
    for spec in SPECS:
        face_type = face_types.get(spec.id, 1)
        lines.extend(
            [
                "            new PaletteSpec(",
                f"                {json_string(spec.id)},",
                f"                {json_string(spec.display)},",
                (
                    "                "
                    f"{spec.sex}, {face_type}, {spec.hair_style}, {spec.hair_color}, {spec.skin}, "
                    f"{spec.eye}, {spec.facial_hair}, {spec.bust},"
                ),
                (
                    "                "
                    f"{spec.head}, {spec.chest}, {spec.arm}, {spec.waist}, {spec.leg}, "
                    f"{spec.weapon}, {spec.shield},"
                ),
                f"                {json_string(spec.background)},",
                "                new[]",
                "                {",
            ]
        )
        for key in COLOR_ORDER:
            color = spec.colors.get(key)
            if color is not None:
                lines.append(cs_color(key, color))
        lines.extend(["                }),"])
    lines.extend(["        };", "    }"])
    return "\n".join(lines)


def update_tool(face_types: dict[str, int]) -> None:
    text = TOOL_PATH.read_text(encoding="utf-8")
    apply_part_ids = """    private static void ApplyPartIds(BattleP09AppearancePreset preset, PaletteSpec spec)
    {
        preset.SetContentId(BattleP09AppearancePartType.Weapon, spec.WeaponId);
        preset.SetContentId(BattleP09AppearancePartType.Shield, spec.ShieldId);
        preset.SetContentId(BattleP09AppearancePartType.Head, spec.HeadId);
        preset.SetContentId(BattleP09AppearancePartType.Chest, spec.ChestId);
        preset.SetContentId(BattleP09AppearancePartType.Arm, spec.ArmId);
        preset.SetContentId(BattleP09AppearancePartType.Waist, spec.WaistId);
        preset.SetContentId(BattleP09AppearancePartType.Leg, spec.LegId);
        preset.SetContentId(BattleP09AppearancePartType.Sex, spec.SexId);
        preset.SetContentId(BattleP09AppearancePartType.FaceType, spec.FaceTypeId);
        preset.SetContentId(BattleP09AppearancePartType.HairStyle, spec.HairStyleId);
        preset.SetContentId(BattleP09AppearancePartType.HairColor, spec.HairColorId);
        preset.SetContentId(BattleP09AppearancePartType.Skin, spec.SkinId);
        preset.SetContentId(BattleP09AppearancePartType.EyeColor, spec.EyeColorId);
        preset.SetContentId(BattleP09AppearancePartType.FacialHair, spec.FacialHairId);
        preset.SetContentId(BattleP09AppearancePartType.BustSize, spec.BustSizeId);
    }"""
    text = re.sub(
        r"    private static void ApplyPartIds\(BattleP09AppearancePreset preset, PaletteSpec spec\)\n    \{.*?\n    \}",
        apply_part_ids,
        text,
        count=1,
        flags=re.DOTALL,
    )
    text = re.sub(
        r"    private static IReadOnlyList<PaletteSpec> BuildCanonicalSpecs\(\)\n    \{.*?\n    \}\n\n    private static ColorOverrideSpec O\(",
        generate_csharp_specs(face_types) + "\n\n    private static ColorOverrideSpec O(",
        text,
        count=1,
        flags=re.DOTALL,
    )
    text = re.sub(
        r"        int ArmorId,\n        int WeaponId,",
        "        int HeadId,\n        int ChestId,\n        int ArmId,\n        int WaistId,\n        int LegId,\n        int WeaponId,",
        text,
        count=1,
    )
    TOOL_PATH.write_text(text, encoding="utf-8")


def update_manifest() -> None:
    manifest = yaml.safe_load(MANIFEST_PATH.read_text(encoding="utf-8"))
    by_id = {spec.id: spec for spec in SPECS}
    characters = manifest.setdefault("characters", [])
    seen_ids: set[str] = set()
    for item in characters:
        character_id = item.get("id")
        if isinstance(character_id, str):
            seen_ids.add(character_id)
        spec = by_id.get(item.get("id"))
        if spec is None:
            continue
        item["display_name"] = spec.display
        item["faction"] = spec.faction
        item["combat_class"] = spec.role
        item["uniform_group"] = f"{spec.faction}_{spec.role}"
        item["silhouette_family"] = (
            f"p09_h{spec.head}_c{spec.chest}_a{spec.arm}_w{spec.waist}_l{spec.leg}"
            f"_wp{spec.weapon}_sh{spec.shield}"
        )
        item["visual_intent"] = spec.intent
    for spec in SPECS:
        if spec.id in seen_ids:
            continue
        characters.append(
            {
                "id": spec.id,
                "display_name": spec.display,
                "faction": spec.faction,
                "combat_class": spec.role,
                "archetype_id": spec.id,
                "uniform_group": f"{spec.faction}_{spec.role}",
                "silhouette_family": (
                    f"p09_h{spec.head}_c{spec.chest}_a{spec.arm}_w{spec.waist}_l{spec.leg}"
                    f"_wp{spec.weapon}_sh{spec.shield}"
                ),
                "visual_intent": spec.intent,
            }
        )
    MANIFEST_PATH.write_text(
        yaml.safe_dump(manifest, allow_unicode=True, sort_keys=False, width=140),
        encoding="utf-8",
    )


def apply_all(*, skip_tool: bool = False, skip_assets: bool = False, skip_manifest: bool = False) -> None:
    face_types = parse_existing_face_types()
    if not skip_tool:
        update_tool(face_types)
    if not skip_assets:
        by_id = {spec.id: spec for spec in SPECS}
        for spec in SPECS:
            write_preset_asset(spec, face_types.get(spec.id, 1))
        for alias_id, (display, target_id) in ALIASES.items():
            spec = by_id[target_id]
            write_preset_asset(spec, face_types.get(target_id, 1), character_id=alias_id, display=display)
    if not skip_manifest:
        update_manifest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--skip-tool", action="store_true")
    parser.add_argument("--skip-assets", action="store_true")
    parser.add_argument("--skip-manifest", action="store_true")
    args = parser.parse_args()
    apply_all(skip_tool=args.skip_tool, skip_assets=args.skip_assets, skip_manifest=args.skip_manifest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
