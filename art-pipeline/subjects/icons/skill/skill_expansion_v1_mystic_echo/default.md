---
slug: skill_expansion_v1_mystic_echo--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_mystic_echo
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- phase_tether
- echo_archive
- lattice_listener
- savant_last_word
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_phase_tether
  icon_id: skill_icon_phase_tether
  target_hero: hero_echo_savant
  target_class: mystic
  slot: UtilityActive
  kind: Debuff
  delivery: Zone
  damage: Magical
  statuses:
  - id: root
    duration: 2
    magnitude: 1
  - id: slow
    duration: 3
    magnitude: 1
- skill_id: skill_echo_archive
  icon_id: skill_icon_echo_archive
  target_hero: hero_echo_savant
  target_class: mystic
  slot: Passive
  kind: Buff
  delivery: Aura
  damage: Magical
  statuses: []
- skill_id: skill_lattice_listener
  icon_id: skill_icon_lattice_listener
  target_hero: hero_echo_savant
  target_class: mystic
  slot: Passive
  kind: Utility
  delivery: Zone
  damage: Magical
  statuses: []
- skill_id: skill_savant_last_word
  icon_id: skill_icon_savant_last_word
  target_hero: hero_echo_savant
  target_class: mystic
  slot: Passive
  kind: Debuff
  delivery: Zone
  damage: Magical
  statuses:
  - id: silence
    duration: 1.4
    magnitude: 1
style_seed: false
status: blocked_style_seed_ref
---

# Mystic Echo Savant

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_phase_tether (Phase Tether)
- (1,0): skill_icon_echo_archive (Echo Archive)
- (0,1): skill_icon_lattice_listener (Lattice Listener)
- (1,1): skill_icon_savant_last_word (Last Word)

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

(0,0) skill_icon_phase_tether:
teal phase cord knot wrapping a small black prism. Palette: teal, black, pale gold. Runtime: skill_phase_tether / Phase Tether (위상 결박): mystic UtilityActive Debuff, Zone, Magical, power 0, cooldown 5.2, statuses root(2s/1), slow(3s/1). Effect: Tethers an exposed enemy in phase noise, rooting and slowing them..

(1,0) skill_icon_echo_archive:
small open archive prism with a contained echo ring, no letters. Palette: deep teal, ivory, blue. Runtime: skill_echo_archive / Echo Archive (반향 기록고): mystic Passive Buff, Aura, Magical, power 0, cooldown 0, statuses none. Effect: Passive; repeated status applications improve reliability..

(0,1) skill_icon_lattice_listener:
quiet lattice antenna glyph listening to a blue ripple. Palette: teal, blue-white, gold. Runtime: skill_lattice_listener / Lattice Listener (격자 청취자): mystic Passive Utility, Zone, Magical, power 0, cooldown 0, statuses none. Effect: Passive; improves target selection against controlled enemies..

(1,1) skill_icon_savant_last_word:
final blue sound glyph sealed inside a dark memory bead, no text. Palette: dark teal, blue, pale gold. Runtime: skill_savant_last_word / Last Word (마지막 발화): mystic Passive Debuff, Zone, Magical, power 0, cooldown 0, statuses silence(1.4s/1). Effect: Passive; once per combat, a collapsing ally can emit a brief silence pulse..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
