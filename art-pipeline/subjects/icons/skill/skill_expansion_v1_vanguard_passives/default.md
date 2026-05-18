---
slug: skill_expansion_v1_vanguard_passives--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_vanguard_passives
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- sentinel_oath
- lattice_bastion
- pelt_last_stand
- glass_pathfinder
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_sentinel_oath
  icon_id: skill_icon_sentinel_oath
  target_hero: hero_aegis_sentinel
  target_class: vanguard
  slot: Passive
  kind: Buff
  delivery: Melee
  damage: Physical
  statuses: []
- skill_id: skill_lattice_bastion
  icon_id: skill_icon_lattice_bastion
  target_hero: hero_aegis_sentinel
  target_class: vanguard
  slot: Passive
  kind: Buff
  delivery: Zone
  damage: Magical
  statuses: []
- skill_id: skill_pelt_last_stand
  icon_id: skill_icon_pelt_last_stand
  target_hero: hero_iron_pelt
  target_class: vanguard
  slot: Passive
  kind: Buff
  delivery: Aura
  damage: Physical
  statuses:
  - id: unstoppable
    duration: 2
    magnitude: 1
- skill_id: skill_glass_pathfinder
  icon_id: skill_icon_glass_pathfinder
  target_hero: hero_prism_seeker
  target_class: ranger
  slot: Passive
  kind: Utility
  delivery: Zone
  damage: Magical
  statuses: []
style_seed: false
status: rendered
---

# Vanguard Guard / Pathfinder Passives

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_sentinel_oath (Oath Weight)
- (1,0): skill_icon_lattice_bastion (Lattice Bastion)
- (0,1): skill_icon_pelt_last_stand (Last Pelt Standing)
- (1,1): skill_icon_glass_pathfinder (Glass Pathfinder)

Global icon rules:
- Each cell has exactly one centered focal symbol, filling about 65-75% of the cell.
- No character, no hand, no face, no body, no portrait, no scene.
- No text, no numerals, no letters, no UI frame or ring.
- Background in every cell, gutter, and margin is flat #FF00FF.
- Each symbol has a continuous 2-4 px dark outer stroke.
- Outside the outer stroke there is only pure #FF00FF: no shadow, no blur, no glow, no particles, no haze.
- Subject colors must never use magenta, hot pink, or fuchsia.
- Match the attached refs for painterly line quality and color restraint, but every cell must remain a standalone skill symbol.
- Icons in this sheet should share a coherent same-artist game style, while each symbol stays readable at 64 px.

Per-cell descriptors:

(0,0) skill_icon_sentinel_oath:
heavy oath seal tied to a shield base with two restrained cords. Palette: iron, ivory, old gold. Runtime: skill_sentinel_oath / Oath Weight (맹세의 무게): vanguard Passive Buff, Melee, Physical, power 0, cooldown 0, statuses none. Effect: Passive; improves reliability when holding anchor posture..

(1,0) skill_icon_lattice_bastion:
small fortress block overlaid with a clean geometric lattice cross. Palette: blue-white, slate, gold point. Runtime: skill_lattice_bastion / Lattice Bastion (격자 보루): vanguard Passive Buff, Zone, Magical, power 0, cooldown 0, statuses none. Effect: Passive; converts repeated pressure into extra barrier budget..

(0,1) skill_icon_pelt_last_stand:
cracked hide plate refusing to split, with a small green ember inside. Palette: rust black, green ember, bone edge. Runtime: skill_pelt_last_stand / Last Pelt Standing (마지막 철피): vanguard Passive Buff, Aura, Physical, power 0, cooldown 0, statuses unstoppable(2s/1). Effect: Passive; low health briefly grants unstoppable once per combat..

(1,1) skill_icon_glass_pathfinder:
angular glass path split into two clean tactical lanes. Palette: glass blue, white, muted gold. Runtime: skill_glass_pathfinder / Glass Pathfinder (유리길 탐색자): ranger Passive Utility, Zone, Magical, power 0, cooldown 0, statuses none. Effect: Passive; improves target switching when fighting through marked zones..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
