---
slug: skill_catalog_v2_duelist_motion--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_duelist_motion
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- raider_utility
- reaver_utility
- slayer_utility
- support_executioner
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_raider_utility
  icon_id: skill_icon_raider_utility
  slot: UtilityActive
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.skill_raider_utility
- skill_id: skill_reaver_utility
  icon_id: skill_icon_reaver_utility
  slot: UtilityActive
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.skill_reaver_utility
- skill_id: skill_slayer_utility
  icon_id: skill_icon_slayer_utility
  slot: UtilityActive
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.skill_slayer_utility
- skill_id: support_executioner
  icon_id: skill_icon_support_executioner
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_executioner
status: prompted
---

# Duelist Motion

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_raider_utility (skill_raider_utility)
- (1,0): skill_icon_reaver_utility (skill_reaver_utility)
- (0,1): skill_icon_slayer_utility (skill_slayer_utility)
- (1,1): skill_icon_support_executioner (support_executioner)

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

(0,0) skill_icon_raider_utility:
returning hooked path, bronze arc curving back to its start. Runtime: skill_raider_utility: UtilityActive Utility, Melee, Physical, power 0, statuses none, effect family mark_followup.

(1,0) skill_icon_reaver_utility:
reposition slash trail, two steel arcs crossing like a sidestep. Runtime: skill_reaver_utility: UtilityActive Utility, Melee, Physical, power 0, statuses none, effect family burst_followup.

(0,1) skill_icon_slayer_utility:
short lunge arrow embedded in a blade edge, aggressive forward motion. Runtime: skill_slayer_utility: UtilityActive Utility, Melee, Physical, power 0, statuses none, effect family bleed_followup.

(1,1) skill_icon_support_executioner:
clean execution mark, blade tip over a small cracked target diamond. Runtime: support_executioner: Support Utility, Aura, Physical, power 0, statuses none, effect family executioner_support.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
