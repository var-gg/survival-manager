---
slug: skill_expansion_v1_seed_style_mix--default
kind: skill_icon_catalog_sheet
subject_id: skill_expansion_v1_seed_style_mix
variant: default
refs:
- hero_aegis_sentinel:portrait_full
- hero_prism_seeker:portrait_full
- hero_shardblade:portrait_full
- hero_echo_savant:portrait_full
aspect: '1:1'
output_size: 1568x1568
chroma: '#FF00FF'
skills:
- aegis_sentinel_oath
- prism_lance
- shardblade_sever
- echo_resonance
output_directory: art-pipeline/output/icons/skill/expansion_v1
output_prefix: skill_icon
skill_bindings:
- skill_id: skill_aegis_sentinel_oath
  icon_id: skill_icon_aegis_sentinel_oath
  target_hero: hero_aegis_sentinel
  target_class: vanguard
  slot: CoreActive
  kind: Shield
  delivery: Nova
  damage: Magical
  statuses:
  - id: barrier
    duration: 0
    magnitude: 7
  - id: guarded
    duration: 2.4
    magnitude: 1
- skill_id: skill_prism_lance
  icon_id: skill_icon_prism_lance
  target_hero: hero_prism_seeker
  target_class: ranger
  slot: CoreActive
  kind: Strike
  delivery: Projectile
  damage: Magical
  statuses:
  - id: marked
    duration: 4
    magnitude: 1
- skill_id: skill_shardblade_sever
  icon_id: skill_icon_shardblade_sever
  target_hero: hero_shardblade
  target_class: duelist
  slot: CoreActive
  kind: Strike
  delivery: Melee
  damage: Magical
  statuses:
  - id: wound
    duration: 4
    magnitude: 1
  - id: sunder
    duration: 3
    magnitude: 1
- skill_id: skill_echo_resonance
  icon_id: skill_icon_echo_resonance
  target_hero: hero_echo_savant
  target_class: mystic
  slot: CoreActive
  kind: Debuff
  delivery: Projectile
  damage: Magical
  statuses:
  - id: silence
    duration: 2
    magnitude: 1
style_seed: true
status: rendered
---

# Expansion v1 Seed Style Mix

Expansion-v1 skill icon sheet. This first sheet uses character portrait refs to lock the painterly hand, palette discipline, and line weight.

```prompt
=== EXPANSION V1 SKILL ICON SHEET ===
Generate a 2-column x 2-row sprite sheet of 4 separate game skill icons.

Canvas: 1568 x 1568 px.
Cell size: 768 x 768 px.
Gutters and outer margin: exactly 32 px, solid #FF00FF.
Reading order:
- (0,0): skill_icon_aegis_sentinel_oath (Sentinel Oath)
- (1,0): skill_icon_prism_lance (Prism Lance)
- (0,1): skill_icon_shardblade_sever (Shardblade Sever)
- (1,1): skill_icon_echo_resonance (Echo Resonance)

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

(0,0) skill_icon_aegis_sentinel_oath:
square ivory shield sigil with a lattice oath knot and a blue-white pulse contained inside the stroke. Palette: ivory steel, muted blue, warm gold accent. Runtime: skill_aegis_sentinel_oath / Sentinel Oath (파수의 맹세): vanguard CoreActive Shield, Nova, Magical, power 0, cooldown 3.2, statuses barrier(0s/7), guarded(2.4s/1). Effect: Projects a square oath mark, granting barrier and guarded to the ally under pressure..

(1,0) skill_icon_prism_lance:
thin crystal arrow-lance piercing a small blue focus diamond. Palette: cyan, pale gold, glass white. Runtime: skill_prism_lance / Prism Lance (프리즘 창): ranger CoreActive Strike, Projectile, Magical, power 3.9, cooldown 2.2, statuses marked(4s/1). Effect: Fires a thin prism bolt that prefers marked targets and refreshes mark..

(0,1) skill_icon_shardblade_sever:
single broken crystal blade cutting a red-black pressure thread. Palette: indigo, crystal white, dark red accent. Runtime: skill_shardblade_sever / Shardblade Sever (편검 절단): duelist CoreActive Strike, Melee, Magical, power 3.6, cooldown 2.7, statuses wound(4s/1), sunder(3s/1). Effect: A precise shard cut that wounds and sunders an exposed target..

(1,1) skill_icon_echo_resonance:
blue sound line passing through a small memory prism, no letters. Palette: deep teal, blue-white, old gold. Runtime: skill_echo_resonance / Echo Resonance (공명 반향): mystic CoreActive Debuff, Projectile, Magical, power 3.2, cooldown 3, statuses silence(2s/1). Effect: Sends a focused resonance pulse that silences a marked target..

Output a single transparent-ready chroma sprite sheet exactly matching the grid.
```
