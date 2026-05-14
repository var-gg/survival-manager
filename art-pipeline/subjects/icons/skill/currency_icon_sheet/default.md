---
slug: currency_icon_sheet--default
kind: skill_icon
subject_id: currency_icon_sheet
variant: default
emotion: default
refs: []
aspect: "2:1"
output_size: "1024x512"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: currency_icon_sheet (2 currency icons / 2 재화 아이콘) — sheet

Use case: UI currency header — Gold (금화) + Echo (메아리 크리스탈). Town의 Inventory tab + Equipment Refit (Echo cost) + Recruit modal + Permanent Augment modal cost indicator 등에 재사용.

## Output target (sheet_split 분할 후)

- `art-pipeline/output/icons/currency/currency_gold.png`
- `art-pipeline/output/icons/currency/currency_echo.png`

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/currency_icon_sheet/default.png `
    --rows 1 --cols 2 `
    --emotions gold,echo `
    --out-dir art-pipeline/output/icons/currency/ `
    --prefix currency
```

```prompt
# Icon Sheet: 2 currency icons on a single sheet — for sprite atlas

A 2-cell horizontal sheet of stylized currency icons for a premium mobile JRPG / gacha-banner game's UI. Same painterly anime icon style as the class icon sheet — clean stylized silhouette + painterly base + sparse cel-edge at hard edges. NOT photoreal, NOT realistic, NOT chibi. Each cell contains a single currency item centered on solid #FF00FF magenta background.

## Sheet structure (CRITICAL)

- **1024×512 canvas = 1 row × 2 columns = 2 cells**
- Each cell ~480×480 active icon area
- **Magenta gutter (#FF00FF, ~30-50px wide)** between cells and on outer edges
- Each cell pure #FF00FF background — NO gradient, NO tint
- Icon centered with breathing room ~60-80px from cell edges

## Cell contents (reading order: left-to-right)

### Cell 1: Gold (금화) — Stack of gold coins
A small stack of 3 stylized **gold coins**, slightly fanned out for depth, viewed from above-front 3/4 angle. Warm metallic gold (`#e6b751` family, Foundation token `--gold-300`) with subtle painterly highlights. The top coin has a **simple stylized sunburst** mark embossed at its center (matching Solarum frontier currency). Slight warm orange glow underneath suggesting value. NO realistic coin engraving detail — clean iconic shape.

### Cell 2: Echo (메아리 크리스탈) — Crystal shard
A single **violet-teal crystal shard**, slightly faceted, vertical orientation, faintly glowing from within. Cool teal-violet hue (`#8a5cb8` Mystic family-adjacent, but cooler), with **inner radiance** suggesting memory / echo essence. Crystal faceting simple — 5-6 visible flat planes, not photoreal gem cutting. **Soft particle hint** floating around (1-2 tiny floating motes) implying magical resonance. NOT a full magical effect — minimal mystic suggestion.

## Style consistency with class icon sheet baseline

Both currency icons must read at the same scale and weight as the 4 class icons (`class_vanguard.png` etc.) so the UI feels unified. Same:
- Hard outline weight + cel-edge at hard geometric edges
- Painterly base for organic surfaces (coin metallic, crystal facet)
- Single iconic silhouette centered per cell
- Magenta background strict

## Color discipline

- Gold cell: warm metallic gold + subtle orange undertone
- Echo cell: cool teal-violet + faint inner glow
- Magenta background **strict** — NO tint into icon
- NO magenta hue on icon (chroma key forbidden)

## What this is NOT

- NOT a realistic coin photograph or gem photograph
- NOT a magical sparkle explosion — minimal mystic hint only
- NOT ornate filigree — clean iconic
- NOT gradient background — strict flat #FF00FF
- NOT different style per cell — both in same painterly anime icon style
- NOT readable text on coin (no numbers, no letters)
- NOT cluttered — single subject centered, breathing room
```
