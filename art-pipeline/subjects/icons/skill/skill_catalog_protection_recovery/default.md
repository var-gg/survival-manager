---
slug: skill_catalog_protection_recovery--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_protection_recovery
variant: default
refs: []
aspect: "1:1"
output_size: "1568x1568"
chroma: "#FF00FF"
skills:
  - sigil_shield
  - platinum_aegis
  - ash_purification
  - faith_absent
status: prompted
---

# Skill Catalog Sheet - Protection / Recovery

Canonical skill-owned icon sheet. This subject is not character-owned and must not borrow a character portrait, body, outfit, or palette as ownership. It defines reusable `SkillId -> IconId -> Sprite` presentation symbols for protection and recovery skills.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): sigil_shield
- (1,0): platinum_aegis
- (0,1): ash_purification
- (1,1): faith_absent

Global icon rules:
- Each cell has exactly one centered focal symbol, filling about 65-75% of the cell.
- No character, no hand, no face, no body, no portrait, no scene.
- No text, no numerals, no letters, no UI frame or ring.
- Background in every cell, gutter, and margin is flat #FF00FF.
- Each symbol has a continuous 2-4 px dark outer stroke.
- Outside the outer stroke there is only pure #FF00FF: no shadow, no blur, no glow, no particles, no haze.
- Subject colors must never use magenta, hot pink, or fuchsia.

Per-cell descriptors:

(0,0) sigil_shield:
Frontal ivory and steel ward shield with a compact abstract central sigil. Warm gold inner light stays inside the shield silhouette. Continuous dark outer stroke. Reads as ritual protection.

(1,0) platinum_aegis:
Translucent platinum-gold protective dome above a small triangular ward base. No hand or human body. Clean hard silhouette with pale gold facets and contained inner glow.

(0,1) ash_purification:
Central cleansing glyph with rising amber ash motes contained inside a single oval-like silhouette. Particles remain inside the outer stroke. Reads as sacred cleansing, not fireball.

(1,1) faith_absent:
Dim fragmented ivory sigil with muted grey-gold cracks. Little to no emission, solemn and restrained. Reads as protection with the divine light gone quiet.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
