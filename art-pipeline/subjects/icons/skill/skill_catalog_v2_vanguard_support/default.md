---
slug: skill_catalog_v2_vanguard_support--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_vanguard_support
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- vanguard_support_1
- vanguard_support_2
- support_anchored
- support_guarded
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_vanguard_support_1
  icon_id: skill_icon_vanguard_support_1
  slot: Support
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_vanguard_support_1
- skill_id: skill_vanguard_support_2
  icon_id: skill_icon_vanguard_support_2
  slot: Support
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_vanguard_support_2
- skill_id: support_anchored
  icon_id: skill_icon_support_anchored
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_anchored
- skill_id: support_guarded
  icon_id: skill_icon_support_guarded
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_guarded
status: prompted
---

# Vanguard Support

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_vanguard_support_1 (skill_vanguard_support_1)
- (1,0): skill_icon_vanguard_support_2 (skill_vanguard_support_2)
- (0,1): skill_icon_support_anchored (support_anchored)
- (1,1): skill_icon_support_guarded (support_guarded)

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

(0,0) skill_icon_vanguard_support_1:
team guard banner abstracted as two small shields under one arch. Runtime: skill_vanguard_support_1: Support Buff, Aura, Physical, power 0, statuses none, effect family guard_support.

(1,0) skill_icon_vanguard_support_2:
resolute oath seal, square shield glyph bound by gold cords. Runtime: skill_vanguard_support_2: Support Buff, Aura, Physical, power 0, statuses none, effect family bulwark_support.

(0,1) skill_icon_support_anchored:
iron anchor mark fused with a shield base, grounded and immovable. Runtime: support_anchored: Support Utility, Aura, Physical, power 0, statuses none, effect family anchored_support.

(1,1) skill_icon_support_guarded:
small protected ally diamond enclosed by a shield crescent. Runtime: support_guarded: Support Utility, Aura, Physical, power 0, statuses none, effect family guard_signature.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
