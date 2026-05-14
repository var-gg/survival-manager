---
slug: marksman--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: marksman
variant: face_combat_state_sheet
refs:
- marksman
- marksman:portrait_full
- marksman:portrait_face_default
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
# 냉시 (冷矢) / Dread Marksman — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 냉시 (冷矢) / Dread Marksman.

=== REFERENCE PRIORITY ===
1. The P09 anchor for marksman is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The marksman:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 냉시 (冷矢) / Dread Marksman
- Pipeline subject_id: marksman
- Faction / job lane: pale_conclave / dread_marksman
- Uniform group: pale_conclave_dread_marksman
- Silhouette family: p09_h5_c5_a5_w9_l5_wp12_sh0
- Intent: Armor_005 chain에 Armor_009 sash와 cyan bow glow를 둔 cold precision archer.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 2 / HairStyleId 5 / HairColorId 5
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 2
- Outfit A5/W12/S0 / Head 5 / Chest 5 / Arm 5 / Waist 9 / Leg 5
- WeaponId 12 (cold marksman bow) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #D0D6D8
- Head gear: #7F8E94
- Chest / upper outfit: #7F8E94
- Arm / sleeve: #6F7E84
- Waist / sash: #B8B0A3
- Leg / lower outfit: #5C666A
- Weapon: #3E4652
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
