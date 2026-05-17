---
slug: skill_catalog_assault_motion--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_assault_motion
variant: default
refs: []
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - fang_strike
  - return_path
  - pack_position
  - jest_bell
status: prompted
---

# Skill Catalog Sheet - Assault / Motion

Canonical skill-owned icon sheet. This subject groups strike, return-path, positioning, and interrupt motifs by skill semantics rather than by character ownership.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): fang_strike
- (1,0): return_path
- (0,1): pack_position
- (1,1): jest_bell

Global icon rules:
- Each cell has exactly one centered focal symbol, filling about 65-75% of the cell.
- No character, no hand, no face, no body, no portrait, no scene.
- No text, no numerals, no letters, no UI frame or ring.
- Background in every cell, gutter, and margin is flat #FF00FF.
- Each symbol has a continuous 2-4 px dark outer stroke.
- Outside the outer stroke there is only pure #FF00FF: no shadow, no blur, no glow, no particles, no haze.
- Subject colors must never use magenta, hot pink, or fuchsia.

Per-cell descriptors:

(0,0) fang_strike:
Single fang-shaped blade slash, crimson and steel, with a sharp forward diagonal. Reads as direct assault, not a portrait.

(1,0) return_path:
Curved returning blade path as a boomerang-like steel arc with amber trail contained inside the stroke. Reads as strike and return.

(0,1) pack_position:
Three triangular tactical markers orbiting one central point, bronze and deep red. Abstract formation symbol, no living figures.

(1,1) jest_bell:
Small cracked bell with an interrupt glyph clapper, muted gold and red. No face, no joker hat, no text. Reads as disruptive timing.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
