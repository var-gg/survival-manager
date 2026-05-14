---
slug: hero_ember_runner--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: hero_ember_runner
variant: face_combat_state_sheet
refs:
- hero_ember_runner
- hero_ember_runner:portrait_full
- hero_ember_runner:portrait_face_default
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
# 연주 (燕走) / Ember Runner — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 연주 (燕走) / Ember Runner.

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
