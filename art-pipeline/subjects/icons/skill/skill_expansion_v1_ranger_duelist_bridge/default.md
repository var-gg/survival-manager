---
slug: skill_expansion_v1_ranger_duelist_bridge--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_ranger_duelist_bridge
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- mirror_cut
- fracture_step
- riposte_angle
- support_resonance_cleanse
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_mirror_cut
  icon_id: skill_icon_mirror_cut
  target_hero: hero_shardblade
  target_class: duelist
  slot: CoreActive
  kind: Strike
  delivery: Projectile
  damage: Magical
  statuses:
  - id: exposed
    duration: 2
    magnitude: 1
- skill_id: skill_fracture_step
  icon_id: skill_icon_fracture_step
  target_hero: hero_shardblade
  target_class: duelist
  slot: UtilityActive
  kind: Utility
  delivery: Zone
  damage: 'True'
  statuses:
  - id: slow
    duration: 2
    magnitude: 1
- skill_id: skill_riposte_angle
  icon_id: skill_icon_riposte_angle
  target_hero: hero_shardblade
  target_class: duelist
  slot: UtilityActive
  kind: Buff
  delivery: Ranged
  damage: Physical
  statuses:
  - id: guarded
    duration: 1.8
    magnitude: 1
- skill_id: support_resonance_cleanse
  icon_id: skill_icon_support_resonance_cleanse
  target_hero: hero_echo_savant
  target_class: mystic
  slot: Support
  kind: Heal
  delivery: Aura
  damage: Healing
  statuses:
  - id: unstoppable
    duration: 1.2
    magnitude: 1
style_seed: false
status: blocked_style_seed_ref
---

# Ranger / Duelist Precision Bridge

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_mirror_cut (Mirror Cut)
- (1,0): skill_icon_fracture_step (Fracture Step)
- (0,1): skill_icon_riposte_angle (Riposte Angle)
- (1,1): skill_icon_support_resonance_cleanse (Resonance Cleanse)

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

(0,0) skill_icon_mirror_cut:
thin mirror shard slash reflecting a small cyan cut line. Palette: mirror silver, indigo, cyan. Runtime: skill_mirror_cut / Mirror Cut (거울 베기): duelist CoreActive Strike, Projectile, Magical, power 3.5, cooldown 2.4, statuses exposed(2s/1). Effect: Throws a mirror-thin shard at a marked target and exposes them..

(1,0) skill_icon_fracture_step:
footless fracture lane, two indigo shards separated by a white crack. Palette: indigo, white crystal, dark steel. Runtime: skill_fracture_step / Fracture Step (균열 보법): duelist UtilityActive Utility, Zone, True, power 0, cooldown 4.6, statuses slow(2s/1). Effect: Leaves a fractured lane that slows enemies trying to follow..

(0,1) skill_icon_riposte_angle:
narrow parry line bouncing a red spark back along a measured angle. Palette: steel, indigo, red spark. Runtime: skill_riposte_angle / Riposte Angle (반격각): duelist UtilityActive Buff, Ranged, Physical, power 0, cooldown 3.8, statuses guarded(1.8s/1). Effect: Sets a narrow counter angle and briefly gains guarded..

(1,1) skill_icon_support_resonance_cleanse:
clean teal resonance ring washing over a small gold spark. Palette: teal, pale green, gold. Runtime: support_resonance_cleanse / Resonance Cleanse (공명 정화): mystic Support Heal, Aura, Healing, power 0, cooldown 0, statuses unstoppable(1.2s/1). Effect: Support modifier; cleanse effects also grant a short unstoppable pulse..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
