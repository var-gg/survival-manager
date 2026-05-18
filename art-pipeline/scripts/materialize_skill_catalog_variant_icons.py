#!/usr/bin/env python3
"""Materialize distinct skill catalog icons from catalog subject metadata.

This is a deterministic bridge for Play Mode review. It keeps the authored
SkillId -> IconId binding intact while replacing duplicated catalog placeholders
with unique transparent icons and matching 2x2 review sheets.
"""
from __future__ import annotations

import argparse
import hashlib
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml
from PIL import Image, ImageDraw


PIPELINE_ROOT = Path(__file__).resolve().parents[1]
SUBJECT_ROOT = PIPELINE_ROOT / "subjects" / "icons" / "skill"
CATALOG_OUTPUT_DIR = PIPELINE_ROOT / "output" / "icons" / "skill" / "catalog_v2"
SHEET_OUTPUT_ROOT = PIPELINE_ROOT / "output"

ICON_SIZE = 768
SHEET_SIZE = 1568
GAP = 32
SCALE = 3
CHROMA = (255, 0, 255, 255)


@dataclass(frozen=True)
class IconSpec:
    subject_id: str
    index: int
    skill_id: str
    icon_id: str
    slot: str
    kind: str
    status_ids: tuple[str, ...]


@dataclass(frozen=True)
class Palette:
    fill: tuple[int, int, int, int]
    accent: tuple[int, int, int, int]
    secondary: tuple[int, int, int, int]
    dark: tuple[int, int, int, int]
    light: tuple[int, int, int, int]


def stable_int(value: str) -> int:
    return int(hashlib.sha256(value.encode("utf-8")).hexdigest()[:16], 16)


def read_frontmatter(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        raise ValueError(f"{path}: missing YAML frontmatter")
    end = text.find("\n---\n", 4)
    if end == -1:
        raise ValueError(f"{path}: unterminated YAML frontmatter")
    data = yaml.safe_load(text[4:end])
    if not isinstance(data, dict):
        raise ValueError(f"{path}: frontmatter must be a mapping")
    return data


def load_subject_specs(subject_path: Path) -> list[IconSpec]:
    fm = read_frontmatter(subject_path)
    subject_id = str(fm.get("subject_id") or subject_path.parent.name)
    bindings = fm.get("skill_bindings")
    if not isinstance(bindings, list) or len(bindings) != 4:
        raise ValueError(f"{subject_path}: skill_bindings must contain exactly 4 entries")

    specs: list[IconSpec] = []
    for index, item in enumerate(bindings):
        if not isinstance(item, dict):
            raise ValueError(f"{subject_path}: skill_bindings[{index}] must be a mapping")
        skill_id = str(item.get("skill_id") or "").strip()
        icon_id = str(item.get("icon_id") or "").strip()
        if not skill_id or not icon_id:
            raise ValueError(f"{subject_path}: skill_bindings[{index}] requires skill_id and icon_id")
        statuses = item.get("status_ids") or []
        if not isinstance(statuses, list):
            statuses = []
        specs.append(
            IconSpec(
                subject_id=subject_id,
                index=index,
                skill_id=skill_id,
                icon_id=icon_id,
                slot=str(item.get("slot") or ""),
                kind=str(item.get("kind") or ""),
                status_ids=tuple(str(status) for status in statuses),
            )
        )
    return specs


def color_family(spec: IconSpec) -> str:
    sid = spec.skill_id.lower()
    if any(token in sid for token in ("guard", "bulwark", "vanguard", "warden", "priest", "aegis", "sentinel", "iron", "square", "anchored")):
        return "vanguard"
    if any(token in sid for token in ("raider", "reaver", "slayer", "duelist", "strike", "blade", "riposte", "fracture", "ash", "cinder", "heat", "maul", "rusthide", "bloodless", "mirror")):
        return "duelist"
    if any(token in sid for token in ("ranger", "scout", "marksman", "hunter", "shot", "arrow", "longshot", "piercing", "ember", "prism", "signal", "glass", "snare")):
        return "ranger"
    if any(token in sid for token in ("mystic", "hexer", "shaman", "heal", "siphon", "memory", "echo", "lattice", "phase", "savant", "shard")):
        return "mystic"
    return "neutral"


def palette_for(spec: IconSpec) -> Palette:
    palettes = {
        "vanguard": Palette((44, 99, 146, 255), (236, 185, 73, 255), (211, 221, 226, 255), (23, 24, 30, 255), (255, 241, 184, 255)),
        "duelist": Palette((147, 41, 36, 255), (238, 126, 43, 255), (195, 204, 210, 255), (25, 18, 18, 255), (255, 217, 151, 255)),
        "ranger": Palette((42, 132, 104, 255), (96, 209, 172, 255), (218, 190, 80, 255), (16, 31, 31, 255), (219, 255, 228, 255)),
        "mystic": Palette((55, 117, 108, 255), (124, 207, 140, 255), (226, 205, 114, 255), (22, 25, 29, 255), (232, 255, 215, 255)),
        "neutral": Palette((89, 91, 126, 255), (218, 170, 72, 255), (188, 205, 215, 255), (24, 24, 32, 255), (240, 232, 185, 255)),
    }
    return palettes[color_family(spec)]


def scaled(points: list[tuple[int, int]]) -> list[tuple[int, int]]:
    return [(x * SCALE, y * SCALE) for x, y in points]


def box(x0: int, y0: int, x1: int, y1: int) -> tuple[int, int, int, int]:
    return (x0 * SCALE, y0 * SCALE, x1 * SCALE, y1 * SCALE)


def line(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], fill: tuple[int, int, int, int], width: int) -> None:
    draw.line(scaled(points), fill=fill, width=width * SCALE, joint="curve")


