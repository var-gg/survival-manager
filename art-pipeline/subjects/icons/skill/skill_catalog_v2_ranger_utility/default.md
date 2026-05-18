---
slug: skill_catalog_v2_ranger_utility--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_ranger_utility
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- scout_utility
- marksman_utility
- hunter_utility
- support_swift
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_scout_utility
  icon_id: skill_icon_scout_utility
  slot: UtilityActive
  kind: Utility
  status_ids:
  - exposed
  vfx_hook_id: vfx.skill_scout_utility
- skill_id: skill_marksman_utility
  icon_id: skill_icon_marksman_utility
  slot: UtilityActive
  kind: Utility
  status_ids:
  - sunder
  vfx_hook_id: vfx.skill_marksman_utility
- skill_id: skill_hunter_utility
  icon_id: skill_icon_hunter_utility
  slot: UtilityActive
  kind: Utility
  status_ids:
  - slow
  vfx_hook_id: vfx.skill_hunter_utility
- skill_id: support_swift
  icon_id: skill_icon_support_swift
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_swift
status: prompted
---

# Ranger Utility

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_scout_utility (skill_scout_utility)
- (1,0): skill_icon_marksman_utility (skill_marksman_utility)
- (0,1): skill_icon_hunter_utility (skill_hunter_utility)
- (1,1): skill_icon_support_swift (support_swift)

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

(0,0) skill_icon_scout_utility:
exposed target marker, split green target plate opening at the center. Runtime: skill_scout_utility: UtilityActive Utility, Aura, Physical, power 0, statuses exposed, effect family scout_exposed.

(1,0) skill_icon_marksman_utility:
sunder arrowhead cracking a small armor plate. Runtime: skill_marksman_utility: UtilityActive Utility, Aura, Physical, power 0, statuses sunder, effect family marksman_pierce.

(0,1) skill_icon_hunter_utility:
slowing snare wind glyph, green cord loop with a small weighted dart. Runtime: skill_hunter_utility: UtilityActive Utility, Aura, Physical, power 0, statuses slow, effect family hunter_mark.

(1,1) skill_icon_support_swift:
swift footless wind streak, teal feather-like motion slash with no body. Runtime: support_swift: Support Utility, Aura, Physical, power 0, statuses none, effect family swift_support.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
