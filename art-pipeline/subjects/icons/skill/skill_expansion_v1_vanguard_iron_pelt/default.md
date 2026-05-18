---
slug: skill_expansion_v1_vanguard_iron_pelt--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_vanguard_iron_pelt
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- iron_pelt_maul
- rusthide_charge
- iron_pelt_roar
- iron_hide_memory
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_iron_pelt_maul
  icon_id: skill_icon_iron_pelt_maul
  target_hero: hero_iron_pelt
  target_class: vanguard
  slot: CoreActive
  kind: Strike
  delivery: Melee
  damage: Physical
  statuses:
  - id: wound
    duration: 4
    magnitude: 1
- skill_id: skill_rusthide_charge
  icon_id: skill_icon_rusthide_charge
  target_hero: hero_iron_pelt
  target_class: vanguard
  slot: CoreActive
  kind: Strike
  delivery: Melee
  damage: Physical
  statuses:
  - id: root
    duration: 1.6
    magnitude: 1
- skill_id: skill_iron_pelt_roar
  icon_id: skill_icon_iron_pelt_roar
  target_hero: hero_iron_pelt
  target_class: vanguard
  slot: UtilityActive
  kind: Debuff
  delivery: Nova
  damage: Physical
  statuses:
  - id: exposed
    duration: 3
    magnitude: 1
- skill_id: skill_iron_hide_memory
  icon_id: skill_icon_iron_hide_memory
  target_hero: hero_iron_pelt
  target_class: vanguard
  slot: Passive
  kind: Buff
  delivery: Melee
  damage: Physical
  statuses: []
style_seed: false
status: rendered
---

# Vanguard Iron Pelt / Attrition

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_iron_pelt_maul (Iron Pelt Maul)
- (1,0): skill_icon_rusthide_charge (Rusthide Charge)
- (0,1): skill_icon_iron_pelt_roar (Iron Pelt Roar)
- (1,1): skill_icon_iron_hide_memory (Iron-Hide Memory)

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

(0,0) skill_icon_iron_pelt_maul:
rust-dark maul head colliding with a cracked hide plate, no hand. Palette: dark iron, rust red, moss green accent. Runtime: skill_iron_pelt_maul / Iron Pelt Maul (철피 난타): vanguard CoreActive Strike, Melee, Physical, power 4.8, cooldown 2.6, statuses wound(4s/1). Effect: Heavy close strike that wounds the nearest enemy..

(1,0) skill_icon_rusthide_charge:
forward iron plate wedge with moss-green impact cracks trapped inside the outline. Palette: blackened iron, rust, muted green. Runtime: skill_rusthide_charge / Rusthide Charge (녹가죽 돌진): vanguard CoreActive Strike, Melee, Physical, power 4.3, cooldown 3.1, statuses root(1.6s/1). Effect: Charges the exposed enemy and briefly roots them..

(0,1) skill_icon_iron_pelt_roar:
contained jagged sound wedge bursting from a dark iron pelt sigil, no mouth. Palette: iron black, ochre, rust. Runtime: skill_iron_pelt_roar / Iron Pelt Roar (철피 포효): vanguard UtilityActive Debuff, Nova, Physical, power 0, cooldown 5.2, statuses exposed(3s/1). Effect: Emits a short pressure roar that exposes nearby enemies..

(1,1) skill_icon_iron_hide_memory:
layered hide scale with an old iron memory knot in the center. Palette: dark hide, iron, moss. Runtime: skill_iron_hide_memory / Iron-Hide Memory (철가죽 기억): vanguard Passive Buff, Melee, Physical, power 0, cooldown 0, statuses none. Effect: Passive; repeated damage grants a small durability budget..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
