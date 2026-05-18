---
slug: skill_catalog_v2_ranger_boons--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_ranger_boons
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- ranger_passive_1
- ranger_passive_2
- ranger_support_1
- ranger_support_2
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_ranger_passive_1
  icon_id: skill_icon_ranger_passive_1
  slot: Passive
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_ranger_passive_1
- skill_id: skill_ranger_passive_2
  icon_id: skill_icon_ranger_passive_2
  slot: Passive
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_ranger_passive_2
- skill_id: skill_ranger_support_1
  icon_id: skill_icon_ranger_support_1
  slot: Support
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_ranger_support_1
- skill_id: skill_ranger_support_2
  icon_id: skill_icon_ranger_support_2
  slot: Support
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_ranger_support_2
status: prompted
---

# Ranger Passive / Support

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_ranger_passive_1 (skill_ranger_passive_1)
- (1,0): skill_icon_ranger_passive_2 (skill_ranger_passive_2)
- (0,1): skill_icon_ranger_support_1 (skill_ranger_support_1)
- (1,1): skill_icon_ranger_support_2 (skill_ranger_support_2)

Global icon rules:
- Each cell has exactly one centered focal symbol, filling about 65-75% of the cell.
- No character, no hand, no face, no body, no portrait, no scene.
- No text, no numerals, no letters, no UI frame or ring.
- Background in every cell, gutter, and margin is flat #FF00FF.
- Each symbol has a continuous 2-4 px dark outer stroke.
- Outside the outer stroke there is only pure #FF00FF: no shadow, no blur, no glow, no particles, no haze.
- Subject colors must never use magenta, hot pink, or fuchsia.
- Icons in this sheet should share a coherent painterly game style, but each symbol must be visibly distinct at 64 px.

Per-cell descriptors:

(0,0) skill_icon_ranger_passive_1:
ranger patience glyph, arrow resting over a quiet green leaf-shaped notch. Runtime: skill_ranger_passive_1: Passive Buff, Aura, Physical, power 0, statuses none, effect family none.

(1,0) skill_icon_ranger_passive_2:
range advantage chevron, stacked arrow distance markers without numbers. Runtime: skill_ranger_passive_2: Passive Buff, Aura, Physical, power 0, statuses none, effect family none.

(0,1) skill_icon_ranger_support_1:
ranger support knot, two small arrows converging on one target diamond. Runtime: skill_ranger_support_1: Support Buff, Aura, Physical, power 0, statuses none, effect family hunter_support.

(1,1) skill_icon_ranger_support_2:
piercing support seal, arrowhead threading through layered plates. Runtime: skill_ranger_support_2: Support Buff, Aura, Physical, power 0, statuses none, effect family scout_support.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
