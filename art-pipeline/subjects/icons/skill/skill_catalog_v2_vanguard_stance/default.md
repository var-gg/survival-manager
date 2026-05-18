---
slug: skill_catalog_v2_vanguard_stance--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_vanguard_stance
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- bulwark_utility
- guardian_utility
- vanguard_passive_1
- vanguard_passive_2
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_bulwark_utility
  icon_id: skill_icon_bulwark_utility
  slot: UtilityActive
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.skill_bulwark_utility
- skill_id: skill_guardian_utility
  icon_id: skill_icon_guardian_utility
  slot: UtilityActive
  kind: Utility
  status_ids: []
  vfx_hook_id: vfx.skill_guardian_utility
- skill_id: skill_vanguard_passive_1
  icon_id: skill_icon_vanguard_passive_1
  slot: Passive
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_vanguard_passive_1
- skill_id: skill_vanguard_passive_2
  icon_id: skill_icon_vanguard_passive_2
  slot: Passive
  kind: Buff
  status_ids: []
  vfx_hook_id: vfx.skill_vanguard_passive_2
status: prompted
---

# Vanguard Utility / Stance

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_bulwark_utility (skill_bulwark_utility)
- (1,0): skill_icon_guardian_utility (skill_guardian_utility)
- (0,1): skill_icon_vanguard_passive_1 (skill_vanguard_passive_1)
- (1,1): skill_icon_vanguard_passive_2 (skill_vanguard_passive_2)

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

(0,0) skill_icon_bulwark_utility:
fortified wall segment with two interlocking plates and amber brace lines. Runtime: skill_bulwark_utility: UtilityActive Utility, Aura, Physical, power 0, statuses none, effect family none.

(1,0) skill_icon_guardian_utility:
protective intercept arrow bending into a shield face. Runtime: skill_guardian_utility: UtilityActive Utility, Aura, Physical, power 0, statuses none, effect family guard_rally.

(0,1) skill_icon_vanguard_passive_1:
compact stance rune with stacked shield plates and a calm blue core. Runtime: skill_vanguard_passive_1: Passive Buff, Aura, Physical, power 0, statuses none, effect family none.

(1,1) skill_icon_vanguard_passive_2:
retaliation guard knot, shield corner plus restrained red counter-spark inside stroke. Runtime: skill_vanguard_passive_2: Passive Buff, Aura, Physical, power 0, statuses none, effect family none.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
