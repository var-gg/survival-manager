---
slug: hero_ember_runner--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hero_ember_runner
variant: battle_stance_sheet
refs:
- hero_ember_runner
- hero_ember_runner:portrait_full
- hero_ember_runner:portrait_face_default
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
# 연주 (燕走) / Ember Runner — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 연주 (燕走) / Ember Runner.

=== REFERENCE PRIORITY ===
1. The P09 anchor for hero_ember_runner is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The hero_ember_runner:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 연주 (燕走) / Ember Runner
- Pipeline subject_id: hero_ember_runner
- Faction / job lane: irisol / ember_runner
- Uniform group: irisol_ember_runner
- Silhouette family: p09_h0_c3_a4_w4_l3_wp10_sh0
- Intent: 가벼운 travel/leather mix와 bright ember hair로 이리솔 견습 사냥꾼의 빠른 에너지를 만든다.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 1 / HairStyleId 1 / HairColorId 6
- SkinId 1 / EyeColorId 1 / FacialHairId 0 / BustSizeId 1
- Outfit A3/W10/S0 / Head 0 / Chest 3 / Arm 4 / Waist 4 / Leg 3
- WeaponId 10 (long bow) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #B66A34
- Head gear: read from REF
- Chest / upper outfit: #8A6B3E
- Arm / sleeve: #8A4F2E
- Waist / sash: #5A3A25
- Leg / lower outfit: #7A4A32
- Weapon: #8A6A3F
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 10: long bow.
- ShieldId 0: no shield, no buckler, no mirror shield, no defensive prop in any cell.
- attack: bow fully drawn, arrow aimed diagonally toward an off-screen threat; no sword or staff.
- guard: bow angled defensively while stepping back or lowering stance; off-hand controls the string/arrow, no shield unless P09 has one.
- cast: tactical focus / class technique pose using the bow as focus, subtle non-magenta energy only if appropriate.


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
