---
slug: mirror_cantor--battle_stance_sheet
kind: battle_stance_sheet
subject_id: mirror_cantor
variant: battle_stance_sheet
refs:
- mirror_cantor
- mirror_cantor:portrait_full
- mirror_cantor:portrait_face_default
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
# 명음 (明音) / Mirror Cantor — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 명음 (明音) / Mirror Cantor.

=== REFERENCE PRIORITY ===
1. The P09 anchor for mirror_cantor is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The mirror_cantor:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 명음 (明音) / Mirror Cantor
- Pipeline subject_id: mirror_cantor
- Faction / job lane: lattice_order / mirror_cantor
- Uniform group: lattice_order_mirror_cantor
- Silhouette family: p09_h9_c9_a8_w9_l8_wp9_sh5
- Intent: Armor_009 cantor robe에 Armor_008 arm/leg와 mirror shield를 둔 crystal white/magenta/cyan 봉합자.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 2 / HairStyleId 9 / HairColorId 8
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 2
- Outfit A9/W9/S5 / Head 9 / Chest 9 / Arm 8 / Waist 9 / Leg 8
- WeaponId 9 (ceremonial staff) / ShieldId 5

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #2D1F3F
- Head gear: #D8D2E3
- Chest / upper outfit: #D8D2E3
- Arm / sleeve: #AFA7B8
- Waist / sash: #C7C0D8
- Leg / lower outfit: #4A3A5C
- Weapon: #D8D2E3
- Shield: #C7C0D8


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 9: ceremonial staff.
- ShieldId 5: render the shield exactly as P09 shows in every stance where the shield arm is visible.
- attack: staff thrust or sweeping strike, staff clearly in the correct hand, no sword or bow.
- guard: staff held across the body as a warding bar or barrier focus.
- cast: staff raised or planted with restrained faction-colored glow; avoid oversized spell effects.


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
