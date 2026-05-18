---
slug: skill_catalog_v2_ranger_core--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_ranger_core
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- scout_core
- marksman_core
- precision_shot
- support_hunter_mark
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_scout_core
  icon_id: skill_icon_scout_core
  slot: CoreActive
  kind: Strike
  status_ids: []
  vfx_hook_id: vfx.skill_scout_core
- skill_id: skill_marksman_core
  icon_id: skill_icon_marksman_core
  slot: CoreActive
  kind: Strike
  status_ids: []
  vfx_hook_id: vfx.skill_marksman_core
- skill_id: skill_precision_shot
  icon_id: skill_icon_precision_shot
  slot: CoreActive
  kind: Strike
  status_ids: []
  vfx_hook_id: vfx.skill_precision_shot
- skill_id: support_hunter_mark
  icon_id: skill_icon_support_hunter_mark
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_hunter_mark
status: prompted
---

# Ranger Core Shots

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_scout_core (skill_scout_core)
- (1,0): skill_icon_marksman_core (skill_marksman_core)
- (0,1): skill_icon_precision_shot (skill_precision_shot)
- (1,1): skill_icon_support_hunter_mark (support_hunter_mark)

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

(0,0) skill_icon_scout_core:
light scout arrowhead with green wind notch and minimal bow curve. Runtime: skill_scout_core: CoreActive Strike, Projectile, Physical, power 3.7, statuses none, effect family none.

(1,0) skill_icon_marksman_core:
precise longbow arrow aligned through a small blue aiming diamond. Runtime: skill_marksman_core: CoreActive Strike, Projectile, Physical, power 4.1, statuses none, effect family none.

(0,1) skill_icon_precision_shot:
thin silver arrow piercing a tiny gold focus ring, no UI frame. Runtime: skill_precision_shot: CoreActive Strike, Projectile, Physical, power 2, statuses none, effect family none.

(1,1) skill_icon_support_hunter_mark:
hunter mark diamond with an arrow notch and amber tracking dot. Runtime: support_hunter_mark: Support Utility, Aura, Physical, power 0, statuses none, effect family hunter_mark_support.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
