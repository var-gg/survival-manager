---
slug: ui_tactical_workshop--dusk
kind: environment_site
subject_id: ui_tactical_workshop
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

# UI Mockup: ui_tactical_workshop (전술 워크숍 모달 / Tactical Workshop) — dusk

Use case: V1 Town 씬의 **Tactical Workshop modal** UI 시안. 전술 / 포메이션 / Posture / per-unit Tactic을 한 화면에서 풀이하는 핵심 build management UI. modal이 Town hub 위에 떠있는 상태를 한 컷에 시각화.

```prompt
# UI Mockup: Tactical Workshop modal panel overlaid on the frontier village hub at dusk

An illustrated UI mockup screenshot of a premium mobile JRPG / gacha-banner game's tactical setup screen — anime-painted UI panel overlay on top of the frontier village hub at dusk. The frame reads as **a screenshot from a top-tier mobile RPG**, like Arknights or Honkai Star Rail in their character/squad management screens. 16:9 wide.

**STYLE LOCK (CRITICAL)**: Same illustrated world as the project's character roster splash art and the hub baseline. Two reference images attached: (1) a character portrait — the canonical painted-world stylization, and (2) the hub baseline `town_frontier_village:dusk` — the canonical dusk village backdrop. The new UI mockup must read as **the same painted world with a UI panel layered on top**. Anime-leaning painterly base with sparse cel-edge, vivid harmonious palette, hand-drawn brushwork. NOT photoreal, NOT concept-art realism, NOT a wireframe diagram.

## Composition

The illustration is a **layered composition** showing how the modal sits on top of the Town hub:

1. **Background layer (~100% frame, dimmed)** — the frontier village hub baseline (same dusk, same buildings, same frontier gate distance) but **dimmed to about 35% brightness with a soft cool tint**, so the village is recognizably there but visually quiet. This is the hub being "paused" while the player makes tactical decisions.

2. **Modal panel (center, ~75% width × ~80% height)** — a large translucent UI panel floats in front of the dimmed village. The panel has:
   - **Soft frosted-glass treatment**: warm-tinted translucent surface with a subtle steel-grey border trim
   - **Top header bar**: a thin band with a stylized title placeholder ("⬢⬢⬢⬢ ⬢⬢⬢⬢" abstract glyph blocks for the title, NOT readable text) + close button on the right (a stylized X glyph)
   - **Three vertical columns** dividing the modal interior

### Modal interior — three columns

#### Column 1 (left, ~40% of modal) — Anchor Pad

A **3×2 grid of anchor cells** rendered as soft-rimmed hexagonal or square pads. The grid represents the deployment pad — front row (3 cells, closer to the right) and back row (3 cells, further from the right). The grid is shown in slight **isometric perspective** so the pads have visible depth, like a small tabletop diorama.

In four of the six pads, **stylized character standees** stand — small painterly anime-game character figures (front view, 3/4 view, like P09 baseline standees), each clearly distinct silhouette:
- **Front-left pad**: a vanguard with shield + sword silhouette (steel-grey + warm wood tones)
- **Front-center pad**: a duelist with twin blade silhouette (leaner figure, dark navy + gold trim)
- **Back-left pad**: a ranger with bow silhouette (forest green + leather brown)
- **Back-right pad**: a mystic with staff silhouette (deep violet robe + crystal staff glow)

The two empty pads are visibly empty (faint pad outline only, no figure).

**Threat-answer lines**: from outside the pad (the right edge of the grid, where enemies would spawn), thin **stylized colored arcs** trace toward each unit — a red arc from "frontline pressure" zone curving to the vanguard, a yellow arc from "ranged threat" curving to the ranger, etc. The arcs read as **painterly brushstrokes**, not engineering CAD lines.

Above the pad: a small label placeholder ("⬢⬢⬢⬢⬢" abstract glyph, NOT readable text).

#### Column 2 (center, ~30% of modal) — Decision Cards

Stacked vertically, two card sections:

**Top — Posture Cards (3 cards in a horizontal row)**:
Each card is a small painterly illustration showing the formation visually:
- **Card 1 (방어 / Defensive)**: a tank figure crouches behind a raised shield, two figures behind it — fortress feel. Steel-grey + cool tone.
- **Card 2 (균형 / Balanced)** — currently highlighted with a warm gold rim glow: figures spaced evenly across the pad. Mid-tone palette.
- **Card 3 (돌격 / Aggressive)**: a dive figure springs forward toward implied enemy line, others follow. Warm orange + red tone.

The cards are mini-illustrations, **not slider toggles** — each is a tiny scene that visually shows what the posture means.

**Bottom — Per-unit Tactic Cards (4 stacked cards)**:
Each tactic card is a horizontal strip showing:
- Left edge: small **portrait token** of the unit (small face crop, anime-painted)
- Middle: a single line of **placeholder hook text** (abstract glyph blocks, NOT readable)
- Right edge: a small **icon glyph** indicating the trigger (a heart for HP threshold, a sword for burst, an arrow for priority target, etc.)

The cards stack neatly, each a thin strip. NO dropdown depth, NO condition tree — each card is one line, one decision.

#### Column 3 (right, ~30% of modal) — Live Preview + Threat Chart

**Top — Live Preview window (~60% of column height)**:
A small rectangular frame showing **a miniature combat scene** — the same anchor pad now zoomed slightly with a few enemy figures painted in from the right edge mid-engagement. The scene is **in motion** — a slash effect, a projectile arc, a healing glow. This is the "5-second sample combat" preview rendered as one suggestive painterly snapshot. Above the frame: a small label placeholder ("⬢⬢⬢⬢" abstract glyph).

Three small tabs above the preview frame: **placeholder labels** for the three sandbox scenarios (anti-burst / endgame fortress / balanced opening) — abstract glyph blocks, NOT readable.

**Bottom — Threat-Answer Hexagon Chart (~40% of column height)**:
A stylized hexagonal radar chart with **8 vertices labeled with small icon glyphs** (each glyph representing a threat lane — burst / sustain / control / swarm / dive / pierce / heal / summon-pressure). The chart fills with a soft warm-toned area showing current squad coverage. Below the chart: 3-4 **synergy chips** (small rounded rectangles with a stylized icon + abstract glyph label, one chip glowing softly to indicate active synergy).

## Mood

**Confident clarity.** The modal feels like a craftsman's workbench — every decision visible at a glance, no hidden depth, no abstract slider. The dusk village in the background is paused, watchful. The player should feel "I can see what I'm building, and I can see why it works."

## Lighting

- Background village: dimmed dusk LUT, ~35% brightness, cool slate tint
- Modal panel: warm-tinted translucent glass, soft inner glow from a slightly warm light source above
- Card highlights: gold rim on selected card (Balanced posture), subtle warm glow on active synergy chip
- Anchor pad: small warm glow under each occupied pad, subtle cool glow under empty pads
- Live preview window: warm/cool contrast inside (warm key on hero side, cool from enemy side)

## Palette

- Steel grey + weathered wood + deep navy (modal frame, hub backdrop) — dominant
- Warm orange-gold (selected card rim, active synergy chip, character warm accents) — restrained warm anchors
- Soft pearl/ivory (modal frosted glass) — neutral
- Cool slate-blue (dimmed background, empty pad outline) — atmospheric
- Painterly anime palette throughout — vivid harmonious, NOT muted concept-art

## Layout proportions (CRITICAL — readable hierarchy)

The frame must read at a glance as a layered composition:
- **Background (35% brightness)**: village + frontier gate + dusk sky, recognizable but quiet
- **Modal panel (75% width × 80% height, centered)**: dominant frame, frosted glass, three columns clearly divided
- **Column 1 (left 40% of modal)**: anchor pad with 4 standees + threat-answer arcs — visually busiest
- **Column 2 (middle 30%)**: posture cards (top, 3 horizontal) + tactic cards (bottom, 4 vertical strips)
- **Column 3 (right 30%)**: live preview window (top 60%) + hexagon chart + synergy chips (bottom 40%)

## Reproduction feasibility (CRITICAL)

The illustration must look like a stylized painted UI mockup — recreatable in a real game's UI rendering pipeline.
- Modal panel: simple frosted-glass shape with subtle border, NOT intricate ornament.
- Cards: stylized illustrated card shapes, NOT photoreal trading card detail.
- Standees: simplified painterly figures, NOT individually rendered character portraits.
- Hexagon chart: clean stylized vector shape with soft fill, NOT photoreal data viz.
- Threat-answer arcs: painterly brushstrokes, NOT engineering CAD lines.
- All "text" is **abstract glyph blocks** — placeholder shapes that suggest text without being readable.

## What this is NOT

- NOT a wireframe diagram (no boxes-and-lines technical drawing)
- NOT a photorealistic UI screenshot (this is anime-painted illustration)
- NOT a real working UI with legible text (all text is abstract glyph blocks)
- NOT character portrait close-up (standees are small figures inside the anchor pad, not foreground portraits)
- NOT cluttered (each column reads cleanly, three sections distinct)
- NOT full opaque modal (the village must remain visible at ~35% brightness behind the frosted glass)
- NOT a different stylization from the hub baseline — same painted world, just with a UI layer
- NOT magical glowing runes / fantasy ornament — the UI is **modern painterly fantasy game UI**, not enchanted parchment
```
