---
slug: ui_equipment_refit--dusk
kind: environment_site
subject_id: ui_equipment_refit
variant: dusk
emotion: default
refs:
  - hero_dawn_priest:portrait_full
  - town_frontier_village:dusk
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: idea
---

# UI Mockup: ui_equipment_refit (장비 / Refit modal) — dusk

Use case: V1 Town 씬의 **Equipment / Refit modal** UI 시안. 3-slot 장비 (weapon / armor / accessory) + Echo 1-affix 재굴림 preview 풀이.

```prompt
# UI Mockup: Equipment & Refit modal panel overlaid on the frontier village hub at dusk

An illustrated UI mockup screenshot of a premium mobile JRPG / gacha-banner game's equipment management screen — anime-painted UI panel overlay on top of the frontier village hub at dusk. Reads as a screenshot from a top-tier mobile RPG, like Arknights or Honkai Star Rail in their gear/loadout screens. 16:9 wide.

**STYLE LOCK (CRITICAL)**: Same illustrated world as the project's character roster splash art and the hub baseline. Two reference images attached: (1) a character portrait — the canonical painted-world stylization, and (2) the hub baseline `town_frontier_village:dusk` — the canonical dusk village backdrop. The new UI mockup must read as the same painted world with a UI panel layered on top. Anime-leaning painterly base, sparse cel-edge, vivid harmonious palette, hand-drawn brushwork. NOT photoreal, NOT concept-art realism, NOT a wireframe diagram.

## Composition

Layered composition: dimmed Town hub backdrop + frosted glass modal panel.

1. **Background layer (~100% frame, dimmed ~35% brightness)** — frontier village hub at dusk, recognizable but quiet. Cool slate tint.

2. **Modal panel (center, ~75% width × ~80% height)** — translucent frosted-glass UI panel. Soft warm-tinted with steel-grey border trim. Top header bar with a stylized title placeholder (abstract glyph blocks "⬢⬢⬢⬢ ⬢⬢⬢⬢", NOT readable text) + close X button on right.

### Modal interior — three sections

#### Section 1 (left, ~30% of modal) — Character Portrait + Slot Stack

A **character standee** (3/4 view, vanguard archetype with shield and sword, anime-painted, P09 baseline style) stands on the left side of the modal, mid-height. Below the portrait: small placeholder for character name (abstract glyph "⬢⬢ ⬢⬢⬢").

To the right of the portrait, **3 vertical equipment slot tiles** stacked top-to-bottom:
- **Top slot — Weapon**: a hexagonal slot frame containing a stylized illustrated sword icon (warm steel + gold trim), with a small rarity gem in the corner (epic-purple). Below the slot: a small placeholder text strip showing item name + tier (abstract glyphs).
- **Middle slot — Armor**: hexagonal slot with a stylized illustrated chest plate icon (steel-grey + leather brown), rarity gem (rare-blue).
- **Bottom slot — Accessory**: hexagonal slot with a stylized illustrated amulet/ring icon (gold + small glowing gem), rarity gem (common-grey).

Each slot has a soft cool inner glow when not selected, warm gold rim when selected (currently the **Weapon slot is selected** — gold rim glow).

#### Section 2 (center, ~40% of modal) — Affix Detail + Refit Action

The center column shows the **selected weapon's detail card**:

**Top half — Affix list** (4-5 affix lines stacked):
Each affix line is a horizontal strip with:
- Left: small icon glyph indicating affix type (+attack, +crit, +pierce, etc.)
- Middle: placeholder text strip (abstract glyphs)
- Right: numeric value placeholder (small abstract digit blocks)

The **second affix from top is highlighted** with a warm gold rim — this is the affix targeted for refit. Other affixes are dim grey rim.

**Bottom half — Refit action panel**:
A small framed panel with:
- Left: **before/after preview** — two columns showing the highlighted affix's current value vs. the rolled value placeholder. A subtle warm glow between them suggesting the reroll motion.
- Right: **Echo cost display** — a stylized Echo currency icon (glowing crystal shard, cool teal-violet) + numeric placeholder. Below it: a **Refit button** (rectangular tile with stylized text placeholder "⬢⬢⬢⬢", warm gold fill, slight inner glow, like an action button in a premium mobile RPG).

A small caption strip at the bottom: warning placeholder text ("⬢⬢⬢⬢ ⬢⬢⬢⬢ ⬢⬢" — implying "Echo로 affix 1개만 재굴림" caveat).

#### Section 3 (right, ~30% of modal) — Inventory Pool

A vertical scroll list of **inventory items** that fit the selected slot category (weapons). Each entry is a horizontal strip:
- Left: small hexagonal item icon (sword variants, painterly anime style)
- Middle: placeholder name strip + tier glyph
- Right: small rarity gem dot

About 6-8 items visible in the scroll, with 2-3 items showing visual variety (sword / spear / dagger silhouettes). The currently equipped item is at the top with a soft gold rim. Below the inventory list: a small filter/sort toolbar (abstract glyph dots).

A scroll bar on the right edge of the inventory column.

## Mood

**Workshop confidence.** The modal feels like a craftsman bench — the character is right there, the gear is in clear slots, the affix to be re-rolled is highlighted, the Echo cost is visible. Every decision is one glance away. The dusk village in the background is paused, watchful. The player should feel "I see what I have, what I can change, what it costs."

## Lighting

- Background village: dimmed dusk, ~35% brightness, cool slate
- Modal panel: warm-tinted translucent glass, soft inner glow from above
- Selected slot (weapon): warm gold rim glow
- Selected affix: warm gold rim glow
- Refit button: warm gold fill with subtle inner glow (call-to-action feel)
- Echo currency icon: cool teal-violet glow (distinct from warm gold)
- Inventory list: warm parchment-tone background panel

## Palette

- Steel grey + weathered wood + deep navy (modal frame, hub backdrop)
- Warm orange-gold (selected highlights, refit button, character warm accents)
- Soft pearl/ivory (modal frosted glass)
- Cool slate-blue (dimmed background)
- Cool teal-violet (Echo currency)
- Rarity gem accents: epic-purple / rare-blue / common-grey
- Painterly anime palette — vivid harmonious, NOT muted concept-art

## Layout proportions (CRITICAL)

- Background dim 35%
- Modal panel 75% × 80%, centered
- Section 1 (left 30%): character standee + 3 slot stack
- Section 2 (center 40%): affix detail + refit action
- Section 3 (right 30%): inventory pool list
- Three sections clearly divided with subtle vertical rule lines

## Reproduction feasibility (CRITICAL)

Stylized painted UI mockup recreatable in a real game's UI rendering pipeline:
- Modal panel: simple frosted-glass shape, NOT intricate ornament
- Slot tiles: stylized hexagonal frames with item icons, NOT photoreal trading card detail
- Item icons: painterly anime icon shapes, NOT realistic photo objects
- Affix lines: stylized strips with icon + placeholder, NOT spreadsheet-grid look
- Refit button: simple call-to-action rectangle, NOT skeuomorphic 3D button
- All "text" is abstract glyph blocks

## What this is NOT

- NOT a wireframe diagram
- NOT a photorealistic UI screenshot — anime-painted illustration
- NOT a real working UI with legible text — all text is abstract glyph blocks
- NOT character portrait close-up (standee is small figure, fits in left section)
- NOT cluttered (each section reads cleanly)
- NOT full opaque modal (village must remain visible at ~35% brightness)
- NOT a different stylization from the hub baseline
- NOT magical glowing runes / fantasy ornament — modern painterly fantasy game UI
```
