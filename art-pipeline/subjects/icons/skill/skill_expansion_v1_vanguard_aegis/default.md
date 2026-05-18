---
slug: skill_expansion_v1_vanguard_aegis--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_vanguard_aegis
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- aegis_linebreaker
- square_wall
- aegis_intercept
- support_line_anchor
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_aegis_linebreaker
  icon_id: skill_icon_aegis_linebreaker
  target_hero: hero_aegis_sentinel
  target_class: vanguard
  slot: CoreActive
  kind: Strike
  delivery: Melee
  damage: Physical
  statuses:
  - id: sunder
    duration: 3
    magnitude: 1
- skill_id: skill_square_wall
  icon_id: skill_icon_square_wall
  target_hero: hero_aegis_sentinel
  target_class: vanguard
  slot: UtilityActive
  kind: Shield
  delivery: Zone
  damage: Magical
  statuses:
  - id: barrier
    duration: 0
    magnitude: 9
- skill_id: skill_aegis_intercept
  icon_id: skill_icon_aegis_intercept
  target_hero: hero_aegis_sentinel
  target_class: vanguard
  slot: UtilityActive
  kind: Buff
  delivery: Aura
  damage: Magical
  statuses:
  - id: guarded
    duration: 3
    magnitude: 1
- skill_id: support_line_anchor
  icon_id: skill_icon_support_line_anchor
  target_hero: hero_aegis_sentinel
  target_class: vanguard
  slot: Support
  kind: Buff
  delivery: Aura
  damage: Physical
  statuses:
  - id: guarded
    duration: 2
    magnitude: 1
style_seed: false
status: rendered
---

# Vanguard Aegis / Formation

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_aegis_linebreaker (Linebreaker)
- (1,0): skill_icon_square_wall (Square Wall)
- (0,1): skill_icon_aegis_intercept (Intercept Angle)
- (1,1): skill_icon_support_line_anchor (Line Anchor)

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

(0,0) skill_icon_aegis_linebreaker:
blunt shield corner breaking a straight enemy line into two clean plate shards. Palette: steel, chalk white, muted amber. Runtime: skill_aegis_linebreaker / Linebreaker (전열 파쇄): vanguard CoreActive Strike, Melee, Physical, power 4.4, cooldown 2.4, statuses sunder(3s/1). Effect: Shield-bashes the exposed enemy and applies sunder..

(1,0) skill_icon_square_wall:
four interlocking square plates forming a compact wall, viewed as a single emblem. Palette: pale stone, blue-grey, gold seam. Runtime: skill_square_wall / Square Wall (방진벽): vanguard UtilityActive Shield, Zone, Magical, power 0, cooldown 5, statuses barrier(0s/9). Effect: Raises a short-lived lattice wall around the front line, adding barrier..

(0,1) skill_icon_aegis_intercept:
bent silver arrow redirecting into a small shield diamond, no figure. Palette: steel, desaturated blue, copper accent. Runtime: skill_aegis_intercept / Intercept Angle (가로막는 각도): vanguard UtilityActive Buff, Aura, Magical, power 0, cooldown 4.2, statuses guarded(3s/1). Effect: Marks a protected ally and biases the sentinel to intercept incoming pressure..

(1,1) skill_icon_support_line_anchor:
anchored horizontal shield line with one central square pin. Palette: dark steel, ivory, muted blue. Runtime: support_line_anchor / Line Anchor (전열 고정): vanguard Support Buff, Aura, Physical, power 0, cooldown 0, statuses guarded(2s/1). Effect: Support modifier; guard effects also steady nearby allies..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
