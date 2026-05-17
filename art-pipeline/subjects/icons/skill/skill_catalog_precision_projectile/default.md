---
slug: skill_catalog_precision_projectile--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_precision_projectile
variant: default
refs: []
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - knot_arrow
  - wind_read
  - weathering_pause
  - dormant_ward
status: prompted
---

# Skill Catalog Sheet - Precision / Projectile

Canonical skill-owned icon sheet. This subject groups projectile, prediction, and delaying-control motifs by skill semantics rather than by character ownership.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): knot_arrow
- (1,0): wind_read
- (0,1): weathering_pause
- (1,1): dormant_ward

Global icon rules:
- Each cell has exactly one centered focal symbol, filling about 65-75% of the cell.
- No character, no hand, no face, no body, no portrait, no scene.
- No text, no numerals, no letters, no UI frame or ring.
- Background in every cell, gutter, and margin is flat #FF00FF.
- Each symbol has a continuous 2-4 px dark outer stroke.
- Outside the outer stroke there is only pure #FF00FF: no shadow, no blur, no glow, no particles, no haze.
- Subject colors must never use magenta, hot pink, or fuchsia.

Per-cell descriptors:

(0,0) knot_arrow:
Single knotted arrowhead with a taut bowstring glyph. Teal steel body, pale gold notch, compact readable silhouette. Reads as precise bound shot.

(1,0) wind_read:
Curved wind ribbon wrapping a small compass needle. Cyan-green accents and dark steel core. Motion is contained inside the outer stroke.

(0,1) weathering_pause:
Eroded hourglass and pause-bar glyph merged into one object. Blue-grey stone with thin gold edge wear. Reads as time delay through weathering.

(1,1) dormant_ward:
Sleeping ward stone with a closed teal rune shell. Low light, quiet defensive shape, no face or creature motif.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
