---
slug: ui_recruit--dusk
kind: environment_site
subject_id: ui_recruit
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

# UI Mockup: ui_recruit (영입 / Recruit modal) — dusk

Use case: V1 Town 씬의 **Recruit modal** UI 시안. 4-slot recruit pack + protected/refresh/scout 풀이. 가챠 ceremony 약간 강한 톤.

```prompt
# UI Mockup: Recruit modal panel overlaid on the frontier village hub at dusk

An illustrated UI mockup screenshot of a premium mobile JRPG / gacha-banner game's recruitment screen — anime-painted UI panel overlay on top of the frontier village hub at dusk. The recruitment screen has a slightly more **ceremonial weight** than other modals — this is where new heroes join the squad. Reads as a screenshot from a top-tier mobile RPG, like Arknights's recruitment screen or Honkai Star Rail's warp screen, but more intimate (4 candidates, not full gacha). 16:9 wide.

**STYLE LOCK (CRITICAL)**: Same illustrated world as the project's character roster splash art and the hub baseline. Two reference images attached: (1) a character portrait — the canonical painted-world stylization, and (2) the hub baseline `town_frontier_village:dusk` — the canonical dusk village backdrop. The new UI mockup must read as the same painted world with a UI panel layered on top. Anime-leaning painterly base, sparse cel-edge, vivid harmonious palette, hand-drawn brushwork. NOT photoreal, NOT concept-art realism, NOT a wireframe diagram.

## Composition

Layered composition with slightly more ceremonial framing than other modals.

1. **Background layer (~100% frame, dimmed ~30% brightness)** — frontier village hub at dusk, slightly more dimmed than other modals (this is a ceremony moment), cool slate tint, subtle warm glow from the village lanterns suggesting "the village is watching".

2. **Modal panel (center, ~80% width × ~85% height)** — translucent frosted-glass UI panel slightly larger than other modals. Warm-tinted with deeper navy + subtle gold trim (ceremony accent). Top header bar with stylized title placeholder + close X button.

### Modal interior — recruit pack ceremony

#### Section 1 (top, ~70% of modal height) — 4 Candidate Cards in a row

Four large vertical recruit cards arranged horizontally in a row, each card occupying ~22% of modal width. Each card is a **portrait-style recruitment card**:

- **Card frame**: warm parchment / deep navy frame with stylized corner ornament (small triangular flourish), subtle gold rim
- **Top of card (~70%)**: a stylized **character portrait** (painterly anime, 3/4 or front view, head + upper torso) — each of the four candidates is a clearly distinct silhouette and class:
  - Card 1: a vanguard archetype (steel armor + shield silhouette, warm wood + steel-grey palette)
  - Card 2: a duelist archetype (lean figure + twin daggers, dark navy + silver palette)
  - Card 3: a ranger archetype (cloak + bow, forest green + leather brown palette)
  - Card 4: a mystic archetype (robe + crystal staff, deep violet + soft glow palette)
- **Bottom of card (~30%)**: a small info strip with placeholder name + class glyph + a row of small synergy chip icons (race + class) + small star/tier indicators (abstract glyph blocks)

Each card has a faint protective lock icon in the corner — three cards have unlocked icon, **one card (the duelist, card 2) has a glowing lock indicating "protected"** — held over from previous refresh.

The cards float slightly forward of the modal frame with subtle warm glow underneath each, like exhibits on display at a ceremonial table.

#### Section 2 (bottom, ~30% of modal height) — Action bar

A horizontal action bar at the bottom of the modal, divided into three sub-zones:

**Left zone** (~30%): **Scout indicator** — a small painterly icon of a hooded scout figure with a stylized arrow + a small caption strip placeholder (abstract glyphs). Suggests "reroll candidate hint" but small.

**Center zone** (~40%): **Refresh button** — a large prominent action button (warm gold fill, deeper inner glow, the most call-to-action element in the frame) with a stylized refresh icon (circular arrow) + placeholder text "⬢⬢⬢⬢ ⬢⬢⬢" (implying "다시 모집" or similar). Below it, a small cost indicator (gold currency icon + numeric placeholder).

**Right zone** (~30%): **Recruit button** — another action button (slightly less prominent than refresh, deep navy with gold trim) with a placeholder name strip showing "currently selected card" and a confirm checkmark glyph. Smaller cost indicator below.

A small caption at the bottom: protect/refresh policy hint (abstract glyphs).

## Mood

**Quiet ceremony.** The recruit screen feels like a moment of decision — the four candidates wait, the village is paused, the player chooses who joins the cause. Slightly more weight than a regular modal, but not full gacha spectacle (no rainbow particle explosion, no 10-pull animation). Intimate, deliberate. The Solarum frontier village backdrop subtly reminds the player "these candidates are people who agreed to walk into the wilderness with you."

## Lighting

- Background village: dimmed dusk, ~30% brightness (slightly more dim than other modals for ceremony)
- Modal panel: warm-tinted translucent glass with deeper navy trim
- Each candidate card: subtle warm glow underneath, suggesting display lighting
- Protected card: extra warm gold inner rim
- Refresh button: prominent warm gold inner glow (most called-out element)
- Recruit button: deep navy with gold trim (secondary CTA)
- Scout icon: cool grey with small warm hint

## Palette

- Steel grey + weathered wood + deep navy (modal frame, hub backdrop)
- Warm parchment / cream (card body) — ceremony anchor
- Warm orange-gold (refresh button, protected card rim, accent gold)
- Soft pearl/ivory (modal frosted glass)
- Cool slate-blue (dimmed background)
- Each candidate's faction palette: steel-grey/wood (vanguard) / dark navy + silver (duelist) / forest green + leather (ranger) / deep violet + soft glow (mystic)
- Painterly anime palette — vivid harmonious

## Layout proportions (CRITICAL)

- Background dim 30%
- Modal panel 80% × 85%, centered
- Top section (70% of modal height): 4 candidate cards in row, each ~22% modal width
- Bottom section (30% of modal height): action bar with scout / refresh / recruit zones

## Reproduction feasibility (CRITICAL)

Stylized painted UI mockup recreatable in a real game's UI pipeline:
- Modal panel: frosted-glass with subtle gold trim, NOT ornate filigree
- Candidate cards: stylized vertical card shapes with painterly portraits, NOT trading-card-game level ornament
- Character portraits inside cards: simplified painterly anime figures, NOT individual finished character splashes
- Action buttons: clean call-to-action rectangles, NOT skeuomorphic 3D
- All "text" is abstract glyph blocks

## What this is NOT

- NOT a wireframe diagram
- NOT a photorealistic UI screenshot — anime-painted illustration
- NOT a real working UI with legible text
- NOT a full character splash showcase (portraits are simplified, fit inside cards)
- NOT a 10-pull gacha screen with rainbow particle explosion (this is intimate 4-pack)
- NOT cluttered — 4 cards + 3-zone action bar reads cleanly
- NOT full opaque modal (village must remain visible at ~30% brightness)
- NOT a different stylization from the hub baseline
- NOT magical glowing runes / fantasy ornament — modern painterly fantasy game UI with subtle ceremonial accent
```
