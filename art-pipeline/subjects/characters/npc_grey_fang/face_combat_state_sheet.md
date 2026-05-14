---
slug: npc_grey_fang--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: npc_grey_fang
variant: face_combat_state_sheet
refs:
- npc_grey_fang
- npc_grey_fang:portrait_full
- npc_grey_fang:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: 1.51:1
output_size: 2368x1568
chroma: '#FF00FF'
status: rendered
states:
- wounded
- stunned
- feared
- charmed
- pained
- downed
---
# 회조 (灰爪) / Grey Fang — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 회조 (灰爪) / Grey Fang.

=== REFERENCE PRIORITY ===
1. The P09 anchor for npc_grey_fang is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The npc_grey_fang:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 회조 (灰爪) / Grey Fang
- Pipeline subject_id: npc_grey_fang
- Faction / job lane: irisol / grey_fang_duelist
- Uniform group: irisol_grey_fang_duelist
- Silhouette family: p09_h0_c4_a11_w4_l3_wp1_sh0
- Intent: 피형제 leather baseline에 frost hair와 rogue arm/travel boots를 더한 분리파 결투가.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 1 / HairStyleId 2 / HairColorId 5
- SkinId 1 / EyeColorId 1 / FacialHairId 5 / BustSizeId 2
- Outfit A4/W1/S0 / Head 0 / Chest 4 / Arm 11 / Waist 4 / Leg 3
- WeaponId 1 (short sword / hunting blade) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #B0B3B0
- Head gear: read from REF
- Chest / upper outfit: #8B7A68
- Arm / sleeve: #6A625F
- Waist / sash: #5C5248
- Leg / lower outfit: #6F665C
- Weapon: #5B5A58
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
