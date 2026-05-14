---
slug: hero_aegis_sentinel--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: hero_aegis_sentinel
variant: face_combat_state_sheet
refs:
- hero_aegis_sentinel
- hero_aegis_sentinel:portrait_full
- hero_aegis_sentinel:portrait_face_default
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
# 방진 (方陣) / Aegis Sentinel — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 방진 (方陣) / Aegis Sentinel.

=== REFERENCE PRIORITY ===
1. The P09 anchor for hero_aegis_sentinel is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The hero_aegis_sentinel:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 방진 (方陣) / Aegis Sentinel
- Pipeline subject_id: hero_aegis_sentinel
- Faction / job lane: lattice_order / aegis_sentinel
- Uniform group: lattice_order_aegis_sentinel
- Silhouette family: p09_h0_c6_a6_w9_l6_wp5_sh5
- Intent: Head를 비워 회백 노년 hair/moustache를 보이고 plate+ritual sash+heraldic shield로 침묵의 결사 보초를 만든다.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 1 / HairStyleId 5 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 1 / BustSizeId 2
- Outfit A6/W5/S5 / Head 0 / Chest 6 / Arm 6 / Waist 9 / Leg 6
- WeaponId 5 (formal duelist sword) / ShieldId 5

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #C9C6BC
- Head gear: read from REF
- Chest / upper outfit: #9EA5B0
- Arm / sleeve: #8D96A0
- Waist / sash: #4B3A32
- Leg / lower outfit: #7E8792
- Weapon: #B8B0C8
- Shield: #D8D2E3


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
