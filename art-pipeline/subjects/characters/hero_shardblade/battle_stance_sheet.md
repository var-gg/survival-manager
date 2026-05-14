---
slug: hero_shardblade--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hero_shardblade
variant: battle_stance_sheet
refs:
- hero_shardblade
- hero_shardblade:portrait_full
- hero_shardblade:portrait_face_default
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
# 편검 (片劍) / Shardblade — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 편검 (片劍) / Shardblade.

=== REFERENCE PRIORITY ===
1. The P09 anchor for hero_shardblade is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The hero_shardblade:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 편검 (片劍) / Shardblade
- Pipeline subject_id: hero_shardblade
- Faction / job lane: lattice_order / shardblade
- Uniform group: lattice_order_shardblade
- Silhouette family: p09_h0_c4_a5_w4_l3_wp4_sh0
- Intent: 외부 일상복 같은 Armor_004에 실전 bracer와 격자검을 더한 earth/indigo/prism 직공 결투가.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 1 / HairStyleId 3 / HairColorId 8
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 2
- Outfit A4/W4/S0 / Head 0 / Chest 4 / Arm 5 / Waist 4 / Leg 3
- WeaponId 4 (oath execution sword) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #6F4B88
- Head gear: read from REF
- Chest / upper outfit: #7B614A
- Arm / sleeve: #6F737A
- Waist / sash: #2E3440
- Leg / lower outfit: #5C4A38
- Weapon: #D8D2E3
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 4: oath execution sword.
- ShieldId 0: no shield, no buckler, no mirror shield, no defensive prop in any cell.
- attack: sword lunge or diagonal strike, clear blade silhouette, no bow or staff.
- guard: sword parry or evasive low guard; off-hand empty, no shield or buckler.
- cast: class technique pose with weapon held low or used as focus; restrained effect, not a new weapon.


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
