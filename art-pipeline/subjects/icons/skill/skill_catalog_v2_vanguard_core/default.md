---
slug: skill_catalog_v2_vanguard_core--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_vanguard_core
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- priest_core
- guardian_core
- bulwark_core
- warden_utility
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_priest_core
  icon_id: skill_icon_priest_core
  slot: CoreActive
  kind: Strike
  status_ids:
  - barrier
  vfx_hook_id: vfx.skill_priest_core
- skill_id: skill_guardian_core
  icon_id: skill_icon_guardian_core
  slot: CoreActive
  kind: Strike
  status_ids:
  - guarded
  vfx_hook_id: vfx.skill_guardian_core
- skill_id: skill_bulwark_core
  icon_id: skill_icon_bulwark_core
  slot: CoreActive
  kind: Strike
  status_ids:
  - barrier
  vfx_hook_id: vfx.skill_bulwark_core
- skill_id: skill_warden_utility
  icon_id: skill_icon_warden_utility
  slot: UtilityActive
  kind: Utility
  status_ids:
  - unstoppable
  vfx_hook_id: vfx.skill_warden_utility
status: prompted
---

# Vanguard Core / Barrier

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_priest_core (skill_priest_core)
- (1,0): skill_icon_guardian_core (skill_guardian_core)
- (0,1): skill_icon_bulwark_core (skill_bulwark_core)
- (1,1): skill_icon_warden_utility (skill_warden_utility)

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

(0,0) skill_icon_priest_core:
ivory shield sigil with a small gold sunburst and a clean barrier arc. Runtime: skill_priest_core: CoreActive Strike, Melee, Magical, power 3.8, statuses barrier, effect family priest_signature.

(1,0) skill_icon_guardian_core:
steel kite shield tilted forward with a blue guard chevron. Runtime: skill_guardian_core: CoreActive Strike, Melee, Physical, power 4, statuses guarded, effect family guard_signature.

(0,1) skill_icon_bulwark_core:
heavy tower shield impact wedge with bronze edge plates. Runtime: skill_bulwark_core: CoreActive Strike, Melee, Physical, power 4.2, statuses barrier, effect family bulwark_signature.

(1,1) skill_icon_warden_utility:
unyielding stone bootstep and short silver shockwave, no foot or body. Runtime: skill_warden_utility: UtilityActive Utility, Aura, Physical, power 0, statuses unstoppable, effect family guard_cleanse.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
