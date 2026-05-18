---
slug: skill_catalog_v2_mystic_recovery--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_mystic_recovery
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- minor_heal
- mystic_support_1
- mystic_support_2
- support_siphon
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_minor_heal
  icon_id: skill_icon_minor_heal
  slot: UtilityActive
  kind: Heal
  status_ids: []
  vfx_hook_id: vfx.skill_minor_heal
- skill_id: skill_mystic_support_1
  icon_id: skill_icon_mystic_support_1
  slot: Support
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_mystic_support_1
- skill_id: skill_mystic_support_2
  icon_id: skill_icon_mystic_support_2
  slot: Support
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_mystic_support_2
- skill_id: support_siphon
  icon_id: skill_icon_support_siphon
  slot: Support
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.support_siphon
status: prompted
---

# Mystic Recovery

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_minor_heal (skill_minor_heal)
- (1,0): skill_icon_mystic_support_1 (skill_mystic_support_1)
- (0,1): skill_icon_mystic_support_2 (skill_mystic_support_2)
- (1,1): skill_icon_support_siphon (support_siphon)

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

(0,0) skill_icon_minor_heal:
platinum aegis heal mark, small shield with a clean white-gold cross-like glint but no text. Runtime: skill_minor_heal: UtilityActive Heal, Melee, Healing, power 4, statuses none, effect family minor_heal.

(1,0) skill_icon_mystic_support_1:
mystic support seal, white-gold ward bead with green inner pulse. Runtime: skill_mystic_support_1: Support Buff, Aura, Healing, power 0, statuses none, effect family priest_support.

(0,1) skill_icon_mystic_support_2:
linked recovery sigil, two small light motes joined inside a platinum crescent. Runtime: skill_mystic_support_2: Support Buff, Aura, Healing, power 0, statuses none, effect family hexer_support.

(1,1) skill_icon_support_siphon:
siphon spiral, green energy thread pulling into a dark memory bead. Runtime: support_siphon: Support Utility, Aura, Physical, power 0, statuses none, effect family siphon_support.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
