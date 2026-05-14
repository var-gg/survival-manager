---
slug: reaver--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: reaver
variant: face_combat_state_sheet
refs:
- reaver
- reaver:portrait_full
- reaver:portrait_face_default
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
# 묵괴 (墨壞) / Grave Reaver — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 묵괴 (墨壞) / Grave Reaver.

=== REFERENCE PRIORITY ===
1. The P09 anchor for reaver is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The reaver:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 묵괴 (墨壞) / Grave Reaver
- Pipeline subject_id: reaver
- Faction / job lane: pale_conclave / grave_reaver
- Uniform group: pale_conclave_grave_reaver
- Silhouette family: p09_h11_c11_a4_w11_l5_wp5_sh0
- Intent: Armor_011 rogue에 leather arm과 chain leg를 섞은 ash/black/rust 묘역 순찰병.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 3 / HairStyleId 8 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 0 / BustSizeId 1
- Outfit A11/W5/S0 / Head 11 / Chest 11 / Arm 4 / Waist 11 / Leg 5
- WeaponId 5 (formal duelist sword) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #CFCAC0
- Head gear: #4A4648
- Chest / upper outfit: #4A4648
- Arm / sleeve: #6B5544
- Waist / sash: #3E3A36
- Leg / lower outfit: #5C5A58
- Weapon: #3B3745
- Shield: none in P09 preset


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
