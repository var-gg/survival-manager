---
slug: skill_catalog_memory_ritual--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_memory_ritual
variant: default
refs: []
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - memory_project
  - time_distance
  - voice_scar
  - external_lexicon
status: prompted
---

# Skill Catalog Sheet - Memory / Ritual

Canonical skill-owned icon sheet. This subject groups memory, time, voice, and external-knowledge motifs by skill semantics rather than by character ownership.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): memory_project
- (1,0): time_distance
- (0,1): voice_scar
- (1,1): external_lexicon

Global icon rules:
- Each cell has exactly one centered focal symbol, filling about 65-75% of the cell.
- No character, no hand, no face, no body, no portrait, no scene.
- No text, no numerals, no letters, no UI frame or ring.
- Background in every cell, gutter, and margin is flat #FF00FF.
- Each symbol has a continuous 2-4 px dark outer stroke.
- Outside the outer stroke there is only pure #FF00FF: no shadow, no blur, no glow, no particles, no haze.
- Subject colors must never use magenta, hot pink, or fuchsia.

Per-cell descriptors:

(0,0) memory_project:
Crystalline memory shard projecting a small geometric echo shape. Blue-violet crystal is allowed only as muted indigo, never magenta. Reads as projecting a stored memory.

(1,0) time_distance:
Concentric clock-ring glyph with a distant star point, no numerals. Bronze and cool silver rings, readable as separation across time.

(0,1) voice_scar:
Cracked sound-wave sigil, emerald and grey, with the fracture as the main silhouette. No mouth, no face, no letters.

(1,1) external_lexicon:
Abstract open tablet or book form with glowing glyph shapes that are not real letters. Ivory pages, teal-gold marks, hard outer stroke.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
