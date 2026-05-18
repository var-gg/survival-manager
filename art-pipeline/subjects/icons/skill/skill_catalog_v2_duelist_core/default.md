---
slug: skill_catalog_v2_duelist_core--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_duelist_core
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- power_strike
- raider_core
- reaver_core
- slayer_core
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_power_strike
  icon_id: skill_icon_power_strike
  slot: CoreActive
  kind: Strike
  status_ids: []
  vfx_hook_id: vfx.skill_power_strike
- skill_id: skill_raider_core
  icon_id: skill_icon_raider_core
  slot: CoreActive
  kind: Strike
  status_ids:
  - marked
  vfx_hook_id: vfx.skill_raider_core
- skill_id: skill_reaver_core
  icon_id: skill_icon_reaver_core
  slot: CoreActive
  kind: Strike
  status_ids: []
  vfx_hook_id: vfx.skill_reaver_core
- skill_id: skill_slayer_core
  icon_id: skill_icon_slayer_core
  slot: CoreActive
  kind: Strike
  status_ids:
  - bleed
  vfx_hook_id: vfx.skill_slayer_core
status: prompted
---

# Duelist Core Strikes

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_power_strike (skill_power_strike)
- (1,0): skill_icon_raider_core (skill_raider_core)
- (0,1): skill_icon_reaver_core (skill_reaver_core)
- (1,1): skill_icon_slayer_core (skill_slayer_core)

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

(0,0) skill_icon_power_strike:
single heavy diagonal sword slash, steel and crimson, direct impact. Runtime: skill_power_strike: CoreActive Strike, Melee, Physical, power 3, statuses none, effect family none.

(1,0) skill_icon_raider_core:
hooked axe fang striking downward with orange motion bite. Runtime: skill_raider_core: CoreActive Strike, Melee, Physical, power 4.2, statuses marked, effect family raider_signature.

(0,1) skill_icon_reaver_core:
crescent reaver blade with a dark red cleave trail inside the outline. Runtime: skill_reaver_core: CoreActive Strike, Melee, Physical, power 4, statuses none, effect family none.

(1,1) skill_icon_slayer_core:
executioner greatblade point with bright red pressure notch. Runtime: skill_slayer_core: CoreActive Strike, Melee, Physical, power 4.4, statuses bleed, effect family slayer_signature.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