def polygon(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], fill: tuple[int, int, int, int], outline: tuple[int, int, int, int], width: int = 14) -> None:
    draw.polygon(scaled(points), fill=fill)
    line(draw, [*points, points[0]], outline, width)


def ellipse(draw: ImageDraw.ImageDraw, bounds: tuple[int, int, int, int], fill: tuple[int, int, int, int] | None, outline: tuple[int, int, int, int], width: int = 14) -> None:
    draw.ellipse(box(*bounds), fill=fill, outline=outline, width=width * SCALE)


def rectangle(draw: ImageDraw.ImageDraw, bounds: tuple[int, int, int, int], fill: tuple[int, int, int, int], outline: tuple[int, int, int, int], width: int = 14) -> None:
    draw.rounded_rectangle(box(*bounds), radius=18 * SCALE, fill=fill, outline=outline, width=width * SCALE)


def arc(draw: ImageDraw.ImageDraw, bounds: tuple[int, int, int, int], start: int, end: int, fill: tuple[int, int, int, int], width: int = 14) -> None:
    draw.arc(box(*bounds), start=start, end=end, fill=fill, width=width * SCALE)


def symbol_kind(spec: IconSpec) -> str:
    sid = spec.skill_id.lower()
    statuses = " ".join(status.lower() for status in spec.status_ids)
    if any(token in sid for token in ("guard", "bulwark", "aegis", "warden", "sentinel", "square_wall", "anchored")) or any(token in statuses for token in ("barrier", "guarded", "unstoppable")):
        return "shield"
    if "heal" in sid or "recovery" in sid or "purifying" in sid or spec.kind == "Heal":
        return "boon"
    if any(token in sid for token in ("scout", "marksman", "hunter", "shot", "arrow", "longshot", "piercing", "signal", "ember", "lance", "snare")):
        return "arrow"
    if any(token in sid for token in ("strike", "raider", "reaver", "slayer", "blade", "cut", "riposte", "sever", "maul", "fracture", "ash_step", "heat_haze", "rusthide")):
        return "blade"
    if any(token in sid for token in ("hex", "shaman", "memory", "echo", "lattice", "phase", "prism", "savant", "shard", "siphon")):
        return "rune"
    if "passive" in sid or "support" in sid:
        return "boon"
    return "burst"


