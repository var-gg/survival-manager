---
slug: slayer--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: slayer
variant: face_combat_state_sheet
refs:
- slayer
- slayer:portrait_full
- slayer:portrait_face_default
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
# 서검 (誓劍) / Oath Slayer — Face Combat State Sheet

## prompt 명세

```prompt
Create a 3-column x 2-row face combat-state sprite sheet for 서검 (誓劍) / Oath Slayer.

=== REFERENCE PRIORITY ===
1. The P09 anchor for slayer is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The slayer:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 서검 (誓劍) / Oath Slayer
- Pipeline subject_id: slayer
- Faction / job lane: solarum / oath_slayer
- Uniform group: solarum_oath_slayer
- Silhouette family: p09_h0_c5_a12_w5_l5_wp4_sh0
- Intent: Head를 비워 black hair를 보이고 Armor_005 chain에 Armor_012 arm을 붙인 executor duelist.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 2 / HairStyleId 6 / HairColorId 1
- SkinId 1 / EyeColorId 2 / FacialHairId 0 / BustSizeId 1
- Outfit A5/W4/S0 / Head 0 / Chest 5 / Arm 12 / Waist 5 / Leg 5
- WeaponId 4 (oath execution sword) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #1A1718
- Head gear: #D1C7B2
- Chest / upper outfit: #8A8E92
- Arm / sleeve: #5F6570
- Waist / sash: #3E4652
- Leg / lower outfit: #5C6370
- Weapon: #B8B8BC
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
