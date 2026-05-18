---
slug: skill_expansion_v1_ranger_passives--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_ranger_passives
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- ash_step
- prism_sight
- heat_haze
- quick_kindling
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_ash_step
  icon_id: skill_icon_ash_step
  target_hero: hero_ember_runner
  target_class: ranger
  slot: UtilityActive
  kind: Utility
  delivery: Zone
  damage: Physical
  statuses:
  - id: unstoppable
    duration: 1.5
    magnitude: 1
- skill_id: skill_prism_sight
  icon_id: skill_icon_prism_sight
  target_hero: hero_prism_seeker
  target_class: ranger
  slot: Passive
  kind: Buff
  delivery: Projectile
  damage: Magical
  statuses: []
- skill_id: skill_heat_haze
  icon_id: skill_icon_heat_haze
  target_hero: hero_ember_runner
  target_class: ranger
  slot: Passive
  kind: Buff
  delivery: Ranged
  damage: Physical
  statuses: []
- skill_id: skill_quick_kindling
  icon_id: skill_icon_quick_kindling
  target_hero: hero_ember_runner
  target_class: ranger
  slot: Passive
  kind: Buff
  delivery: Ranged
  damage: Physical
  statuses: []
style_seed: false
status: blocked_style_seed_ref
---

# Ranger Sight / Motion Passives

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_ash_step (Ash Step)
- (1,0): skill_icon_prism_sight (Prism Sight)
- (0,1): skill_icon_heat_haze (Heat Haze)
- (1,1): skill_icon_quick_kindling (Quick Kindling)

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

(0,0) skill_icon_ash_step:
footless ash path slash with two ember ticks, no leg or body. Palette: ash grey, ember, muted green. Runtime: skill_ash_step / Ash Step (잿걸음): ranger UtilityActive Utility, Zone, Physical, power 0, cooldown 4.8, statuses unstoppable(1.5s/1). Effect: Creates a short ash path and grants brief unstoppable repositioning..

(1,0) skill_icon_prism_sight:
clear prism eye-shaped diamond crossed by a single arrow line, no eye. Palette: cyan glass, white, gold. Runtime: skill_prism_sight / Prism Sight (프리즘 시야): ranger Passive Buff, Projectile, Magical, power 0, cooldown 0, statuses none. Effect: Passive; marked targets increase projectile reliability..

(0,1) skill_icon_heat_haze:
contained amber heat ripple bending a small arrow silhouette. Palette: ember amber, charcoal, cream. Runtime: skill_heat_haze / Heat Haze (열아지랑이): ranger Passive Buff, Ranged, Physical, power 0, cooldown 0, statuses none. Effect: Passive; after moving, incoming ranged pressure is less reliable..

(1,1) skill_icon_quick_kindling:
three small kindling sparks aligned like fast arrow ticks. Palette: ember, soot, warm gold. Runtime: skill_quick_kindling / Quick Kindling (빠른 불쏘시개): ranger Passive Buff, Ranged, Physical, power 0, cooldown 0, statuses none. Effect: Passive; first burn application in combat gains extra tempo budget..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
