---
slug: skill_catalog_v2_cross_support--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_cross_support
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- support_brutal
- support_longshot
- support_piercing
- support_purifying
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: support_brutal
  icon_id: skill_icon_support_brutal
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_brutal
- skill_id: support_longshot
  icon_id: skill_icon_support_longshot
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_longshot
- skill_id: support_piercing
  icon_id: skill_icon_support_piercing
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_piercing
- skill_id: support_purifying
  icon_id: skill_icon_support_purifying
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_purifying
status: prompted
---

# Cross-role Support Edges

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_support_brutal (support_brutal)
- (1,0): skill_icon_support_longshot (support_longshot)
- (0,1): skill_icon_support_piercing (support_piercing)
- (1,1): skill_icon_support_purifying (support_purifying)

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

(0,0) skill_icon_support_brutal:
brutal pressure shard, red broken blade chunk with blunt impact notch. Runtime: support_brutal: Support Utility, Aura, Physical, power 0, statuses none, effect family brutal_support.

(1,0) skill_icon_support_longshot:
longshot arc, thin arrow flying over a blue distance curve. Runtime: support_longshot: Support Utility, Aura, Physical, power 0, statuses none, effect family longshot_support.

(0,1) skill_icon_support_piercing:
piercing point splitting two armor slivers, silver and teal. Runtime: support_piercing: Support Utility, Aura, Physical, power 0, statuses none, effect family piercing_support.

(1,1) skill_icon_support_purifying:
ash purification flame, pale ash plume wrapped around a small gold spark. Runtime: support_purifying: Support Utility, Aura, Physical, power 0, statuses none, effect family priest_signature.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
