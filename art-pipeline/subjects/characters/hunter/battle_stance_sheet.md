---
slug: hunter--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hunter
variant: battle_stance_sheet
refs:
- hunter
- hunter:portrait_full
- hunter:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '1:1.49'
output_size: 1568x2336
chroma: '#FF00FF'
status: prompted
stances:
- idle
- attack
- guard
- cast
---
# 원시 (遠矢) / Longshot Hunter — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 원시 (遠矢) / Longshot Hunter.

=== REFERENCE PRIORITY ===
1. The P09 anchor for hunter is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The hunter:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 원시 (遠矢) / Longshot Hunter
- Pipeline subject_id: hunter
- Faction / job lane: solarum / longshot_hunter
- Uniform group: solarum_longshot_hunter
- Silhouette family: p09_h3_c3_a5_w3_l3_wp10_sh0
- Intent: Armor_003 travel hunter에 Armor_005 bracer와 tan/blue cloth를 둔 원정 정찰병.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 2 / HairStyleId 12 / HairColorId 1
- SkinId 1 / EyeColorId 2 / FacialHairId 1 / BustSizeId 2
- Outfit A3/W10/S0 / Head 3 / Chest 3 / Arm 5 / Waist 3 / Leg 3
- WeaponId 10 (long bow) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #5A3D2B
- Head gear: #C0A078
- Chest / upper outfit: #C0A078
- Arm / sleeve: #8A8E92
- Waist / sash: #3F5F8F
- Leg / lower outfit: #6A5A45
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
