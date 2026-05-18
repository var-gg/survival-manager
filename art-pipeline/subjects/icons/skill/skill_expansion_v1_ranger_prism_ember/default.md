---
slug: skill_expansion_v1_ranger_prism_ember--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_ranger_prism_ember
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- ember_arrow
- cinder_overrun
- refracting_snare
- signal_flare
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_ember_arrow
  icon_id: skill_icon_ember_arrow
  target_hero: hero_ember_runner
  target_class: ranger
  slot: CoreActive
  kind: Strike
  delivery: Projectile
  damage: Physical
  statuses:
  - id: burn
    duration: 4
    magnitude: 1
- skill_id: skill_cinder_overrun
  icon_id: skill_icon_cinder_overrun
  target_hero: hero_ember_runner
  target_class: ranger
  slot: CoreActive
  kind: Strike
  delivery: Ranged
  damage: Physical
  statuses:
  - id: slow
    duration: 2.5
    magnitude: 1
- skill_id: skill_refracting_snare
  icon_id: skill_icon_refracting_snare
  target_hero: hero_prism_seeker
  target_class: ranger
  slot: UtilityActive
  kind: Debuff
  delivery: Trap
  damage: Magical
  statuses:
  - id: root
    duration: 2
    magnitude: 1
- skill_id: skill_signal_flare
  icon_id: skill_icon_signal_flare
  target_hero: hero_prism_seeker
  target_class: ranger
  slot: UtilityActive
  kind: Debuff
  delivery: Ranged
  damage: Magical
  statuses:
  - id: marked
    duration: 5
    magnitude: 1
  - id: exposed
    duration: 2.5
    magnitude: 1
style_seed: false
status: blocked_style_seed_ref
---

# Ranger Prism / Ember Actives

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_ember_arrow (Ember Arrow)
- (1,0): skill_icon_cinder_overrun (Cinder Overrun)
- (0,1): skill_icon_refracting_snare (Refracting Snare)
- (1,1): skill_icon_signal_flare (Signal Flare)

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

(0,0) skill_icon_ember_arrow:
compact ember arrowhead with a contained orange coal core. Palette: charcoal, ember orange, warm cream. Runtime: skill_ember_arrow / Ember Arrow (불씨 화살): ranger CoreActive Strike, Projectile, Physical, power 3.7, cooldown 2, statuses burn(4s/1). Effect: Shoots a fast ember-tipped arrow that applies burn..

(1,0) skill_icon_cinder_overrun:
low arcing cinder shot chasing a small cracked target diamond. Palette: ember orange, soot, muted teal. Runtime: skill_cinder_overrun / Cinder Overrun (잿불 추격): ranger CoreActive Strike, Ranged, Physical, power 3.4, cooldown 2.8, statuses slow(2.5s/1). Effect: Pressures a weakened enemy with a cinder shot and slows them..

(0,1) skill_icon_refracting_snare:
angular glass snare loop closing around a tiny light shard. Palette: glass cyan, silver, pale amber. Runtime: skill_refracting_snare / Refracting Snare (굴절 덫): ranger UtilityActive Debuff, Trap, Magical, power 0, cooldown 5, statuses root(2s/1). Effect: Places a prism snare that roots an exposed target..

(1,1) skill_icon_signal_flare:
small cyan flare over a gold target notch, no explosion outside stroke. Palette: cyan, gold, dark blue. Runtime: skill_signal_flare / Signal Flare (신호 섬광): ranger UtilityActive Debuff, Ranged, Magical, power 0, cooldown 4.5, statuses marked(5s/1), exposed(2.5s/1). Effect: Marks a priority target and briefly exposes it..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
