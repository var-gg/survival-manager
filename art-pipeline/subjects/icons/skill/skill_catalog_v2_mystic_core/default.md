---
slug: skill_catalog_v2_mystic_core--default
kind: skill_icon_catalog_sheet
subject_id: skill_catalog_v2_mystic_core
variant: default
refs: []
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- hexer_core
- shaman_core
- hexer_utility
- shaman_utility
output_directory: art-pipeline/output/icons/skill/catalog_v2
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_hexer_core
  icon_id: skill_icon_hexer_core
  slot: CoreActive
  kind: Strike
  status_ids:
  - burn
  - silence
  vfx_hook_id: vfx.skill_hexer_core
- skill_id: skill_shaman_core
  icon_id: skill_icon_shaman_core
  slot: CoreActive
  kind: Strike
  status_ids:
  - burn
  vfx_hook_id: vfx.skill_shaman_core
- skill_id: skill_hexer_utility
  icon_id: skill_icon_hexer_utility
  slot: UtilityActive
  kind: Heal
  status_ids: []
  vfx_hook_id: vfx.skill_hexer_utility
- skill_id: skill_shaman_utility
  icon_id: skill_icon_shaman_utility
  slot: UtilityActive
  kind: Heal
  status_ids: []
  vfx_hook_id: vfx.skill_shaman_utility
status: prompted
---

# Mystic Core / Utility

Canonical skill-owned icon sheet for actual authored SkillDefinition assets. This subject is not character-owned; every cell maps a runtime `SkillId` to exactly one stable `IconId`.

```prompt
=== CANONICAL SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_hexer_core (skill_hexer_core)
- (1,0): skill_icon_shaman_core (skill_shaman_core)
- (0,1): skill_icon_hexer_utility (skill_hexer_utility)
- (1,1): skill_icon_shaman_utility (skill_shaman_utility)

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

(0,0) skill_icon_hexer_core:
time-distance hex sigil, dark teal hourglass shard with violet-free black rune cuts. Runtime: skill_hexer_core: CoreActive Strike, Ranged, Magical, power 4, statuses burn, silence, effect family hexer_signature.

(1,0) skill_icon_shaman_core:
voice scar wave, ochre sound ripple slicing through a cracked bead. Runtime: skill_shaman_core: CoreActive Strike, Ranged, Magical, power 3.9, statuses burn, effect family shaman_signature.

(0,1) skill_icon_hexer_utility:
memory projection prism, green spectral shard casting a contained echo. Runtime: skill_hexer_utility: UtilityActive Heal, Ranged, Healing, power 3.8, statuses none, effect family hexer_silence.

(1,1) skill_icon_shaman_utility:
ritual healing drum mark, warm green pulse inside a small bone-white circle. Runtime: skill_shaman_utility: UtilityActive Heal, Ranged, Healing, power 3.9, statuses none, effect family shaman_zone.

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
