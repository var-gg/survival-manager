---
slug: npc_aldric--battle_stance_sheet
kind: battle_stance_sheet
subject_id: npc_aldric
variant: battle_stance_sheet
refs:
- npc_aldric
- npc_aldric:portrait_full
- npc_aldric:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '1:1.49'
output_size: 1568x2336
chroma: '#FF00FF'
status: rendered
stances:
- idle
- attack
- guard
- cast
---
# 단현 스턴홀트 (丹玄) / Aldric Sternfeld — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 단현 스턴홀트 (丹玄) / Aldric Sternfeld.

=== REFERENCE PRIORITY ===
1. The P09 anchor for npc_aldric is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The npc_aldric:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 단현 스턴홀트 (丹玄) / Aldric Sternfeld
- Pipeline subject_id: npc_aldric
- Faction / job lane: solarum / historical_scholar
- Uniform group: solarum_historical_scholar
- Silhouette family: p09_h0_c3_a3_w3_l3_wp0_sh0
- Intent: Male_Armor_003 scholar robe, ivory tied hair, and dignified facial hair for the historical dead scholar with no combat weapon.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 1 / HairStyleId 8 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 6 / BustSizeId 2
- Outfit A3/W0/S0 / Head 0 / Chest 3 / Arm 3 / Waist 3 / Leg 3
- WeaponId 0 (P09 weapon from REF) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #D8D4C8
- Head gear: read from REF
- Chest / upper outfit: #5C5A58
- Arm / sleeve: #4A4648
- Waist / sash: #2A2A2E
- Leg / lower outfit: #3F3B36
- Weapon: read from REF
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 0: use the exact P09 weapon from REF.
- ShieldId 0: no shield, no buckler, no mirror shield, no defensive prop in any cell.
- attack: use the exact P09 weapon in a readable offensive motion.
- guard: defensive posture using only the P09 weapon/shield state.
- cast: class technique pose using only the P09 weapon/shield state.


=== GRID SHEET LAYOUT ===
- Canvas: 1568 x 2336 PNG.
- 2 columns x 2 rows.
- Each cell: exactly 768 x 1152 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: idle, attack
  - row 1: guard, cast

=== CELL CONTENT ===
- Full body in every cell: head to feet visible, weapon and shield state readable.
- Same character, same outfit, same hair, same color zones, same weapon, same shield state across all 4 cells.
- Three-quarter front view with dynamic stance differences.
- Same scale and vertical footprint across all cells.
- Clean 1-2 px dark outline around the entire character silhouette for chroma cleanup.

=== STANCE SET ===
- idle: combat-ready rest posture, readable class identity.
- attack: active offensive motion as specified in weapon rules.
- guard: defensive posture as specified in weapon/shield rules.
- cast: class technique or support stance; restrained effect only, no environment.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No UI, captions, labels, environment, ground shadows, companions, extra weapons, or extra props.
- Do not change gender presentation, hair, face, outfit slots, weapon family, shield state, or palette between cells.
- Do not add capes, halos, animal features, scars, tattoos, jewelry, class symbols, or ornaments not visible in P09.
- No magenta tint on the character. Any effect must avoid #FF00FF/fuchsia and must not bridge cells.
```
