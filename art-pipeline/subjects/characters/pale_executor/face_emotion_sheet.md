---
slug: pale_executor--face_emotion_sheet
kind: face_emotion_sheet
subject_id: pale_executor
variant: face_emotion_sheet
refs:
- pale_executor
- pale_executor:portrait_full
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '2:1'
output_size: 3168x1568
chroma: '#FF00FF'
status: rendered
emotions:
- default
- smile
- serious
- shock
- anger
- sad
- cry
- quiet
---
# 백집행 (白執行) / Pale Executor — Face Emotion Sheet

## prompt 명세

```prompt
Create a 4-column x 2-row face emotion sprite sheet for 백집행 (白執行) / Pale Executor.

=== REFERENCE PRIORITY ===
1. The P09 anchor for pale_executor is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The pale_executor:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. No face-default ref is attached yet because this sheet creates it; use portrait_full for face and costume continuity.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 백집행 (白執行) / Pale Executor
- Pipeline subject_id: pale_executor
- Faction / job lane: pale_conclave / pale_executor
- Uniform group: pale_conclave_pale_executor
- Silhouette family: p09_h12_c12_a9_w12_l11_wp12_sh0
- Intent: Armor_012 formal executor에 robe arm/exile leg와 teal bow glow를 더한 망명 처형자.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 3 / HairStyleId 13 / HairColorId 5
- SkinId 1 / EyeColorId 4 / FacialHairId 3 / BustSizeId 2
- Outfit A12/W12/S0 / Head 12 / Chest 12 / Arm 9 / Waist 12 / Leg 11
- WeaponId 12 (cold marksman bow) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #D8D4C8
- Head gear: #B8B0C8
- Chest / upper outfit: #B8B0C8
- Arm / sleeve: #AFA79A
- Waist / sash: #4A405A
- Leg / lower outfit: #3B3745
- Weapon: #3B3745
- Shield: none in P09 preset


=== GRID SHEET LAYOUT ===
- Canvas: 3168 x 1568 PNG.
- 4 columns x 2 rows.
- Each cell: exactly 768 x 768 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: default, smile, serious, shock
  - row 1: anger, sad, cry, quiet

=== CELL CONTENT ===
- Face close-up only: top of hair visible, ears inside frame, neck/collar visible at bottom.
- Same character in all 8 cells: same hair, eye color, skin, face proportions, collar, and outfit colors.
- Frontal view with a subtle 5-10 degree turn only.
- Same zoom, same head height, same lighting, same line thickness across all cells.
- Clean 1-2 px dark outline around the visible head / hair / collar silhouette.

=== EXPRESSION SET ===
- default: neutral readable resting state that fits the role and faction.
- smile: restrained character-appropriate warmth, not a broad comedy grin.
- serious: focused, jaw set, eyes intent.
- shock: eyes widened and breath caught, controlled rather than theatrical.
- anger: controlled anger, narrowed eyes, tension in brow and mouth.
- sad: quiet grief, lowered gaze, no melodrama.
- cry: one restrained tear, dignity preserved.
- quiet: emotionally emptied stillness, distant eyes, almost no mouth movement.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No weapon, hands, UI, captions, labels, environment, shadows, or props.
- Do not change hairstyle, hair color, eye color, collar color, gender presentation, facial hair, or head gear between cells.
- Do not add scars, tattoos, extra ornaments, glasses, animal ears, halos, or jewelry unless visible in the P09 ref.
- No magenta tint on the character.
```
