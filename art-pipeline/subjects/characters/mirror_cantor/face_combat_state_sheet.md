---
slug: mirror_cantor--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: mirror_cantor
variant: face_combat_state_sheet
refs:
- mirror_cantor
- mirror_cantor:portrait_full
- mirror_cantor:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: 1.51:1
output_size: 2368x1568
chroma: '#FF00FF'
status: prompted
states:
- wounded
- stunned
- feared
- charmed
- pained
- downed
---
# 명음 (明音) / Mirror Cantor — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 명음 (明音) / Mirror Cantor.

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


=== GRID SHEET LAYOUT ===
- Canvas: 2368 x 1568 PNG.
- 3 columns x 2 rows.
- Each cell: exactly 768 x 768 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: wounded, stunned, feared
  - row 1: charmed, pained, downed

=== CELL CONTENT ===
- Face close-up only: top of hair visible, ears inside frame, neck/collar visible at bottom.
- Same character in all 6 cells: same identity, same hair, same eye color, same face proportions, same collar.
- Frontal view with a subtle 5-10 degree turn only.
- Same zoom and head height across all cells.
- Clean 1-2 px dark outline around the visible silhouette.

=== GAME STATE SET ===
- wounded: HP critical; small blood trace, sweat, focused pain, still alive and fighting.
- stunned: unfocused pupils, slack jaw, head slightly tilted, no gore.
- feared: sustained fear, wide eyes, cold sweat, not comedic panic.
- charmed: absent softened smile, unfocused gaze, very faint non-magenta violet/cyan iris hint only.
- pained: damage-over-time endurance, eyes squeezed or narrowed, clenched jaw, sweat.
- downed: recoverable HP=0 state, eyes closed or half-closed, limp but not dead, small blood trace.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No weapon, hands, UI, captions, labels, environment, shadows, or props.
- Do not change hairstyle, hair color, eye color, collar color, gender presentation, facial hair, or head gear between cells.
- No horror gore, exposed injury, corpse framing, or rolled-back eyes.
- No magenta tint on the character. Charmed effect must avoid #FF00FF/fuchsia.
```
