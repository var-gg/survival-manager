---
slug: warden--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: warden
variant: face_combat_state_sheet
refs:
- warden
- warden:portrait_full
- warden:portrait_face_default
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
# 철위 (鐵衛) / Iron Warden — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 철위 (鐵衛) / Iron Warden.

=== REFERENCE PRIORITY ===
1. The P09 anchor for warden is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The warden:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 철위 (鐵衛) / Iron Warden
- Pipeline subject_id: warden
- Faction / job lane: solarum / iron_warden
- Uniform group: solarum_iron_warden
- Silhouette family: p09_h6_c6_a6_w5_l6_wp3_sh5
- Intent: Armor_006 full plate에 field waist와 royal blue shield를 둔 veteran warden.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 2 / HairStyleId 5 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 4 / BustSizeId 2
- Outfit A6/W3/S5 / Head 6 / Chest 6 / Arm 6 / Waist 5 / Leg 6
- WeaponId 3 (regular military sword) / ShieldId 5

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #C0C0B8
- Head gear: #9AA0A6
- Chest / upper outfit: #9AA0A6
- Arm / sleeve: #8D949C
- Waist / sash: #4A5160
- Leg / lower outfit: #7A828C
- Weapon: #B0B4B8
- Shield: #4A6FA8


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
