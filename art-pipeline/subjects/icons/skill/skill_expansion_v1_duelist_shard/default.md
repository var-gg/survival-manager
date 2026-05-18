---
slug: skill_expansion_v1_duelist_shard--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_duelist_shard
variant: default
refs:
- skill_expansion_v1_style_ref_01
- skill_expansion_v1_style_ref_02
- skill_expansion_v1_style_ref_03
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- shard_memory
- edge_of_sentence
- bloodless_form
- memory_tuning
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_shard_memory
  icon_id: skill_icon_shard_memory
  target_hero: hero_shardblade
  target_class: duelist
  slot: Passive
  kind: Buff
  delivery: Ranged
  damage: Magical
  statuses: []
- skill_id: skill_edge_of_sentence
  icon_id: skill_icon_edge_of_sentence
  target_hero: hero_shardblade
  target_class: duelist
  slot: Passive
  kind: Buff
  delivery: Melee
  damage: 'True'
  statuses: []
- skill_id: skill_bloodless_form
  icon_id: skill_icon_bloodless_form
  target_hero: hero_shardblade
  target_class: duelist
  slot: Passive
  kind: Utility
  delivery: Melee
  damage: 'True'
  statuses: []
- skill_id: skill_memory_tuning
  icon_id: skill_icon_memory_tuning
  target_hero: hero_echo_savant
  target_class: mystic
  slot: UtilityActive
  kind: Heal
  delivery: Aura
  damage: Healing
  statuses:
  - id: barrier
    duration: 0
    magnitude: 4
style_seed: false
status: rendered
---

# Duelist Shardblade Passives

Expansion-v1 skill icon sheet. Use the attached promoted seed icon refs as the canonical same-artist baseline from the first expansion sheet.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_shard_memory (Shard Memory)
- (1,0): skill_icon_edge_of_sentence (Edge of Sentence)
- (0,1): skill_icon_bloodless_form (Bloodless Form)
- (1,1): skill_icon_memory_tuning (Memory Tuning)

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

(0,0) skill_icon_shard_memory:
small memory bead trapped inside a broken crystal shard. Palette: crystal white, indigo, pale gold. Runtime: skill_shard_memory / Shard Memory (파편 기억): duelist Passive Buff, Ranged, Magical, power 0, cooldown 0, statuses none. Effect: Passive; hitting exposed targets stores a small follow-up budget..

(1,0) skill_icon_edge_of_sentence:
execution point hovering over a small sealed verdict diamond, no letters. Palette: black steel, red, pale crystal. Runtime: skill_edge_of_sentence / Edge of Sentence (선고의 칼끝): duelist Passive Buff, Melee, True, power 0, cooldown 0, statuses none. Effect: Passive; execution windows gain a small true-damage bias..

(0,1) skill_icon_bloodless_form:
pale empty stance sigil made of two clean blade arcs, no body. Palette: pale grey, indigo, muted red. Runtime: skill_bloodless_form / Bloodless Form (무혈식): duelist Passive Utility, Melee, True, power 0, cooldown 0, statuses none. Effect: Passive; reduces self-risk when chaining utility movement..

(1,1) skill_icon_memory_tuning:
tuning fork-shaped light around a green memory bead, no text. Palette: teal, pale green, gold. Runtime: skill_memory_tuning / Memory Tuning (기억 조율): mystic UtilityActive Heal, Aura, Healing, power 3, cooldown 4.8, statuses barrier(0s/4). Effect: Tunes an injured ally's memory pattern, healing and adding a light barrier..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