def draw_unique_marks(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    seed = stable_int(spec.skill_id)
    count = 2 + seed % 3
    for i in range(count):
        angle = (seed >> (i * 5) & 255) / 255.0 * math.tau
        radius = 130 + 25 * i
        x = int(384 + math.cos(angle) * radius)
        y = int(384 + math.sin(angle) * radius)
        if i % 2 == 0:
            ellipse(draw, (x - 18, y - 18, x + 18, y + 18), pal.light, pal.dark, 5)
        else:
            polygon(
                draw,
                [(x, y - 24), (x + 22, y + 14), (x - 22, y + 14)],
                pal.accent,
                pal.dark,
                5,
            )


def draw_shield(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    seed = stable_int(spec.skill_id)
    if seed % 2 == 0:
        pts = [(384, 120), (585, 198), (546, 514), (384, 650), (222, 514), (183, 198)]
    else:
        pts = [(384, 102), (552, 166), (604, 330), (498, 590), (384, 665), (270, 590), (164, 330), (216, 166)]
    polygon(draw, pts, pal.fill, pal.dark, 18)
    line(draw, [(384, 158), (384, 604)], pal.secondary, 14)
    line(draw, [(260, 285), (384, 215), (508, 285)], pal.accent, 16)
    line(draw, [(262, 405), (384, 510), (506, 405)], pal.light, 13)
    ellipse(draw, (314, 318, 454, 458), None, pal.accent, 12)
    draw_unique_marks(draw, spec, pal)


def draw_blade(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    seed = stable_int(spec.skill_id)
    flip = -1 if seed % 2 else 1
    blade = [(242, 602), (332, 642), (554, 160), (505, 126)]
    if flip < 0:
        blade = [(768 - x, y) for x, y in blade]
    polygon(draw, blade, pal.secondary, pal.dark, 18)
    line(draw, [(300 if flip > 0 else 468, 575), (520 if flip > 0 else 248, 190)], pal.light, 9)
    slash = [(171, 288), (214, 244), (604, 470), (580, 530)]
    if flip < 0:
        slash = [(768 - x, y) for x, y in slash]
    polygon(draw, slash, pal.accent, pal.dark, 12)
    line(draw, [(240 if flip > 0 else 528, 616), (170 if flip > 0 else 598, 686)], pal.fill, 28)
    draw_unique_marks(draw, spec, pal)


def draw_arrow(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    seed = stable_int(spec.skill_id)
    angle_flip = -1 if seed % 2 else 1
    shaft = [(180, 430), (525, 250)] if angle_flip > 0 else [(588, 430), (243, 250)]
    line(draw, shaft, pal.secondary, 42)
    line(draw, shaft, pal.dark, 16)
    head_x, head_y = shaft[-1]
    direction = 1 if angle_flip > 0 else -1
    head = [
        (head_x, head_y),
        (head_x - direction * 72, head_y - 96),
        (head_x + direction * 140, head_y - 34),
        (head_x + direction * 44, head_y + 94),
    ]
    polygon(draw, head, pal.accent, pal.dark, 16)
    ellipse(draw, (270, 260, 498, 488), None, pal.fill, 15)
    line(draw, [(302, 374), (466, 374)], pal.light, 10)
    line(draw, [(384, 292), (384, 456)], pal.light, 10)
    draw_unique_marks(draw, spec, pal)


def draw_rune(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    seed = stable_int(spec.skill_id)
    sides = 4 + seed % 3
    pts: list[tuple[int, int]] = []
    for i in range(sides):
        angle = -math.pi / 2 + math.tau * i / sides
        radius = 230 if i % 2 == 0 else 180
        pts.append((int(384 + math.cos(angle) * radius), int(384 + math.sin(angle) * radius)))
    polygon(draw, pts, pal.fill, pal.dark, 18)
    ellipse(draw, (255, 255, 513, 513), None, pal.accent, 14)
    diamond = [(384, 204), (500, 384), (384, 564), (268, 384)]
    polygon(draw, diamond, (0, 0, 0, 0), pal.secondary, 10)
    line(draw, [(300, 306), (468, 462)], pal.light, 10)
    line(draw, [(468, 306), (300, 462)], pal.light, 10)
    draw_unique_marks(draw, spec, pal)


def draw_boon(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    ellipse(draw, (168, 168, 600, 600), pal.fill, pal.dark, 18)
    ellipse(draw, (230, 230, 538, 538), None, pal.accent, 14)
    line(draw, [(384, 220), (384, 548)], pal.light, 24)
    line(draw, [(236, 384), (532, 384)], pal.light, 24)
    if "support" in spec.skill_id or "passive" in spec.skill_id:
        line(draw, [(270, 492), (384, 560), (498, 492)], pal.secondary, 18)
        line(draw, [(270, 276), (384, 208), (498, 276)], pal.secondary, 18)
    else:
        ellipse(draw, (316, 316, 452, 452), pal.secondary, pal.dark, 10)
    draw_unique_marks(draw, spec, pal)


def draw_burst(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    pts: list[tuple[int, int]] = []
    for i in range(12):
        angle = -math.pi / 2 + math.tau * i / 12
        radius = 255 if i % 2 == 0 else 120
        pts.append((int(384 + math.cos(angle) * radius), int(384 + math.sin(angle) * radius)))
    polygon(draw, pts, pal.fill, pal.dark, 16)
    ellipse(draw, (292, 292, 476, 476), pal.accent, pal.dark, 10)
    line(draw, [(384, 180), (384, 588)], pal.light, 9)
    line(draw, [(180, 384), (588, 384)], pal.light, 9)
    draw_unique_marks(draw, spec, pal)


def draw_sun_seal(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    ellipse(draw, (150, 150, 618, 618), pal.secondary, pal.dark, 18)
    ellipse(draw, (234, 234, 534, 534), pal.fill, pal.accent, 14)
    for angle in range(0, 360, 45):
        x0 = int(384 + math.cos(math.radians(angle)) * 78)
        y0 = int(384 + math.sin(math.radians(angle)) * 78)
        x1 = int(384 + math.cos(math.radians(angle)) * 218)
        y1 = int(384 + math.sin(math.radians(angle)) * 218)
        line(draw, [(x0, y0), (x1, y1)], pal.light, 11)
    ellipse(draw, (328, 328, 440, 440), pal.accent, pal.dark, 8)
    draw_unique_marks(draw, spec, pal)


def draw_wall(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    rectangle(draw, (176, 170, 592, 598), pal.fill, pal.dark, 18)
    for y in (282, 394, 506):
        line(draw, [(204, y), (564, y)], pal.secondary, 11)
    for x, y0, y1 in ((302, 170, 282), (466, 282, 394), (302, 394, 506), (466, 506, 598)):
        line(draw, [(x, y0), (x, y1)], pal.accent, 10)
    polygon(draw, [(214, 174), (384, 92), (554, 174)], pal.accent, pal.dark, 12)
    draw_unique_marks(draw, spec, pal)


def draw_bootstep(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    sole = [(248, 246), (448, 246), (548, 368), (468, 486), (292, 486), (210, 380)]
    polygon(draw, sole, pal.fill, pal.dark, 18)
    line(draw, [(282, 318), (498, 318)], pal.accent, 16)
    line(draw, [(310, 392), (468, 392)], pal.secondary, 16)
    arc(draw, (170, 462, 598, 712), 205, 335, pal.light, 18)
    arc(draw, (226, 500, 542, 690), 205, 335, pal.accent, 14)
    draw_unique_marks(draw, spec, pal)


def draw_anchor(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    line(draw, [(384, 142), (384, 562)], pal.secondary, 38)
    ellipse(draw, (314, 104, 454, 244), None, pal.dark, 18)
    line(draw, [(270, 282), (498, 282)], pal.accent, 32)
    arc(draw, (176, 302, 592, 690), 25, 155, pal.fill, 48)
    polygon(draw, [(176, 470), (246, 520), (188, 574)], pal.fill, pal.dark, 12)
    polygon(draw, [(592, 470), (522, 520), (580, 574)], pal.fill, pal.dark, 12)
    draw_unique_marks(draw, spec, pal)


def draw_banner(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    arc(draw, (134, 150, 634, 560), 205, 335, pal.accent, 34)
    left = [(264, 302), (366, 348), (342, 524), (264, 580), (186, 524), (162, 348)]
    right = [(504, 302), (606, 348), (582, 524), (504, 580), (426, 524), (402, 348)]
    polygon(draw, left, pal.fill, pal.dark, 14)
    polygon(draw, right, pal.secondary, pal.dark, 14)
    line(draw, [(226, 410), (306, 470)], pal.light, 11)
    line(draw, [(466, 410), (546, 470)], pal.accent, 11)
    draw_unique_marks(draw, spec, pal)


def draw_intercept(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    arc(draw, (130, 190, 638, 578), 205, 348, pal.accent, 44)
    polygon(draw, [(566, 250), (636, 326), (532, 354)], pal.accent, pal.dark, 12)
    polygon(draw, [(384, 268), (520, 330), (490, 560), (384, 638), (278, 560), (248, 330)], pal.fill, pal.dark, 16)
    line(draw, [(318, 438), (384, 498), (468, 366)], pal.light, 16)
    draw_unique_marks(draw, spec, pal)


def draw_crescent_blade(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    arc(draw, (160, 118, 620, 650), 112, 282, pal.secondary, 62)
    arc(draw, (252, 188, 558, 580), 118, 278, pal.fill, 34)
    polygon(draw, [(268, 144), (194, 210), (300, 228)], pal.secondary, pal.dark, 10)
    polygon(draw, [(268, 624), (194, 558), (300, 540)], pal.secondary, pal.dark, 10)
    line(draw, [(356, 208), (522, 384), (356, 560)], pal.accent, 16)
    draw_unique_marks(draw, spec, pal)


def draw_axe(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    line(draw, [(286, 596), (492, 172)], pal.secondary, 34)
    polygon(draw, [(420, 148), (610, 190), (546, 378), (426, 318)], pal.accent, pal.dark, 16)
    polygon(draw, [(402, 190), (268, 160), (306, 334), (420, 304)], pal.fill, pal.dark, 14)
    draw_unique_marks(draw, spec, pal)


def draw_leaf_arrow(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    leaf = [(220, 438), (336, 178), (574, 154), (498, 414), (338, 594)]
    polygon(draw, leaf, pal.fill, pal.dark, 16)
    line(draw, [(256, 488), (522, 206)], pal.light, 14)
    line(draw, [(160, 520), (606, 304)], pal.accent, 32)
    polygon(draw, [(606, 304), (504, 270), (536, 386)], pal.accent, pal.dark, 10)
    draw_unique_marks(draw, spec, pal)


def draw_greatblade(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    blade = [(384, 98), (500, 456), (424, 650), (344, 650), (268, 456)]
    polygon(draw, blade, pal.secondary, pal.dark, 18)
    line(draw, [(384, 150), (384, 604)], pal.light, 9)
    line(draw, [(242, 472), (526, 472)], pal.accent, 30)
    draw_unique_marks(draw, spec, pal)


def draw_path_slash(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    points = [(164, 494), (254, 374), (378, 430), (512, 262), (604, 326)]
    line(draw, points, pal.accent, 46)
    line(draw, points, pal.dark, 16)
    polygon(draw, [(590, 238), (650, 336), (532, 326)], pal.accent, pal.dark, 10)
    ellipse(draw, (180, 454, 260, 534), pal.light, pal.dark, 8)
    draw_unique_marks(draw, spec, pal)


def draw_converging_arrows(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    line(draw, [(126, 310), (372, 384)], pal.accent, 34)
    line(draw, [(642, 310), (396, 384)], pal.secondary, 34)
    polygon(draw, [(372, 384), (284, 324), (296, 440)], pal.accent, pal.dark, 10)
    polygon(draw, [(396, 384), (484, 324), (472, 440)], pal.secondary, pal.dark, 10)
    ellipse(draw, (302, 302, 466, 466), None, pal.light, 14)
    draw_unique_marks(draw, spec, pal)


def draw_distance_chevrons(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    for i, y in enumerate((238, 356, 474)):
        color = (pal.accent, pal.secondary, pal.light)[i]
        line(draw, [(188, y), (384, y - 72), (580, y)], color, 34)
    line(draw, [(384, 170), (384, 606)], pal.fill, 24)
    polygon(draw, [(384, 128), (324, 240), (444, 240)], pal.fill, pal.dark, 10)
    draw_unique_marks(draw, spec, pal)


def draw_target_mark(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    diamond = [(384, 128), (620, 384), (384, 640), (148, 384)]
    polygon(draw, diamond, pal.fill, pal.dark, 18)
    ellipse(draw, (262, 262, 506, 506), None, pal.accent, 16)
    line(draw, [(384, 206), (384, 562)], pal.light, 10)
    line(draw, [(206, 384), (562, 384)], pal.light, 10)
    polygon(draw, [(498, 212), (610, 256), (520, 324)], pal.secondary, pal.dark, 10)
    draw_unique_marks(draw, spec, pal)


def draw_linked_orbs(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    ellipse(draw, (170, 294, 338, 462), pal.fill, pal.dark, 14)
    ellipse(draw, (430, 294, 598, 462), pal.secondary, pal.dark, 14)
    line(draw, [(322, 384), (446, 384)], pal.accent, 28)
    arc(draw, (224, 184, 544, 584), 32, 148, pal.light, 18)
    arc(draw, (224, 184, 544, 584), 212, 328, pal.accent, 18)
    draw_unique_marks(draw, spec, pal)


def draw_snare(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    ellipse(draw, (190, 190, 578, 578), None, pal.accent, 34)
    ellipse(draw, (286, 286, 482, 482), None, pal.light, 16)
    line(draw, [(470, 500), (604, 632)], pal.secondary, 34)
    polygon(draw, [(596, 612), (690, 648), (620, 706)], pal.secondary, pal.dark, 10)
    draw_unique_marks(draw, spec, pal)


def draw_wind(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    line(draw, [(132, 334), (310, 242), (520, 274), (640, 180)], pal.accent, 36)
    line(draw, [(156, 452), (338, 380), (536, 414), (654, 342)], pal.secondary, 32)
    line(draw, [(220, 558), (394, 502), (548, 532)], pal.light, 24)
    polygon(draw, [(558, 142), (682, 176), (598, 260)], pal.accent, pal.dark, 10)
    draw_unique_marks(draw, spec, pal)


def draw_bow_arrow(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    arc(draw, (164, 112, 486, 656), 276, 84, pal.fill, 34)
    line(draw, [(342, 150), (342, 618)], pal.secondary, 10)
    line(draw, [(236, 384), (600, 384)], pal.accent, 34)
    polygon(draw, [(600, 384), (510, 320), (510, 448)], pal.accent, pal.dark, 12)
    draw_unique_marks(draw, spec, pal)


def draw_pierce_plates(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    rectangle(draw, (190, 260, 340, 508), pal.fill, pal.dark, 12)
    rectangle(draw, (428, 260, 578, 508), pal.fill, pal.dark, 12)
    line(draw, [(150, 384), (624, 384)], pal.accent, 42)
    polygon(draw, [(624, 384), (520, 314), (520, 454)], pal.accent, pal.dark, 12)
    line(draw, [(338, 300), (430, 468)], pal.light, 10)
    draw_unique_marks(draw, spec, pal)


def draw_prism(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    prism = [(384, 116), (566, 294), (500, 590), (268, 590), (202, 294)]
    polygon(draw, prism, pal.fill, pal.dark, 18)
    line(draw, [(384, 116), (384, 590)], pal.light, 11)
    line(draw, [(202, 294), (566, 294)], pal.secondary, 11)
    line(draw, [(268, 590), (384, 294), (500, 590)], pal.accent, 11)
    draw_unique_marks(draw, spec, pal)


def draw_echo_wave(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    polygon(draw, [(236, 270), (360, 224), (360, 544), (236, 498)], pal.fill, pal.dark, 16)
    for offset, color in ((0, pal.accent), (74, pal.secondary), (148, pal.light)):
        arc(draw, (262 + offset, 220, 546 + offset, 548), -55, 55, color, 22)
    draw_unique_marks(draw, spec, pal)


def draw_siphon(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> None:
    points: list[tuple[int, int]] = []
    for i in range(54):
        angle = i / 8.0
        radius = 12 + i * 5
        points.append((int(384 + math.cos(angle) * radius), int(384 + math.sin(angle) * radius)))
    line(draw, points, pal.accent, 30)
    line(draw, [(600, 260), (484, 330)], pal.secondary, 30)
    polygon(draw, [(610, 248), (644, 348), (540, 316)], pal.secondary, pal.dark, 10)
    ellipse(draw, (316, 316, 452, 452), pal.fill, pal.dark, 12)
    draw_unique_marks(draw, spec, pal)


def draw_skill_specific(draw: ImageDraw.ImageDraw, spec: IconSpec, pal: Palette) -> bool:
    sid = spec.skill_id
    if sid == "skill_priest_core":
        draw_sun_seal(draw, spec, pal)
    elif sid in {"skill_bulwark_core", "skill_bulwark_utility", "skill_square_wall", "skill_lattice_bastion"}:
        draw_wall(draw, spec, pal)
    elif sid == "skill_vanguard_passive_1":
        draw_wall(draw, spec, pal)
    elif sid in {"skill_vanguard_passive_2", "skill_aegis_linebreaker"}:
        draw_intercept(draw, spec, pal)
    elif sid == "skill_vanguard_support_1":
        draw_banner(draw, spec, pal)
    elif sid in {"skill_vanguard_support_2", "skill_aegis_sentinel_oath", "skill_sentinel_oath"}:
        draw_sun_seal(draw, spec, pal)
    elif sid in {"skill_warden_utility", "skill_rusthide_charge"}:
        draw_bootstep(draw, spec, pal)
    elif sid == "support_anchored":
        draw_anchor(draw, spec, pal)
    elif sid in {"skill_guardian_utility", "skill_aegis_intercept"}:
        draw_intercept(draw, spec, pal)
    elif sid in {"skill_raider_core", "skill_iron_pelt_maul"}:
        draw_axe(draw, spec, pal)
    elif sid in {"skill_reaver_core", "support_brutal"}:
        draw_crescent_blade(draw, spec, pal)
    elif sid in {"skill_slayer_core", "skill_shardblade_sever"}:
        draw_greatblade(draw, spec, pal)
    elif sid in {"skill_raider_utility", "skill_reaver_utility", "skill_slayer_utility", "skill_fracture_step", "skill_ash_step", "skill_heat_haze"}:
        draw_path_slash(draw, spec, pal)
    elif sid in {"skill_duelist_passive_1", "skill_duelist_support_1"}:
        draw_converging_arrows(draw, spec, pal)
    elif sid in {"skill_duelist_passive_2", "skill_riposte_angle"}:
        draw_target_mark(draw, spec, pal)
    elif sid == "skill_duelist_support_2":
        draw_greatblade(draw, spec, pal)
    elif sid in {"support_hunter_mark", "skill_scout_utility", "skill_precision_shot", "skill_signal_flare"}:
        draw_target_mark(draw, spec, pal)
    elif sid in {"skill_hunter_utility", "skill_refracting_snare"}:
        draw_snare(draw, spec, pal)
    elif sid in {"support_swift", "skill_glass_pathfinder"}:
        draw_wind(draw, spec, pal)
    elif sid in {"skill_scout_core", "skill_ranger_passive_1", "skill_quick_kindling"}:
        draw_leaf_arrow(draw, spec, pal)
    elif sid in {"skill_marksman_core", "skill_marksman_utility", "support_longshot", "skill_ember_arrow", "skill_prism_lance"}:
        draw_bow_arrow(draw, spec, pal)
    elif sid in {"skill_ranger_passive_2", "skill_ranger_support_1"}:
        draw_distance_chevrons(draw, spec, pal)
    elif sid in {"support_piercing", "skill_ranger_support_2"}:
        draw_pierce_plates(draw, spec, pal)
    elif sid in {"skill_hexer_core", "skill_prism_sight", "skill_phase_tether", "skill_memory_tuning", "skill_shard_memory"}:
        draw_prism(draw, spec, pal)
    elif sid in {"skill_hexer_utility", "skill_bloodless_form"}:
        draw_siphon(draw, spec, pal)
    elif sid in {"skill_shaman_core", "support_lingering", "support_echo", "skill_echo_archive", "skill_echo_resonance", "skill_lattice_listener", "skill_savant_last_word"}:
        draw_echo_wave(draw, spec, pal)
    elif sid in {"skill_shaman_utility", "skill_minor_heal", "skill_mystic_support_1", "support_purifying"}:
        draw_boon(draw, spec, pal)
    elif sid in {"skill_mystic_support_2", "skill_mystic_passive_2"}:
        draw_linked_orbs(draw, spec, pal)
    elif sid == "skill_mystic_passive_1":
        draw_prism(draw, spec, pal)
    elif sid == "support_siphon":
        draw_siphon(draw, spec, pal)
    else:
        return False
    return True


def render_icon(spec: IconSpec) -> Image.Image:
    pal = palette_for(spec)
    image = Image.new("RGBA", (ICON_SIZE * SCALE, ICON_SIZE * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    if draw_skill_specific(draw, spec, pal):
        return image.resize((ICON_SIZE, ICON_SIZE), Image.Resampling.LANCZOS)
    kind = symbol_kind(spec)
    if kind == "shield":
        draw_shield(draw, spec, pal)
    elif kind == "blade":
        draw_blade(draw, spec, pal)
    elif kind == "arrow":
        draw_arrow(draw, spec, pal)
    elif kind == "rune":
        draw_rune(draw, spec, pal)
    elif kind == "boon":
        draw_boon(draw, spec, pal)
    else:
        draw_burst(draw, spec, pal)
    return image.resize((ICON_SIZE, ICON_SIZE), Image.Resampling.LANCZOS)


def write_outputs(specs: list[IconSpec], dry_run: bool) -> tuple[int, int]:
    CATALOG_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    sheet_count = 0
    icon_count = 0
    by_subject: dict[str, list[IconSpec]] = {}
    for spec in specs:
        by_subject.setdefault(spec.subject_id, []).append(spec)

    for subject_id, sheet_specs in sorted(by_subject.items()):
        if len(sheet_specs) != 4:
            raise ValueError(f"{subject_id}: expected 4 specs, found {len(sheet_specs)}")
        icons = [(spec, render_icon(spec)) for spec in sorted(sheet_specs, key=lambda item: item.index)]
        if not dry_run:
            sheet_dir = SHEET_OUTPUT_ROOT / subject_id
            sheet_dir.mkdir(parents=True, exist_ok=True)
            sheet = Image.new("RGBA", (SHEET_SIZE, SHEET_SIZE), CHROMA)
            for spec, icon in icons:
                x = (ICON_SIZE + GAP) * (spec.index % 2)
                y = (ICON_SIZE + GAP) * (spec.index // 2)
                sheet.alpha_composite(icon, dest=(x, y))
                icon.save(CATALOG_OUTPUT_DIR / f"{spec.icon_id}.png", "PNG")
                icon_count += 1
            sheet.save(sheet_dir / "default.png", "PNG")
            sheet_count += 1
        else:
            icon_count += len(icons)
            sheet_count += 1
    return sheet_count, icon_count


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    subject_paths = sorted(SUBJECT_ROOT.glob("skill_catalog_v2_*/default.md"))
    specs = [spec for path in subject_paths for spec in load_subject_specs(path)]
    seen: set[str] = set()
    duplicates = sorted({spec.icon_id for spec in specs if spec.icon_id in seen or seen.add(spec.icon_id)})
    if duplicates:
        raise ValueError(f"duplicate icon ids in catalog subjects: {duplicates}")

    sheets, icons = write_outputs(specs, dry_run=args.dry_run)
    verb = "would materialize" if args.dry_run else "materialized"
    print(f"[materialize_skill_catalog_variant_icons] {verb} {icons} icons across {sheets} sheets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
